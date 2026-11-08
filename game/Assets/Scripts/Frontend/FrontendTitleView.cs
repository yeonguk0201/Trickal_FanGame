using System;
using TMPro;
using TrickalFanGame.Data;
using TrickalFanGame.Meta;
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

        [Header("Credits")]
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private FrontendCreditsView creditsView;
        [SerializeField] private Button creditsButton;

        public Button StartButton => startButton;
        public TMP_Text StatusText => statusText;
        public FrontendNicknameView NicknameView => nicknameView;
        public GameObject TitlePanel => titlePanel;
        public FrontendHomeView HomeView => homeView;
        public GameObject CreditsPanel => creditsPanel;
        public FrontendCreditsView CreditsView => creditsView;
        public Button CreditsButton => creditsButton;
        public bool CreditsReturnToHome { get; private set; }

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

        public void ConfigureCredits(GameObject panel, FrontendCreditsView view, Button button)
        {
            creditsPanel = panel;
            creditsView = view;
            creditsButton = button;
        }

        private void OnEnable()
        {
            if (startButton != null) startButton.onClick.AddListener(RequestStart);
            if (nicknameView != null) nicknameView.OnRegistrationComplete += OnNicknameRegistered;
            if (creditsButton != null) creditsButton.onClick.AddListener(ShowCreditsFromTitle);
            if (creditsView != null) creditsView.OnBackRequested += CloseCredits;
            if (homeView != null) homeView.OnCreditsRequested += ShowCreditsFromHome;
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
            if (creditsButton != null) creditsButton.onClick.RemoveListener(ShowCreditsFromTitle);
            if (creditsView != null) creditsView.OnBackRequested -= CloseCredits;
            if (homeView != null) homeView.OnCreditsRequested -= ShowCreditsFromHome;
        }

        private void InitializeView()
        {
            if (!SyncOfflineProfile()) RecoverPendingRuns();

            if (FrontendEntryContext.TryConsumeHome())
            {
                ShowHomeScreen();
                OnHomeRequested?.Invoke();
                return;
            }
            ShowTitleScreen();
        }

        public ProfileRegistrationOutcome? LastProfileSync { get; private set; }

        // Offline-0: a profile kept on this PC tries to register on each launch. Pending Runs are sent after it
        // succeeds. Returns false when there was nothing to register.
        public bool SyncOfflineProfile(IUserRegistrationClient overrideClient = null,
            IGameApiClient recoveryClient = null)
        {
            IUserRegistrationClient client = overrideClient ?? ApiClient.Instance as IUserRegistrationClient;
            return ProfileRegistration.TrySyncOfflineProfile(client, outcome =>
            {
                LastProfileSync = outcome;
                Debug.Log($"[FrontendTitleView] Offline profile sync: {outcome}");
                if (outcome == ProfileRegistrationOutcome.Registered) RecoverPendingRuns(recoveryClient);
            });
        }

        public PendingSyncOutcome? LastPendingSync { get; private set; }

        // Offline-1: sends this profile's queued Runs and skill upgrades oldest first (PendingSync). A failure keeps
        // the rest queued for the next launch.
        public void RecoverPendingRuns(IGameApiClient overrideClient = null)
        {
            if (RunResultContext.HasPending || !LocalProfile.HasServerAccount) return;

            ProfileRegistration.MovePendingRunsToServerAccount();
            if (!LocalProgression.HasPendingWrites()) return;

            IGameApiClient apiClient = overrideClient ?? ApiClient.Instance;
            if (apiClient == null)
            {
                Debug.LogWarning("[FrontendTitleView] ApiClient not available for recovery.");
                return;
            }

            Debug.Log("[FrontendTitleView] Sending pending Runs and skill upgrades.");
            PendingSync.Flush(apiClient, apiClient as ISkillProgressApiClient ?? ApiClient.Instance, outcome =>
            {
                LastPendingSync = outcome;
                Debug.Log($"[FrontendTitleView] Pending writes: {outcome}");
            });
        }

        public void ShowTitleScreen()
        {
            if (titlePanel != null) titlePanel.SetActive(true);
            if (nicknamePanel != null) nicknamePanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (homeView != null) homeView.Hide();
            if (startButton != null) startButton.gameObject.SetActive(true);
            FocusStart();
        }

        public void ShowNicknameRegistration()
        {
            if (titlePanel != null) titlePanel.SetActive(false);
            if (startButton != null) startButton.gameObject.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (homeView != null) homeView.Hide();
            if (nicknamePanel != null) nicknamePanel.SetActive(true);
            if (nicknameView != null)
            {
                if (LocalProfile.NeedsNewNickname) nicknameView.ShowNicknameTakenNotice(LocalProfile.Nickname);
                nicknameView.FocusInput();
            }
        }

        public void ShowHomeScreen()
        {
            if (!LocalProfile.IsRegistered) return;

            if (titlePanel != null) titlePanel.SetActive(false);
            if (nicknamePanel != null) nicknamePanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (homeView != null) homeView.Show(LocalProfile.Nickname);
        }

        private void ShowCreditsFromTitle() => ShowCredits(false);
        private void ShowCreditsFromHome() => ShowCredits(true);

        // Credits are reachable from the title screen even before registration: a CC BY 4.0 credit has to
        // stay findable for anyone running the game, not only for a player who already has a profile.
        public void ShowCredits(bool returnToHome)
        {
            CreditsReturnToHome = returnToHome;
            if (titlePanel != null) titlePanel.SetActive(false);
            if (nicknamePanel != null) nicknamePanel.SetActive(false);
            if (startButton != null) startButton.gameObject.SetActive(false);
            if (homeView != null) homeView.Hide();
            if (creditsPanel != null) creditsPanel.SetActive(true);
            if (creditsView != null) creditsView.Show();
        }

        public void CloseCredits()
        {
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (CreditsReturnToHome && LocalProfile.IsRegistered) ShowHomeScreen();
            else ShowTitleScreen();
            CreditsReturnToHome = false;
        }

        public void FocusStart()
        {
            if (startButton != null && startButton.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        public void RequestStart()
        {
            if (!LocalProfile.IsRegistered || LocalProfile.NeedsNewNickname)
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
