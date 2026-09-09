using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud4BVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-4B Explored Minimap")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            VerifySafeRoomPreClear();
            VerifyExplorationState(scene);
            Debug.Log("Week 13 HUD-4B verification passed: cumulative exploration, special doorway icons, and no pre-entry treasure clear badge.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Hud2Setup.Setup();
            Week13Hud3ASetup.Setup();
            Week13Hud3BSetup.Setup();
            Week13Hud3CSetup.Setup();
            Week13Hud4ASetup.Setup();
            Week13Hud4BSetup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud4BSetup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-4B setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-4B setup changed the Game Scene GUID.");
            Verify();
            Week13Flow4Verification.Verify();
            Debug.Log("Week 13 HUD-4B batch verification passed: setup twice, stable Scene GUID and HUD-4A/HUD-3C/HUD-7A/Flow-4 regression.");
        }

        public static void ValidateScene(Scene scene)
        {
            GameMinimapView view = FindAll<GameMinimapView>(scene).Single();
            Assert(view.MarkerTemplate.ClearedBadgeText != null,
                "HUD-4B minimap template requires a non-color cleared badge.");
            Assert(view.MarkerTemplate.ClearedBadgeText.text == "V" &&
                !view.MarkerTemplate.ClearedBadgeText.gameObject.activeSelf,
                "HUD-4B cleared badge must use an inactive V template.");
            Assert(!view.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget),
                "HUD-4B graphics must not intercept combat input.");
            Week13Hud4AVerification.ValidateScene(scene);
        }

        private static void VerifySafeRoomPreClear()
        {
            GameObject roomObject = new("HUD-4B Pre-Cleared Treasure Room", typeof(BoxCollider2D),
                typeof(RoomController));
            try
            {
                RoomRunState state = new("hud-4b-pre-cleared-treasure");
                RoomController controller = roomObject.GetComponent<RoomController>();
                controller.BindRunState(state, true);
                Assert(controller.State == RoomState.Cleared && state.IsCleared && !state.HasVisited,
                    "A safe room must start open and cleared without being recorded as entered.");
                state.MarkVisited();
                Assert(state.HasVisited && state.IsCleared,
                    "Actual entry must preserve a safe room's pre-cleared state and add visitation.");
            }
            finally
            {
                Object.DestroyImmediate(roomObject);
            }
        }

        private static void VerifyExplorationState(Scene scene)
        {
            FloorGenerator generator = FindAll<FloorGenerator>(scene).Single();
            Assert(generator.TryGenerateForSeed(20260910, out GeneratedFloorGraph graph, out string error),
                "HUD-4B test graph generation failed: " + error);
            GeneratedFloor floor = graph.FindFloor(1);
            GeneratedRoomNode start = FindRoom(floor, floor.StartingRoomId);
            GeneratedRoomNode special = floor.Nodes.First(node => node.Role == GeneratedRoomRole.Treasure);
            List<GeneratedRoomNode> path = FindPath(floor, start, special);
            Assert(path.Count >= 2, "HUD-4B test graph needs a path from the start to a special room.");

            GameObject progressObject = new("HUD-4B Verification Progress", typeof(RunProgress));
            GameObject minimapClone = Object.Instantiate(FindAll<GameMinimapView>(scene).Single().gameObject);
            try
            {
                RunProgress progress = progressObject.GetComponent<RunProgress>();
                Assert(progress.TrySetGeneratedGraph(graph, out error),
                    "HUD-4B test progress rejected the generated graph: " + error);
                RoomRunState specialState = progress.GetRoomState(special.RoomId);
                int preClearChanges = 0;
                void CountPreClearChange() => preClearChanges++;
                specialState.Changed += CountPreClearChange;
                specialState.MarkPreCleared();
                specialState.MarkPreCleared();
                specialState.Changed -= CountPreClearChange;
                Assert(preClearChanges == 1 && specialState.IsCleared && !specialState.HasVisited,
                    "A safe treasure room must be pre-cleared idempotently without being marked visited.");
                GameMinimapView view = minimapClone.GetComponent<GameMinimapView>();
                view.Configure(progress, view.MapRoot, view.MarkerTemplate, view.ConnectionTemplate, 48f);

                progress.RecordRoomEntry(start.FloorNumber, start.RoomNumber);
                AssertExplorationMatches(view, progress, floor, start);
                int previousVisibleCount = view.VisibleMarkers.Count;

                for (int index = 1; index < path.Count; index++)
                {
                    GeneratedRoomNode destination = path[index];
                    MinimapRoomMarkerView unknown = view.VisibleMarkers.Single(marker => marker.RoomId == destination.RoomId);
                    string expectedSymbol = destination.Role == GeneratedRoomRole.Treasure ? "T"
                        : destination.Role == GeneratedRoomRole.Boss ? "B" : "?";
                    bool expectsSpecialDoor = destination.Role is GeneratedRoomRole.Treasure or GeneratedRoomRole.Boss;
                    Assert(!unknown.HasVisited && unknown.SymbolText.text == expectedSymbol &&
                        unknown.IsSpecialRoomRevealed == expectsSpecialDoor &&
                        !unknown.ClearedBadgeText.gameObject.activeSelf,
                        "An adjacent unvisited room must reveal a special doorway type but never a clear badge.");

                    progress.RecordRoomEntry(destination.FloorNumber, destination.RoomNumber);
                    AssertExplorationMatches(view, progress, floor, destination);
                    Assert(view.VisibleMarkers.Count >= previousVisibleCount,
                        "Exploring a room must not remove previously revealed map rooms.");
                    previousVisibleCount = view.VisibleMarkers.Count;
                }

                MinimapRoomMarkerView currentSpecial = view.VisibleMarkers.Single(marker => marker.RoomId == special.RoomId);
                Assert(currentSpecial.IsCurrent && currentSpecial.IsCleared &&
                    currentSpecial.ClearedBadgeText.gameObject.activeSelf && currentSpecial.ClearedBadgeText.text == "V",
                    "Entering a pre-cleared treasure room must reveal its clear badge at that point.");

                GeneratedRoomNode previous = path[^2];
                progress.RecordRoomEntry(previous.FloorNumber, previous.RoomNumber);
                MinimapRoomMarkerView revealedSpecial = view.VisibleMarkers.Single(marker => marker.RoomId == special.RoomId);
                Assert(revealedSpecial.HasVisited && revealedSpecial.IsSpecialRoomRevealed &&
                    revealedSpecial.SymbolText.text == "T" && revealedSpecial.ClearedBadgeText.gameObject.activeSelf,
                    "A visited treasure room must retain its special icon and cleared badge after leaving it.");
                AssertExplorationMatches(view, progress, floor, previous);
            }
            finally
            {
                Object.DestroyImmediate(minimapClone);
                Object.DestroyImmediate(progressObject);
            }
        }

        private static void AssertExplorationMatches(GameMinimapView view, RunProgress progress,
            GeneratedFloor floor, GeneratedRoomNode current)
        {
            HashSet<string> visited = floor.Nodes
                .Where(node => progress.GetRoomState(node.RoomId)?.HasVisited == true)
                .Select(node => node.RoomId).ToHashSet(StringComparer.Ordinal);
            HashSet<string> expectedVisible = new(visited, StringComparer.Ordinal);
            foreach (GeneratedRoomNode node in floor.Nodes)
                if (visited.Contains(node.RoomId))
                    foreach (GeneratedRoomConnection connection in node.DirectionalConnections)
                        expectedVisible.Add(connection.DestinationRoomId);

            Assert(view.VisibleMarkers.Select(marker => marker.RoomId).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(expectedVisible),
                "HUD-4B must show all visited rooms and only their directly connected frontier rooms.");
            Assert(view.VisibleMarkers.Count(marker => marker.IsCurrent) == 1 &&
                view.VisibleMarkers.Single(marker => marker.IsCurrent).RoomId == current.RoomId,
                "HUD-4B must move exactly one current marker through the accumulated graph.");

            int expectedConnections = 0;
            HashSet<string> edges = new(StringComparer.Ordinal);
            foreach (GeneratedRoomNode node in floor.Nodes)
            {
                if (!visited.Contains(node.RoomId)) continue;
                foreach (GeneratedRoomConnection connection in node.DirectionalConnections)
                {
                    string key = string.CompareOrdinal(node.RoomId, connection.DestinationRoomId) < 0
                        ? node.RoomId + "|" + connection.DestinationRoomId
                        : connection.DestinationRoomId + "|" + node.RoomId;
                    if (edges.Add(key)) expectedConnections++;
                }
            }
            Assert(view.VisibleConnectionCount == expectedConnections,
                "HUD-4B connection lines must match every explored doorway without duplicates.");

            foreach (MinimapRoomMarkerView marker in view.VisibleMarkers)
            {
                GeneratedRoomNode node = FindRoom(floor, marker.RoomId);
                if (!marker.HasVisited)
                {
                    string expectedSymbol = node.Role == GeneratedRoomRole.Treasure ? "T"
                        : node.Role == GeneratedRoomRole.Boss ? "B" : "?";
                    bool expectsSpecialDoor = node.Role is GeneratedRoomRole.Treasure or GeneratedRoomRole.Boss;
                    Assert(marker.SymbolText.text == expectedSymbol &&
                        marker.IsSpecialRoomRevealed == expectsSpecialDoor &&
                        !marker.ClearedBadgeText.gameObject.activeSelf,
                        "Unvisited frontier rooms may reveal special doors but must never show a clear badge.");
                }
                foreach (MinimapRoomMarkerView other in view.VisibleMarkers)
                {
                    GeneratedRoomNode otherNode = FindRoom(floor, other.RoomId);
                    Vector2 expectedDelta = new(
                        (otherNode.GridPosition.X - node.GridPosition.X) * view.EffectiveSpacing,
                        (otherNode.GridPosition.Y - node.GridPosition.Y) * view.EffectiveSpacing);
                    Vector2 actualDelta = other.GetComponent<RectTransform>().anchoredPosition -
                                          marker.GetComponent<RectTransform>().anchoredPosition;
                    Assert((actualDelta - expectedDelta).sqrMagnitude < 0.001f,
                        "HUD-4B accumulated graph changed a generated room's relative grid position.");
                }

                RectTransform rect = marker.GetComponent<RectTransform>();
                Vector2 halfSize = rect.sizeDelta * rect.localScale.x * 0.5f;
                Assert(Mathf.Abs(rect.anchoredPosition.x) + halfSize.x <= view.MapRoot.rect.width * 0.5f + 0.01f &&
                    Mathf.Abs(rect.anchoredPosition.y) + halfSize.y <= view.MapRoot.rect.height * 0.5f + 0.01f,
                    "HUD-4B explored graph must fit inside the minimap viewport.");
            }
        }

        private static List<GeneratedRoomNode> FindPath(GeneratedFloor floor, GeneratedRoomNode start,
            GeneratedRoomNode destination)
        {
            Queue<GeneratedRoomNode> queue = new();
            Dictionary<string, string> previous = new(StringComparer.Ordinal) { [start.RoomId] = null };
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                GeneratedRoomNode current = queue.Dequeue();
                if (current.RoomId == destination.RoomId) break;
                foreach (GeneratedRoomConnection connection in current.DirectionalConnections)
                {
                    if (previous.ContainsKey(connection.DestinationRoomId)) continue;
                    previous.Add(connection.DestinationRoomId, current.RoomId);
                    queue.Enqueue(FindRoom(floor, connection.DestinationRoomId));
                }
            }

            List<GeneratedRoomNode> path = new();
            for (string roomId = destination.RoomId; roomId != null; roomId = previous[roomId])
                path.Add(FindRoom(floor, roomId));
            path.Reverse();
            return path;
        }

        private static GeneratedRoomNode FindRoom(GeneratedFloor floor, string roomId) =>
            floor.Nodes.First(node => node.RoomId == roomId);

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static int CountTransforms(Scene scene) => scene.GetRootGameObjects()
            .Sum(root => root.GetComponentsInChildren<Transform>(true).Length);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
