using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13Setting1CSetup
    {
        public const string HudPanelName = "Detailed Stats HUD";

        [MenuItem("Trickal Fan Game/Week 13/Setup Setting-1C Detailed Stats HUD")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Setting-1C setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ConfigureFrontend();
            ConfigureGameHud();
            AssetDatabase.SaveAssets();
            Debug.Log("Setting-1C setup completed. The saved toggle controls the unobtrusive detailed stats HUD.");
        }

        private static void ConfigureFrontend()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendSettingsView settings = FindSingle<FrontendSettingsView>(scene, "settings view");
            RectTransform panel = settings.GetComponent<RectTransform>();
            TMP_FontAsset font = RequireFont();

            Undo.RecordObject(panel, "Resize Settings Panel");
            panel.sizeDelta = new Vector2(720, 720);
            Layout(panel, "SettingsTitle", 310);
            Layout(panel, "VolumeSection", 245);
            Layout(panel, "MasterVolumeLabel", 195);
            Layout(panel, "MasterVolumeSlider", 195);
            Layout(panel, "MasterVolumeValue", 195);
            Layout(panel, "BgmVolumeLabel", 145);
            Layout(panel, "BgmVolumeSlider", 145);
            Layout(panel, "BgmVolumeValue", 145);
            Layout(panel, "SfxVolumeLabel", 95);
            Layout(panel, "SfxVolumeSlider", 95);
            Layout(panel, "SfxVolumeValue", 95);
            Layout(panel, "DisplaySection", 30);
            Layout(panel, "ResolutionLabel", -20);
            Layout(panel, "ResolutionDropdown", -20);
            Layout(panel, "FullscreenLabel", -75);
            Layout(panel, "FullscreenToggle", -75);
            Layout(panel, "SettingsBackButton", -290);

            TMP_Text section = Text(panel, "HudSection", "HUD 설정", new Vector2(-200, -140),
                new Vector2(280, 40), 28, font);
            TMP_Text label = Text(panel, "DetailedStatsHudLabel", "상세 스탯 표시", new Vector2(-200, -190),
                new Vector2(200, 32), 20, font);
            label.alignment = TextAlignmentOptions.Left;
            Toggle toggle = Toggle(panel, "DetailedStatsHudToggle", new Vector2(20, -190), new Vector2(40, 40));

            settings.Configure(settings.MasterVolumeSlider, settings.MasterVolumeLabel,
                settings.BgmVolumeSlider, settings.BgmVolumeLabel,
                settings.SfxVolumeSlider, settings.SfxVolumeLabel,
                settings.ResolutionDropdown, settings.FullscreenToggle, settings.BackButton, toggle);
            EnsureGlyphs(font, section.text + label.text);
            DirtyAndSave(scene, settings.gameObject);
        }

        private static void ConfigureGameHud()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerStats stats = FindSingle<PlayerStats>(scene, "player stats");
            Canvas canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Single(candidate => candidate.name == Week13Hud1Setup.HudRootName);
            RectTransform frame = canvas.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("Game HUD ReferenceFrame is missing.");
            TMP_FontAsset font = RequireFont();

            RectTransform panel = Rect(frame, HudPanelName, new Vector2(-776, 234), new Vector2(240, 216));
            Image background = Component<Image>(panel.gameObject);
            background.color = new Color(0.025f, 0.04f, 0.06f, 0.08f);
            background.raycastTarget = false;
            CanvasGroup group = Component<CanvasGroup>(panel.gameObject);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            string[] symbols = { "A", "AS", "M", "C" };
            Color[] colors =
            {
                new(0.95f, 0.38f, 0.42f, 0.55f),
                new(0.95f, 0.68f, 0.30f, 0.55f),
                new(0.34f, 0.78f, 0.70f, 0.55f),
                new(0.58f, 0.56f, 0.92f, 0.55f)
            };
            for (int i = 0; i < symbols.Length; i++)
            {
                float y = 72 - i * 48;
                RectTransform icon = Rect(panel, "Placeholder Icon " + symbols[i], new Vector2(-102, y), new Vector2(24, 24));
                Image iconImage = Component<Image>(icon.gameObject);
                iconImage.color = colors[i];
                iconImage.raycastTarget = false;
                TMP_Text glyph = Text(icon, "Glyph", symbols[i], Vector2.zero, new Vector2(24, 24),
                    symbols[i].Length == 1 ? 14 : 11, font);
                glyph.color = new Color(1f, 1f, 1f, 0.72f);
            }

            TMP_Text values = Text(panel, "Values", "공격력  1\n공격속도  1\n이동속도  5\n치명타율  5%",
                new Vector2(18, 0), new Vector2(188, 200), 20, font);
            values.alignment = TextAlignmentOptions.MidlineLeft;
            values.color = new Color(0.92f, 0.96f, 1f, 0.68f);
            values.textWrappingMode = TextWrappingModes.NoWrap;
            values.lineSpacing = 22f;

            GameDetailedStatsHudView view = Component<GameDetailedStatsHudView>(panel.gameObject);
            view.Configure(stats, group, values);
            group.alpha = 0f;
            EnsureGlyphs(font, "공격력속도이동치명타율0123456789.%ASM C");
            DirtyAndSave(scene, panel.gameObject);
        }

        private static void Layout(Transform parent, string name, float y)
        {
            RectTransform rect = parent.Find(name) as RectTransform;
            if (rect == null) throw new InvalidOperationException("Settings control is missing: " + name);
            Undo.RecordObject(rect, "Layout " + name);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        private static Toggle Toggle(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(parent, name, position, size);
            Toggle toggle = Component<Toggle>(rect.gameObject);
            RectTransform backgroundRect = Rect(rect, "Background", Vector2.zero, size);
            Image background = Component<Image>(backgroundRect.gameObject);
            background.color = new Color(0.15f, 0.2f, 0.25f);
            RectTransform checkRect = Rect(rect, "Checkmark", Vector2.zero, size * 0.6f);
            Image check = Component<Image>(checkRect.gameObject);
            check.color = new Color(0.18f, 0.68f, 0.64f);
            toggle.targetGraphic = background;
            toggle.graphic = check;
            return toggle;
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

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            RectTransform rect = existing as RectTransform;
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

        private static T Component<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(owner);
            else Undo.RecordObject(component, "Configure " + typeof(T).Name);
            return component;
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException($"{scene.path} requires one {label}; found {matches.Length}.");
            return matches[0];
        }

        private static TMP_FontAsset RequireFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            return font != null ? font : throw new InvalidOperationException("Frontend TMP font is missing.");
        }

        private static void EnsureGlyphs(TMP_FontAsset font, string required)
        {
            if (!font.HasCharacters(required) && !font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing Setting-1C glyphs: " + missing);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        private static void DirtyAndSave(Scene scene, GameObject root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true)) EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save " + scene.path);
        }
    }
}
