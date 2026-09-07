namespace TrickalFanGame.Network
{
    public enum RegistrationErrorType
    {
        Unknown,
        ValidationError,
        NicknameExists,
        ProfileConflict,
        NetworkError,
        ServerError,
        ParseError
    }

    public readonly struct RegistrationError
    {
        public RegistrationErrorType Type { get; }
        public string Code { get; }
        public string Message { get; }
        public bool IsRetryable { get; }

        private RegistrationError(RegistrationErrorType type, string code, string message, bool isRetryable)
        {
            Type = type;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            IsRetryable = isRetryable;
        }

        public static RegistrationError FromApiError(long httpStatusCode, string code, string message)
        {
            var type = ClassifyError(httpStatusCode, code);
            bool isRetryable = type == RegistrationErrorType.NetworkError
                            || type == RegistrationErrorType.ServerError
                            || type == RegistrationErrorType.ProfileConflict;

            return new RegistrationError(type, code, message, isRetryable);
        }

        public static RegistrationError NetworkFailure(string message)
        {
            return new RegistrationError(
                RegistrationErrorType.NetworkError,
                "NETWORK_ERROR",
                message ?? "Network connection failed",
                isRetryable: true);
        }

        public static RegistrationError ParseFailure(string message)
        {
            return new RegistrationError(
                RegistrationErrorType.ParseError,
                "PARSE_ERROR",
                message ?? "Failed to parse server response",
                isRetryable: true);
        }

        private static RegistrationErrorType ClassifyError(long httpStatusCode, string code)
        {
            if (httpStatusCode == 422)
            {
                return RegistrationErrorType.ValidationError;
            }

            if (httpStatusCode == 409)
            {
                return code == "NICKNAME_EXISTS"
                    ? RegistrationErrorType.NicknameExists
                    : RegistrationErrorType.ProfileConflict;
            }

            if (httpStatusCode >= 500)
            {
                return RegistrationErrorType.ServerError;
            }

            if (httpStatusCode == 0)
            {
                return RegistrationErrorType.NetworkError;
            }

            return RegistrationErrorType.Unknown;
        }
    }
}
