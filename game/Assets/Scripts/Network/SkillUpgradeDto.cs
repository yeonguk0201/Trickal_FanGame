using System;
using UnityEngine.Networking;

namespace TrickalFanGame.Network
{
    public enum SkillType { LowGrade, HighGrade }

    public enum SkillUpgradeErrorType
    {
        Unknown,
        NotEnoughPoints,
        MaximumLevel,
        Conflict,
        Network,
        Server,
        Parse
    }

    [Serializable]
    public sealed class UpgradeSkillRequest { public int targetLevel; }

    [Serializable]
    public sealed class CharacterProgressResponse
    {
        public bool success;
        public CharacterProgressDto data;
        public ApiError error;
    }

    public readonly struct SkillUpgradeError
    {
        public SkillUpgradeErrorType Type { get; }
        public string Code { get; }
        public string Message { get; }

        private SkillUpgradeError(SkillUpgradeErrorType type, string code, string message)
        {
            Type = type;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public static SkillUpgradeError FromResponse(
            long status,
            UnityWebRequest.Result result,
            string requestError,
            string responseText)
        {
            if (result == UnityWebRequest.Result.ConnectionError)
                return new SkillUpgradeError(SkillUpgradeErrorType.Network, "NETWORK_ERROR", requestError);
            try
            {
                ApiErrorResponse response = UnityEngine.JsonUtility.FromJson<ApiErrorResponse>(responseText);
                return FromApiError(status, response?.error?.code, response?.error?.message ?? requestError);
            }
            catch
            {
                return FromApiError(status, "HTTP_ERROR", requestError);
            }
        }

        public static SkillUpgradeError FromApiError(long status, string code, string message)
        {
            SkillUpgradeErrorType type = code switch
            {
                "SKILL_POINT_NOT_ENOUGH" => SkillUpgradeErrorType.NotEnoughPoints,
                "SKILL_LEVEL_MAX" => SkillUpgradeErrorType.MaximumLevel,
                "INVALID_SKILL_TARGET_LEVEL" => SkillUpgradeErrorType.Conflict,
                _ when status == 409 => SkillUpgradeErrorType.Conflict,
                _ when status >= 500 => SkillUpgradeErrorType.Server,
                _ => SkillUpgradeErrorType.Unknown
            };
            return new SkillUpgradeError(type, code, message);
        }

        public static SkillUpgradeError ParseFailure(string message) =>
            new SkillUpgradeError(SkillUpgradeErrorType.Parse, "PARSE_ERROR", message);
    }
}
