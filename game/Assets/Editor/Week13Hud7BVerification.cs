using System;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud7BVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-7B Pause Menu")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            VerifyRuntimeState(scene);
            Debug.Log("Week 13 HUD-7B verification passed: live detailed stats, pause-only UI input, confined focus, restoration, and modal non-stacking.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Hud2Setup.Setup();
            Week13Hud3ASetup.Setup();
            Week13Hud3BSetup.Setup();
            Week13Hud3CSetup.Setup();
            Week13Hud7BSetup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud7BSetup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-7B setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-7B setup changed the Game Scene GUID.");
            Verify();
            Week13Hud3CVerification.ValidateScene(EditorSceneManager.GetActiveScene());
            Week13Flow4Verification.Verify();
            Debug.Log("Week 13 HUD-7B batch verification passed: setup twice, stable Scene GUID and HUD-3C/Flow-4 regression.");
        }

        public static void ValidateScene(Scene scene)
        {
            GamePauseArtifactView view = FindAll<GamePauseArtifactView>(scene).Single();
            Assert(view.PlayerStats != null && view.StatsText != null && view.ResumeButton != null &&
                view.FocusScope != null && view.GraphicRaycaster != null,
                "HUD-7B runtime or visual references are missing.");
            Assert(view.PlayerStats == FindAll<PlayerStats>(scene).Single(),
                "HUD-7B must read the actual player stats.");
            Assert(view.FocusScope == view.transform.Find("Pause Panel"),
                "HUD-7B focus scope must be the central pause panel.");
            Assert(!view.GraphicRaycaster.enabled,
                "HUD input raycasting must remain disabled outside pause.");
            Assert(view.ResumeButton.transform.IsChildOf(view.FocusScope) &&
                view.ResumeButton.GetComponent<RectTransform>().sizeDelta == new Vector2(260, 52),
                "HUD-7B requires a standard-size resume button inside the focus scope.");
            RectTransform statsPanel = view.FocusScope.Find("Detailed Stats") as RectTransform;
            RectTransform viewport = view.FocusScope.Find("Artifact Viewport") as RectTransform;
            Assert(statsPanel != null && statsPanel.sizeDelta == new Vector2(300, 560) &&
                viewport != null && viewport.sizeDelta == new Vector2(700, 520),
                "HUD-7B detailed stats and artifact list must fit the central popup without overlap.");
            EventSystem[] systems = FindAll<EventSystem>(scene);
            Assert(systems.Length == 1 && systems[0].GetComponent<InputSystemUIInputModule>() != null,
                "HUD-7B requires exactly one Input System EventSystem.");
            Assert(systems[0].firstSelectedGameObject == view.ResumeButton.gameObject,
                "HUD-7B EventSystem first selection must be the resume action.");
            Week13Hud3CVerification.ValidateScene(scene);
        }

        private static void VerifyRuntimeState(Scene scene)
        {
            GameObject player = new("HUD-7B Verification Player", typeof(Health), typeof(PlayerStats));
            GameObject clone = Object.Instantiate(FindAll<GamePauseArtifactView>(scene).Single().gameObject);
            GameObject outside = new("HUD-7B Previous Control", typeof(RectTransform), typeof(Image), typeof(Button));
            float originalTimeScale = Time.timeScale;
            EventSystem eventSystem = FindAll<EventSystem>(scene).Single();
            EventSystem previousEventSystem = EventSystem.current;
            try
            {
                InvokeLifecycle(eventSystem, "OnEnable");
                EventSystem.current = eventSystem;
                Assert(EventSystem.current == eventSystem,
                    "HUD-7B verification could not activate the Scene EventSystem lifecycle.");
                Health health = player.GetComponent<Health>();
                PlayerStats stats = player.GetComponent<PlayerStats>();
                InvokeLifecycle(health, "Awake");
                InvokeLifecycle(stats, "Awake");
                stats.AddAttackDamage(2.5f);
                stats.AddAttackSpeedPercent(0.25f);
                stats.AddMoveSpeed(1f);
                stats.AddCriticalChance(0.1f);
                stats.AddProjectiles(1);
                stats.AddPierce(2);

                GamePauseArtifactView view = clone.GetComponent<GamePauseArtifactView>();
                GraphicRaycaster runtimeRaycaster = clone.AddComponent<GraphicRaycaster>();
                runtimeRaycaster.enabled = false;
                view.ConfigureMenu(stats, view.StatsText, view.ResumeButton, view.FocusScope,
                    runtimeRaycaster);
                eventSystem.SetSelectedGameObject(outside);
                Time.timeScale = 1f;
                Assert(view.TryPause(), "HUD-7B failed to open from active combat.");
                Assert(view.IsPaused, "Opening pause did not set the paused state.");
                Assert(Mathf.Approximately(Time.timeScale, 0f), "Opening pause did not stop scaled game time.");
                Assert(view.GraphicRaycaster.enabled, "Opening pause did not enable pause UI raycasting.");
                Assert(eventSystem.currentSelectedGameObject == view.ResumeButton.gameObject,
                    $"Opening pause did not focus the resume action. Current=" +
                    $"{eventSystem.currentSelectedGameObject?.name ?? "<null>"}, " +
                    $"Resume={view.ResumeButton.gameObject.name}, " +
                    $"CurrentEventSystem={EventSystem.current?.gameObject.name ?? "<null>"}.");
                Assert(view.StatsText.text.Contains("공격력  3.5") &&
                    view.StatsText.text.Contains("공격속도  1.25") &&
                    view.StatsText.text.Contains("이동속도  6") &&
                    view.StatsText.text.Contains("치명타율  15%") &&
                    view.StatsText.text.Contains("투사체  2") && view.StatsText.text.Contains("관통  2"),
                    "HUD-7B detailed stats do not match the live PlayerStats values.");

                eventSystem.SetSelectedGameObject(outside);
                InvokeLifecycle(view, "LateUpdate");
                Assert(eventSystem.currentSelectedGameObject == view.ResumeButton.gameObject,
                    "HUD-7B focus escaped the pause popup.");
                view.ResumeButton.onClick.Invoke();
                Assert(!view.IsPaused && Mathf.Approximately(Time.timeScale, 1f) &&
                    !view.GraphicRaycaster.enabled && eventSystem.currentSelectedGameObject == outside,
                    "Closing pause must restore time, disable HUD raycasting, and return previous focus.");

                Time.timeScale = 0f;
                Assert(!view.TryPause() && !view.IsPaused,
                    "HUD-7B must not stack over a reward, result, or other time-stopping modal.");
            }
            finally
            {
                if (clone != null)
                {
                    GamePauseArtifactView view = clone.GetComponent<GamePauseArtifactView>();
                    if (view != null && view.IsPaused) view.Resume();
                    Object.DestroyImmediate(clone);
                }
                Object.DestroyImmediate(outside);
                Object.DestroyImmediate(player);
                eventSystem.SetSelectedGameObject(null);
                EventSystem.current = previousEventSystem;
                Time.timeScale = originalTimeScale;
            }
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
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
