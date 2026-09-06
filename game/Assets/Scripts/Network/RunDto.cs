using System;

namespace TrickalFanGame.Network
{
    [Serializable]
    public class CreateRunRequest
    {
        public string clientRunId;
        public string userId;
        public string characterId;
        public string gameVersion;
        public string startedAt;
        public string endedAt;
        // API contract: total play time in seconds.
        public int playTime;
        public int reachedFloor;
        public bool isCleared;
        public int killCount;
        public string deathReason;
        public RunItemDto[] items;
    }

    [Serializable]
    public class RunItemDto
    {
        public string itemId;
        public int floor;
        public int order;
        public string acquiredAt;
    }

    [Serializable]
    public class CreateRunResponse
    {
        public bool success;
        public RunData data;
        public ApiError error;
    }

    [Serializable]
    public class ApiError
    {
        public string code;
        public string message;
    }

    [Serializable]
    public class ApiErrorResponse
    {
        public bool success;
        public ApiError error;
    }

    [Serializable]
    public class RunData
    {
        public string runId;
        public int experienceGained;
        public CharacterProgressDto progress;
    }

    [Serializable]
    public class CharacterProgressDto
    {
        public string characterId;
        public int level;
        public int maxLevel;
        public int experience;
        public int experienceToNextLevel;
        public int skillPoints;
        public int lowGradeSkillLevel;
        public int highGradeSkillLevel;
        public int maxSkillLevel;
    }

    [Serializable]
    public class UserProfileResponse
    {
        public bool success;
        public UserProfileData data;
        public ApiError error;
    }

    [Serializable]
    public class UserProfileData
    {
        public string id;
        public string nickname;
        public UserStatsDto stats;
        public CharacterProgressDto[] characterProgress;
    }

    [Serializable]
    public class UserStatsDto
    {
        public int totalRuns;
        public int clears;
        public float winRate;
        public float averagePlayTime;
        public float averageFloor;
        public int highestFloor;
    }

    [Serializable]
    public class CreateUserRequest
    {
        public string clientProfileId;
        public string nickname;
    }

    [Serializable]
    public class CreateUserResponse
    {
        public bool success;
        public CreateUserData data;
        public ApiError error;
    }

    [Serializable]
    public class CreateUserData
    {
        public string id;
        public string clientProfileId;
        public string nickname;
        public CreateUserCharacterProgressDto[] characterProgress;
    }

    [Serializable]
    public class CreateUserCharacterProgressDto
    {
        public string characterId;
        public int level;
        public int experience;
        public int experienceToNextLevel;
        public int skillPoints;
        public int lowGradeSkillLevel;
        public int highGradeSkillLevel;
    }
}
