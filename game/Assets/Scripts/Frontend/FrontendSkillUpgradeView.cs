using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Character;
using TrickalFanGame.Data;
using TrickalFanGame.Meta;
using TrickalFanGame.Network;
using TrickalFanGame.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public sealed class FrontendSkillUpgradeView : MonoBehaviour
    {
        public const string LoadingMessage = "진행 정보를 불러오는 중입니다...";
        public const string SelectMessage = "강화할 스킬을 선택해 주세요.";
        public const string UpgradedMessage = "스킬 강화가 완료되었습니다.";
        // Offline-1 (docs/30 §3.1 S4): without the server the screen shows this PC's progress and upgrades it.
        public const string OfflineProgressMessage = "오프라인 진행 정보예요. 강화는 서버에 연결되면 반영돼요.";
        public const string OfflineUpgradeMessage = "오프라인으로 강화했어요. 서버에 연결되면 반영돼요.";
        public const string SyncedMessage = "오프라인 기록을 서버에 반영했어요.";
        public const string NotEnoughPointsMessage = "사용할 수 있는 스킬 포인트가 부족합니다.";
        public const string MaximumLevelMessage = "이미 최대 레벨인 스킬입니다.";
        public const string ConflictMessage = "진행 정보가 변경되었습니다. 다시 불러온 뒤 시도해 주세요.";

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text characterLevelText;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private TMP_Text skillPointsText;
        [SerializeField] private TMP_Text lowGradeLevelText;
        [SerializeField] private TMP_Text lowGradeEffectText;
        [SerializeField] private Button lowGradeUpgradeButton;
        [SerializeField] private TMP_Text highGradeLevelText;
        [SerializeField] private TMP_Text highGradeEffectText;
        [SerializeField] private Button highGradeUpgradeButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button backButton;

        private IGameApiClient profileClient;
        private ISkillProgressApiClient skillClient;
        private CharacterDefinition character;
        private CharacterProgressDto progress;
        private bool requestInFlight;
        private int viewVersion;

        public CharacterDefinition Character => character;
        public CharacterProgressDto Progress => progress;
        public bool RequestInFlight => requestInFlight;
        public TMP_Text CharacterLevelText => characterLevelText;
        public TMP_Text ExperienceText => experienceText;
        public TMP_Text SkillPointsText => skillPointsText;
        public TMP_Text LowGradeLevelText => lowGradeLevelText;
        public TMP_Text HighGradeLevelText => highGradeLevelText;
        public TMP_Text StatusText => statusText;
        public Button LowGradeUpgradeButton => lowGradeUpgradeButton;
        public Button HighGradeUpgradeButton => highGradeUpgradeButton;
        public Button RetryButton => retryButton;
        public Button BackButton => backButton;

        public event Action OnBackRequested;

        public void Configure(
            TMP_Text title,
            TMP_Text characterLevel,
            TMP_Text experience,
            TMP_Text skillPoints,
            TMP_Text lowLevel,
            TMP_Text lowEffect,
            Button lowUpgrade,
            TMP_Text highLevel,
            TMP_Text highEffect,
            Button highUpgrade,
            TMP_Text status,
            Button retry,
            Button back)
        {
            titleText = title;
            characterLevelText = characterLevel;
            experienceText = experience;
            skillPointsText = skillPoints;
            lowGradeLevelText = lowLevel;
            lowGradeEffectText = lowEffect;
            lowGradeUpgradeButton = lowUpgrade;
            highGradeLevelText = highLevel;
            highGradeEffectText = highEffect;
            highGradeUpgradeButton = highUpgrade;
            statusText = status;
            retryButton = retry;
            backButton = back;
        }

        public void SetApiClients(IGameApiClient configuredProfileClient, ISkillProgressApiClient configuredSkillClient)
        {
            profileClient = configuredProfileClient;
            skillClient = configuredSkillClient;
        }

        private void OnEnable()
        {
            lowGradeUpgradeButton?.onClick.AddListener(UpgradeLowGrade);
            highGradeUpgradeButton?.onClick.AddListener(UpgradeHighGrade);
            retryButton?.onClick.AddListener(LoadProgress);
            backButton?.onClick.AddListener(RequestBack);
        }

        private void OnDisable()
        {
            lowGradeUpgradeButton?.onClick.RemoveListener(UpgradeLowGrade);
            highGradeUpgradeButton?.onClick.RemoveListener(UpgradeHighGrade);
            retryButton?.onClick.RemoveListener(LoadProgress);
            backButton?.onClick.RemoveListener(RequestBack);
        }

        public void Show(CharacterDefinition selectedCharacter)
        {
            character = selectedCharacter;
            progress = null;
            requestInFlight = false;
            viewVersion++;
            gameObject.SetActive(true);
            if (titleText != null) titleText.text = $"{character?.DisplayName ?? "캐릭터"} 강화";
            LoadProgress();
        }

        public void Hide()
        {
            viewVersion++;
            requestInFlight = false;
            character = null;
            progress = null;
            gameObject.SetActive(false);
        }

        public void LoadProgress()
        {
            if (requestInFlight || character == null) return;
            int version = viewVersion;
            IGameApiClient client = profileClient ?? ApiClient.Instance;
            if (!LocalProfile.HasServerAccount || client == null)
            {
                // Offline-0: a nickname kept only on this PC is never looked up on the server.
                ShowLocalProgress(OfflineProgressMessage);
                return;
            }

            requestInFlight = true;
            SetLoadingState(LoadingMessage);
            client.GetUser(LocalProfile.Nickname,
                response =>
                {
                    if (version != viewVersion) return;
                    requestInFlight = false;
                    CharacterProgressDto found = response?.data?.characterProgress?
                        .FirstOrDefault(item => item != null && item.characterId == character.CharacterId);
                    if (found != null) PlayerProgressClient.StoreConfirmedProgress(LocalProfile.Nickname, found);
                    ApplyProgress(LocalProgression.Current(character.CharacterId),
                        LocalProgression.HasPendingWrites() ? OfflineProgressMessage : SelectMessage);
                },
                _ =>
                {
                    if (version != viewVersion) return;
                    ShowLocalProgress(OfflineProgressMessage);
                });
        }

        public void UpgradeLowGrade() => Upgrade(SkillType.LowGrade);
        public void UpgradeHighGrade() => Upgrade(SkillType.HighGrade);

        private void Upgrade(SkillType skillType)
        {
            if (requestInFlight || progress == null) return;
            int currentLevel = skillType == SkillType.LowGrade
                ? progress.lowGradeSkillLevel : progress.highGradeSkillLevel;
            int maxSkillLevel = ResolveMaxSkillLevel();
            if (currentLevel >= maxSkillLevel)
            {
                statusText.text = MaximumLevelMessage;
                return;
            }
            if (progress.skillPoints <= 0)
            {
                statusText.text = NotEnoughPointsMessage;
                return;
            }

            // Offline-1: without a server account, or behind writes still waiting, the upgrade joins the queue so
            // the server receives it after the Runs that gave its point.
            ISkillProgressApiClient client = skillClient ?? ApiClient.Instance;
            if (!LocalProfile.HasServerAccount || client == null || LocalProgression.HasPendingWrites())
            {
                QueueOfflineUpgrade(skillType);
                return;
            }

            requestInFlight = true;
            int version = viewVersion;
            SetButtonsInteractable(false);
            statusText.text = "스킬을 강화하는 중입니다...";
            client.UpgradeSkill(LocalProfile.Nickname, character.CharacterId, skillType, currentLevel + 1,
                updated =>
                {
                    if (version != viewVersion) return;
                    requestInFlight = false;
                    PlayerProgressClient.StoreConfirmedProgress(LocalProfile.Nickname, updated);
                    ApplyProgress(LocalProgression.Current(character.CharacterId), UpgradedMessage);
                },
                error =>
                {
                    if (version != viewVersion) return;
                    requestInFlight = false;
                    if (IsServerUnavailable(error))
                    {
                        // The target level makes a later resend apply only once, even if this one arrived.
                        QueueOfflineUpgrade(skillType);
                        return;
                    }
                    statusText.text = MessageFor(error.Type);
                    RenderProgress();
                });
        }

        private void QueueOfflineUpgrade(SkillType skillType)
        {
            CharacterProgressDto updated = LocalProgression.QueueSkillUpgrade(character.CharacterId, skillType);
            if (updated == null)
            {
                statusText.text = progress.skillPoints <= 0 ? NotEnoughPointsMessage : MaximumLevelMessage;
                RenderProgress();
                return;
            }

            ApplyProgress(updated, OfflineUpgradeMessage);
            TrySendPending();
        }

        // A registered profile tries to send the queue right away; the title retries on the next launch otherwise.
        private void TrySendPending()
        {
            if (!LocalProfile.HasServerAccount) return;
            IGameApiClient runClient = profileClient ?? ApiClient.Instance;
            ISkillProgressApiClient upgradeClient = skillClient ?? ApiClient.Instance;
            if (runClient == null || upgradeClient == null) return;

            int version = viewVersion;
            PendingSync.Flush(runClient, upgradeClient, outcome =>
            {
                if (version != viewVersion || requestInFlight || character == null) return;
                if (outcome != PendingSyncOutcome.Completed) return;
                ApplyProgress(LocalProgression.Current(character.CharacterId), SyncedMessage);
            });
        }

        private void ShowLocalProgress(string message)
        {
            requestInFlight = false;
            ApplyProgress(LocalProgression.Current(character.CharacterId), message);
            retryButton.gameObject.SetActive(LocalProfile.HasServerAccount);
        }

        private static bool IsServerUnavailable(SkillUpgradeError error) =>
            error.Type == SkillUpgradeErrorType.Network ||
            error.Type == SkillUpgradeErrorType.Server ||
            error.Type == SkillUpgradeErrorType.Parse;

        private void ApplyProgress(CharacterProgressDto updated, string message)
        {
            progress = updated;
            requestInFlight = false;
            statusText.text = message;
            retryButton.gameObject.SetActive(false);
            RenderProgress();
            Focus(FirstAvailableAction());
        }

        private void RenderProgress()
        {
            if (progress == null) return;
            int maxCharacterLevel = progress.maxLevel > 0 ? progress.maxLevel : 19;
            int maxSkillLevel = ResolveMaxSkillLevel();
            characterLevelText.text = $"캐릭터 레벨  Lv. {progress.level} / {maxCharacterLevel}";
            experienceText.text = progress.level >= maxCharacterLevel
                ? "경험치  MAX"
                : $"경험치  {progress.experience} / {progress.experienceToNextLevel}";
            skillPointsText.text = $"남은 스킬 포인트  {progress.skillPoints}";
            lowGradeLevelText.text = $"Lv. {progress.lowGradeSkillLevel} / {maxSkillLevel}";
            highGradeLevelText.text = $"Lv. {progress.highGradeSkillLevel} / {maxSkillLevel}";
            lowGradeEffectText.text = $"피해 {SkillProgressionRules.DamageMultiplier(progress.lowGradeSkillLevel) * 100f:0}%" +
                (progress.lowGradeSkillLevel >= 5 ? " · 투사체 +1" : " · Lv.5에 투사체 +1");
            highGradeEffectText.text = $"피해 {SkillProgressionRules.DamageMultiplier(progress.highGradeSkillLevel) * 200f:0}%" +
                (progress.highGradeSkillLevel >= 5 ? " · 쿨타임 -15%" : " · Lv.5에 쿨타임 -15%");
            SetButtonsInteractable(!requestInFlight);
            retryButton.gameObject.SetActive(false);
        }

        private void SetButtonsInteractable(bool allow)
        {
            if (progress == null)
            {
                lowGradeUpgradeButton.interactable = false;
                highGradeUpgradeButton.interactable = false;
                return;
            }
            int max = ResolveMaxSkillLevel();
            lowGradeUpgradeButton.interactable = allow && progress.skillPoints > 0 && progress.lowGradeSkillLevel < max;
            highGradeUpgradeButton.interactable = allow && progress.skillPoints > 0 && progress.highGradeSkillLevel < max;
        }

        private void SetLoadingState(string message, bool retryVisible = false)
        {
            statusText.text = message;
            characterLevelText.text = "캐릭터 레벨  -";
            experienceText.text = "경험치  -";
            skillPointsText.text = "남은 스킬 포인트  -";
            lowGradeLevelText.text = "Lv. -";
            highGradeLevelText.text = "Lv. -";
            lowGradeEffectText.text = "서버 진행 정보가 필요합니다.";
            highGradeEffectText.text = "서버 진행 정보가 필요합니다.";
            lowGradeUpgradeButton.interactable = false;
            highGradeUpgradeButton.interactable = false;
            retryButton.gameObject.SetActive(retryVisible);
            Focus(retryVisible ? retryButton : backButton);
        }

        private int ResolveMaxSkillLevel() => progress != null && progress.maxSkillLevel > 0
            ? progress.maxSkillLevel : SkillProgressionRules.MaximumLevel;

        private Selectable FirstAvailableAction() => lowGradeUpgradeButton.interactable
            ? lowGradeUpgradeButton
            : highGradeUpgradeButton.interactable ? highGradeUpgradeButton : backButton;

        private static string MessageFor(SkillUpgradeErrorType type) => type switch
        {
            SkillUpgradeErrorType.NotEnoughPoints => NotEnoughPointsMessage,
            SkillUpgradeErrorType.MaximumLevel => MaximumLevelMessage,
            SkillUpgradeErrorType.Conflict => ConflictMessage,
            SkillUpgradeErrorType.Network => "네트워크 연결에 실패했습니다. 다시 시도해 주세요.",
            SkillUpgradeErrorType.Server => "서버 오류로 강화하지 못했습니다. 잠시 후 다시 시도해 주세요.",
            SkillUpgradeErrorType.Parse => "서버 응답을 확인하지 못했습니다. 다시 시도해 주세요.",
            _ => "스킬 강화에 실패했습니다. 다시 시도해 주세요."
        };

        private void RequestBack() => OnBackRequested?.Invoke();

        private static void Focus(Selectable selectable)
        {
            if (selectable != null && selectable.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
