using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week23Enemy6Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-5600f, 5600f);

        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Enemy-6 Jyubi")]
        public static void SetupAndVerifyBatch()
        {
            Week23Enemy6Setup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(Week23Enemy6Setup.PrefabPath);
            Week23Enemy6Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) && guid == AssetDatabase.AssetPathToGUID(Week23Enemy6Setup.PrefabPath),
                "Enemy-6 setup changed or lost the Jyubi Prefab GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Enemy-6 Jyubi")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week23Enemy6Setup.PrefabPath);
            Assert(prefab != null, "Run Enemy-6 setup first: the Jyubi Prefab is missing.");
            ValidatePrefab(prefab);
            ValidateHalfHeartDamage(prefab);
            ValidateOneHitKill(prefab);
            ValidateFlight(prefab);
            ValidateDirectChase(prefab);
            ValidateRoomSpawn(prefab);
            ValidateProjectileHitsOneOverlappingEnemy(prefab);
            Debug.Log("Enemy-6 verification passed: setup is idempotent, Jyubi is a small Enemy-layer body with 1 " +
                      "health that any player hit kills on every floor, its touch takes exactly half a heart on " +
                      "floors 1 and 3 while other enemy damage keeps the one-heart minimum, it is faster than the " +
                      "player's base speed, it ignores pits and low obstacles but not trees or the player, it heads " +
                      "straight for its target past an obstacle, and a room registers it as an extra enemy that " +
                      "counts as a kill without changing the room state, and a player shot without pierce " +
                      "hits only one of several overlapping enemies while each pierce adds exactly one more.");
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Enemy-6 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week23Obstacle6Verification.VerifyWithRegressionsBatch();
            Hp1HealthUnitsVerification.Verify();
            Week21Range0Verification.Verify();
            Debug.Log("Enemy-6 regression verification passed.");
        }

        private static void ValidatePrefab(GameObject prefab)
        {
            Health health = prefab.GetComponent<Health>();
            EnemyChase chase = prefab.GetComponent<EnemyChase>();
            ContactDamage contact = prefab.GetComponent<ContactDamage>();
            Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
            Assert(prefab.layer == LayerMask.NameToLayer("Enemy") && health != null && chase != null &&
                   contact != null && body != null && prefab.GetComponent<EnemyFlight>() != null &&
                   prefab.GetComponent<EnemyBehaviorContext>() != null &&
                   prefab.GetComponent<KnockbackReceiver>() != null && prefab.GetComponent<TestEnemy>() != null &&
                   prefab.GetComponent<Collider2D>() != null && !prefab.GetComponent<Collider2D>().isTrigger,
                "Jyubi must be a solid Enemy-layer body with health, chase, contact damage and flight.");
            Assert(prefab.GetComponent<MeleeEnemyAttack>() == null &&
                   prefab.GetComponent<EnemyMovementAnimator>() == null &&
                   prefab.GetComponent<EnemyAttackArtwork>() == null &&
                   GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0,
                "Jyubi must not keep the ground chaser's melee attack or animation, or a missing script.");
            Assert(Mathf.Approximately(health.MaxHealth, Week23Enemy6Setup.MaxHealth) &&
                   Mathf.Approximately(health.MaxHealth, 1f),
                "Jyubi must have 1 health.");
            Assert(chase.Flies && Mathf.Approximately(chase.MoveSpeed, Week23Enemy6Setup.MoveSpeed) &&
                   Mathf.Approximately(chase.StopDistance, 0f),
                "Jyubi must fly straight into its target at its configured speed.");
            Assert(contact.HalfHeartDamage && Mathf.Approximately(contact.Cooldown, Week23Enemy6Setup.ContactCooldown),
                "Jyubi's touch must deal half a heart.");
            Assert(Mathf.Approximately(body.gravityScale, 0f) &&
                   Mathf.Approximately(prefab.transform.localScale.x, Week23Enemy6Setup.Scale) &&
                   prefab.transform.localScale.x < 1f,
                "Jyubi must be a small body without gravity.");

            PlayerStats player = Object.FindFirstObjectByType<PlayerStats>();
            Assert(player != null, "Enemy-6 verification needs the Game Scene player.");
            Assert(chase.MoveSpeed > player.MoveSpeed && chase.MoveSpeed <= player.MoveSpeed * 1.25f,
                $"Jyubi (speed {chase.MoveSpeed}) must be a little faster than the player ({player.MoveSpeed}).");
        }

        private static void ValidateHalfHeartDamage(GameObject prefab)
        {
            Assert(HealthUnits.HalfHeartDamageUnits == 1 && HealthUnits.ToDamageUnits(1f) == 2 &&
                   HealthUnits.ToDamageUnits(1f, true) == 1 && HealthUnits.ToDamageUnits(3f, true) == 3 &&
                   HealthUnits.ToDamageUnits(0f, true) == 0,
                "Only a hit that allows half a heart may go below the one-heart minimum.");

            GameObject root = new("Enemy-6 Damage Verification");
            try
            {
                foreach (int floor in new[] { 1, 3 })
                {
                    Health player = CreatePlayer(root.transform);
                    GameObject jyubi = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    EnemyFloorLevel.Apply(jyubi, floor);
                    float before = player.CurrentHealth;
                    Assert(jyubi.GetComponent<ContactDamage>().TryApplyDamage(player, 0f) &&
                           Mathf.Approximately(before - player.CurrentHealth, 1f),
                        $"Jyubi's touch must take exactly half a heart (1 unit) on floor {floor}.");
                    Assert(!jyubi.GetComponent<ContactDamage>().TryApplyDamage(player, 0.5f),
                        "Jyubi's touch must respect its cooldown.");
                }

                Health other = CreatePlayer(root.transform);
                GameObject normal = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                normal.GetComponent<ContactDamage>().ConfigureHalfHeart(false);
                float otherBefore = other.CurrentHealth;
                Assert(normal.GetComponent<ContactDamage>().TryApplyDamage(other, 0f) &&
                       Mathf.Approximately(otherBefore - other.CurrentHealth,
                           HealthUnits.GetEnemyDamageUnits(EnemyDamageTier.Light, 1)),
                    "Contact damage without the half-heart option must keep the Light tier's one heart.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateOneHitKill(GameObject prefab)
        {
            PlayerStats player = Object.FindFirstObjectByType<PlayerStats>();
            GameObject root = new("Enemy-6 Health Verification");
            try
            {
                foreach (int floor in new[] { 1, 2, 3 })
                {
                    GameObject jyubi = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    Health health = jyubi.GetComponent<Health>();
                    InvokeAwake(health);
                    FloorDifficultyScaler.ApplyScaling(jyubi, floor);
                    Assert(health.CurrentHealth <= player.AttackDamage,
                        $"Jyubi's floor {floor} health {health.CurrentHealth} must not exceed one base attack.");
                    health.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, player.AttackDamage));
                    Assert(health.IsDead, $"One player hit must kill Jyubi on floor {floor}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateFlight(GameObject prefab)
        {
            Assert(!Physics2D.GetIgnoreLayerCollision(LayerMask.NameToLayer("Enemy"), LayerMask.NameToLayer("Player")),
                "Jyubi must still collide with the player to deal contact damage.");
            GameObject root = new("Enemy-6 Flight Verification");
            try
            {
                GameObject jyubi = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                jyubi.transform.position = Origin;
                Collider2D low = Solid(root.transform, "Low Obstacle", Origin + Vector2.right, false);
                Collider2D tree = Solid(root.transform, "Tree", Origin + Vector2.left, true);
                Collider2D wall = Solid(root.transform, "Wall", Origin + Vector2.up, false);
                Object.DestroyImmediate(wall.GetComponent<RoomStaticObstacle>());
                Physics2D.SyncTransforms();

                EnemyFlight flight = jyubi.GetComponent<EnemyFlight>();
                flight.ApplyToBody();
                flight.IgnoreNearbyLowObstacles();
                Assert((jyubi.GetComponent<Rigidbody2D>().excludeLayers.value & (1 << RoomPit.Layer)) != 0,
                    "Jyubi must fly over pits.");
                Assert(flight.IsIgnoring(low) && !flight.IsIgnoring(tree) && !flight.IsIgnoring(wall),
                    "Jyubi must pass low obstacles and still collide with trees and walls.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateDirectChase(GameObject prefab)
        {
            GameObject root = new("Enemy-6 Chase Verification");
            try
            {
                Health player = CreatePlayer(root.transform);
                player.transform.position = Origin + Vector2.right * 4f;
                Solid(root.transform, "Low Obstacle", Origin + Vector2.right * 2f, false);
                GameObject jyubi = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                jyubi.transform.position = Origin;
                InvokeAwake(jyubi.GetComponent<Health>());
                Physics2D.SyncTransforms();

                EnemyChase chase = jyubi.GetComponent<EnemyChase>();
                chase.SetTarget(player.transform);
                chase.TickChase();
                Vector2 velocity = jyubi.GetComponent<Rigidbody2D>().linearVelocity;
                Assert(Vector2.Distance(velocity, Vector2.right * Week23Enemy6Setup.MoveSpeed) < 0.01f,
                    $"Jyubi must head straight for its target over an obstacle, but moved {velocity}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateRoomSpawn(GameObject prefab)
        {
            GameObject root = new("Enemy-6 Room Verification", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin;
            try
            {
                RoomController room = root.AddComponent<RoomController>();
                RoomState before = room.State;
                Health jyubi = room.SpawnExtraEnemy(prefab, Origin);
                Assert(jyubi != null && jyubi.transform.parent == room.transform && room.AliveEnemyCount == 1 &&
                       room.State == before && jyubi.GetComponent<EnemyFloorLevel>() != null,
                    "A room must register an extra Jyubi under itself without changing its state.");
                jyubi.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, 10f));
                Assert(jyubi.IsDead && room.AliveEnemyCount == 0 && room.State == before,
                    "Killing an extra Jyubi must release it from the room without a clear.");
                Assert(room.SpawnExtraEnemy(null, Origin) == null, "A missing Prefab must not spawn an extra enemy.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // Overlapping enemies (a 쥬비 swarm) reach one projectile in the same physics step. Destroy is deferred in
        // play, so the spent projectile itself must refuse the later contacts.
        private static void ValidateProjectileHitsOneOverlappingEnemy(GameObject prefab)
        {
            GameObject projectilePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ErpinProjectileArtworkSetup.BasicPrefabPath);
            Assert(projectilePrefab != null && projectilePrefab.GetComponent<Projectile>() != null,
                "The player projectile Prefab is missing.");
            MethodInfo hit = typeof(Projectile).GetMethod("Hit", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(hit != null, "Projectile.Hit was renamed; update the overlapping enemy check.");

            foreach (int pierces in new[] { 0, 1 })
            {
                GameObject root = new("Enemy-6 Projectile Verification");
                try
                {
                    Health player = CreatePlayer(root.transform);
                    Health[] enemies = new Health[3];
                    for (int index = 0; index < enemies.Length; index++)
                    {
                        GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                        enemy.transform.position = Origin;
                        enemies[index] = enemy.GetComponent<Health>();
                        InvokeAwake(enemies[index]);
                    }

                    GameObject shot = (GameObject)PrefabUtility.InstantiatePrefab(projectilePrefab, root.transform);
                    shot.transform.position = Origin;
                    Projectile projectile = shot.GetComponent<Projectile>();
                    projectile.Launch(Vector2.right, player,
                        new DamageContext(player.gameObject, DamageSourceType.PlayerProjectile, 10f), pierces);
                    foreach (Health enemy in enemies)
                        hit.Invoke(projectile, new object[] { enemy.GetComponent<Collider2D>() });

                    int dead = 0;
                    foreach (Health enemy in enemies)
                        if (enemy.IsDead) dead++;
                    Assert(projectile.IsSpent && dead == pierces + 1 && enemies[0].IsDead && !enemies[^1].IsDead,
                        $"A shot with {pierces} pierce(s) must hit exactly {pierces + 1} of three overlapping " +
                        $"enemies, but hit {dead}.");
                }
                finally
                {
                    Object.DestroyImmediate(root);
                    Physics2D.SyncTransforms();
                }
            }
        }

        private static Health CreatePlayer(Transform parent)
        {
            GameObject player = new("Enemy-6 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
            player.transform.SetParent(parent);
            player.transform.position = Origin + Vector2.down * 20f;
            player.layer = LayerMask.NameToLayer("Player");
            PlayerFeet.Ensure(player, out _);
            player.GetComponent<Rigidbody2D>().gravityScale = 0f;
            Health health = player.GetComponent<Health>();
            InvokeAwake(health);
            health.EnableHealthUnits();
            return health;
        }

        private static Collider2D Solid(Transform parent, string label, Vector2 position, bool blocksFlight)
        {
            GameObject solid = new($"Enemy-6 {label}", typeof(BoxCollider2D), typeof(RoomStaticObstacle));
            solid.transform.SetParent(parent);
            solid.transform.position = position;
            solid.layer = LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName);
            RoomStaticObstacle obstacle = solid.GetComponent<RoomStaticObstacle>();
            obstacle.Configure("solid-01");
            obstacle.ConfigureHeight(blocksFlight);
            return solid.GetComponent<BoxCollider2D>();
        }

        private static void InvokeAwake(Health health)
        {
            typeof(Health).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(health, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
