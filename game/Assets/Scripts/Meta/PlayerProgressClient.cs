using System;
using TrickalFanGame.Network;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Meta
{
    [DisallowMultipleComponent]
    public sealed class PlayerProgressClient : MonoBehaviour
    {
        private const string CachePrefix = "TrickalFanGame.CharacterProgress.";

        [SerializeField] private string userNickname = "test-player";
        [SerializeField] private PlayerSkill lowerGradeSkill;
        [SerializeField] private PlayerUltimate highGradeSkill;
        [SerializeField] private string statusMessage = "Progress not loaded.";

        private IGameApiClient apiClient;
        private bool isLoading;

        public string UserNickname => userNickname;
        public string ResolvedUserId { get; private set; }
        public string AppliedCharacterId { get; private set; }
        public CharacterProgressDto AppliedProgress { get; private set; }
        public CharacterProgressDto LastResultProgress { get; private set; }
        public int LastExperienceGained { get; private set; }
        public bool IsOfflineFallback { get; private set; }
        public bool IsLoading => isLoading;
        public string StatusMessage => statusMessage;

        private void Awake()
        {
            ResolveSkillReferences();
            apiClient = ApiClient.Instance;
        }

        public void Configure(
            string configuredNickname,
            PlayerSkill configuredLowerGradeSkill,
            PlayerUltimate configuredHighGradeSkill)
        {
            if (!string.IsNullOrWhiteSpace(configuredNickname))
            {
                userNickname = configuredNickname;
            }

            lowerGradeSkill = configuredLowerGradeSkill;
            highGradeSkill = configuredHighGradeSkill;
        }

        public void SetApiClient(IGameApiClient configuredApiClient)
        {
            apiClient = configuredApiClient;
        }

        public bool LoadAndApply(string characterId, Action<bool> onReady)
        {
            if (isLoading || string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            ResolveSkillReferences();
            isLoading = true;
            statusMessage = $"Loading progression for {characterId}...";
            IGameApiClient client = apiClient ?? ApiClient.Instance;
            if (client == null)
            {
                ApplyFallback(characterId, "ApiClient is unavailable.");
                onReady?.Invoke(true);
                return true;
            }

            client.GetUser(
                userNickname,
                response =>
                {
                    CharacterProgressDto progress = FindProgress(response, characterId);
                    if (progress == null)
                    {
                        ApplyFallback(characterId, "No saved progression exists for this character.");
                    }
                    else
                    {
                        ResolvedUserId = response.data.id;
                        Apply(characterId, progress, false);
                        SaveCachedProgress(characterId, progress);
                        statusMessage = $"Online progression loaded: {characterId} Lv.{progress.level}.";
                    }

                    isLoading = false;
                    onReady?.Invoke(true);
                },
                error =>
                {
                    ApplyFallback(characterId, error);
                    onReady?.Invoke(true);
                });
            return true;
        }

        public void ApplyFallback(string characterId, string reason)
        {
            CharacterProgressDto cached = LoadCachedProgress(characterId);
            bool hasCache = cached != null;
            Apply(characterId, cached ?? CreateDefaultProgress(characterId), true);
            isLoading = false;
            statusMessage = hasCache
                ? $"Offline cached progression: {characterId} Lv.{AppliedProgress.level}. {reason}"
                : $"Offline default progression: {characterId} Lv.1. {reason}";
        }

        public void ShowRunResult(RunData result)
        {
            if (result == null || result.progress == null)
            {
                statusMessage = "Run saved, but progression result is unavailable.";
                return;
            }

            LastExperienceGained = result.experienceGained;
            LastResultProgress = Normalize(result.progress.characterId, result.progress);
            SaveCachedProgress(LastResultProgress.characterId, LastResultProgress);
            statusMessage =
                $"Run result: +{LastExperienceGained} XP, Lv.{LastResultProgress.level}, " +
                $"XP {LastResultProgress.experience}/{LastResultProgress.experienceToNextLevel}, " +
                $"points {LastResultProgress.skillPoints}, skills " +
                $"{LastResultProgress.lowGradeSkillLevel}/{LastResultProgress.highGradeSkillLevel}.";
        }

        public bool IsAppliedFor(string characterId)
        {
            return AppliedProgress != null && AppliedCharacterId == characterId;
        }

        private void Apply(string characterId, CharacterProgressDto progress, bool offlineFallback)
        {
            CharacterProgressDto normalized = Normalize(characterId, progress);
            AppliedCharacterId = characterId;
            AppliedProgress = normalized;
            IsOfflineFallback = offlineFallback;
            lowerGradeSkill?.ApplyProgressionLevel(normalized.lowGradeSkillLevel);
            highGradeSkill?.ApplyProgressionLevel(normalized.highGradeSkillLevel);
        }

        private void ResolveSkillReferences()
        {
            if (lowerGradeSkill == null)
            {
                lowerGradeSkill = FindFirstObjectByType<PlayerSkill>();
            }

            if (highGradeSkill == null)
            {
                highGradeSkill = FindFirstObjectByType<PlayerUltimate>();
            }
        }

        private static CharacterProgressDto FindProgress(
            UserProfileResponse response,
            string characterId)
        {
            CharacterProgressDto[] progressList = response?.data?.characterProgress;
            if (progressList == null)
            {
                return null;
            }

            foreach (CharacterProgressDto progress in progressList)
            {
                if (progress != null && progress.characterId == characterId)
                {
                    return progress;
                }
            }

            return null;
        }

        private void SaveCachedProgress(string characterId, CharacterProgressDto progress)
        {
            PlayerPrefs.SetString(BuildCacheKey(characterId), JsonUtility.ToJson(progress));
            PlayerPrefs.Save();
        }

        private CharacterProgressDto LoadCachedProgress(string characterId)
        {
            string json = PlayerPrefs.GetString(BuildCacheKey(characterId), string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return Normalize(characterId, JsonUtility.FromJson<CharacterProgressDto>(json));
            }
            catch
            {
                return null;
            }
        }

        private string BuildCacheKey(string characterId)
        {
            return $"{CachePrefix}{userNickname}.{characterId}";
        }

        private static CharacterProgressDto CreateDefaultProgress(string characterId)
        {
            return new CharacterProgressDto
            {
                characterId = characterId,
                level = 1,
                experience = 0,
                experienceToNextLevel = 400,
                skillPoints = 0,
                lowGradeSkillLevel = 1,
                highGradeSkillLevel = 1
            };
        }

        private static CharacterProgressDto Normalize(
            string characterId,
            CharacterProgressDto progress)
        {
            if (progress == null)
            {
                return CreateDefaultProgress(characterId);
            }

            return new CharacterProgressDto
            {
                characterId = characterId,
                level = Mathf.Clamp(progress.level, 1, 19),
                experience = Mathf.Max(0, progress.experience),
                experienceToNextLevel = Mathf.Max(0, progress.experienceToNextLevel),
                skillPoints = Mathf.Max(0, progress.skillPoints),
                lowGradeSkillLevel = SkillProgressionRules.ClampLevel(progress.lowGradeSkillLevel),
                highGradeSkillLevel = SkillProgressionRules.ClampLevel(progress.highGradeSkillLevel)
            };
        }

        private void OnGUI()
        {
            if (AppliedProgress == null && LastResultProgress == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(10, 60, 520, 72));
            GUILayout.Label(statusMessage, GUI.skin.box);
            GUILayout.EndArea();
        }
    }
}
