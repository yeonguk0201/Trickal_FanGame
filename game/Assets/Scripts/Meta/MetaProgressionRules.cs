using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Network;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Meta
{
    // Offline-1 (docs/30 §3.1 S4): the same experience, level and skill point rules as the Backend
    // (backend/src/contracts/meta-progression.ts, backend/src/domain/meta-progression.ts), so the game grows a
    // character without the server. The server value stays the reference; these results are a local cache until a
    // Run or skill upgrade is confirmed. Keep every value here equal to the Backend contract (contract-drift-check).
    public static class MetaProgressionRules
    {
        public const int CharacterMaxLevel = 19;
        public const int SkillMaxLevel = SkillProgressionRules.MaximumLevel;
        public const int ReachedFloorExperience = 100;
        public const int KillExperience = 5;

        // Index 0 is the XP required to advance from Lv.1 to Lv.2.
        public static readonly int[] ExperienceToNextLevel =
        {
            400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700,
            1800, 1900, 2000, 2100
        };

        public static int RarityExperience(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 10,
            ItemRarity.Uncommon => 20,
            ItemRarity.Rare => 30,
            ItemRarity.Epic => 50,
            _ => 0
        };

        public static int RequiredExperience(int level) =>
            level >= CharacterMaxLevel ? 0 : ExperienceToNextLevel[Mathf.Clamp(level, 1, CharacterMaxLevel) - 1];

        // calculateRunExperience: reached floor × 100 + kills × 5 + each recorded item by rarity.
        public static int CalculateRunExperience(int reachedFloor, int killCount, IEnumerable<ItemRarity> itemRarities)
        {
            int experience = Mathf.Max(0, reachedFloor) * ReachedFloorExperience +
                             Mathf.Max(0, killCount) * KillExperience;
            if (itemRarities != null)
                foreach (ItemRarity rarity in itemRarities)
                    experience += RarityExperience(rarity);
            return experience;
        }

        // applyRunExperience: every level gained gives one skill point; the maximum level keeps 0/0 experience.
        public static CharacterProgressDto ApplyRunExperience(CharacterProgressDto progress, int experienceGained)
        {
            CharacterProgressDto result = Clone(progress);
            if (result.level >= CharacterMaxLevel)
            {
                result.level = CharacterMaxLevel;
                result.experience = 0;
                result.experienceToNextLevel = 0;
                return result;
            }

            int level = result.level;
            long experience = (long)result.experience + Mathf.Max(0, experienceGained);
            int levelsGained = 0;
            while (level < CharacterMaxLevel)
            {
                int required = ExperienceToNextLevel[level - 1];
                if (experience < required) break;
                experience -= required;
                level++;
                levelsGained++;
            }

            if (level == CharacterMaxLevel) experience = 0;
            result.level = level;
            result.experience = (int)Mathf.Min(experience, int.MaxValue);
            result.experienceToNextLevel = RequiredExperience(level);
            result.skillPoints += levelsGained;
            return result;
        }

        // The Backend upgrade: the target must be one above the current level, below the maximum, with a point to
        // spend. A target already reached changes nothing (the request is idempotent).
        public static bool TryApplySkillUpgrade(CharacterProgressDto progress, SkillType skillType, int targetLevel)
        {
            if (progress == null) return false;
            int current = skillType == SkillType.LowGrade ? progress.lowGradeSkillLevel : progress.highGradeSkillLevel;
            if (targetLevel != current + 1 || current >= SkillMaxLevel || progress.skillPoints < 1) return false;

            progress.skillPoints--;
            if (skillType == SkillType.LowGrade) progress.lowGradeSkillLevel++;
            else progress.highGradeSkillLevel++;
            return true;
        }

        public static CharacterProgressDto CreateDefault(string characterId) => new()
        {
            characterId = characterId,
            level = 1,
            maxLevel = CharacterMaxLevel,
            experience = 0,
            experienceToNextLevel = ExperienceToNextLevel[0],
            skillPoints = 0,
            lowGradeSkillLevel = 1,
            highGradeSkillLevel = 1,
            maxSkillLevel = SkillMaxLevel
        };

        public static CharacterProgressDto Normalize(string characterId, CharacterProgressDto progress)
        {
            if (progress == null) return CreateDefault(characterId);
            int level = Mathf.Clamp(progress.level, 1, CharacterMaxLevel);
            return new CharacterProgressDto
            {
                characterId = characterId,
                level = level,
                maxLevel = CharacterMaxLevel,
                experience = level >= CharacterMaxLevel ? 0 : Mathf.Max(0, progress.experience),
                experienceToNextLevel = RequiredExperience(level),
                skillPoints = Mathf.Max(0, progress.skillPoints),
                lowGradeSkillLevel = SkillProgressionRules.ClampLevel(progress.lowGradeSkillLevel),
                highGradeSkillLevel = SkillProgressionRules.ClampLevel(progress.highGradeSkillLevel),
                maxSkillLevel = SkillMaxLevel
            };
        }

        private static CharacterProgressDto Clone(CharacterProgressDto progress) =>
            Normalize(progress?.characterId, progress);
    }
}
