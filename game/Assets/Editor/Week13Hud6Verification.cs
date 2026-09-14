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
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud6Verification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-6 World Combat Numbers")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            VerifyRuntimeState(scene);
            Debug.Log("Week 13 HUD-6 verification passed: enemy-only damage numbers, regular-enemy health bars, rise/fade lifetime, simultaneous scatter, runtime binding, and pool reuse.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Hud5Verification.SetupAndVerifyBatch();
            Week13Hud6Setup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud6Setup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-6 setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-6 setup changed the Game Scene GUID.");
            Verify();
            Week13Hud5Verification.ValidateScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Week 13 HUD-6 batch verification passed: setup twice, stable Scene GUID, and HUD-5 regression.");
        }

        public static void ValidateScene(Scene scene)
        {
            WorldCombatNumberPool[] pools = FindAll<WorldCombatNumberPool>(scene);
            Assert(pools.Length == 1, "Game Scene requires exactly one world combat number pool.");
            WorldCombatNumberPool pool = pools[0];
            Assert(pool.gameObject.name == Week13Hud6Setup.RootName && pool.Template != null,
                "HUD-6 root or template reference is missing.");
            Assert(pool.Template.transform.parent == pool.transform && !pool.Template.gameObject.activeSelf,
                "HUD-6 template must be an inactive child of its pool.");
            Assert(pool.Template is TextMeshPro && pool.Template.GetComponent<MeshRenderer>() != null,
                "HUD-6 numbers must be world-space TextMeshPro objects.");
            Assert(pool.Lifetime >= 0.6f && pool.Lifetime <= 1f && pool.RiseDistance > 0f &&
                pool.HorizontalScatter > 0f,
                "HUD-6 lifetime, rise, and simultaneous-number scatter are outside the UI specification.");
            EnemyWorldHealthBarView barTemplate = pool.HealthBarTemplate;
            Assert(barTemplate != null && barTemplate.transform.parent == pool.transform &&
                !barTemplate.gameObject.activeSelf,
                "The regular-enemy health bar template must be an inactive child of the HUD-6 root.");
            Assert(barTemplate.GetComponent<Canvas>()?.renderMode == RenderMode.WorldSpace &&
                barTemplate.CanvasGroup != null && barTemplate.HealthFill != null,
                "The enemy health bar requires a world-space canvas, non-blocking group, and fill image.");
            Assert(barTemplate.HealthFill.sprite != null && barTemplate.HealthFill.type == Image.Type.Filled &&
                barTemplate.HealthFill.fillMethod == Image.FillMethod.Horizontal,
                "The enemy health bar must have a Sprite so Unity renders its horizontal fill amount.");
        }

        private static void VerifyRuntimeState(Scene scene)
        {
            GameObject root = new("HUD-6 Runtime Verification");
            WorldCombatNumberPool scenePool = FindAll<WorldCombatNumberPool>(scene).Single();
            WorldCombatNumberPool pool = Object.Instantiate(scenePool.gameObject, root.transform)
                .GetComponent<WorldCombatNumberPool>();
            GameObject enemyObject = new("Enemy", typeof(TestEnemy));
            enemyObject.transform.SetParent(root.transform);
            enemyObject.transform.position = new Vector3(3f, 2f, 0f);
            GameObject playerObject = new("Player", typeof(PlayerMovement));
            playerObject.transform.SetParent(root.transform);
            playerObject.transform.position = new Vector3(-2f, 1f, 0f);
            GameObject bossObject = new("Boss", typeof(BossController));
            bossObject.transform.SetParent(root.transform);
            bossObject.transform.position = new Vector3(1f, -2f, 0f);
            try
            {
                Health enemy = enemyObject.GetComponent<Health>();
                Health player = playerObject.GetComponent<Health>();
                Health boss = bossObject.GetComponent<Health>();
                enemy.ResetHealth();
                player.ResetHealth();
                boss.ResetHealth();
                pool.Configure(pool.Template, pool.HealthBarTemplate, 0.8f, 0.65f, 0.18f);
                pool.RefreshBindings();

                enemy.TakeDamage(2.25f);
                Assert(pool.ActiveCount == 1 && pool.GetActiveText(0) == "3",
                    "A regular enemy hit must create one rounded-up damage number.");
                EnemyWorldHealthBarView bar = enemyObject.GetComponentInChildren<EnemyWorldHealthBarView>(true);
                Assert(bar != null && bar.IsVisible && bar.ObservedHealth == enemy &&
                    Mathf.Approximately(bar.HealthFill.fillAmount, 0.775f),
                    "The first regular-enemy hit must create and show a live world health bar.");
                Vector3 first = pool.GetActiveStartPosition(0);
                Assert(first.y > enemyObject.transform.position.y && first.x < enemyObject.transform.position.x,
                    "The first enemy number must start above and horizontally offset from its target.");
                Assert(first.y > bar.transform.position.y,
                    "Damage numbers must start above the regular-enemy health bar instead of covering it.");

                player.TakeDamage(1f);
                player.Heal(0.5f);
                Assert(pool.ActiveCount == 1 &&
                    playerObject.GetComponentInChildren<EnemyWorldHealthBarView>(true) == null,
                    "Player damage and healing must not create world numbers or enemy health bars.");

                enemy.TakeDamage(1f);
                Vector3 second = pool.GetActiveStartPosition(1);
                Assert(pool.ActiveCount == 2 && second.x > enemyObject.transform.position.x,
                    "Simultaneous enemy numbers must alternate their horizontal start offset.");
                enemy.Heal(100f);
                Assert(!bar.IsVisible && Mathf.Approximately(bar.HealthFill.fillAmount, 1f),
                    "A regular-enemy health bar must hide again at full health.");
                enemy.TakeDamage(1f);
                Assert(bar.IsVisible && enemyObject.GetComponentsInChildren<EnemyWorldHealthBarView>(true).Length == 1,
                    "A later hit must reuse the existing regular-enemy health bar.");

                int created = pool.TotalInstanceCount;
                pool.Tick(pool.Lifetime + 0.01f);
                Assert(pool.ActiveCount == 0 && pool.PooledCount == created,
                    "Combat numbers must disappear within their lifetime and return to the pool.");
                boss.TakeDamage(1f);
                Assert(pool.ActiveCount == 1 && pool.TotalInstanceCount == created,
                    "A boss hit must reuse a pooled number instead of instantiating another.");
                Assert(bossObject.GetComponentInChildren<EnemyWorldHealthBarView>(true) == null,
                    "Bosses must use the dedicated lower HUD without an overhead health bar.");
                enemy.TakeDamage(enemy.CurrentHealth);
                Assert(!bar.IsVisible, "A defeated regular enemy must hide its health bar immediately.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(rootObject => rootObject.GetComponentsInChildren<T>(true)).ToArray();

        private static int CountTransforms(Scene scene) => scene.GetRootGameObjects()
            .Sum(rootObject => rootObject.GetComponentsInChildren<Transform>(true).Length);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
