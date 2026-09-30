using TMPro;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class ArtifactHudSlotView : MonoBehaviour
    {
        private static readonly Color CommonColor = new(0.36f, 0.4f, 0.46f, 1f);
        private static readonly Color UncommonColor = new(0.24f, 0.62f, 0.38f, 1f);
        private static readonly Color RareColor = new(0.25f, 0.48f, 0.85f, 1f);
        private static readonly Color EpicColor = new(0.68f, 0.34f, 0.82f, 1f);

        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private Image stackBadge;
        [SerializeField] private TMP_Text stackText;

        public ItemDefinition Definition { get; private set; }
        public int StackCount { get; private set; }
        public Image Icon => icon;
        public TMP_Text IconText => iconText;
        public Image StackBadge => stackBadge;
        public TMP_Text StackText => stackText;

        public void ConfigureVisuals(Image configuredIcon, TMP_Text configuredIconText,
            Image configuredStackBadge, TMP_Text configuredStackText)
        {
            icon = configuredIcon;
            iconText = configuredIconText;
            stackBadge = configuredStackBadge;
            stackText = configuredStackText;
        }

        public void Bind(ItemDefinition definition, int stackCount)
        {
            Definition = definition;
            StackCount = Mathf.Max(1, stackCount);
            if (icon != null) icon.color = GetRarityColor(definition != null ? definition.Rarity : ItemRarity.Common);
            if (iconText != null) iconText.text = GetShortStableId(definition != null ? definition.ItemId : string.Empty);
            if (stackText != null) stackText.text = StackCount.ToString();
            if (stackBadge != null) stackBadge.gameObject.SetActive(true);
        }

        public static string GetShortStableId(string itemId)
        {
            const string prefix = "item-";
            return itemId != null && itemId.StartsWith(prefix) ? itemId.Substring(prefix.Length) : "?";
        }

        public static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Uncommon => UncommonColor,
                ItemRarity.Rare => RareColor,
                ItemRarity.Epic => EpicColor,
                _ => CommonColor,
            };
        }
    }
}
