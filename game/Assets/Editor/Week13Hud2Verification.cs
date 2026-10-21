using System;
using System.Collections.Generic;
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
                view.HighGradeCooldownText != null && view.HighGradeSkillText == null &&
                view.LowerGradeSkillText == null,
                "HUD-2 visual references are missing.");
            Vector2 iconSize = Vector2.one * Week27Ux1Setup.SkillIconSize;
            Assert(view.LowerGradeSkillState.rectTransform.sizeDelta == iconSize &&
                view.HighGradeSkillState.rectTransform.sizeDelta == iconSize,
                "HUD-2 must use two equal UX-1 skill icons.");
            foreach (Image art in new[] { view.LowerGradeSkillState, view.HighGradeSkillState })
            {
                Image tile = art.transform.parent.GetComponent<Image>();
                Mask mask = art.transform.parent.GetComponent<Mask>();
                Assert(tile != null && mask != null && tile.sprite ==
                    AssetDatabase.LoadAssetAtPath<Sprite>(Week13FrontendUiAssets.RoundedSquareSpritePath) &&
                    ((RectTransform)tile.transform).sizeDelta == iconSize,
                    "UX-1 skill artwork must sit in a rounded tile of the same size.");
            }
            Assert(view.LowerGradeCooldownFill.sprite != null && view.HighGradeCooldownFill.sprite != null,
                "Skill overlays need a sprite; a sprite-less Filled Image ignores fillAmount and covers the icon.");
            Assert(view.LowerGradeCooldownFill.transform.parent == view.LowerGradeSkillState.transform.parent &&
                view.HighGradeCooldownFill.transform.parent == view.HighGradeSkillState.transform.parent,
                "The skill overlay must be clipped by the same rounded tile.");
            Assert(view.LowerGradeCooldownFill.type == Image.Type.Filled &&
                view.LowerGradeCooldownFill.fillMethod == Image.FillMethod.Radial360 &&
                view.HighGradeCooldownFill.type == Image.Type.Filled &&
                view.HighGradeCooldownFill.fillMethod == Image.FillMethod.Radial360 &&
                !view.HighGradeCooldownFill.fillClockwise,
                "Skill cooldown overlays must be counter-clockwise radial filled Images.");
            // Filled counter-clockwise from the top, the overlay's edge moves clockwise as the cooldown runs out.
            Assert(view.LowerGradeCooldownFill.fillOrigin == (int)Image.Origin360.Top &&
                view.HighGradeCooldownFill.fillOrigin == (int)Image.Origin360.Top &&
                !view.LowerGradeCooldownFill.fillClockwise &&
                view.HighGradeCooldownFill.color == GameHudView.SkillOverlayColor &&
                view.HighGradeCooldownFill.color.a < 0.9f,
                "UX-1: the skill overlay must be see-through dark and sweep clockwise from the top.");
            Assert(!view.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget &&
                graphic.GetComponentInParent<GamePauseArtifactView>() == null &&
                graphic.GetComponentInParent<ItemRewardSelectionView>() == null &&
                graphic.GetComponentInParent<ShopView>(true) == null),
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
                if (sp.CurrentSP == 0)
                {
                    view.RefreshSkillStateAt(100f);
                    Assert(!view.IsLowerGradeSkillAvailable && view.LowerGradeSkillState.color == Color.white &&
                        Mathf.Approximately(view.LowerGradeCooldownFill.fillAmount, 1f),
                        "UX-1: a lower grade without SP keeps its artwork and is covered by the dark overlay.");
                }
                Assert(sp.TryAdd(), "HUD-2 readiness verification could not add SP.");
                view.RefreshSkillStateAt(100f);
                Assert(view.IsLowerGradeSkillAvailable && view.IsHighGradeSkillAvailable,
                    "Ready HUD state does not match the actual lower/high-grade input conditions.");
                Assert(Mathf.Approximately(view.LowerGradeCooldownFill.fillAmount, 0f) &&
                    Mathf.Approximately(view.HighGradeCooldownFill.fillAmount, 0f) &&
                    string.IsNullOrEmpty(view.HighGradeCooldownText.text),
                    "Ready skills must not show a cooldown overlay or countdown.");
                AssertOverlayDrawsFill(view.LowerGradeCooldownFill);
                AssertOverlayDrawsFill(view.HighGradeCooldownFill);

                Assert(ultimate.TryActivate(100f), "HUD-2 verification could not activate the high-grade skill.");
                view.RefreshSkillStateAt(100f);
                Assert(!view.IsHighGradeSkillAvailable,
                    "Active high-grade skill must be shown as unavailable and in use.");
                Assert(view.HighGradeSkillState.color == Color.white &&
                    Mathf.Approximately(view.HighGradeCooldownFill.fillAmount, 1f),
                    "UX-1: a high grade in use keeps its artwork under a full dark overlay."); 

                float cooldownStart = 100f + ultimate.MaximumDuration;
                ultimate.Tick(cooldownStart);
                ultimate.Tick(cooldownStart + ultimate.CoastRecoveryDuration);
                float midpoint = cooldownStart + ultimate.Cooldown * 0.5f;
                view.RefreshSkillStateAt(midpoint);
                Assert(Mathf.Approximately(view.HighGradeCooldownRemaining, ultimate.Cooldown * 0.5f) &&
                    Mathf.Approximately(view.HighGradeCooldownFill.fillAmount, 0.5f) &&
                    view.HighGradeCooldownText.text == Mathf.CeilToInt(ultimate.Cooldown * 0.5f).ToString() &&
                    !view.IsHighGradeSkillAvailable &&
                    view.HighGradeSkillState.color == Color.white,
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
                    Mathf.Approximately(view.HighGradeCooldownFill.fillAmount, 0f),
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

        // UX-1: what the overlay actually draws. Nothing when empty, a part at half, everything when full; a
        // sprite-less Filled Image draws the full square at every amount.
        private static void AssertOverlayDrawsFill(Image overlay)
        {
            float original = overlay.fillAmount;
            try
            {
                Assert(MeshArea(overlay, 0f) < 0.001f && MeshArea(overlay, 0.5f) > 0.001f &&
                    MeshArea(overlay, 0.5f) < MeshArea(overlay, 1f) - 0.001f,
                    $"{overlay.transform.parent.parent.name} overlay must draw only its fill amount.");
            }
            finally
            {
                overlay.fillAmount = original;
            }
        }

        private static float MeshArea(Image overlay, float amount)
        {
            overlay.fillAmount = amount;
            using UnityEngine.UI.VertexHelper helper = new();
            MethodInfo populate = typeof(Image).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(UnityEngine.UI.VertexHelper) }, null);
            populate.Invoke(overlay, new object[] { helper });
            float area = 0f;
            UIVertex a = default, b = default, c = default;
            List<UIVertex> stream = new();
            helper.GetUIVertexStream(stream);
            for (int index = 0; index + 2 < stream.Count; index += 3)
            {
                a = stream[index];
                b = stream[index + 1];
                c = stream[index + 2];
                area += Mathf.Abs((b.position.x - a.position.x) * (c.position.y - a.position.y) -
                                  (c.position.x - a.position.x) * (b.position.y - a.position.y)) * 0.5f;
            }
            return area;
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
