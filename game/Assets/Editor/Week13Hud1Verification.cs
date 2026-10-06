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
            Assert(view.HeartRowRoot != null && view.HeartTemplate != null &&
                view.SpSlotsRoot != null && view.SpSlotTemplate != null && view.SpValueText != null &&
                view.LowerGradeSkillState != null && view.LowerGradeSkillText != null,
                "GameHudView visual references are missing.");
            Assert(!view.SpSlotTemplate.gameObject.activeSelf && view.SpSlotTemplate.rectTransform.sizeDelta == new Vector2(40, 40),
                "HUD-1 must use one inactive 40x40 SP slot template.");
            ValidateHeartRow(view);
            Assert(!view.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget &&
                graphic.GetComponentInParent<GamePauseArtifactView>() == null &&
                graphic.GetComponentInParent<ItemRewardSelectionView>() == null),
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
                // Values are half-heart units: 10 units = 5 hearts.
                AssertHearts(view, "initial 10/10", 0, 1f, 1f, 1f, 1f, 1f);

                health.TakeDamage(3f);
                AssertHearts(view, "damage to 7/10", 0, 1f, 1f, 1f, 0.5f, 0f);
                Assert(Mathf.Approximately(view.LastHealthDelta, -3f) && view.PulsingHeartCount == 2 &&
                    !view.IsHeartPulsing(2) && view.IsHeartPulsing(3) && view.IsHeartPulsing(4),
                    "Damage must pop only the hearts whose fill changed.");
                health.Heal(2f);
                AssertHearts(view, "heal to 9/10", 0, 1f, 1f, 1f, 1f, 0.5f);
                Assert(Mathf.Approximately(view.LastHealthDelta, 2f),
                    "Healing did not immediately update healing feedback.");
                SetMaxHealth(health, 12f);
                AssertHearts(view, "max growth to 11/12", 0, 1f, 1f, 1f, 1f, 1f, 0.5f);
                Assert(Mathf.Approximately(view.LastHealthDelta, 2f),
                    "Maximum HP growth did not immediately update the heart row.");

                health.EnableHealthUnits();
                health.TakeDamage(1f);
                Assert(health.UsesHealthUnits && Mathf.Approximately(health.CurrentHealth, 9f),
                    "Unit-mode setup must apply the one-heart minimum damage.");
                AssertHearts(view, "unit-mode 9/12", 0, 1f, 1f, 1f, 1f, 0.5f, 0f);

                health.SetShield(3f);
                AssertHearts(view, "shield 3 units", 2, 1f, 1f, 1f, 1f, 0.5f, 0f, 1f, 0.5f);
                Assert(!view.IsShieldHeart(5) && view.IsShieldHeart(6) && view.IsShieldHeart(7),
                    "Shield hearts must follow the health hearts.");

                SetMaxHealth(health, 40f);
                Assert(view.HeartCount == 22 && view.HeartSize < 36f &&
                    view.HeartSize * view.HeartCount + 4f * (view.HeartCount - 1) <=
                    view.HeartRowRoot.rect.width + 0.01f,
                    "Many hearts must shrink to stay inside the HP row.");
                SetMaxHealth(health, 12f);
                health.SetShield(0f);
                Assert(view.HeartCount == 6 && view.ShieldHeartCount == 0 && Mathf.Approximately(view.HeartSize, 36f),
                    "Removing shield and extra max HP must remove surplus hearts and restore the icon size.");

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

        private static void ValidateHeartRow(GameHudView view)
        {
            RectTransform template = view.HeartTemplate;
            Assert(view.HeartRowRoot.name == Week13Hud1Setup.HeartRowName &&
                view.HeartRowRoot.sizeDelta == Week13Hud1Setup.HeartRowSize &&
                view.HeartRowRoot.anchoredPosition == Week13Hud1Setup.HeartRowPosition &&
                view.HeartRowRoot.GetComponent<HorizontalLayoutGroup>() != null,
                "HP hearts must use the configured top row of the Survival HUD.");
            Assert(template.parent == view.HeartRowRoot && !template.gameObject.activeSelf &&
                template.name == Week13Hud1Setup.HeartTemplateName,
                "HP-2 must keep one inactive heart template inside the heart row.");
            Image background = template.Find("Background")?.GetComponent<Image>();
            Image fill = template.Find("Fill")?.GetComponent<Image>();
            Sprite heart = AssetDatabase.LoadAssetAtPath<Sprite>(Week13FrontendUiAssets.HeartSpritePath);
            Assert(heart != null && background != null && fill != null &&
                background.sprite == heart && fill.sprite == heart &&
                fill.type == Image.Type.Filled && fill.fillMethod == Image.FillMethod.Horizontal &&
                fill.fillOrigin == (int)Image.OriginHorizontal.Left,
                "Heart template must layer an empty heart and a left-origin horizontal fill using the heart sprite.");
            Transform panel = view.HeartRowRoot.parent;
            Assert(panel.Find("HP Bar") == null && panel.Find("HP Label") == null,
                "The legacy HP gauge must be removed.");
            Assert(view.HealthFeedback == null && panel.Find("HP Change Feedback") == null,
                "HP changes must not flash a row-wide rectangle behind the hearts.");
        }

        private static void AssertHearts(GameHudView view, string label, int shieldHearts, params float[] fills)
        {
            Assert(view.HeartCount == fills.Length && view.ShieldHeartCount == shieldHearts &&
                view.HealthHeartCount == fills.Length - shieldHearts,
                $"{label}: expected {fills.Length} hearts ({shieldHearts} shield), got {view.HeartCount} " +
                $"({view.ShieldHeartCount} shield).");
            for (int i = 0; i < fills.Length; i++)
            {
                Assert(Mathf.Approximately(view.GetHeartFill(i), fills[i]),
                    $"{label}: heart {i + 1} fill must be {fills[i]}, got {view.GetHeartFill(i)}.");
            }
        }

        private static void SetMaxHealth(Health health, float value)
        {
            typeof(Health).GetMethod("SetMaxHealth", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(health, new object[] { value, true });
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
