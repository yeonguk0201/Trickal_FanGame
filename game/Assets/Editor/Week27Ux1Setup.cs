using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    // UX-1: the HUD layout. Top left holds the 권능, then the hearts and SP with gold/key/bomb below them; the
    // right column holds the minimap (current room centred in a masked viewport) and the artifacts. It also adds
    // the panel that describes the nearest floor item before it is collected. HUD-1, HUD-3A, HUD-4A, Setting-1C
    // and Power-1 read their positions from here, so re-running any of them keeps this layout. Re-running updates
    // the same objects instead of adding more.
    public static class Week27Ux1Setup
    {
        public const string ResourcePanelName = "Resource HUD";
        public const string DescriptionPanelName = "Pickup Description";
        public const string MapContentName = "Content";
        public const string GoldSpritePath = "Assets/Resources/UserArtwork/gold-pickup.png";
        public const string KeySpritePath = "Assets/Art/Drafts/FairyKingdom/Pickups/pickup-key.png";
        public const string BombSpritePath = "Assets/Art/Drafts/FairyKingdom/Pickups/pickup-bomb.png";
        // Elif-2: the permanent elif the Run can spend, as the fourth cell.
        public const string ElifSpritePath = "Assets/Resources/UserArtwork/permanent-elif.png";
        public static readonly string[] ResourceSpritePaths =
            { GoldSpritePath, KeySpritePath, BombSpritePath, ElifSpritePath };
        public const float MinimapMaskSoftness = 14f;
        public const float DescriptionWidth = 420f;

        // Top-left, in the 1920 x 1080 reference frame (centre origin): the 권능 in the corner, hearts and SP to its
        // right, and gold/key/bomb/elif under the hearts and SP (96 per cell, left edge kept where the three-cell row
        // started). The detailed stats sit lower on the left.
        public static readonly Vector2 SurvivalPosition = new(-432, 420);
        public static readonly Vector2 SurvivalSize = new(560, 168);
        public static readonly Vector2 HeartRowPosition = new(0, 42);
        public static readonly Vector2 HeartRowSize = new(532, 52);
        public const float HeartMaximumSize = 48f;
        public const float SpSlotSize = 48f;
        public static readonly Vector2 SpLabelPosition = new(-248, -34);
        public static readonly Vector2 SpSlotsPosition = new(-86, -34);
        public static readonly Vector2 SpSlotsSize = new(272, 48);
        public static readonly Vector2 SpValuePosition = new(196, -34);
        public static readonly Vector2 PowerPosition = new(-830, 420);
        public static readonly Vector2 PowerSize = new(220, 168);
        public static readonly Vector2 ResourcePosition = new(-520, 296);
        public static readonly Vector2 ResourceSize = new(384, 56);
        public static readonly Vector2 DetailedStatsPosition = new(-780, 140);

        // Skill HUD (bottom right): two equal rounded tiles, centre to centre.
        public const float SkillIconSize = 104f;
        public const float SkillIconSpacing = 124f;
        public static readonly Color SkillTileColor = new(0.08f, 0.1f, 0.15f, 1f);

        // Right column, top to bottom.
        public static readonly Vector2 MinimapPosition = new(796, 412);
        public static readonly Vector2 MinimapSize = new(288, 216);
        public static readonly Vector2 MinimapHeaderPosition = new(0, 94);
        public static readonly Vector2 MinimapHeaderSize = new(264, 20);
        public static readonly Vector2 MinimapViewportPosition = new(0, -10);
        public static readonly Vector2 MinimapViewportSize = new(264, 184);
        public static readonly Vector2 ArtifactPosition = new(796, 236);
        public static readonly Vector2 ArtifactSize = new(288, 112);

        private static readonly Color PanelColor = new(0.035f, 0.055f, 0.09f, 0.88f);

        [MenuItem("Trickal Fan Game/Week 27/Setup UX-1 Right HUD, Minimap and Pickup Description")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before UX-1 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // HUD-1 and HUD-2 only build the Game HUD, so they are re-run to take the larger hearts and SP and the
            // skill overlay colour. Setting-1C also lays out the Frontend settings, so only its panel is moved here.
            Week13Hud1Setup.Setup();
            Week13Hud2Setup.Setup();

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("UX-1 requires the Frontend TMP font.");
            Sprite[] resourceSprites = ResourceSpritePaths.Select(LoadSprite).ToArray();

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup UX-1 Right HUD");

            Week27Power1Setup.EnsureGlyphs(font, AllDescriptionText());
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameHudView gameHud = FindSingle<GameHudView>(scene, "GameHudView");
            RunProgress progress = FindSingle<RunProgress>(scene, "RunProgress");
            PlayerMovement player = FindSingle<PlayerMovement>(scene, "player");
            GameMinimapView minimap = FindSingle<GameMinimapView>(scene, "GameMinimapView");
            GameArtifactHudView artifacts = FindSingle<GameArtifactHudView>(scene, "GameArtifactHudView");
            RectTransform frame = gameHud.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("Game HUD ReferenceFrame is missing.");

            ConfigureMinimap(minimap);
            PlayerPowerSlot powerSlot = player.GetComponent<PlayerPowerSlot>();
            if (powerSlot == null) throw new InvalidOperationException("Run Power-1 setup before UX-1 setup.");
            Week27Power1Setup.BuildHud(frame, powerSlot, font);
            RectTransform stats = frame.Find(Week13Setting1CSetup.HudPanelName) as RectTransform;
            if (stats == null) throw new InvalidOperationException("Run Setting-1C setup before UX-1 setup.");
            Place(stats, DetailedStatsPosition, stats.sizeDelta);
            Place((RectTransform)artifacts.transform, ArtifactPosition, ArtifactSize);
            RectTransform resources = BuildResourceHud(frame, progress, font, resourceSprites);
            RectTransform description = BuildDescription(frame, player.transform, font);

            // New panels sit right after the minimap so the pause, settings and GAME OVER layers stay on top.
            int index = minimap.transform.GetSiblingIndex();
            resources.SetSiblingIndex(index + 1);
            description.SetSiblingIndex(index + 2);

            foreach (Component component in frame.GetComponentsInChildren<Component>(true))
                if (component != null) EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during UX-1 setup.");
            Undo.CollapseUndoOperations(group);
            Debug.Log("UX-1 setup complete: right HUD column (minimap, gold/key/bomb/elif, artifacts), current-room " +
                      "minimap viewport and the nearest floor item description.");
        }

        // Every item name and effect line the description panel can show, plus its fixed words.
        public static string AllDescriptionText()
        {
            ItemDefinition[] items = AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null).ToArray();
            return PickupDescriptionView.AllFixedText + GameResourceHudView.AllFixedText +
                   string.Concat(items.Select(item => item.DisplayName + ArtifactEffectDescription.Build(item) +
                                                     PickupDescriptionView.DetailLine(item)));
        }

        // Shared by HUD-4A so a later HUD-4A run keeps the UX-1 size and position.
        public static void ApplyMinimapLayout(RectTransform panel)
        {
            Place(panel, MinimapPosition, MinimapSize);
            if (panel.Find("Header") is RectTransform header) Place(header, MinimapHeaderPosition, MinimapHeaderSize);
            if (panel.Find("Map") is RectTransform map) Place(map, MinimapViewportPosition, MinimapViewportSize);
        }

        private static void ConfigureMinimap(GameMinimapView minimap)
        {
            RectTransform panel = (RectTransform)minimap.transform;
            ApplyMinimapLayout(panel);
            RectTransform map = minimap.MapRoot;
            if (map == null || map.parent != panel) throw new InvalidOperationException("Minimap Map root is missing.");
            RectMask2D mask = Component<RectMask2D>(map.gameObject);
            mask.softness = Vector2Int.one * Mathf.RoundToInt(MinimapMaskSoftness);
            RectTransform content = Rect(map, MapContentName, Vector2.zero, Vector2.zero);
            content.SetAsLastSibling();
            Undo.RecordObject(minimap, "Configure UX-1 minimap");
            minimap.ConfigureViewport(content, GameMinimapView.DefaultFollowSharpness);
        }

        public static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("UX-1 requires the pickup artwork: " + path);
            return sprite;
        }

        // Gold, key, bomb, elif in GameResourceHudView.DisplayOrder, each with its floor pickup artwork.
        private static RectTransform BuildResourceHud(RectTransform frame, RunProgress progress, TMP_FontAsset font,
            Sprite[] sprites)
        {
            RectTransform panel = Rect(frame, ResourcePanelName, ResourcePosition, ResourceSize);
            Image background = Component<Image>(panel.gameObject);
            background.color = PanelColor;
            background.raycastTarget = false;

            int count = GameResourceHudView.DisplayOrder.Length;
            Image[] icons = new Image[count];
            TMP_Text[] texts = new TMP_Text[count];
            float cellWidth = ResourceSize.x / count;
            for (int i = 0; i < count; i++)
            {
                RunResourceType type = GameResourceHudView.DisplayOrder[i];
                RectTransform cell = Rect(panel, type.ToString(), new Vector2((i - (count - 1) * 0.5f) * cellWidth, 0),
                    new Vector2(cellWidth, ResourceSize.y));
                icons[i] = Component<Image>(Rect(cell, "Icon", new Vector2(-22, 0), new Vector2(34, 34)).gameObject);
                icons[i].raycastTarget = false;
                icons[i].preserveAspect = true;
                icons[i].sprite = sprites[i];
                icons[i].color = Color.white;
                // Earlier UX-1 builds drew keys and bombs as a lettered circle.
                Outline outline = icons[i].GetComponent<Outline>();
                if (outline != null) Undo.DestroyObjectImmediate(outline);
                Transform letter = icons[i].transform.Find("Letter");
                if (letter != null) Undo.DestroyObjectImmediate(letter.gameObject);

                texts[i] = Text(cell, "Count", "00", new Vector2(20, 0), new Vector2(46, 34), 26, font);
                texts[i].fontStyle = FontStyles.Bold;
                texts[i].alignment = TextAlignmentOptions.Left;
            }

            GameResourceHudView view = Component<GameResourceHudView>(panel.gameObject);
            view.Configure(progress, icons, texts);
            return panel;
        }

        private static RectTransform BuildDescription(RectTransform frame, Transform player, TMP_FontAsset font)
        {
            RectTransform panel = Rect(frame, DescriptionPanelName, Vector2.zero, new Vector2(DescriptionWidth, 140));
            panel.pivot = new Vector2(0.5f, 1f);
            Image background = Component<Image>(panel.gameObject);
            background.color = new Color(0.03f, 0.045f, 0.075f, 0.94f);
            background.raycastTarget = false;
            CanvasGroup group = Component<CanvasGroup>(panel.gameObject);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            const float inner = DescriptionWidth - 28f;
            TMP_Text kind = TopText(panel, "Kind", -12, new Vector2(inner, 22), 16, font, TextAlignmentOptions.Left);
            kind.color = new Color(0.72f, 0.78f, 0.86f);
            TMP_Text rarity = TopText(panel, "Rarity", -12, new Vector2(inner, 22), 16, font, TextAlignmentOptions.Right);
            TMP_Text itemName = TopText(panel, "Name", -36, new Vector2(inner, 32), 26, font, TextAlignmentOptions.Left);
            itemName.fontStyle = FontStyles.Bold;
            itemName.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text effect = TopText(panel, "Effect", -74, new Vector2(inner, 24), 19, font, TextAlignmentOptions.TopLeft);
            effect.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text detail = TopText(panel, "Detail", -104, new Vector2(inner, 22), 16, font, TextAlignmentOptions.Left);
            detail.color = new Color(1f, 0.86f, 0.5f);

            PickupDescriptionView view = Component<PickupDescriptionView>(panel.gameObject);
            view.Configure(player, frame, kind, rarity, itemName, effect, detail);
            return panel;
        }

        // Text anchored to the panel's top edge, so the panel can grow downward with the effect text.
        private static TMP_Text TopText(RectTransform parent, string name, float y, Vector2 size, float fontSize,
            TMP_FontAsset font, TextAlignmentOptions alignment)
        {
            TMP_Text text = Text(parent, name, string.Empty, new Vector2(0, y), size, fontSize, font);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, y);
            text.alignment = alignment;
            return text;
        }

        internal static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
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

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            Undo.RecordObject(rect, "Layout " + rect.name);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
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
            Place(rect, position, size);
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
