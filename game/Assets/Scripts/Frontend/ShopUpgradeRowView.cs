using System;
using TMPro;
using TrickalFanGame.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // Elif-3: one row of the shop's invest list: an upgrade's name, its level, what the next level does and its
    // elif cost.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ShopUpgradeRowView : MonoBehaviour
    {
        private static readonly Color CostColor = new(0.78f, 0.9f, 1f, 1f);
        private static readonly Color BlockedColor = new(1f, 0.45f, 0.4f, 1f);
        private static readonly Color DoneColor = new(0.6f, 0.65f, 0.72f, 1f);
        // Shop UI artwork: dark text on the cream rows, with the elif picture beside the cost number.
        public static readonly Color ArtworkCostColor = new(0.29f, 0.2f, 0.2f, 1f);
        public static readonly Color ArtworkBlockedColor = new(0.82f, 0.34f, 0.29f, 1f);
        public static readonly Color ArtworkDoneColor = new(0.55f, 0.48f, 0.44f, 1f);

        [SerializeField] private int rowIndex;
        [SerializeField] private Button button;
        [SerializeField] private Behaviour focusFrame;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Image costIcon;
        [SerializeField] private ShopHoverTarget hover;

        private bool buttonBound;
        private bool hoverBound;

        public int RowIndex => rowIndex;
        public string UpgradeId { get; private set; }
        public ShopUpgradeStatus Status { get; private set; } = ShopUpgradeStatus.Unavailable;
        public bool IsFocused { get; private set; }
        public Button Button => button;
        public Behaviour FocusFrame => focusFrame;
        public TMP_Text NameText => nameText;
        public TMP_Text LevelText => levelText;
        public TMP_Text EffectText => effectText;
        public TMP_Text CostText => costText;
        public Image CostIcon => costIcon;
        public ShopHoverTarget Hover => hover;
        public bool UsesArtwork => costIcon != null;
        public event Action<ShopUpgradeRowView> Clicked;
        public event Action<ShopUpgradeRowView> Hovered;

        public void ConfigureVisuals(int configuredIndex, Button configuredButton, Behaviour configuredFocusFrame,
            TMP_Text configuredName, TMP_Text configuredLevel, TMP_Text configuredEffect, TMP_Text configuredCost)
        {
            UnbindButton();
            rowIndex = configuredIndex;
            button = configuredButton;
            focusFrame = configuredFocusFrame;
            nameText = configuredName;
            levelText = configuredLevel;
            effectText = configuredEffect;
            costText = configuredCost;
            BindButton();
        }

        // Shop UI artwork: the cost shows a number beside the elif picture, and hovering the row focuses it.
        public void ConfigureArtwork(Image configuredCostIcon, ShopHoverTarget configuredHover)
        {
            UnbindHover();
            costIcon = configuredCostIcon;
            hover = configuredHover;
            BindHover();
        }

        private void Awake() => BindButton();

        private void OnEnable()
        {
            BindButton();
            BindHover();
        }

        private void OnDestroy()
        {
            UnbindButton();
            UnbindHover();
        }

        public void Bind(ShopUpgradeDefinition definition, int level, ShopUpgradeStatus status, bool focused)
        {
            UpgradeId = definition?.UpgradeId;
            Status = status;
            IsFocused = focused;
            if (focusFrame != null) focusFrame.enabled = focused;
            if (definition == null) return;
            SetText(nameText, definition.DisplayName);
            SetText(levelText, ShopView.FormatUpgradeLevel(level, definition.MaxLevel));
            SetText(effectText, ShopView.FormatUpgradeNext(definition, level));
            bool maxLevel = level >= definition.MaxLevel;
            SetText(costText, UsesArtwork
                ? maxLevel ? ShopView.MaxLevelLabel : definition.GetCost(level + 1).ToString()
                : ShopView.FormatUpgradeCost(definition, level));
            if (costIcon != null) costIcon.enabled = !maxLevel;
            if (costText != null)
                costText.color = status switch
                {
                    ShopUpgradeStatus.Available => UsesArtwork ? ArtworkCostColor : CostColor,
                    ShopUpgradeStatus.MaxLevel or ShopUpgradeStatus.DevelopmentOverride =>
                        UsesArtwork ? ArtworkDoneColor : DoneColor,
                    _ => UsesArtwork ? ArtworkBlockedColor : BlockedColor,
                };
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }

        private void BindButton()
        {
            if (buttonBound || button == null) return;
            button.onClick.AddListener(NotifyClicked);
            buttonBound = true;
        }

        private void UnbindButton()
        {
            if (!buttonBound || button == null) return;
            button.onClick.RemoveListener(NotifyClicked);
            buttonBound = false;
        }

        private void BindHover()
        {
            if (hoverBound || hover == null) return;
            hover.Entered += NotifyHovered;
            hoverBound = true;
        }

        private void UnbindHover()
        {
            if (!hoverBound || hover == null) return;
            hover.Entered -= NotifyHovered;
            hoverBound = false;
        }

        private void NotifyClicked() => Clicked?.Invoke(this);
        private void NotifyHovered(ShopHoverTarget _) => Hovered?.Invoke(this);
    }
}
