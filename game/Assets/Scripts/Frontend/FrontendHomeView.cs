using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public enum FrontendDestination
    {
        CharacterSelection,
        SkillUpgrade,
        Settings
    }

    public sealed class FrontendHomeView : MonoBehaviour
    {
        public const string CharacterSelectionTitle = "캐릭터 선택";
        public const string SkillUpgradeTitle = "스킬 강화";
        public const string SettingsTitle = "설정";
        public const string PreparationMessage = "다음 작업에서 기능이 연결됩니다.";
        public const string QuitMessage = "게임을 종료합니다.";

        [SerializeField] private GameObject homePanel;
        [SerializeField] private TMP_Text welcomeText;
        [SerializeField] private Button gameStartButton;
        [SerializeField] private Button skillUpgradeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject destinationPanel;
        [SerializeField] private TMP_Text destinationTitle;
        [SerializeField] private TMP_Text destinationMessage;
        [SerializeField] private Button backButton;

        [Header("Settings")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private FrontendSettingsView settingsView;

        [Header("Character Selection")]
        [SerializeField] private GameObject characterSelectionPanel;
        [SerializeField] private FrontendCharacterSelectionView characterSelectionView;

        [Header("Skill Upgrade")]
        [SerializeField] private GameObject skillUpgradePanel;
        [SerializeField] private FrontendSkillUpgradeView skillUpgradeView;

        public GameObject HomePanel => homePanel;
        public TMP_Text WelcomeText => welcomeText;
        public Button GameStartButton => gameStartButton;
        public Button SkillUpgradeButton => skillUpgradeButton;
        public Button SettingsButton => settingsButton;
        public Button QuitButton => quitButton;
        public GameObject DestinationPanel => destinationPanel;
        public TMP_Text DestinationTitle => destinationTitle;
        public Button BackButton => backButton;
        public GameObject SettingsPanel => settingsPanel;
        public FrontendSettingsView SettingsView => settingsView;
        public GameObject CharacterSelectionPanel => characterSelectionPanel;
        public FrontendCharacterSelectionView CharacterSelectionView => characterSelectionView;
        public GameObject SkillUpgradePanel => skillUpgradePanel;
        public FrontendSkillUpgradeView SkillUpgradeView => skillUpgradeView;
        public FrontendDestination? CurrentDestination { get; private set; }

        public event Action<FrontendDestination> OnDestinationRequested;
        public event Action OnQuitRequested;
        public event Action<string> OnCharacterConfirmed;

        public void Configure(
            GameObject home,
            TMP_Text welcome,
            Button gameStart,
            Button skillUpgrade,
            Button settings,
            Button quit,
            GameObject destination,
            TMP_Text destinationHeading,
            TMP_Text message,
            Button back)
        {
            homePanel = home;
            welcomeText = welcome;
            gameStartButton = gameStart;
            skillUpgradeButton = skillUpgrade;
            settingsButton = settings;
            quitButton = quit;
            destinationPanel = destination;
            destinationTitle = destinationHeading;
            destinationMessage = message;
            backButton = back;
        }

        public void ConfigureSettings(GameObject settings, FrontendSettingsView view)
        {
            settingsPanel = settings;
            settingsView = view;
        }

        public void ConfigureCharacterSelection(GameObject panel, FrontendCharacterSelectionView view)
        {
            characterSelectionPanel = panel;
            characterSelectionView = view;
        }

        public void ConfigureSkillUpgrade(GameObject panel, FrontendSkillUpgradeView view)
        {
            skillUpgradePanel = panel;
            skillUpgradeView = view;
        }

        private void OnEnable()
        {
            if (gameStartButton != null) gameStartButton.onClick.AddListener(RequestCharacterSelection);
            if (skillUpgradeButton != null) skillUpgradeButton.onClick.AddListener(RequestSkillUpgrade);
            if (settingsButton != null) settingsButton.onClick.AddListener(RequestSettings);
            if (quitButton != null) quitButton.onClick.AddListener(RequestQuit);
            if (backButton != null) backButton.onClick.AddListener(ReturnHome);
            if (settingsView != null) settingsView.OnBackRequested += ReturnHomeFromSettings;
            if (characterSelectionView != null)
            {
                characterSelectionView.OnBackRequested += ReturnHomeFromCharacterSelection;
                characterSelectionView.OnCharacterConfirmed += ForwardCharacterConfirmation;
                characterSelectionView.OnUpgradeCharacterConfirmed += ShowSkillUpgrade;
            }
            if (skillUpgradeView != null) skillUpgradeView.OnBackRequested += ReturnToSkillCharacterSelection;
        }

        private void OnDisable()
        {
            if (gameStartButton != null) gameStartButton.onClick.RemoveListener(RequestCharacterSelection);
            if (skillUpgradeButton != null) skillUpgradeButton.onClick.RemoveListener(RequestSkillUpgrade);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(RequestSettings);
            if (quitButton != null) quitButton.onClick.RemoveListener(RequestQuit);
            if (backButton != null) backButton.onClick.RemoveListener(ReturnHome);
            if (settingsView != null) settingsView.OnBackRequested -= ReturnHomeFromSettings;
            if (characterSelectionView != null)
            {
                characterSelectionView.OnBackRequested -= ReturnHomeFromCharacterSelection;
                characterSelectionView.OnCharacterConfirmed -= ForwardCharacterConfirmation;
                characterSelectionView.OnUpgradeCharacterConfirmed -= ShowSkillUpgrade;
            }
            if (skillUpgradeView != null) skillUpgradeView.OnBackRequested -= ReturnToSkillCharacterSelection;
        }

        public void Show(string nickname)
        {
            gameObject.SetActive(true);
            CurrentDestination = null;
            if (homePanel != null) homePanel.SetActive(true);
            if (destinationPanel != null) destinationPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (characterSelectionPanel != null) characterSelectionPanel.SetActive(false);
            if (skillUpgradePanel != null) skillUpgradePanel.SetActive(false);
            if (welcomeText != null) welcomeText.text = $"{nickname} 님, 환영합니다";
            Focus(gameStartButton);
        }

        public void Hide()
        {
            CurrentDestination = null;
            if (characterSelectionView != null) characterSelectionView.Hide();
            if (skillUpgradeView != null) skillUpgradeView.Hide();
            gameObject.SetActive(false);
        }

        public void RequestCharacterSelection()
        {
            CurrentDestination = FrontendDestination.CharacterSelection;
            if (homePanel != null) homePanel.SetActive(false);
            if (destinationPanel != null) destinationPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (skillUpgradeView != null) skillUpgradeView.Hide();
            if (characterSelectionView != null) characterSelectionView.Show(FrontendCharacterSelectionPurpose.StartRun);
            OnDestinationRequested?.Invoke(FrontendDestination.CharacterSelection);
        }
        public void RequestSkillUpgrade()
        {
            CurrentDestination = FrontendDestination.SkillUpgrade;
            if (homePanel != null) homePanel.SetActive(false);
            if (destinationPanel != null) destinationPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (skillUpgradeView != null) skillUpgradeView.Hide();
            if (characterSelectionView != null)
                characterSelectionView.Show(FrontendCharacterSelectionPurpose.UpgradeSkills);
            OnDestinationRequested?.Invoke(FrontendDestination.SkillUpgrade);
        }

        public void RequestSettings()
        {
            if (settingsPanel != null && settingsView != null)
            {
                CurrentDestination = FrontendDestination.Settings;
                if (homePanel != null) homePanel.SetActive(false);
                if (destinationPanel != null) destinationPanel.SetActive(false);
                settingsPanel.SetActive(true);
                settingsView.FocusMasterSlider();
                OnDestinationRequested?.Invoke(FrontendDestination.Settings);
            }
            else
            {
                ShowDestination(FrontendDestination.Settings);
            }
        }

        public void ReturnHome()
        {
            CurrentDestination = null;
            if (homePanel != null) homePanel.SetActive(true);
            if (destinationPanel != null) destinationPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (characterSelectionView != null) characterSelectionView.Hide();
            if (skillUpgradeView != null) skillUpgradeView.Hide();
            Focus(gameStartButton);
        }

        private void ReturnHomeFromSettings()
        {
            ReturnHome();
        }

        private void ReturnHomeFromCharacterSelection() => ReturnHome();
        private void ForwardCharacterConfirmation(string characterId) => OnCharacterConfirmed?.Invoke(characterId);

        private void ShowSkillUpgrade(TrickalFanGame.Character.CharacterDefinition character)
        {
            characterSelectionView.Hide();
            skillUpgradeView.Show(character);
        }

        private void ReturnToSkillCharacterSelection()
        {
            skillUpgradeView.Hide();
            characterSelectionView.Show(FrontendCharacterSelectionPurpose.UpgradeSkills);
        }

        public void RequestQuit()
        {
            if (destinationMessage != null) destinationMessage.text = QuitMessage;
            OnQuitRequested?.Invoke();
            Application.Quit();
        }

        private void ShowDestination(FrontendDestination destination)
        {
            CurrentDestination = destination;
            if (homePanel != null) homePanel.SetActive(false);
            if (destinationPanel != null) destinationPanel.SetActive(true);
            if (characterSelectionPanel != null) characterSelectionPanel.SetActive(false);
            if (skillUpgradePanel != null) skillUpgradePanel.SetActive(false);
            if (destinationTitle != null) destinationTitle.text = GetTitle(destination);
            if (destinationMessage != null) destinationMessage.text = PreparationMessage;
            Focus(backButton);
            OnDestinationRequested?.Invoke(destination);
        }

        private static string GetTitle(FrontendDestination destination)
        {
            return destination switch
            {
                FrontendDestination.CharacterSelection => CharacterSelectionTitle,
                FrontendDestination.SkillUpgrade => SkillUpgradeTitle,
                FrontendDestination.Settings => SettingsTitle,
                _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, null)
            };
        }

        private static void Focus(Selectable selectable)
        {
            if (selectable != null && selectable.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
