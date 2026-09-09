using TMPro;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class ArtifactPauseListEntryView : MonoBehaviour
    {
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

        public void ConfigureVisuals(Image configuredIcon, TMP_Text configuredStableIdText,
            TMP_Text configuredNameText, TMP_Text configuredStackText, TMP_Text configuredDescriptionText)
        {
            icon = configuredIcon;
            stableIdText = configuredStableIdText;
            nameText = configuredNameText;
            stackText = configuredStackText;
            descriptionText = configuredDescriptionText;
        }

        public void Bind(ItemDefinition definition, int stackCount)
        {
            Definition = definition;
            StackCount = Mathf.Max(1, stackCount);
            if (icon != null) icon.color = ArtifactHudSlotView.GetRarityColor(
                definition != null ? definition.Rarity : ItemRarity.Common);
            if (stableIdText != null) stableIdText.text = ArtifactHudSlotView.GetShortStableId(
                definition != null ? definition.ItemId : string.Empty);
            if (nameText != null) nameText.text = definition != null ? definition.DisplayName : string.Empty;
            if (stackText != null) stackText.text = $"×{StackCount}";
            if (descriptionText != null) descriptionText.text = ArtifactEffectDescription.Build(definition);
        }
    }
}
