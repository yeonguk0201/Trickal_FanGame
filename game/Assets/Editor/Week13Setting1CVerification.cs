using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13Setting1CVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify Setting-1C Detailed Stats HUD")]
        public static void Verify()
        {
            ValidateFrontend();
            ValidateGameHud();
            ValidatePersistenceAndImmediateRefresh();
            Debug.Log("Setting-1C verification passed: default-off persistence, settings toggle, live PlayerStats values, translucent side layout, and non-blocking HUD input.");
        }

        public static void SetupAndVerifyBatch()
        {
            string frontendGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath);
            string gameGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Setting1CSetup.Setup();
            Week13Setting1CSetup.Setup();
            Assert(frontendGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath) &&
                   gameGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Setting-1C setup changed a Scene GUID.");
            Verify();
            Week13Hud1Verification.Verify();
            Debug.Log("Setting-1C batch verification passed: repeated setup, stable Scene GUIDs, HUD-1 regression, and detailed stats contract.");
        }

        private static void ValidateFrontend()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendSettingsView settings = FindSingle<FrontendSettingsView>(scene);
            Assert(settings.DetailedStatsHudToggle != null, "Settings requires the detailed stats HUD toggle.");
            Assert(settings.DetailedStatsHudToggle.transform.parent == settings.transform,
                "The detailed stats toggle must stay inside the settings popup.");
            Assert(settings.GetComponent<RectTransform>().sizeDelta == new Vector2(720, 720),
                "The expanded settings popup must remain within the 720px UI-spec height.");
            TMP_Text label = settings.transform.Find("DetailedStatsHudLabel")?.GetComponent<TMP_Text>();
            Assert(label != null && label.text == "상세 스탯 표시" && label.fontSize >= 20f,
                "The detailed stats toggle needs a readable Korean label.");
        }

        private static void ValidateGameHud()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameDetailedStatsHudView view = FindSingle<GameDetailedStatsHudView>(scene);
            RectTransform panel = view.GetComponent<RectTransform>();
            Assert(panel.parent.name == "ReferenceFrame", "Detailed stats HUD must use the centered 16:9 frame.");
            Assert(panel.anchoredPosition == new Vector2(-776, 234) && panel.sizeDelta == new Vector2(240, 216),
                "Detailed stats HUD must occupy the specified left safe-area slot without entering central combat space.");
            Assert(view.PlayerStats != null && view.Content != null && view.ValuesText != null,
                "Detailed stats HUD is missing its live PlayerStats or UI references.");
            Assert(!view.Content.interactable && !view.Content.blocksRaycasts,
                "Detailed stats HUD must never intercept combat input.");
            Image background = panel.GetComponent<Image>();
            Assert(background != null && !background.raycastTarget && background.color.a <= 0.1f,
                "Detailed stats HUD background must stay nearly transparent and non-interactive.");
            Assert(view.ValuesText.color.a >= 0.6f && view.ValuesText.color.a <= 0.72f,
                "Detailed stats text must use the requested subdued opacity.");
            Image[] placeholderIcons = panel.GetComponentsInChildren<Image>(true)
                .Where(image => image.name.StartsWith("Placeholder Icon ", StringComparison.Ordinal)).ToArray();
            Assert(placeholderIcons.Length == 4 && placeholderIcons.All(icon => !icon.raycastTarget && icon.color.a <= 0.6f),
                "Four subdued temporary stat icons are required until final assets exist.");
        }

        private static void ValidatePersistenceAndImmediateRefresh()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameDetailedStatsHudView view = FindSingle<GameDetailedStatsHudView>(scene);
            bool original = LocalSettings.DetailedStatsHud;
            try
            {
                Assert(!LocalSettings.DefaultDetailedStatsHud, "Detailed stats HUD must default to off.");
                LocalSettings.DetailedStatsHud = false;
                view.RefreshNow();
                Assert(Mathf.Approximately(view.Content.alpha, 0f), "Turning the setting off must hide the current HUD.");

                LocalSettings.DetailedStatsHud = true;
                view.RefreshNow();
                Assert(Mathf.Approximately(view.Content.alpha, 1f), "Turning the setting on must show the current HUD.");
                string[] lines = view.ValuesText.text.Split('\n');
                Assert(lines.Length == 4 && lines[0] == $"{view.PlayerStats.AttackDamage:0.##}" &&
                       lines[1] == $"{view.PlayerStats.AttackSpeed:0.##}" &&
                       lines[2] == $"{view.PlayerStats.MoveSpeed:0.##}" &&
                       lines[3] == $"{view.PlayerStats.CriticalChance * 100f:0.#}%",
                    "Visible detailed stats must match the live PlayerStats values.");

                LocalSettings.Load();
                view.RefreshNow();
                Assert(LocalSettings.DetailedStatsHud && Mathf.Approximately(view.Content.alpha, 1f),
                    "Detailed stats choice must survive LocalSettings reload.");
            }
            finally
            {
                LocalSettings.DetailedStatsHud = original;
                view.RefreshNow();
            }
        }

        private static T FindSingle<T>(Scene scene) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            Assert(matches.Length == 1, $"{scene.path} requires exactly one {typeof(T).Name}; found {matches.Length}.");
            return matches[0];
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
