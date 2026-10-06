using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Trickal Fan Game/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private ItemKind kind;
        [SerializeField] private ItemRarity rarity;
        [SerializeField] private bool isActive = true;
        [SerializeField] private List<ItemEffectEntry> effects = new();

        // Retained while older verification helpers and assets migrate to the compound contract.
        [SerializeField] private ItemEffectType effectType;
        [SerializeField] private float effectValue = 1f;
        [SerializeField] private ItemStackMode stackMode = ItemStackMode.Additive;
        [SerializeField, Min(0)] private int maxStacks;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public ItemKind Kind => kind;
        public ItemRarity Rarity => rarity;
        public bool IsActive => isActive;
        public IReadOnlyList<ItemEffectEntry> Effects => effects ?? (IReadOnlyList<ItemEffectEntry>)System.Array.Empty<ItemEffectEntry>();
        public ItemEffectType EffectType => effects != null && effects.Count > 0 ? effects[0].EffectType : effectType;
        public float EffectValue => effects != null && effects.Count > 0 ? effects[0].Magnitude : effectValue;
        public ItemStackMode StackMode => stackMode;
        public int MaxStacks => maxStacks;
        public bool IsSingleUse => ItemKindText.IsSingleUse(kind);

        public bool IsValid => IsItemIdValidForKind(itemId, kind) &&
                               !string.IsNullOrWhiteSpace(displayName) &&
                               maxStacks >= 0 &&
                               (!IsSingleUse || maxStacks == 1) &&
                               (effects != null && effects.Count > 0
                                   ? effects.All(effect => effect != null && effect.TryValidate(out _))
                                   : effectValue > 0f);

        public static bool IsItemIdValidForKind(string configuredItemId, ItemKind configuredKind)
        {
            if (string.IsNullOrWhiteSpace(configuredItemId))
            {
                return false;
            }

            return configuredKind switch
            {
                ItemKind.Artifact => configuredItemId.StartsWith("item-", System.StringComparison.Ordinal) ||
                                     configuredItemId.StartsWith("artifact-", System.StringComparison.Ordinal),
                ItemKind.Spell => configuredItemId.StartsWith("spell-", System.StringComparison.Ordinal),
                ItemKind.SingleUseSpell => configuredItemId.StartsWith("single-spell-", System.StringComparison.Ordinal),
                ItemKind.JjangsemSpell => configuredItemId.StartsWith("jjangsem-", System.StringComparison.Ordinal),
                _ => false,
            };
        }

#if UNITY_EDITOR
        public void ConfigureContract(
            string configuredItemId,
            string configuredDisplayName,
            ItemRarity configuredRarity,
            bool configuredIsActive,
            int configuredMaxStacks,
            params ItemEffectEntry[] configuredEffects)
        {
            ConfigureContract(
                configuredItemId,
                configuredDisplayName,
                ItemKind.Artifact,
                configuredRarity,
                configuredIsActive,
                configuredMaxStacks,
                configuredEffects);
        }

        public void ConfigureContract(
            string configuredItemId,
            string configuredDisplayName,
            ItemKind configuredKind,
            ItemRarity configuredRarity,
            bool configuredIsActive,
            int configuredMaxStacks,
            params ItemEffectEntry[] configuredEffects)
        {
            itemId = configuredItemId;
            displayName = configuredDisplayName;
            kind = configuredKind;
            rarity = configuredRarity;
            isActive = configuredIsActive;
            maxStacks = configuredMaxStacks;
            stackMode = ItemStackMode.Additive;
            effects = configuredEffects?.ToList() ?? new List<ItemEffectEntry>();

            if (effects.Count > 0)
            {
                effectType = effects[0].EffectType;
                effectValue = effects[0].Magnitude > 0f
                    ? effects[0].Magnitude
                    : Mathf.Max(1, effects[0].IntegerAmount);
            }
        }
#endif
    }
}
