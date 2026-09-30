using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week18Obstacle1Verification
    {
        // Far from every authored scene so only the verification colliders take part in physics queries.
        private static readonly Vector2 Origin = new(-5000f, 5000f);
        private const int DistributionSeedCount = 200000;
        private const float ChanceTolerance = 0.003f;
        private const float ShareTolerance = 0.025f;

        [MenuItem("Trickal Fan Game/Week 18/Setup and Verify Obstacle-1 Destructible Obstacle")]
        public static void SetupAndVerifyBatch()
        {
            Week18Obstacle1Setup.EnsurePrefab();
            string prefabGuid = AssetDatabase.AssetPathToGUID(Week18Obstacle1Setup.PrefabPath);
            string tableGuid = AssetDatabase.AssetPathToGUID(Week18Obstacle1Setup.DropTablePath);
            DestructibleObstacle prefab = Week18Obstacle1Setup.EnsurePrefab();
            Assert(prefabGuid == AssetDatabase.AssetPathToGUID(Week18Obstacle1Setup.PrefabPath) &&
                   tableGuid == AssetDatabase.AssetPathToGUID(Week18Obstacle1Setup.DropTablePath),
                "Rerunning Obstacle-1 setup must keep the prefab and drop table GUIDs.");
            Assert(prefab.GetComponents<DestructibleObstacle>().Length == 1 &&
                   prefab.GetComponents<BoxCollider2D>().Length == 1 &&
                   prefab.GetComponentsInChildren<SpriteRenderer>(true).Length == 1,
                "Rerunning Obstacle-1 setup must not duplicate components or visuals.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Verify();
            // Resource-3 shares the drop table code; its room clear table must still hold. It leaves the Game Scene
            // open, which the high-grade skill check needs for its configured player after the ultimate change.
            Week17Resource3Verification.Verify();
            Week7HighGradeSkillVerification.Verify();
        }

        [MenuItem("Trickal Fan Game/Week 18/Verify Obstacle-1 Destructible Obstacle")]
        public static void Verify()
        {
            ResourceDropTable table = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week18Obstacle1Setup.DropTablePath);
            ValidatePrefab(table);
            ValidateLayerBlocking();
            ValidateDropTable(table);
            ValidateHitCounting();
            ValidatePlayerAttackSources();
            ValidateNonPlayerSourcesDoNotBreak();
            ValidateDropOnceAndRevisit(table);
            Debug.Log("Obstacle-1 verification passed: the 1x1 Environment obstacle blocks players, enemies, " +
                      "projectiles and charges, breaks on exactly the 4th player attack or skill hit regardless of " +
                      "attack power, ignores enemy projectiles and charges, rolls its seeded 3% drop (heart 25 / SP " +
                      "25 / key 10 / bomb 8 / elif 30 / pit 2, pit re-weighted without a secret room) once, and stays broken " +
                      "without a second drop when the room is rebuilt from its state.");
        }

        private static void ValidatePrefab(ResourceDropTable table)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week18Obstacle1Setup.PrefabPath);
            Assert(prefab != null && table != null, "Run Obstacle-1 setup before verification.");
            DestructibleObstacle obstacle = prefab.GetComponent<DestructibleObstacle>();
            BoxCollider2D collider = prefab.GetComponent<BoxCollider2D>();
            Assert(obstacle != null && collider != null && !collider.isTrigger && collider.size == Vector2.one &&
                   prefab.transform.localScale == Vector3.one,
                "The destructible obstacle must be a solid 1x1 box.");
            Assert(prefab.layer == LayerMask.NameToLayer("Environment"),
                "The destructible obstacle must use the Environment layer so everything treats it as terrain.");
            bool valid = obstacle.TryValidate(out string error);
            Assert(obstacle.RequiredHits == DestructibleObstacle.DefaultRequiredHits && obstacle.RequiredHits == 4 &&
                   obstacle.DropTable == table && valid,
                $"The destructible obstacle must need 4 hits and use the basic drop table. {error}");
            Assert(prefab.GetComponentInChildren<Health>(true) == null &&
                   prefab.GetComponent<Rigidbody2D>() == null,
                "The destructible obstacle must break by hit count, not Health, and never move.");
        }

        private static void ValidateLayerBlocking()
        {
            int environment = LayerMask.NameToLayer("Environment");
            foreach (string layerName in new[] { "Player", "Enemy", "PlayerProjectile", HealthPickup.LayerName })
            {
                int layer = LayerMask.NameToLayer(layerName);
                Assert(layer >= 0 && !Physics2D.GetIgnoreLayerCollision(environment, layer),
                    $"Environment obstacles must collide with the {layerName} layer.");
            }

            GameObject root = new("Obstacle-1 Blocking Verification");
            try
            {
                DestructibleObstacle obstacle = CreateObstacle(root.transform, Origin + Vector2.right * 2f);
                Physics2D.SyncTransforms();
                Assert(!EnemyObstacleNavigator.HasClearPath(Origin, Origin + Vector2.right * 4f, 0.4f) &&
                       !EnemyObstacleNavigator.HasLineOfFire(Origin, Origin + Vector2.right * 4f),
                    "Enemies must treat the destructible obstacle as blocking movement and line of fire.");
                Assert(obstacle != null, "The obstacle must exist for the blocking check.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateDropTable(ResourceDropTable table)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(Mathf.Approximately(table.DropChance, 0.03f), "The basic obstacle must drop 3% of the time.");
            Assert(table.Entries.Select(entry => (entry.DropId, entry.Weight))
                    .SequenceEqual(Week18Obstacle1Setup.DropWeights),
                "Obstacle drops must be heart 25 / sp 25 / key 10 / bomb 8 / elif 30 / pit 2.");
            Dictionary<string, ResourceDropEntry> byId = table.Entries.ToDictionary(entry => entry.DropId);
            Assert(byId["heart"].Prefab.GetComponent<HealthPickup>() != null &&
                   byId["sp"].Prefab.GetComponent<SPPickup>() != null &&
                   byId["key"].Prefab.GetComponent<RunResourcePickup>().ResourceType == RunResourceType.Key &&
                   byId["bomb"].Prefab.GetComponent<RunResourcePickup>().ResourceType == RunResourceType.Bomb &&
                   byId["elif"].Prefab.GetComponent<RunResourcePickup>().ResourceType == RunResourceType.Elif,
                "Obstacle drop candidates must spawn their matching pickups.");
            Assert(byId["pit"].Prefab != null && byId["pit"].Prefab.GetComponent<SecretPit>() != null,
                "The pit candidate must spawn the Special-3 secret pit.");

            Dictionary<string, int> counts = table.Entries.ToDictionary(entry => entry.DropId, _ => 0);
            int drops = 0;
            for (int index = 0; index < DistributionSeedCount; index++)
            {
                int seed = DestructibleObstacle.DeriveDropSeed(index, "obstacle-01");
                if (!table.TryRoll(seed, out ResourceDropEntry entry)) continue;
                drops++;
                counts[entry.DropId]++;
            }

            float rate = drops / (float)DistributionSeedCount;
            Assert(Mathf.Abs(rate - table.DropChance) <= ChanceTolerance,
                $"Obstacle drop rate {rate:P2} must stay near {table.DropChance:P0}.");
            int totalWeight = table.Entries.Sum(entry => entry.Weight);
            foreach (ResourceDropEntry entry in table.Entries)
            {
                float share = counts[entry.DropId] / (float)drops;
                float expected = entry.Weight / (float)totalWeight;
                Assert(counts[entry.DropId] > 0 && Mathf.Abs(share - expected) <= ShareTolerance,
                    $"Obstacle drop '{entry.DropId}' share {share:P1} must stay near {expected:P0}.");
            }

            Assert(DestructibleObstacle.DeriveDropSeed(42, "obstacle-01") ==
                   DestructibleObstacle.DeriveDropSeed(42, "obstacle-01") &&
                   DestructibleObstacle.DeriveDropSeed(42, "obstacle-01") !=
                   DestructibleObstacle.DeriveDropSeed(42, "obstacle-02") &&
                   DestructibleObstacle.DeriveDropSeed(42, "obstacle-01") !=
                   DestructibleObstacle.DeriveDropSeed(43, "obstacle-01"),
                "Obstacle drop seeds must replay per room seed and obstacle ID and differ between them.");
        }

        private static void ValidateHitCounting()
        {
            GameObject root = new("Obstacle-1 Hit Verification");
            try
            {
                DestructibleObstacle obstacle = CreateObstacle(root.transform, Origin);
                for (int hit = 1; hit < DestructibleObstacle.DefaultRequiredHits; hit++)
                {
                    Assert(obstacle.RegisterPlayerHit() && obstacle.HitsTaken == hit && !obstacle.IsBroken &&
                           obstacle.gameObject.activeSelf,
                        $"Hit {hit} must count without breaking the obstacle.");
                }

                Assert(obstacle.RegisterPlayerHit() && obstacle.IsBroken && !obstacle.gameObject.activeSelf,
                    "The final hit must break the obstacle and remove it from the room.");
                Assert(!obstacle.RegisterPlayerHit() && obstacle.HitsTaken == DestructibleObstacle.DefaultRequiredHits,
                    "A broken obstacle must ignore further hits.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidatePlayerAttackSources()
        {
            GameObject root = new("Obstacle-1 Player Source Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                stats.AddAttackDamage(10000);

                // Melee: the default aim is down, so the obstacle sits one unit below the player.
                PlayerAttack attack = player.AddComponent<PlayerAttack>();
                SerializedObject serializedAttack = new(attack);
                serializedAttack.FindProperty("targetLayers").intValue = 1 << LayerMask.NameToLayer("Enemy");
                serializedAttack.ApplyModifiedPropertiesWithoutUndo();
                InvokeLifecycle(attack, "Awake");
                DestructibleObstacle meleeTarget = CreateObstacle(root.transform, Origin + Vector2.down);
                Physics2D.SyncTransforms();
                for (int swing = 1; swing < DestructibleObstacle.DefaultRequiredHits; swing++) Invoke(attack, "DealDamage");
                Assert(meleeTarget.HitsTaken == DestructibleObstacle.DefaultRequiredHits - 1 && !meleeTarget.IsBroken,
                    "Each melee swing must count one hit, and huge attack power must not break it early.");
                Invoke(attack, "DealDamage");
                Assert(meleeTarget.IsBroken, "The final melee swing must break the obstacle.");

                // Basic projectile: one hit, then the projectile stops like at a wall.
                DestructibleObstacle projectileTarget = CreateObstacle(root.transform, Origin + Vector2.right * 3f);
                Physics2D.SyncTransforms();
                GameObject shot = new("Obstacle-1 Player Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D));
                shot.transform.SetParent(root.transform);
                Projectile projectile = shot.AddComponent<Projectile>();
                Invoke(projectile, "Hit", projectileTarget.GetComponent<Collider2D>());
                Assert(projectileTarget.HitsTaken == 1 && shot == null,
                    "A player projectile must count one hit and stop at the obstacle.");

                // Lower-grade skill: an untargeted shot stops at the obstacle with one hit, an explosion hits the
                // obstacles in its radius, and a targeted shot that touches then explodes still counts once.
                DestructibleObstacle skillTarget = CreateObstacle(root.transform, Origin + Vector2.left * 3f);
                Physics2D.SyncTransforms();
                HomingSkillProjectile contactShot = CreateSkillShot(root.transform, player, null);
                Invoke(contactShot, "OnTriggerEnter2D", skillTarget.GetComponent<Collider2D>());
                Assert(skillTarget.HitsTaken == 1,
                    "An untargeted skill projectile must count one hit when it stops at the obstacle.");

                CreateSkillShot(root.transform, player, null).ExplodeNow();
                Assert(skillTarget.HitsTaken == 2, "A skill explosion must count one hit on obstacles in its radius.");

                GameObject skillEnemy = new("Obstacle-1 Skill Enemy", typeof(CircleCollider2D));
                skillEnemy.transform.SetParent(root.transform);
                skillEnemy.transform.position = Origin + Vector2.left * 20f;
                skillEnemy.layer = LayerMask.NameToLayer("Enemy");
                Health skillEnemyHealth = skillEnemy.AddComponent<Health>();
                InvokeLifecycle(skillEnemyHealth, "Awake");
                HomingSkillProjectile targetedShot = CreateSkillShot(root.transform, player, skillEnemyHealth);
                Invoke(targetedShot, "OnTriggerEnter2D", skillTarget.GetComponent<Collider2D>());
                targetedShot.ExplodeNow();
                Assert(skillTarget.HitsTaken == 3,
                    "A skill projectile must count one hit per obstacle across contact and explosion.");

                // High-grade ultimate impact area.
                DestructibleObstacle ultimateTarget = CreateObstacle(root.transform, Origin + Vector2.up * 1.2f);
                Physics2D.SyncTransforms();
                PlayerUltimate ultimate = player.AddComponent<PlayerUltimate>();
                InvokeLifecycle(ultimate, "Awake");
                Invoke(ultimate, "ApplyImpact", new object[] { null });
                Assert(ultimateTarget.HitsTaken == 1,
                    "The ultimate impact must count one hit on obstacles inside its radius.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateNonPlayerSourcesDoNotBreak()
        {
            GameObject root = new("Obstacle-1 Enemy Source Verification");
            try
            {
                DestructibleObstacle obstacle = CreateObstacle(root.transform, Origin + Vector2.up * 3f);
                Collider2D obstacleCollider = obstacle.GetComponent<Collider2D>();
                Physics2D.SyncTransforms();

                GameObject enemy = new("Obstacle-1 Enemy", typeof(CircleCollider2D));
                enemy.transform.SetParent(root.transform);
                EnemyProjectile enemyShot = EnemyProjectile.Create(Origin, Vector2.up, enemy,
                    EnemyDamageTier.Light, 5f, 4f, null);
                Assert(enemyShot.TryHit(obstacleCollider) && enemyShot == null && obstacle.HitsTaken == 0,
                    "An enemy projectile must be stopped by the obstacle without counting a hit.");

                GameObject player = CreatePlayer(root.transform, Origin + Vector2.right * 5f);
                GameObject chargingObject = new("Obstacle-1 Charger", typeof(Rigidbody2D), typeof(CircleCollider2D));
                chargingObject.transform.SetParent(root.transform);
                chargingObject.transform.position = Origin;
                chargingObject.layer = LayerMask.NameToLayer("Enemy");
                Health chargingHealth = chargingObject.AddComponent<Health>();
                KnockbackReceiver knockback = chargingObject.AddComponent<KnockbackReceiver>();
                ChargingEnemyController charging = chargingObject.AddComponent<ChargingEnemyController>();
                Physics2D.SyncTransforms();
                InvokeLifecycle(chargingHealth, "Awake");
                InvokeLifecycle(knockback, "Awake");
                InvokeLifecycle(charging, "Awake");
                charging.Configure(10f, 0.65f, 9f, 0.8f, 0.6f, 1.5f, EnemyDamageTier.Heavy);
                charging.SetTarget(player.transform);
                charging.TickBehavior(0f);
                charging.TickBehavior(0.65f);
                Assert(charging.State == ChargingEnemyState.Dashing, "The charger must be dashing for the check.");
                Assert(charging.TryResolveCollision(obstacleCollider, 0.7f) &&
                       charging.State == ChargingEnemyState.Recovering && obstacle.HitsTaken == 0,
                    "A charge must stop at the obstacle and recover without counting a hit.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateDropOnceAndRevisit(ResourceDropTable table)
        {
            const string obstacleId = Week18Obstacle1Setup.DefaultObstacleId;
            int keyRoomSeed = FindRoomSeed(table, obstacleId, entry => entry != null && entry.DropId == "key");
            int pitRoomSeed = FindRoomSeed(table, obstacleId, entry => entry != null && entry.DropId == "pit");
            int emptyRoomSeed = FindRoomSeed(table, obstacleId, entry => entry == null);

            GameObject root = new("Obstacle-1 Drop Verification");
            GameObject progressHolder = new("Obstacle-1 Drop Progress");
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.right * 6f);

                RoomRunState keyState = new("floor-01-room-02");
                DestructibleObstacle keyObstacle = CreateObstacle(root.transform, Origin);
                keyObstacle.Bind(keyState, keyRoomSeed, root.transform, progress);
                BreakWithHits(keyObstacle);
                RunResourcePickup key = keyObstacle.LastDrop != null
                    ? keyObstacle.LastDrop.GetComponent<RunResourcePickup>()
                    : null;
                Assert(key != null && keyState.IsObstacleDestroyed(obstacleId),
                    "Breaking the obstacle must record it and drop the seed's key once.");
                InvokeLifecycle(key, "Awake");
                Assert(key.TryCollectFrom(player.GetComponent<Collider2D>()) &&
                       progress.GetResourceCount(RunResourceType.Key) == 1,
                    "The dropped key must be granted to the bound Run.");

                DestructibleObstacle rebuilt = CreateObstacle(root.transform, Origin);
                rebuilt.Bind(keyState, keyRoomSeed, root.transform, progress);
                Assert(rebuilt.IsBroken && !rebuilt.gameObject.activeSelf && !rebuilt.RegisterPlayerHit() &&
                       rebuilt.LastDrop == null,
                    "A rebuilt room must keep the obstacle broken and never drop again.");

                RoomRunState replayState = new("floor-01-room-02");
                DestructibleObstacle replay = CreateObstacle(root.transform, Origin);
                replay.Bind(replayState, keyRoomSeed, root.transform, progress);
                BreakWithHits(replay);
                Assert(replay.LastDrop != null && replay.LastDrop.name.Contains("key"),
                    "The same room seed and obstacle ID must replay the same drop in another Run.");

                RoomRunState pitState = new("floor-01-room-03");
                DestructibleObstacle pitObstacle = CreateObstacle(root.transform, Origin);
                pitObstacle.Bind(pitState, pitRoomSeed, root.transform, progress);
                BreakWithHits(pitObstacle);
                Assert(pitObstacle.IsBroken && pitState.IsObstacleDestroyed(obstacleId) &&
                       (pitObstacle.LastDrop == null || pitObstacle.LastDrop.GetComponent<SecretPit>() == null),
                    "Without a secret room on the floor, a pit roll must re-weight to another candidate, not a pit.");

                RoomRunState emptyState = new("floor-01-room-04");
                DestructibleObstacle emptyObstacle = CreateObstacle(root.transform, Origin);
                emptyObstacle.Bind(emptyState, emptyRoomSeed, root.transform, progress);
                BreakWithHits(emptyObstacle);
                Assert(emptyObstacle.IsBroken && emptyObstacle.LastDrop == null,
                    "Most obstacles must break without a drop.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(progressHolder);
                Physics2D.SyncTransforms();
            }
        }

        private static void BreakWithHits(DestructibleObstacle obstacle)
        {
            for (int hit = 0; hit < obstacle.RequiredHits; hit++) obstacle.RegisterPlayerHit();
            Assert(obstacle.IsBroken, "The obstacle must break after its required hits.");
        }

        private static int FindRoomSeed(ResourceDropTable table, string obstacleId,
            Func<ResourceDropEntry, bool> predicate)
        {
            for (int roomSeed = 1; roomSeed < 1000000; roomSeed++)
            {
                table.TryRoll(DestructibleObstacle.DeriveDropSeed(roomSeed, obstacleId), out ResourceDropEntry entry);
                if (predicate(entry)) return roomSeed;
            }

            throw new InvalidOperationException("No room seed matched the obstacle drop verification case.");
        }

        private static HomingSkillProjectile CreateSkillShot(Transform parent, GameObject player, Health target)
        {
            GameObject shot = new("Obstacle-1 Skill Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D));
            shot.transform.SetParent(parent);
            shot.transform.position = Origin + Vector2.left * 2.3f;
            HomingSkillProjectile skill = shot.AddComponent<HomingSkillProjectile>();
            skill.Launch(Vector2.left, player.GetComponent<Health>(), target,
                new DamageContext(player, DamageSourceType.PlayerProjectile, 1),
                1 << LayerMask.NameToLayer("Enemy"));
            return skill;
        }

        private static DestructibleObstacle CreateObstacle(Transform parent, Vector2 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week18Obstacle1Setup.PrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            return instance.GetComponent<DestructibleObstacle>();
        }

        private static GameObject CreatePlayer(Transform parent, Vector2 position)
        {
            GameObject player = new("Obstacle-1 Player");
            player.transform.SetParent(parent);
            player.transform.position = position;
            player.layer = LayerMask.NameToLayer("Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            Health health = player.AddComponent<Health>();
            PlayerStats stats = player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerActionState>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerCombatEvents>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(stats, "Awake");
            return player;
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null) throw new MissingMethodException(target.GetType().FullName, methodName);
            method.Invoke(target, arguments);
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
