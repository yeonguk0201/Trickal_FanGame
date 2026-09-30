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
    public static class Week13Hud3CSetup
    {
        public const string OverlayName = "Pause Artifact Overlay";

        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-3C Pause Artifact List")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-3C setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameHudView gameHud = FindSingle<GameHudView>(scene, "GameHudView");
            PlayerInventory inventory = FindSingle<PlayerInventory>(scene, "player inventory");
            if (FindAll<GameArtifactHudView>(scene).Length != 1)
                throw new InvalidOperationException("Run HUD-3A setup before HUD-3C setup.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            RectTransform frame = gameHud.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("Game HUD ReferenceFrame is missing.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-3C Pause Artifact List");

            RectTransform overlayRect = Rect(frame, OverlayName, Vector2.zero, new Vector2(1920, 1080));
            overlayRect.SetAsLastSibling();
            Image backdrop = Component<Image>(overlayRect.gameObject);
            backdrop.color = new Color(0.01f, 0.015f, 0.025f, 0.82f);
            backdrop.raycastTarget = true;
            CanvasGroup overlay = Component<CanvasGroup>(overlayRect.gameObject);
            overlay.alpha = 0f;
            overlay.interactable = false;
            overlay.blocksRaycasts = false;

            RectTransform panel = Rect(overlayRect, "Pause Panel", Vector2.zero, new Vector2(1120, 760));
            Image panelImage = Component<Image>(panel.gameObject);
            panelImage.color = new Color(0.035f, 0.055f, 0.09f, 0.98f);
            panelImage.raycastTarget = false;
            TMP_Text title = Text(panel, "Title", "일시정지 · 보유 아티팩트", new Vector2(0, 330),
                new Vector2(1040, 52), 40, font, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            TMP_Text hint = Text(panel, "Resume Hint", "ESC · 게임으로 돌아가기", new Vector2(0, -338),
                new Vector2(1040, 32), 20, font, TextAlignmentOptions.Center);
            hint.color = new Color(0.72f, 0.8f, 0.88f, 1f);

            RectTransform viewport = Rect(panel, "Artifact Viewport", new Vector2(0, -4), new Vector2(1024, 600));
            Image viewportImage = Component<Image>(viewport.gameObject);
            viewportImage.color = new Color(0.02f, 0.03f, 0.05f, 0.75f);
            viewportImage.raycastTarget = true;
            Component<RectMask2D>(viewport.gameObject);

            RectTransform content = Rect(viewport, "Content", Vector2.zero, new Vector2(-24, 0));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0, -12);
            VerticalLayoutGroup layout = Component<VerticalLayoutGroup>(content.gameObject);
            layout.padding = new RectOffset(12, 12, 0, 12);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = Component<ContentSizeFitter>(content.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = Component<ScrollRect>(viewport.gameObject);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            RectTransform templateRect = Rect(content, "Artifact Detail Template", Vector2.zero, new Vector2(976, 104));
            LayoutElement templateLayout = Component<LayoutElement>(templateRect.gameObject);
            templateLayout.preferredHeight = 104f;
            Image rowImage = Component<Image>(templateRect.gameObject);
            rowImage.color = new Color(0.08f, 0.11f, 0.16f, 1f);
            rowImage.raycastTarget = false;

            RectTransform iconRect = Rect(templateRect, "Icon", new Vector2(-432, 0), new Vector2(72, 72));
            Image icon = Component<Image>(iconRect.gameObject);
            icon.color = new Color(0.36f, 0.4f, 0.46f, 1f);
            icon.raycastTarget = false;
            TMP_Text stableId = Text(iconRect, "Stable ID", "00", Vector2.zero, new Vector2(68, 40), 24, font,
                TextAlignmentOptions.Center);
            stableId.fontStyle = FontStyles.Bold;
            TMP_Text name = Text(templateRect, "Name", "아티팩트 이름", new Vector2(-294, 22),
                new Vector2(188, 32), 24, font, TextAlignmentOptions.Left);
            name.fontStyle = FontStyles.Bold;
            TMP_Text stack = Text(templateRect, "Stack", "×1", new Vector2(-174, 22),
                new Vector2(48, 32), 20, font, TextAlignmentOptions.Right);
            stack.color = new Color(1f, 0.83f, 0.3f, 1f);
            TMP_Text description = Text(templateRect, "Description", "아티팩트 효과 설명", new Vector2(90, -22),
                new Vector2(716, 36), 20, font, TextAlignmentOptions.Left);
            description.color = new Color(0.76f, 0.84f, 0.92f, 1f);
            description.textWrappingMode = TextWrappingModes.Normal;
            ArtifactPauseListEntryView entryTemplate = Component<ArtifactPauseListEntryView>(templateRect.gameObject);
            entryTemplate.ConfigureVisuals(icon, stableId, name, stack, description);
            templateRect.gameObject.SetActive(false);

            TMP_Text empty = Text(viewport, "Empty Message", "보유한 아티팩트가 없습니다.", Vector2.zero,
                new Vector2(800, 48), 24, font, TextAlignmentOptions.Center);
            empty.gameObject.SetActive(false);

            GamePauseArtifactView pauseView = Component<GamePauseArtifactView>(overlayRect.gameObject);
            pauseView.Configure(inventory, overlay, content, entryTemplate, empty);

            foreach (Component component in overlayRect.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-3C setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-3C setup complete. Escape pause and the complete artifact detail list are connected.");
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
            float fontSize, TMP_FontAsset font, TextAlignmentOptions alignment)
        {
            TMP_Text text = Component<TextMeshProUGUI>(Rect(parent, name, position, size).gameObject);
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.96f, 1f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
