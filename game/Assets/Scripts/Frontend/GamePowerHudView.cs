using TMPro;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // The 교주의 권능 slot on the game HUD (Power-1): held 권능 name, one gauge cell per room to clear, and the use key.
    // Final placement and resolution checks belong to UX-1.
    [DisallowMultipleComponent]
    public sealed class GamePowerHudView : MonoBehaviour
    {
        public const string KindLabel = "교주의 권능";
        public const string EmptyText = "없음";
        public const string UsableText = "[" + PlayerPowerSlot.UseKeyLabel + "] 사용";
        public const string ChargingText = "충전 중";
        public const string UnusableText = "[" + PlayerPowerSlot.UseKeyLabel + "] 사용 불가";
        public const string AllFixedText = KindLabel + EmptyText + UsableText + ChargingText + UnusableText + "/0123456789";
        public static readonly Color FilledCellColor = new(1f, 0.82f, 0.32f, 1f);
        public static readonly Color EmptyCellColor = new(0.22f, 0.25f, 0.32f, 0.95f);

        private static readonly Color EmptyIconColor = new(0.2f, 0.22f, 0.27f, 0.9f);
        private static readonly Color UsableKeyColor = new(1f, 0.82f, 0.32f, 1f);
        private static readonly Color UnusableKeyColor = new(0.55f, 0.6f, 0.66f, 1f);

        [SerializeField] private PlayerPowerSlot slot;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text gaugeText;
        [SerializeField] private TMP_Text keyText;
        // One cell per room to clear, left to right. Cells beyond the held 권능's requirement are hidden.
        [SerializeField] private Image[] gaugeCells = System.Array.Empty<Image>();

        private bool subscribed;
        private bool hasRendered;
        private ItemDefinition renderedDefinition;
        private int renderedCharge;
        private PowerSlotUseResult renderedState;

        public PlayerPowerSlot Slot => slot;
        public Image Icon => icon;
        public TMP_Text NameText => nameText;
        public TMP_Text GaugeText => gaugeText;
        public TMP_Text KeyText => keyText;
        public System.Collections.Generic.IReadOnlyList<Image> GaugeCells => gaugeCells;

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
            hasRendered = false;
            if (isActiveAndEnabled) Subscribe();
            RefreshNow();
        }

        private void OnEnable()
        {
            Subscribe();
            hasRendered = false;
            RefreshNow();
        }

        private void OnDisable() => Unsubscribe();

        // Usability follows pause, death and room conditions that raise no event, so it is polled.
        private void Update() => RefreshNow();

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

            if (icon != null) icon.color = held == null ? EmptyIconColor : ActivePowerPickup.PowerColor;
            UserArtwork.Apply(icon, held);
            if (nameText != null) nameText.text = held != null ? held.DisplayName : EmptyText;
            int required = held != null ? held.ActiveChargeRooms : 0;
            if (gaugeText != null) gaugeText.text = BuildGauge(charge, required);
            for (int index = 0; index < gaugeCells.Length; index++)
            {
                Image cell = gaugeCells[index];
                if (cell == null) continue;
                cell.gameObject.SetActive(index < required);
                cell.color = index < charge ? FilledCellColor : EmptyCellColor;
            }
            if (keyText != null)
            {
                keyText.gameObject.SetActive(held != null);
                bool usable = state == PowerSlotUseResult.Used;
                keyText.text = usable ? UsableText : state == PowerSlotUseResult.NotCharged ? ChargingText : UnusableText;
                keyText.color = usable ? UsableKeyColor : UnusableKeyColor;
            }
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
