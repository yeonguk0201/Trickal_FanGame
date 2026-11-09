using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Number-0: critical, tick and blocked ("무적 0") damage numbers on top of the HUD-6 world numbers.
    public static class Number0Verification
    {
        [MenuItem("Trickal Fan Game/Beta/Verify Number-0 Damage Numbers")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            WorldCombatNumberPool scenePool = FindAll<WorldCombatNumberPool>(scene).Single();
            Assert(scenePool.Template is TextMeshPro, "Damage numbers must stay world-space TextMeshPro.");
            Assert(scenePool.Template.font == AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath),
                "Damage numbers must use the frontend TMP font.");
            Assert(scenePool.Template.font.HasCharacters("0123456789" + WorldCombatNumberPool.BlockedLabel),
                "The frontend font is missing digits or the blocked label glyphs.");
            Assert(scenePool.CriticalScale >= 1.2f && scenePool.PeriodicScale < 1f,
                "Critical numbers must be larger and tick numbers smaller than normal hits.");
            VerifyOutlineMaterial(scenePool.NumberMaterial, Number0Setup.NumberMaterialPath);
            VerifyOutlineMaterial(scenePool.ShadowMaterial, Number0Setup.ShadowMaterialPath);
            VerifyOutlineMaterial(scenePool.BlockedLabelMaterial, Number0Setup.BlockedLabelMaterialPath);
            Assert(scenePool.NumberMaterial.GetTexture(ShaderUtilities.ID_MainTex) ==
                   scenePool.Template.fontSharedMaterial.GetTexture(ShaderUtilities.ID_MainTex),
                "The number material must sample the frontend font atlas.");
            VerifyBurstSprites();
            VerifyRuntime(scenePool);
            Debug.Log("Number-0 verification passed: critical numbers are larger, recoloured and backed by varied " +
                      "spiky shapes; ticks are smaller; every number shares the outline and shadow; blocked hits show 무적 0 once per interval; " +
                      "pooled numbers reset between kinds.");
        }

        public static void SetupAndVerifyBatch()
        {
            Number0Setup.Setup();
            string materialGuid = AssetDatabase.AssetPathToGUID(Number0Setup.NumberMaterialPath);
            Number0Setup.Setup();
            Assert(materialGuid == AssetDatabase.AssetPathToGUID(Number0Setup.NumberMaterialPath),
                "Rerunning Number-0 setup must keep the same material asset.");
            Verify();
        }

        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week13Hud6Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Number-0 regression batch passed: Number-0, HUD-6 and the development panel.");
        }

        // Without OUTLINE_ON the TMP SDF shader skips the outline entirely, whatever the width.
        private static void VerifyOutlineMaterial(Material material, string path)
        {
            Assert(material != null && AssetDatabase.GetAssetPath(material) == path,
                "Run Number-0 setup: the pool needs the saved outline material " + path + ".");
            Assert(material.IsKeywordEnabled(ShaderUtilities.Keyword_Outline) &&
                   material.GetFloat(ShaderUtilities.ID_OutlineWidth) >= 0.3f,
                "The outline material must enable OUTLINE_ON with a visible width: " + path + ".");
        }

        private static void VerifyBurstSprites()
        {
            Sprite[] sprites = Enumerable.Range(0, CriticalBurstSprites.VariantCount)
                .Select(CriticalBurstSprites.Get).ToArray();
            Assert(sprites.All(sprite => sprite != null && sprite.texture != null),
                "Every critical burst shape must have a sprite.");
            int[] coverage = sprites.Select(sprite => sprite.texture.GetPixels32().Count(pixel => pixel.a > 127))
                .ToArray();
            int size = sprites[0].texture.width * sprites[0].texture.height;
            Assert(coverage.All(count => count > size / 6 && count < size * 2 / 3),
                "Critical burst shapes must be spiky stars, neither empty nor filled squares.");
            Color32 corner = sprites[0].texture.GetPixel(0, 0);
            Assert(corner.a == 0, "Critical burst corners must be transparent.");
            Color32 centre = sprites[0].texture.GetPixel(64, 64);
            Assert(centre.Equals(CriticalBurstSprites.FillColor),
                "The inside of a critical burst must be the lighter orange.");
            Assert(sprites[0].texture.GetPixels32().Any(pixel => pixel.a == 255 &&
                    pixel.Equals(CriticalBurstSprites.EdgeColor)),
                "A critical burst must have a darker orange rim.");
            Assert(coverage.Distinct().Count() >= CriticalBurstSprites.VariantCount - 1,
                "Critical burst shapes must differ from each other.");
        }

        private static void VerifyRuntime(WorldCombatNumberPool scenePool)
        {
            GameObject root = new("Number-0 Runtime Verification");
            WorldCombatNumberPool pool = Object.Instantiate(scenePool.gameObject, root.transform)
                .GetComponent<WorldCombatNumberPool>();
            GameObject enemyObject = new("Enemy", typeof(TestEnemy));
            enemyObject.transform.SetParent(root.transform);
            GameObject playerObject = new("Player", typeof(PlayerMovement));
            playerObject.transform.SetParent(root.transform);
            try
            {
                Health enemy = enemyObject.GetComponent<Health>();
                Health player = playerObject.GetComponent<Health>();
                enemy.ResetHealth();
                player.ResetHealth();
                pool.RefreshBindings();

                enemy.TakeDamage(new DamageContext(null, DamageSourceType.Unknown, 2f, criticalChance: 1f,
                    criticalDamageMultiplier: 2f, criticalRollOverride: 0f));
                Assert(pool.ActiveCount == 1 && pool.GetActiveKind(0) == WorldCombatNumberKind.Critical &&
                       pool.GetActiveText(0) == "4", "A critical hit must show one critical number with its damage.");
                SpriteRenderer burst = pool.GetActiveBurst(0);
                Assert(burst != null && burst.gameObject.activeSelf && burst.sprite != null &&
                       burst.sortingOrder < pool.Template.GetComponent<Renderer>().sortingOrder,
                    "A critical number must have a spiky shape drawn behind it.");
                Assert(pool.GetActiveTextMaterial(0) == pool.NumberMaterial,
                    "A critical number must use the outlined number material.");
                TMP_Text shadow = pool.GetActiveShadow(0);
                Renderer shadowRenderer = shadow != null ? shadow.GetComponent<Renderer>() : null;
                Assert(shadow != null && shadow.gameObject.activeSelf && shadow.text == "4" &&
                       shadow.fontSharedMaterial == pool.ShadowMaterial &&
                       pool.NumberCharacterSpacing < 0f && Mathf.Approximately(shadow.characterSpacing, pool.NumberCharacterSpacing) &&
                       burst.sortingOrder < shadowRenderer.sortingOrder &&
                       shadowRenderer.sortingOrder < pool.Template.GetComponent<Renderer>().sortingOrder,
                    "A critical number must have a shadow copy between the star and the number.");
                Vector3 shadowShift = shadow.transform.position - shadow.transform.parent.position;
                Assert(shadowShift.x > 0f && shadowShift.y < 0f &&
                       Mathf.Abs(shadowShift.x - pool.ShadowOffset.x) < 0.001f,
                    "The critical shadow must sit slightly right and below by its world offset.");
                Color criticalColor = pool.GetActiveTextColor(0);

                enemy.TakeDamage(1f);
                Assert(pool.GetActiveKind(1) == WorldCombatNumberKind.Normal &&
                       pool.GetActiveScale(1) < pool.GetActiveScale(0) && pool.GetActiveTextColor(1) == criticalColor &&
                       pool.GetActiveTextMaterial(1) == pool.NumberMaterial &&
                       pool.GetActiveShadow(1).gameObject.activeInHierarchy,
                    "A normal hit must share the critical colour, outline and shadow, and only be smaller.");
                Assert(pool.GetActiveBurst(1) == null || !pool.GetActiveBurst(1).gameObject.activeSelf,
                    "A normal hit must not show the critical shape.");

                enemy.TakeDamage(new DamageContext(null, DamageSourceType.Unknown, 1f, criticalChance: 1f,
                    criticalDamageMultiplier: 2f, criticalRollOverride: 0f, deliveryType: DamageDeliveryType.Periodic));
                Assert(pool.GetActiveKind(2) == WorldCombatNumberKind.Periodic && pool.GetActiveText(2) == "1" &&
                       pool.GetActiveScale(2) < 1f,
                    "Ticks never crit and must show a smaller tick number.");

                int beforeBlocked = pool.ActiveCount;
                float healthBefore = enemy.CurrentHealth;
                enemy.SetInvulnerable(true);
                enemy.TakeDamage(5f);
                Assert(pool.ActiveCount == beforeBlocked + 1 && Mathf.Approximately(enemy.CurrentHealth, healthBefore),
                    "A hit on an invulnerable enemy must deal nothing and show one number.");
                int blocked = pool.ActiveCount - 1;
                TMP_Text label = pool.GetActiveLabel(blocked);
                Assert(pool.GetActiveKind(blocked) == WorldCombatNumberKind.Blocked && pool.GetActiveText(blocked) == "0" &&
                       label != null && label.gameObject.activeSelf && label.text == WorldCombatNumberPool.BlockedLabel &&
                       label.fontSharedMaterial == pool.BlockedLabelMaterial &&
                       Mathf.Approximately(label.characterSpacing, pool.BlockedLabelCharacterSpacing) &&
                       pool.BlockedLabelCharacterSpacing < pool.NumberCharacterSpacing,
                    "A blocked hit must show 무적 over 0.");
                enemy.TakeDamage(5f);
                enemy.TakeDamage(new DamageContext(null, DamageSourceType.Unknown, 5f,
                    deliveryType: DamageDeliveryType.Periodic));
                Assert(pool.ActiveCount == beforeBlocked + 1,
                    "Repeated blocked hits within the interval and blocked ticks must not stack more labels.");
                pool.Tick(0.4f);
                enemy.TakeDamage(5f);
                Assert(pool.ActiveCount == beforeBlocked + 2, "A blocked hit after the interval must show again.");
                enemy.SetInvulnerable(false);

                player.SetInvulnerable(true);
                player.TakeDamage(1f);
                Assert(pool.ActiveCount == beforeBlocked + 2, "Blocked hits on the player must not show numbers.");

                int created = pool.TotalInstanceCount;
                pool.Tick(pool.Lifetime + 0.01f);
                Assert(pool.ActiveCount == 0 && pool.PooledCount == created,
                    "Every kind of number must return to the pool after its lifetime.");
                enemy.ResetHealth();
                for (int i = 0; i < created; i++) enemy.TakeDamage(0.1f);
                Assert(pool.TotalInstanceCount == created, "Reused numbers must not create new instances.");
                for (int i = 0; i < pool.ActiveCount; i++)
                {
                    Assert(pool.GetActiveKind(i) == WorldCombatNumberKind.Normal &&
                           Mathf.Approximately(pool.GetActiveScale(i), 1f) &&
                           (pool.GetActiveBurst(i) == null || !pool.GetActiveBurst(i).gameObject.activeSelf) &&
                           pool.GetActiveTextMaterial(i) == pool.NumberMaterial &&
                           (pool.GetActiveLabel(i) == null || !pool.GetActiveLabel(i).gameObject.activeSelf),
                        "A reused number must drop the critical shape, blocked label and size of its last kind.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(rootObject => rootObject.GetComponentsInChildren<T>(true)).ToArray();

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
