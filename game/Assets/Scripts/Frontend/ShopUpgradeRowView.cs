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

        [SerializeField] private int rowIndex;
        [SerializeField] private Button button;
        [SerializeField] private Behaviour focusFrame;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text costText;

        private bool buttonBound;

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
        public event Action<ShopUpgradeRowView> Clicked;

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

        private void Awake() => BindButton();
        private void OnEnable() => BindButton();
        private void OnDestroy() => UnbindButton();

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
            SetText(costText, ShopView.FormatUpgradeCost(definition, level));
            if (costText != null)
                costText.color = status switch
                {
                    ShopUpgradeStatus.Available => CostColor,
                    ShopUpgradeStatus.MaxLevel or ShopUpgradeStatus.DevelopmentOverride => DoneColor,
                    _ => BlockedColor,
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

        private void NotifyClicked() => Clicked?.Invoke(this);
    }
}
