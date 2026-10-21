using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week25Ux0Verification
    {
        [MenuItem("Trickal Fan Game/Week 25/Verify UX-0 In-game Settings and Game Over")]
        public static void Verify()
        {
            string[] frontendControls = FrontendSettingsControlNames();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null, "Frontend TMP font is missing.");
            GamePauseArtifactView view = Week25Ux0Setup.FindSingle<GamePauseArtifactView>(scene, "pause menu");
            RunSession session = Week25Ux0Setup.FindSingle<RunSession>(scene, "RunSession");
            GameOverSequence sequence = Week25Ux0Setup.FindSingle<GameOverSequence>(scene, "GAME OVER sequence");

            VerifyPauseSettingsLayout(view, frontendControls, font);
            VerifyGameOverLayout(scene, session, sequence, font);
            VerifyPauseSettingsFlow(view);
            VerifyGameOverTiming(sequence);

            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            Debug.Log("Week 25 UX-0 verification passed: pause settings share the Frontend panel and LocalSettings, " +
                      "focus stays in the open layer, and the GAME OVER sequence waits for the confirm input.");
        }

        public static void SetupAndVerifyBatch()
        {
            string gameGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week25Ux0Setup.Setup();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            int firstCount = CountUx0Objects(scene);
            Week25Ux0Setup.Setup();
            scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            Assert(CountUx0Objects(scene) == firstCount, "Running UX-0 setup twice duplicated objects.");
            Assert(gameGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "UX-0 setup changed the Game Scene GUID.");
            Verify();
        }

        // Pause menu regressions plus the Run result flow that the death sequence now gates.
        public static void VerifyWithRegressionsBatch()
        {
            SetupAndVerifyBatch();
            Week13Hud7CVerification.Verify();
            Week13Hud7DVerification.Verify();
            Week13Flow5Verification.Verify();
            Debug.Log("Week 25 UX-0 regression verification passed.");
        }

        private static void VerifyPauseSettingsLayout(GamePauseArtifactView view, string[] frontendControls,
            TMP_FontAsset font)
        {
            Assert(view.SettingsButton != null && view.SettingsRoot != null && view.SettingsView != null,
                "Pause menu settings references are missing.");
            Assert(view.SettingsButton.transform.IsChildOf(view.FocusScope),
                "The settings button must sit inside the pause menu focus scope.");
            Assert(view.SettingsRoot.transform.parent == view.transform && !view.SettingsRoot.activeSelf,
                "In-game settings must be a closed layer of the pause overlay.");
            Assert(view.SettingsView.transform.IsChildOf(view.SettingsRoot.transform),
                "The settings panel must live inside the in-game settings layer.");
            Image dim = view.SettingsRoot.GetComponent<Image>();
            RectTransform rootRect = (RectTransform)view.SettingsRoot.transform;
            Assert(dim != null && dim.raycastTarget && rootRect.sizeDelta == new Vector2(1920, 1080),
                "In-game settings must cover and block the pause menu behind it.");

            FrontendSettingsView settings = view.SettingsView;
            Assert(settings.MasterVolumeSlider != null && settings.BgmVolumeSlider != null &&
                   settings.SfxVolumeSlider != null && settings.ResolutionDropdown != null &&
                   settings.FullscreenToggle != null && settings.DetailedStatsHudToggle != null &&
                   settings.BackButton != null, "In-game settings controls are missing.");
            foreach (Selectable control in new Selectable[]
                     {
                         settings.MasterVolumeSlider, settings.BgmVolumeSlider, settings.SfxVolumeSlider,
                         settings.ResolutionDropdown, settings.FullscreenToggle, settings.DetailedStatsHudToggle,
                         settings.BackButton
                     })
                Assert(control.transform.IsChildOf(settings.transform),
                    "In-game settings control points outside its panel: " + control.name);
            string[] gameControls = ControlNames(settings.transform);
            Assert(frontendControls.SequenceEqual(gameControls),
                "In-game settings must match the Frontend settings panel. Frontend: " +
                string.Join(", ", frontendControls) + " / Game: " + string.Join(", ", gameControls));

            Button[] order = { view.ResumeButton, view.SettingsButton, view.RestartRunButton, view.LeaveRunButton };
            List<Rect> rects = new();
            for (int i = 0; i < order.Length; i++)
            {
                RectTransform rect = (RectTransform)order[i].transform;
                Assert(rect.anchoredPosition == new Vector2(Week25Ux0Setup.PauseButtonX[i], Week25Ux0Setup.PauseButtonY),
                    "Pause button order or position changed: " + order[i].name);
                Rect area = new(rect.anchoredPosition - rect.sizeDelta * 0.5f, rect.sizeDelta);
                Assert(rects.All(other => !other.Overlaps(area)), "Pause buttons overlap: " + order[i].name);
                Assert(Mathf.Abs(area.xMin) <= view.FocusScope.rect.width * 0.5f &&
                       Mathf.Abs(area.xMax) <= view.FocusScope.rect.width * 0.5f,
                    "Pause button leaves the pause panel: " + order[i].name);
                rects.Add(area);
                Navigation navigation = order[i].navigation;
                Assert(navigation.mode == Navigation.Mode.Explicit &&
                       navigation.selectOnRight == order[(i + 1) % order.Length] &&
                       navigation.selectOnLeft == order[(i + order.Length - 1) % order.Length],
                    "Pause button navigation is broken: " + order[i].name);
            }

            TMP_Text label = view.SettingsButton.GetComponentInChildren<TMP_Text>(true);
            Assert(label != null && label.text == "설정" && label.font == font, "Settings button label is wrong.");
            foreach (TMP_Text text in settings.GetComponentsInChildren<TMP_Text>(true))
                Assert(text.font == font, "In-game settings must use the Frontend TMP font: " + text.name);
        }

        private static void VerifyGameOverLayout(Scene scene, RunSession session, GameOverSequence sequence,
            TMP_FontAsset font)
        {
            Assert(session.GameOverSequence == sequence, "RunSession is not connected to the GAME OVER sequence.");
            Canvas canvas = sequence.GetComponent<Canvas>();
            Canvas hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Single(candidate => candidate.name == Week13Hud1Setup.HudRootName);
            Assert(canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                   canvas.sortingOrder > hud.sortingOrder, "GAME OVER must draw above the Game HUD.");
            Assert(sequence.Overlay != null && sequence.IrisHole != null && sequence.Title != null &&
                   sequence.ConfirmButton != null && sequence.GraphicRaycaster != null,
                "GAME OVER references are missing.");
            RectTransform overlay = (RectTransform)sequence.Overlay.transform;
            Assert(overlay.anchorMin == Vector2.zero && overlay.anchorMax == Vector2.one &&
                   overlay.offsetMin == Vector2.zero && overlay.offsetMax == Vector2.zero,
                "The circular fade must cover the whole screen, not only the reference frame.");
            Assert(sequence.Overlay.alpha == 0f && !sequence.Overlay.blocksRaycasts &&
                   !sequence.GraphicRaycaster.enabled && !sequence.ConfirmButton.gameObject.activeSelf,
                "GAME OVER must start hidden and non-interactive.");
            TMP_Text title = sequence.Title.GetComponentInChildren<TMP_Text>(true);
            TMP_Text confirm = sequence.ConfirmButton.GetComponentInChildren<TMP_Text>(true);
            Assert(title != null && title.text == "GAME OVER" && title.font == font,
                "GAME OVER title is missing or uses another font.");
            Assert(confirm != null && confirm.text == "결과 보기" && confirm.font == font,
                "GAME OVER confirm label is missing or uses another font.");
            Assert(sequence.HoldAfterFall >= 4f && sequence.HoldAfterFall <= 6f,
                "The pause after the fall should stay about five seconds.");
        }

        private static void VerifyPauseSettingsFlow(GamePauseArtifactView view)
        {
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                Assert(view.TryPause() && view.IsPaused && Time.timeScale == 0f, "Pause did not stop time.");
                Assert(view.OpenSettings() && view.IsSettingsOpen && Time.timeScale == 0f,
                    "Opening settings must keep the game paused.");
                Assert(!view.ShowLeaveConfirmation() && !view.ShowRestartConfirmation(),
                    "Run actions must stay closed while settings are open.");
                Assert(view.CloseSettings() && !view.IsSettingsOpen && view.IsPaused && Time.timeScale == 0f,
                    "Closing settings must return to the paused menu.");
                Assert(view.OpenSettings() && view.Resume() && !view.IsSettingsOpen && Time.timeScale == 1f,
                    "Resuming must close settings and restore time.");
            }
            finally
            {
                if (view.IsPaused) view.Resume();
                Time.timeScale = previousTimeScale;
            }
        }

        private static void VerifyGameOverTiming(GameOverSequence sequence)
        {
            int confirmations = 0;
            Assert(sequence.TryBegin(null, () => confirmations++), "GAME OVER did not start.");
            Assert(!sequence.TryBegin(null, () => confirmations++), "GAME OVER started twice.");
            Assert(sequence.CurrentPhase == GameOverSequence.Phase.Falling, "GAME OVER must start with the fall.");
            sequence.Advance(sequence.FallDuration + 0.01f);
            Assert(sequence.CurrentPhase == GameOverSequence.Phase.Holding, "The hold must follow the fall.");
            sequence.Advance(sequence.HoldAfterFall - 0.1f);
            Assert(sequence.CurrentPhase == GameOverSequence.Phase.Holding && sequence.Overlay.alpha == 0f,
                "The circular fade started before the hold ended.");
            sequence.Advance(0.2f);
            Assert(sequence.CurrentPhase == GameOverSequence.Phase.Closing && sequence.Overlay.alpha == 1f,
                "The circular fade did not start after the hold.");

            float previousRadius = sequence.IrisRadius;
            Assert(previousRadius > 0f, "The circular fade must start open.");
            for (int step = 0; step < 20 && sequence.CurrentPhase == GameOverSequence.Phase.Closing; step++)
            {
                sequence.Advance(sequence.IrisDuration / 20f + 0.0001f);
                Assert(sequence.IrisRadius <= previousRadius, "The circular fade reopened while closing.");
                previousRadius = sequence.IrisRadius;
            }
            Assert(sequence.CurrentPhase == GameOverSequence.Phase.Title && sequence.IrisRadius <= 0.01f,
                "The circular fade did not close fully before GAME OVER.");
            Assert(!sequence.ConfirmButton.gameObject.activeSelf, "The confirm input appeared before GAME OVER.");

            sequence.Advance(5f);
            Assert(sequence.IsAwaitingConfirm && sequence.Title.alpha == 1f &&
                   sequence.ConfirmButton.gameObject.activeSelf && sequence.ConfirmButton.interactable &&
                   sequence.GraphicRaycaster.enabled, "GAME OVER did not show its confirm input.");
            sequence.Advance(600f);
            Assert(sequence.IsAwaitingConfirm && confirmations == 0,
                "GAME OVER must not open the result by time alone.");
            Assert(sequence.Confirm() && confirmations == 1, "Confirming GAME OVER did not open the result.");
            Assert(!sequence.Confirm() && confirmations == 1, "Confirming twice opened the result twice.");
        }

        private static string[] FrontendSettingsControlNames()
        {
            Scene frontend = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendSettingsView settings = Week25Ux0Setup.FindSingle<FrontendSettingsView>(frontend,
                "Frontend settings view");
            return ControlNames(settings.transform);
        }

        private static string[] ControlNames(Transform panel)
        {
            return panel.GetComponentsInChildren<Selectable>(true)
                .Where(selectable => selectable.GetComponentInParent<TMP_Dropdown>(true) == null ||
                                     selectable is TMP_Dropdown)
                .Select(selectable => selectable.name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        private static int CountUx0Objects(Scene scene)
        {
            return scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Count(
                transform => transform.name == Week25Ux0Setup.SettingsRootName ||
                             transform.name == Week25Ux0Setup.SettingsPanelName ||
                             transform.name == Week25Ux0Setup.SettingsButtonName ||
                             transform.name == Week25Ux0Setup.GameOverCanvasName));
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
