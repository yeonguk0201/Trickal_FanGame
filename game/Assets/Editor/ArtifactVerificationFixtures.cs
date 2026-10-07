using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Edit Mode objects shared by the artifact verifications. Components do not get their Awake call in Edit
    // Mode, so the fixtures call the ones the checks rely on.
    public static class ArtifactVerificationFixtures
    {
        public const float Tolerance = 0.001f;

        public static GameObject CreatePlayer(Transform parent, Vector2 position, params Type[] extraComponents)
        {
            GameObject player = new("Artifact Verification Player", new[]
            {
                typeof(CircleCollider2D), typeof(PlayerCombatEvents), typeof(PlayerInventory),
            }.Concat(extraComponents).ToArray());
            player.transform.SetParent(parent);
            player.transform.position = position;
            player.layer = LayerMask.NameToLayer("Player");
            Invoke(player.GetComponent<Health>(), "Awake");
            Invoke(player.GetComponent<PlayerSP>(), "Awake");
            Invoke(player.GetComponent<PlayerStats>(), "Awake");
            Invoke(player.GetComponent<PlayerInventory>(), "Awake");
            return player;
        }

        public static Health CreateEnemy(Transform parent, Vector2 position, float maxHealth)
        {
            GameObject enemy = new("Artifact Verification Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(KnockbackReceiver));
            enemy.transform.SetParent(parent);
            enemy.transform.position = position;
            enemy.layer = LayerMask.NameToLayer("Enemy");
            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            Health health = enemy.GetComponent<Health>();
            SerializedObject serialized = new(health);
            serialized.FindProperty("maxHealth").floatValue = maxHealth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Invoke(health, "Awake");
            return health;
        }

        public static Projectile Launch(Transform parent, Vector2 position, Vector2 velocity, Health owner,
            DamageContext damage, ProjectileHitEffects hitEffects = default, int pierces = 0,
            ProjectileBounceSettings bounce = default)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ErpinProjectileArtworkSetup.BasicPrefabPath);
            Assert(prefab != null && prefab.GetComponent<Projectile>() != null,
                "The player projectile Prefab is missing.");
            GameObject shot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            shot.transform.position = position;
            Projectile projectile = shot.GetComponent<Projectile>();
            projectile.Launch(velocity, owner, damage, pierces, configuredHitEffects: hitEffects,
                configuredBounce: bounce);
            return projectile;
        }

        public static void Hit(Projectile shot, Health target)
        {
            MethodInfo hit = typeof(Projectile).GetMethod("Hit", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(hit != null, "Projectile.Hit was renamed; update the artifact hit checks.");
            hit.Invoke(shot, new object[] { target.GetComponent<Collider2D>() });
        }

        public static ItemDefinition LoadItem(string itemId)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ArtifactSetupUtility.ItemPath(itemId));
            Assert(item != null && item.ItemId == itemId && item.IsValid,
                $"Run the item's setup first: {itemId} is missing.");
            return item;
        }

        public static RoomGraphAssembler LoadAssembler()
        {
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>(FindObjectsInactive.Include);
            Assert(assembler != null, "The verification needs the Game Scene assembler.");
            return assembler;
        }

        // Logs every active artifact whose name, effect or stack text does not fit the reward card, instead of
        // stopping at the first one like the Reward-2 fit check. Batch entry point for writing descriptions.
        public static void LogRewardCardOverflowsBatch()
        {
            UnityEngine.SceneManagement.Scene scene =
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
            TrickalFanGame.Frontend.ItemRewardCardView card = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TrickalFanGame.Frontend.ItemRewardSelectionView>(true))
                .Single().Cards[0];
            Health playerHealth = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerInventory>(true)).Single()
                .GetComponent<Health>();
            int overflows = 0;
            foreach (ItemDefinition definition in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                         .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                         .Where(definition => definition != null && definition.IsActive && !definition.IsSingleUse))
            {
                card.Bind(ItemRewardCandidate.ForItem(definition, Math.Max(0, definition.MaxStacks - 1)), playerHealth);
                foreach ((TMPro.TMP_Text text, string label) in new[]
                         {
                             (card.NameText, "name"), (card.EffectText, "effect"), (card.StackText, "stack"),
                         })
                {
                    text.ForceMeshUpdate(true, true);
                    Rect rect = text.rectTransform.rect;
                    float needed = text.GetPreferredValues(text.text, rect.width, 0f).y;
                    if (needed <= rect.height + 0.5f) continue;
                    overflows++;
                    Debug.Log($"[card-fit] {definition.ItemId} {label} needs {needed:0.#} px of {rect.height:0.#} " +
                              $"at width {rect.width:0.#}: {text.text}");
                }
            }

            Debug.Log($"[card-fit] done: {overflows} overflow(s).");
        }

        // Instantiates the three floor pickup Prefabs and checks the size their artwork is really drawn at: an
        // item or spell at UserArtwork.PickupWorldSize, a heart and an SP capsule at the same
        // UserArtwork.ResourcePickupWorldSize.
        public static void VerifyPickupArtworkSizesBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            (string Prefab, string ItemId, float Expected)[] cases =
            {
                ("ItemPickup", "artifact-life-gem", TrickalFanGame.Frontend.UserArtwork.PickupWorldSize),
                ("SingleUseItemPickup", "single-spell-catch-that-one",
                    TrickalFanGame.Frontend.UserArtwork.SpellPickupWorldSize),
                ("HealthPickup", null, TrickalFanGame.Frontend.UserArtwork.ResourcePickupWorldSize),
                ("SPPickup", null, TrickalFanGame.Frontend.UserArtwork.ResourcePickupWorldSize),
            };
            foreach ((string prefabName, string itemId, float expected) in cases)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{prefabName}.prefab");
                Assert(prefab != null, $"The pickup Prefab is missing: {prefabName}.");
                GameObject instance = Object.Instantiate(prefab);
                try
                {
                    if (instance.TryGetComponent(out ItemPickup itemPickup)) itemPickup.Configure(LoadItem(itemId));
                    else if (instance.TryGetComponent(out SingleUseItemPickup spellPickup))
                        spellPickup.Configure(LoadItem(itemId), "verification", false);
                    else
                        foreach (MonoBehaviour behaviour in instance.GetComponents<MonoBehaviour>())
                            Invoke(behaviour, "Awake");

                    SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>();
                    float drawn = TrickalFanGame.Frontend.PickupArtworkFit.DrawnWorldSize(renderer);
                    Debug.Log($"[pickup-size] {prefabName}: drawn {drawn:0.###} of {expected:0.###}, root scale " +
                              $"{instance.transform.lossyScale.x:0.###}, renderer on " +
                              $"{(renderer.transform == instance.transform ? "root" : renderer.name)} scale " +
                              $"{renderer.transform.lossyScale.x:0.###}, size {renderer.size}, bounds " +
                              $"{renderer.bounds.size}, sprite {renderer.sprite.name} {renderer.sprite.bounds.size}");
                    Assert(Mathf.Approximately(instance.transform.localScale.x, prefab.transform.localScale.x),
                        $"{prefabName} must keep its Prefab scale (and so its collider size), but is " +
                        $"{instance.transform.localScale.x} instead of {prefab.transform.localScale.x}.");
                    Assert(Near(drawn, expected),
                        $"{prefabName} artwork must be drawn {expected} wide, but is {drawn}.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            Debug.Log("[pickup-size] verification passed.");
        }

        public static void Invoke(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?
                .Invoke(target, null);
        }

        public static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) <= Tolerance;

        public static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
