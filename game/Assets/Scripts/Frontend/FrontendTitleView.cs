using System;
using TMPro;
using TrickalFanGame.Data;
using TrickalFanGame.Network;
using TrickalFanGame.Run;
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
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Nickname Registration")]
        [SerializeField] private GameObject nicknamePanel;
        [SerializeField] private FrontendNicknameView nicknameView;

        [Header("Home Screen")]
        [SerializeField] private FrontendHomeView homeView;

        public Button StartButton => startButton;
        public TMP_Text StatusText => statusText;
        public FrontendNicknameView NicknameView => nicknameView;
        public GameObject TitlePanel => titlePanel;
        public FrontendHomeView HomeView => homeView;

        public event Action OnHomeRequested;

        public void Configure(Button button, TMP_Text status)
        {
            startButton = button;
            statusText = status;
        }

        public void ConfigureTitlePanel(GameObject panel)
        {
            titlePanel = panel;
        }

        public void ConfigureNicknamePanel(GameObject panel, FrontendNicknameView view)
        {
            nicknamePanel = panel;
            nicknameView = view;
        }

        public void ConfigureHomeView(FrontendHomeView view)
        {
            homeView = view;
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
            if (FrontendEntryContext.TryConsumeHome())
            {
                ShowHomeScreen();
                OnHomeRequested?.Invoke();
                return;
            }
            ShowTitleScreen();
        }

        public void ShowTitleScreen()
        {
            if (titlePanel != null) titlePanel.SetActive(true);
            if (nicknamePanel != null) nicknamePanel.SetActive(false);
            if (homeView != null) homeView.Hide();
            if (startButton != null) startButton.gameObject.SetActive(true);
            FocusStart();
        }

        public void ShowNicknameRegistration()
        {
            if (titlePanel != null) titlePanel.SetActive(false);
            if (startButton != null) startButton.gameObject.SetActive(false);
            if (homeView != null) homeView.Hide();
            if (nicknamePanel != null) nicknamePanel.SetActive(true);
            if (nicknameView != null) nicknameView.FocusInput();
        }

        public void ShowHomeScreen()
        {
            if (!LocalProfile.IsRegistered) return;

            if (titlePanel != null) titlePanel.SetActive(false);
            if (nicknamePanel != null) nicknamePanel.SetActive(false);
            if (homeView != null) homeView.Show(LocalProfile.Nickname);
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

            ShowHomeScreen();
            OnHomeRequested?.Invoke();
        }

        private void OnNicknameRegistered(CreateUserData userData)
        {
            ShowHomeScreen();
            OnHomeRequested?.Invoke();
        }
    }
}
