using System;
using TMPro;
using TrickalFanGame.Data;
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

        private bool _isSubmitting;
        private IGameApiClient _apiClient;

        public event Action<CreateUserData> OnRegistrationComplete;

        public TMP_InputField NicknameInput => nicknameInput;
        public TMP_Text ValidationText => validationText;
        public TMP_Text ErrorText => errorText;
        public Button SubmitButton => submitButton;

        public void Configure(TMP_InputField input, TMP_Text validation, TMP_Text error, Button submit)
        {
            nicknameInput = input;
            validationText = validation;
            errorText = error;
            submitButton = submit;
        }

        public void SetApiClient(IGameApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        private void OnEnable()
        {
            if (nicknameInput != null)
            {
                nicknameInput.onValueChanged.AddListener(OnNicknameChanged);
                nicknameInput.characterLimit = NicknameValidator.MaxLength;
            }
            if (submitButton != null)
            {
                submitButton.onClick.AddListener(OnSubmitClicked);
            }

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
            }
            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(OnSubmitClicked);
            }
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
                validationText.text = $"닉네임 ({NicknameValidator.MinLength}~{NicknameValidator.MaxLength}자)";
                validationText.color = Color.gray;
                SetSubmitInteractable(false);
                return;
            }

            var result = NicknameValidator.Validate(nickname);
            if (result.IsValid)
            {
                validationText.text = $"{nickname.Trim().Length}/{NicknameValidator.MaxLength}자";
                validationText.color = Color.green;
                SetSubmitInteractable(true);
            }
            else
            {
                validationText.text = result.ErrorMessage;
                validationText.color = Color.red;
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
            _isSubmitting = true;
            SetSubmitInteractable(false);
            ClearError();

            var request = new CreateUserRequest
            {
                clientProfileId = LocalProfile.ClientProfileId,
                nickname = nickname
            };

            IGameApiClient apiClient = _apiClient ?? ApiClient.Instance;
            if (apiClient == null)
            {
                ShowError("API 클라이언트를 찾을 수 없습니다.");
                _isSubmitting = false;
                SetSubmitInteractable(true);
                return;
            }

            apiClient.PostUser(request, OnRegistrationSuccess, OnRegistrationError);
        }

        private void OnRegistrationSuccess(CreateUserResponse response)
        {
            _isSubmitting = false;

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
            _isSubmitting = false;
            ShowError(error);
            SetSubmitInteractable(true);
        }

        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.gameObject.SetActive(true);
            }
        }

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
