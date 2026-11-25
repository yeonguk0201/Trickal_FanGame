using TMPro;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // The 교주의 권능 slot on the game HUD (Power-1): the held 권능's icon, one gauge cell per room to clear, and the
    // "R" key strip across the icon's bottom like the spell and skill tiles. While the 권능 cannot be used the icon is
    // tinted grey instead of saying so. The name and the "0/4" count are optional (UX-1 shows the icon and cells only).
    // Final placement and resolution checks belong to UX-1.
    [DisallowMultipleComponent]
    public sealed class GamePowerHudView : MonoBehaviour, IHudDescribed
    {
        public const string KindLabel = "교주의 권능";
        public const string EmptyText = "없음";
        public const string KeyLabel = PlayerPowerSlot.UseKeyLabel;
        public const string AllFixedText = KindLabel + EmptyText + KeyLabel + "/0123456789";
        // The key strip matches the spell and skill tiles: a dark band 24 high across the drawn artwork's bottom.
        public const float KeyStripHeight = 24f;
        public static readonly Color KeyBackingColor = new(0f, 0f, 0f, 0.55f);
        // Multiplied into the icon while the held 권능 cannot be used (charging, paused, conditions not met).
        public static readonly Color UnusableTint = new(0.5f, 0.5f, 0.5f, 1f);
        // Power-2: the line a 3택1 card or a shop detail shows for a 권능 instead of artifact stacks.
        public const string OfferFixedText = "충전방0123456789";

        public static string BuildOfferLine(ItemDefinition power) =>
            $"충전 {power.ActiveChargeRooms}방";
        public static readonly Color FilledCellColor = new(0.45f, 0.82f, 1f, 1f);
        public static readonly Color EmptyCellColor = new(0.34f, 0.38f, 0.47f, 0.95f);
        // Balance-1: a full gauge turns gold and glows, so a ready 권능 reads at a glance. The cells drop the
        // sky-blue artwork for plain gold, then the cells and the track pulse toward FullGlowColor.
        public static readonly Color FullCellColor = new(1f, 0.78f, 0.22f, 1f);
        public static readonly Color FullGlowColor = new(1f, 0.95f, 0.6f, 1f);
        public const float FullGlowPeriod = 1.1f;

        private static readonly Color EmptyIconColor = new(0.2f, 0.22f, 0.27f, 0.9f);

        [SerializeField] private PlayerPowerSlot slot;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text gaugeText;
        [SerializeField] private TMP_Text keyText;
        // One cell per room to clear, bottom to top (UX-1 vertical gauge). Cells beyond the held 권능's requirement are hidden.
        [SerializeField] private Image[] gaugeCells = System.Array.Empty<Image>();
        // In-game presentation guide: a thin-frame background shown only while no 권능 is held (the icon hides then),
        // and the sky-blue cell artwork for charged cells. The artwork already holds the colour, so it draws untinted.
        [SerializeField] private GameObject emptySlot;
        [SerializeField] private Sprite filledCellSprite;
        [SerializeField] private Image keyBacking;

        private bool subscribed;
        private bool hasRendered;
        private ItemDefinition renderedDefinition;
        private int renderedCharge;
        private PowerSlotUseResult renderedState;
        private Image gaugeTrack;
        private Color gaugeTrackColor;
        private bool gaugeTrackCached;

        public PlayerPowerSlot Slot => slot;
        public Image Icon => icon;
        public TMP_Text NameText => nameText;
        public TMP_Text GaugeText => gaugeText;
        public TMP_Text KeyText => keyText;
        public System.Collections.Generic.IReadOnlyList<Image> GaugeCells => gaugeCells;
        public GameObject EmptySlot => emptySlot;
        public Sprite FilledCellSprite => filledCellSprite;
        public Image KeyBacking => keyBacking;
        public bool IsShowingUsable => renderedState == PowerSlotUseResult.Used;
        public bool IsShowingFull { get; private set; }
        // The colour a charged cell draws with: white over the cell artwork, the plain sky blue without it.
        public Color FilledColor => filledCellSprite != null ? Color.white : FilledCellColor;
        // The mouse resting on the 권능 icon shows its description (PickupDescriptionView).
        public RectTransform HoverArea => icon != null ? icon.rectTransform : (RectTransform)transform;
        public ItemDefinition Definition => slot != null ? slot.HeldDefinition : null;

        public void Configure(PlayerPowerSlot configuredSlot, Image configuredIcon, TMP_Text configuredNameText,
            TMP_Text configuredGaugeText, TMP_Text configuredKeyText, Image[] configuredGaugeCells)
        {
            Unsubscribe();
            slot = configuredSlot;
            icon = configuredIcon;
            nameText = configuredNameText;
            gaugeText = configuredGaugeText;
            keyText = configuredKeyText;
            gaugeCells = configuredGaugeCells ?? System.Array.Empty<Image>();
            gaugeTrackCached = false;
            hasRendered = false;
            if (isActiveAndEnabled) Subscribe();
            RefreshNow();
        }

        public void ConfigureArtwork(GameObject configuredEmptySlot, Sprite configuredFilledCellSprite)
        {
            emptySlot = configuredEmptySlot;
            filledCellSprite = configuredFilledCellSprite;
            hasRendered = false;
            RefreshNow();
        }

        public void ConfigureKeyBacking(Image configuredKeyBacking)
        {
            keyBacking = configuredKeyBacking;
            hasRendered = false;
            RefreshNow();
        }

        private void OnEnable()
        {
            Subscribe();
            HudDescribedRegistry.Register(this);
            hasRendered = false;
            RefreshNow();
        }

        private void OnDisable()
        {
            Unsubscribe();
            HudDescribedRegistry.Unregister(this);
        }

        // Usability follows pause, death and room conditions that raise no event, so it is polled.
        private void Update()
        {
            RefreshNow();
            if (IsShowingFull) ApplyFullGlow(Time.unscaledTime);
        }

        // 0 at the pulse's dimmest, 1 at its brightest.
        public static float FullGlowAmount(float time) =>
            0.5f - 0.5f * Mathf.Cos(time * (Mathf.PI * 2f / FullGlowPeriod));

        private void ApplyFullGlow(float time)
        {
            float glow = FullGlowAmount(time);
            int required = renderedDefinition != null ? renderedDefinition.ActiveChargeRooms : 0;
            for (int index = 0; index < gaugeCells.Length && index < required; index++)
                if (gaugeCells[index] != null)
                    gaugeCells[index].color = Color.Lerp(FullCellColor, FullGlowColor, glow * 0.6f);
            Image track = GaugeTrack;
            if (track != null) track.color = Color.Lerp(FullCellColor, FullGlowColor, glow);
        }

        // The pill outline around the cells (the cells' grandparent), recoloured only while full.
        private Image GaugeTrack
        {
            get
            {
                if (gaugeTrackCached) return gaugeTrack;
                gaugeTrackCached = true;
                Transform bar = gaugeCells.Length > 0 && gaugeCells[0] != null ? gaugeCells[0].transform.parent : null;
                gaugeTrack = bar != null && bar.parent != null && bar.parent != transform
                    ? bar.parent.GetComponent<Image>()
                    : null;
                if (gaugeTrack != null) gaugeTrackColor = gaugeTrack.color;
                return gaugeTrack;
            }
        }

        // The gauge is one pill-shaped bar: a thin outline (GaugeOutline wide) around an inner bar that masks the
        // cells to its round ends. The inner bar's colour shows through the thin GaugeCellGap lines between cells.
        public const float GaugeOutline = 2.5f;
        public const float GaugeCellGap = 1.5f;
        public static readonly Color GaugeFrameColor = new(0.1f, 0.12f, 0.18f, 0.9f);

        // The first `count` cells split the inner bar's height evenly, bottom to top, so a 3-room and a 4-room
        // 권능 gauge are the same height as the icon beside it.
        public static void LayoutGauge(System.Collections.Generic.IReadOnlyList<Image> cells, int count)
        {
            if (cells == null || cells.Count == 0 || count <= 0) return;
            RectTransform column = cells[0] != null ? cells[0].rectTransform.parent as RectTransform : null;
            if (column == null) return;
            Vector2 inner = column.sizeDelta;
            count = Mathf.Min(count, cells.Count);
            float height = (inner.y - GaugeCellGap * (count - 1)) / count;
            for (int index = 0; index < count; index++)
            {
                if (cells[index] == null) continue;
                RectTransform rect = cells[index].rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(inner.x, height);
                rect.anchoredPosition = new Vector2(0f, -inner.y * 0.5f + height * 0.5f + index * (height + GaugeCellGap));
            }
        }

        public static string BuildGauge(int charge, int required) =>
            required > 0 ? $"{Mathf.Clamp(charge, 0, required)}/{required}" : string.Empty;

        public void RefreshNow()
        {
            ItemDefinition held = slot != null ? slot.HeldDefinition : null;
            int charge = slot != null ? slot.Charge : 0;
            PowerSlotUseResult state = slot != null ? slot.GetUseState() : PowerSlotUseResult.Empty;
            if (hasRendered && held == renderedDefinition && charge == renderedCharge && state == renderedState) return;
            hasRendered = true;
            renderedDefinition = held;
            renderedCharge = charge;
            renderedState = state;

            bool usable = state == PowerSlotUseResult.Used;
            if (icon != null) icon.color = held == null ? EmptyIconColor : ActivePowerPickup.PowerColor;
            UserArtwork.Apply(icon, held);
            if (icon != null && held != null && !usable) icon.color *= UnusableTint;
            if (emptySlot != null) emptySlot.SetActive(held == null);
            // With the empty-slot frame the dark empty icon would cover it, so the icon only draws while held.
            if (icon != null) icon.enabled = held != null || emptySlot == null;
            if (nameText != null) nameText.text = held != null ? held.DisplayName : EmptyText;
            int required = held != null ? held.ActiveChargeRooms : 0;
            if (gaugeText != null) gaugeText.text = BuildGauge(charge, required);
            if (required > 0) LayoutGauge(gaugeCells, required);
            // The whole bar (outline and inner bar, the cells' grandparent) only shows while a 권능 is held.
            Transform bar = gaugeCells.Length > 0 && gaugeCells[0] != null ? gaugeCells[0].transform.parent : null;
            if (bar != null && bar.parent != null && bar.parent != transform)
                bar.parent.gameObject.SetActive(required > 0);
            IsShowingFull = required > 0 && charge >= required;
            for (int index = 0; index < gaugeCells.Length; index++)
            {
                Image cell = gaugeCells[index];
                if (cell == null) continue;
                cell.gameObject.SetActive(index < required);
                bool filled = index < charge;
                cell.sprite = filled && !IsShowingFull ? filledCellSprite : null;
                cell.color = IsShowingFull ? FullCellColor : filled ? FilledColor : EmptyCellColor;
            }
            Image track = GaugeTrack;
            if (track != null) track.color = IsShowingFull ? FullCellColor : gaugeTrackColor;
            if (keyText != null)
            {
                keyText.gameObject.SetActive(held != null);
                keyText.text = KeyLabel;
            }
            if (keyBacking != null) keyBacking.gameObject.SetActive(held != null);
            if (held != null) LayoutKeyStrip();
        }

        // The icon keeps the artwork's aspect inside its square, so the strip spans the drawn width, not the square.
        private void LayoutKeyStrip()
        {
            if (icon == null) return;
            RectTransform iconRect = icon.rectTransform;
            Vector2 drawn = iconRect.sizeDelta;
            Sprite sprite = icon.sprite;
            if (icon.preserveAspect && sprite != null && sprite.rect.height > 0f && drawn.y > 0f)
            {
                float aspect = sprite.rect.width / sprite.rect.height;
                if (aspect < drawn.x / drawn.y) drawn.x = drawn.y * aspect;
                else drawn.y = drawn.x / aspect;
            }
            Vector2 center = iconRect.anchoredPosition + new Vector2(0f, -drawn.y * 0.5f + KeyStripHeight * 0.5f);
            Place(keyBacking != null ? keyBacking.rectTransform : null, center, new Vector2(drawn.x, KeyStripHeight));
            Place(keyText != null ? keyText.rectTransform : null, center, new Vector2(drawn.x - 8f, KeyStripHeight - 2f));
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void OnSlotChanged() => RefreshNow();

        private void Subscribe()
        {
            if (subscribed || slot == null) return;
            slot.Changed += OnSlotChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (slot != null) slot.Changed -= OnSlotChanged;
            subscribed = false;
        }
    }
}
