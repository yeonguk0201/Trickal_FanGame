using System;
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
    public static class Week13Hud4AVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-4A Minimap Foundation")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            VerifyGeneratedSpatialState(scene);
            Debug.Log("Week 13 HUD-4A verification passed: current room and directional connections match the generated floor graph.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Hud2Setup.Setup();
            Week13Hud3ASetup.Setup();
            Week13Hud3BSetup.Setup();
            Week13Hud3CSetup.Setup();
            Week13Hud4ASetup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud4ASetup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-4A setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-4A setup changed the Game Scene GUID.");
            Verify();
            Week13Flow4Verification.Verify();
            Debug.Log("Week 13 HUD-4A batch verification passed: setup twice, stable Scene GUID and HUD-3C/HUD-7A/Flow-4 regression.");
        }

        public static void ValidateScene(Scene scene)
        {
            GameMinimapView[] views = FindAll<GameMinimapView>(scene);
            Assert(views.Length == 1, "Game Scene requires exactly one GameMinimapView.");
            GameMinimapView view = views[0];
            RunProgress progress = FindAll<RunProgress>(scene).Single();
            Assert(view.Progress == progress, "HUD-4A must reference the Run's actual progress state.");
            Assert(view.MapRoot != null && view.MarkerTemplate != null && view.ConnectionTemplate != null,
                "HUD-4A visual references are missing.");
            Assert(!view.MarkerTemplate.gameObject.activeSelf && !view.ConnectionTemplate.gameObject.activeSelf,
                "HUD-4A requires inactive room and connection templates.");
            Assert(view.MarkerTemplate.RoomFill != null && view.MarkerTemplate.CurrentOutline != null &&
                view.MarkerTemplate.SymbolText != null,
                "HUD-4A room template must have fill, current outline, and non-color current marker visuals.");
            RectTransform panel = view.GetComponent<RectTransform>();
            Assert(panel.sizeDelta == new Vector2(220, 180) && panel.anchoredPosition == new Vector2(786, 396),
                "Minimap HUD must occupy the specified top-right 220x180 safe region.");
            Assert(Mathf.Approximately(view.RoomSpacing, 48f),
                "HUD-4A room spacing must match its verified directional layout.");
            Assert(!view.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget),
                "HUD-4A graphics must not intercept combat input.");
            Assert(FindAll<MinimapRoomMarkerView>(scene).Length == 1,
                "The saved Game Scene must contain only the inactive minimap room template.");
            Week13Hud3CVerification.ValidateScene(scene);
        }

        private static void VerifyGeneratedSpatialState(Scene scene)
        {
            FloorGenerator generator = FindAll<FloorGenerator>(scene).Single();
            Assert(generator.TryGenerateForSeed(20260910, out GeneratedFloorGraph graph, out string error),
                "HUD-4A test graph generation failed: " + error);

            GameObject progressObject = new("HUD-4A Verification Progress", typeof(RunProgress));
            GameObject minimapClone = Object.Instantiate(FindAll<GameMinimapView>(scene).Single().gameObject);
            try
            {
                RunProgress progress = progressObject.GetComponent<RunProgress>();
                Assert(progress.TrySetGeneratedGraph(graph, out error),
                    "HUD-4A test progress rejected the generated graph: " + error);
                GeneratedFloor floor = graph.FindFloor(1);
                GeneratedRoomNode current = floor.Nodes.First(node => node.RoomId == floor.StartingRoomId);

                GameMinimapView view = minimapClone.GetComponent<GameMinimapView>();
                view.Configure(progress, view.MapRoot, view.MarkerTemplate, view.ConnectionTemplate, 48f);
                progress.RecordRoomEntry(current.FloorNumber, current.RoomNumber);
                AssertSpatialMatch(view, floor, current);

                GeneratedRoomNode destination = FindRoom(floor, current.DirectionalConnections[0].DestinationRoomId);
                progress.RecordRoomEntry(destination.FloorNumber, destination.RoomNumber);
                AssertSpatialMatch(view, floor, destination);
            }
            finally
            {
                Object.DestroyImmediate(minimapClone);
                Object.DestroyImmediate(progressObject);
            }
        }

        private static void AssertSpatialMatch(GameMinimapView view, GeneratedFloor floor,
            GeneratedRoomNode current)
        {
            Assert(view.CurrentRoomId == current.RoomId,
                "HUD-4A current room does not match RunProgress.");
            // Special-3 hidden passages stay off the map until opened.
            GeneratedRoomConnection[] knownConnections =
                current.DirectionalConnections.Where(connection => !connection.IsSecret).ToArray();
            Assert(view.VisibleMarkers.Count >= knownConnections.Length + 1 &&
                view.VisibleConnectionCount >= knownConnections.Length,
                "HUD-4A must retain the explored graph and include every direct connection of the current room.");
            MinimapRoomMarkerView currentMarker = view.VisibleMarkers.Single(marker => marker.IsCurrent);
            Assert(currentMarker.RoomId == current.RoomId &&
                currentMarker.CurrentOutline.gameObject.activeSelf && currentMarker.SymbolText.text == "P",
                $"HUD-4A must visibly mark the current room without relying only on color. " +
                $"Room={currentMarker.RoomId}, Position={currentMarker.GetComponent<RectTransform>().anchoredPosition}, " +
                $"Outline={currentMarker.CurrentOutline.gameObject.activeSelf}, Symbol='{currentMarker.SymbolText.text}'.");

            foreach (GeneratedRoomConnection connection in knownConnections)
            {
                GeneratedRoomNode destination = FindRoom(floor, connection.DestinationRoomId);
                MinimapRoomMarkerView marker = view.VisibleMarkers.Single(candidate => candidate.RoomId == destination.RoomId);
                RoomGridPosition delta = new(destination.GridPosition.X - current.GridPosition.X,
                    destination.GridPosition.Y - current.GridPosition.Y);
                Vector2 expectedDelta = new(delta.X * view.EffectiveSpacing, delta.Y * view.EffectiveSpacing);
                Vector2 actualDelta = marker.GetComponent<RectTransform>().anchoredPosition -
                                      currentMarker.GetComponent<RectTransform>().anchoredPosition;
                Assert(!marker.IsCurrent && actualDelta == expectedDelta &&
                    !marker.CurrentOutline.gameObject.activeSelf,
                    $"HUD-4A room {destination.RoomId} does not match generated grid position {destination.GridPosition}.");
            }
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
