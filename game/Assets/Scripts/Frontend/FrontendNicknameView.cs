using System;
using TMPro;
using TrickalFanGame.Data;
using TrickalFanGame.Meta;
using TrickalFanGame.Network;
using TrickalFanGame.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public sealed class FrontendNicknameView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private TMP_Text validationText;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text progressText;
        [Header("Menu-2 palette")]
        [SerializeField] private Color ruleColor = new(0.52f, 0.38f, 0.44f);
        [SerializeField] private Color validColor = new(0.22f, 0.48f, 0.18f);
        [SerializeField] private Color invalidColor = new(0.80f, 0.27f, 0.27f);

        public const string SubmittingText = "확인 중...";

        private bool _isSubmitting;
        private IGameApiClient _apiClient;
        private IUserRegistrationClient _registrationClient;

        public event Action<CreateUserData> OnRegistrationComplete;
        public event Action OnBackRequested;

        public TMP_InputField NicknameInput => nicknameInput;
        public TMP_Text ValidationText => validationText;
        public TMP_Text ErrorText => errorText;
        public Button SubmitButton => submitButton;
        public Button BackButton => backButton;
        public TMP_Text ProgressText => progressText;
        // Short enough for the slot between the card's two clover dividers.
        public static string RuleMessage =>
            $"한글·영문·숫자 {NicknameValidator.MinLength}~{NicknameValidator.MaxLength}자";

        public void Configure(TMP_InputField input, TMP_Text validation, TMP_Text error, Button submit)
        {
            nicknameInput = input;
            validationText = validation;
            errorText = error;
            submitButton = submit;
        }

        // Menu-2: back returns to the title. The button art carries its label, so "확인 중..." is a separate text
        // shown in the error line while the request is out.
        public void ConfigureMenu(Button back, TMP_Text progress)
        {
            backButton = back;
            progressText = progress;
        }

        public void SetApiClient(IGameApiClient apiClient)
        {
            _apiClient = apiClient;
            _registrationClient = apiClient as IUserRegistrationClient;
        }

        private void OnEnable()
        {
            if (nicknameInput != null)
            {
                nicknameInput.onValueChanged.AddListener(OnNicknameChanged);
                nicknameInput.onSubmit.AddListener(OnInputSubmitted);
                nicknameInput.characterLimit = NicknameValidator.MaxLength;
            }
            if (submitButton != null)
            {
                submitButton.onClick.AddListener(OnSubmitClicked);
            }
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
            SetSubmitting(_isSubmitting);

            ClearError();
            UpdateValidation(string.Empty);
        }

        private void Start()
        {
            FocusInput();
        }

        private void OnDisable()
        {
            if (nicknameInput != null)
            {
                nicknameInput.onValueChanged.RemoveListener(OnNicknameChanged);
                nicknameInput.onSubmit.RemoveListener(OnInputSubmitted);
            }
            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(OnSubmitClicked);
            }
            if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
        }

        public void FocusInput()
        {
            if (nicknameInput != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(nicknameInput.gameObject);
                nicknameInput.ActivateInputField();
            }
        }

        private void OnNicknameChanged(string value)
        {
            ClearError();
            UpdateValidation(value);
        }

        private void UpdateValidation(string nickname)
        {
            if (validationText == null) return;

            if (string.IsNullOrEmpty(nickname))
            {
                validationText.text = RuleMessage;
                validationText.color = ruleColor;
                SetSubmitInteractable(false);
                return;
            }

            var result = NicknameValidator.Validate(nickname);
            if (result.IsValid)
            {
                validationText.text = $"{nickname.Trim().Length}/{NicknameValidator.MaxLength}자";
                validationText.color = validColor;
                SetSubmitInteractable(true);
            }
            else
            {
                // The rule stays in red; the reason goes to the error line with its warning icon.
                validationText.text = RuleMessage;
                validationText.color = invalidColor;
                ShowError(result.ErrorMessage);
                SetSubmitInteractable(false);
            }
        }

        private void SetSubmitInteractable(bool interactable)
        {
            if (submitButton != null)
            {
                submitButton.interactable = interactable && !_isSubmitting;
            }
        }

        // Enter in the field submits like the button, only when the button would accept it.
        private void OnInputSubmitted(string _)
        {
            if (submitButton != null && submitButton.IsInteractable()) OnSubmitClicked();
        }

        private void OnBackClicked()
        {
            if (!_isSubmitting) OnBackRequested?.Invoke();
        }

        private void SetSubmitting(bool submitting)
        {
            _isSubmitting = submitting;
            if (progressText != null)
            {
                progressText.text = SubmittingText;
                progressText.gameObject.SetActive(submitting);
            }
            if (backButton != null) backButton.interactable = !submitting;
        }

        private void OnSubmitClicked()
        {
            if (_isSubmitting) return;

            string nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
            var validation = NicknameValidator.Validate(nickname);
            if (!validation.IsValid)
            {
                ShowError(validation.ErrorMessage);
                return;
            }

            SubmitRegistration(nickname);
        }

        private void SubmitRegistration(string nickname)
        {
            SetSubmitting(true);
            SetSubmitInteractable(false);
            ClearError();

            var request = new CreateUserRequest
            {
                clientProfileId = LocalProfile.ClientProfileId,
                nickname = nickname
            };

            IUserRegistrationClient registrationClient = _registrationClient
                ?? ApiClient.Instance as IUserRegistrationClient;
            IGameApiClient apiClient = _apiClient ?? ApiClient.Instance;

            // Offline-0: with the registration client (or no client at all) an unreachable server keeps the
            // nickname on this PC and play starts anyway.
            if (registrationClient != null || apiClient == null)
            {
                ProfileRegistration.Register(registrationClient, nickname, OnRegistrationFinished);
                return;
            }

            apiClient.PostUser(request, OnRegistrationSuccess, OnRegistrationError);
        }

        private void OnRegistrationFinished(ProfileRegistrationOutcome outcome, RegistrationError? error)
        {
            SetSubmitting(false);
            if (outcome == ProfileRegistrationOutcome.Registered ||
                outcome == ProfileRegistrationOutcome.SavedOffline)
            {
                OnRegistrationComplete?.Invoke(new CreateUserData
                {
                    id = LocalProfile.UserId,
                    clientProfileId = LocalProfile.ClientProfileId,
                    nickname = LocalProfile.Nickname
                });
                return;
            }

            ShowError(error.HasValue
                ? GetUserFriendlyMessage(error.Value)
                : "알 수 없는 오류가 발생했습니다. 다시 시도해주세요.");
            SetSubmitInteractable(true);
        }

        // Offline-0: the server reported that the nickname chosen offline is taken.
        public void ShowNicknameTakenNotice(string takenNickname)
        {
            ShowError($"'{takenNickname}'은(는) 이미 다른 사람이 쓰고 있어요. 새 닉네임을 정해주세요.");
        }

        private void OnRegistrationSuccess(CreateUserResponse response)
        {
            SetSubmitting(false);

            if (response.data == null)
            {
                ShowError("응답 데이터가 없습니다.");
                SetSubmitInteractable(true);
                return;
            }

            LocalProfile.SaveProfile(response.data.id, response.data.nickname);
            OnRegistrationComplete?.Invoke(response.data);
        }

        private void OnRegistrationError(string error)
        {
            SetSubmitting(false);
            ShowError(error);
            SetSubmitInteractable(true);
        }

        private static string GetUserFriendlyMessage(RegistrationError error)
        {
            return error.Type switch
            {
                RegistrationErrorType.NicknameExists =>
                    "이미 사용 중인 닉네임입니다. 다른 닉네임을 입력해주세요.",
                RegistrationErrorType.ProfileConflict =>
                    "프로필 등록 충돌이 발생했습니다. 다시 시도해주세요.",
                RegistrationErrorType.ValidationError =>
                    "닉네임 형식이 올바르지 않습니다. 한글, 영문, 숫자만 2~12자로 입력해주세요.",
                RegistrationErrorType.NetworkError =>
                    "네트워크 연결을 확인해주세요. 잠시 후 다시 시도해주세요.",
                RegistrationErrorType.ServerError =>
                    "서버에 일시적인 문제가 발생했습니다. 잠시 후 다시 시도해주세요.",
                RegistrationErrorType.ParseError or RegistrationErrorType.Unknown =>
                    "알 수 없는 오류가 발생했습니다. 다시 시도해주세요.",
                _ => "알 수 없는 오류가 발생했습니다. 다시 시도해주세요."
            };
        }

        // Public for the Menu-2 review capture, which shows a sample error without Play Mode.
        public void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.gameObject.SetActive(true);
                PlaceErrorIcon(message);
            }
        }

        // The error text is centred, so its icon (a child named WarningIcon) moves to just left of the first glyph.
        private void PlaceErrorIcon(string message)
        {
            var icon = errorText.transform.Find("WarningIcon") as RectTransform;
            if (icon == null) return;
            float box = errorText.rectTransform.rect.width;
            float width = Mathf.Min(errorText.GetPreferredValues(message, box, Mathf.Infinity).x, box);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(1f, 0.5f);
            icon.anchoredPosition = new Vector2(-width / 2f - ErrorIconGap, 0f);
        }

        private const float ErrorIconGap = 10f;

        private void ClearError()
        {
            if (errorText != null)
            {
                errorText.text = string.Empty;
                errorText.gameObject.SetActive(false);
            }
        }
    }
}
