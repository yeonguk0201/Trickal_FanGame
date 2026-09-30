using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week20Special2Verification
    {
        [MenuItem("Trickal Fan Game/Week 20/Setup and Verify Special-2 Player Bomb")]
        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week20Special2Setup.Setup();
            string prefabGuid = AssetDatabase.AssetPathToGUID(Week20Special2Setup.PlacedBombPrefabPath);
            Week20Special2Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Special-2 setup changed the Game Scene GUID.");
            Assert(!string.IsNullOrWhiteSpace(prefabGuid) &&
                   prefabGuid == AssetDatabase.AssetPathToGUID(Week20Special2Setup.PlacedBombPrefabPath),
                "Special-2 setup changed the placed-bomb Prefab GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 20/Verify Special-2 Player Bomb")]
        public static void Verify()
        {
            ValidatePrefabAndSceneBinding();
            ValidatePlacementAndExplosion();
            Debug.Log("Special-2 verification passed: F-bound placement consumes exactly one bomb, blocks duplicate " +
                      "placement during the 0.75s fuse, applies one 30-damage enemy hit and one-heart self damage " +
                      "inside radius 2.5, ignores targets outside, destroys each obstacle once with persisted state, " +
                      "and rejects use while blocked, paused by action state, empty, dead, or after Run end.");
        }

        private static void ValidatePrefabAndSceneBinding()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special2Setup.PlacedBombPrefabPath);
            PlacedBomb bomb = prefab != null ? prefab.GetComponent<PlacedBomb>() : null;
            Assert(prefab != null && bomb != null && prefab.GetComponents<PlacedBomb>().Length == 1 &&
                   prefab.GetComponents<SpriteRenderer>().Length == 1 &&
                   prefab.GetComponents<Collider2D>().Length == 0 &&
                   prefab.GetComponents<Rigidbody2D>().Length == 0 &&
                   prefab.GetComponents<RunResourcePickup>().Length == 0,
                "PlacedBomb Prefab must be a single non-pickup visual without physics collisions.");
            Assert(Mathf.Approximately(bomb.FuseDuration, 0.75f) &&
                   Mathf.Approximately(bomb.ExplosionRadius, 2.5f) &&
                   Mathf.Approximately(bomb.EnemyDamage, 30f) &&
                   Mathf.Approximately(bomb.SelfDamage, 2f) &&
                   bomb.EnemyLayers.value == LayerMask.GetMask("Enemy"),
                "PlacedBomb Prefab values or Enemy layer mask drifted from the Special-2 contract.");

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerBombController[] controllers = Object.FindObjectsByType<PlayerBombController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert(controllers.Length == 1 && controllers[0].gameObject.scene == scene &&
                   controllers[0].Progress != null && controllers[0].BombPrefab == bomb,
                "Game Scene must bind one PlayerBombController to RunProgress and PlacedBomb Prefab.");
        }

        private static void ValidatePlacementAndExplosion()
        {
            GameObject root = new("Special-2 explosion verification");
            GameObject player = new("Player", typeof(Health), typeof(PlayerActionState));
            player.transform.SetParent(root.transform);
            GameObject enemy = CreateHealthTarget(root.transform, "Enemy", Vector2.right, 100f, "Enemy", true);
            GameObject farEnemy = CreateHealthTarget(root.transform, "Far Enemy", Vector2.right * 3.1f, 100f,
                "Enemy", false);
            GameObject obstacleObject = new("Obstacle", typeof(BoxCollider2D), typeof(SpriteRenderer),
                typeof(DestructibleObstacle));
            obstacleObject.transform.SetParent(root.transform);
            obstacleObject.transform.position = Vector2.left;
            obstacleObject.layer = LayerMask.NameToLayer("Environment");
            PlacedBomb placed = null;
            try
            {
                Health playerHealth = player.GetComponent<Health>();
                InvokeLifecycle(playerHealth, "Awake");
                playerHealth.EnableHealthUnits();
                PlayerActionState actionState = player.GetComponent<PlayerActionState>();
                RunProgress progress = root.AddComponent<RunProgress>();
                PlayerBombController controller = player.AddComponent<PlayerBombController>();
                PlacedBomb prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Week20Special2Setup.PlacedBombPrefabPath).GetComponent<PlacedBomb>();
                controller.Configure(progress, prefab);

                RoomRunState roomState = new("special-2-room");
                DestructibleObstacle obstacle = obstacleObject.GetComponent<DestructibleObstacle>();
                obstacle.Configure("bomb-obstacle", DestructibleObstacle.DefaultRequiredHits, null,
                    obstacleObject.GetComponent<SpriteRenderer>());
                obstacle.Bind(roomState, 777, root.transform, progress);
                Physics2D.SyncTransforms();

                Assert(!controller.TryPlaceBomb(10f), "An empty bomb wallet must reject placement.");
                Assert(progress.TryAddResource(RunResourceType.Bomb, 2) == 2 && controller.TryPlaceBomb(10f),
                    "A normal player with bombs must place one at the requested time.");
                placed = controller.PendingBomb;
                Assert(placed != null && progress.GetResourceCount(RunResourceType.Bomb) == 1 &&
                       Mathf.Approximately(placed.ExplodeAt, 10.75f),
                    "Placement must consume exactly one bomb and schedule the 0.75-second fuse once.");
                Assert(!controller.TryPlaceBomb(10.1f) && progress.GetResourceCount(RunResourceType.Bomb) == 1,
                    "A pending bomb must block duplicate placement and consumption.");
                Assert(!placed.Tick(10.749f) && !placed.HasExploded,
                    "A bomb must remain armed before the fuse boundary.");

                Health enemyHealth = enemy.GetComponent<Health>();
                Health farHealth = farEnemy.GetComponent<Health>();
                float playerBefore = playerHealth.CurrentHealth;
                Assert(placed.Tick(10.75f) && placed.HasExploded && controller.PendingBomb == null,
                    "The bomb must explode exactly at the fuse boundary and release the placement lock.");
                Assert(Mathf.Approximately(enemyHealth.CurrentHealth, 70f) &&
                       Mathf.Approximately(farHealth.CurrentHealth, 100f),
                    "Explosion must deal 30 once inside radius 2.5 and ignore a target outside it.");
                Assert(Mathf.Approximately(playerHealth.CurrentHealth, playerBefore - 2f),
                    "Explosion self-damage must remove exactly one heart (2 health units).");
                Assert(obstacle.IsBroken && roomState.IsObstacleDestroyed("bomb-obstacle"),
                    "Explosion must destroy an obstacle immediately and persist its destroyed state.");
                Assert(!placed.ApplyExplosion() && Mathf.Approximately(enemyHealth.CurrentHealth, 70f) &&
                       Mathf.Approximately(playerHealth.CurrentHealth, playerBefore - 2f),
                    "Repeated explosion callbacks must not apply damage or obstacle destruction twice.");

                actionState.SetRewardSelectionBlocked(true);
                Assert(!controller.TryPlaceBomb(11f) && progress.GetResourceCount(RunResourceType.Bomb) == 1,
                    "Reward-selection input blocking must reject bomb use without consuming it.");
                actionState.SetRewardSelectionBlocked(false);
                Assert(actionState.TryBeginUltimate(Vector2.right, 3f) &&
                       !controller.TryPlaceBomb(11f) && progress.GetResourceCount(RunResourceType.Bomb) == 1,
                    "Ultimate action state must reject bomb use without consuming it.");
                actionState.ForceNormal();
                Time.timeScale = 0f;
                Assert(!controller.TryPlaceBomb(11f) && progress.GetResourceCount(RunResourceType.Bomb) == 1,
                    "Paused time must reject bomb use without consuming it.");
                Time.timeScale = 1f;
                playerHealth.TakeDamage(999f);
                Assert(playerHealth.IsDead && !controller.TryPlaceBomb(11f) &&
                       progress.GetResourceCount(RunResourceType.Bomb) == 1,
                    "A dead player must reject bomb use without consuming it.");
                playerHealth.ResetHealth();
                progress.StopProgression();
                Assert(!controller.TryPlaceBomb(11f) && progress.GetResourceCount(RunResourceType.Bomb) == 1,
                    "A stopped Run must reject bomb use without consuming it.");
            }
            finally
            {
                Time.timeScale = 1f;
                if (placed != null) Object.DestroyImmediate(placed.gameObject);
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateHealthTarget(Transform parent, string name, Vector2 position,
            float maxHealth, string layerName, bool duplicateCollider)
        {
            GameObject target = new(name, typeof(CircleCollider2D), typeof(Health));
            target.transform.SetParent(parent);
            target.transform.position = position;
            target.layer = LayerMask.NameToLayer(layerName);
            SerializedObject serializedHealth = new(target.GetComponent<Health>());
            serializedHealth.FindProperty("maxHealth").floatValue = maxHealth;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            InvokeLifecycle(target.GetComponent<Health>(), "Awake");
            if (duplicateCollider)
            {
                GameObject child = new("Additional Collider", typeof(CircleCollider2D));
                child.transform.SetParent(target.transform, false);
                child.layer = target.layer;
            }
            return target;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            component.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
