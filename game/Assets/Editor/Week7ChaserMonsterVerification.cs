using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week7ChaserMonsterVerification
    {
        private const string EnemyPrefabPath = "Assets/Prefabs/TestEnemy.prefab";

        [MenuItem("Trickal Fan Game/Verify Phase E-1 Chaser Monster")]
        public static void Verify()
        {
            VerifyEnemyBehaviorOnly();

            Week7HighGradeSkillVerification.Verify();
            Week7LowerGradeSkillVerification.Verify();
            Week7RoomGraphVerification.Verify();

            Debug.Log(
                "Phase E-1 chaser verification passed: Rigidbody chase/stop behavior, knockback and stun " +
                "suppression, death/disable cleanup, dash area damage, kill/SP routing, room-state preservation, " +
                "and solid wall/portal boundaries are valid.");
        }

        public static void VerifyEnemyBehaviorOnly()
        {
            ValidatePrefabContract();
            ValidateChaseAndInterruption();
        }

        private static void ValidatePrefabContract()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            Assert(prefab != null, $"Missing chaser prefab at {EnemyPrefabPath}.");

            Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
            Collider2D collider = prefab.GetComponent<Collider2D>();
            Assert(prefab.layer == LayerMask.NameToLayer("Enemy"),
                "The chaser prefab must remain on the Enemy layer for player skill targeting.");
            Assert(body != null && body.bodyType == RigidbodyType2D.Dynamic &&
                   (body.constraints & RigidbodyConstraints2D.FreezeRotation) != 0,
                "The chaser must use a rotation-locked Dynamic Rigidbody2D so solid walls and doors block it.");
            Assert(collider != null && collider.enabled && !collider.isTrigger,
                "The chaser needs an enabled non-trigger collider for wall, door, and player contact.");
            Assert(prefab.GetComponent<Health>() != null && prefab.GetComponent<TestEnemy>() != null &&
                   prefab.GetComponent<EnemyChase>() != null && prefab.GetComponent<ContactDamage>() != null &&
                   prefab.GetComponent<KnockbackReceiver>() != null,
                "The chaser prefab is missing health, death, chase, contact-damage, or knockback behavior.");
        }

        private static void ValidateChaseAndInterruption()
        {
            GameObject root = new("Phase E-1 Chaser Verification Root");
            GameObject player = new("Phase E-1 Target");
            GameObject enemy = new("Phase E-1 Chaser");
            player.transform.SetParent(root.transform);
            enemy.transform.SetParent(root.transform);

            try
            {
                Health playerHealth = player.AddComponent<Health>();
                Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                enemy.AddComponent<CircleCollider2D>();
                Health enemyHealth = enemy.AddComponent<Health>();
                KnockbackReceiver knockback = enemy.AddComponent<KnockbackReceiver>();
                EnemyChase chase = enemy.AddComponent<EnemyChase>();

                InvokeLifecycle(playerHealth, "Awake");
                InvokeLifecycle(enemyHealth, "Awake");
                InvokeLifecycle(knockback, "Awake");
                InvokeLifecycle(chase, "Awake");
                chase.Configure(2f, 6f, 0.8f);
                chase.SetTarget(player.transform);

                player.transform.position = Vector2.right * 3f;
                chase.TickChase();
                Assert(Vector2.Dot(body.linearVelocity.normalized, Vector2.right) > 0.999f &&
                       Mathf.Approximately(body.linearVelocity.magnitude, 2f),
                    "The chaser must move toward a living target inside detection range.");

                float interruptionStart = Time.time;
                Assert(knockback.Apply(Vector2.left, 8f, 0.2f, 0.4f),
                    "The chaser must accept ultimate knockback.");
                chase.TickChase();
                Assert(knockback.IsKnockedBack && Vector2.Dot(body.linearVelocity.normalized, Vector2.left) > 0.999f,
                    "Chase movement must not overwrite active knockback velocity.");

                knockback.Tick(interruptionStart + 0.21f);
                chase.TickChase();
                Assert(knockback.IsStunned && body.linearVelocity == Vector2.zero,
                    "The chaser must remain stopped during post-knockback stun.");

                knockback.Tick(interruptionStart + 0.61f);
                chase.TickChase();
                Assert(!knockback.IsActive && Vector2.Dot(body.linearVelocity.normalized, Vector2.right) > 0.999f,
                    "The chaser must resume tracking after stun ends.");

                player.transform.position = Vector2.right * 0.5f;
                chase.TickChase();
                Assert(body.linearVelocity == Vector2.zero,
                    "The chaser must stop inside its configured contact distance.");

                player.transform.position = Vector2.right * 7f;
                chase.TickChase();
                Assert(Vector2.Dot(body.linearVelocity.normalized, Vector2.right) > 0.999f,
                    "Once alerted, the chaser must keep tracking outside its initial detection range.");

                player.transform.position = Vector2.right * 3f;
                chase.TickChase();
                InvokeLifecycle(chase, "OnDisable");
                Assert(body.linearVelocity == Vector2.zero,
                    "Disabling a room must clear residual chase velocity without resetting enemy health.");

                enemyHealth.TakeDamage(enemyHealth.MaxHealth);
                chase.TickChase();
                Assert(enemyHealth.IsDead && chase.IsMovementSuppressed && body.linearVelocity == Vector2.zero,
                    "A dead chaser must stop permanently.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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
