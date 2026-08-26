using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week7ChargingEnemyVerification
    {
        private const string ChargingPrefabPath = "Assets/Prefabs/ChargingEnemy.prefab";

        [MenuItem("Trickal Fan Game/Verify Phase E-3 Charging Enemy")]
        public static void Verify()
        {
            GameObject prefab = ValidatePrefabContract();
            ValidateFixedGraphAssignmentIfPresent(prefab);
            ValidateChargePattern();
            Debug.Log(
                "Phase E-3 charging enemy verification passed: target locking, windup/dash/recovery timing, " +
                "single-hit damage, wall/portal interruption, cooldown, knockback/stun suppression, death, " +
                "and room-disable cleanup are valid.");
        }

        private static GameObject ValidatePrefabContract()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChargingPrefabPath);
            Assert(prefab != null,
                $"Missing {ChargingPrefabPath}. Run Setup Phase E-3 Charging Enemy first.");

            Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
            Collider2D collider = prefab.GetComponent<Collider2D>();
            Assert(prefab.layer == LayerMask.NameToLayer("Enemy"),
                "The charging prefab must remain on the Enemy layer for player skill targeting.");
            Assert(body != null && body.bodyType == RigidbodyType2D.Dynamic &&
                   (body.constraints & RigidbodyConstraints2D.FreezeRotation) != 0 &&
                   body.collisionDetectionMode == CollisionDetectionMode2D.Continuous,
                "The charging enemy must use a rotation-locked Dynamic Rigidbody2D with continuous collision detection.");
            Assert(collider != null && collider.enabled && !collider.isTrigger,
                "The charging enemy needs an enabled non-trigger collider.");
            Assert(prefab.GetComponent<Health>() != null && prefab.GetComponent<TestEnemy>() != null &&
                   prefab.GetComponent<KnockbackReceiver>() != null &&
                   prefab.GetComponent<ChargingEnemyController>() != null,
                "The charging prefab is missing health, death, knockback, or charging behavior.");
            Assert(prefab.GetComponent<EnemyChase>() == null &&
                   prefab.GetComponent<RangedEnemyController>() == null &&
                   prefab.GetComponent<ContactDamage>() == null,
                "The charging prefab must not retain chaser, ranged, or unrestricted contact-damage behavior.");
            return prefab;
        }

        private static void ValidateFixedGraphAssignmentIfPresent(GameObject prefab)
        {
            foreach (RoomNode node in Resources.FindObjectsOfTypeAll<RoomNode>())
            {
                if (node == null || !node.gameObject.scene.IsValid() ||
                    node.GetComponentInParent<RoomGraphController>() == null ||
                    node.FloorNumber != 1 || node.RoomNumber != 2 || node.ContentRoot == null)
                {
                    continue;
                }

                RoomController controller = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                Assert(controller != null, "Fixed graph room 2 is missing its RoomController.");
                SerializedObject serializedController = new(controller);
                Assert(serializedController.FindProperty("enemyPrefab").objectReferenceValue == prefab,
                    "Fixed graph room 2 must use ChargingEnemy after Phase E-3 setup.");
                return;
            }
        }

        private static void ValidateChargePattern()
        {
            GameObject root = new("Phase E-3 Verification Root");
            GameObject player = CreatePlayer(root.transform, out Health playerHealth,
                out Collider2D playerCollider, out PlayerDeathReason deathReason);
            GameObject enemy = CreateEnemy(root.transform, out Health enemyHealth, out Rigidbody2D enemyBody,
                out KnockbackReceiver knockback, out ChargingEnemyController charging);

            try
            {
                charging.Configure(7f, 0.65f, 9f, 0.8f, 0.6f, 1.5f, 2);
                charging.SetTarget(player.transform);

                player.transform.position = Vector2.right * 5f;
                charging.TickBehavior(0f);
                Assert(charging.State == ChargingEnemyState.Windup &&
                       Vector2.Dot(charging.LockedDirection, Vector2.right) > 0.999f &&
                       enemyBody.linearVelocity == Vector2.zero,
                    "A detected target must start a stationary windup and lock its current direction.");

                player.transform.position = Vector2.left * 5f;
                charging.TickBehavior(0.64f);
                Assert(charging.State == ChargingEnemyState.Windup &&
                       Vector2.Dot(charging.LockedDirection, Vector2.right) > 0.999f,
                    "Moving during windup must not redirect the committed charge.");
                charging.TickBehavior(0.65f);
                Assert(charging.State == ChargingEnemyState.Dashing &&
                       Vector2.Dot(enemyBody.linearVelocity.normalized, Vector2.right) > 0.999f &&
                       Mathf.Approximately(enemyBody.linearVelocity.magnitude, 9f),
                    "The charging enemy must dash in its locked direction when windup ends.");

                int healthBeforeHit = playerHealth.CurrentHealth;
                Assert(charging.TryResolveCollision(playerCollider, 0.7f) &&
                       playerHealth.CurrentHealth == healthBeforeHit - 2 &&
                       deathReason.CurrentReason == "ENEMY" &&
                       charging.State == ChargingEnemyState.Recovering &&
                       enemyBody.linearVelocity == Vector2.zero,
                    "A dash collision must damage the player once, set ENEMY as death reason, and enter recovery.");
                Assert(!charging.TryResolveCollision(playerCollider, 0.71f) &&
                       playerHealth.CurrentHealth == healthBeforeHit - 2,
                    "The same charge must not damage the player more than once.");

                charging.TickBehavior(1.3f);
                Assert(charging.State == ChargingEnemyState.Idle,
                    "The charging enemy must leave recovery after its configured duration.");
                charging.TickBehavior(2.19f);
                Assert(charging.State == ChargingEnemyState.Idle,
                    "The charging enemy must respect cooldown after a completed dash.");
                charging.TickBehavior(2.2f);
                charging.TickBehavior(2.85f);
                Assert(charging.State == ChargingEnemyState.Dashing &&
                       Vector2.Dot(enemyBody.linearVelocity.normalized, Vector2.left) > 0.999f,
                    "A later charge must lock the target's new position rather than reusing the previous direction.");

                GameObject wall = new("Phase E-3 Solid Wall");
                wall.transform.SetParent(root.transform);
                wall.layer = LayerMask.NameToLayer("Environment");
                Collider2D wallCollider = wall.AddComponent<BoxCollider2D>();
                Assert(charging.TryResolveCollision(wallCollider, 2.9f) &&
                       charging.State == ChargingEnemyState.Recovering &&
                       enemyBody.linearVelocity == Vector2.zero,
                    "Solid room boundaries must stop a charge and start recovery.");

                charging.TickBehavior(3.5f);
                charging.TickBehavior(4.4f);
                Assert(charging.State == ChargingEnemyState.Windup,
                    "The charging enemy must be ready for another pattern after recovery and cooldown.");

                float interruptionStart = Time.time;
                Assert(knockback.Apply(Vector2.right, 8f, 0.2f, 0.4f),
                    "The charging enemy must accept ultimate knockback.");
                charging.TickBehavior(4.5f);
                Assert(charging.State == ChargingEnemyState.Idle && knockback.IsKnockedBack &&
                       Vector2.Dot(enemyBody.linearVelocity.normalized, Vector2.right) > 0.999f,
                    "Knockback must cancel windup without overwriting knockback velocity.");
                knockback.Tick(interruptionStart + 0.21f);
                charging.TickBehavior(4.6f);
                Assert(knockback.IsStunned && enemyBody.linearVelocity == Vector2.zero,
                    "The charging enemy must remain stopped during post-knockback stun.");
                knockback.Tick(interruptionStart + 0.61f);
                charging.TickBehavior(5.99f);
                Assert(!knockback.IsActive && charging.State == ChargingEnemyState.Idle,
                    "An interrupted charge must still respect its cooldown after stun ends.");

                charging.TickBehavior(6f);
                charging.TickBehavior(6.65f);
                Assert(charging.State == ChargingEnemyState.Dashing,
                    "The charging pattern must resume after interruption cooldown ends.");

                GameObject doorObject = new("Phase E-3 Portal Door");
                doorObject.transform.SetParent(root.transform);
                Collider2D doorCollider = doorObject.AddComponent<BoxCollider2D>();
                DoorController door = doorObject.AddComponent<DoorController>();
                door.ConfigurePortalBarrier(true);
                Assert(charging.TryResolveCollision(doorCollider, 6.7f) &&
                       charging.State == ChargingEnemyState.Recovering,
                    "Green portal barriers must stop an active charge.");

                InvokeLifecycle(charging, "OnDisable");
                Assert(charging.State == ChargingEnemyState.Idle && enemyBody.linearVelocity == Vector2.zero,
                    "Disabling a room must clear the active pattern and residual charge velocity.");

                PlayerCombatEvents combatEvents = player.GetComponent<PlayerCombatEvents>();
                int killCount = 0;
                Health killedTarget = null;
                combatEvents.EnemyKilled += killEvent =>
                {
                    killCount++;
                    killedTarget = killEvent.Target;
                };
                enemyHealth.TakeDamage(new DamageContext(
                    player,
                    DamageSourceType.PlayerProjectile,
                    enemyHealth.MaxHealth));
                charging.TickBehavior(10f);
                Assert(killCount == 1 && killedTarget == enemyHealth && enemyHealth.IsDead &&
                       charging.IsActionSuppressed && enemyBody.linearVelocity == Vector2.zero,
                    "Defeating the charging enemy must report one shared kill event and stop its pattern.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePlayer(
            Transform parent,
            out Health health,
            out Collider2D collider,
            out PlayerDeathReason deathReason)
        {
            GameObject player = new("Phase E-3 Player");
            player.transform.SetParent(parent);
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            collider = player.AddComponent<CircleCollider2D>();
            health = player.AddComponent<Health>();
            player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerActionState>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerCombatEvents>();
            deathReason = player.AddComponent<PlayerDeathReason>();
            InvokeLifecycle(health, "Awake");
            return player;
        }

        private static GameObject CreateEnemy(
            Transform parent,
            out Health health,
            out Rigidbody2D body,
            out KnockbackReceiver knockback,
            out ChargingEnemyController charging)
        {
            GameObject enemy = new("Phase E-3 Charging Enemy");
            enemy.transform.SetParent(parent);
            enemy.layer = LayerMask.NameToLayer("Enemy");
            body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            enemy.AddComponent<SpriteRenderer>();
            enemy.AddComponent<CircleCollider2D>();
            health = enemy.AddComponent<Health>();
            knockback = enemy.AddComponent<KnockbackReceiver>();
            charging = enemy.AddComponent<ChargingEnemyController>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(knockback, "Awake");
            InvokeLifecycle(charging, "Awake");
            return enemy;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
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
