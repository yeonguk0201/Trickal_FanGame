using System.Collections.Generic;
using System.Linq;
using TMPro;
using TrickalFanGame.Item;
using TrickalFanGame.Network;
using TrickalFanGame.Run;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class RunResultStageView : MonoBehaviour
    {
        internal static readonly Color Lime = new(0.75f, 0.98f, 0.31f);
        internal static readonly Color Mint = new(0.22f, 0.96f, 0.79f);
        internal const float LevelUpLightPeriod = 3.3f;
        private static Sprite roundedSprite;

        private TMP_FontAsset font;
        private TMP_Text detail;
        private TMP_Text level;
        private TMP_Text experience;
        private TMP_Text death;
        private TMP_Text floor;
        private TMP_Text kills;
        private TMP_Text time;
        private TMP_Text emptyRewards;
        private ScrollRect rewards;
        private HorizontalLayoutGroup rewardRow;
        private CanvasGroup levelUpGlow;
        private CanvasGroup softLevelUpRays;
        private CanvasGroup sharpLevelUpRays;
        private float levelUpLightStart;

        public TMP_Text Title { get; private set; }
        public TMP_Text ExperienceSource { get; private set; }
        internal CanvasGroup ExperienceSourceFade { get; private set; }
        public TMP_Text ExperienceValue => experience;
        public Slider ExperienceBar { get; private set; }
        public int RewardCount => rewardRow == null ? 0 : rewardRow.transform.childCount;

        public static RunResultStageView CreateRecord(Transform parent, TMP_FontAsset font)
        {
            var view = Create(parent, font, "RecordAndExperience", "RUN 기록");
            view.BuildRecord();
            return view;
        }

        public static RunResultStageView CreateLevelUp(Transform parent, TMP_FontAsset font)
        {
            var view = Create(parent, font, "LevelUp", "LEVEL UP!", topRules: false);
            view.BuildLevelUp();
            return view;
        }

        public static RunResultStageView CreateSkillPoints(Transform parent, TMP_FontAsset font)
        {
            var view = Create(parent, font, "SkillPoints", "스킬 포인트");
            view.BuildSkillPoints();
            return view;
        }

        public static RunResultStageView CreateArtifacts(Transform parent, TMP_FontAsset font)
        {
            var view = Create(parent, font, "AcquiredArtifacts", "획득 아티팩트");
            view.BuildRewardRow(69, 142);
            return view;
        }

        public static RunResultStageView CreateSummary(Transform parent, TMP_FontAsset font)
        {
            var view = Create(parent, font, "Summary", "RUN 결과");
            view.BuildSummary();
            return view;
        }

        public void SetExperienceSource(string label, float opacity)
        {
            ExperienceSource.text = label;
            ExperienceSourceFade.alpha = Mathf.Clamp01(opacity);
        }

        private static RunResultStageView Create(Transform parent, TMP_FontAsset font, string name, string title,
            bool topRules = true)
        {
            RectTransform root = Rect(parent, name, Vector2.zero, new Vector2(860, 700));
            RunResultStageView view = root.gameObject.AddComponent<RunResultStageView>();
            view.font = font;
            view.Title = view.Label(root, "Title", title, 42, 0, 260, 510, Lime);
            view.Title.fontSizeMax = 42;
            view.Title.fontSizeMin = 28;
            view.Title.enableAutoSizing = true;
            if (topRules)
            {
                Plate(root, "LeftRule", new Vector2(-325, 260), new Vector2(190, 3), Lime);
                Plate(root, "RightRule", new Vector2(325, 260), new Vector2(190, 3), Lime);
            }
            Plate(root, "BottomRule", new Vector2(0, -286), new Vector2(840, 3), Lime);
            root.gameObject.SetActive(false);
            return view;
        }

        private void BuildRecord()
        {
            BuildStats(150, 40);
            BuildGauge(-100);
            ExperienceSource = Label(transform, "ExperienceSource", "", 29, 0, -53, 790, Color.white);
            ExperienceSourceFade = ExperienceSource.gameObject.AddComponent<CanvasGroup>();
            ExperienceSourceFade.alpha = 0f;
        }

        private void BuildLevelUp()
        {
            BuildLevelUpLight();
            Icon(transform, "LevelUpCrown", UserArtwork.Load("run-result-level-up-crown"), 0, 208, 145);

            Sprite wordmark = UserArtwork.Load("run-result-level-up-wordmark");
            if (wordmark != null)
            {
                Title.gameObject.SetActive(false);
                Image image = Plate(transform, "LevelUpWordmark", new Vector2(0, 88),
                    new Vector2(435, 120), Color.white, wordmark);
                image.preserveAspect = true;
            }
            else
            {
                Title.rectTransform.anchoredPosition = new Vector2(0, 88);
            }

            Sprite leaf = UserArtwork.Load("run-result-level-up-leaf");
            Icon(transform, "LevelUpLeftLeaf", leaf, -253, 65, 104);
            Image rightLeaf = Icon(transform, "LevelUpRightLeaf", leaf, 253, 65, 104);
            rightLeaf.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            detail = Label(transform, "LevelChange", "", 80, 0, -80, 820, Color.white);
        }

        private void BuildLevelUpLight()
        {
            RectTransform root = Rect(transform, "LevelUpLight", new Vector2(0, 82), Vector2.zero);
            Sprite softLight = UserArtwork.Load("run-result-aura");
            if (softLight != null)
            {
                RectTransform glow = Rect(root, "Glow Layers", Vector2.zero, Vector2.zero);
                levelUpGlow = glow.gameObject.AddComponent<CanvasGroup>();
                Plate(glow, "Glow", new Vector2(0, 7), new Vector2(368, 219),
                    new Color(0.52f, 0.92f, 0.22f, 0.29f), softLight);
                Plate(glow, "Lower Glow", new Vector2(0, -58), new Vector2(296, 130),
                    new Color(0.52f, 0.92f, 0.22f, 0.17f), softLight);
                Plate(glow, "Center Glow", Vector2.zero, new Vector2(146, 104),
                    new Color(0.76f, 1f, 0.37f, 0.36f), softLight);
            }

            softLevelUpRays = CreateRayLayer(root, "Soft Rays", 15f, 1f, new Color(0.55f, 0.96f, 0.26f, 0.23f));
            sharpLevelUpRays = CreateRayLayer(root, "Sharp Rays", 10f, 0.66f, new Color(0.66f, 0.97f, 0.30f, 0.26f));
        }

        private static CanvasGroup CreateRayLayer(Transform parent, string name, float width, float feather, Color tint)
        {
            RectTransform layer = Rect(parent, name, Vector2.zero, new Vector2(520, 520));
            layer.gameObject.AddComponent<RunResultLevelUpRays>().Configure(width, feather, tint);
            return layer.gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (sharpLevelUpRays == null) return;
            levelUpLightStart = Time.unscaledTime;
            UpdateLevelUpLight(0f);
        }

        private void Update()
        {
            if (sharpLevelUpRays != null)
                UpdateLevelUpLight(Time.unscaledTime - levelUpLightStart);
        }

        private void UpdateLevelUpLight(float elapsed)
        {
            float sharpness = 0.5f - 0.5f * Mathf.Cos(elapsed * (2f * Mathf.PI / LevelUpLightPeriod));
            if (levelUpGlow != null)
                levelUpGlow.alpha = Mathf.Lerp(0.58f, 1f, sharpness);
            softLevelUpRays.alpha = Mathf.Lerp(0.47f, 0.63f, sharpness);
            sharpLevelUpRays.alpha = Mathf.Lerp(0.06f, 0.53f, sharpness);
        }

        private void BuildSkillPoints()
        {
            Icon(transform, "SkillToken", UserArtwork.Load("run-result-skill-point-token"), -115, 56, 142);
            detail = Label(transform, "PointsGained", "", 108, 81, 50, 300, Lime);
            ExperienceSource = Label(transform, "PointsBalance", "", 38, 0, -91, 740, Color.white);
        }

        private void BuildSummary()
        {
            BuildGauge(154);
            BuildStats(62, 31);
            detail = Label(transform, "SkillPointsBalance", "", 32, 0, -36, 800, Color.white);
            BuildRewardRow(-157, 96);
        }

        public void SetRecord(CreateRunRequest request, string deathDescription = null)
        {
            floor.text = $"도달 <color=#FFEB73>{request.reachedFloor}층</color>";
            kills.text = $"처치 <color=#FFEB73>{request.killCount}</color>";
            int seconds = Mathf.Max(0, request.playTime);
            time.text = $"{seconds / 60:00}:{seconds % 60:00}";
            string reason = deathDescription ?? PlayerDeathReason.DescribeRecordedReason(request.deathReason);
            death.text = request.isCleared ? "" : $"사망 원인  {reason}";
        }

        public void SetExperience(int characterLevel, int value, int required)
        {
            level.text = $"Lv. {characterLevel}";
            ExperienceBar.value = required > 0 ? Mathf.Clamp01((float)value / required) : 1f;
            experience.text = required > 0 ? $"{value} / {required}" : "MAX";
        }

        public void SetLevelUp(int from, int to) => detail.text = $"Lv. {from}  →  <color=#FFEB73>Lv. {to}</color>";

        public void SetSkillPoints(int gained, int balance)
        {
            detail.text = $"+{gained}";
            ExperienceSource.text = $"보유 {balance} 포인트";
        }

        public void SetSummary(RunResultPayload payload, CharacterProgressDto progress)
        {
            SetRecord(payload.Request, payload.DeathDescription);
            SetExperience(progress.level, progress.experience, progress.experienceToNextLevel);
            detail.text = $"스킬 포인트 <color=#FFEB73>{progress.skillPoints}</color>";
            SetRewards(payload);
        }

        public void SetRewards(RunResultPayload payload)
        {
            for (int i = rewardRow.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = rewardRow.transform.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            var groups = (payload.Request.items ?? System.Array.Empty<RunItemDto>())
                .Where(item => item != null && IsArtifact(item.itemId))
                .GroupBy(item => item.itemId).ToArray();
            rewards.gameObject.SetActive(groups.Length > 0);
            emptyRewards.gameObject.SetActive(groups.Length == 0);

            foreach (var group in groups)
            {
                int size = (int)((RectTransform)rewards.transform).rect.height - 36;
                ItemRarity rarity = payload.ItemRarities.TryGetValue(group.Key, out ItemRarity recorded)
                    ? recorded : ItemRarity.Common;
                RectTransform tile = Rect(rewardRow.transform, group.Key, Vector2.zero, new Vector2(size, size + 28));
                LayoutElement layout = tile.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = size;
                layout.preferredHeight = size + 28;
                Color rarityColor = rarity switch
                {
                    ItemRarity.Uncommon => new Color(0.20f, 0.45f, 0.26f),
                    ItemRarity.Rare => new Color(0.22f, 0.36f, 0.63f),
                    ItemRarity.Epic => new Color(0.48f, 0.28f, 0.64f),
                    _ => new Color(0.35f, 0.38f, 0.47f)
                };
                Plate(tile, "Tile", new Vector2(0, 14), new Vector2(size, size), rarityColor, Rounded).raycastTarget = true;
                Sprite icon = UserArtwork.Load(group.Key);
                if (icon != null)
                    Icon(tile, "Icon", icon, 0, 14, size - 14);
                else
                {
                    TMP_Text placeholder = Label(tile, "ItemId", group.Key, 18, 0, 14, size - 6, Color.white);
                    placeholder.enableAutoSizing = true;
                    placeholder.fontSizeMin = 10;
                    placeholder.fontSizeMax = 18;
                }
                Label(tile, "Count", $"×{group.Count()}", 24, 0, -size * 0.5f, size, Color.white);
            }
            rewardRow.transform.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        }

        internal static bool IsArtifact(string id) =>
            ItemDefinition.IsItemIdValidForKind(id, ItemKind.Artifact);

        private void BuildStats(float y, float size)
        {
            floor = Label(transform, "ReachedFloor", "", size, -265, y, 260, Color.white);
            kills = Label(transform, "KillCount", "", size, 0, y, 230, Color.white);
            time = Label(transform, "PlayTime", "", size, 265, y, 260, Color.white);
            Plate(transform, "FloorSeparator", new Vector2(-135, y), new Vector2(2, size), Lime);
            Plate(transform, "TimeSeparator", new Vector2(135, y), new Vector2(2, size), Lime);
            death = Label(transform, "DeathReason", "", 24, 0, y - 44, 800, Color.white);
            death.enableAutoSizing = true;
            death.fontSizeMin = 16;
            death.fontSizeMax = 24;
        }

        private void BuildGauge(float y)
        {
            level = Label(transform, "CurrentLevel", "", 41, -330, y, 170, Color.white);
            Image outline = Plate(transform, "ExperienceGauge", new Vector2(83, y), new Vector2(668, 55),
                new Color(0.84f, 0.89f, 0.79f), Rounded);
            Plate(outline.transform, "Track", Vector2.zero, new Vector2(660, 47),
                new Color(0.09f, 0.15f, 0.16f), Rounded);
            RectTransform area = Rect(outline.transform, "FillArea", Vector2.zero, new Vector2(660, 47));
            Image fill = Plate(area, "Fill", Vector2.zero, Vector2.zero, Mint, Rounded);
            ExperienceBar = outline.gameObject.AddComponent<Slider>();
            ExperienceBar.interactable = false;
            ExperienceBar.fillRect = fill.rectTransform;
            experience = Label(outline.transform, "Progress", "", 34, 0, 0, 600, Color.white);
        }

        private void BuildRewardRow(float y, int tileSize)
        {
            RectTransform viewport = Rect(transform, "RewardSlots", new Vector2(0, y), new Vector2(830, tileSize + 36));
            viewport.gameObject.AddComponent<RectMask2D>().padding = new Vector4(0f, -20f, 0f, 0f);
            Image hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            rewards = viewport.gameObject.AddComponent<ScrollRect>();
            RectTransform content = Rect(viewport, "Items", Vector2.zero, new Vector2(0, tileSize + 36));
            content.anchorMin = content.anchorMax = new Vector2(0, 0.5f);
            content.pivot = new Vector2(0, 0.5f);
            content.anchoredPosition = Vector2.zero;
            rewardRow = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            rewardRow.spacing = 18;
            rewardRow.childAlignment = TextAnchor.MiddleLeft;
            rewardRow.childControlWidth = rewardRow.childControlHeight = true;
            rewardRow.childForceExpandHeight = rewardRow.childForceExpandWidth = false;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rewards.viewport = viewport;
            rewards.content = content;
            rewards.vertical = false;
            rewards.movementType = ScrollRect.MovementType.Clamped;
            emptyRewards = Label(transform, "EmptyRewards", "획득한 아티팩트 없음", 32, 0, y, 800, Color.white);
        }

        internal TMP_Text Label(Transform parent, string name, string value, float size, float x, float y,
            float width, Color color) => Text(parent, font, name, value, size, x, y, width, color);

        internal static TMP_Text Text(Transform parent, TMP_FontAsset font, string name, string value,
            float size, float x, float y, float width, Color color)
        {
            TMP_Text label = Rect(parent, name, new Vector2(x, y), new Vector2(width, size * 1.45f))
                .gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        internal static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        internal static Image Plate(Transform parent, string name, Vector2 position, Vector2 size,
            Color color, Sprite sprite = null)
        {
            Image image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sprite == roundedSprite && sprite != null) image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        internal static Image Icon(Transform parent, string name, Sprite sprite, float x, float y, float size,
            Color? tint = null)
        {
            Image image = Plate(parent, name, new Vector2(x, y), new Vector2(size, size),
                tint ?? Color.white, sprite);
            image.preserveAspect = true;
            image.enabled = sprite != null;
            return image;
        }

        internal static Sprite Rounded
        {
            get
            {
                if (roundedSprite != null) return roundedSprite;
                const int radius = 16;
                const int size = 2 * radius + 2;
                Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
                texture.name = "Result UI Rounded Corners";
                texture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 offset = new(x - Mathf.Clamp(x, radius, size - radius - 1),
                        y - Mathf.Clamp(y, radius, size - radius - 1));
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius + 0.5f - offset.magnitude)));
                }
                texture.Apply(false, true);
                roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * radius);
                return roundedSprite;
            }
        }
    }
}
