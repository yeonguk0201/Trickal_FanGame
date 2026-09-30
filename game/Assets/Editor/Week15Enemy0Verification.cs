using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Enemy0Verification
    {
        [MenuItem("Trickal Fan Game/Week 15/Verify Enemy-0 Shared Behavior")]
        public static void Verify()
        {
            ValidatePrefabContracts();
            ValidateEncounterAlertAndDisable();
            ValidateDamageAlertAndSuppression();
            ValidateControllerPersistence();
            Week7ChaserMonsterVerification.VerifyEnemyBehaviorOnly();
            Week7RangedEnemyVerification.Verify();
            Week7ChargingEnemyVerification.Verify();
            Debug.Log("Week 15 Enemy-0 verification passed: shared target lookup, encounter/damage alerts, persistent pursuit outside detection, player death, knockback, enemy death, and room-disable cleanup are valid for all three normal enemy controllers.");
        }

        public static void SetupAndVerifyBatch()
        {
            string[] paths =
            {
                Week15Enemy0Setup.ChaserPrefabPath,
                Week15Enemy0Setup.RangedPrefabPath,
                Week15Enemy0Setup.ChargingPrefabPath,
            };
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week15Enemy0Setup.Setup();
            Week15Enemy0Setup.Setup();
            Assert(guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID)),
                "Enemy-0 Setup changed an existing enemy prefab GUID.");
            Verify();
        }

        private static void ValidatePrefabContracts()
        {
            ValidatePrefab(Week15Enemy0Setup.ChaserPrefabPath, typeof(EnemyChase));
            ValidatePrefab(Week15Enemy0Setup.RangedPrefabPath, typeof(RangedEnemyController));
            ValidatePrefab(Week15Enemy0Setup.ChargingPrefabPath, typeof(ChargingEnemyController));
        }

        private static void ValidatePrefab(string path, Type controllerType)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert(prefab != null && prefab.GetComponent(controllerType) != null,
                $"Enemy-0 prefab {path} is missing {controllerType.Name}.");
            Assert(prefab.GetComponents<EnemyBehaviorContext>().Length == 1,
                $"Enemy-0 prefab {path} must contain exactly one shared behavior context.");
        }

        private static void ValidateEncounterAlertAndDisable()
        {
            GameObject root = new("Enemy-0 Encounter Verification");
            GameObject player = CreatePlayer(root.transform, out Health playerHealth);
            GameObject roomObject = new("Enemy-0 Room");
            roomObject.transform.SetParent(root.transform);
            roomObject.AddComponent<BoxCollider2D>();
            RoomController room = roomObject.AddComponent<RoomController>();
            GameObject chaser = CreateEnemy<EnemyChase>(roomObject.transform, out _, out _);
            GameObject ranged = CreateEnemy<RangedEnemyController>(roomObject.transform, out _, out _);
            GameObject charging = CreateEnemy<ChargingEnemyController>(roomObject.transform, out _, out _);
            Health[] enemies = { chaser.GetComponent<Health>(), ranged.GetComponent<Health>(), charging.GetComponent<Health>() };

            try
            {
                chaser.transform.position = new Vector2(40f, 0f);
                ranged.transform.position = new Vector2(45f, 0f);
                charging.transform.position = new Vector2(50f, 0f);
                room.ConfigurePreplacedEnemies(enemies);
                InvokeLifecycle(room, "Awake");
                Assert(enemies.All(enemy => !enemy.gameObject.activeSelf),
                    "Preplaced enemies must remain inactive before room combat begins.");

                room.BeginCombat(playerHealth);
                Assert(room.State == RoomState.Combat && room.AliveEnemyCount == 3 &&
                       enemies.All(enemy => enemy.gameObject.activeSelf &&
                                            enemy.GetComponent<EnemyBehaviorContext>().IsAlerted),
                    "Room combat must alert every encounter enemy, including enemies outside detection range.");

                roomObject.SetActive(false);
                foreach (Health enemy in enemies)
                {
                    InvokeLifecycle(enemy.GetComponent<EnemyBehaviorContext>(), "OnDisable");
                }
                Assert(enemies.All(enemy => !enemy.GetComponent<EnemyBehaviorContext>().IsAlerted &&
                                            enemy.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero),
                    "Disabling a room must clear all enemy alerts and residual movement.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateDamageAlertAndSuppression()
        {
            GameObject root = new("Enemy-0 Damage Verification");
            GameObject player = CreatePlayer(root.transform, out Health playerHealth);
            GameObject enemy = CreateEnemy<EnemyChase>(root.transform, out Health enemyHealth,
                out Rigidbody2D body);
            EnemyChase chase = enemy.GetComponent<EnemyChase>();
            EnemyBehaviorContext behavior = enemy.GetComponent<EnemyBehaviorContext>();
            KnockbackReceiver knockback = enemy.GetComponent<KnockbackReceiver>();

            try
            {
                player.transform.position = new Vector2(20f, 0f);
                chase.Configure(2f, 6f, 0.8f);
                chase.SetTarget(player.transform);
                chase.TickChase();
                Assert(!behavior.IsAlerted && body.linearVelocity == Vector2.zero,
                    "An idle enemy must not act before detection, encounter start, or damage.");

                enemyHealth.TakeDamage(new DamageContext(player, DamageSourceType.PlayerAttack, 1f));
                chase.TickChase();
                Assert(behavior.IsAlerted && body.linearVelocity.x > 0f,
                    "Player damage must alert an enemy and keep pursuit active outside detection range.");

                Assert(knockback.Apply(Vector2.left, 5f, 0.2f, 0.2f),
                    "The Enemy-0 verification enemy must accept knockback.");
                chase.TickChase();
                Assert(behavior.IsMovementSuppressed && body.linearVelocity.x < 0f,
                    "Shared suppression must preserve knockback velocity instead of overwriting it.");

                playerHealth.TakeDamage(playerHealth.MaxHealth);
                chase.TickChase();
                Assert(!behavior.IsAlerted && body.linearVelocity == Vector2.zero,
                    "Player death must end the alert and stop the enemy action.");

                playerHealth.ResetHealth();
                behavior.BeginCombat(player.transform);
                enemyHealth.TakeDamage(enemyHealth.MaxHealth);
                chase.TickChase();
                Assert(enemyHealth.IsDead && !behavior.IsAlerted && body.linearVelocity == Vector2.zero,
                    "Enemy death must end the alert and stop movement.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateControllerPersistence()
        {
            GameObject root = new("Enemy-0 Controller Verification");
            GameObject player = CreatePlayer(root.transform, out _);
            GameObject rangedObject = CreateEnemy<RangedEnemyController>(root.transform, out _, out Rigidbody2D rangedBody);
            GameObject chargingObject = CreateEnemy<ChargingEnemyController>(root.transform, out _, out _);
            RangedEnemyController ranged = rangedObject.GetComponent<RangedEnemyController>();
            ChargingEnemyController charging = chargingObject.GetComponent<ChargingEnemyController>();

            try
            {
                player.transform.position = new Vector2(30f, 0f);
                ranged.SetTarget(player.transform);
                rangedObject.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                ranged.TickBehavior(10f);
                Assert(rangedBody.linearVelocity.x > 0f,
                    "An alerted ranged enemy must reposition toward a target outside its original detection range.");

                charging.SetTarget(player.transform);
                chargingObject.GetComponent<EnemyBehaviorContext>().BeginCombat(player.transform);
                charging.TickBehavior(10f);
                Assert(charging.State == ChargingEnemyState.Windup && charging.LockedDirection.x > 0f,
                    "An alerted charging enemy must start its pattern outside its original detection range.");

                chargingObject.SetActive(false);
                InvokeLifecycle(charging, "OnDisable");
                InvokeLifecycle(chargingObject.GetComponent<EnemyBehaviorContext>(), "OnDisable");
                Assert(charging.State == ChargingEnemyState.Idle &&
                       !chargingObject.GetComponent<EnemyBehaviorContext>().IsAlerted,
                    "Disabling a charging enemy must cancel its in-progress pattern and alert.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePlayer(Transform parent, out Health health)
        {
            GameObject player = new("Enemy-0 Player");
            player.transform.SetParent(parent);
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            health = player.AddComponent<Health>();
            player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerActionState>();
            player.AddComponent<PlayerMovement>();
            InvokeLifecycle(health, "Awake");
            return player;
        }

        private static GameObject CreateEnemy<T>(Transform parent, out Health health, out Rigidbody2D body)
            where T : MonoBehaviour
        {
            GameObject enemy = new(typeof(T).Name);
            enemy.transform.SetParent(parent);
            body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            enemy.AddComponent<CircleCollider2D>();
            health = enemy.AddComponent<Health>();
            KnockbackReceiver knockback = enemy.AddComponent<KnockbackReceiver>();
            EnemyBehaviorContext behavior = enemy.AddComponent<EnemyBehaviorContext>();
            T controller = enemy.AddComponent<T>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(knockback, "Awake");
            InvokeLifecycle(behavior, "Awake");
            InvokeLifecycle(controller, "Awake");
            return enemy;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }

            method.Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
