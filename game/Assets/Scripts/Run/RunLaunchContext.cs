using TrickalFanGame.Network;

namespace TrickalFanGame.Run
{
    public sealed class RunLaunchRequest
    {
        public RunLaunchRequest(string userId, string nickname, string characterId, IGameApiClient apiClient)
        {
            UserId = userId;
            Nickname = nickname;
            CharacterId = characterId;
            ApiClient = apiClient;
        }

        public string UserId { get; }
        public string Nickname { get; }
        public string CharacterId { get; }
        public IGameApiClient ApiClient { get; }
    }

    public static class RunLaunchContext
    {
        private static RunLaunchRequest pending;
        private static IGameApiClient verificationApiClient;

        public static bool HasPending => pending != null;

        public static bool TryPrepare(string userId, string nickname, string characterId)
        {
            IGameApiClient apiClient = verificationApiClient;
            if (!TryPrepare(userId, nickname, characterId, apiClient)) return false;
            verificationApiClient = null;
            return true;
        }

        public static bool TryPrepare(string userId, string nickname, string characterId,
            IGameApiClient apiClient)
        {
            if (pending != null || string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            pending = new RunLaunchRequest(userId, nickname, characterId, apiClient);
            return true;
        }

        public static bool TryPeek(out RunLaunchRequest request)
        {
            request = pending;
            return request != null;
        }

        public static bool TryConsume(out RunLaunchRequest request)
        {
            request = pending;
            pending = null;
            return request != null;
        }

        public static void Clear()
        {
            pending = null;
            verificationApiClient = null;
        }

        public static void SetVerificationApiClient(IGameApiClient apiClient)
        {
            verificationApiClient = apiClient;
        }
    }
}
