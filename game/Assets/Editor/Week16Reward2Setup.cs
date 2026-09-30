using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week16Reward2Setup
    {
        public const string OverlayName = "Reward Selection Overlay";
        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        public static readonly Vector2 PanelSize = new(1560f, 760f);
        public static readonly Vector2 CardSize = new(420f, 520f);

        [MenuItem("Trickal Fan Game/Week 16/Setup Reward-2 Placeholder Cards")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Reward-2 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameHudView hud = FindSingle<GameHudView>(scene, "Game HUD");
            PlayerInventory inventory = FindSingle<PlayerInventory>(scene, "player inventory");
            Health health = inventory.GetComponent<Health>();
            PlayerActionState actionState = inventory.GetComponent<PlayerActionState>();
            RunProgress progress = FindSingle<RunProgress>(scene, "Run progress");
            EventSystem eventSystem = FindSingle<EventSystem>(scene, "EventSystem");
            Canvas canvas = hud.GetComponent<Canvas>();
            GraphicRaycaster raycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
            RectTransform frame = hud.transform.Find("ReferenceFrame") as RectTransform;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (health == null || actionState == null || canvas == null || raycaster == null || frame == null ||
                font == null || eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                throw new InvalidOperationException(
                    "Reward-2 requires the configured Game HUD, player, TMP font, and Input System EventSystem.");
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Reward-2 Placeholder Cards");

            RectTransform overlayRect = Rect(frame, OverlayName, Vector2.zero, ReferenceResolution);
            overlayRect.SetAsLastSibling();
            Image dimmer = Component<Image>(overlayRect.gameObject);
            dimmer.color = new Color(0.015f, 0.025f, 0.045f, 0.92f);
            dimmer.raycastTarget = false;
            CanvasGroup overlay = Component<CanvasGroup>(overlayRect.gameObject);

            RectTransform panel = Rect(overlayRect, "Reward Panel", new Vector2(0f, -8f), PanelSize);
            Image panelImage = Component<Image>(panel.gameObject);
            panelImage.color = new Color(0.055f, 0.075f, 0.12f, 1f);
            panelImage.raycastTarget = false;
            Outline panelOutline = Component<Outline>(panel.gameObject);
            panelOutline.effectColor = new Color(0.45f, 0.75f, 0.82f, 0.9f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            TMP_Text title = Text(panel, "Title", "보상을 하나 선택하세요", new Vector2(0f, 326f),
                new Vector2(1120f, 58f), 38f, font, TextAlignmentOptions.Center, false);
            title.fontStyle = FontStyles.Bold;
            TMP_Text subtitle = Text(panel, "Subtitle", "아티팩트와 스펠은 같은 후보 목록에서 등장합니다.",
                new Vector2(0f, 278f), new Vector2(1120f, 38f), 21f, font,
                TextAlignmentOptions.Center, false);
            subtitle.color = new Color(0.7f, 0.78f, 0.86f, 1f);

            ItemRewardCardView[] cards = new ItemRewardCardView[ArtifactRewardSelector.MaximumCandidateCount];
            float[] cardPositions = { -480f, 0f, 480f };
            for (int index = 0; index < cards.Length; index++)
                cards[index] = SetupCard(panel, index, cardPositions[index], font);

            Button confirmButton = SetupActionButton(panel, "Confirm Selection", "선택",
                new Vector2(-150f, -306f), new Color(0.12f, 0.48f, 0.56f, 1f), font);
            Button cancelButton = SetupActionButton(panel, "Cancel Selection", "취소",
                new Vector2(150f, -306f), new Color(0.28f, 0.32f, 0.4f, 1f), font);

            TMP_Text hint = Text(panel, "Input Hint", "후보를 고른 뒤 선택하거나 취소해 나중에 다시 확인할 수 있습니다.",
                new Vector2(0f, -352f), new Vector2(1100f, 36f), 19f, font,
                TextAlignmentOptions.Center, false);
            hint.color = new Color(0.72f, 0.82f, 0.9f, 1f);

            ItemRewardSelectionSession session = Component<ItemRewardSelectionSession>(overlayRect.gameObject);
            session.Configure(progress, inventory, health, actionState);
            ItemRewardSelectionView view = Component<ItemRewardSelectionView>(overlayRect.gameObject);
            view.Configure(session, health, overlay, panel, raycaster, eventSystem, title, hint, cards,
                confirmButton, cancelButton);
            EnsureGlyphs(font);

            foreach (Component component in overlayRect.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(font);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Reward-2 setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 16 Reward-2 setup complete. Three replaceable placeholder cards, mouse/keyboard " +
                      "selection, focus markers, and Item/healing detail fields are connected.");
        }

        private static ItemRewardCardView SetupCard(Transform parent, int index, float x, TMP_FontAsset font)
        {
            RectTransform cardRect = Rect(parent, $"Reward Card {index + 1}", new Vector2(x, -16f), CardSize);
            Image background = Component<Image>(cardRect.gameObject);
            background.color = new Color(0.09f, 0.125f, 0.18f, 1f);
            background.raycastTarget = true;
            Button button = Component<Button>(cardRect.gameObject);
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.selectedColor = new Color(0.75f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.62f, 0.82f, 0.88f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
            button.colors = colors;
            Outline outline = Component<Outline>(cardRect.gameObject);
            outline.effectColor = new Color(0.55f, 0.88f, 0.94f, 0.95f);
            outline.effectDistance = new Vector2(3f, -3f);

            TMP_Text state = Text(cardRect, "State", "[선택 가능]", new Vector2(0f, 226f),
                new Vector2(360f, 36f), 21f, font, TextAlignmentOptions.Center, false);
            state.fontStyle = FontStyles.Bold;
            state.color = new Color(0.65f, 0.92f, 0.95f, 1f);
            TMP_Text kind = Text(cardRect, "Kind", "아티팩트", new Vector2(0f, 182f),
                new Vector2(360f, 34f), 20f, font, TextAlignmentOptions.Center, false);
            kind.color = new Color(0.75f, 0.82f, 0.9f, 1f);
            TMP_Text name = Text(cardRect, "Name", "임시 보상 이름", new Vector2(0f, 126f),
                new Vector2(360f, 70f), 31f, font, TextAlignmentOptions.Center, true);
            name.fontStyle = FontStyles.Bold;
            TMP_Text rarity = Text(cardRect, "Rarity", "일반 / COMMON", new Vector2(0f, 74f),
                new Vector2(360f, 30f), 19f, font, TextAlignmentOptions.Center, false);
            rarity.color = new Color(0.92f, 0.78f, 0.38f, 1f);

            RectTransform divider = Rect(cardRect, "Divider", new Vector2(0f, 46f), new Vector2(340f, 2f));
            Image dividerImage = Component<Image>(divider.gameObject);
            dividerImage.color = new Color(0.35f, 0.48f, 0.58f, 0.8f);
            dividerImage.raycastTarget = false;

            TMP_Text effect = Text(cardRect, "Effect", "효과 설명이 이 영역에 표시됩니다.",
                new Vector2(0f, -42f), new Vector2(348f, 150f), 23f, font,
                TextAlignmentOptions.TopLeft, true);
            effect.lineSpacing = 8f;
            TMP_Text stack = Text(cardRect, "Stack Or Health", "현재 스택 0 / 1",
                new Vector2(0f, -148f), new Vector2(348f, 64f), 20f, font,
                TextAlignmentOptions.Center, true);
            stack.color = new Color(0.78f, 0.86f, 0.92f, 1f);

            RectTransform action = Rect(cardRect, "Action", new Vector2(0f, -216f), new Vector2(344f, 48f));
            Image actionImage = Component<Image>(action.gameObject);
            actionImage.color = new Color(0.12f, 0.4f, 0.48f, 0.95f);
            actionImage.raycastTarget = false;
            TMP_Text actionText = Text(action, "Label", "선택", Vector2.zero, new Vector2(320f, 40f),
                23f, font, TextAlignmentOptions.Center, false);
            actionText.fontStyle = FontStyles.Bold;

            ItemRewardCardView card = Component<ItemRewardCardView>(cardRect.gameObject);
            card.ConfigureVisuals(button, state, kind, name, rarity, effect, stack);
            return card;
        }

        private static Button SetupActionButton(Transform parent, string name, string label,
            Vector2 position, Color color, TMP_FontAsset font)
        {
            RectTransform rect = Rect(parent, name, position, new Vector2(260f, 58f));
            Image image = Component<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = true;
            Button button = Component<Button>(rect.gameObject);
            button.targetGraphic = image;
            TMP_Text text = Text(rect, "Label", label, Vector2.zero, new Vector2(230f, 48f), 23f,
                font, TextAlignmentOptions.Center, false);
            text.fontStyle = FontStyles.Bold;
            return button;
        }

        private static void EnsureGlyphs(TMP_FontAsset font)
        {
            const string required = "보상을하나선택하세요아티팩트와스펠은같은후보목록에서등장합니다후보를고른뒤선택하거나취소해나중에다시확인할수있습니다상호작용임시이름효과설명이영역에표시됩니다현재스택제한없음회복응급대체최대즉시합니다획득완료종료가능중일반고급희귀전설현재0123456789%/HPENTERSPACECOMMONUNCOMMONRAREEPIC[]E";
            if (!font.HasCharacters(required) && !font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing Reward-2 glyphs: " + missing);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }

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
            float fontSize, TMP_FontAsset font, TextAlignmentOptions alignment, bool wrap)
        {
            TMP_Text text = Component<TextMeshProUGUI>(Rect(parent, name, position, size).gameObject);
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.96f, 1f);
            text.raycastTarget = false;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
