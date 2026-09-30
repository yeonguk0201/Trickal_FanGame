using System;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud2Verification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-2 Skill States")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            VerifyRuntimeState(scene);
            Debug.Log("Week 13 HUD-2 verification passed: skill icons, actual readiness, cooldown countdown and pause-safe game time.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Hud1Setup.Setup();
            Week13Hud2Setup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud2Setup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-2 setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-2 setup changed the Game Scene GUID.");
            Verify();
            Week13Flow4Verification.Verify();
            Debug.Log("Week 13 HUD-2 batch verification passed: setup twice, stable Scene GUID and Flow-4 regression.");
        }

        public static void ValidateScene(Scene scene)
        {
            GameHudView[] views = FindAll<GameHudView>(scene);
            Assert(views.Length == 1, "Game Scene requires exactly one GameHudView.");
            GameHudView view = views[0];
            Assert(view.HighGradeSkill != null && view.HighGradeSkill.gameObject == view.LowerGradeSkill.gameObject,
                "Both HUD-2 skill references must point to the same player.");
            Assert(view.LowerGradeSkillState != null && view.LowerGradeCooldownFill != null &&
                view.HighGradeSkillState != null && view.HighGradeCooldownFill != null &&
                view.HighGradeCooldownText != null && view.HighGradeSkillText != null,
                "HUD-2 visual references are missing.");
            Assert(view.LowerGradeSkillState.rectTransform.sizeDelta == new Vector2(72, 72) &&
                view.HighGradeSkillState.rectTransform.sizeDelta == new Vector2(72, 72),
                "HUD-2 must use two 72x72 skill icons.");
            Assert(view.LowerGradeCooldownFill.type == Image.Type.Filled &&
                view.LowerGradeCooldownFill.fillMethod == Image.FillMethod.Radial360 &&
                view.HighGradeCooldownFill.type == Image.Type.Filled &&
                view.HighGradeCooldownFill.fillMethod == Image.FillMethod.Radial360 &&
                !view.HighGradeCooldownFill.fillClockwise,
                "Skill cooldown overlays must be counter-clockwise radial filled Images.");
            Assert(!view.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget &&
                graphic.GetComponentInParent<GamePauseArtifactView>() == null &&
                graphic.GetComponentInParent<ItemRewardSelectionView>() == null),
                "HUD-2 graphics must not intercept combat input.");

            RectTransform panel = view.transform.Find("ReferenceFrame/" + Week13Hud2Setup.SkillPanelName) as RectTransform;
            Assert(panel != null && panel.sizeDelta == new Vector2(300, 128) &&
                panel.anchoredPosition == new Vector2(746, -428),
                "Skill HUD must occupy the specified bottom-right 300x128 region.");
            Assert(panel.Find("Lower Grade/Icon/Key").GetComponent<TMP_Text>().text == "SPACE" &&
                panel.Find("High Grade/Icon/Key").GetComponent<TMP_Text>().text == "Q",
                "HUD-2 skill icons must show their actual input keys.");
            Assert(frameChildCount(panel, "Lower Grade") == 1 && frameChildCount(panel, "High Grade") == 1,
                "HUD-2 contains duplicate skill icon roots.");
            Week13Hud1Verification.ValidateScene(scene);
        }

        private static void VerifyRuntimeState(Scene scene)
        {
            PlayerUltimate ultimate = FindAll<PlayerUltimate>(scene).Single();
            PlayerSkill lowerSkill = ultimate.GetComponent<PlayerSkill>();
            PlayerSP sp = ultimate.GetComponent<PlayerSP>();
            InvokeLifecycle(ultimate.GetComponent<TrickalFanGame.Combat.Health>(), "Awake");
            InvokeLifecycle(sp, "Awake");
            InvokeLifecycle(ultimate.GetComponent<PlayerMovement>(), "Awake");
            InvokeLifecycle(ultimate.GetComponent<PlayerActionState>(), "Awake");
            InvokeLifecycle(lowerSkill, "Awake");
            InvokeLifecycle(ultimate, "Awake");

            GameObject hudClone = Object.Instantiate(FindAll<GameHudView>(scene).Single().gameObject);
            float originalTimeScale = Time.timeScale;
            try
            {
                GameHudView view = hudClone.GetComponent<GameHudView>();
                Assert(sp.TryAdd(), "HUD-2 readiness verification could not add SP.");
                view.RefreshSkillStateAt(100f);
                Assert(view.IsLowerGradeSkillAvailable && view.IsHighGradeSkillAvailable,
                    "Ready HUD state does not match the actual lower/high-grade input conditions.");
                Assert(Mathf.Approximately(view.LowerGradeCooldownFill.fillAmount, 0f) &&
                    Mathf.Approximately(view.HighGradeCooldownFill.fillAmount, 0f) &&
                    string.IsNullOrEmpty(view.HighGradeCooldownText.text),
                    "Ready skills must not show a cooldown overlay or countdown.");

                Assert(ultimate.TryActivate(100f), "HUD-2 verification could not activate the high-grade skill.");
                view.RefreshSkillStateAt(100f);
                Assert(!view.IsHighGradeSkillAvailable && view.HighGradeSkillText.text == "사용 중",
                    "Active high-grade skill must be shown as unavailable and in use.");

                float cooldownStart = 100f + ultimate.MaximumDuration;
                ultimate.Tick(cooldownStart);
                ultimate.Tick(cooldownStart + ultimate.CoastRecoveryDuration);
                float midpoint = cooldownStart + ultimate.Cooldown * 0.5f;
                view.RefreshSkillStateAt(midpoint);
                Assert(Mathf.Approximately(view.HighGradeCooldownRemaining, ultimate.Cooldown * 0.5f) &&
                    Mathf.Approximately(view.HighGradeCooldownFill.fillAmount, 0.5f) &&
                    view.HighGradeCooldownText.text == Mathf.CeilToInt(ultimate.Cooldown * 0.5f).ToString() &&
                    view.HighGradeSkillText.text == "쿨타임" && !view.IsHighGradeSkillAvailable,
                    "High-grade cooldown countdown does not match PlayerUltimate's actual cooldown.");

                Time.timeScale = 0f;
                InvokeLifecycle(view, "Update");
                float pausedRemaining = view.HighGradeCooldownRemaining;
                string pausedText = view.HighGradeCooldownText.text;
                InvokeLifecycle(view, "Update");
                Assert(Mathf.Approximately(view.HighGradeCooldownRemaining, pausedRemaining) &&
                    view.HighGradeCooldownText.text == pausedText,
                    "High-grade cooldown changed while scaled game time was paused.");

                Time.timeScale = originalTimeScale;
                view.RefreshSkillStateAt(cooldownStart + ultimate.Cooldown);
                Assert(view.IsHighGradeSkillAvailable && Mathf.Approximately(view.HighGradeCooldownRemaining, 0f) &&
                    view.HighGradeSkillText.text == "사용 가능",
                    "High-grade skill HUD did not become ready at the cooldown boundary.");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                InvokeLifecycle(ultimate, "OnDisable");
                Object.DestroyImmediate(hudClone);
                sp.TrySpend();
            }
        }

        private static int frameChildCount(Transform panel, string name) => panel.Cast<Transform>()
            .Count(child => child.name == name);

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
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
