using UnityEngine;

namespace TrickalFanGame.Item
{
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Trickal Fan Game/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private ItemEffectType effectType;
        [SerializeField] private float effectValue = 1f;
        [SerializeField] private ItemStackMode stackMode = ItemStackMode.Additive;
        [SerializeField, Min(0)] private int maxStacks;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public ItemEffectType EffectType => effectType;
        public float EffectValue => effectValue;
        public ItemStackMode StackMode => stackMode;
        public int MaxStacks => maxStacks;

        public bool IsValid => !string.IsNullOrWhiteSpace(itemId) && effectValue > 0f;
    }
}
