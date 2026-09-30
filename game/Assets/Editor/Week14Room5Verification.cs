using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Room5Verification
    {
        [MenuItem("Trickal Fan Game/Week 14/Verify Room-5 Runtime Room Transitions")]
        public static void Verify()
        {
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            GameMinimapView minimap = UnityEngine.Object.FindFirstObjectByType<GameMinimapView>();
            Assert(assembler != null && assembler.Generator != null && assembler.Graph != null,
                "Run Room-5 Setup before verification.");
            Assert(minimap != null, "Room-5 requires the configured HUD minimap.");

            int seed = FindSeedWithDifferentStartingNeighbor(assembler.Generator);
            Assert(seed > 0, "Could not find a verification seed with different adjacent room profiles.");
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            ValidateInstantiatedProfiles(assembler);

            RoomGraphController graph = assembler.Graph;
            SerializedObject serializedGraph = new(graph);
            serializedGraph.FindProperty("transitionCooldown").floatValue = 0f;
            serializedGraph.ApplyModifiedPropertiesWithoutUndo();
            RoomNode source = graph.CurrentNode;
            RoomDoorway forward = source.Doorways.First(doorway =>
                doorway != null && doorway.Destination != null && doorway.Destination.Profile != source.Profile);
            RoomNode destination = forward.Destination;
            PlayerMovement player = graph.Player;
            Assert(player != null, "Room-5 transition verification requires the configured player.");
            Assert(player.GetComponent<Rigidbody2D>()?.interpolation == RigidbodyInterpolation2D.Interpolate,
                "Room-5 requires player Rigidbody interpolation so render-frame camera tracking does not jitter.");

            Assert(graph.TryTransition(source, destination, forward.DestinationEntryPoint, player),
                "Forward transition between different Room Profiles failed.");
            AssertTransitionState(graph, assembler.Progress, minimap, destination,
                forward.DestinationEntryPoint, player);

            RoomDoorway backward = destination.Doorways.Single(doorway => doorway.Destination == source);
            Assert(graph.TryTransition(destination, source, backward.DestinationEntryPoint, player),
                "Reverse transition back to the starting room failed.");
            AssertTransitionState(graph, assembler.Progress, minimap, source,
                backward.DestinationEntryPoint, player);
            Assert(source.HasBeenVisited && destination.HasBeenVisited,
                "Bidirectional revisit must retain both rooms' visited state.");

            Week14Room4Verification.Verify();
            Debug.Log(
                "Week 14 Room-5 verification passed: selected Small/Basic/Wide Prefabs are instantiated with " +
                "their profiles; bidirectional transitions use opposite safe entries; only the current room is " +
                "visible; camera framing, RunProgress, and minimap silhouettes update on entry and revisit; " +
                "Room-0~4 regressions remain valid.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week14Room5Setup.Setup();
            Week14Room5Setup.Setup();
            Verify();
        }

        private static int FindSeedWithDifferentStartingNeighbor(FloorGenerator generator)
        {
            for (int seed = 1; seed <= 512; seed++)
            {
                if (!generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                GeneratedFloor floor = graph.FindFloor(1);
                GeneratedRoomNode start = floor?.Nodes.FirstOrDefault(node => node.RoomId == floor.StartingRoomId);
                if (start?.Template?.Profile == null) continue;
                if (floor.Nodes.Select(node => node.TemplateId).Distinct(StringComparer.Ordinal).Count() < 3)
                    continue;
                foreach (GeneratedRoomConnection connection in start.DirectionalConnections)
                {
                    GeneratedRoomNode neighbor = floor.Nodes.First(node => node.RoomId == connection.DestinationRoomId);
                    if (neighbor.Template?.Profile != null && neighbor.Template.Profile != start.Template.Profile)
                        return seed;
                }
            }
            return -1;
        }

        private static void ValidateInstantiatedProfiles(RoomGraphAssembler assembler)
        {
            Assert(assembler.CurrentFloorRoot != null, "Room-5 did not create a current floor root.");
            Dictionary<string, GeneratedRoomNode> generated = assembler.GeneratedGraph.FindFloor(1).Nodes
                .ToDictionary(node => node.RoomId, StringComparer.Ordinal);
            RoomPrefab[] instances = assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true);
            Assert(instances.Length == generated.Count,
                "Room-5 must instantiate exactly one Prefab for every generated room in the current floor.");
            foreach (RoomPrefab instance in instances)
            {
                GeneratedRoomNode node = generated[instance.Node.RoomId];
                Assert(instance.Node.Profile == node.Template.Profile,
                    $"Room {node.RoomId} did not receive selected profile '{node.Template.Profile.ProfileId}'.");
                BoxCollider2D encounter = instance.Controller.GetComponent<BoxCollider2D>();
                Assert(encounter != null && Approximately(encounter.size, node.Template.Profile.EncounterBounds.size),
                    $"Room {node.RoomId} did not instantiate the selected template's Encounter layout.");
            }
        }

        private static void AssertTransitionState(RoomGraphController graph, RunProgress progress,
            GameMinimapView minimap, RoomNode expectedRoom, Transform expectedEntry, PlayerMovement player)
        {
            Assert(graph.CurrentNode == expectedRoom,
                "RoomGraphController current room did not update during transition.");
            Assert(graph.Nodes.Count(node => node.IsVisible) == 1 && expectedRoom.IsVisible,
                "Exactly the destination room must remain visible after transition.");
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            Vector2 playerPosition = playerBody != null ? playerBody.position : (Vector2)player.transform.position;
            Assert(Approximately(playerPosition, expectedEntry.position),
                "Player was not placed at the destination's opposite safe entry point.");
            Assert(progress.CurrentFloor == expectedRoom.FloorNumber && progress.CurrentRoom == expectedRoom.RoomNumber,
                "RunProgress did not update to the entered room.");

            RoomCameraController camera = graph.RoomCamera;
            Assert(camera != null && camera.RoomCamera != null &&
                   Mathf.Approximately(camera.RoomCamera.orthographicSize,
                       expectedRoom.Profile.CameraOrthographicSize),
                "Camera did not apply the destination Room Profile.");
            Vector2 cameraLocal = expectedRoom.CameraAnchor.InverseTransformPoint(camera.transform.position);
            Rect bounds = camera.ActiveCenterBounds;
            const float tolerance = 0.001f;
            Assert(cameraLocal.x >= bounds.xMin - tolerance && cameraLocal.x <= bounds.xMax + tolerance &&
                   cameraLocal.y >= bounds.yMin - tolerance && cameraLocal.y <= bounds.yMax + tolerance,
                "Camera center is outside the destination profile's valid bounds.");

            minimap.RefreshNow();
            MinimapRoomMarkerView marker = minimap.VisibleMarkers.Single(candidate => candidate.IsCurrent);
            Vector2 expectedSize = new(
                30f * expectedRoom.Profile.InteriorSize.x / RoomLayout.Width,
                30f * expectedRoom.Profile.InteriorSize.y / RoomLayout.Height);
            Assert(marker.RoomId == expectedRoom.RoomId && Approximately(marker.SilhouetteSize, expectedSize),
                "Minimap current room or selected profile silhouette did not update.");
        }

        private static bool Approximately(Vector2 first, Vector2 second) =>
            (first - second).sqrMagnitude < 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
