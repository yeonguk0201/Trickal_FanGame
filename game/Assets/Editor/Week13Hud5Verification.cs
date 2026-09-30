using System;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
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
    public static class Week13Hud5Verification
    {
        private const string BossDefinitionPath = "Assets/Rooms/Definitions/boss-standard.asset";

        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-5 Boss Status")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            VerifyRuntimeState(scene);
            Debug.Log("Week 13 HUD-5 verification passed: active-boss-only visibility, live health, phase changes, defeat hiding, normal-room hiding, and non-blocking lower-center layout.");
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
            Week13Hud5Setup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud5Setup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-5 setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-5 setup changed the Game Scene GUID.");
            Verify();
            Week13Hud4CVerification.ValidateScene(EditorSceneManager.GetActiveScene());
            Week13Flow4Verification.Verify();
            Debug.Log("Week 13 HUD-5 batch verification passed: setup twice, stable Scene GUID and HUD-4C/Flow-4 regression.");
        }

        public static void ValidateScene(Scene scene)
        {
            GameBossHudView[] views = FindAll<GameBossHudView>(scene);
            Assert(views.Length == 1, "Game Scene requires exactly one boss HUD view.");
            GameBossHudView view = views[0];
            Assert(view.RoomGraph == FindAll<RoomGraphController>(scene).Single(),
                "HUD-5 must observe the actual room graph.");
            Assert(view.CanvasGroup != null && view.BossNameText != null && view.PhaseText != null &&
                view.HealthText != null && view.HealthFill != null,
                "HUD-5 visual references are missing.");
            RectTransform panel = view.GetComponent<RectTransform>();
            Assert(panel.sizeDelta == new Vector2(720, 64) && panel.anchoredPosition == new Vector2(0, -454),
                "Boss HUD must occupy the specified 720x64 lower-center safe area.");
            Assert(!view.IsVisible && !view.CanvasGroup.interactable && !view.CanvasGroup.blocksRaycasts &&
                !view.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget),
                "Hidden HUD-5 must not intercept combat input.");
            Assert(view.HealthFill.sprite != null && view.HealthFill.type == Image.Type.Filled &&
                view.HealthFill.fillMethod == Image.FillMethod.Horizontal,
                "Boss HP must have a Sprite so Unity renders its horizontal fill amount.");
            Week13Hud4CVerification.ValidateScene(scene);
        }

        private static void VerifyRuntimeState(Scene scene)
        {
            GameObject root = new("HUD-5 Runtime Verification");
            GameObject graphObject = new("Graph", typeof(RoomGraphController));
            graphObject.transform.SetParent(root.transform);
            GameObject canvasObject = new("Canvas", typeof(Canvas));
            canvasObject.transform.SetParent(root.transform);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameBossHudView sceneView = FindAll<GameBossHudView>(scene).Single();
            GameObject viewObject = Object.Instantiate(sceneView.gameObject, canvasObject.transform);
            try
            {
                RoomGraphController graph = graphObject.GetComponent<RoomGraphController>();
                GameBossHudView view = viewObject.GetComponent<GameBossHudView>();
                view.Configure(graph, view.CanvasGroup, view.BossNameText, view.PhaseText,
                    view.HealthText, view.HealthFill);
                Assert(!view.IsVisible, "HUD-5 must start hidden without a current boss room.");

                RoomNode bossNode = CreateNode(root.transform, "Boss Room", true);
                GameObject bossObject = new("Verification Boss", typeof(Health), typeof(KnockbackReceiver),
                    typeof(BossController));
                bossObject.transform.SetParent(bossNode.ContentRoot.transform);
                Health health = bossObject.GetComponent<Health>();
                health.ResetHealth();
                BossController boss = bossObject.GetComponent<BossController>();
                boss.ConfigureHud("검증 보스", 3);
                boss.SetPhase(2);
                SetCurrentNode(graph, bossNode);
                view.RefreshNow();
                Assert(view.IsVisible && view.ObservedBoss == boss && view.BossNameText.text == "검증 보스" &&
                    view.PhaseText.text == "페이즈 2 / 3" && view.HealthText.text == "10 / 10" &&
                    Mathf.Approximately(view.HealthFill.fillAmount, 1f),
                    "An active current-room boss must show its real identity, health, and phase.");

                health.TakeDamage(4f);
                Assert(view.HealthText.text == "6 / 10" && Mathf.Approximately(view.HealthFill.fillAmount, 0.6f),
                    "Boss damage must update the HUD immediately from Health events.");
                boss.SetPhase(3);
                Assert(view.PhaseText.text == "페이즈 3 / 3",
                    "Boss phase changes must update the HUD immediately.");

                RoomNode normalNode = CreateNode(root.transform, "Normal Room", false);
                SetCurrentNode(graph, normalNode);
                view.RefreshNow();
                Assert(!view.IsVisible && view.ObservedBoss == null,
                    "Returning to a normal room must hide and unbind the boss HUD.");

                SetCurrentNode(graph, bossNode);
                view.RefreshNow();
                Assert(view.IsVisible, "Returning to a living boss room must restore the boss HUD.");
                health.TakeDamage(health.CurrentHealth);
                Assert(!view.IsVisible, "Boss defeat must hide the HUD immediately.");
                view.RefreshNow();
                Assert(!view.IsVisible && view.ObservedBoss == null,
                    "A defeated boss must not be rebound on later refreshes.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RoomNode CreateNode(Transform parent, string name, bool boss)
        {
            GameObject nodeObject = new(name, typeof(RoomNode));
            nodeObject.transform.SetParent(parent);
            GameObject content = new("Content");
            content.transform.SetParent(nodeObject.transform);
            GameObject anchor = new("Camera Anchor");
            anchor.transform.SetParent(nodeObject.transform);
            GameObject entry = new("Entry");
            entry.transform.SetParent(nodeObject.transform);
            RoomNode node = nodeObject.GetComponent<RoomNode>();
            node.Configure(name, 1, boss ? 2 : 1, content, anchor.transform, entry.transform,
                Array.Empty<RoomDoorway>());
            string definitionPath = boss ? BossDefinitionPath : "Assets/Rooms/Definitions/normal-chaser.asset";
            RoomDefinition definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(definitionPath);
            Assert(definition != null, "HUD-5 verification room definition is missing: " + definitionPath);
            node.ApplyGeneratedDefinition(definition);
            return node;
        }

        private static void SetCurrentNode(RoomGraphController graph, RoomNode node)
        {
            PropertyInfo property = typeof(RoomGraphController).GetProperty(nameof(RoomGraphController.CurrentNode),
                BindingFlags.Instance | BindingFlags.Public);
            Assert(property?.SetMethod != null, "RoomGraphController current-node verification hook is missing.");
            property.SetValue(graph, node);
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(rootObject => rootObject.GetComponentsInChildren<T>(true)).ToArray();

        private static int CountTransforms(Scene scene) => scene.GetRootGameObjects()
            .Sum(rootObject => rootObject.GetComponentsInChildren<Transform>(true).Length);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
