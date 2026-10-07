using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week23Obstacle6Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-5400f, 5400f);
        private const int SeedCount = 8192;

        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Obstacle-6 Remaining Obstacles")]
        public static void SetupAndVerifyBatch()
        {
            Week23Obstacle6Setup.Setup();
            string[] paths = Week23Obstacle6Setup.CreatedAssetPaths()
                .Concat(Week23Obstacle5Setup.CreatedAssetPaths()).ToArray();
            Dictionary<string, string> guids = paths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Week23Obstacle6Setup.Setup();
            foreach (KeyValuePair<string, string> entry in guids)
                Assert(!string.IsNullOrWhiteSpace(entry.Value) && entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Obstacle-6 setup changed or lost the GUID for {entry.Key}.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Obstacle-6 Remaining Obstacles")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null && generator.RoomContentVersion >= Week23Obstacle5Setup.RoomContentVersion,
                "Run Obstacle-6 setup before verification.");
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(assembler != null, "Obstacle-6 verification needs the Game Scene assembler.");

            ObstacleVariantDefinition explosive = LoadVariant(Week23Obstacle5Setup.ExplosiveBox);
            ObstacleVariantDefinition random = LoadVariant(Week23Obstacle5Setup.ShadyRandomBox);
            ObstacleVariantDefinition collection = LoadVariant(Week23Obstacle5Setup.MayoCollectionBox);
            ObstacleVariantDefinition vault = LoadVariant(Week23Obstacle5Setup.SistVault);
            ObstacleVariantDefinition rock = LoadVariant(Week23Obstacle5Setup.GoldRock);

            ValidateBombRadius(explosive);
            ValidateExplosiveBox(explosive, vault, rock);
            ValidateRandomBox(random);
            ValidateCollectionBox(collection, assembler);
            ValidateFlyingVault(vault);
            ValidateTreeLayout(generator);
            Debug.Log("Obstacle-6 verification passed: setup is idempotent, the player bomb and exploding obstacles " +
                      "share one radius-2 explosion, the exploding box arms a 0.5-second fuse on its 2nd hit or a " +
                      "bomb, hurts enemies and the player, breaks obstacles, chains to the next box and opens a " +
                      "vault without dropping anything or exploding again after a rebuild, Shady's random box " +
                      "replays an explosion, five Jyubi, three pickups or nothing per seed, Mayo's collection box always " +
                      "leaves two pickups with an artifact a quarter of the time, a flying player opens a vault " +
                      "by overlapping it with a key, and basic-tree-grove passes the room contract with four " +
                      "trees while the central pillar stays a low obstacle. Obstacle-7 verifies the trees.");
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Obstacle-6 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week23Obstacle5Verification.VerifyWithRegressionsBatch();
            Week22Flight0Verification.Verify();
            Week22Chest0Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Obstacle-6 regression verification passed.");
        }

        private static ObstacleVariantDefinition LoadVariant(Week23Obstacle5Setup.VariantSpec spec)
        {
            ObstacleVariantDefinition variant = AssetDatabase.LoadAssetAtPath<ObstacleVariantDefinition>(spec.VariantPath);
            Assert(variant != null && variant.TryValidate(out _),
                $"Obstacle kind '{spec.VariantId}' is missing or invalid. Run Obstacle-6 setup.");
            return variant;
        }

        private static void ValidateBombRadius(ObstacleVariantDefinition explosive)
        {
            PlacedBomb bomb = AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special2Setup.PlacedBombPrefabPath)
                ?.GetComponent<PlacedBomb>();
            Assert(bomb != null && Mathf.Approximately(PlacedBomb.DefaultExplosionRadius, 2f) &&
                   Mathf.Approximately(bomb.ExplosionRadius, 2f),
                "The player bomb must explode with radius 2.");
            Assert(explosive.ExplosionPrefab == bomb,
                "An exploding obstacle must use the player bomb Prefab so both explosions stay the same.");
        }

        private static void ValidateExplosiveBox(ObstacleVariantDefinition explosive, ObstacleVariantDefinition vault,
            ObstacleVariantDefinition rockVariant)
        {
            GameObject root = new("Obstacle-6 Explosion Verification");
            GameObject progressHolder = new("Obstacle-6 Explosion Progress");
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                RoomRunState state = new("floor-01-room-02");
                DestructibleObstacle box = CreateObstacle(root.transform, explosive, Origin, "box-01");
                DestructibleObstacle chained = CreateObstacle(root.transform, explosive, Origin + Vector2.right * 1.5f,
                    "box-02");
                // Its nearest edge is 2.3 away: inside the old radius 2.5, outside the new radius 2.
                DestructibleObstacle farRock = CreateObstacle(root.transform, rockVariant, Origin + Vector2.left * 2.8f,
                    "rock-far");
                DestructibleObstacle nearRock = CreateObstacle(root.transform, rockVariant, Origin + Vector2.up * 1.5f,
                    "rock-near");
                DestructibleObstacle nearVault = CreateObstacle(root.transform, vault, Origin + Vector2.down * 1.5f,
                    "vault-near");
                foreach (DestructibleObstacle obstacle in new[] { box, chained, farRock, nearRock, nearVault })
                    obstacle.Bind(state, 11, root.transform, progress);
                Health enemy = CreateHealthTarget(root.transform, "Enemy", Origin + new Vector2(1f, 1f), 100f, "Enemy");
                Health farEnemy = CreateHealthTarget(root.transform, "Far Enemy", Origin + new Vector2(4f, 4f), 100f,
                    "Enemy");
                Health player = CreateHealthTarget(root.transform, "Player", Origin + new Vector2(-1f, -1f), 100f,
                    "Player");
                player.EnableHealthUnits();
                Physics2D.SyncTransforms();

                Assert(box.RequiredHits == Week23Obstacle5Setup.ExplosiveBoxHits && box.RequiredHits == 2 &&
                       box.TryRollExplosion() && box.RollDrops().Count == 0,
                    "The exploding box must need 2 hits, always explode and never roll a drop.");
                Assert(box.RegisterPlayerHit() && !box.IsBroken && box.LastExplosion == null,
                    "The first hit must not set the exploding box off.");
                Assert(box.RegisterPlayerHit() && box.IsBroken && state.IsObstacleDestroyed("box-01") &&
                       box.LastDrops.Count == 0,
                    "The second hit must break the exploding box without a pickup.");
                PlacedBomb explosion = box.LastExplosion;
                Assert(explosion != null && !explosion.HasExploded &&
                       Vector2.Distance(explosion.transform.position, box.transform.position) < 0.001f,
                    "A broken exploding box must arm one explosion on its cell.");
                float fuse = Week23Obstacle5Setup.ExplosionFuse;
                Assert(!explosion.Tick(explosion.ExplodeAt - fuse * 0.5f) && !explosion.HasExploded &&
                       !nearRock.IsBroken && Mathf.Approximately(enemy.CurrentHealth, 100f),
                    "The exploding box must wait for its fuse before it explodes.");

                float playerBefore = player.CurrentHealth;
                Assert(explosion.Tick(explosion.ExplodeAt) && explosion.HasExploded,
                    "The exploding box must explode when its fuse ends.");
                Assert(Mathf.Approximately(enemy.CurrentHealth, 100f - PlacedBomb.DefaultEnemyDamage) &&
                       Mathf.Approximately(farEnemy.CurrentHealth, 100f),
                    "The obstacle explosion must deal the bomb's enemy damage inside its radius only.");
                Assert(Mathf.Approximately(player.CurrentHealth, playerBefore - PlacedBomb.DefaultSelfDamage),
                    "The obstacle explosion must hurt a player in range like their own bomb does.");
                Assert(nearRock.IsBroken && !farRock.IsBroken,
                    "The obstacle explosion must break obstacles inside radius 2 and leave one 2.3 away.");
                Assert(nearVault.IsBroken && nearVault.LastDrops.Count >= vault.DropCount,
                    "The obstacle explosion must open a vault like a player bomb.");
                Assert(chained.IsBroken && chained.LastExplosion != null && !chained.LastExplosion.HasExploded &&
                       chained.LastExplosion != explosion,
                    "An exploding box caught in an explosion must arm its own explosion.");
                Assert(!explosion.ApplyExplosion() &&
                       Mathf.Approximately(enemy.CurrentHealth, 100f - PlacedBomb.DefaultEnemyDamage),
                    "An obstacle explosion must apply once.");

                PlacedBomb second = chained.LastExplosion;
                Assert(second.Tick(second.ExplodeAt) && second.HasExploded && !farRock.IsBroken,
                    "The chained box must explode after its own fuse.");

                DestructibleObstacle rebuilt = CreateObstacle(root.transform, explosive, Origin, "box-01");
                rebuilt.Bind(state, 11, root.transform, progress);
                Assert(rebuilt.IsBroken && !rebuilt.gameObject.activeSelf && rebuilt.LastExplosion == null,
                    "A rebuilt room must keep the exploding box broken without another explosion.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(progressHolder);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateRandomBox(ObstacleVariantDefinition random)
        {
            GameObject root = new("Obstacle-6 Random Box Verification");
            GameObject progressHolder = new("Obstacle-6 Random Box Progress");
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                DestructibleObstacle probe = CreateObstacle(root.transform, random, Origin, "random-01");
                int explosions = 0;
                int jackpots = 0;
                int releases = 0;
                int enemySeed = 0;
                int explosionSeed = 0;
                int jackpotSeed = 0;
                int emptySeed = 0;
                for (int seed = 1; seed <= SeedCount; seed++)
                {
                    probe.Bind(new RoomRunState("floor-01-room-02"), seed, root.transform, progress);
                    bool explodes = probe.TryRollExplosion();
                    Assert(explodes == probe.TryRollExplosion(), $"The random box changed for repeated seed {seed}.");
                    int drops = probe.RollDrops().Count;
                    Assert(drops == 0 || drops == random.DropCount,
                        "The random box must hold nothing or its full drop count.");
                    if (explodes)
                    {
                        explosions++;
                        if (explosionSeed == 0) explosionSeed = seed;
                    }
                    else if (probe.TryRollEnemies())
                    {
                        releases++;
                        if (enemySeed == 0) enemySeed = seed;
                    }
                    else if (drops > 0)
                    {
                        jackpots++;
                        if (jackpotSeed == 0) jackpotSeed = seed;
                    }
                    else if (emptySeed == 0)
                    {
                        emptySeed = seed;
                    }
                }

                float explosionRate = explosions / (float)SeedCount;
                float jackpotRate = jackpots / (float)SeedCount;
                float releaseRate = releases / (float)SeedCount;
                float calm = 1f - Week23Obstacle5Setup.RandomBoxExplosionChance;
                float expectedRelease = calm * Week23Obstacle5Setup.RandomBoxEnemyChance;
                float expectedJackpot = calm * (1f - Week23Obstacle5Setup.RandomBoxEnemyChance) *
                                        Week23Obstacle5Setup.ShadyRandomBox.DropChance;
                Assert(Mathf.Abs(explosionRate - Week23Obstacle5Setup.RandomBoxExplosionChance) <= 0.02f &&
                       Mathf.Abs(releaseRate - expectedRelease) <= 0.02f &&
                       Mathf.Abs(jackpotRate - expectedJackpot) <= 0.02f &&
                       explosionSeed != 0 && enemySeed != 0 && jackpotSeed != 0 && emptySeed != 0,
                    $"The random box exploded {explosionRate:P1}, released enemies {releaseRate:P1} and paid out " +
                    $"{jackpotRate:P1} of the time; expected {Week23Obstacle5Setup.RandomBoxExplosionChance:P0}, " +
                    $"{expectedRelease:P0} and {expectedJackpot:P0}.");

                DestructibleObstacle releasing = CreateObstacle(root.transform, random, Origin, "random-01");
                releasing.Bind(new RoomRunState("floor-01-room-06"), enemySeed, root.transform, progress);
                BreakWithHits(releasing);
                Assert(releasing.LastExplosion == null && releasing.LastDrops.Count == 0 &&
                       releasing.LastEnemies.Count == Week23Obstacle5Setup.RandomBoxEnemyCount &&
                       releasing.LastEnemies.Count == 5 &&
                       releasing.LastEnemies.All(enemy => enemy.GetComponent<TrickalFanGame.Enemy.EnemyFlight>() != null),
                    "A random box that releases enemies must leave five Jyubi and nothing else.");

                DestructibleObstacle exploding = CreateObstacle(root.transform, random, Origin, "random-01");
                exploding.Bind(new RoomRunState("floor-01-room-03"), explosionSeed, root.transform, progress);
                BreakWithHits(exploding);
                Assert(exploding.LastExplosion != null && exploding.LastDrops.Count == 0,
                    "A random box that explodes must not also drop pickups.");

                DestructibleObstacle paying = CreateObstacle(root.transform, random, Origin, "random-01");
                paying.Bind(new RoomRunState("floor-01-room-04"), jackpotSeed, root.transform, progress);
                BreakWithHits(paying);
                Assert(paying.LastExplosion == null && paying.LastEnemies.Count == 0 &&
                       paying.LastDrops.Count == random.DropCount &&
                       random.DropCount == 3,
                    "A random box that pays out must leave three pickups and no explosion.");

                DestructibleObstacle empty = CreateObstacle(root.transform, random, Origin, "random-01");
                empty.Bind(new RoomRunState("floor-01-room-05"), emptySeed, root.transform, progress);
                BreakWithHits(empty);
                Assert(empty.LastExplosion == null && empty.LastDrops.Count == 0,
                    "A random box that misses must leave nothing.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(progressHolder);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateCollectionBox(ObstacleVariantDefinition collection, RoomGraphAssembler assembler)
        {
            GameObject root = new("Obstacle-6 Collection Box Verification");
            GameObject progressHolder = new("Obstacle-6 Collection Box Progress");
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                DestructibleObstacle probe = CreateObstacle(root.transform, collection, Origin, "collection-01");
                int artifacts = 0;
                int artifactSeed = 0;
                for (int seed = 1; seed <= SeedCount; seed++)
                {
                    probe.Bind(new RoomRunState("floor-01-room-02"), seed, root.transform, progress);
                    probe.BindRareItems(assembler.SelectionRewardPool, null);
                    Assert(!probe.TryRollExplosion() && probe.RollDrops().Count == collection.DropCount,
                        "The collection box must never explode and always hold its two pickups.");
                    if (!probe.TryRollRareItem(out ItemDefinition item)) continue;
                    Assert(item != null && item.Kind == ItemKind.Artifact &&
                           assembler.SelectionRewardPool.Contains(item),
                        "The collection box's extra item must be an artifact from the selection reward pool.");
                    artifacts++;
                    if (artifactSeed == 0) artifactSeed = seed;
                }

                float rate = artifacts / (float)SeedCount;
                Assert(Mathf.Abs(rate - Week23Obstacle5Setup.CollectionBoxArtifactChance) <= 0.02f && artifactSeed != 0,
                    $"The collection box gave an artifact {rate:P1} of the time; expected " +
                    $"{Week23Obstacle5Setup.CollectionBoxArtifactChance:P0}.");

                DestructibleObstacle box = CreateObstacle(root.transform, collection, Origin, "collection-01");
                box.Bind(new RoomRunState("floor-01-room-03"), artifactSeed, root.transform, progress);
                box.BindRareItems(assembler.SelectionRewardPool, null);
                BreakWithHits(box);
                Assert(collection.DropCount == 2 && box.LastDrops.Count == 3 &&
                       box.LastDrops.Take(2).All(drop => drop.GetComponent<ItemPickup>() == null) &&
                       box.LastDrops[^1].GetComponent<ItemPickup>() != null &&
                       box.LastDrops[^1].GetComponent<ItemPickup>().Definition.Kind == ItemKind.Artifact,
                    "A collection box whose artifact roll hits must leave two pickups and one artifact pickup.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(progressHolder);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateFlyingVault(ObstacleVariantDefinition vaultVariant)
        {
            GameObject root = new("Obstacle-6 Flying Vault Verification");
            GameObject progressHolder = new("Obstacle-6 Flying Vault Progress");
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                RoomRunState state = new("floor-01-room-02");
                DestructibleObstacle vault = CreateObstacle(root.transform, vaultVariant, Origin, "vault-01");
                vault.Bind(state, 5, root.transform, progress);
                PlayerFlight flight = CreateFlyingBody(root.transform, Origin + Vector2.right * 3f);
                Physics2D.SyncTransforms();

                progress.TryAddResource(RunResourceType.Key, 1);
                flight.IgnoreNearbyLowObstacles();
                Assert(!vault.IsBroken && progress.GetResourceCount(RunResourceType.Key) == 1,
                    "A flying player away from the vault must not open it.");

                progress.TrySpendResource(RunResourceType.Key);
                flight.transform.position = Origin + Vector2.right * 0.3f;
                Physics2D.SyncTransforms();
                flight.IgnoreNearbyLowObstacles();
                Assert(!vault.IsBroken && flight.IsIgnoring(vault.GetComponent<Collider2D>()),
                    "A flying player without a key must pass over the vault without opening it.");

                progress.TryAddResource(RunResourceType.Key, 2);
                flight.IgnoreNearbyLowObstacles();
                Assert(vault.IsBroken && state.IsObstacleDestroyed("vault-01") &&
                       progress.GetResourceCount(RunResourceType.Key) == 1 &&
                       vault.LastDrops.Count >= vaultVariant.DropCount,
                    "A flying player over the vault with a key must spend exactly one key and open it.");
                flight.IgnoreNearbyLowObstacles();
                Assert(progress.GetResourceCount(RunResourceType.Key) == 1,
                    "An opened vault must not take another key from a flying player.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(progressHolder);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateTreeLayout(FloorGenerator generator)
        {
            RoomTemplateDefinition template = generator.RoomTemplates.SingleOrDefault(candidate =>
                candidate != null && candidate.TemplateId == Week23Obstacle6Setup.TemplateId);
            Assert(template != null, "The basic-tree-grove Layout is not registered. Run Obstacle-6 setup.");
            Assert(template.TryValidate(out string error) && template.TryValidateLayout(out error),
                $"The basic-tree-grove Layout must pass the room contract. {error}");
            Assert(template.Profile.ProfileId == Week14Room1Setup.BasicProfileId &&
                   template.LayoutDifficultyModifier == Week19Difficulty1Setup.ObstacleLayoutModifier,
                "The basic-tree-grove Layout must use the Basic profile and the obstacle Layout modifier.");

            // Obstacle-7 turned the trees into breakable high obstacles; its verification checks their rules.
            DestructibleObstacle[] trees =
                template.RoomPrefabAsset.GetComponentsInChildren<DestructibleObstacle>(true);
            Assert(trees.Length == Week23Obstacle6Setup.TreeCells.Length &&
                   trees.Select(tree => tree.ObstacleId).Distinct().Count() == trees.Length,
                "The basic-tree-grove Layout must hold its four trees once each.");
            foreach (DestructibleObstacle tree in trees)
            {
                BoxCollider2D box = tree.GetComponent<BoxCollider2D>();
                Assert(tree.TryValidate(out error) && tree.BlocksFlight && box != null && box.size == Vector2.one &&
                       !RoomMovementClass.IsLowObstacle(box) &&
                       Week23Obstacle6Setup.TreeCells.Any(cell =>
                           Vector2.Distance(cell, tree.transform.localPosition) < 0.001f),
                    $"Tree '{tree.ObstacleId}' must be a 1x1 high obstacle on its authored cell. {error}");
            }

            RoomTemplateDefinition pillar = generator.RoomTemplates.Single(candidate =>
                candidate != null && candidate.TemplateId == Week14Room7Setup.TemplateId);
            RoomStaticObstacle pillarObstacle = pillar.RoomPrefabAsset.GetComponentInChildren<RoomStaticObstacle>(true);
            Assert(pillarObstacle != null && !pillarObstacle.BlocksFlight &&
                   RoomMovementClass.IsLowObstacle(pillarObstacle.GetComponent<Collider2D>()),
                "The existing central pillar must stay a low obstacle.");

            bool selected = false;
            for (int seed = 1; seed <= 512 && !selected; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out error), error);
                selected = graph.Nodes.Any(node =>
                    node.Template != null && node.Template.TemplateId == Week23Obstacle6Setup.TemplateId);
            }

            Assert(selected, "The basic-tree-grove Layout was never selected across 512 generated seeds.");
        }

        private static PlayerFlight CreateFlyingBody(Transform parent, Vector2 position)
        {
            GameObject body = new("Obstacle-6 Flying Player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(SpriteRenderer));
            body.transform.SetParent(parent);
            body.transform.position = position;
            // Hitbox-0: one circle stands in for the feet, the part that meets terrain.
            body.layer = PlayerFeet.Layer;
            body.GetComponent<CircleCollider2D>().radius = 0.3f;
            body.GetComponent<Rigidbody2D>().gravityScale = 0f;
            PlayerFlight flight = body.AddComponent<PlayerFlight>();
            Assert(flight.TryStartFlying(), "The test body must start flying.");
            return flight;
        }

        private static Health CreateHealthTarget(Transform parent, string name, Vector2 position, float maxHealth,
            string layerName)
        {
            GameObject target = new($"Obstacle-6 {name}", typeof(CircleCollider2D), typeof(Health));
            target.transform.SetParent(parent);
            target.transform.position = position;
            target.layer = LayerMask.NameToLayer(layerName);
            Health health = target.GetComponent<Health>();
            SerializedObject serializedHealth = new(health);
            serializedHealth.FindProperty("maxHealth").floatValue = maxHealth;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            typeof(Health).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(health, null);
            return health;
        }

        private static void BreakWithHits(DestructibleObstacle obstacle)
        {
            for (int hit = 0; hit < obstacle.RequiredHits; hit++) obstacle.RegisterPlayerHit();
            Assert(obstacle.IsBroken, "The obstacle must break after its required hits.");
        }

        private static DestructibleObstacle CreateObstacle(Transform parent, ObstacleVariantDefinition variant,
            Vector2 position, string obstacleId)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week18Obstacle1Setup.PrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            DestructibleObstacle obstacle = instance.GetComponent<DestructibleObstacle>();
            obstacle.Configure(obstacleId, DestructibleObstacle.DefaultRequiredHits, obstacle.DropTable,
                instance.GetComponentInChildren<SpriteRenderer>(true));
            obstacle.ApplyVariant(variant);
            return obstacle;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
