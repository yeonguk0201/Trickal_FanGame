using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week20Special1Verification
    {
        [MenuItem("Trickal Fan Game/Week 20/Verify Special-1 Locked Treasure Rooms")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null,
                "Special-1 requires the configured Game Scene generator and assembler.");

            int runtimeSeed = ValidateDeterministicOptionalLocks(generator);
            ValidateAtomicKeySpending();
            ValidateRuntimeUnlockAndReload(assembler, runtimeSeed);
            Debug.Log("Special-1 verification passed: treasure key locks are 50% seed-deterministic, never " +
                      "block the boss route, consume one key exactly once, reject entry without a key, and " +
                      "remain open after floor unload/rebuild while the required route remains key-free.");
        }

        private static int ValidateDeterministicOptionalLocks(FloorGenerator generator)
        {
            int locked = 0;
            int unlocked = 0;
            int runtimeSeed = 0;
            for (int seed = 1; seed <= 512; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeated, out error), error);
                for (int floorNumber = 1; floorNumber <= first.Floors.Count; floorNumber++)
                {
                    GeneratedFloor floor = first.FindFloor(floorNumber);
                    GeneratedFloor repeatedFloor = repeated.FindFloor(floorNumber);
                    GeneratedRoomNode treasure = floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Treasure);
                    GeneratedRoomNode repeatedTreasure = repeatedFloor.Nodes.Single(node =>
                        node.Role == GeneratedRoomRole.Treasure);
                    Assert(treasure.RequiresKey == repeatedTreasure.RequiresKey,
                        $"Seed {seed}, floor {floorNumber} changed its treasure lock result.");
                    if (treasure.RequiresKey)
                    {
                        locked++;
                        Assert(CanReachBossWithoutTreasure(floor, treasure.RoomId),
                            $"Seed {seed}, floor {floorNumber} locked treasure blocks the boss route.");
                        if (floorNumber == 1 && runtimeSeed == 0) runtimeSeed = seed;
                    }
                    else
                    {
                        unlocked++;
                    }
                }
            }

            float observed = locked / (float)(locked + unlocked);
            Assert(locked > 0 && unlocked > 0 && observed >= 0.2f && observed <= 0.5f,
                $"Optional-route filtering should still expose locked and open treasure rooms; observed {observed:P1} locked.");
            Assert(runtimeSeed > 0, "No floor-1 locked treasure room was found for runtime verification.");
            return runtimeSeed;
        }

        private static bool CanReachBossWithoutTreasure(GeneratedFloor floor, string treasureId)
        {
            Dictionary<string, GeneratedRoomNode> byId = floor.Nodes.ToDictionary(node => node.RoomId,
                StringComparer.Ordinal);
            HashSet<string> visited = new(StringComparer.Ordinal) { floor.StartingRoomId };
            Queue<string> queue = new();
            queue.Enqueue(floor.StartingRoomId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (current == floor.BossRoomId) return true;
                foreach (GeneratedRoomConnection connection in byId[current].DirectionalConnections)
                    if (connection.DestinationRoomId != treasureId && visited.Add(connection.DestinationRoomId))
                        queue.Enqueue(connection.DestinationRoomId);
            }
            return false;
        }

        private static void ValidateAtomicKeySpending()
        {
            GameObject root = new("Special-1 wallet verification");
            try
            {
                RunProgress progress = root.AddComponent<RunProgress>();
                Assert(!progress.TrySpendResource(RunResourceType.Key) &&
                       progress.GetResourceCount(RunResourceType.Key) == 0,
                    "A missing key must not be spent or underflow.");
                Assert(progress.TryAddResource(RunResourceType.Key, 2) == 2 &&
                       progress.TrySpendResource(RunResourceType.Key) &&
                       progress.GetResourceCount(RunResourceType.Key) == 1,
                    "Spending must remove exactly one key.");
                progress.StopProgression();
                Assert(!progress.TrySpendResource(RunResourceType.Key) &&
                       progress.GetResourceCount(RunResourceType.Key) == 1,
                    "Keys must not be spent after Run progression stops.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRuntimeUnlockAndReload(RoomGraphAssembler assembler, int seed)
        {
            assembler.Progress.ResetProgress();
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            GeneratedFloor floor = assembler.GeneratedGraph.FindFloor(1);
            GeneratedRoomNode treasure = floor.Nodes.Single(node =>
                node.Role == GeneratedRoomRole.Treasure && node.RequiresKey);
            GeneratedRoomNode source = floor.Nodes.Single(node =>
                node.DirectionalConnections.Any(connection =>
                    !connection.IsSecret && connection.DestinationRoomId == treasure.RoomId));
            GeneratedRoomConnection connection = source.DirectionalConnections.Single(candidate =>
                candidate.DestinationRoomId == treasure.RoomId);
            RoomDoorSlot slot = FindRuntimeRoom(assembler, source.RoomId).FindSlot(connection.Direction);
            RoomRunState state = assembler.Progress.GetRoomState(treasure.RoomId);
            RoomPrefab sourceRoom = FindRuntimeRoom(assembler, source.RoomId);
            sourceRoom.Controller.BindRunState(assembler.Progress.GetRoomState(source.RoomId), true);
            Assert(assembler.Graph.TryReplaceFloor(assembler.Graph.Nodes.ToArray(), sourceRoom.Node,
                assembler.Graph.Player, out error), error);

            Assert(slot.Doorway.RequiresKey && !slot.Doorway.IsKeyLockOpen && slot.Blocker.IsLocked &&
                   slot.Blocker.VisualKind == DoorVisualKind.KeyLockedTreasure &&
                   Approximately(slot.Blocker.VisualColor, DoorController.KeyLockedColor),
                "The doorway into a locked treasure room must expose the key lock and locked palette.");
            Assert(!slot.Doorway.TryEnter(assembler.Graph.Player) && !state.IsKeyLockOpen &&
                   assembler.Graph.CurrentNode == sourceRoom.Node,
                "A locked treasure room must reject entry without a key.");
            Assert(assembler.Progress.TryAddResource(RunResourceType.Key, 1) == 1 &&
                   slot.Doorway.TryEnter(assembler.Graph.Player) && state.IsKeyLockOpen &&
                   assembler.Progress.GetResourceCount(RunResourceType.Key) == 0 && !slot.Blocker.IsLocked &&
                   assembler.Graph.CurrentNode.RoomId == treasure.RoomId &&
                   Approximately(slot.Blocker.VisualColor, DoorController.KeyOpenColor),
                "The first successful entry must consume one key, open the doorway, and enter the treasure room.");
            Assert(slot.Doorway.TryUnlockWithKey() && assembler.Progress.GetResourceCount(RunResourceType.Key) == 0,
                "Reusing an open treasure doorway must not consume another key.");

            Assert(assembler.TryLoadFloor(2, null, out error) && assembler.TryLoadFloor(1, null, out error), error);
            RoomDoorSlot restored = FindRuntimeRoom(assembler, source.RoomId).FindSlot(connection.Direction);
            Assert(restored.Doorway.RequiresKey && restored.Doorway.IsKeyLockOpen &&
                   !restored.Blocker.IsLocked && assembler.Progress.GetRoomState(treasure.RoomId).IsKeyLockOpen,
                "An unlocked treasure doorway must remain open after floor unload and rebuild.");
        }

        private static RoomPrefab FindRuntimeRoom(RoomGraphAssembler assembler, string roomId)
        {
            foreach (RoomNode node in assembler.Graph.Nodes)
                if (node.RoomId == roomId) return node.GetComponent<RoomPrefab>();
            return null;
        }

        private static bool Approximately(Color first, Color second) =>
            Mathf.Abs(first.r - second.r) < 0.001f && Mathf.Abs(first.g - second.g) < 0.001f &&
            Mathf.Abs(first.b - second.b) < 0.001f && Mathf.Abs(first.a - second.a) < 0.001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
