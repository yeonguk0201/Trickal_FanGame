using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud5Setup
    {
        public const string PanelName = "Boss HUD";

        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-5 Boss Status")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-5 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameHudView gameHud = FindSingle<GameHudView>(scene, "GameHudView");
            RoomGraphController roomGraph = FindSingle<RoomGraphController>(scene, "room graph");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            Sprite fillSprite = Week13FrontendUiAssets.LoadPlaceholderFillSprite();
            RectTransform frame = gameHud.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("Game HUD ReferenceFrame is missing.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-5 Boss Status");

            RectTransform panel = Rect(frame, PanelName, new Vector2(0, -454), new Vector2(720, 64));
            CanvasGroup canvasGroup = Component<CanvasGroup>(panel.gameObject);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            Image panelImage = Component<Image>(panel.gameObject);
            panelImage.color = new Color(0.025f, 0.035f, 0.06f, 0.94f);
            panelImage.raycastTarget = false;

            TMP_Text bossName = Text(panel, "Boss Name", "보스", new Vector2(-250, 18),
                new Vector2(220, 28), 20, font, TextAlignmentOptions.Left);
            bossName.fontStyle = FontStyles.Bold;
            TMP_Text phase = Text(panel, "Phase", "페이즈 1 / 1", new Vector2(250, 18),
                new Vector2(220, 28), 18, font, TextAlignmentOptions.Right);

            RectTransform bar = Rect(panel, "Health Bar", new Vector2(0, -16), new Vector2(720, 32));
            Image barBackground = Component<Image>(bar.gameObject);
            barBackground.color = new Color(0.1f, 0.12f, 0.16f, 1f);
            barBackground.raycastTarget = false;
            RectTransform fillRect = Rect(bar, "Fill", Vector2.zero, new Vector2(708, 20));
            Image healthFill = Component<Image>(fillRect.gameObject);
            healthFill.sprite = fillSprite;
            healthFill.color = new Color(0.72f, 0.12f, 0.2f, 1f);
            healthFill.raycastTarget = false;
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = 0;
            healthFill.fillAmount = 1f;
            TMP_Text healthText = Text(bar, "Health Value", "10 / 10", Vector2.zero,
                new Vector2(680, 28), 18, font, TextAlignmentOptions.Center);
            healthText.fontStyle = FontStyles.Bold;

            GameBossHudView view = Component<GameBossHudView>(panel.gameObject);
            view.Configure(roomGraph, canvasGroup, bossName, phase, healthText, healthFill);
            EnsureGlyphs(font);

            foreach (Component component in panel.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-5 setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-5 setup complete. The lower-center boss name, live HP, and phase status are connected to the active generated boss room.");
        }

        private static void EnsureGlyphs(TMP_FontAsset font)
        {
            const string required = "보스페이즈검증0123456789/";
            if (!font.HasCharacters(required) && !font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing HUD-5 glyphs: " + missing);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<T>(true)).ToArray();
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
            float fontSize, TMP_FontAsset font, TextAlignmentOptions alignment)
        {
            TMP_Text text = Component<TextMeshProUGUI>(Rect(parent, name, position, size).gameObject);
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.94f, 0.96f, 1f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
