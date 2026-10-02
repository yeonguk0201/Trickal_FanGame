using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Room8Verification
    {
        private const int SeedCount = 256;
        private const float PositionTolerance = 0.001f;

        [MenuItem("Trickal Fan Game/Week 14/Verify Room-8 Full Run Integration")]
        public static void Verify()
        {
            // Validate the current catalog below. Room-7's historical pillar-crossfire is no longer a required Encounter.
            Week18Obstacle2Setup.OpenGameScene();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(assembler != null && assembler.Generator != null && assembler.Graph != null &&
                   assembler.Progress != null && assembler.EnemyRoster != null &&
                   assembler.EncounterClearDropTable != null,
                "Run Room-7 Setup before Room-8 verification.");
            ValidateThreeFloorSeeds(assembler.Generator);
            ValidateRendererVisibilityBoundary(assembler.Generator);
            ValidateHorizontalMixedSizeTransitions(assembler);
            ValidateWaveRevisitAndRewardAcrossFloorReload(assembler);
            Debug.Log("Week 14 Room-8 verification passed: 256 complete three-floor seeds are deterministic, " +
                      "all templates and Encounter waves remain compatible, Wide/Large <-> Basic/Small " +
                      "horizontal transitions snap the camera and expose exactly one room, and a partial wave, " +
                      "room clear, and single reward survive floor unload/reload without duplicate enemies or rewards.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week14Room7Setup.Setup();
            Week14Room7Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Room-8 prerequisite setup changed the Game Scene GUID.");
            Verify();
        }

        private static void ValidateThreeFloorSeeds(FloorGenerator generator)
        {
            HashSet<string> templates = new(StringComparer.Ordinal);
            HashSet<string> encounters = new(StringComparer.Ordinal);
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph second, out error), error);
                Assert(first.Floors.Count == 3 && Signature(first) == Signature(second),
                    $"Seed {seed} did not reproduce the same complete three-floor Run.");

                foreach (GeneratedFloor floor in first.Floors)
                {
                    int secretRooms = floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Secret);
                    int shopRooms = floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Shop);
                    int regularRooms = floor.Nodes.Count - secretRooms - shopRooms;
                    bool validSize = floor.Settings != null
                        ? floor.Nodes.Count >= floor.Settings.MinimumTotalRooms &&
                          floor.Nodes.Count <= floor.Settings.MaximumTotalRooms
                        : regularRooms >= generator.MinimumRoomsPerFloor && regularRooms <= generator.MaximumRoomsPerFloor;
                    Assert(validSize && secretRooms <= 1 && shopRooms <= 1,
                        $"Seed {seed} floor {floor.FloorNumber} has an invalid room count.");
                    Assert(floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Start) == 1 &&
                           floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Boss) == 1,
                        $"Seed {seed} floor {floor.FloorNumber} needs one start and one boss room.");
                }

                foreach (GeneratedRoomNode node in first.Nodes)
                {
                    Assert(node.Template != null && node.Template.TryValidate(out error), error);
                    Assert(node.Template.SupportsRoomType(node.RoomType) &&
                           node.Template.SupportsConnections(node.DirectionalConnections) &&
                           node.Template.SupportsFloor(node.FloorNumber),
                        $"Room {node.RoomId} received incompatible template '{node.TemplateId}'.");
                    templates.Add(node.TemplateId);
                    if (node.Role != GeneratedRoomRole.Intermediate) continue;
                    Assert(node.Encounter != null && node.Encounter.TryValidateFor(node.Template,
                               node.FloorNumber, node.DirectionalConnections, out error), error);
                    encounters.Add(node.EncounterId);
                    for (int waveIndex = 0; waveIndex < node.Encounter.Waves.Count; waveIndex++)
                    {
                        Assert(node.Encounter.TryResolveWave(node.Template, node.FloorNumber,
                                   node.DirectionalConnections, waveIndex,
                                   out ResolvedEncounterSpawn[] resolved, out error), error);
                        Assert(resolved.Length > 0 &&
                               resolved.Select(spawn => spawn.SpawnPointIndex).Distinct().Count() == resolved.Length,
                            $"Room {node.RoomId} wave {waveIndex + 1} reused a SpawnPoint.");
                    }
                }
            }

            string[] requiredTemplates =
            {
                Week14Room1Setup.BasicTemplateId, Week14Room3Setup.SmallTemplateId,
                Week14Room3Setup.WideTemplateId, Week14Room6Setup.TallTemplateId,
                Week14Room6Setup.LargeTemplateId, Week14Room7Setup.TemplateId,
                Week14Room6Setup.BossFloor1TemplateId, Week14Room6Setup.BossFloor2TemplateId,
                Week14Room6Setup.BossFloor3TemplateId,
            };
            Assert(requiredTemplates.All(templates.Contains),
                "The Room-8 seed range did not exercise every authored Room Template.");
            string[] requiredPatterns = { "pressure", "crossfire", "swarm", "elite-pair" };
            Assert(requiredPatterns.All(pattern => encounters.Any(id => id.EndsWith($"-{pattern}-v1", StringComparison.Ordinal))),
                "The Room-8 seed range did not exercise the four current Encounter patterns.");
        }

        private static void ValidateHorizontalMixedSizeTransitions(RoomGraphAssembler assembler)
        {
            Assert(TryFindHorizontalMixedSizePair(assembler.Generator, out int seed, out int floorNumber,
                out string firstRoomId, out string secondRoomId),
                "Could not find a Wide/Large <-> Basic/Small horizontal transition pair.");
            PrepareRun(assembler, seed);
            Assert(assembler.TryLoadFloor(floorNumber, assembler.Graph.Player, out string error), error);

            RoomGraphController graph = assembler.Graph;
            SerializedObject serializedGraph = new(graph);
            serializedGraph.FindProperty("transitionCooldown").floatValue = 0f;
            serializedGraph.FindProperty("returnDoorwayBlockDuration").floatValue = 0f;
            serializedGraph.ApplyModifiedPropertiesWithoutUndo();
            RoomNode first = graph.Nodes.Single(node => node.RoomId == firstRoomId);
            RoomNode second = graph.Nodes.Single(node => node.RoomId == secondRoomId);
            MoveAlongPath(graph, first);

            Camera camera = graph.RoomCamera.RoomCamera;
            Assert(camera != null && camera.clearFlags == CameraClearFlags.SolidColor &&
                   Mathf.Approximately(camera.backgroundColor.a, 1f) && camera.targetTexture == null,
                "The room camera must clear its viewport with an opaque color every frame instead of " +
                "preserving or compositing a prior room buffer.");

            RoomDoorway forward = first.Doorways.Single(doorway => doorway.Destination == second);
            PushCameraToHorizontalEdge(graph, first, second);
            Assert(graph.TryTransition(first, second, forward.DestinationEntryPoint, graph.Player),
                "Large-to-small horizontal transition failed.");
            AssertTransitionFrame(graph, second, forward.DestinationEntryPoint);

            RoomDoorway reverse = second.Doorways.Single(doorway => doorway.Destination == first);
            PushCameraToHorizontalEdge(graph, second, first);
            Assert(graph.TryTransition(second, first, reverse.DestinationEntryPoint, graph.Player),
                "Small-to-large horizontal transition failed.");
            AssertTransitionFrame(graph, first, reverse.DestinationEntryPoint);
        }

        private static void ValidateRendererVisibilityBoundary(FloorGenerator generator)
        {
            foreach (RoomTemplateDefinition template in generator.RoomTemplates)
            {
                RoomPrefab room = template.RoomPrefabAsset.GetComponent<RoomPrefab>();
                Assert(room != null && room.Node != null && room.Node.ContentRoot != null,
                    $"Template '{template.TemplateId}' is missing its room visibility root.");
                Renderer[] renderers = template.RoomPrefabAsset.GetComponentsInChildren<Renderer>(true);
                Assert(renderers.All(renderer =>
                        renderer.transform == room.Node.ContentRoot.transform ||
                        renderer.transform.IsChildOf(room.Node.ContentRoot.transform)),
                    $"Template '{template.TemplateId}' has a Renderer outside RoomNode.ContentRoot, so the " +
                    "previous room could remain visible after a transition.");
            }
        }

        private static void ValidateWaveRevisitAndRewardAcrossFloorReload(RoomGraphAssembler assembler)
        {
            int seed = FindTwoWaveSeed(assembler.Generator);
            Assert(seed > 0, "Could not find a three-floor Run containing the two-wave Encounter.");
            PrepareRun(assembler, seed);
            GeneratedRoomNode generated = assembler.GeneratedGraph.Nodes.First(node =>
                node.Encounter != null && node.ResolvedEncounterWaves.Count == 2);
            Assert(assembler.TryLoadFloor(generated.FloorNumber, assembler.Graph.Player, out string error), error);

            RoomPrefab room = assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(candidate => candidate.Node.RoomId == generated.RoomId);
            RoomRunState state = assembler.Progress.GetRoomState(generated.RoomId);
            Assert(state != null && state.TryMarkWaveCompleted(1),
                "Room-8 could not persist the first completed wave.");
            Health playerHealth = assembler.Graph.Player.GetComponent<Health>();
            List<GameObject> spawned = new();
            room.Controller.EnemySpawned += spawned.Add;
            room.Controller.BeginCombat(playerHealth);
            Assert(room.Controller.CurrentWaveNumber == 2 && spawned.Count == generated.ResolvedEncounterWaves[1].Length &&
                   state.CompletedWaveCount == 1 && !state.IsCleared,
                "Revisiting a partial room did not resume at its first incomplete wave.");
            foreach (GameObject enemy in spawned.ToArray()) Kill(enemy);
            Assert(state.CompletedWaveCount == 2 && state.IsCleared && state.HasGrantedClearReward,
                "The final required-enemy wipe did not persist clear and reward state.");
            RoomClearRewardSpawner clearDrop = room.Controller.GetComponent<RoomClearRewardSpawner>();
            // Chest-1: the clear reward is a seeded chest when the assembler has a chest content table.
            Assert(clearDrop != null && clearDrop.HasRolled &&
                   (clearDrop.LastSpawnedReward != null) == (clearDrop.ChestTable != null
                       ? clearDrop.ChestTable.TryRollChest(clearDrop.ChestSeed, out _)
                       : clearDrop.DropTable.TryRoll(clearDrop.DropSeed, out _)),
                "The completed Encounter did not roll its one seeded clear reward.");

            int otherFloor = generated.FloorNumber == 1 ? 2 : 1;
            Assert(assembler.TryLoadFloor(otherFloor, assembler.Graph.Player, out error), error);
            Assert(assembler.TryLoadFloor(generated.FloorNumber, assembler.Graph.Player, out error), error);
            RoomPrefab revisited = assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(candidate => candidate.Node.RoomId == generated.RoomId);
            int revisitSpawnCount = 0;
            revisited.Controller.EnemySpawned += _ => revisitSpawnCount++;
            revisited.Controller.BeginCombat(playerHealth);
            RoomClearRewardSpawner reward = revisited.Controller.GetComponent<RoomClearRewardSpawner>();
            Assert(revisited.Controller.State == RoomState.Cleared && revisitSpawnCount == 0 &&
                   reward != null && !reward.TrySpawn() && reward.LastSpawnedReward == null &&
                   state.CompletedWaveCount == 2 && state.HasGrantedClearReward,
                "Floor reload duplicated cleared-room enemies, wave progress, or the clear reward.");
        }

        private static void PrepareRun(RoomGraphAssembler assembler, int seed)
        {
            assembler.Progress.ResetProgress();
            Assert(assembler.Progress.TryInitializeRunSeed(seed, out string error), error);
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out error), error);
            Assert(ReferenceEquals(assembler.Progress.GeneratedGraph, assembler.GeneratedGraph),
                "RunProgress did not retain the generated three-floor graph.");
        }

        private static bool TryFindHorizontalMixedSizePair(FloorGenerator generator, out int seed,
            out int floorNumber, out string firstRoomId, out string secondRoomId)
        {
            for (int candidateSeed = 1; candidateSeed <= 4096; candidateSeed++)
            {
                if (!generator.TryGenerateForSeed(candidateSeed, out GeneratedFloorGraph graph, out _)) continue;
                foreach (GeneratedRoomNode node in graph.Nodes)
                foreach (GeneratedRoomConnection connection in node.DirectionalConnections.Where(connection =>
                             !connection.IsSecret &&
                             (connection.Direction == RoomDoorDirection.Left ||
                              connection.Direction == RoomDoorDirection.Right)))
                {
                    GeneratedRoomNode other = graph.Nodes.First(candidate =>
                        candidate.RoomId == connection.DestinationRoomId);
                    float firstWidth = node.Template.Profile.InteriorSize.x;
                    float secondWidth = other.Template.Profile.InteriorSize.x;
                    if (!((firstWidth >= 24f && secondWidth <= 16f) ||
                          (secondWidth >= 24f && firstWidth <= 16f))) continue;
                    seed = candidateSeed;
                    floorNumber = node.FloorNumber;
                    firstRoomId = firstWidth >= secondWidth ? node.RoomId : other.RoomId;
                    secondRoomId = firstWidth >= secondWidth ? other.RoomId : node.RoomId;
                    return true;
                }
            }
            seed = floorNumber = -1;
            firstRoomId = secondRoomId = null;
            return false;
        }

        private static int FindTwoWaveSeed(FloorGenerator generator)
        {
            for (int seed = 1; seed <= 4096; seed++)
                if (generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _) &&
                    graph.Nodes.Any(node => node.Encounter != null && node.ResolvedEncounterWaves.Count == 2)) return seed;
            return -1;
        }

        private static void MoveAlongPath(RoomGraphController graph, RoomNode destination)
        {
            Dictionary<RoomNode, RoomDoorway> previous = new();
            Queue<RoomNode> queue = new();
            queue.Enqueue(graph.CurrentNode);
            previous[graph.CurrentNode] = null;
            while (queue.Count > 0 && !previous.ContainsKey(destination))
            {
                RoomNode current = queue.Dequeue();
                foreach (RoomDoorway doorway in current.Doorways)
                {
                    if (doorway?.Destination == null || previous.ContainsKey(doorway.Destination)) continue;
                    previous[doorway.Destination] = doorway;
                    queue.Enqueue(doorway.Destination);
                }
            }
            Assert(previous.ContainsKey(destination), $"No runtime path reaches {destination.RoomId}.");
            List<RoomDoorway> path = new();
            for (RoomNode cursor = destination; cursor != graph.CurrentNode;)
            {
                RoomDoorway doorway = previous[cursor];
                path.Add(doorway);
                cursor = doorway.Source;
            }
            path.Reverse();
            foreach (RoomDoorway doorway in path)
                Assert(graph.TryTransition(doorway.Source, doorway.Destination,
                           doorway.DestinationEntryPoint, graph.Player),
                    $"Path transition {doorway.Source.RoomId} -> {doorway.Destination.RoomId} failed.");
        }

        private static void PushCameraToHorizontalEdge(RoomGraphController graph, RoomNode source,
            RoomNode destination)
        {
            float direction = destination.transform.position.x >= source.transform.position.x ? 1f : -1f;
            Rigidbody2D body = graph.Player.GetComponent<Rigidbody2D>();
            Vector2 edge = source.CameraAnchor.TransformPoint(new Vector2(direction * 100f, 0f));
            if (body != null) body.position = edge;
            else graph.Player.transform.position = edge;
            graph.RoomCamera.SnapToTarget();
            Vector2 local = source.CameraAnchor.InverseTransformPoint(graph.RoomCamera.transform.position);
            Rect bounds = graph.RoomCamera.ActiveCenterBounds;
            float expected = direction > 0f ? bounds.xMax : bounds.xMin;
            Assert(Mathf.Abs(local.x - expected) <= PositionTolerance,
                "The source camera did not reach the requested horizontal tracking edge.");
        }

        private static void AssertTransitionFrame(RoomGraphController graph, RoomNode destination,
            Transform expectedEntry)
        {
            Assert(graph.CurrentNode == destination && graph.Nodes.Count(node => node.IsVisible) == 1 &&
                   destination.IsVisible,
                "The transition left a previous room visible beside the destination.");
            Rigidbody2D body = graph.Player.GetComponent<Rigidbody2D>();
            Vector2 playerPosition = body != null ? body.position : (Vector2)graph.Player.transform.position;
            Assert((playerPosition - (Vector2)expectedEntry.position).sqrMagnitude <=
                   PositionTolerance * PositionTolerance,
                "The transition did not use the destination safe entry point.");
            Vector2 local = destination.CameraAnchor.InverseTransformPoint(graph.RoomCamera.transform.position);
            Rect bounds = graph.RoomCamera.ActiveCenterBounds;
            Assert(local.x >= bounds.xMin - PositionTolerance && local.x <= bounds.xMax + PositionTolerance &&
                   local.y >= bounds.yMin - PositionTolerance && local.y <= bounds.yMax + PositionTolerance,
                "The first destination frame retained a camera center outside the new Room Profile.");
            Vector2 desiredLocal = destination.CameraAnchor.InverseTransformPoint(
                graph.Player.transform.position);
            Vector2 clampedLocal = new(Mathf.Clamp(desiredLocal.x, bounds.xMin, bounds.xMax),
                Mathf.Clamp(desiredLocal.y, bounds.yMin, bounds.yMax));
            Vector3 expectedWorld = destination.CameraAnchor.TransformPoint(clampedLocal);
            Assert(((Vector2)graph.RoomCamera.transform.position - (Vector2)expectedWorld).sqrMagnitude <=
                   PositionTolerance * PositionTolerance,
                "The camera did not snap to the destination frame before interpolation.");
        }

        private static void Kill(GameObject enemy)
        {
            Health health = enemy != null ? enemy.GetComponent<Health>() : null;
            Assert(health != null && !health.IsDead, "A required Encounter enemy was missing or already dead.");
            health.TakeDamage(health.MaxHealth + 1f);
            Assert(health.IsDead, "A required Encounter enemy did not die.");
        }

        private static string Signature(GeneratedFloorGraph graph)
        {
            StringBuilder signature = new();
            foreach (GeneratedRoomNode node in graph.Nodes.OrderBy(node => node.RoomId, StringComparer.Ordinal))
            {
                signature.Append(node.RoomId).Append(':').Append(node.TemplateId).Append(':')
                    .Append(node.EncounterId ?? "-").Append(':');
                foreach (GeneratedRoomConnection connection in node.DirectionalConnections
                             .OrderBy(connection => connection.Direction))
                    signature.Append(connection.Direction).Append('>').Append(connection.DestinationRoomId).Append(',');
                signature.Append('|');
            }
            return signature.ToString();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
