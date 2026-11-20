using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static TrickalFanGame.Editor.MenuUiBuilder;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // docs/ingame-presentation-asset-guide.md: the first-entry banner, the reward altar, the 권능 slot art, the stat
    // icons and the ESC pause window in the magic-library look. Only appearance changes; every runtime reference and
    // behaviour stays. Rerunning finds the same objects by name, so nothing is duplicated. HUD-3C/7B/7C/7D, UX-0 and
    // UX-1 call back into this (ReapplyPause, Week27Power1Setup.BuildHud), so rerunning them keeps this look.
    public static class InGamePresentationSetup
    {
        public const string Folder = "Assets/Art/UI/InGamePresentation";
        public const string MapIntroBanner = "map-intro-banner";
        public const string RewardAltar = "reward-stone-altar";
        public const string RewardGift = "reward-floating-gift";
        public const string PowerEmptySlot = "power-empty-slot";
        public const string PowerGaugeTrack = "power-gauge-track";
        public const string PowerGaugeFill = "power-gauge-fill";
        public const string StatAttackSpeed = "stat-attack-speed";
        public const string StatMoveSpeed = "stat-move-speed";
        public const string StatCriticalDamage = "stat-critical-damage";
        public const string StatProjectile = "stat-projectile";
        public const string StatPierce = "stat-pierce";
        public const string PauseTitlePlaque = "pause-title-plaque";
        public const string PauseSeedPlate = "pause-seed-plate";

        public static readonly string[] All =
        {
            MapIntroBanner, RewardAltar, RewardGift, PowerEmptySlot, PowerGaugeTrack, PowerGaugeFill, StatAttackSpeed,
            StatMoveSpeed, StatCriticalDamage, StatProjectile, StatPierce, PauseTitlePlaque, PauseSeedPlate
        };

        // First-entry banner (1536 x 320 art). The plaque between the wings is about 77% of the width; the name
        // uses the middle 70% of that and shrinks for long names so it never reaches the wings.
        public static readonly Vector2 BannerPosition = new(0, 230);
        public static readonly Vector2 BannerSize = new(768, 160);
        public static readonly Vector2 BannerTitleSize = new(416, 72);
        // TMP centres Korean glyphs a little high; this puts the name on the middle of the plaque body.
        public static readonly Vector2 BannerTitleOffset = new(0, -7);
        public const float BannerTitleMaxFontSize = 34f;
        public const float BannerTitleMinFontSize = 18f;
        // Fog in 0.5 s, hold 1.0 s, fog out 0.5 s.
        public const float BannerFadeSeconds = 0.5f;
        public const float BannerDisplaySeconds = 2f;

        // Detailed stats HUD: the Setting-1C rows, A · AS · M · C from the top.
        public static readonly string[] HudStatKeys = { "A", "AS", "M", "C" };
        public const string HudStatIconPrefix = "Stat Icon ";
        public const float HudStatIconSize = 34f;
        // Line spacing (TMP em/100) that puts the four value lines about one icon row apart.
        public const float HudValueLineSpacing = 110f;
        public const string HudValueSample = "0\n0\n0\n0%";

        // Reward altar. Character_Erpin shows about 1175 px of its 1254 px sprite at 1000 px per unit; the altar
        // starts at 80% of that height. Visible pixel sizes are the art's opaque bounds at 100 px per unit.
        public const string AltarVisualName = "Reward Altar";
        public const string AltarSpriteName = "Altar";
        public const string GiftName = "Floating Gift";
        public const string AltarTopName = "Altar Top"; // where the gift's shadow lies
        // The altar's solid floor footprint (Environment layer, so it blocks like a wall): a flat capsule a little
        // inside the stone ring's outline, resting on the art's visible bottom (12 px above the sprite's edge).
        public const string AltarFootprintName = "Altar Footprint";
        public static readonly Vector2 AltarFootprintSize = new(1.3f, 0.6f);
        public const float AltarVisibleBottomPixels = 12f;
        public static float AltarFootprintCenterY =>
            AltarVisibleBottomPixels / 100f * AltarScale + AltarFootprintSize.y / 2f;
        // Separate reviewed shadows (Resources/GroundShadowProfiles.json): the altar on the floor, the gift on the altar.
        public const string AltarShadowProfileId = "reward-altar";
        public const string GiftShadowProfileId = "reward-gift";
        public const float PlayerVisibleHeight = 1.175f;
        public const float AltarHeightRatio = 0.8f;
        public static readonly Vector2 AltarVisiblePixels = new(500, 347);
        public const float AltarTopCentrePixels = 252f; // middle of the top face, above the sprite's bottom edge
        public const float GiftWidthRatio = 0.5f;
        public static readonly Vector2 GiftVisiblePixels = new(355, 368);
        public const float GiftHoverGap = 0.1f;           // gap left under the gift at its lowest point
        public const float AltarBaseY = -0.55f;           // altar bottom under the reward root
        public const float PromptY = 1.35f;
        public const int AltarSortingOrder = 1;           // below the player body (PlayerMovement.BodySortingOrder)

        public static float AltarScale => PlayerVisibleHeight * AltarHeightRatio / (AltarVisiblePixels.y / 100f);
        public static float GiftScale =>
            AltarVisiblePixels.x / 100f * AltarScale * GiftWidthRatio / (GiftVisiblePixels.x / 100f);
        public static float AltarTopY => AltarTopCentrePixels / 100f * AltarScale;
        public static float GiftHalfHeight => GiftVisiblePixels.y / 200f * GiftScale;
        public static float GiftBaseY => AltarTopY + GiftHalfHeight + RewardGiftFloat.DefaultAmplitude + GiftHoverGap;

        // ESC pause window, laid out like docs/art-references/pause-menu-approved.png inside the docs/16 popup limit
        // (720~1120 wide, 360~760 high).
        public const string PausePanelName = "Pause Panel";
        public const string TitlePlaqueName = "Title Plaque";
        public const string SeedPlateName = "Seed Plate";
        public const string ArtifactBoxName = "Artifact Box";
        public static readonly Vector2 PausePanelSize = new(1120, 720);
        public static readonly Vector2 TitlePlaquePosition = new(0, 318);
        public static readonly Vector2 TitlePlaqueSize = new(500, 102);
        // Puts 일시정지 on the middle of the plaque body.
        public static readonly Vector2 PauseTitleOffset = new(0, -8);
        public static readonly Vector2 SeedPlatePosition = new(0, 236);
        public static readonly Vector2 SeedPlateSize = new(420, 61);
        public static readonly Vector2 StatsBoxPosition = new(-380, -30);
        public static readonly Vector2 StatsBoxSize = new(300, 380);
        public static readonly Vector2 ArtifactBoxPosition = new(160, -30);
        public static readonly Vector2 ArtifactBoxSize = new(740, 380);
        public static readonly Vector2 ArtifactViewportPosition = new(160, -48);
        public static readonly Vector2 ArtifactViewportSize = new(708, 320);
        public static readonly Vector2 ArtifactRowSize = new(660, 80);
        public const float StatRowTop = 130f;
        public const float StatRowStep = 48f;
        public static readonly Color BoxTint = new(1f, 0.97f, 0.93f, 0.8f);
        public static readonly Color ArtifactRowTint = new(1f, 0.88f, 0.79f, 1f); // light peach
        public static readonly Color LineTint = new(0.52f, 0.38f, 0.44f, 0.35f);

        // Pause stat rows in GamePauseArtifactView.BuildStatValues order.
        public static readonly string[] PauseStatKeys =
            { "Attack", "AttackSpeed", "MoveSpeed", "CriticalChance", "CriticalDamage", "Projectile", "Pierce" };
        public const string PauseStatIconPrefix = "Stat Icon ";
        public const string PauseStatValuePrefix = "Stat Value ";
        public const int CriticalChanceRow = 3;

        public const string FixedText = "일시정지현재 시드상세 스탯찾은 아티팩트게임 재개설정홈으로 나가기처음부터 다시하기" +
                                        GamePauseArtifactView.SeedPlaceholder + "×-.%0123456789";

        [MenuItem("Trickal Fan Game/Menu/Setup In-game Presentation")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before in-game presentation setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            MenuArtSetup.Apply();
            foreach (string name in All) LoadSprite(name);

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("In-game presentation requires the Frontend TMP font.");
            Week27Power1Setup.EnsureGlyphs(font, FixedText);
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup In-game Presentation");

            RectTransform frame = FindSingle<GameHudView>(scene).transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("Game HUD ReferenceFrame is missing.");
            ApplyBanner(frame);
            Week27Power1Setup.BuildHud(frame, FindSingle<PlayerPowerSlot>(scene), font);
            ApplyDetailedStatsHud(frame);

            GamePauseArtifactView pause = FindSingle<GamePauseArtifactView>(scene);
            PauseParts parts = StylePause(pause);
            Undo.RecordObject(pause, "Configure pause presentation");
            pause.ConfigurePresentation(FindSingle<RunProgress>(scene), parts.SeedValue, parts.StatValues,
                parts.CriticalChanceIcon);

            foreach (Component component in frame.GetComponentsInChildren<Component>(true))
                if (component != null) EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during in-game presentation setup.");
            Undo.CollapseUndoOperations(group);

            int altars = ApplyRewardAltars();
            AssetDatabase.SaveAssets();
            Debug.Log($"In-game presentation setup complete: banner, 권능 slot, stat icons, pause window and {altars} " +
                      "reward altars.");
        }

        public static string PathOf(string name) => $"{Folder}/{name}.png";

        public static Sprite LoadSprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PathOf(name));
            if (sprite == null) throw new InvalidOperationException("Missing in-game presentation sprite: " + PathOf(name));
            return sprite;
        }

        // ---------- First-entry banner ----------

        private static void ApplyBanner(RectTransform frame)
        {
            RectTransform panel = frame.Find(Week13Hud4CSetup.AnnouncementPanelName) as RectTransform;
            GameFloorNameView view = panel != null ? panel.GetComponent<GameFloorNameView>() : null;
            if (view == null || view.AnnouncementText == null)
                throw new InvalidOperationException("The floor entry announcement (HUD-4C) is missing.");
            Place(panel, BannerPosition, BannerSize);
            Image banner = Art(panel.gameObject, MapIntroBanner);

            TMP_Text title = view.AnnouncementText;
            Place(title.rectTransform, BannerTitleOffset, BannerTitleSize);
            StyleText(title, BannerTitleMaxFontSize, RibbonText);
            title.fontStyle = FontStyles.Normal;
            title.enableAutoSizing = true;
            title.fontSizeMax = BannerTitleMaxFontSize;
            title.fontSizeMin = BannerTitleMinFontSize;
            title.overflowMode = TextOverflowModes.Ellipsis;

            BannerMist mist = panel.GetComponent<BannerMist>();
            if (mist == null) mist = Undo.AddComponent<BannerMist>(panel.gameObject);
            Undo.RecordObject(mist, "Configure banner mist");
            mist.Configure(banner, title);
            Undo.RecordObject(view, "Configure banner timing");
            view.ConfigurePresentation(mist, BannerDisplaySeconds, BannerFadeSeconds);
        }

        // ---------- Detailed stats HUD ----------

        private static void ApplyDetailedStatsHud(RectTransform frame)
        {
            RectTransform panel = frame.Find(Week13Setting1CSetup.HudPanelName) as RectTransform;
            GameDetailedStatsHudView view = panel != null ? panel.GetComponent<GameDetailedStatsHudView>() : null;
            if (view == null) throw new InvalidOperationException("The detailed stats HUD (Setting-1C) is missing.");
            // Each icon sits on the middle of its value line, measured from the text itself.
            TMP_Text values = view.ValuesText;
            Undo.RecordObject(values, "Space stat lines");
            values.lineSpacing = HudValueLineSpacing;
            float[] lineMiddles = MeasureLineMiddles(values, HudValueSample);
            Image critical = null;
            for (int i = 0; i < HudStatKeys.Length; i++)
            {
                string key = HudStatKeys[i];
                Transform icon = panel.Find(HudStatIconPrefix + key) ?? panel.Find("Placeholder Icon " + key);
                if (icon == null) throw new InvalidOperationException("Detailed stats HUD icon is missing: " + key);
                if (icon.name != HudStatIconPrefix + key)
                {
                    Undo.RecordObject(icon.gameObject, "Rename stat icon");
                    icon.name = HudStatIconPrefix + key;
                }
                Place((RectTransform)icon, new Vector2(-102, lineMiddles[i]), Vector2.one * HudStatIconSize);
                Image image = StatIcon(icon.gameObject, HudStatSprite(key));
                Deactivate(icon, "Glyph");
                if (key == "C") critical = image;
            }
            Undo.RecordObject(view, "Configure stat icons");
            view.ConfigureCriticalChanceIcon(critical);
        }

        // Middle of each line of `sample` in the text's parent space; the text's own content is put back.
        public static float[] MeasureLineMiddles(TMP_Text text, string sample)
        {
            string previous = text.text;
            try
            {
                text.text = sample;
                text.ForceMeshUpdate(true);
                TMP_TextInfo info = text.textInfo;
                int lines = sample.Split('\n').Length;
                if (info.lineCount != lines)
                    throw new InvalidOperationException($"{text.name} laid out {info.lineCount} lines, expected {lines}.");
                float origin = text.rectTransform.anchoredPosition.y;
                return Enumerable.Range(0, lines)
                    .Select(i => origin + (info.lineInfo[i].ascender + info.lineInfo[i].descender) * 0.5f).ToArray();
            }
            finally
            {
                text.text = previous;
                text.ForceMeshUpdate(true);
            }
        }

        // The critical chance icon is the runtime critical burst; the menu starburst stands in while editing.
        private static Sprite HudStatSprite(string key) => key switch
        {
            "A" => MenuArtSetup.Load(MenuArtSetup.IconSword),
            "AS" => LoadSprite(StatAttackSpeed),
            "M" => LoadSprite(StatMoveSpeed),
            _ => MenuArtSetup.Load(MenuArtSetup.IconStarburst)
        };

        public static Sprite PauseStatSprite(int row) => row switch
        {
            0 => MenuArtSetup.Load(MenuArtSetup.IconSword),
            1 => LoadSprite(StatAttackSpeed),
            2 => LoadSprite(StatMoveSpeed),
            3 => MenuArtSetup.Load(MenuArtSetup.IconStarburst),
            4 => LoadSprite(StatCriticalDamage),
            5 => LoadSprite(StatProjectile),
            _ => LoadSprite(StatPierce)
        };

        private static Image StatIcon(GameObject owner, Sprite sprite)
        {
            Image image = Ensure<Image>(owner);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        // ---------- ESC pause window ----------

        public readonly struct PauseParts
        {
            public PauseParts(TMP_Text seedValue, TMP_Text[] statValues, Image criticalChanceIcon)
            {
                SeedValue = seedValue;
                StatValues = statValues;
                CriticalChanceIcon = criticalChanceIcon;
            }

            public TMP_Text SeedValue { get; }
            public TMP_Text[] StatValues { get; }
            public Image CriticalChanceIcon { get; }
        }

        public static bool IsPauseApplied(GamePauseArtifactView view) =>
            view != null && view.transform.Find(PausePanelName + "/" + TitlePlaqueName) != null;

        // Older setups rebuild the pause panel in the old look; once this look exists they call this to put it back.
        public static void ReapplyPause(GamePauseArtifactView view)
        {
            if (IsPauseApplied(view)) StylePause(view);
        }

        private static PauseParts StylePause(GamePauseArtifactView view)
        {
            // The window sits straight over the game: nothing is drawn outside it, but clicks are still blocked.
            Image backdrop = view.GetComponent<Image>();
            if (backdrop != null)
            {
                Undo.RecordObject(backdrop, "Clear pause backdrop");
                backdrop.sprite = null;
                backdrop.color = new Color(0f, 0f, 0f, 0f);
                backdrop.raycastTarget = true;
            }

            RectTransform panel = view.transform.Find(PausePanelName) as RectTransform;
            if (panel == null) throw new InvalidOperationException("The pause panel (HUD-3C) is missing.");
            Undo.RecordObject(panel, "Size pause panel");
            panel.sizeDelta = PausePanelSize;
            Skin(panel.gameObject, MenuArtSetup.PanelMain, Color.white);

            Image plaque = Art(Node(panel, TitlePlaqueName, TitlePlaquePosition, TitlePlaqueSize).gameObject,
                PauseTitlePlaque);
            TMP_Text title = panel.Find("Title")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                DrawBehind(plaque.transform, title.transform);
                Place(title.rectTransform, TitlePlaquePosition + PauseTitleOffset, new Vector2(300, 64));
                StyleText(title, 42, Ink);
                title.fontStyle = FontStyles.Normal;
                title.text = "일시정지";
            }
            Deactivate(panel, "Resume Hint");

            RectTransform plate = Node(panel, SeedPlateName, SeedPlatePosition, SeedPlateSize);
            Art(plate.gameObject, PauseSeedPlate);
            Label(plate, "Seed Label", "현재 시드", new Vector2(-84, 1), new Vector2(150, 44), 22, Ink);
            Line(plate, "Seed Divider", Vector2.zero, new Vector2(2, 30));
            TMP_Text seedValue = Label(plate, "Seed Value", null, new Vector2(84, 1), new Vector2(150, 44), 26, Ink);
            if (string.IsNullOrEmpty(seedValue.text)) seedValue.text = GamePauseArtifactView.SeedPlaceholder;

            (TMP_Text[] statValues, Image critical) = StylePauseStats(panel);
            StylePauseArtifacts(panel);
            StylePauseButtons(view, panel);
            InGameUiPolishSetup.StyleConfirmation(view);
            return new PauseParts(seedValue, statValues, critical);
        }

        private static (TMP_Text[] values, Image critical) StylePauseStats(RectTransform panel)
        {
            RectTransform stats = panel.Find("Detailed Stats") as RectTransform;
            if (stats == null) return (Array.Empty<TMP_Text>(), null);
            Place(stats, StatsBoxPosition, StatsBoxSize);
            Skin(stats.gameObject, MenuArtSetup.RoundedFill, BoxTint);
            SectionHeader(stats, stats.Find("Section Title")?.GetComponent<TMP_Text>(), "Section Ribbon",
                new Vector2(0, StatsBoxSize.y / 2f), 230f, "상세 스탯");
            // The old one-text list stays (GamePauseArtifactView still fills it) but the rows replace it on screen.
            Deactivate(stats, "Values");

            var values = new TMP_Text[PauseStatKeys.Length];
            Image critical = null;
            for (int i = 0; i < PauseStatKeys.Length; i++)
            {
                float y = StatRowTop - i * StatRowStep;
                Image icon = StatIcon(Node(stats, PauseStatIconPrefix + PauseStatKeys[i], new Vector2(-60, y),
                    new Vector2(42, 42)).gameObject, PauseStatSprite(i));
                if (i == CriticalChanceRow) critical = icon;
                values[i] = Label(stats, PauseStatValuePrefix + PauseStatKeys[i], null, new Vector2(40, y),
                    new Vector2(130, 44), 28, Ink, TextAlignmentOptions.Right);
                if (string.IsNullOrEmpty(values[i].text)) values[i].text = "-";
            }
            return (values, critical);
        }

        private static void StylePauseArtifacts(RectTransform panel)
        {
            RectTransform viewport = panel.Find("Artifact Viewport") as RectTransform;
            if (viewport == null) throw new InvalidOperationException("The pause artifact list (HUD-3C) is missing.");
            RectTransform box = Node(panel, ArtifactBoxName, ArtifactBoxPosition, ArtifactBoxSize);
            Skin(box.gameObject, MenuArtSetup.RoundedFill, BoxTint);
            DrawBehind(box, viewport);
            TMP_Text title = panel.Find("Artifact Section Title")?.GetComponent<TMP_Text>();
            RectTransform ribbon = SectionHeader(panel, title, "Artifact Section Ribbon",
                ArtifactBoxPosition + new Vector2(0, ArtifactBoxSize.y / 2f), 280f, "찾은 아티팩트");
            DrawBehind(box, ribbon);

            Place(viewport, ArtifactViewportPosition, ArtifactViewportSize);
            Image viewportImage = viewport.GetComponent<Image>();
            if (viewportImage != null)
            {
                Undo.RecordObject(viewportImage, "Clear viewport");
                viewportImage.sprite = null;
                viewportImage.color = new Color(1f, 1f, 1f, 0f); // still catches the scroll wheel
                viewportImage.raycastTarget = true;
            }
            VerticalLayoutGroup layout = viewport.Find("Content")?.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                Undo.RecordObject(layout, "Row spacing");
                layout.spacing = 10f;
            }
            TMP_Text empty = viewport.Find("Empty Message")?.GetComponent<TMP_Text>();
            if (empty != null) StyleText(empty, 24, InkSoft);

            RectTransform template = viewport.Find("Content/Artifact Detail Template") as RectTransform;
            if (template == null) throw new InvalidOperationException("The pause artifact row template is missing.");
            Undo.RecordObject(template, "Size artifact row");
            template.sizeDelta = ArtifactRowSize;
            LayoutElement element = Ensure<LayoutElement>(template.gameObject);
            element.preferredHeight = ArtifactRowSize.y;
            Skin(template.gameObject, MenuArtSetup.RoundedFill, ArtifactRowTint);

            RectTransform icon = Require<RectTransform>(template, "Icon");
            Place(icon, new Vector2(-288, 0), new Vector2(58, 58));
            TMP_Text stableId = icon.Find("Stable ID")?.GetComponent<TMP_Text>();
            if (stableId != null)
            {
                Place(stableId.rectTransform, Vector2.zero, new Vector2(54, 34));
                StyleText(stableId, 20, RibbonText);
            }
            TMP_Text name = Require<TMP_Text>(template, "Name");
            Place(name.rectTransform, new Vector2(-170, 0), new Vector2(160, 40));
            StyleText(name, 24, Ink, TextAlignmentOptions.Left);
            name.fontStyle = FontStyles.Normal;
            name.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text stack = Require<TMP_Text>(template, "Stack");
            Place(stack.rectTransform, new Vector2(-62, 0), new Vector2(50, 40));
            StyleText(stack, 22, Ink);
            Line(template, "Divider", new Vector2(-30, 0), new Vector2(2, 44));
            TMP_Text description = Require<TMP_Text>(template, "Description");
            Place(description.rectTransform, new Vector2(140, 0), new Vector2(330, 70));
            StyleText(description, 19, InkSoft, TextAlignmentOptions.Left);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void StylePauseButtons(GamePauseArtifactView view, RectTransform panel)
        {
            Button settings = view.SettingsButton != null
                ? view.SettingsButton
                : panel.Find(Week25Ux0Setup.SettingsButtonName)?.GetComponent<Button>();
            if (settings != null && view.ResumeButton != null && view.LeaveRunButton != null &&
                view.RestartRunButton != null)
                Week25Ux0Setup.LayoutPauseButtons(view, settings);
            foreach (Button button in new[] { settings, view.LeaveRunButton, view.ResumeButton, view.RestartRunButton })
                if (button != null) StylePauseButton(button, button == view.ResumeButton);
            TMP_Text resume = view.ResumeButton != null
                ? view.ResumeButton.transform.Find("Label")?.GetComponent<TMP_Text>()
                : null;
            if (resume != null) resume.text = "게임 재개";
        }

        private static void StylePauseButton(Button button, bool primary)
        {
            var rect = (RectTransform)button.transform;
            Image image = Skin(button.gameObject, primary ? MenuArtSetup.ButtonPrimary : MenuArtSetup.ButtonSecondary,
                Color.white, raycast: true, fitHeight: rect.sizeDelta.y);
            Undo.RecordObject(button, "Style " + button.name);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = Colors();
            Outline outline = button.GetComponent<Outline>();
            if (outline != null) Undo.DestroyObjectImmediate(outline);
            TMP_Text label = rect.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null)
            {
                StyleText(label, 26, primary ? ButtonText : Ink);
                label.fontStyle = FontStyles.Normal;
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(20, 4);
                label.rectTransform.offsetMax = new Vector2(-20, -4);
            }
            Pop(button);
        }

        private static RectTransform SectionHeader(RectTransform parent, TMP_Text title, string ribbonName,
            Vector2 position, float width, string text)
        {
            RectTransform ribbon = Node(parent, ribbonName, position, new Vector2(width, 52));
            Skin(ribbon.gameObject, MenuArtSetup.SectionRibbon, Color.white, fitHeight: 52);
            if (title == null) return ribbon;
            DrawBehind(ribbon, title.transform);
            Place(title.rectTransform, position + new Vector2(0, 1), new Vector2(width - 60, 44));
            StyleText(title, 26, RibbonText);
            title.fontStyle = FontStyles.Normal;
            title.text = text;
            return ribbon;
        }

        private static void Line(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Image image = Ensure<Image>(Node(parent, name, position, size).gameObject);
            image.sprite = null;
            image.color = LineTint;
            image.raycastTarget = false;
        }

        private static Image Art(GameObject owner, string spriteName)
        {
            Image image = Ensure<Image>(owner);
            image.sprite = LoadSprite(spriteName);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        // ---------- Reward altar ----------

        // Every room prefab's treasure reward gets the altar and the floating gift. The gift takes over the old
        // interaction marker's job (hidden once the reward is taken); the reward root keeps the trigger and selection.
        public static int ApplyRewardAltars()
        {
            Sprite altarSprite = LoadSprite(RewardAltar);
            Sprite giftSprite = LoadSprite(RewardGift);
            string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Rooms/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            int count = 0;
            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    RewardRoom reward = root.GetComponentInChildren<RewardRoom>(true);
                    if (reward == null) continue;
                    ApplyRewardAltar(reward, altarSprite, giftSprite);
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new InvalidOperationException("Could not save " + path);
                    count++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return count;
        }

        private static void ApplyRewardAltar(RewardRoom reward, Sprite altarSprite, Sprite giftSprite)
        {
            Transform visual = Child(reward.transform, AltarVisualName, new Vector3(0f, AltarBaseY, 0f), Vector3.one);
            SortingGroup group = Get<SortingGroup>(visual.gameObject);
            group.sortingLayerID = 0;
            group.sortingOrder = AltarSortingOrder;

            Transform altar = Child(visual, AltarSpriteName, Vector3.zero, Vector3.one * AltarScale);
            SpriteRenderer altarRenderer = Get<SpriteRenderer>(altar.gameObject);
            altarRenderer.sprite = altarSprite;
            altarRenderer.drawMode = SpriteDrawMode.Simple;
            altarRenderer.color = Color.white;
            altarRenderer.sortingOrder = 0;
            BindShadow(altar.gameObject, AltarShadowProfileId, null);

            Transform altarTop = Child(visual, AltarTopName, new Vector3(0f, AltarTopY, 0f), Vector3.one);
            Transform gift = Child(visual, GiftName, new Vector3(0f, GiftBaseY, 0f), Vector3.one * GiftScale);
            SpriteRenderer giftRenderer = Get<SpriteRenderer>(gift.gameObject);
            giftRenderer.sprite = giftSprite;
            giftRenderer.drawMode = SpriteDrawMode.Simple;
            giftRenderer.color = Color.white;
            giftRenderer.sortingOrder = 2; // its shadow draws at 1, between the altar and the gift
            Get<RewardGiftFloat>(gift.gameObject).Configure(GiftBaseY);
            BindShadow(gift.gameObject, GiftShadowProfileId, altarTop);

            Transform footprint = Child(visual, AltarFootprintName, new Vector3(0f, AltarFootprintCenterY, 0f),
                Vector3.one);
            int environment = LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName);
            if (environment < 0) throw new InvalidOperationException("Missing Environment layer.");
            footprint.gameObject.layer = environment;
            CapsuleCollider2D solid = Get<CapsuleCollider2D>(footprint.gameObject);
            solid.isTrigger = false;
            solid.direction = CapsuleDirection2D.Horizontal;
            solid.size = AltarFootprintSize;
            solid.offset = Vector2.zero;
            Get<RewardAltarDepth>(visual.gameObject).Configure(solid, AltarSortingOrder);

            Transform marker = reward.transform.Find("Interaction Marker");
            if (marker != null) Object.DestroyImmediate(marker.gameObject);
            Transform prompt = reward.transform.Find("Interaction Prompt");
            if (prompt != null) prompt.localPosition = new Vector3(0f, PromptY, 0f);
            reward.ConfigureInteractionVisuals(gift.gameObject, prompt != null ? prompt.gameObject : null);
        }

        // The shadow child itself is created at play time; the prefab only keeps the binding.
        private static void BindShadow(GameObject owner, string profileId, Transform surface)
        {
            GroundShadow shadow = Get<GroundShadow>(owner);
            shadow.SetProfileId(profileId);
            shadow.SetSurface(surface);
        }

        private static Transform Child(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            child.localPosition = position;
            child.localRotation = Quaternion.identity;
            child.localScale = scale;
            return child;
        }

        private static T Get<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }
    }
}
