using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Network;

namespace TrickalFanGame.Run
{
    public sealed class RunResultPayload
    {
        public RunResultPayload(CreateRunRequest request, CharacterProgressDto startingProgress,
            IGameApiClient apiClient, IReadOnlyDictionary<string, ItemRarity> itemRarities = null,
            string deathDescription = null)
        {
            Request = request;
            StartingProgress = startingProgress;
            ApiClient = apiClient;
            DeathDescription = deathDescription;
            ItemRarities = itemRarities == null
                ? new Dictionary<string, ItemRarity>()
                : new Dictionary<string, ItemRarity>(itemRarities);
        }

        public CreateRunRequest Request { get; }
        public CharacterProgressDto StartingProgress { get; }
        public IGameApiClient ApiClient { get; }
        public string DeathDescription { get; }
        public IReadOnlyDictionary<string, ItemRarity> ItemRarities { get; }
    }

    public static class RunResultContext
    {
        private static RunResultPayload pending;

        public static bool HasPending => pending != null;

        public static bool TryPrepare(CreateRunRequest request, CharacterProgressDto startingProgress,
            IGameApiClient apiClient, IReadOnlyDictionary<string, ItemRarity> itemRarities = null,
            string deathDescription = null)
        {
            if (pending != null || request == null || string.IsNullOrWhiteSpace(request.clientRunId))
                return false;

            pending = new RunResultPayload(request, Clone(startingProgress), apiClient, itemRarities, deathDescription);
            return true;
        }

        public static bool TryPeek(out RunResultPayload payload)
        {
            payload = pending;
            return payload != null;
        }

        public static void Clear()
        {
            pending = null;
        }

        private static CharacterProgressDto Clone(CharacterProgressDto source)
        {
            if (source == null) return null;
            return new CharacterProgressDto
            {
                characterId = source.characterId,
                level = source.level,
                maxLevel = source.maxLevel,
                experience = source.experience,
                experienceToNextLevel = source.experienceToNextLevel,
                skillPoints = source.skillPoints,
                lowGradeSkillLevel = source.lowGradeSkillLevel,
                highGradeSkillLevel = source.highGradeSkillLevel,
                maxSkillLevel = source.maxSkillLevel
            };
        }
    }
}
