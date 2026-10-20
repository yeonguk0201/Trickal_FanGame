using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // 광기의 가면 floor field (DamageAuraField) and its link to PlayerDamageAura. The aura's damage rules stay in
    // PhaseGDamageAuraVerification.
    public static class DamageAuraFieldVerification
    {
        public const float Radius = 2.5f;

        [MenuItem("Trickal Fan Game/Effects/Verify Damage Aura Field With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            PhaseGDamageAuraVerification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Damage aura field regressions passed: field, Phase G-6 aura rules and development panel.");
        }

        [MenuItem("Trickal Fan Game/Effects/Verify Damage Aura Field")]
        public static void Verify()
        {
            Material material = CombatEffectArtwork.Material("DamageAura");
            Require(material != null && !ShaderUtil.ShaderHasError(material.shader),
                "The damage aura shader is missing or failed compilation.");
            Random.InitState(4141);
            Random.State randomBefore = Random.state;
            VerifyField();
            Require(Random.state.Equals(randomBefore), "The damage aura field must not consume UnityEngine.Random.");
            VerifyPlayerLink();
            Debug.Log("Damage aura field verification passed.");
        }

        private static void VerifyField()
        {
            var owner = new GameObject("Damage aura verification owner");
            try
            {
                // A scaled, flipped owner must still get a circle of exactly the damage radius.
                owner.transform.localScale = new Vector3(-1.5f, 1.5f, 1f);
                DamageAuraField aura = DamageAuraField.Create(owner.transform, 0);
                Require(aura != null && DamageAuraField.Create(owner.transform, 0) == aura &&
                        owner.GetComponentsInChildren<MeshRenderer>(true).Length == 1,
                    "The field must be created once per owner.");
                Require(!aura.Field.enabled, "The field must stay hidden until it is rendered.");
                aura.RenderAt(1f, Radius, 1, 1f, -1f);
                Require(aura.Field.enabled && aura.Field.sortingOrder == DamageAuraField.SortingOrder &&
                        DamageAuraField.SortingOrder > -100 && DamageAuraField.SortingOrder < GroundShadowOrder(),
                    "The field must draw over the room floor and under ground shadows and bodies.");
                Bounds bounds = aura.Field.bounds;
                Require(Mathf.Abs(bounds.extents.x - Radius) < 1e-3f && Mathf.Abs(bounds.extents.y - Radius) < 1e-3f &&
                        Vector2.Distance(bounds.center, owner.transform.position) < 1e-4f,
                    $"The field must be a circle of the damage radius on the owner ({bounds.extents}).");
                Require(DamageAuraField.Wobble > 0.03f && DamageAuraField.Wobble < 0.2f,
                    "The edge must wobble visibly but stay close to the damage radius.");
                Require(aura.PulseProgress < 0f, "No tick ring may show without a tick.");
                aura.RenderAt(1f, Radius, 3, 1f, DamageAuraField.PulseDuration * 0.5f);
                Require(Mathf.Abs(aura.PulseProgress - 0.5f) < 1e-4f, "The tick ring must follow the time since the tick.");
                aura.RenderAt(1f, Radius, 3, 1f, DamageAuraField.PulseDuration);
                Require(aura.PulseProgress < 0f, "The tick ring must end after its duration.");
                aura.RenderAt(1f, Radius, 1, 0f, -1f);
                Require(!aura.Field.enabled, "A field at zero opacity must be hidden.");
                aura.RenderAt(1f, 0f, 1, 1f, -1f);
                Require(!aura.Field.enabled, "A field without radius must be hidden.");
                aura.Stop();
                Require(owner.transform.childCount == 0, "Stopping the field must remove it.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static void VerifyPlayerLink()
        {
            ItemDefinition mask = AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(item => item != null && item.ItemId == "item-14");
            Require(mask != null, "광기의 가면 (item-14) must exist.");
            var runObject = new GameObject("Damage aura verification Run");
            var player = new GameObject("Damage aura verification player",
                typeof(SpriteRenderer), typeof(Health), typeof(PlayerCombatEvents));
            try
            {
                RunSession run = runObject.AddComponent<RunSession>();
                Health health = player.GetComponent<Health>();
                typeof(Health).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(health, null);
                PlayerDamageAura aura = player.AddComponent<PlayerDamageAura>();
                aura.BindRunSession(run);
                // Only the field calls are covered: creating the test Run and player is not part of the effect.
                Random.InitState(5151);
                Random.State randomBefore = Random.state;
                aura.UpdateField(0f, 1f);
                Require(aura.Field == null, "No field may appear before the mask is acquired.");
                Require(aura.AddAttackDamageStack(0.2f, Radius, 1f), "The mask stack must apply.");
                aura.UpdateField(0f, DamageAuraField.FadeSeconds * 0.5f);
                Require(aura.Field != null && aura.Field.Field.enabled &&
                        Mathf.Abs(aura.Field.Opacity - 0.5f) < 1e-3f &&
                        aura.Field.transform.parent == player.transform,
                    "The field must fade in under the player once the mask is held.");
                aura.UpdateField(0.2f, 1f);
                Require(Mathf.Approximately(aura.Field.Opacity, 1f) && Mathf.Abs(aura.Field.DrawnRadius - aura.Radius) < 1e-4f,
                    "The field must show the aura's damage radius.");
                Require(aura.Tick(aura.NextTickTime), "The aura must tick at its schedule.");
                aura.UpdateField(aura.LastTickTime + 0.1f, 0.016f);
                Require(aura.Field.PulseProgress > 0f && aura.Field.PulseProgress < 1f,
                    "A damage tick must start the tick ring.");
                Require(Random.state.Equals(randomBefore), "The damage aura field must not consume UnityEngine.Random.");
                health.TakeDamage(1000f);
                Require(health.IsDead, "The verification player must die from the lethal hit.");
                aura.UpdateField(5f, 1f);
                Require(!aura.Field.Field.enabled, "The field must disappear when the player dies.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(runObject);
            }
        }

        private static int GroundShadowOrder() => TrickalFanGame.Frontend.GroundShadow.FloorSortingOrder;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException("[DamageAuraFieldVerification] " + message);
        }
    }
}
