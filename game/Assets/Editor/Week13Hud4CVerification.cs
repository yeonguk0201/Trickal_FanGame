using System;
using System.Linq;
using System.Reflection;
using TMPro;
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
    public static class Week13Hud4CVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-4C Floor Name")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            VerifyRuntimeState(scene);
            Debug.Log("Week 13 HUD-4C verification passed: floor-only names, one alert per floor, 1.5-second unscaled duration, and future floor-name overrides.");
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
            Week13Hud4CSetup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud4CSetup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-4C setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-4C setup changed the Game Scene GUID.");
            Verify();
            Week13Hud4BVerification.ValidateScene(EditorSceneManager.GetActiveScene());
            Week13Flow4Verification.Verify();
            Debug.Log("Week 13 HUD-4C batch verification passed: setup twice, stable Scene GUID and HUD-4B/Flow-4 regression.");
        }

        public static void ValidateScene(Scene scene)
        {
            GameFloorNameView[] views = FindAll<GameFloorNameView>(scene);
            Assert(views.Length == 1, "Game Scene requires exactly one floor name view.");
            GameFloorNameView view = views[0];
            Assert(view.Progress == FindAll<RunProgress>(scene).Single(),
                "HUD-4C must observe the actual RunProgress.");
            Assert(view.HeaderText != null && view.AnnouncementCanvasGroup != null && view.AnnouncementText != null,
                "HUD-4C visual references are missing.");
            Assert(view.RegionName == Week13Hud4CSetup.RegionName &&
                view.ResolveFloorName(1) == "요정의 숲 · 1층" && view.ResolveFloorName(3) == "요정의 숲 · 3층",
                "HUD-4C must use the current numeric floor name without a room identifier.");
            Assert(Mathf.Approximately(view.DisplayDuration, 1.5f),
                "Floor entry notification must last 1.5 seconds.");
            RectTransform panel = view.GetComponent<RectTransform>();
            Assert(panel.sizeDelta == new Vector2(720, 104) && panel.anchoredPosition == new Vector2(0, 180),
                "Floor entry notification must appear slightly above the screen center.");
            Assert(view.gameObject.activeSelf && view.enabled &&
                Mathf.Approximately(view.AnnouncementCanvasGroup.alpha, 0f),
                "HUD-4C must stay active while remaining hidden between floor entries.");
            Assert(!view.AnnouncementCanvasGroup.interactable && !view.AnnouncementCanvasGroup.blocksRaycasts &&
                !view.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget),
                "HUD-4C must not intercept combat input.");
            Week13Hud4BVerification.ValidateScene(scene);
        }

        private static void VerifyRuntimeState(Scene scene)
        {
            GameObject progressObject = new("HUD-4C Verification Progress", typeof(RunProgress));
            GameObject canvasObject = new("HUD-4C Verification Canvas", typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameFloorNameView sceneView = FindAll<GameFloorNameView>(scene).Single();
            GameObject viewClone = Object.Instantiate(sceneView.gameObject);
            viewClone.transform.SetParent(canvasObject.transform, false);
            TMP_Text headerClone = Object.Instantiate(sceneView.HeaderText, canvasObject.transform);
            float originalTimeScale = Time.timeScale;
            try
            {
                RunProgress progress = progressObject.GetComponent<RunProgress>();
                GameFloorNameView view = viewClone.GetComponent<GameFloorNameView>();
                view.Configure(progress, headerClone, view.AnnouncementCanvasGroup, view.AnnouncementText,
                    Week13Hud4CSetup.RegionName, Array.Empty<string>(), 1.5f);
                Assert(view.HeaderText.text == "요정의 숲 · 1층" && !view.IsShowing,
                    "HUD-4C must show the initial floor header while beginning with its alert hidden.");

                progress.RecordRoomEntry(1, 1);
                Assert(view.IsShowing && view.LastAnnouncedFloor == 1 &&
                    view.HeaderText.text == "요정의 숲 · 1층" && view.AnnouncementText.text == "요정의 숲 · 1층" &&
                    Mathf.Approximately(view.RemainingSeconds, 1.5f),
                    "Entering the first floor must show its floor-only name immediately.");
                AssertAnnouncementMesh(view.AnnouncementText, "요정의 숲 · 1층");
                Advance(view, 0.7f);
                float roomEntryRemaining = view.RemainingSeconds;
                progress.RecordRoomEntry(1, 6);
                Assert(view.IsShowing && Mathf.Approximately(view.RemainingSeconds, roomEntryRemaining) &&
                    !view.AnnouncementText.text.Contains("6"),
                    "Entering another room on the same floor must not restart or add a room identifier to the alert.");

                progress.RecordRoomEntry(2, 1);
                Assert(view.IsShowing && view.LastAnnouncedFloor == 2 &&
                    view.HeaderText.text == "요정의 숲 · 2층" && view.AnnouncementText.text == "요정의 숲 · 2층" &&
                    Mathf.Approximately(view.RemainingSeconds, 1.5f),
                    "Entering a different floor must replace the current floor name and restart the alert duration.");
                AssertAnnouncementMesh(view.AnnouncementText, "요정의 숲 · 2층");
                Advance(view, 1.49f);
                Assert(view.IsShowing, "The floor entry notification disappeared before 1.5 unscaled seconds.");
                Advance(view, 0.02f);
                Assert(!view.IsShowing && Mathf.Approximately(view.AnnouncementCanvasGroup.alpha, 0f),
                    "The floor entry notification must hide after 1.5 unscaled seconds.");
                Assert(Mathf.Approximately(Time.timeScale, originalTimeScale),
                    "HUD-4C must not alter combat time.");

                view.Configure(progress, view.HeaderText, view.AnnouncementCanvasGroup, view.AnnouncementText,
                    Week13Hud4CSetup.RegionName, new[] { "숲의 초입" }, 1.5f);
                Assert(view.ResolveFloorName(1) == "요정의 숲 · 숲의 초입" &&
                    view.ResolveFloorName(2) == "요정의 숲 · 2층",
                    "A future per-floor name must override only its configured numeric fallback.");
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(progressObject);
            }
        }

        private static void Advance(GameFloorNameView view, float seconds)
        {
            MethodInfo method = typeof(GameFloorNameView).GetMethod("Advance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, "HUD-4C deterministic timing hook is missing.");
            method.Invoke(view, new object[] { seconds });
        }

        private static void AssertAnnouncementMesh(TMP_Text message, string expectedText)
        {
            Canvas.ForceUpdateCanvases();
            message.ForceMeshUpdate(true, true);
            Assert(message.GetParsedText() == expectedText && message.textInfo.characterCount == expectedText.Length,
                "HUD-4C TMP mesh must contain the complete visible floor name.");
            Assert(message.textInfo.lineCount == 1 && message.preferredHeight <= message.rectTransform.rect.height,
                "HUD-4C floor name must fit inside one uncropped TMP line.");
        }

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
