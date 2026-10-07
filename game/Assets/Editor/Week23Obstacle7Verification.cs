using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Obstacle-7: a tree breaks from 4 player hits or a bomb, drops nothing and blocks a flying player until then.
    // With a burn artifact held it catches fire on its 2nd hit, and breaking a burning tree is recorded on the Run.
    public static class Week23Obstacle7Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-5700f, 5700f);
        private const int SeedCount = 512;

        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Obstacle-7 Tree Breaking")]
        public static void SetupAndVerifyBatch()
        {
            Week23Obstacle6Setup.Setup();
            string[] paths = Week23Obstacle6Setup.CreatedAssetPaths();
            Dictionary<string, string> guids = paths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Week23Obstacle6Setup.Setup();
            foreach (KeyValuePair<string, string> entry in guids)
                Assert(!string.IsNullOrWhiteSpace(entry.Value) && entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Obstacle-7 setup changed or lost the GUID for {entry.Key}.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Obstacle-7 Tree Breaking")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null, "Obstacle-7 verification needs the Game Scene generator.");
            Assert(!PlayerStats.DevelopmentForceBurnSource, "The development burn source flag must default to off.");

            RoomTemplateDefinition template = ValidateTreeLayout(generator);
            ValidateBreaking();
            ValidateBurning();
            ValidateRuntimeRoom(template);
            Debug.Log("Obstacle-7 verification passed: basic-tree-grove passes the room contract with four trees " +
                      "that are fixed-kind destructible high obstacles, a tree blocks a flying player until its 4th " +
                      "hit or a bomb breaks it and never drops anything, a rebuilt room keeps it broken, a held " +
                      "burn artifact lights it on the 2nd hit without a chance roll and breaking it while burning " +
                      "is recorded once on the Run, and a tree without a burn source or a rock never burns.");
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Obstacle-7 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week23Obstacle6Verification.VerifyWithRegressionsBatch();
            Week23Enemy6Verification.Verify();
            Week20Special3Verification.Verify();
            Debug.Log("Obstacle-7 regression verification passed.");
        }

        private static RoomTemplateDefinition ValidateTreeLayout(FloorGenerator generator)
        {
            RoomTemplateDefinition template = generator.RoomTemplates.SingleOrDefault(candidate =>
                candidate != null && candidate.TemplateId == Week23Obstacle6Setup.TemplateId);
            Assert(template != null, "The basic-tree-grove Layout is not registered. Run Obstacle-6 setup.");
            Assert(template.TryValidate(out string error) && template.TryValidateLayout(out error),
                $"The basic-tree-grove Layout must pass the room contract. {error}");

            GameObject prefab = template.RoomPrefabAsset;
            DestructibleObstacle[] trees = prefab.GetComponentsInChildren<DestructibleObstacle>(true);
            Assert(trees.Length == Week23Obstacle6Setup.TreeCells.Length &&
                   trees.Select(tree => tree.ObstacleId).Distinct().Count() == trees.Length &&
                   prefab.GetComponentsInChildren<RoomStaticObstacle>(true).Length == 0 &&
                   prefab.GetComponentsInChildren<RoomObstacleVariantSlot>(true).Length == 0,
                "The basic-tree-grove Layout must hold four destructible trees, no fixed obstacle and no " +
                "special candidate slot. Run Obstacle-7 setup.");
            foreach (DestructibleObstacle tree in trees)
            {
                BoxCollider2D box = tree.GetComponent<BoxCollider2D>();
                Assert(tree.TryValidate(out error) && tree.VariantId == DestructibleObstacle.TreeVariantId &&
                       tree.RequiredHits == 4 && tree.DropTable == null && tree.BlocksFlight &&
                       tree.BurnHits == DestructibleObstacle.TreeBurnHits && tree.BurnHits == 2 &&
                       box != null && box.size == Vector2.one && !RoomMovementClass.IsLowObstacle(box) &&
                       Week23Obstacle6Setup.TreeCells.Any(cell =>
                           Vector2.Distance(cell, tree.transform.localPosition) < 0.001f),
                    $"Tree '{tree.ObstacleId}' must be a 4-hit high obstacle without drops that burns from its " +
                    $"2nd hit. {error}");
            }

            Assert(RoomObstacleLayout.TryCollectFootprints(prefab, out List<RoomObstacleFootprint> footprints,
                       out error) && footprints.Count == trees.Length &&
                   footprints.All(footprint => footprint.IsDestructible && footprint.BlocksProjectiles),
                $"The trees must stay solid 1x1 Layout footprints. {error}");
            return template;
        }

        private static void ValidateBreaking()
        {
            GameObject root = new("Obstacle-7 Breaking Verification");
            GameObject progressHolder = new("Obstacle-7 Breaking Progress");
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                PlayerStats stats = CreatePlayerStats(root.transform);
                RoomRunState state = new("floor-01-room-02");
                DestructibleObstacle tree = CreateTree(root.transform, Origin + Vector2.right, "tree-01");
                DestructibleObstacle bombed = CreateTree(root.transform, Origin + Vector2.left, "tree-02");
                foreach (DestructibleObstacle obstacle in new[] { tree, bombed })
                {
                    obstacle.Bind(state, 7, root.transform, progress);
                    obstacle.BindBurnSource(stats);
                }

                for (int seed = 1; seed <= SeedCount; seed++)
                {
                    DestructibleObstacle probe = CreateTree(root.transform, Origin + Vector2.up * 40f, "tree-probe");
                    probe.Bind(new RoomRunState("floor-01-room-09"), seed, root.transform, progress);
                    Assert(probe.RollDrops().Count == 0 && !probe.TryRollExplosion() && !probe.TryRollEnemies() &&
                           !probe.TryRollRareItem(out _),
                        $"A tree must never roll a drop, an explosion, enemies or a rare item (seed {seed}).");
                    Object.DestroyImmediate(probe.gameObject);
                }

                PlayerFlight flight = CreateFlyingBody(root.transform, Origin);
                Physics2D.SyncTransforms();
                Collider2D treeCollider = tree.GetComponent<Collider2D>();
                for (int hit = 1; hit < tree.RequiredHits; hit++)
                {
                    Assert(tree.RegisterPlayerHit() && !tree.IsBroken && tree.HitsTaken == hit && !tree.IsBurning,
                        $"A tree must survive hit {hit} and never burn without a burn source.");
                    flight.IgnoreNearbyLowObstacles();
                    Assert(!RoomMovementClass.IsLowObstacle(treeCollider) && !flight.IsIgnoring(treeCollider),
                        "A damaged tree must still block a flying player.");
                }

                Assert(tree.RegisterPlayerHit() && tree.IsBroken && !tree.gameObject.activeSelf &&
                       state.IsObstacleDestroyed("tree-01") && tree.LastDrops.Count == 0 &&
                       tree.LastExplosion == null && tree.LastEnemies.Count == 0 && !tree.BrokeWhileBurning,
                    "The 4th hit must break a tree without leaving anything.");
                Assert(!tree.RegisterPlayerHit(), "A broken tree must ignore further hits.");

                Assert(DestructibleObstacle.DestroyByBombInCircle(Origin, 3f) == 1 && bombed.IsBroken &&
                       state.IsObstacleDestroyed("tree-02") && bombed.LastDrops.Count == 0 &&
                       !bombed.BrokeWhileBurning,
                    "A bomb must break an untouched tree without leaving anything.");
                Assert(progress.BurnedObstacleCount == 0, "Trees broken without burning must not be recorded.");

                DestructibleObstacle rebuilt = CreateTree(root.transform, Origin + Vector2.right, "tree-01");
                rebuilt.Bind(state, 7, root.transform, progress);
                Assert(rebuilt.IsBroken && !rebuilt.gameObject.activeSelf && rebuilt.LastDrops.Count == 0,
                    "A rebuilt room must keep a broken tree broken.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(progressHolder);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateBurning()
        {
            GameObject root = new("Obstacle-7 Burning Verification");
            GameObject progressHolder = new("Obstacle-7 Burning Progress");
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                PlayerStats stats = CreatePlayerStats(root.transform);
                RoomRunState state = new("floor-01-room-03");
                DestructibleObstacle late = Bound(CreateTree(root.transform, Origin + Vector2.up * 4f, "tree-late"),
                    state, progress, stats, root.transform);
                DestructibleObstacle tree = Bound(CreateTree(root.transform, Origin + Vector2.right, "tree-01"),
                    state, progress, stats, root.transform);
                DestructibleObstacle bombed = Bound(CreateTree(root.transform, Origin + Vector2.left, "tree-02"),
                    state, progress, stats, root.transform);
                DestructibleObstacle fresh = Bound(CreateTree(root.transform, Origin + Vector2.down, "tree-03"),
                    state, progress, stats, root.transform);
                DestructibleObstacle rock = Bound(CreateRock(root.transform, Origin + Vector2.up * 8f, "rock-01"),
                    state, progress, stats, root.transform);

                // Three hits before the burn artifact: the breaking hit does not light the tree.
                for (int hit = 0; hit < 3; hit++) late.RegisterPlayerHit();
                Assert(!stats.HasBurnSource && !late.IsBurning, "A player without a burn artifact has no burn source.");
                stats.AddBurnSource();
                Assert(stats.HasBurnSource && late.RegisterPlayerHit() && late.IsBroken && !late.BrokeWhileBurning &&
                       progress.BurnedObstacleCount == 0,
                    "A tree lit by nothing before its breaking hit must break like any other obstacle.");

                SpriteRenderer visual = tree.GetComponentInChildren<SpriteRenderer>(true);
                Assert(tree.RegisterPlayerHit() && !tree.IsBurning,
                    "The 1st hit must not light a tree even with a burn artifact.");
                PlayerFlight flight = CreateFlyingBody(root.transform, Origin);
                Physics2D.SyncTransforms();
                Assert(tree.RegisterPlayerHit() && tree.IsBurning && !tree.IsBroken &&
                       visual.color == Week23Obstacle6Setup.TreeBurningColor,
                    "The 2nd hit must set a tree on fire while a burn artifact is held, without a chance roll.");
                flight.IgnoreNearbyLowObstacles();
                Assert(!flight.IsIgnoring(tree.GetComponent<Collider2D>()),
                    "A burning tree must still block a flying player.");
                Assert(tree.RegisterPlayerHit() && tree.IsBurning && !tree.IsBroken && progress.BurnedObstacleCount == 0,
                    "A burning tree must keep burning until the rest of its hits land.");
                Assert(tree.RegisterPlayerHit() && tree.IsBroken && tree.BrokeWhileBurning &&
                       tree.LastDrops.Count == 0 && progress.BurnedObstacleCount == 1,
                    "Breaking a burning tree must record one burned obstacle and still drop nothing.");

                bombed.RegisterPlayerHit();
                bombed.RegisterPlayerHit();
                Assert(bombed.IsBurning && !fresh.IsBurning &&
                       DestructibleObstacle.DestroyByBombInCircle(Origin, 1.6f) == 2 && bombed.BrokeWhileBurning &&
                       fresh.IsBroken && !fresh.BrokeWhileBurning && progress.BurnedObstacleCount == 2,
                    "A bomb must count a burning tree as burned and an unlit one as a plain break.");

                for (int hit = 0; hit < rock.RequiredHits; hit++)
                {
                    rock.RegisterPlayerHit();
                    Assert(!rock.IsBurning, "An obstacle that is not a tree must never burn.");
                }

                Assert(rock.IsBroken && progress.BurnedObstacleCount == 2, "A broken rock must not be recorded as burned.");

                // A rebuilt room replays neither the break nor the record.
                DestructibleObstacle rebuilt = Bound(CreateTree(root.transform, Origin + Vector2.right, "tree-01"),
                    state, progress, stats, root.transform);
                Assert(rebuilt.IsBroken && !rebuilt.gameObject.activeSelf && progress.BurnedObstacleCount == 2,
                    "A rebuilt room must keep a burned tree broken without recording it again.");

                progress.ResetProgress();
                Assert(progress.BurnedObstacleCount == 0, "A new Run must start without burned obstacles.");

                PlayerStats plain = CreatePlayerStats(root.transform);
                DestructibleObstacle forced = Bound(CreateTree(root.transform, Origin + Vector2.up * 12f, "tree-04"),
                    new RoomRunState("floor-01-room-04"), progress, plain, root.transform);
                PlayerStats.DevelopmentForceBurnSource = true;
                forced.RegisterPlayerHit();
                forced.RegisterPlayerHit();
                Assert(plain.HasBurnSource && forced.IsBurning,
                    "The development burn source must light a tree like a held burn artifact.");
            }
            finally
            {
                PlayerStats.DevelopmentForceBurnSource = false;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(progressHolder);
                Physics2D.SyncTransforms();
            }
        }

        // The Game Scene assembler builds the Layout and binds each tree to the room state and the player's stats.
        private static void ValidateRuntimeRoom(RoomTemplateDefinition template)
        {
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(assembler != null && assembler.Progress != null,
                "Obstacle-7 verification needs the Game Scene assembler.");
            RunProgress progress = assembler.Progress;
            try
            {
                GeneratedRoomNode treeRoom = null;
                for (int seed = 1; seed <= SeedCount && treeRoom == null; seed++)
                {
                    progress.ResetProgress();
                    Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
                    treeRoom = assembler.GeneratedGraph.FindFloor(1).Nodes
                        .FirstOrDefault(node => node.Template == template);
                }

                Assert(treeRoom != null, $"No floor 1 used basic-tree-grove across {SeedCount} seeds.");
                RoomNode runtimeNode = assembler.Graph.Nodes.Single(node => node.RoomId == treeRoom.RoomId);
                // Only the current room is active, and an inactive obstacle ignores hits.
                Assert(assembler.Graph.TryReplaceFloor(assembler.Graph.Nodes.ToArray(), runtimeNode,
                    assembler.Graph.Player, out string enterError), enterError);
                Physics2D.SyncTransforms();
                DestructibleObstacle[] trees = runtimeNode.GetComponentsInChildren<DestructibleObstacle>(true);
                PlayerStats stats = assembler.Graph.Player.GetComponent<PlayerStats>();
                Assert(trees.Length == Week23Obstacle6Setup.TreeCells.Length && stats != null && !stats.HasBurnSource,
                    "The runtime tree room must hold its four trees and a player without a burn source.");

                RoomRunState state = progress.GetRoomState(treeRoom.RoomId);
                DestructibleObstacle plain = trees[0];
                for (int hit = 0; hit < plain.RequiredHits; hit++) plain.RegisterPlayerHit();
                Assert(plain.IsBroken && !plain.BrokeWhileBurning && state.IsObstacleDestroyed(plain.ObstacleId) &&
                       progress.BurnedObstacleCount == 0,
                    "A runtime tree must break after 4 hits and be kept in its room state.");

                PlayerStats.DevelopmentForceBurnSource = true;
                DestructibleObstacle burned = trees[1];
                for (int hit = 0; hit < burned.RequiredHits; hit++) burned.RegisterPlayerHit();
                Assert(burned.BrokeWhileBurning && progress.BurnedObstacleCount == 1,
                    "A runtime tree must be bound to the player's burn source and record its burned break.");
            }
            finally
            {
                PlayerStats.DevelopmentForceBurnSource = false;
                progress.ResetProgress();
            }
        }

        private static DestructibleObstacle Bound(DestructibleObstacle obstacle, RoomRunState state,
            RunProgress progress, PlayerStats stats, Transform dropParent)
        {
            obstacle.Bind(state, 7, dropParent, progress);
            obstacle.BindBurnSource(stats);
            return obstacle;
        }

        // Built the way Obstacle-6 setup authors a tree in the Layout.
        private static DestructibleObstacle CreateTree(Transform parent, Vector2 position, string obstacleId)
        {
            DestructibleObstacle tree = CreateRock(parent, position, obstacleId);
            tree.Configure(obstacleId, Week23Obstacle6Setup.TreeHits, null,
                tree.GetComponentInChildren<SpriteRenderer>(true));
            tree.ConfigureFixedKind(DestructibleObstacle.TreeVariantId, Week23Obstacle6Setup.TreeColor,
                Week23Obstacle6Setup.TreeCrackedColor, true, DestructibleObstacle.TreeBurnHits,
                Week23Obstacle6Setup.TreeBurningColor);
            return tree;
        }

        private static DestructibleObstacle CreateRock(Transform parent, Vector2 position, string obstacleId)
        {
            GameObject owner = new($"Obstacle-7 {obstacleId}", typeof(BoxCollider2D), typeof(DestructibleObstacle));
            owner.transform.SetParent(parent);
            owner.transform.position = position;
            owner.layer = LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName);
            owner.GetComponent<BoxCollider2D>().size = Vector2.one;
            GameObject visual = new("Visual", typeof(SpriteRenderer));
            visual.transform.SetParent(owner.transform, false);
            DestructibleObstacle obstacle = owner.GetComponent<DestructibleObstacle>();
            obstacle.Configure(obstacleId, DestructibleObstacle.DefaultRequiredHits, null,
                visual.GetComponent<SpriteRenderer>());
            Assert(obstacle.TryValidate(out string error), error);
            return obstacle;
        }

        private static PlayerStats CreatePlayerStats(Transform parent)
        {
            GameObject player = new("Obstacle-7 Player Stats", typeof(PlayerStats));
            player.transform.SetParent(parent);
            player.transform.position = Origin + Vector2.down * 60f;
            return player.GetComponent<PlayerStats>();
        }

        private static PlayerFlight CreateFlyingBody(Transform parent, Vector2 position)
        {
            GameObject body = new("Obstacle-7 Flying Player", typeof(Rigidbody2D), typeof(CircleCollider2D),
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

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
