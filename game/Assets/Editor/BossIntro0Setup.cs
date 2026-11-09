using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Character;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    // Boss-Intro-0: the boss intro overlay on the Game HUD. Boss on the upper left and the player on the lower right
    // on slanted red bands, "VS" in the middle. Rerunning reuses the same objects and the same VS material.
    public static class BossIntro0Setup
    {
        public const string OverlayName = "Boss Intro";
        public const string VsMaterialPath = "Assets/Fonts/Boss Intro VS.mat";
        public const string PrefabFolder = "Assets/Prefabs";
        public const float BandAngle = 7f;
        public const float VsAngle = -8f;
        public static readonly Vector2 BossPanelPosition = new(-380f, 170f);
        public static readonly Vector2 PlayerPanelPosition = new(380f, -190f);
        public static readonly Color BandColor = new(0.86f, 0.1f, 0.12f, 1f);
        public static readonly Color VsColor = new(1f, 0.86f, 0.08f, 1f);
        public static readonly Color VsOutlineColor = new(0.32f, 0.06f, 0.04f, 1f);
        public const float VsOutlineWidth = 0.3f;

        [MenuItem("Trickal Fan Game/Beta/Setup Boss-Intro-0 Boss Intro")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Boss-Intro-0 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null || font.material == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            CharacterDefinition[] characters = LoadCharacters();
            EnsureGlyphs(font, characters);
            Material vsMaterial = VsMaterial(font.material);

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameBossHudView bossHud = FindSingle<GameBossHudView>(scene, "boss HUD (run HUD-5 setup first)");
            RoomGraphController roomGraph = FindSingle<RoomGraphController>(scene, "room graph");
            RunSession runSession = FindSingle<RunSession>(scene, "run session");
            RectTransform frame = bossHud.transform.parent as RectTransform;
            if (frame == null || frame.name != "ReferenceFrame")
                throw new InvalidOperationException("The boss HUD must sit under the Game HUD ReferenceFrame.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Boss-Intro-0 Boss Intro");

            RectTransform overlay = Rect(frame, OverlayName, Vector2.zero, FrontendLayout.ReferenceSize);
            Undo.SetTransformParent(overlay, frame, "Order " + OverlayName);
            overlay.SetAsLastSibling();
            CanvasGroup canvasGroup = Component<CanvasGroup>(overlay.gameObject);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Solid(Rect(overlay, "Dim", Vector2.zero, FrontendLayout.ReferenceSize), new Color(0f, 0f, 0f, 0.55f));
            RectTransform shakeRoot = Rect(overlay, "Shake Root", Vector2.zero, FrontendLayout.ReferenceSize);

            RectTransform bossPanel = Rect(shakeRoot, "Boss Panel", BossPanelPosition, new Vector2(1000f, 460f));
            Band(bossPanel, new Vector2(-500f, -120f));
            Image bossPortrait = Portrait(bossPanel, new Vector2(-150f, 70f));
            TMP_Text bossName = BandText(bossPanel, "보스", new Vector2(-200f, -83f), font);

            RectTransform playerPanel = Rect(shakeRoot, "Player Panel", PlayerPanelPosition, new Vector2(1000f, 460f));
            Band(playerPanel, new Vector2(500f, -120f));
            Image playerPortrait = Portrait(playerPanel, new Vector2(150f, 70f));
            TMP_Text playerName = BandText(playerPanel, "플레이어", new Vector2(200f, -157f), font);

            TMP_Text vs = Text(shakeRoot, "VS", "VS", new Vector2(0f, -10f), new Vector2(520f, 320f), 240f, font,
                TextAlignmentOptions.Center);
            vs.color = VsColor;
            vs.fontSharedMaterial = vsMaterial;
            vs.transform.localRotation = Quaternion.Euler(0f, 0f, VsAngle);

            Image flash = Solid(Rect(overlay, "Flash", Vector2.zero, FrontendLayout.ReferenceSize),
                new Color(1f, 1f, 1f, 0f));

            GameBossIntroView view = Component<GameBossIntroView>(overlay.gameObject);
            view.Configure(roomGraph, bossHud, runSession, characters, canvasGroup, shakeRoot, bossPanel,
                bossPortrait, bossName, playerPanel, playerPortrait, playerName, vs, flash);

            foreach (Component component in overlay.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Boss-Intro-0 setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Boss-Intro-0 setup complete. Entering a boss room stops time, slides in the boss and player " +
                      "panels, slams VS and fills the boss HP bar before combat starts.");
        }

        public static CharacterDefinition[] LoadCharacters()
        {
            CharacterDefinition[] characters = AssetDatabase
                .FindAssets("t:CharacterDefinition", new[] { Week13FrontendSetup.CharacterFolder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterDefinition>).Where(character => character != null)
                .ToArray();
            if (characters.Length == 0) throw new InvalidOperationException("No CharacterDefinition assets found.");
            return characters;
        }

        public static string[] LoadBossNames() => AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Select(prefab => prefab != null ? prefab.GetComponent<BossController>() : null)
            .Where(boss => boss != null).Select(boss => boss.DisplayName).Distinct().ToArray();

        public static string RequiredGlyphs(CharacterDefinition[] characters) =>
            new string(("VS보스플레이어" + string.Concat(LoadBossNames()) +
                        string.Concat(characters.Select(character => character.DisplayName)))
                .Where(c => !char.IsWhiteSpace(c)).Distinct().ToArray());

        private static void EnsureGlyphs(TMP_FontAsset font, CharacterDefinition[] characters)
        {
            string required = RequiredGlyphs(characters);
            if (font.HasCharacters(required)) return;
            if (!font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing Boss-Intro-0 glyphs: " + missing);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        // The TMP SDF shader draws an outline only with OUTLINE_ON on a saved material (see Number0Setup).
        private static Material VsMaterial(Material source)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(VsMaterialPath);
            bool created = material == null;
            if (created) material = new Material(source);
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(VsMaterialPath);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, VsOutlineWidth);
            material.SetColor(ShaderUtilities.ID_OutlineColor, VsOutlineColor);
            ShaderUtilities.UpdateShaderRatios(material);
            if (created) AssetDatabase.CreateAsset(material, VsMaterialPath);
            EditorUtility.SetDirty(material);
            return material;
        }

        // A white edge behind the red band, like the sketch's outlined strokes. Long enough to run off the screen side.
        private static void Band(RectTransform panel, Vector2 position)
        {
            RectTransform edge = Rect(panel, "Band Edge", position, new Vector2(2200f, 186f));
            edge.localRotation = Quaternion.Euler(0f, 0f, BandAngle);
            Solid(edge, Color.white);
            RectTransform band = Rect(panel, "Band", position, new Vector2(2200f, 160f));
            band.localRotation = Quaternion.Euler(0f, 0f, BandAngle);
            Solid(band, BandColor);
            edge.SetSiblingIndex(0);
            band.SetSiblingIndex(1);
        }

        // On the band's slope: the band centre is 300 units sideways, so the name sits tan(7°)·300 ≈ 37 up or down.
        private static TMP_Text BandText(RectTransform panel, string value, Vector2 position, TMP_FontAsset font)
        {
            TMP_Text text = Text(panel, "Name", value, position, new Vector2(640f, 110f), 72f, font,
                TextAlignmentOptions.Center);
            text.transform.localRotation = Quaternion.Euler(0f, 0f, BandAngle);
            return text;
        }

        private static Image Portrait(RectTransform panel, Vector2 position)
        {
            Image image = Component<Image>(Rect(panel, "Portrait", position, new Vector2(420f, 420f)).gameObject);
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Image Solid(RectTransform rect, Color color)
        {
            Image image = Component<Image>(rect.gameObject);
            image.sprite = null;
            image.color = color;
            image.raycastTarget = false;
            return image;
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
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size,
            float fontSize, TMP_FontAsset font, TextAlignmentOptions alignment)
        {
            TMP_Text text = Component<TextMeshProUGUI>(Rect(parent, name, position, size).gameObject);
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
