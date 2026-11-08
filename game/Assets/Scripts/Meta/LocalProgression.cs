using System;
using System.Collections.Generic;
using TrickalFanGame.Data;
using TrickalFanGame.Network;
using UnityEngine;

namespace TrickalFanGame.Meta
{
    // Offline-1 (docs/30 §3.1 S3·S4): growth on this PC without the server.
    // - The cache holds only values the server confirmed (a Run save, a skill upgrade or a profile lookup), keyed by
    //   the device key so a nickname change (S3) keeps it.
    // - The progress the game shows and plays with is that cache plus every write still waiting in the queue
    //   (Runs and skill upgrades, oldest first), recalculated with MetaProgressionRules. Once the server confirms a
    //   write it leaves the queue and its result becomes the cache, so the server value always wins.
    public static class LocalProgression
    {
        public const string CachePrefix = "TrickalFanGame.CharacterProgress.";
        public const string DeviceCachePrefix = CachePrefix + "device.";

        public static string CacheKey(string characterId) =>
            $"{DeviceCachePrefix}{LocalProfile.ClientProfileId}.{characterId}";

        public static string LegacyNicknameCacheKey(string nickname, string characterId) =>
            $"{CachePrefix}{nickname}.{characterId}";

        // Every owner that pending writes of this PC's profile can carry: the device key before registration and
        // the server user ID after it (ProfileRegistration moves them).
        public static string[] PendingOwners() => LocalProfile.HasServerAccount
            ? new[] { LocalProfile.UserId, LocalProfile.ClientProfileId }
            : new[] { LocalProfile.ClientProfileId };

        public static void StoreConfirmed(CharacterProgressDto progress)
        {
            if (progress == null || string.IsNullOrWhiteSpace(progress.characterId)) return;
            PlayerPrefs.SetString(CacheKey(progress.characterId),
                JsonUtility.ToJson(MetaProgressionRules.Normalize(progress.characterId, progress)));
            PlayerPrefs.Save();
        }

        // The last server-confirmed progress, or null. A registered profile without a device-key entry reads the
        // older nickname-keyed cache once (Offline-0 and earlier wrote that one).
        public static CharacterProgressDto LoadConfirmed(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return null;
            CharacterProgressDto progress = Read(CacheKey(characterId), characterId);
            if (progress != null || !LocalProfile.HasServerAccount) return progress;

            progress = Read(LegacyNicknameCacheKey(LocalProfile.Nickname, characterId), characterId);
            if (progress != null) StoreConfirmed(progress);
            return progress;
        }

        public static CharacterProgressDto Current(string characterId) =>
            Replay(LoadConfirmed(characterId), characterId, LocalPendingRunStorage.LoadQueue(PendingOwners()));

        public static bool HasPendingWrites() => LocalPendingRunStorage.LoadQueue(PendingOwners()).Count > 0;

        // Confirmed progress (or Lv.1) with every pending write of this character applied in queue order. A Run
        // without a recorded experience (queued before Offline-1) counts floor and kills only. A skill upgrade that
        // no longer fits (no point, wrong level) is skipped here; the server refuses it too and it is dropped then.
        public static CharacterProgressDto Replay(CharacterProgressDto confirmed, string characterId,
            IReadOnlyList<PendingSyncItem> queue)
        {
            CharacterProgressDto progress = MetaProgressionRules.Normalize(characterId, confirmed);
            if (queue == null) return progress;

            foreach (PendingSyncItem item in queue)
            {
                if (item == null) continue;
                if (item.IsRun)
                {
                    if (item.Run.characterId != characterId) continue;
                    progress = MetaProgressionRules.ApplyRunExperience(progress, ExperienceOf(item.Run));
                }
                else if (item.SkillUpgrade.characterId == characterId)
                {
                    MetaProgressionRules.TryApplySkillUpgrade(progress, item.SkillUpgrade.skillType,
                        item.SkillUpgrade.targetLevel);
                }
            }

            return progress;
        }

        public static int ExperienceOf(CreateRunRequest request)
        {
            if (request == null) return 0;
            return LocalPendingRunStorage.TryGetRunExperience(request.clientRunId, out int experience)
                ? experience
                : MetaProgressionRules.CalculateRunExperience(request.reachedFloor, request.killCount, null);
        }

        // Queues an upgrade of the shown progress. Returns the progress with it applied, or null when the rules
        // refuse it (the caller shows the reason).
        public static CharacterProgressDto QueueSkillUpgrade(string characterId, SkillType skillType)
        {
            CharacterProgressDto current = Current(characterId);
            int level = skillType == SkillType.LowGrade ? current.lowGradeSkillLevel : current.highGradeSkillLevel;
            if (!MetaProgressionRules.TryApplySkillUpgrade(current, skillType, level + 1)) return null;

            LocalPendingRunStorage.SaveSkillUpgrade(new PendingSkillUpgrade
            {
                upgradeId = Guid.NewGuid().ToString(),
                ownerId = LocalProfile.OwnerId,
                characterId = characterId,
                skillType = skillType,
                targetLevel = level + 1
            });
            return current;
        }

        private static CharacterProgressDto Read(string key, string characterId)
        {
            string json = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return MetaProgressionRules.Normalize(characterId, JsonUtility.FromJson<CharacterProgressDto>(json));
            }
            catch
            {
                return null;
            }
        }
    }
}
