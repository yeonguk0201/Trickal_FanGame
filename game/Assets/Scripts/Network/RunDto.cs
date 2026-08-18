using System;

namespace TrickalFanGame.Network
{
    [Serializable]
    public class CreateRunRequest
    {
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
    public class RunData
    {
        public string runId;
    }
}
