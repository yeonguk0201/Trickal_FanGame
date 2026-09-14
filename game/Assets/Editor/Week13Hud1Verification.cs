using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
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
    public static class Week13Hud1Verification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-1 HP and SP")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            VerifyRuntimeState(scene);
            Debug.Log("Week 13 HUD-1 verification passed: live HP, repeated SP slots, change feedback and lower-grade skill readiness.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Hud1Setup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud1Setup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-1 setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-1 setup changed the Game Scene GUID.");
            Verify();
            Week13Flow4Verification.Verify();
            Debug.Log("Week 13 HUD-1 batch verification passed: setup twice, stable Scene GUID and Flow-4 regression.");
        }

        public static void ValidateScene(Scene scene)
        {
            GameHudView[] views = FindAll<GameHudView>(scene);
            Assert(views.Length == 1, "Game Scene requires exactly one GameHudView.");
            GameHudView view = views[0];
            Assert(view.gameObject.name == Week13Hud1Setup.HudRootName,
                "GameHudView must be placed on the dedicated Game HUD Canvas root.");
            Assert(view.PlayerHealth != null && view.PlayerSP != null && view.LowerGradeSkill != null,
                "GameHudView player state references are missing.");
            Assert(view.PlayerHealth.gameObject == view.PlayerSP.gameObject &&
                view.PlayerSP.gameObject == view.LowerGradeSkill.gameObject,
                "HUD-1 state references must point to the same player.");
            Assert(view.HpFill != null && view.HpValueText != null && view.HealthFeedback != null &&
                view.SpSlotsRoot != null && view.SpSlotTemplate != null && view.SpValueText != null &&
                view.LowerGradeSkillState != null && view.LowerGradeSkillText != null,
                "GameHudView visual references are missing.");
            Assert(!view.SpSlotTemplate.gameObject.activeSelf && view.SpSlotTemplate.rectTransform.sizeDelta == new Vector2(40, 40),
                "HUD-1 must use one inactive 40x40 SP slot template.");
            Assert(view.HpFill.sprite != null && view.HpFill.type == Image.Type.Filled &&
                view.HpFill.fillMethod == Image.FillMethod.Horizontal,
                "HP fill must have a Sprite so Unity renders its horizontal fill amount.");
            Assert(!view.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget &&
                graphic.GetComponentInParent<GamePauseArtifactView>() == null),
                "HUD-1 graphics must not intercept combat input.");

            Canvas canvas = view.GetComponent<Canvas>();
            CanvasScaler scaler = view.GetComponent<CanvasScaler>();
            GraphicRaycaster raycaster = view.GetComponent<GraphicRaycaster>();
            Assert(canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.sortingOrder == 20,
                "HUD-1 Canvas render settings are invalid.");
            Assert(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                scaler.referenceResolution == new Vector2(1920, 1080) &&
                scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight &&
                Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f),
                "HUD-1 Canvas Scaler does not match the UI specification.");
            Assert(raycaster != null && !raycaster.enabled, "HUD-1 GraphicRaycaster must be disabled.");

            RectTransform panel = view.transform.Find("ReferenceFrame/Survival HUD") as RectTransform;
            Assert(panel != null && panel.sizeDelta == new Vector2(500, 128) &&
                panel.anchoredPosition == new Vector2(-646, 422),
                "Survival HUD must occupy the specified top-left 500x128 region.");
            Assert(FindAll<PlayerSP>(scene).Length == 1, "HUD-1 setup changed player SP component cardinality.");
        }

        private static void VerifyRuntimeState(Scene scene)
        {
            GameHudView sourceView = FindAll<GameHudView>(scene).Single();
            GameObject player = new("HUD-1 Verification Player", typeof(Health), typeof(PlayerSP));
            GameObject hudClone = Object.Instantiate(sourceView.gameObject);
            hudClone.name = "HUD-1 Verification Canvas";
            try
            {
                Health health = player.GetComponent<Health>();
                PlayerSP sp = player.GetComponent<PlayerSP>();
                InvokeLifecycle(health, "Awake");
                InvokeLifecycle(sp, "Awake");

                GameHudView view = hudClone.GetComponent<GameHudView>();
                view.Configure(health, sp, null, view.HpFill, view.HpValueText, view.HealthFeedback,
                    view.SpSlotsRoot, view.SpSlotTemplate, view.SpValueText, view.LowerGradeSkillState,
                    view.LowerGradeSkillText);
                view.RefreshNow();

                Assert(view.SlotCount == 3 && view.ActiveSlotCount == 0 && view.SpValueText.text == "SP 0 / 3",
                    "Initial SP slots do not match current and maximum SP.");
                Assert(Mathf.Approximately(view.HpFill.fillAmount, 1f) && view.HpValueText.text == "10 / 10",
                    "Initial HP HUD does not match player Health.");

                health.TakeDamage(3f);
                Assert(Mathf.Approximately(view.HpFill.fillAmount, 0.7f) && view.HpValueText.text == "7 / 10" &&
                    Mathf.Approximately(view.LastHealthDelta, -3f) && view.HealthFeedback.gameObject.activeSelf,
                    "Damage did not immediately update HP and damage feedback.");
                health.Heal(2f);
                Assert(Mathf.Approximately(view.HpFill.fillAmount, 0.9f) && view.HpValueText.text == "9 / 10" &&
                    Mathf.Approximately(view.LastHealthDelta, 2f),
                    "Healing did not immediately update HP and healing feedback.");
                typeof(Health).GetMethod("SetMaxHealth", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(health, new object[] { 12f, true });
                Assert(Mathf.Approximately(view.HpFill.fillAmount, 11f / 12f) &&
                    view.HpValueText.text == "11 / 12" && Mathf.Approximately(view.LastHealthDelta, 2f),
                    "Maximum HP growth did not immediately update the HP value and fill.");

                Assert(sp.TryAdd(2) && view.ActiveSlotCount == 2 && view.LastSPDelta == 2,
                    "SP gain did not activate repeated slots.");
                Assert(sp.TrySpend() && view.ActiveSlotCount == 1 && view.LastSPDelta == -1,
                    "SP spend did not deactivate one slot.");
                Assert(sp.AddMaxSP() && view.SlotCount == 4 && view.ActiveSlotCount == 1 &&
                    view.SpValueText.text == "SP 1 / 4",
                    "Maximum SP growth did not rebuild the repeated slot list.");
            }
            finally
            {
                Object.DestroyImmediate(hudClone);
                Object.DestroyImmediate(player);
            }

            VerifyActualSkillReadiness(scene);
        }

        private static void VerifyActualSkillReadiness(Scene scene)
        {
            PlayerSkill skill = FindAll<PlayerSkill>(scene).Single();
            Health health = skill.GetComponent<Health>();
            PlayerSP sp = skill.GetComponent<PlayerSP>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(sp, "Awake");
            InvokeLifecycle(skill.GetComponent<PlayerMovement>(), "Awake");
            InvokeLifecycle(skill, "Awake");

            GameHudView sourceView = FindAll<GameHudView>(scene).Single();
            GameObject hudClone = Object.Instantiate(sourceView.gameObject);
            try
            {
                GameHudView view = hudClone.GetComponent<GameHudView>();
                view.Configure(health, sp, skill, view.HpFill, view.HpValueText, view.HealthFeedback,
                    view.SpSlotsRoot, view.SpSlotTemplate, view.SpValueText, view.LowerGradeSkillState,
                    view.LowerGradeSkillText);
                view.RefreshNow();
                Assert(!view.IsLowerGradeSkillAvailable, "Lower-grade skill must be unavailable at zero SP.");
                Assert(sp.TryAdd(), "Skill readiness verification could not add SP.");
                view.RefreshNow();
                Assert(view.IsLowerGradeSkillAvailable && view.LowerGradeSkillText.text.Contains("사용 가능"),
                    "Lower-grade skill readiness did not match the actual cast preconditions.");
                sp.TrySpend();
            }
            finally
            {
                Object.DestroyImmediate(hudClone);
            }
        }

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
