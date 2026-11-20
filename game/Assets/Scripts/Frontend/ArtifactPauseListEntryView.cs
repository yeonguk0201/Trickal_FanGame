using TMPro;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class ArtifactPauseListEntryView : MonoBehaviour
    {
        // The stack column of a held 권능 or spell row names its kind instead (the column fits two letters).
        public const string PowerLabel = "권능";
        public const string SpellLabel = "스펠";
        public const string JjangsemSpellLabel = "짱셈";

        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text stableIdText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text stackText;
        [SerializeField] private TMP_Text descriptionText;

        public ItemDefinition Definition { get; private set; }
        public int StackCount { get; private set; }
        public Image Icon => icon;
        public TMP_Text StableIdText => stableIdText;
        public TMP_Text NameText => nameText;
        public TMP_Text StackText => stackText;
        public TMP_Text DescriptionText => descriptionText;
        public bool IsHeldSlot { get; private set; }

        public void ConfigureVisuals(Image configuredIcon, TMP_Text configuredStableIdText,
            TMP_Text configuredNameText, TMP_Text configuredStackText, TMP_Text configuredDescriptionText)
        {
            icon = configuredIcon;
            stableIdText = configuredStableIdText;
            nameText = configuredNameText;
            stackText = configuredStackText;
            descriptionText = configuredDescriptionText;
        }

        public void Bind(ItemDefinition definition, int stackCount, bool spent = false)
        {
            IsHeldSlot = false;
            Definition = definition;
            StackCount = Mathf.Max(1, stackCount);
            if (icon != null) icon.color = ArtifactHudSlotView.GetRarityColor(
                definition != null ? definition.Rarity : ItemRarity.Common);
            if (stableIdText != null) stableIdText.text = ArtifactHudSlotView.GetShortStableId(
                definition != null ? definition.ItemId : string.Empty);
            UserArtwork.Apply(icon, definition, stableIdText, spent);
            if (nameText != null) nameText.text = definition != null ? definition.DisplayName : string.Empty;
            if (stackText != null) stackText.text = $"×{StackCount}";
            if (descriptionText != null) descriptionText.text = ArtifactEffectDescription.Build(definition);
        }

        // The held 교주의 권능 or spell (ESC menu, 2026-11-20 user request): its artwork, name, kind and effect.
        public void BindHeld(ItemDefinition definition)
        {
            Bind(definition, 1);
            IsHeldSlot = true;
            if (definition == null) return;
            if (icon != null)
            {
                icon.color = definition.IsActivePower ? ActivePowerPickup.PowerColor
                    : definition.Kind == ItemKind.JjangsemSpell ? SingleUseItemPickup.JjangsemSpellColor
                    : SingleUseItemPickup.SpellColor;
                UserArtwork.Apply(icon, definition, stableIdText);
            }
            if (stackText != null) stackText.text = HeldLabel(definition);
        }

        public static string HeldLabel(ItemDefinition definition) =>
            definition == null ? string.Empty
            : definition.IsActivePower ? PowerLabel
            : definition.Kind == ItemKind.JjangsemSpell ? JjangsemSpellLabel
            : SpellLabel;
    }
}
