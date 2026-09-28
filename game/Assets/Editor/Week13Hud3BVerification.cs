using System;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud3BVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-3B Artifact Acquisition Toast")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            VerifyDescriptions();
            VerifyRuntimeState(scene);
            Debug.Log("Week 13 HUD-3B verification passed: 1.5-second unscaled notifications, effect descriptions, queueing and input passthrough.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Hud2Setup.Setup();
            Week13Hud3ASetup.Setup();
            Week13Hud3BSetup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud3BSetup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-3B setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-3B setup changed the Game Scene GUID.");
            Verify();
            Week13Flow4Verification.Verify();
            Debug.Log("Week 13 HUD-3B batch verification passed: setup twice, stable Scene GUID and HUD-3A/HUD-2/Flow-4 regression.");
        }

        public static void ValidateScene(Scene scene)
        {
            GameArtifactAcquisitionToastView[] views = FindAll<GameArtifactAcquisitionToastView>(scene);
            Assert(views.Length == 1, "Game Scene requires exactly one artifact acquisition toast.");
            GameArtifactAcquisitionToastView view = views[0];
            PlayerInventory inventory = FindAll<PlayerInventory>(scene).Single();
            Assert(view.Inventory == inventory, "HUD-3B must reference the actual player inventory.");
            Assert(view.ToastCanvasGroup != null && view.MessageText != null,
                "HUD-3B visual references are missing.");
            Assert(Mathf.Approximately(view.DisplayDuration, 1.5f),
                "Artifact acquisition notification must last 1.5 seconds.");

            RectTransform panel = view.GetComponent<RectTransform>();
            Assert(panel.sizeDelta == new Vector2(720, 120) && panel.anchoredPosition == Vector2.zero,
                "Artifact acquisition notification must occupy the temporary center region.");
            Assert(view.gameObject.activeSelf && view.enabled && Mathf.Approximately(view.ToastCanvasGroup.alpha, 0f),
                "HUD-3B must stay active for inventory events while remaining hidden between notifications.");
            Assert(!view.ToastCanvasGroup.interactable && !view.ToastCanvasGroup.blocksRaycasts &&
                !view.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget),
                "HUD-3B must not intercept combat input.");
            Assert(view.MessageText.fontSize == 28 && view.MessageText.richText &&
                view.MessageText.rectTransform.sizeDelta == new Vector2(680, 104) &&
                view.MessageText.textWrappingMode == TMPro.TextWrappingModes.NoWrap &&
                view.MessageText.overflowMode == TMPro.TextOverflowModes.Overflow,
                "HUD-3B must render its name and smaller description through one two-line TMP mesh.");
            Assert(view.transform.Find("Artifact Description") == null,
                "HUD-3B must not retain the separately culled legacy description renderer.");
            Week13Hud3AVerification.ValidateScene(scene);
        }

        private static void VerifyDescriptions()
        {
            string[] assetGuids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" });
            Assert(assetGuids.Length > 0, "HUD-3B description verification requires artifact definitions.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null, "HUD-3B description verification requires the Frontend TMP font.");
            foreach (string guid in assetGuids)
            {
                ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null || !definition.IsValid) continue;
                string description = ArtifactEffectDescription.Build(definition);
                Assert(!string.IsNullOrWhiteSpace(description) && !description.Contains("\n") && !description.Contains("\r"),
                    "Artifact description must be a non-empty single line for " + definition.ItemId + ".");
                Assert(font.HasCharacters(description),
                    "Frontend TMP font is missing a HUD-3B description glyph for " + definition.ItemId + ".");
            }
        }

        private static void VerifyRuntimeState(Scene scene)
        {
            GameObject player = new("HUD-3B Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory));
            GameObject canvasObject = new("HUD-3B Verification Canvas", typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject toastClone = Object.Instantiate(FindAll<GameArtifactAcquisitionToastView>(scene).Single().gameObject);
            toastClone.transform.SetParent(canvasObject.transform, false);
            ItemDefinition first = null;
            ItemDefinition second = null;
            float originalTimeScale = Time.timeScale;
            try
            {
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                InvokeLifecycle(inventory, "Awake");

                GameArtifactAcquisitionToastView view = toastClone.GetComponent<GameArtifactAcquisitionToastView>();
                view.Configure(inventory, view.ToastCanvasGroup, view.MessageText, 1.5f);
                Assert(!view.IsShowing && Mathf.Approximately(view.ToastCanvasGroup.alpha, 0f),
                    "HUD-3B must begin hidden without replaying existing inventory state.");

                first = CreateDefinition("item-toast-01", "첫 아티팩트", 2,
                    new ItemEffectEntry(ItemEffectType.MaxHealthFlat, 2f),
                    new ItemEffectEntry(ItemEffectType.ShieldOnAcquireMaxHealthPercent, 0.5f));
                second = CreateDefinition("item-toast-02", "두 번째 아티팩트", 1,
                    new ItemEffectEntry(ItemEffectType.Pierce, configuredIntegerAmount: 1),
                    new ItemEffectEntry(ItemEffectType.SplitAfterPierce, configuredSecondaryMagnitude: 0.3f,
                        configuredIntegerAmount: 3, configuredMaximumDistance: 3f,
                        configuredSpreadAngleDegrees: 15f, configuredScaleMultiplier: 0.6f));

                EventSystem eventSystem = EventSystem.current;
                Assert(inventory.TryAcquire(first), "First HUD-3B artifact acquisition failed.");
                Assert(view.IsShowing && view.CurrentName == "첫 아티팩트" &&
                    view.CurrentDescription == "최대 HP +1칸 · 획득 시 최대 HP 50% 방어막" &&
                    Mathf.Approximately(view.RemainingSeconds, 1.5f) && Mathf.Approximately(view.ToastCanvasGroup.alpha, 1f),
                    "First artifact acquisition must immediately show its name and one-line effect description.");
                AssertMessageMesh(view.MessageText, view.CurrentName, view.CurrentDescription);

                Assert(inventory.TryAcquire(second) && view.PendingCount == 1 && view.CurrentName == "첫 아티팩트",
                    "A rapid second acquisition must queue without replacing the current notification.");
                Advance(view, 1.49f);
                Assert(view.IsShowing && view.CurrentName == "첫 아티팩트",
                    "The first notification disappeared before 1.5 unscaled seconds.");
                Advance(view, 0.02f);
                Assert(view.IsShowing && view.PendingCount == 0 && view.CurrentName == "두 번째 아티팩트" &&
                    view.CurrentDescription == "관통 +1 · 첫 관통 시 분열탄 3개" && view.RemainingSeconds > 1.48f,
                    "Queued acquisition must appear for its own complete duration.");
                AssertMessageMesh(view.MessageText, view.CurrentName, view.CurrentDescription);
                Advance(view, 1.5f);
                Assert(!view.IsShowing && Mathf.Approximately(view.ToastCanvasGroup.alpha, 0f),
                    "Artifact notification must hide after the queued display duration.");
                Assert(Mathf.Approximately(Time.timeScale, originalTimeScale) && EventSystem.current == eventSystem,
                    "HUD-3B must not alter combat time or EventSystem input state.");

                Assert(!inventory.TryAcquire(second) && !view.IsShowing && view.PendingCount == 0,
                    "A rejected max-stack acquisition must not create a notification.");
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(player);
                if (first != null) Object.DestroyImmediate(first);
                if (second != null) Object.DestroyImmediate(second);
            }
        }

        private static ItemDefinition CreateDefinition(string itemId, string displayName, int maxStacks,
            params ItemEffectEntry[] effects)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = displayName;
            definition.ConfigureContract(itemId, displayName, ItemRarity.Rare, true, maxStacks, effects);
            return definition;
        }

        private static void Advance(GameArtifactAcquisitionToastView view, float seconds)
        {
            MethodInfo method = typeof(GameArtifactAcquisitionToastView).GetMethod("Advance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, "HUD-3B deterministic timing hook is missing.");
            method.Invoke(view, new object[] { seconds });
        }

        private static void AssertMessageMesh(TMP_Text message, string expectedName, string expectedDescription)
        {
            Canvas.ForceUpdateCanvases();
            message.ForceMeshUpdate(true, true);
            string parsed = message.GetParsedText();
            Assert(parsed.Contains(expectedName) && parsed.Contains(expectedDescription),
                "HUD-3B two-line TMP mesh did not contain both the name and description.");
            Assert(message.textInfo.characterCount > expectedName.Length && message.textInfo.lineCount == 2,
                "HUD-3B must generate one visible TMP mesh with exactly two lines.");
            Assert(message.preferredHeight <= message.rectTransform.rect.height,
                "HUD-3B two-line message exceeds its RectTransform height and may be culled in a player build.");
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
