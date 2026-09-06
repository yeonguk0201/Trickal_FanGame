using System;
using TMPro;
using TrickalFanGame.Data;
using TrickalFanGame.Network;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public sealed class FrontendTitleView : MonoBehaviour
    {
        public const string ReadyMessage = "닉네임 등록과 홈 화면은 준비 중입니다.";

        [Header("Title Screen")]
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Nickname Registration")]
        [SerializeField] private GameObject nicknamePanel;
        [SerializeField] private FrontendNicknameView nicknameView;

        public Button StartButton => startButton;
        public TMP_Text StatusText => statusText;
        public FrontendNicknameView NicknameView => nicknameView;

        public event Action OnHomeRequested;

        public void Configure(Button button, TMP_Text status)
        {
            startButton = button;
            statusText = status;
        }

        public void ConfigureNicknamePanel(GameObject panel, FrontendNicknameView view)
        {
            nicknamePanel = panel;
            nicknameView = view;
        }

        private void OnEnable()
        {
            if (startButton != null) startButton.onClick.AddListener(RequestStart);
            if (nicknameView != null) nicknameView.OnRegistrationComplete += OnNicknameRegistered;
        }

        private void Start()
        {
            InitializeView();
        }

        private void Update()
        {
            if (Keyboard.current?.tabKey.wasPressedThisFrame == true) FocusStart();
        }

        private void OnDisable()
        {
            if (startButton != null) startButton.onClick.RemoveListener(RequestStart);
            if (nicknameView != null) nicknameView.OnRegistrationComplete -= OnNicknameRegistered;
        }

        private void InitializeView()
        {
            if (LocalProfile.IsRegistered)
            {
                ShowTitleScreen();
            }
            else
            {
                ShowNicknameRegistration();
            }
        }

        public void ShowTitleScreen()
        {
            if (nicknamePanel != null) nicknamePanel.SetActive(false);
            if (startButton != null) startButton.gameObject.SetActive(true);
            FocusStart();
        }

        public void ShowNicknameRegistration()
        {
            if (startButton != null) startButton.gameObject.SetActive(false);
            if (nicknamePanel != null) nicknamePanel.SetActive(true);
            if (nicknameView != null) nicknameView.FocusInput();
        }

        public void FocusStart()
        {
            if (startButton != null && startButton.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        public void RequestStart()
        {
            if (!LocalProfile.IsRegistered)
            {
                ShowNicknameRegistration();
                return;
            }

            OnHomeRequested?.Invoke();
            if (statusText != null) statusText.text = ReadyMessage;
        }

        private void OnNicknameRegistered(CreateUserData userData)
        {
            ShowTitleScreen();
            OnHomeRequested?.Invoke();
        }
    }
}
