using System.Collections.Generic;
using TMPro;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class GamePauseArtifactView : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private RectTransform entriesRoot;
        [SerializeField] private ArtifactPauseListEntryView entryTemplate;
        [SerializeField] private TMP_Text emptyText;
        [Header("HUD-7B Pause Menu")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private RectTransform focusScope;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [Header("HUD-7C Leave Run")]
        [SerializeField] private RunSession runSession;
        [SerializeField] private Button leaveRunButton;
        [SerializeField] private CanvasGroup leaveConfirmation;
        [SerializeField] private Button confirmLeaveButton;
        [SerializeField] private Button cancelLeaveButton;
        [SerializeField] private RectTransform confirmationFocusScope;
        [Header("HUD-7D Restart Run")]
        [SerializeField] private Button restartRunButton;
        [SerializeField] private TMP_Text confirmationTitle;
        [SerializeField] private TMP_Text confirmationWarning;
        [SerializeField] private TMP_Text confirmationActionLabel;

        private readonly List<ArtifactPauseListEntryView> entries = new();
        private float previousTimeScale = 1f;
        private GameObject previousSelectedObject;
        private PendingRunAction pendingRunAction;

        private enum PendingRunAction
        {
            None,
            Leave,
            Restart
        }

        public PlayerInventory Inventory => inventory;
        public CanvasGroup Overlay => overlay;
        public RectTransform EntriesRoot => entriesRoot;
        public ArtifactPauseListEntryView EntryTemplate => entryTemplate;
        public TMP_Text EmptyText => emptyText;
        public PlayerStats PlayerStats => playerStats;
        public TMP_Text StatsText => statsText;
        public Button ResumeButton => resumeButton;
        public RectTransform FocusScope => focusScope;
        public GraphicRaycaster GraphicRaycaster => graphicRaycaster;
        public RunSession RunSession => runSession;
        public Button LeaveRunButton => leaveRunButton;
        public CanvasGroup LeaveConfirmation => leaveConfirmation;
        public Button ConfirmLeaveButton => confirmLeaveButton;
        public Button CancelLeaveButton => cancelLeaveButton;
        public RectTransform ConfirmationFocusScope => confirmationFocusScope;
        public Button RestartRunButton => restartRunButton;
        public TMP_Text ConfirmationTitle => confirmationTitle;
        public TMP_Text ConfirmationWarning => confirmationWarning;
        public TMP_Text ConfirmationActionLabel => confirmationActionLabel;
        public IReadOnlyList<ArtifactPauseListEntryView> Entries => entries;
        public bool IsPaused { get; private set; }
        public bool IsConfirmingLeave => pendingRunAction == PendingRunAction.Leave;
        public bool IsConfirmingRestart => pendingRunAction == PendingRunAction.Restart;
        public int EntryCount => entries.Count;

        public void Configure(PlayerInventory configuredInventory, CanvasGroup configuredOverlay,
            RectTransform configuredEntriesRoot, ArtifactPauseListEntryView configuredEntryTemplate,
            TMP_Text configuredEmptyText)
        {
            inventory = configuredInventory;
            overlay = configuredOverlay;
            entriesRoot = configuredEntriesRoot;
            entryTemplate = configuredEntryTemplate;
            emptyText = configuredEmptyText;
            ApplyVisibility(false);
        }

        public void ConfigureMenu(PlayerStats configuredPlayerStats, TMP_Text configuredStatsText,
            Button configuredResumeButton, RectTransform configuredFocusScope,
            GraphicRaycaster configuredGraphicRaycaster)
        {
            UnbindResumeButton();
            playerStats = configuredPlayerStats;
            statsText = configuredStatsText;
            resumeButton = configuredResumeButton;
            focusScope = configuredFocusScope;
            graphicRaycaster = configuredGraphicRaycaster;
            BindResumeButton();
            if (!IsPaused && graphicRaycaster != null) graphicRaycaster.enabled = false;
        }

        public void ConfigureLeaveRun(RunSession configuredRunSession, Button configuredLeaveRunButton,
            CanvasGroup configuredLeaveConfirmation, Button configuredConfirmLeaveButton,
            Button configuredCancelLeaveButton, RectTransform configuredConfirmationFocusScope)
        {
            UnbindLeaveButtons();
            runSession = configuredRunSession;
            leaveRunButton = configuredLeaveRunButton;
            leaveConfirmation = configuredLeaveConfirmation;
            confirmLeaveButton = configuredConfirmLeaveButton;
            cancelLeaveButton = configuredCancelLeaveButton;
            confirmationFocusScope = configuredConfirmationFocusScope;
            BindLeaveButtons();
            ApplyLeaveConfirmationVisibility(false);
        }

        public void ConfigureRestartRun(Button configuredRestartRunButton, TMP_Text configuredConfirmationTitle,
            TMP_Text configuredConfirmationWarning, TMP_Text configuredConfirmationActionLabel)
        {
            UnbindRestartButton();
            restartRunButton = configuredRestartRunButton;
            confirmationTitle = configuredConfirmationTitle;
            confirmationWarning = configuredConfirmationWarning;
            confirmationActionLabel = configuredConfirmationActionLabel;
            BindRestartButton();
        }

        private void Awake()
        {
            BindResumeButton();
            BindLeaveButtons();
            BindRestartButton();
            ApplyVisibility(false);
            ApplyLeaveConfirmationVisibility(false);
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (pendingRunAction != PendingRunAction.None) CancelRunActionConfirmation();
            else if (IsPaused) Resume();
            else TryPause();
        }

        private void LateUpdate()
        {
            bool confirmingAction = pendingRunAction != PendingRunAction.None;
            RectTransform activeScope = confirmingAction ? confirmationFocusScope : focusScope;
            Button fallback = confirmingAction ? cancelLeaveButton : resumeButton;
            if (!IsPaused || activeScope == null || fallback == null || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.transform.IsChildOf(activeScope)) Focus(fallback);
        }

        private void OnDisable()
        {
            if (IsPaused) Resume();
        }

        private void OnDestroy()
        {
            UnbindResumeButton();
            UnbindLeaveButtons();
            UnbindRestartButton();
        }

        public bool TryPause()
        {
            if (IsPaused || Time.timeScale <= 0f) return false;
            previousTimeScale = Time.timeScale;
            previousSelectedObject = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            Time.timeScale = 0f;
            RebuildEntries();
            RefreshStats();
            IsPaused = true;
            ApplyVisibility(true);
            if (graphicRaycaster != null) graphicRaycaster.enabled = true;
            FocusResumeButton();
            return true;
        }

        public bool Resume()
        {
            if (!IsPaused) return false;
            IsPaused = false;
            pendingRunAction = PendingRunAction.None;
            ApplyLeaveConfirmationVisibility(false);
            ApplyVisibility(false);
            if (graphicRaycaster != null) graphicRaycaster.enabled = false;
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            RestorePreviousFocus();
            return true;
        }

        public void RefreshStats()
        {
            if (statsText == null) return;
            statsText.text = playerStats == null
                ? "전투 스탯을 불러올 수 없습니다."
                : $"공격력  {playerStats.AttackDamage:0.##}\n" +
                  $"공격속도  {playerStats.AttackSpeed:0.##}\n" +
                  $"이동속도  {playerStats.MoveSpeed:0.##}\n" +
                  $"치명타율  {playerStats.CriticalChance * 100f:0.#}%\n" +
                  $"치명타 피해  {playerStats.CriticalDamageMultiplier * 100f:0.#}%\n" +
                  $"투사체  {playerStats.ProjectileCount}\n" +
                  $"관통  {playerStats.PierceCount}";
        }

        public void RebuildEntries()
        {
            ClearEntries();
            if (inventory != null && entriesRoot != null && entryTemplate != null)
            {
                foreach (ItemDefinition definition in inventory.AcquiredDefinitions)
                {
                    ArtifactPauseListEntryView entry = Instantiate(entryTemplate, entriesRoot);
                    entry.name = "Item Detail " + definition.ItemId;
                    entry.gameObject.SetActive(true);
                    entry.Bind(definition, inventory.GetStackCount(definition.ItemId));
                    entries.Add(entry);
                }
            }

            if (emptyText != null) emptyText.gameObject.SetActive(entries.Count == 0);
        }

        private void ClearEntries()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                ArtifactPauseListEntryView entry = entries[i];
                if (entry == null) continue;
                entry.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(entry.gameObject);
                else DestroyImmediate(entry.gameObject);
            }
            entries.Clear();
        }

        private void ApplyVisibility(bool visible)
        {
            if (overlay == null) return;
            overlay.alpha = visible ? 1f : 0f;
            overlay.interactable = visible;
            overlay.blocksRaycasts = visible;
        }

        private void BindResumeButton()
        {
            if (resumeButton == null) return;
            resumeButton.onClick.RemoveListener(OnResumeClicked);
            resumeButton.onClick.AddListener(OnResumeClicked);
        }

        private void UnbindResumeButton()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumeClicked);
        }

        private void OnResumeClicked()
        {
            Resume();
        }

        public bool ShowLeaveConfirmation()
        {
            return ShowRunActionConfirmation(PendingRunAction.Leave);
        }

        public bool CancelLeaveConfirmation()
        {
            return IsConfirmingLeave && CancelRunActionConfirmation();
        }

        public bool ConfirmLeaveRun()
        {
            if (!IsPaused || !IsConfirmingLeave || runSession == null) return false;
            return runSession.TryAbandonToHome();
        }

        public bool ShowRestartConfirmation()
        {
            return ShowRunActionConfirmation(PendingRunAction.Restart);
        }

        public bool ConfirmRestartRun()
        {
            if (!IsPaused || !IsConfirmingRestart || runSession == null) return false;
            return runSession.TryRestartRun();
        }

        private bool ShowRunActionConfirmation(PendingRunAction action)
        {
            if (!IsPaused || pendingRunAction != PendingRunAction.None || runSession == null ||
                runSession.HasEnded || action == PendingRunAction.None)
            {
                return false;
            }

            pendingRunAction = action;
            if (confirmationTitle != null)
                confirmationTitle.text = action == PendingRunAction.Restart
                    ? "처음부터 다시 시작할까요?"
                    : "현재 Run을 끝낼까요?";
            if (confirmationWarning != null)
                confirmationWarning.text = action == PendingRunAction.Restart
                    ? "현재 Run의 진행과 획득 아이템은 저장되지 않습니다.\n같은 캐릭터로 새 Run을 시작합니다."
                    : "지금까지의 진행과 획득 아이템은 저장되지 않습니다.\n홈으로 돌아가도 결과 보상은 지급되지 않습니다.";
            if (confirmationActionLabel != null)
                confirmationActionLabel.text = action == PendingRunAction.Restart ? "다시 시작" : "Run 끝내기";
            ApplyLeaveConfirmationVisibility(true);
            Focus(cancelLeaveButton);
            return true;
        }

        private bool CancelRunActionConfirmation()
        {
            if (pendingRunAction == PendingRunAction.None) return false;
            Button returnFocus = pendingRunAction == PendingRunAction.Restart ? restartRunButton : leaveRunButton;
            pendingRunAction = PendingRunAction.None;
            ApplyLeaveConfirmationVisibility(false);
            Focus(returnFocus);
            return true;
        }

        private void FocusResumeButton()
        {
            Focus(resumeButton);
        }

        private static void Focus(Button button)
        {
            if (button == null || !button.interactable || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private void BindLeaveButtons()
        {
            if (leaveRunButton != null)
            {
                leaveRunButton.onClick.RemoveListener(OnLeaveRunClicked);
                leaveRunButton.onClick.AddListener(OnLeaveRunClicked);
            }
            if (confirmLeaveButton != null)
            {
                confirmLeaveButton.onClick.RemoveListener(OnConfirmLeaveClicked);
                confirmLeaveButton.onClick.AddListener(OnConfirmLeaveClicked);
            }
            if (cancelLeaveButton != null)
            {
                cancelLeaveButton.onClick.RemoveListener(OnCancelLeaveClicked);
                cancelLeaveButton.onClick.AddListener(OnCancelLeaveClicked);
            }
        }

        private void UnbindLeaveButtons()
        {
            if (leaveRunButton != null) leaveRunButton.onClick.RemoveListener(OnLeaveRunClicked);
            if (confirmLeaveButton != null) confirmLeaveButton.onClick.RemoveListener(OnConfirmLeaveClicked);
            if (cancelLeaveButton != null) cancelLeaveButton.onClick.RemoveListener(OnCancelLeaveClicked);
        }

        private void OnLeaveRunClicked() => ShowLeaveConfirmation();
        private void OnConfirmLeaveClicked()
        {
            if (IsConfirmingRestart) ConfirmRestartRun();
            else ConfirmLeaveRun();
        }
        private void OnCancelLeaveClicked() => CancelRunActionConfirmation();

        private void BindRestartButton()
        {
            if (restartRunButton == null) return;
            restartRunButton.onClick.RemoveListener(OnRestartRunClicked);
            restartRunButton.onClick.AddListener(OnRestartRunClicked);
        }

        private void UnbindRestartButton()
        {
            if (restartRunButton != null) restartRunButton.onClick.RemoveListener(OnRestartRunClicked);
        }

        private void OnRestartRunClicked() => ShowRestartConfirmation();

        private void ApplyLeaveConfirmationVisibility(bool visible)
        {
            if (leaveConfirmation == null) return;
            leaveConfirmation.alpha = visible ? 1f : 0f;
            leaveConfirmation.interactable = visible;
            leaveConfirmation.blocksRaycasts = visible;
        }

        private void RestorePreviousFocus()
        {
            if (EventSystem.current == null) return;
            GameObject target = previousSelectedObject != null && previousSelectedObject.activeInHierarchy
                ? previousSelectedObject
                : null;
            EventSystem.current.SetSelectedGameObject(target);
            previousSelectedObject = null;
        }
    }
}
