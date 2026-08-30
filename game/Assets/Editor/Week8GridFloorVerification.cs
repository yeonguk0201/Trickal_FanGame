using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week8GridFloorVerification
    {
        [MenuItem("Trickal Fan Game/Verify Phase F-5 Seeded Grid Floors")]
        public static void Verify()
        {
            Week8GridFloorSetup.Setup();
            string prefabGuid = AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath);
            Week8GridFloorSetup.Setup();

            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = generator != null ? generator.GetComponent<RoomGraphAssembler>() : null;
            Assert(generator != null && assembler != null && assembler.ConfiguredRoomPrefab != null,
                "Run Phase F-5 Setup before verification.");
            Assert(generator.FloorCount == 3 && generator.MinimumRoomsPerFloor == 6 &&
                   generator.MaximumRoomsPerFloor == 8 && generator.MinimumBossDistance == 3 &&
                   generator.GenerationRetryLimit == 32,
                "Phase F-5 generator settings must be 3 floors, 6-8 rooms, distance 3, and 32 retries.");
            Assert(prefabGuid == AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath) &&
                   CountComponents<RoomGraphAssembler>(generator.gameObject) == 1 &&
                   CountGeneratedFloorRoots(generator.transform) == 1,
                "Running Setup twice must preserve the prefab GUID and avoid duplicate components or floor roots.");

            ValidateGeneration(generator);
            ValidateFailureAndExpansion(generator);
            ValidateMissingLegacyNodesAreReplaced(generator, assembler.ConfiguredRoomPrefab);
            ValidatePrefabAndAssembly(assembler);
            ValidateThreeFloorProgression(generator, assembler.ConfiguredRoomPrefab);
            Debug.Log("Phase F-5 verification passed: deterministic independent seeds, 6-8 room Random Growth, stable IDs, connectivity, required rooms, boss distance, shared 16x9 room layout and camera framing, wall-aligned doors, safe transition positions, directional slot binding, green normal doors, purple boss connections, cyan floor exits, state restoration, repeated encounter prefabs, bounded failures, 8-12 configuration expansion, visible gated floor exits, 1->2->3 progression, final Run clear, and idempotent Setup are valid.");
        }

        private static void ValidateGeneration(FloorGenerator generator)
        {
            const int seed = Week8RandomRoomSetup.FixedVerificationSeed;
            Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
            Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeated, out error), error);
            Assert(Signature(first) == Signature(repeated), "The same seed must reproduce all three floors exactly.");

            bool foundDifferent = false; int directionMasks = 0;
            for (int candidateSeed = seed + 1; candidateSeed < seed + 129; candidateSeed++)
            {
                Assert(generator.TryGenerateForSeed(candidateSeed, out GeneratedFloorGraph candidate, out error), error);
                foundDifferent |= Signature(candidate) != Signature(first);
                directionMasks |= CollectDirectionCombinations(candidate);
            }
            Assert(foundDifferent, "At least one different seed must change topology, role, or content.");
            const int requiredMasks = (1 << 5) | (1 << 10) | (1 << 3) | (1 << 12);
            Assert((directionMasks & requiredMasks) == requiredMasks,
                "Seed coverage must produce left+up, right+down, left+right, and up+down door combinations.");

            foreach (GeneratedFloor floor in first.Floors)
            {
                Assert(floor.Nodes.Count >= 6 && floor.Nodes.Count <= 8, $"Floor {floor.FloorNumber} is outside 6-8 rooms.");
                Assert(floor.FloorSeed != floor.TopologySeed && floor.TopologySeed != floor.ContentSeed,
                    $"Floor {floor.FloorNumber} seed streams must be independently derived.");
                int starts = 0, bosses = 0, treasures = 0;
                foreach (GeneratedRoomNode node in floor.Nodes)
                {
                    Assert(node.RoomId == FloorGenerator.BuildRoomId(node.FloorNumber, node.RoomNumber), "Room ID depends on mutable generation data.");
                    starts += node.Role == GeneratedRoomRole.Start ? 1 : 0;
                    bosses += node.Role == GeneratedRoomRole.Boss ? 1 : 0;
                    treasures += node.Role == GeneratedRoomRole.Treasure ? 1 : 0;
                }
                Assert(starts == 1 && bosses == 1 && treasures >= 1, $"Floor {floor.FloorNumber} lacks required room roles.");
            }
        }

        private static void ValidateFailureAndExpansion(FloorGenerator configured)
        {
            GameObject holder = new("Phase F-5 Generator Boundaries");
            try
            {
                FloorGenerator probe = holder.AddComponent<FloorGenerator>();
                RoomDefinition[] definitions = CopyDefinitions(configured);
                probe.Configure(1, 6, 6, 99, 2, definitions);
                Assert(!probe.TryGenerateForSeed(7711, out _, out string error) &&
                       error.Contains("7711", StringComparison.Ordinal) &&
                       error.Contains("2 attempts", StringComparison.Ordinal) &&
                       error.Contains("distance 99", StringComparison.Ordinal),
                    "Bounded generation failure must include the seed, retry limit, and failed invariant.");
                probe.Configure(1, 8, 12, 3, 32, definitions);
                Assert(probe.TryGenerateForSeed(8822, out GeneratedFloorGraph expanded, out error), error);
                Assert(expanded.Nodes.Count >= 8 && expanded.Nodes.Count <= 12,
                    "Changing settings alone must support an 8-12 room contract.");
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }

        private static void ValidatePrefabAndAssembly(RoomGraphAssembler assembler)
        {
            Assert(assembler.ConfiguredRoomPrefab.TryValidate(out string error), error);
            ValidateLayoutPrefab(assembler.ConfiguredRoomPrefab);
            Assert(assembler.Generator.TryGenerateForSeed(Week8RandomRoomSetup.FixedVerificationSeed,
                out GeneratedFloorGraph generated, out error), error);
            Assert(assembler.TryApplyGeneratedGraphForVerification(Week8RandomRoomSetup.FixedVerificationSeed, out error), error);
            GeneratedFloor floor = generated.FindFloor(1);
            Dictionary<string, GeneratedRoomNode> generatedNodes = new(StringComparer.Ordinal);
            foreach (GeneratedRoomNode node in floor.Nodes) generatedNodes.Add(node.RoomId, node);

            Camera layoutCamera = assembler.Graph.RoomCamera != null
                ? assembler.Graph.RoomCamera.RoomCamera : null;
            Assert(layoutCamera != null && layoutCamera.orthographic &&
                   Mathf.Approximately(layoutCamera.orthographicSize, RoomLayout.CameraOrthographicSize),
                "The room camera must use the shared expanded-layout framing.");

            Assert(assembler.Graph.Nodes.Count == floor.Nodes.Count, "Only the current floor must be assembled.");
            int activeRooms = 0;
            RoomNode treasureNode = null;
            foreach (RoomNode node in assembler.Graph.Nodes)
            {
                GeneratedRoomNode generatedNode = generatedNodes[node.RoomId];
                RoomPrefab instance = node.GetComponent<RoomPrefab>();
                Assert(instance != null && instance.TryValidate(out error), error);
                Vector3 expectedRoomPosition = new(
                    generatedNode.GridPosition.X * RoomLayout.RoomSpacingX,
                    generatedNode.GridPosition.Y * RoomLayout.RoomSpacingY,
                    0f);
                Assert((instance.transform.localPosition - expectedRoomPosition).sqrMagnitude < 0.0001f,
                    $"{node.RoomId} must use the shared room-grid spacing.");
                foreach (RoomDoorDirection direction in Enum.GetValues(typeof(RoomDoorDirection)))
                {
                    RoomDoorSlot slot = instance.FindSlot(direction);
                    bool connected = generatedNode.TryGetConnection(direction, out GeneratedRoomConnection connection);
                    Assert(slot.IsConnected == connected && slot.Seal.activeSelf != connected && slot.Blocker.gameObject.activeSelf == connected,
                        $"{node.RoomId} {direction} slot does not match generated connectivity.");
                    if (connected)
                    {
                        bool expectsBossVisual = generatedNode.Role == GeneratedRoomRole.Boss ||
                                                 generatedNodes[connection.DestinationRoomId].Role == GeneratedRoomRole.Boss;
                        DoorVisualKind expectedVisualKind = expectsBossVisual
                            ? DoorVisualKind.Boss : DoorVisualKind.Normal;
                        Assert(slot.Doorway.Destination.RoomId == connection.DestinationRoomId &&
                               slot.Doorway.DestinationEntryPoint == slot.Doorway.Destination.GetComponent<RoomPrefab>()
                                   .FindSlot(GeneratedFloorGraph.Opposite(direction)).EntryPoint,
                            $"{node.RoomId} {direction} must target the opposite destination entry point.");
                        Assert(slot.Blocker.VisualKind == expectedVisualKind,
                            $"{node.RoomId} {direction} must use the {expectedVisualKind} connection-door palette.");

                        Collider2D transitionTrigger = slot.Doorway.GetComponent<Collider2D>();
                        Collider2D blockerCollider = slot.Blocker.GetComponent<Collider2D>();
                        Vector2 outward = direction switch
                        {
                            RoomDoorDirection.Left => Vector2.left,
                            RoomDoorDirection.Right => Vector2.right,
                            RoomDoorDirection.Up => Vector2.up,
                            _ => Vector2.down,
                        };
                        Assert(transitionTrigger != null && transitionTrigger.enabled && transitionTrigger.isTrigger &&
                               slot.Doorway.transform.localPosition.sqrMagnitude < 0.0001f,
                            $"{node.RoomId} {direction} transition trigger must remain active at the door-slot center.");
                        Assert(Vector2.Dot(slot.EntryPoint.localPosition, -outward) > 0.5f,
                            $"{node.RoomId} {direction} destination entry point must remain safely inside the room.");

                        slot.Blocker.SetLocked(true);
                        Assert(slot.Blocker.IsLocked && slot.Blocker.IsPortalBarrierActive && blockerCollider.enabled &&
                               transitionTrigger.enabled && transitionTrigger.isTrigger,
                            $"{node.RoomId} {direction} locked door must block without disabling its transition trigger.");
                        Assert(Approximately(slot.Blocker.VisualColor, expectsBossVisual
                                ? DoorController.BossLockedColor : DoorController.NormalLockedColor),
                            $"{node.RoomId} {direction} locked door color does not match its connection kind.");
                        slot.Blocker.SetLocked(false);
                        Assert(!slot.Blocker.IsLocked && slot.Blocker.IsPortalBarrier &&
                               slot.Blocker.IsPortalBarrierActive && blockerCollider.enabled &&
                               transitionTrigger.enabled && transitionTrigger.isTrigger,
                            $"{node.RoomId} {direction} open door must preserve its portal barrier and transition trigger.");
                        Assert(Approximately(slot.Blocker.VisualColor, expectsBossVisual
                                ? DoorController.BossOpenColor : DoorController.NormalOpenColor),
                            $"{node.RoomId} {direction} open door color does not match its connection kind.");
                    }
                }
                RoomController controller = instance.Controller;
                for (int i = 0; i < controller.EnemyPrefabs.Count; i++)
                    Assert(controller.EnemyPrefabs[i] == generatedNode.Definition.EncounterPrefabs[i % generatedNode.Definition.EncounterPrefabs.Count],
                        "Multiple spawn points must intentionally repeat the selected definition prefab sequence.");
                if (node.IsVisible) activeRooms++;
                if (generatedNode.Role == GeneratedRoomRole.Treasure) treasureNode = node;
            }

            Assert(assembler.Graph.TryReplaceFloor(ToArray(assembler.Graph.Nodes),
                FindNode(assembler.Graph.Nodes, floor.StartingRoomId), assembler.Graph.Player, out error), error);
            activeRooms = 0; foreach (RoomNode node in assembler.Graph.Nodes) if (node.IsVisible) activeRooms++;
            Assert(activeRooms == 1, "Exactly one current room must be active after floor entry.");
            Vector2 actualPlayerPosition = assembler.Graph.Player != null
                ? PlayerPosition(assembler.Graph.Player) : Vector2.zero;
            Vector2 expectedPlayerPosition = assembler.Graph.CurrentNode.DefaultEntryPoint.position;
            Assert(assembler.Graph.Player == null ||
                   (actualPlayerPosition - expectedPlayerPosition).sqrMagnitude < 0.0001f,
                $"Initial floor assembly must place the Player at the starting room entry point so Player projectiles are visible in the active room. Expected {expectedPlayerPosition}, got {actualPlayerPosition}.");

            Assert(treasureNode != null, "Floor 1 needs a treasure room for state restoration verification.");
            RoomRunState state = assembler.Progress.GetRoomState(treasureNode.RoomId);
            state.MarkCleared(); state.MarkArtifactClaimed();
            Assert(assembler.TryLoadFloor(2, null, out error) && assembler.Graph.Nodes.Count == generated.FindFloor(2).Nodes.Count, error);
            Assert(assembler.TryLoadFloor(1, null, out error), error);
            RoomNode restored = FindNode(assembler.Graph.Nodes, treasureNode.RoomId);
            RoomPrefab restoredPrefab = restored.GetComponent<RoomPrefab>();
            Assert(restoredPrefab.Controller.State == RoomState.Cleared && restoredPrefab.RewardRoom.HasRewarded,
                "Clear and artifact state must survive floor unloading and reassembly without rerolling.");
            Assert(CountGeneratedFloorRoots(assembler.transform) == 1, "Floor switching must clean the previous floor instance.");
        }

        private static void ValidateLayoutPrefab(RoomPrefab prefab)
        {
            Assert(Mathf.Approximately(RoomLayout.Width, 16f) && Mathf.Approximately(RoomLayout.Height, 9f) &&
                   Mathf.Approximately(RoomLayout.RoomSpacingX, 20f) && Mathf.Approximately(RoomLayout.RoomSpacingY, 13f),
                "The shared expanded layout must remain 16x9 with 20x13 grid spacing.");
            Assert(Mathf.Approximately(RoomLayout.TransitionInnerEdgeInset, 0.35f) &&
                   Mathf.Approximately(RoomLayout.SafeEntryInsetFromWall, 1.9f),
                "Room transitions must activate close to the door while preserving the existing safe entry depth.");
            BoxCollider2D encounter = prefab.Controller.GetComponent<BoxCollider2D>();
            Assert(encounter != null && Approximately(encounter.size, RoomLayout.EncounterSize),
                "The encounter trigger must use the shared expanded-room bounds.");
            Transform content = prefab.Node.ContentRoot.transform;
            Vector2 horizontalWallSize = new(RoomLayout.HorizontalWallSegmentLength, RoomLayout.WallThickness);
            Vector2 verticalWallSize = new(RoomLayout.WallThickness, RoomLayout.VerticalWallSegmentLength);
            AssertWall(content, "Top Left Wall",
                new Vector2(-RoomLayout.HorizontalWallSegmentCenter, RoomLayout.VerticalWallCenter), horizontalWallSize);
            AssertWall(content, "Top Right Wall",
                new Vector2(RoomLayout.HorizontalWallSegmentCenter, RoomLayout.VerticalWallCenter), horizontalWallSize);
            AssertWall(content, "Bottom Left Wall",
                new Vector2(-RoomLayout.HorizontalWallSegmentCenter, -RoomLayout.VerticalWallCenter), horizontalWallSize);
            AssertWall(content, "Bottom Right Wall",
                new Vector2(RoomLayout.HorizontalWallSegmentCenter, -RoomLayout.VerticalWallCenter), horizontalWallSize);
            AssertWall(content, "Left Upper Wall",
                new Vector2(-RoomLayout.HorizontalWallCenter, RoomLayout.VerticalWallSegmentCenter), verticalWallSize);
            AssertWall(content, "Left Lower Wall",
                new Vector2(-RoomLayout.HorizontalWallCenter, -RoomLayout.VerticalWallSegmentCenter), verticalWallSize);
            AssertWall(content, "Right Upper Wall",
                new Vector2(RoomLayout.HorizontalWallCenter, RoomLayout.VerticalWallSegmentCenter), verticalWallSize);
            AssertWall(content, "Right Lower Wall",
                new Vector2(RoomLayout.HorizontalWallCenter, -RoomLayout.VerticalWallSegmentCenter), verticalWallSize);
            for (int i = 0; i < prefab.Controller.SpawnPoints.Count; i++)
            {
                Assert(Approximately(prefab.Controller.SpawnPoints[i].localPosition,
                        RoomLayout.SpawnPosition(i, prefab.Controller.SpawnPoints.Count)),
                    $"Spawn {i + 1} must use the shared expanded-room position.");
            }

            foreach (RoomDoorDirection direction in Enum.GetValues(typeof(RoomDoorDirection)))
            {
                RoomDoorSlot slot = prefab.FindSlot(direction);
                Vector2 normal = RoomLayout.Direction(direction);
                Vector2 expectedSlot = normal * RoomLayout.TransitionCenter(direction);
                Vector2 expectedWallCenter = normal * (RoomLayout.IsSideDoor(direction)
                    ? RoomLayout.HorizontalWallCenter : RoomLayout.VerticalWallCenter);
                Assert(Approximately(slot.transform.localPosition, expectedSlot),
                    $"{direction} transition trigger must stay inside the wall center by the shared inset.");
                Assert(Approximately((Vector2)slot.transform.localPosition +
                                     (Vector2)slot.Blocker.transform.localPosition, expectedWallCenter) &&
                       Approximately((Vector2)slot.transform.localPosition +
                                     (Vector2)slot.Seal.transform.localPosition, expectedWallCenter),
                    $"{direction} door and boundary seal must align with the wall center line.");
                Vector2 expectedEntry = normal *
                    (RoomLayout.TransitionCenter(direction) - RoomLayout.EntryInsetFromTransition);
                Assert(Approximately((Vector2)slot.transform.localPosition +
                                     (Vector2)slot.EntryPoint.localPosition, expectedEntry),
                    $"{direction} entry point must remain at the shared safe interior position.");
            }
        }

        private static void AssertWall(Transform content, string wallName, Vector2 expectedPosition, Vector2 expectedSize)
        {
            Transform wall = content.Find(wallName);
            Assert(wall != null && Approximately(wall.localPosition, expectedPosition) &&
                   Approximately(new Vector2(wall.localScale.x, wall.localScale.y), expectedSize),
                $"{wallName} must use the shared expanded-room position and size.");
        }

        private static void ValidateMissingLegacyNodesAreReplaced(
            FloorGenerator generator,
            RoomPrefab prefab)
        {
            GameObject holder = new("Phase F-5 Missing Legacy Node Regression");
            try
            {
                RunProgress progress = holder.AddComponent<RunProgress>();
                RoomGraphController graph = holder.AddComponent<RoomGraphController>();
                graph.Configure(new RoomNode[] { null }, null, null, null, progress);
                RoomGraphAssembler assembler = holder.AddComponent<RoomGraphAssembler>();
                assembler.Configure(generator, graph, progress, prefab);

                Assert(assembler.TryApplyGeneratedGraphForVerification(
                    Week8RandomRoomSetup.FixedVerificationSeed,
                    out string error),
                    $"F-5 must replace stale legacy RoomNode references before validating the new graph. {error}");
                Assert(graph.TryValidateConfiguration(out error), error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static void ValidateThreeFloorProgression(FloorGenerator configured, RoomPrefab prefab)
        {
            GameObject root = new("Phase F-5 Three Floor Progression");
            GameObject playerObject = new("Phase F-5 Progression Player");
            playerObject.transform.SetParent(root.transform);
            playerObject.SetActive(false);
            Rigidbody2D body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            Health playerHealth = playerObject.AddComponent<Health>();
            playerObject.AddComponent<PlayerStats>();
            PlayerMovement player = playerObject.AddComponent<PlayerMovement>();

            try
            {
                RunProgress progress = root.AddComponent<RunProgress>();
                Assert(progress.TryInitializeRunSeed(Week8RandomRoomSetup.FixedVerificationSeed, out string error), error);
                FloorGenerator generator = root.AddComponent<FloorGenerator>();
                generator.Configure(3, 6, 8, 3, 32, CopyDefinitions(configured));
                RoomGraphController graph = root.AddComponent<RoomGraphController>();
                graph.Configure(Array.Empty<RoomNode>(), null, player, null, progress);
                RoomGraphAssembler assembler = root.AddComponent<RoomGraphAssembler>();
                assembler.Configure(generator, graph, progress, prefab);
                Assert(assembler.TryApplyGeneratedGraphForVerification(
                    Week8RandomRoomSetup.FixedVerificationSeed, out error), error);

                GameObject sessionObject = new("Phase F-5 Run Session");
                sessionObject.transform.SetParent(root.transform);
                sessionObject.SetActive(false);
                RunSession session = sessionObject.AddComponent<RunSession>();
                session.Configure(playerHealth, progress, null);
                session.SetResultSavingEnabled(false);
                Assert(session.BeginRun("erpin"), "The F-5 progression verification Run must start.");

                for (int floorNumber = 1; floorNumber <= 2; floorNumber++)
                {
                    GeneratedFloor floor = assembler.GeneratedGraph.FindFloor(floorNumber);
                    RoomNode bossNode = FindNode(graph.Nodes, floor.BossRoomId);
                    RoomController bossController = bossNode.GetComponent<RoomPrefab>().Controller;
                    FloorAdvancePortal[] portals = assembler.GetComponentsInChildren<FloorAdvancePortal>(true);
                    Assert(portals.Length == 1, $"Floor {floorNumber} must contain exactly one next-floor exit.");
                    FloorAdvancePortal portal = portals[0];
                    Assert(portal.DestinationFloor == floorNumber + 1 && portal.Indicator != null &&
                           portal.Indicator.sprite != null && !portal.IsUnlocked &&
                           Approximately(portal.Indicator.color, FloorAdvancePortal.LockedColor),
                        $"Floor {floorNumber} exit must be visible, target the next floor, and stay locked before boss clear.");

                    ChangeRoomState(bossController, RoomState.Cleared);
                    Assert(portal.IsUnlocked &&
                           Approximately(portal.Indicator.color, FloorAdvancePortal.UnlockedColor),
                        $"Floor {floorNumber} exit must use the bright cyan palette after boss clear.");
                    Assert(portal.IsUnlocked && portal.TryEnter(player),
                        $"Clearing floor {floorNumber} must unlock and enter floor {floorNumber + 1}.");
                    Assert(progress.CurrentFloor == floorNumber + 1 && graph.CurrentNode.FloorNumber == floorNumber + 1 &&
                           graph.CurrentNode.RoomId == assembler.GeneratedGraph.FindFloor(floorNumber + 1).StartingRoomId,
                        $"Floor {floorNumber} exit must place the Player in the next floor starting room.");
                }

                Assert(assembler.GetComponentsInChildren<FloorAdvancePortal>(true).Length == 0,
                    "The final floor must not create a next-floor exit.");
                GeneratedFloor finalFloor = assembler.GeneratedGraph.FindFloor(3);
                RoomController finalBossRoom = FindNode(graph.Nodes, finalFloor.BossRoomId)
                    .GetComponent<RoomPrefab>().Controller;
                ChangeRoomState(finalBossRoom, RoomState.Cleared);
                Assert(session.HasEnded && session.IsCleared && progress.IsProgressionStopped,
                    "Clearing the floor 3 boss room must finish the existing RunSession clear flow exactly once.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ChangeRoomState(RoomController controller, RoomState state)
        {
            MethodInfo method = typeof(RoomController).GetMethod(
                "ChangeState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(typeof(RoomController).FullName, "ChangeState");
            method.Invoke(controller, new object[] { state });
        }

        private static int CollectDirectionCombinations(GeneratedFloorGraph graph)
        {
            int result = 0;
            foreach (GeneratedRoomNode node in graph.Nodes)
            {
                int mask = 0; foreach (GeneratedRoomConnection connection in node.DirectionalConnections) mask |= 1 << (int)connection.Direction;
                result |= 1 << mask;
            }
            return result;
        }
        private static string Signature(GeneratedFloorGraph graph)
        {
            StringBuilder value = new();
            foreach (GeneratedFloor floor in graph.Floors)
            {
                value.Append(floor.FloorSeed).Append('/').Append(floor.TopologySeed).Append('/').Append(floor.ContentSeed).Append('|');
                foreach (GeneratedRoomNode node in floor.Nodes)
                {
                    value.Append(node.RoomId).Append('@').Append(node.GridPosition).Append(':').Append(node.Role).Append(':')
                        .Append(node.ContentSeed).Append(':').Append(node.Definition.RoomDefinitionId);
                    foreach (GeneratedRoomConnection connection in node.DirectionalConnections)
                        value.Append('>').Append(connection.Direction).Append('=').Append(connection.DestinationRoomId);
                    value.Append('|');
                }
            }
            return value.ToString();
        }
        private static RoomDefinition[] CopyDefinitions(FloorGenerator generator)
        { RoomDefinition[] result = new RoomDefinition[generator.RoomDefinitions.Count];
          for (int i = 0; i < result.Length; i++) result[i] = generator.RoomDefinitions[i]; return result; }
        private static RoomNode[] ToArray(IReadOnlyList<RoomNode> nodes)
        { RoomNode[] result = new RoomNode[nodes.Count]; for (int i = 0; i < result.Length; i++) result[i] = nodes[i]; return result; }
        private static RoomNode FindNode(IReadOnlyList<RoomNode> nodes, string id)
        { foreach (RoomNode node in nodes) if (node.RoomId == id) return node; return null; }
        private static Vector2 PlayerPosition(PlayerMovement player)
        { Rigidbody2D body = player.GetComponent<Rigidbody2D>(); return body != null ? body.position : player.transform.position; }
        private static int CountGeneratedFloorRoots(Transform parent)
        { int count = 0; for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name.StartsWith("Generated Floor ", StringComparison.Ordinal)) count++; return count; }
        private static int CountComponents<T>(GameObject target) where T : Component => target.GetComponents<T>().Length;
        private static bool Approximately(Color first, Color second) =>
            Mathf.Abs(first.r - second.r) < 0.001f && Mathf.Abs(first.g - second.g) < 0.001f &&
            Mathf.Abs(first.b - second.b) < 0.001f && Mathf.Abs(first.a - second.a) < 0.001f;
        private static bool Approximately(Vector2 first, Vector2 second) =>
            (first - second).sqrMagnitude < 0.0001f;
        private static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
