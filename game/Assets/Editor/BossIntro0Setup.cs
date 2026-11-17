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
    // Boss-Intro-0: the approved 1280×720 layout, mapped to the 1920×1080 HUD reference frame.
    public static class BossIntro0Setup
    {
        public const string OverlayName = "Boss Intro";
        public const string PrefabFolder = "Assets/Prefabs";
        public const string ArtworkFolder = "Assets/Resources/UserArtwork";
        public const string BossRibbonPath = ArtworkFolder + "/boss-intro-boss-ribbon.png";
        public const string PlayerRibbonPath = ArtworkFolder + "/boss-intro-player-ribbon.png";
        public const string VsPath = ArtworkFolder + "/boss-intro-vs.png";
        public const string BossNameMaterialPath = "Assets/Fonts/Boss Intro Boss Name.mat";
        public const string PlayerNameMaterialPath = "Assets/Fonts/Boss Intro Player Name.mat";
        private const float DraftToHud = 1.5f;
        private static readonly Vector2 DraftCenter = new(640f, 360f);
        private static readonly Vector2 BossPanelPosition = HudPoint(1039f, 494f);
        private static readonly Vector2 PlayerPanelPosition = HudPoint(252f, 279f);

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
            Sprite bossRibbonSprite = ImportArtwork(BossRibbonPath);
            Sprite playerRibbonSprite = ImportArtwork(PlayerRibbonPath);
            Sprite vsSprite = ImportArtwork(VsPath);
            Material bossNameMaterial = NameMaterial(font.material, BossNameMaterialPath, new Color32(97, 25, 48, 255));
            Material playerNameMaterial = NameMaterial(font.material, PlayerNameMaterialPath, new Color32(22, 56, 75, 255));

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
            // The pause menu stays on top (HUD-3C also puts it last), so re-running either Setup keeps one order.
            Transform pause = frame.Find(Week13Hud3CSetup.OverlayName);
            if (pause != null) overlay.SetSiblingIndex(pause.GetSiblingIndex());
            CanvasGroup canvasGroup = Component<CanvasGroup>(overlay.gameObject);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            RemoveChild(overlay, "Dim");
            RectTransform shakeRoot = Rect(overlay, "Shake Root", Vector2.zero, FrontendLayout.ReferenceSize);
            RemoveChild(shakeRoot, "VS");

            RectTransform bossPanel = Rect(shakeRoot, "Boss Panel", BossPanelPosition, new Vector2(1000f, 460f));
            RemoveChild(bossPanel, "Band Edge");
            Artwork(bossPanel, "Band", Vector2.zero, 820f, 191f, -9f, bossRibbonSprite, true);
            Image bossPortrait = Artwork(bossPanel, "Portrait", HudPoint(1057f, 394f) - BossPanelPosition,
                347f, 331f, -5f, preserveAspect: true);
            TMP_Text bossName = NameText(bossPanel, "보스", HudPoint(1059f, 570f) - BossPanelPosition,
                540f, 126f, 78f, -9f, font, bossNameMaterial, new Color32(255, 246, 233, 255));

            RectTransform playerPanel = Rect(shakeRoot, "Player Panel", PlayerPanelPosition, new Vector2(1000f, 460f));
            RemoveChild(playerPanel, "Band Edge");
            Artwork(playerPanel, "Band", Vector2.zero, 820f, 191f, -4f, playerRibbonSprite);
            Image playerPortrait = Artwork(playerPanel, "Portrait", HudPoint(246f, 192f) - PlayerPanelPosition,
                340f, 325f, 4f, mirrored: true, preserveAspect: true);
            TMP_Text playerName = NameText(playerPanel, "플레이어", HudPoint(244f, 367f) - PlayerPanelPosition,
                447f, 105f, 85f, -6f, font, playerNameMaterial, new Color32(255, 244, 209, 255));

            Image vs = Artwork(shakeRoot, "VS Artwork", HudPoint(664f, 347f), 234f, 206f, -12f, vsSprite,
                preserveAspect: true);
            vs.transform.SetAsLastSibling();

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
            new string(("보스플레이어" + string.Concat(LoadBossNames()) +
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

        private static Sprite ImportArtwork(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new InvalidOperationException("Missing boss intro artwork: " + path);
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single || !importer.alphaIsTransparency ||
                importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path)
                ?? throw new InvalidOperationException("Cannot load boss intro sprite: " + path);
        }

        private static Material NameMaterial(Material source, string path, Color outline)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(source);
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }
            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.27f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
            ShaderUtilities.UpdateShaderRatios(material);
            if (created) AssetDatabase.CreateAsset(material, path);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Vector2 HudPoint(float x, float y) =>
            new Vector2(x - DraftCenter.x, DraftCenter.y - y) * DraftToHud;

        private static void RemoveChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
        }

        private static RectTransform ArtworkRect(Transform parent, string name, Vector2 position,
            float width, float height, float angle)
        {
            RectTransform rect = Rect(parent, name, position, new Vector2(width, height) * DraftToHud);
            rect.localRotation = Quaternion.Euler(0f, 0f, -angle);
            return rect;
        }

        private static Image Artwork(Transform parent, string name, Vector2 position, float width, float height,
            float angle, Sprite sprite = null, bool mirrored = false, bool preserveAspect = false)
        {
            RectTransform rect = ArtworkRect(parent, name, position, width, height, angle);
            if (mirrored) rect.localScale = new Vector3(-1f, 1f, 1f);
            Image image = Component<Image>(rect.gameObject);
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            if (name == "Band") rect.SetAsFirstSibling();
            return image;
        }

        private static TMP_Text NameText(Transform parent, string value, Vector2 position, float width, float height,
            float fontSize, float angle, TMP_FontAsset font, Material material, Color color)
        {
            TMP_Text text = Component<TextMeshProUGUI>(
                ArtworkRect(parent, "Name", position, width, height, angle).gameObject);
            text.font = font;
            text.fontSharedMaterial = material;
            text.text = value;
            text.fontSize = fontSize * DraftToHud;
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
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
    }
}
