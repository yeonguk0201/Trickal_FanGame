using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week18Obstacle0Verification
    {
        // Far from every authored scene so only the verification colliders take part in physics queries.
        internal static readonly Vector2 Origin = new(5000f, 5000f);
        private const float EnemyRadius = 0.5f;
        private const float WalkStep = 0.05f;
        private const int WalkStepLimit = 2000;

        // Batch entry: Obstacle-0 plus the existing enemy behavior verifiers. They build their own objects near the
        // origin, so an empty scene keeps authored room walls out of the new line-of-sight checks.
        public static void VerifyWithEnemyRegressionsBatch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Verify();
            Week7ChaserMonsterVerification.VerifyEnemyBehaviorOnly();
            Week7RangedEnemyVerification.Verify();
            Week7ChargingEnemyVerification.Verify();
            Week7EnemyBalanceVerification.Verify();
            // Week15Enemy0Verification is left out: its charging check expects an immediate windup, which the
            // default 1.1 s pursuit added in Enemy-3 already replaced before Obstacle-0 (it fails at HEAD too).
            Week15Enemy1Verification.Verify();
            // Week15Enemy2Verification is left out: it checks legacy boss prefab components that Obstacle-0 does not
            // touch and already fails on the current prefab.
            Week15Enemy4Verification.Verify();
            // Week15Enemy3/5Verification are left out: they stop on prefab-value checks (pursuit duration, sprite
            // tint) that Obstacle-0 does not touch and that already fail on the current prefabs.
        }

        [MenuItem("Trickal Fan Game/Week 18/Verify Obstacle-0 Enemy Obstacle Avoidance")]
        public static void Verify()
        {
            ValidateNavigatorRoutes();
            ValidateLineOfFire();
            ValidateChaser();
            ValidateRanged();
            ValidateCharging();
            ValidateMinimumChargeDistance();
            ValidateChargingCollisionAngles();
            ValidateSniper();
            Debug.Log("Obstacle-0 verification passed: enemies walk straight while the line is clear, route around " +
                      "single and cup-shaped obstacles on a deterministic 0.5 grid without entering them, fall back " +
                      "safely when sealed in, ranged and sniper enemies hold fire and reposition while the line of " +
                      "fire is blocked, and charging enemies only telegraph a charge along a clear line, slide along " +
                      "80-degree glancing obstacle contacts, and recover on frontal impacts.");
        }

        private static void ValidateNavigatorRoutes()
        {
            GameObject root = new("Obstacle-0 Route Verification");
            try
            {
                Vector2 start = Origin;
                Vector2 goal = Origin + Vector2.right * 4f;
                EnemyObstacleNavigator navigator = new();
                Physics2D.SyncTransforms();
                Assert(Vector2.Dot(navigator.GetMoveDirection(start, goal, EnemyRadius, 0f), Vector2.right) > 0.999f &&
                       !navigator.IsFollowingPath,
                    "With a clear line the enemy must walk straight without a grid path.");

                GameObject single = CreateObstacle(root.transform, Origin + Vector2.right * 2f);
                Physics2D.SyncTransforms();
                Assert(!EnemyObstacleNavigator.HasClearPath(start, goal, EnemyRadius),
                    "A 1x1 obstacle on the line must block a body-sized path.");
                Vector2 detour = navigator.GetMoveDirection(start, goal, EnemyRadius, 0f);
                Assert(navigator.IsFollowingPath && Vector2.Dot(detour, Vector2.right) < 0.99f,
                    "A blocked line must switch to a grid path that steers off the straight line.");
                WalkAndAssertArrival(start, goal, "single obstacle");

                List<Vector2> first = new();
                List<Vector2> second = new();
                Assert(EnemyObstacleNavigator.TryFindPath(start, goal, EnemyRadius, first) &&
                       EnemyObstacleNavigator.TryFindPath(start, goal, EnemyRadius, second) &&
                       first.SequenceEqual(second),
                    "The same start, goal and obstacles must produce the same path.");
                Object.DestroyImmediate(single);

                // Cup opening away from the goal: pure steering toward the goal gets stuck at its back wall.
                foreach (Vector2 cell in new[]
                         {
                             new Vector2(1.5f, -1.5f), new Vector2(1.5f, -0.5f), new Vector2(1.5f, 0.5f),
                             new Vector2(1.5f, 1.5f), new Vector2(0.5f, 1.5f), new Vector2(-0.5f, 1.5f),
                             new Vector2(0.5f, -1.5f), new Vector2(-0.5f, -1.5f),
                         })
                {
                    CreateObstacle(root.transform, Origin + cell);
                }

                Physics2D.SyncTransforms();
                WalkAndAssertArrival(start, goal, "cup-shaped obstacle");

                // Sealed in: there is no route, so the navigator must fall back to the straight line.
                foreach (Vector2 cell in new[]
                         {
                             new Vector2(-1.5f, -1.5f), new Vector2(-1.5f, -0.5f), new Vector2(-1.5f, 0.5f),
                             new Vector2(-1.5f, 1.5f),
                         })
                {
                    CreateObstacle(root.transform, Origin + cell);
                }

                Physics2D.SyncTransforms();
                EnemyObstacleNavigator sealedNavigator = new();
                Assert(!EnemyObstacleNavigator.TryFindPath(start, goal, EnemyRadius, new List<Vector2>()) &&
                       Vector2.Dot(sealedNavigator.GetMoveDirection(start, goal, EnemyRadius, 0f), Vector2.right) >
                       0.999f,
                    "A sealed-in enemy must fall back to the straight direction instead of failing.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void WalkAndAssertArrival(Vector2 start, Vector2 goal, string label)
        {
            EnemyObstacleNavigator navigator = new();
            int mask = EnemyObstacleNavigator.ObstacleMask;
            Vector2 position = start;
            for (int step = 0; step < WalkStepLimit; step++)
            {
                if ((goal - position).magnitude <= 0.2f) return;
                Vector2 direction = navigator.GetMoveDirection(position, goal, EnemyRadius, step * 0.02f);
                Assert(direction.sqrMagnitude > 0.5f, $"The {label} route stopped before reaching the goal.");
                position += direction * WalkStep;
                Assert(Physics2D.OverlapCircle(position, EnemyRadius * 0.85f, mask) == null,
                    $"The {label} route walked the enemy body into an obstacle at {position - Origin}.");
            }

            throw new InvalidOperationException($"The {label} route did not reach the goal in {WalkStepLimit} steps.");
        }

        private static void ValidateLineOfFire()
        {
            GameObject root = new("Obstacle-0 Line Of Fire Verification");
            try
            {
                Vector2 shooter = Origin;
                Vector2 target = Origin + Vector2.right * 4f;
                Physics2D.SyncTransforms();
                Assert(EnemyObstacleNavigator.HasLineOfFire(shooter, target), "An open line must allow fire.");
                CreateObstacle(root.transform, Origin + Vector2.right * 2f);
                Physics2D.SyncTransforms();
                Assert(!EnemyObstacleNavigator.HasLineOfFire(shooter, target),
                    "An obstacle between shooter and target must block the line of fire.");
                Assert(EnemyObstacleNavigator.HasLineOfFire(shooter, Origin + new Vector2(4f, 3f)),
                    "A line that passes beside the obstacle must stay open.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateChaser()
        {
            GameObject root = new("Obstacle-0 Chaser Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.right * 4f);
                GameObject enemy = CreateEnemy<EnemyChase>(root.transform, out Rigidbody2D body, out EnemyChase chase);
                chase.Configure(2f, 10f, 0.8f);
                chase.SetTarget(player.transform);
                GameObject obstacle = CreateObstacle(root.transform, Origin + Vector2.right * 2f);
                Physics2D.SyncTransforms();
                chase.TickChase();
                Assert(body.linearVelocity.sqrMagnitude > 1f &&
                       Vector2.Dot(body.linearVelocity.normalized, Vector2.right) < 0.99f,
                    "A chaser blocked by an obstacle must steer around it instead of pushing straight into it.");
                Object.DestroyImmediate(obstacle);
                Physics2D.SyncTransforms();
                chase.TickChase();
                Assert(Vector2.Dot(body.linearVelocity.normalized, Vector2.right) > 0.999f,
                    "A chaser with a clear line must walk straight at the target.");
                Assert(enemy != null, "The chaser must survive the checks.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateRanged()
        {
            GameObject root = new("Obstacle-0 Ranged Verification");
            List<EnemyProjectile> fired = new();
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.right * 4f);
                CreateEnemy<RangedEnemyController>(root.transform, out Rigidbody2D body,
                    out RangedEnemyController ranged);
                ranged.SetTarget(player.transform);
                ranged.ProjectileFired += fired.Add;
                GameObject obstacle = CreateObstacle(root.transform, Origin + Vector2.right * 2f);
                Physics2D.SyncTransforms();
                ranged.TickBehavior(10f);
                Assert(fired.Count == 0 && body.linearVelocity.sqrMagnitude > 0.5f &&
                       Vector2.Dot(body.linearVelocity.normalized, Vector2.right) < 0.99f,
                    "A ranged enemy in range but behind an obstacle must hold fire and walk around it.");
                Object.DestroyImmediate(obstacle);
                Physics2D.SyncTransforms();
                ranged.TickBehavior(10f);
                Assert(fired.Count == 1 && body.linearVelocity == Vector2.zero,
                    "A ranged enemy in range with a clear line of fire must stop and fire.");
            }
            finally
            {
                foreach (EnemyProjectile projectile in fired.Where(projectile => projectile != null))
                    Object.DestroyImmediate(projectile.gameObject);
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateCharging()
        {
            GameObject root = new("Obstacle-0 Charging Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.right * 4f);
                CreateEnemy<ChargingEnemyController>(root.transform, out Rigidbody2D body,
                    out ChargingEnemyController charging);
                charging.ConfigurePursuitCharge(10f, 2.5f, 1f, 0.65f, 9f, 0.8f, 0.6f, EnemyDamageTier.Heavy);
                charging.SetTarget(player.transform);
                GameObject obstacle = CreateObstacle(root.transform, Origin + Vector2.right * 2f);
                Physics2D.SyncTransforms();

                charging.TickBehavior(0f);
                Assert(charging.State == ChargingEnemyState.Pursuing &&
                       Vector2.Dot(body.linearVelocity.normalized, Vector2.right) < 0.99f,
                    "A charging enemy must pursue around the obstacle.");
                charging.TickBehavior(5f);
                Assert(charging.State == ChargingEnemyState.Pursuing && body.linearVelocity.sqrMagnitude > 0.5f,
                    "A charging enemy must keep pursuing past its pursuit time while the charge line is blocked.");

                Object.DestroyImmediate(obstacle);
                Physics2D.SyncTransforms();
                charging.TickBehavior(5.1f);
                Assert(charging.State == ChargingEnemyState.Windup,
                    "A charging enemy must telegraph as soon as the charge line is clear.");

                obstacle = CreateObstacle(root.transform, Origin + Vector2.right * 2f);
                Physics2D.SyncTransforms();
                charging.TickBehavior(5.2f);
                LineRenderer preview = charging.GetComponent<LineRenderer>();
                float previewLength = Vector2.Distance(preview.GetPosition(0), preview.GetPosition(1));
                Assert(previewLength > 0.9f && previewLength < 1.1f &&
                       charging.State == ChargingEnemyState.Windup,
                    "The preview must end at body contact while retaining the committed windup.");
                charging.TickBehavior(5.1f + 0.65f);
                Assert(charging.State == ChargingEnemyState.Dashing && body.linearVelocity.x > 0f,
                    "Cover appearing during windup must not cancel the committed dash.");
                charging.TryResolveCollision(obstacle.GetComponent<Collider2D>(), Vector2.left, 5.8f);
                Assert(charging.State == ChargingEnemyState.Recovering,
                    "Only actual frontal contact must stop the committed dash.");
                Assert(obstacle != null, "The blocking obstacle must remain for the dash check.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateMinimumChargeDistance()
        {
            GameObject root = new("Minimum Charge Distance");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.right * 1.9f);
                CreateEnemy<ChargingEnemyController>(root.transform, out Rigidbody2D body,
                    out ChargingEnemyController charging);
                charging.Configure(10f, 0.1f, 9f, 0.8f, 0.6f, 0f, EnemyDamageTier.Heavy);
                charging.SetTarget(player.transform);
                Physics2D.SyncTransforms();
                charging.TickBehavior(0f);
                Assert(charging.State != ChargingEnemyState.Windup,
                    "Less than one unit of body-to-body clearance must not start windup.");
                player.transform.position = Origin + Vector2.right * 2.1f;
                Physics2D.SyncTransforms();
                charging.TickBehavior(0.1f);
                Assert(charging.State == ChargingEnemyState.Windup,
                    "More than one unit of clear travel must allow windup.");
                player.transform.position = Origin + Vector2.right * 1.1f;
                Physics2D.SyncTransforms();
                charging.TickBehavior(0.21f);
                Assert(charging.State == ChargingEnemyState.Dashing,
                    "A target approaching after commitment must not cancel the dash.");
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        private static void ValidateChargingCollisionAngles()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ChargingEnemy.prefab");
            GameObject instance = Object.Instantiate(prefab, Origin, Quaternion.identity);
            try
            {
                EnemyAttackPresentation visual = instance.GetComponent<EnemyAttackPresentation>();
                Vector3 initialScale = instance.transform.localScale;
                Physics2D.SyncTransforms();
                Vector3 initialBounds = instance.GetComponent<Collider2D>().bounds.size;
                foreach (EnemyAttackPhase phase in Enum.GetValues(typeof(EnemyAttackPhase)))
                {
                    visual.SetPhase(phase);
                    Physics2D.SyncTransforms();
                    Assert(instance.transform.localScale == initialScale &&
                           instance.GetComponent<Collider2D>().bounds.size == initialBounds,
                        "The charging prefab must retain the same body size throughout every attack phase.");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            MethodInfo enterHandler = typeof(ChargingEnemyController).GetMethod("OnCollisionEnter2D",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Collision2D) }, null);
            MethodInfo stayHandler = typeof(ChargingEnemyController).GetMethod("OnCollisionStay2D",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Collision2D) }, null);
            Assert(enterHandler != null && stayHandler != null,
                "The charger must resolve both new and persistent collision contacts while dashing.");

            GameObject root = new("Obstacle-0 Charging Collision Angle Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.right * 4f);
                CreateEnemy<ChargingEnemyController>(root.transform, out Rigidbody2D body,
                    out ChargingEnemyController charging);
                charging.Configure(10f, 0.1f, 9f, 0.8f, 0.6f, 0f, EnemyDamageTier.Heavy);
                charging.SetTarget(player.transform);
                charging.TickBehavior(0f);
                charging.TickBehavior(0.1f);
                Assert(charging.State == ChargingEnemyState.Dashing && charging.LockedDirection.x > 0.99f,
                    "The collision-angle check requires a rightward dash.");

                GameObject obstacle = CreateObstacle(root.transform, Origin + Vector2.right * 20f);
                Collider2D obstacleCollider = obstacle.GetComponent<Collider2D>();
                Assert(charging.TryResolveCollision(obstacleCollider, Vector2.right, 0.15f) &&
                       charging.State == ChargingEnemyState.Dashing && charging.LockedDirection == Vector2.right,
                    "A retained wall contact must not cancel or redirect a dash moving away from the wall.");
                Assert(charging.TryResolveCollision(obstacleCollider, new Vector2(0.5f, 0.8660254f), 0.16f) &&
                       charging.State == ChargingEnemyState.Dashing && charging.LockedDirection == Vector2.right,
                    "Diagonal separation from a wall must preserve the original dash direction.");
                float tangentComponent = Mathf.Cos(ChargingEnemyController.GlancingCollisionAngle * Mathf.Deg2Rad);
                Vector2 thresholdNormal = new(-tangentComponent,
                    Mathf.Sqrt(1f - tangentComponent * tangentComponent));
                Assert(charging.TryResolveCollision(obstacleCollider, thresholdNormal, 0.2f) &&
                       charging.State == ChargingEnemyState.Dashing &&
                       Mathf.Abs(body.linearVelocity.magnitude - charging.DashSpeed) < 0.01f &&
                       Vector2.Dot(body.linearVelocity.normalized, thresholdNormal) < 0.001f,
                    "A glancing obstacle contact must preserve the dash along the wall tangent.");
                Assert(charging.LockedDirection == Vector2.right,
                    "Sliding must not overwrite the aimed charge direction.");
                Vector2 firstSlideVelocity = body.linearVelocity;
                Assert(charging.TryResolveCollision(obstacleCollider, thresholdNormal, 0.3f) &&
                       charging.State == ChargingEnemyState.Dashing &&
                       Vector2.Dot(body.linearVelocity.normalized, firstSlideVelocity.normalized) > 0.999f,
                    "A persistent glancing contact must keep a stable tangent instead of stopping or oscillating.");
                charging.TickBehavior(0.35f);
                Assert(Vector2.Dot(body.linearVelocity.normalized, firstSlideVelocity.normalized) > 0.999f,
                    "The tick after a reported contact must keep sliding along the wall.");
                charging.TickBehavior(0.4f);
                Assert(charging.State == ChargingEnemyState.Dashing &&
                       Vector2.Dot(body.linearVelocity.normalized, Vector2.right) > 0.999f &&
                       Mathf.Abs(body.linearVelocity.magnitude - charging.DashSpeed) < 0.01f,
                    "Once the wall contact ends, the dash must resume its original aimed direction.");

                charging.TickBehavior(0.91f);
                Assert(charging.State == ChargingEnemyState.Recovering && body.linearVelocity == Vector2.zero,
                    "Sliding must not extend the original dash time or distance budget.");

                Object.DestroyImmediate(obstacle);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(body.gameObject);
                Physics2D.SyncTransforms();

                player = CreatePlayer(root.transform, Origin + new Vector2(4f, 10f));
                CreateEnemy<ChargingEnemyController>(root.transform, out body, out charging);
                body.transform.position = Origin + Vector2.up * 10f;
                charging.Configure(10f, 0.1f, 9f, 0.8f, 0.6f, 0f, EnemyDamageTier.Heavy);
                charging.SetTarget(player.transform);
                charging.TickBehavior(1f);
                charging.TickBehavior(1.1f);
                obstacle = CreateObstacle(root.transform, Origin + new Vector2(20f, 10f));
                obstacleCollider = obstacle.GetComponent<Collider2D>();
                Assert(charging.TryResolveCollision(obstacleCollider, Vector2.left, 1.2f) &&
                       charging.State == ChargingEnemyState.Recovering && body.linearVelocity == Vector2.zero,
                    "A frontal obstacle impact must end the dash immediately instead of sliding.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateSniper()
        {
            GameObject root = new("Obstacle-0 Sniper Verification");
            List<EnemyProjectile> fired = new();
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.right * 8f);
                CreateEnemy<LongRangeSniperController>(root.transform, out Rigidbody2D body,
                    out LongRangeSniperController sniper);
                sniper.SetTarget(player.transform);
                sniper.ProjectileFired += fired.Add;
                GameObject obstacle = CreateObstacle(root.transform, Origin + Vector2.right * 3f);
                Physics2D.SyncTransforms();

                sniper.TickBehavior(0f);
                sniper.TickBehavior(5f);
                Assert(sniper.State == LongRangeSniperState.Relocating && body.linearVelocity.sqrMagnitude > 0.5f &&
                       fired.Count == 0,
                    "A sniper whose line of fire is blocked must keep relocating instead of aiming.");

                Object.DestroyImmediate(obstacle);
                Physics2D.SyncTransforms();
                sniper.TickBehavior(5.1f);
                Assert(sniper.State == LongRangeSniperState.Aiming,
                    "A sniper must start aiming once its relocation time is over and the line of fire is clear.");

                obstacle = CreateObstacle(root.transform, Origin + Vector2.right * 3f);
                Physics2D.SyncTransforms();
                sniper.TickBehavior(10f);
                Assert(sniper.State == LongRangeSniperState.Relocating && fired.Count == 0,
                    "A sniper whose aim line is blocked when the aim ends must not fire.");
                Assert(obstacle != null, "The blocking obstacle must remain for the fire check.");
            }
            finally
            {
                foreach (EnemyProjectile projectile in fired.Where(projectile => projectile != null))
                    Object.DestroyImmediate(projectile.gameObject);
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        internal static GameObject CreateObstacle(Transform parent, Vector2 position)
        {
            GameObject obstacle = new("Obstacle-0 Box");
            obstacle.transform.SetParent(parent);
            obstacle.transform.position = position;
            obstacle.layer = LayerMask.NameToLayer("Environment");
            obstacle.AddComponent<BoxCollider2D>().size = Vector2.one;
            return obstacle;
        }

        internal static GameObject CreatePlayer(Transform parent, Vector2 position)
        {
            GameObject player = new("Obstacle-0 Player");
            player.transform.SetParent(parent);
            player.transform.position = position;
            player.layer = LayerMask.NameToLayer("Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            Health health = player.AddComponent<Health>();
            player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerActionState>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerCombatEvents>();
            InvokeLifecycle(health, "Awake");
            return player;
        }

        internal static GameObject CreateEnemy<T>(Transform parent, out Rigidbody2D body, out T controller)
            where T : MonoBehaviour
        {
            GameObject enemy = new($"Obstacle-0 {typeof(T).Name}");
            enemy.transform.SetParent(parent);
            enemy.transform.position = Origin;
            enemy.layer = LayerMask.NameToLayer("Enemy");
            body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            enemy.AddComponent<SpriteRenderer>();
            enemy.AddComponent<CircleCollider2D>().radius = EnemyRadius;
            Health health = enemy.AddComponent<Health>();
            KnockbackReceiver knockback = enemy.AddComponent<KnockbackReceiver>();
            controller = enemy.AddComponent<T>();
            Physics2D.SyncTransforms();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(knockback, "Awake");
            InvokeLifecycle(controller, "Awake");
            return enemy;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
