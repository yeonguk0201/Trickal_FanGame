using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud3BSetup
    {
        public const string ToastPanelName = "Artifact Acquisition Toast";

        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-3B Artifact Acquisition Toast")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-3B setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameHudView gameHud = FindSingle<GameHudView>(scene, "GameHudView");
            PlayerInventory inventory = FindSingle<PlayerInventory>(scene, "player inventory");
            if (FindAll<GameArtifactHudView>(scene).Length != 1)
                throw new InvalidOperationException("Run HUD-3A setup before HUD-3B setup.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            EnsureDescriptionGlyphs(font);
            RectTransform frame = gameHud.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("Game HUD ReferenceFrame is missing.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-3B Artifact Acquisition Toast");

            RectTransform panel = Rect(frame, ToastPanelName, Vector2.zero, new Vector2(720, 120));
            panel.SetAsLastSibling();
            Image panelImage = Component<Image>(panel.gameObject);
            panelImage.color = new Color(0.035f, 0.055f, 0.09f, 0.92f);
            panelImage.raycastTarget = false;
            CanvasGroup canvasGroup = Component<CanvasGroup>(panel.gameObject);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Transform obsoleteDescription = panel.Find("Artifact Description");
            if (obsoleteDescription != null) Undo.DestroyObjectImmediate(obsoleteDescription.gameObject);
            TMP_Text messageText = Text(panel, "Artifact Name",
                "<b>아티팩트 이름</b>\n<size=20><color=#C2D6EB>아티팩트 효과 설명</color></size>",
                Vector2.zero, new Vector2(680, 104), 28, font);
            messageText.fontStyle = FontStyles.Normal;
            messageText.richText = true;
            messageText.overflowMode = TextOverflowModes.Overflow;

            GameArtifactAcquisitionToastView toast = Component<GameArtifactAcquisitionToastView>(panel.gameObject);
            toast.Configure(inventory, canvasGroup, messageText, 1.5f);

            foreach (Component component in panel.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-3B setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-3B setup complete. Artifact acquisition notifications are connected without blocking input or game time.");
        }

        internal static void EnsureDescriptionGlyphs(TMP_FontAsset font)
        {
            string descriptions = string.Concat(AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                .Where(definition => definition != null && definition.IsValid)
                .Select(ArtifactEffectDescription.Build));
            if (!font.HasCharacters(descriptions) && !font.TryAddCharacters(descriptions, out string missing))
                throw new InvalidOperationException("Missing HUD-3B description glyphs: " + missing);

            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = FindAll<T>(scene);
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static T Component<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(owner);
            else Undo.RecordObject(component, "Configure " + typeof(T).Name);
            return component;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            RectTransform rect = existing != null ? existing as RectTransform : null;
            if (existing != null && rect == null) throw new InvalidOperationException(name + " requires RectTransform.");
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                Undo.RegisterCreatedObjectUndo(rect.gameObject, "Create " + name);
                rect.SetParent(parent, false);
            }
            Undo.RecordObject(rect, "Layout " + name);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size,
            float fontSize, TMP_FontAsset font)
        {
            TMP_Text text = Component<TextMeshProUGUI>(Rect(parent, name, position, size).gameObject);
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.92f, 0.96f, 1f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
