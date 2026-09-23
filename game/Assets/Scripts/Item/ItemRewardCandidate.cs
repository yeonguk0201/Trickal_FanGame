namespace TrickalFanGame.Item
{
    public enum ItemRewardCandidateType
    {
        Item = 0,
        Healing = 1,
    }

    public sealed class ItemRewardCandidate
    {
        public const string HealingStableId = "reward-heal";

        private ItemRewardCandidate(ItemRewardCandidateType candidateType, ItemDefinition definition,
            int currentStacks, float healMaxHealthRatio)
        {
            CandidateType = candidateType;
            Definition = definition;
            CurrentStacks = currentStacks;
            HealMaxHealthRatio = healMaxHealthRatio;
        }

        public ItemRewardCandidateType CandidateType { get; }
        public ItemDefinition Definition { get; }
        public int CurrentStacks { get; }
        public int MaxStacks => Definition != null ? Definition.MaxStacks : 0;
        public float HealMaxHealthRatio { get; }
        public string StableId => Definition != null ? Definition.ItemId : HealingStableId;
        public bool IsItem => CandidateType == ItemRewardCandidateType.Item && Definition != null;
        public bool IsHealing => CandidateType == ItemRewardCandidateType.Healing;

        public static ItemRewardCandidate ForItem(ItemDefinition definition, int currentStacks) =>
            new(ItemRewardCandidateType.Item, definition, currentStacks, 0f);

        public static ItemRewardCandidate ForHealing(float healMaxHealthRatio) =>
            new(ItemRewardCandidateType.Healing, null, 0, healMaxHealthRatio);
    }
}
