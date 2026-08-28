using System;
using System.Collections.Generic;
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
    public static class Week7RangedEnemyVerification
    {
        private const string RangedPrefabPath = "Assets/Prefabs/RangedEnemy.prefab";

        [MenuItem("Trickal Fan Game/Verify Phase E-2 Ranged Enemy")]
        public static void Verify()
        {
            GameObject prefab = ValidatePrefabContract();
            ValidateFixedGraphAssignmentIfPresent(prefab);
            ValidateBehaviorAndProjectile();
            Debug.Log(
                "Phase E-2 ranged enemy verification passed: approach/retreat distance control, timed firing, " +
                "knockback/stun suppression, EnemyProjectile damage, wall/portal blocking, and room-transition " +
                "projectile cleanup are valid.");
        }

        private static GameObject ValidatePrefabContract()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefabPath);
            Assert(prefab != null,
                $"Missing {RangedPrefabPath}. Run Setup Phase E-2 Ranged Enemy first.");

            Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
            Collider2D collider = prefab.GetComponent<Collider2D>();
            Assert(prefab.layer == LayerMask.NameToLayer("Enemy"),
                "The ranged prefab must remain on the Enemy layer for player skill targeting.");
            Assert(body != null && body.bodyType == RigidbodyType2D.Dynamic &&
                   (body.constraints & RigidbodyConstraints2D.FreezeRotation) != 0,
                "The ranged enemy must use a rotation-locked Dynamic Rigidbody2D for solid boundaries.");
            Assert(collider != null && collider.enabled && !collider.isTrigger,
                "The ranged enemy needs an enabled non-trigger collider.");
            Assert(prefab.GetComponent<Health>() != null && prefab.GetComponent<TestEnemy>() != null &&
                   prefab.GetComponent<KnockbackReceiver>() != null &&
                   prefab.GetComponent<RangedEnemyController>() != null,
                "The ranged prefab is missing health, death, knockback, or ranged behavior.");
            Assert(prefab.GetComponent<EnemyChase>() == null && prefab.GetComponent<ContactDamage>() == null,
                "The ranged prefab must not retain chaser or contact-damage behavior.");
            return prefab;
        }

        private static void ValidateFixedGraphAssignmentIfPresent(GameObject prefab)
        {
            foreach (RoomNode node in Resources.FindObjectsOfTypeAll<RoomNode>())
            {
                if (node == null || !node.gameObject.scene.IsValid() ||
                    node.FloorNumber != 1 || node.RoomNumber != 3 || node.ContentRoot == null)
                {
                    continue;
                }

                RoomController controller = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                Assert(controller != null, "Fixed graph room 3 is missing its RoomController.");
                SerializedObject serializedController = new(controller);
                Assert(serializedController.FindProperty("enemyPrefab").objectReferenceValue == prefab,
                    "Fixed graph room 3 must use RangedEnemy after Phase E-2 setup.");
                return;
            }
        }

        private static void ValidateBehaviorAndProjectile()
        {
            GameObject root = new("Phase E-2 Verification Root");
            GameObject player = CreatePlayer(root.transform, out Health playerHealth, out Collider2D playerCollider,
                out PlayerDeathReason deathReason);
            GameObject enemy = CreateEnemy(root.transform, out Health enemyHealth, out Rigidbody2D enemyBody,
                out KnockbackReceiver knockback, out RangedEnemyController ranged);
            List<EnemyProjectile> fired = new();
            ranged.ProjectileFired += fired.Add;

            try
            {
                ranged.Configure(2f, 8f, 3f, 6f, 1.5f, 5f, 1, 4f);
                ranged.SetTarget(player.transform);

                player.transform.position = Vector2.right * 7f;
                ranged.TickBehavior(10f);
                Assert(Vector2.Dot(enemyBody.linearVelocity.normalized, Vector2.right) > 0.999f && fired.Count == 0,
                    "The ranged enemy must approach a target beyond maximum attack distance without firing.");

                player.transform.position = Vector2.right * 2f;
                ranged.TickBehavior(20f);
                Assert(Vector2.Dot(enemyBody.linearVelocity.normalized, Vector2.left) > 0.999f && fired.Count == 0,
                    "The ranged enemy must retreat from a target inside minimum attack distance without firing.");

                player.transform.position = Vector2.right * 4f;
                ranged.TickBehavior(100f);
                Assert(enemyBody.linearVelocity == Vector2.zero && fired.Count == 1,
                    "The ranged enemy must stop and fire immediately inside its attack band.");
                EnemyProjectile first = fired[0];
                Assert(first != null && first.IsLaunched &&
                       Vector2.Dot(first.Velocity.normalized, Vector2.right) > 0.999f &&
                       Mathf.Approximately(first.Velocity.magnitude, 5f) &&
                       first.DamageContext.Source == enemy &&
                       first.DamageContext.SourceType == DamageSourceType.EnemyProjectile,
                    "A ranged shot must preserve direction, speed, owner, and EnemyProjectile damage context.");

                ranged.TickBehavior(101.49f);
                Assert(fired.Count == 1, "The ranged enemy must not fire before its attack interval ends.");
                ranged.TickBehavior(101.5f);
                Assert(fired.Count == 2, "The ranged enemy must fire again when its attack interval ends.");

                float healthBeforeHit = playerHealth.CurrentHealth;
                Assert(first.TryHit(playerCollider), "A ranged projectile must recognize the player collider.");
                Assert(first == null && playerHealth.CurrentHealth == healthBeforeHit - 1 &&
                       deathReason.CurrentReason == "ENEMY",
                    "A ranged projectile must damage the player once, set ENEMY as death reason, and disappear.");

                GameObject wall = new("Phase E-2 Solid Wall");
                wall.transform.SetParent(root.transform);
                wall.layer = LayerMask.NameToLayer("Environment");
                Collider2D wallCollider = wall.AddComponent<BoxCollider2D>();
                EnemyProjectile wallShot = EnemyProjectile.Create(
                    Vector2.zero, Vector2.right, enemy, 1, 5f, 4f, null);
                Assert(wallShot.TryHit(wallCollider) && wallShot == null,
                    "Enemy projectiles must be consumed by solid environment boundaries.");

                GameObject doorObject = new("Phase E-2 Portal Door");
                doorObject.transform.SetParent(root.transform);
                Collider2D doorCollider = doorObject.AddComponent<BoxCollider2D>();
                DoorController door = doorObject.AddComponent<DoorController>();
                door.ConfigurePortalBarrier(true);
                EnemyProjectile doorShot = EnemyProjectile.Create(
                    Vector2.zero, Vector2.right, enemy, 1, 5f, 4f, null);
                Assert(doorShot.TryHit(doorCollider) && doorShot == null,
                    "Enemy projectiles must be consumed by green portal barriers.");

                int firedBeforeSuppression = fired.Count;
                float interruptionStart = Time.time;
                Assert(knockback.Apply(Vector2.left, 8f, 0.2f, 0.4f),
                    "The ranged enemy must accept ultimate knockback.");
                ranged.TickBehavior(200f);
                Assert(fired.Count == firedBeforeSuppression && knockback.IsKnockedBack &&
                       Vector2.Dot(enemyBody.linearVelocity.normalized, Vector2.left) > 0.999f,
                    "Ranged movement and firing must not overwrite active knockback.");
                knockback.Tick(interruptionStart + 0.21f);
                ranged.TickBehavior(200f);
                Assert(knockback.IsStunned && enemyBody.linearVelocity == Vector2.zero &&
                       fired.Count == firedBeforeSuppression,
                    "The ranged enemy must remain stopped and unable to fire during stun.");
                knockback.Tick(interruptionStart + 0.61f);
                ranged.TickBehavior(200f);
                Assert(!knockback.IsActive && fired.Count == firedBeforeSuppression + 1,
                    "The ranged enemy must resume firing after stun ends.");

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
                Assert(killCount == 1 && killedTarget == enemyHealth,
                    "Defeating the ranged enemy must use the shared player kill-event route exactly once.");

                EnemyProjectile leakedShot = EnemyProjectile.Create(
                    Vector2.zero, Vector2.right, enemy, 1, 5f, 4f, null);
                InvokeStatic(typeof(RoomGraphController), "ClearTransientProjectiles");
                Assert(leakedShot == null,
                    "A room transition must clear launched ranged-enemy projectiles.");
            }
            finally
            {
                ranged.ProjectileFired -= fired.Add;
                foreach (EnemyProjectile projectile in
                         UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                {
                    projectile.StopAtBoundary();
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePlayer(
            Transform parent,
            out Health health,
            out Collider2D collider,
            out PlayerDeathReason deathReason)
        {
            GameObject player = new("Phase E-2 Player");
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
            out RangedEnemyController ranged)
        {
            GameObject enemy = new("Phase E-2 Ranged Enemy");
            enemy.transform.SetParent(parent);
            enemy.layer = LayerMask.NameToLayer("Enemy");
            body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            enemy.AddComponent<SpriteRenderer>();
            enemy.AddComponent<CircleCollider2D>();
            health = enemy.AddComponent<Health>();
            knockback = enemy.AddComponent<KnockbackReceiver>();
            ranged = enemy.AddComponent<RangedEnemyController>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(knockback, "Awake");
            InvokeLifecycle(ranged, "Awake");
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

        private static void InvokeStatic(Type type, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, methodName);
            }

            method.Invoke(null, null);
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
