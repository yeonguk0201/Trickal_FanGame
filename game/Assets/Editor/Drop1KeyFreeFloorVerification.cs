using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Drop-1 (사용자 결정 A): floor 1 of the Game Scene Run needs no key. Its shop has no lock, and golden chests from a
    // clear roll or a Layout become normal chests; floor 2 and later keep their locks and golden chests.
    public static class Drop1KeyFreeFloorVerification
    {
        private const int SeedCount = 512;
        private static readonly Vector2 Origin = new(-9600f, -9600f);

        [MenuItem("Trickal Fan Game/Week 28/Setup and Verify Drop-1 Key-Free Floor 1")]
        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Drop1KeyFreeFloorSetup.Setup();
            Drop1KeyFreeFloorSetup.Setup();
            Assert(!string.IsNullOrWhiteSpace(sceneGuid) &&
                   sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Drop-1 setup changed the Game Scene GUID.");
            Verify();
        }

        // Drop-1 changes the shop lock and the clear and Layout chest kinds that these verifiers exercise on floor 1.
        [MenuItem("Trickal Fan Game/Week 28/Verify Drop-1 Key-Free Floor 1 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            SetupAndVerifyBatch();
            Week20Floor1Verification.Verify();
            Week20Special1Verification.Verify();
            Week20Special4Verification.Verify();
            Week22Shop0Verification.Verify();
            Week23Shop1Verification.Verify();
            Week22Chest1Verification.Verify();
            Week22Chest2Verification.Verify();
            Week22Jjangsem1Verification.Verify();
            Week27Power2Verification.Verify();
            Week27Elif3Verification.Verify();
            Drop0DropRateVerification.Verify();
            RoomLayoutImporterVerification.Verify();
            Debug.Log("Drop-1 key-free floor verification with regressions passed.");
        }

        [MenuItem("Trickal Fan Game/Week 28/Verify Drop-1 Key-Free Floor 1")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(assembler != null && assembler.Generator != null && assembler.Progress != null &&
                   assembler.ChestContentTable != null,
                "Drop-1 requires the Game Scene assembler, generator, Run progress and chest table.");
            Assert(assembler.Generator.RoomContentVersion >= Drop1KeyFreeFloorSetup.RoomContentVersion,
                $"Drop-1 must raise the Room content version to {Drop1KeyFreeFloorSetup.RoomContentVersion}.");
            VerifyKindRule();
            string generation = VerifyGeneration(assembler.Generator);
            VerifyClearChests(assembler);
            VerifyLayoutAndDevelopmentChests(assembler);
            Debug.Log($"Drop-1 key-free floor verification passed. {generation}");
        }

        private static void VerifyKindRule()
        {
            Assert(FloorGenerator.KeyFreeFloor == 1 && FloorGenerator.IsKeyFreeFloor(1, true) &&
                   !FloorGenerator.IsKeyFreeFloor(2, true) && !FloorGenerator.IsKeyFreeFloor(1, false),
                "Only floor 1 of a configured Run is key-free; legacy generators keep their locks.");
            Assert(ChestContentTable.ResolveKindForFloor(ChestKind.Golden, true) == ChestKind.Normal &&
                   ChestContentTable.ResolveKindForFloor(ChestKind.Normal, true) == ChestKind.Normal &&
                   ChestContentTable.ResolveKindForFloor(ChestKind.Diamond, true) == ChestKind.Diamond &&
                   ChestContentTable.ResolveKindForFloor(ChestKind.Golden, false) == ChestKind.Golden,
                "The key-free floor turns only golden chests into normal chests.");
        }

        private static string VerifyGeneration(FloorGenerator generator)
        {
            int floorOneShops = 0, laterShops = 0, laterLockedTreasures = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                foreach (GeneratedFloor floor in graph.Floors)
                {
                    Assert(floor.IsKeyFree == (floor.FloorNumber == FloorGenerator.KeyFreeFloor),
                        $"Seed {seed} floor {floor.FloorNumber} has the wrong key-free flag.");
                    GeneratedRoomNode shop = floor.Nodes.FirstOrDefault(node => node.Role == GeneratedRoomRole.Shop);
                    if (floor.IsKeyFree)
                    {
                        Assert(floor.Nodes.All(node => !node.RequiresKey),
                            $"Seed {seed} floor 1 must not lock any room with a key.");
                        floorOneShops += shop != null ? 1 : 0;
                        continue;
                    }

                    if (shop != null)
                    {
                        Assert(shop.RequiresKey, $"Seed {seed} floor {floor.FloorNumber} shop must keep its key lock.");
                        laterShops++;
                    }

                    laterLockedTreasures += floor.Nodes.Count(node =>
                        node.Role == GeneratedRoomRole.Treasure && node.RequiresKey);
                }
            }

            Assert(floorOneShops > 0 && laterShops > 0 && laterLockedTreasures > 0,
                "Seeds must include an unlocked floor-1 shop and locked shops and treasure rooms on later floors.");
            return $"{SeedCount} seeds: floor-1 shops {floorOneShops} unlocked, later shops {laterShops} locked, " +
                   $"later locked treasure rooms {laterLockedTreasures}.";
        }

        // A floor-1 room whose clear rolls golden places a normal chest with the normal contents of the same seed,
        // records it as normal and restores it so; the same roll on floor 2 still places a golden chest.
        private static void VerifyClearChests(RoomGraphAssembler assembler)
        {
            ChestContentTable table = assembler.ChestContentTable;
            RunProgress progress = assembler.Progress;
            try
            {
                (int floorOneSeed, string floorOneRoom) = FindGoldenRoll(assembler.Generator, table, 1);
                progress.ResetProgress();
                Assert(assembler.TryApplyGeneratedGraphForVerification(floorOneSeed, out string error), error);
                PlayerMovement player = assembler.Graph.Player;
                RoomClearRewardSpawner spawner = Spawner(assembler, floorOneRoom);
                RoomRunState state = progress.GetRoomState(floorOneRoom);
                Assert(spawner.TrySpawn() && spawner.LastSpawnedChest != null &&
                       spawner.LastSpawnedChest.Kind == ChestKind.Normal &&
                       state.GetChest(RoomClearRewardSpawner.ClearChestId)?.Kind == ChestKind.Normal,
                    $"Floor-1 room {floorOneRoom} rolled golden and must place and record a normal chest.");
                TreasureChest chest = spawner.LastSpawnedChest;
                ChestContents normal = table.RollContents(spawner.ChestSeed, ChestKind.Normal);
                int expected = normal.Consumables.Count + (normal.Spell != null ? 1 : 0);
                Room(assembler, floorOneRoom).Node.SetVisible(true);
                int before = ContentChildren(assembler, floorOneRoom);
                Assert(progress.GetResourceCount(RunResourceType.Key) == 0 && chest.TryOpenByTouch(player) &&
                       chest.IsOpened && progress.GetResourceCount(RunResourceType.Key) == 0,
                    "The floor-1 chest must open by touch without a key.");
                Assert(ContentChildren(assembler, floorOneRoom) - before == expected,
                    "The floor-1 chest must drop the normal contents of its seed.");
                Assert(assembler.TryLoadFloor(1, player, out error), error);
                TreasureChest restored = Room(assembler, floorOneRoom).Node.ContentRoot
                    .GetComponentsInChildren<TreasureChest>(true).Single(candidate => candidate.gameObject.activeSelf);
                Assert(restored.Kind == ChestKind.Normal && restored.IsOpened,
                    "A rebuilt floor-1 room must restore its opened normal chest.");

                (int floorTwoSeed, string floorTwoRoom) = FindGoldenRoll(assembler.Generator, table, 2);
                progress.ResetProgress();
                Assert(assembler.TryApplyGeneratedGraphForVerification(floorTwoSeed, out error) &&
                       assembler.TryLoadFloor(2, player, out error), error);
                RoomClearRewardSpawner later = Spawner(assembler, floorTwoRoom);
                Assert(later.TrySpawn() && later.LastSpawnedChest != null &&
                       later.LastSpawnedChest.Kind == ChestKind.Golden,
                    $"Floor-2 room {floorTwoRoom} must keep its golden chest.");
            }
            finally
            {
                progress.ResetProgress();
            }
        }

        // A golden Layout chest turns normal only on a key-free site; a development chest keeps its chosen kind.
        private static void VerifyLayoutAndDevelopmentChests(RoomGraphAssembler assembler)
        {
            ChestContentTable table = assembler.ChestContentTable;
            RoomTemplateDefinition template = assembler.Generator.RoomTemplates.FirstOrDefault(candidate =>
                candidate != null && candidate.RoomPrefabAsset != null &&
                candidate.AuthoredChests.Any(chest => chest.Kind == ChestKind.Golden));
            Assert(template != null, "Drop-1 needs a Game Scene Layout with a golden chest.");
            List<GameObject> roots = new();
            GameObject progressHolder = new("Drop-1 progress");
            roots.Add(progressHolder);
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                foreach (bool keyFree in new[] { true, false })
                {
                    GameObject instance = Object.Instantiate(template.RoomPrefabAsset, Origin, Quaternion.identity);
                    roots.Add(instance);
                    RoomRunState state = new($"drop-1-{keyFree}");
                    RoomChestSite site = new(instance.GetComponent<RoomPrefab>(), template, state, progress, null,
                        keyFree);
                    Assert(RoomChestSpawner.TrySpawnAuthoredChests(table, site, 77, out List<TreasureChest> spawned,
                            out string error) && spawned.Count == template.AuthoredChests.Count, error);
                    foreach (RoomTemplateChest authored in template.AuthoredChests)
                    {
                        ChestKind wanted = ChestContentTable.ResolveKindForFloor(authored.Kind, keyFree);
                        Assert(spawned.Single(chest => chest.ChestId == authored.ChestId).Kind == wanted &&
                               state.GetChest(authored.ChestId).Kind == wanted,
                            $"Layout chest {authored.ChestId} must be {wanted} when key-free is {keyFree}.");
                    }

                    if (!keyFree) continue;
                    TreasureChest development = RoomChestSpawner.SpawnDevelopmentChest(table, site, 77,
                        ChestKind.Golden, out error);
                    Assert(development != null && development.Kind == ChestKind.Golden,
                        $"A development golden chest must stay golden on floor 1. {error}");
                }
            }
            finally
            {
                foreach (GameObject root in roots) Object.DestroyImmediate(root);
            }
        }

        private static (int seed, string roomId) FindGoldenRoll(FloorGenerator generator, ChestContentTable table,
            int floorNumber)
        {
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                if (!generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                GeneratedRoomNode room = graph.FindFloor(floorNumber)?.Nodes.FirstOrDefault(node =>
                    node.Role == GeneratedRoomRole.Intermediate && node.Encounter != null &&
                    table.TryRollChest(RoomClearRewardSpawner.DeriveChestSeed(node.ContentSeed), out ChestKind kind) &&
                    kind == ChestKind.Golden);
                if (room != null) return (seed, room.RoomId);
            }

            throw new InvalidOperationException($"No seed rolls a golden clear chest on floor {floorNumber}.");
        }

        private static RoomPrefab Room(RoomGraphAssembler assembler, string roomId) =>
            assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(room => room.Node.RoomId == roomId);

        private static RoomClearRewardSpawner Spawner(RoomGraphAssembler assembler, string roomId) =>
            Room(assembler, roomId).Controller.GetComponent<RoomClearRewardSpawner>();

        private static int ContentChildren(RoomGraphAssembler assembler, string roomId) =>
            Room(assembler, roomId).Node.ContentRoot.transform.childCount;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
