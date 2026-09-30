using System.Text.RegularExpressions;

namespace TrickalFanGame.Utils
{
    public static class NicknameValidator
    {
        public const int MinLength = 2;
        public const int MaxLength = 12;

        private static readonly Regex ValidPattern = new Regex(
            @"^[가-힣a-zA-Z0-9]+$",
            RegexOptions.Compiled
        );

        public static ValidationResult Validate(string nickname)
        {
            if (string.IsNullOrEmpty(nickname))
            {
                return new ValidationResult(false, "닉네임을 입력해주세요.");
            }

            string trimmed = nickname.Trim();

            if (trimmed.Length < MinLength)
            {
                return new ValidationResult(false, $"닉네임은 {MinLength}자 이상이어야 합니다.");
            }

            if (trimmed.Length > MaxLength)
            {
                return new ValidationResult(false, $"닉네임은 {MaxLength}자 이하여야 합니다.");
            }

            if (!ValidPattern.IsMatch(trimmed))
            {
                return new ValidationResult(false, "닉네임은 한글, 영문, 숫자만 사용할 수 있습니다.");
            }

            return new ValidationResult(true, null);
        }
    }

    public readonly struct ValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        public ValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }
}
