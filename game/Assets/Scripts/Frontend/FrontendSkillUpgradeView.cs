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
        // The server refused a queued upgrade (its progress differed from this PC's), so its value replaced it.
        public const string ServerAdjustedMessage = "서버 기록에 맞춰 진행 정보를 고쳤어요.";
        public const string NotEnoughPointsMessage = "사용할 수 있는 스킬 포인트가 부족합니다.";
        public const string MaximumLevelMessage = "이미 최대 레벨인 스킬입니다.";

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

        // Menu-3: the character card on the left of the upgrade screen. Display only.
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text epithetText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text lowGradeNameText;
        [SerializeField] private TMP_Text highGradeNameText;

        private IGameApiClient profileClient;
        private ISkillProgressApiClient skillClient;
        private CharacterDefinition character;
        private CharacterProgressDto progress;
        private bool lookupInFlight;
        private int viewVersion;
        private int localWrites;

        public CharacterDefinition Character => character;
        public CharacterProgressDto Progress => progress;
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

        public void ConfigurePresentation(TMP_Text characterName, TMP_Text epithet, Image portrait,
            TMP_Text lowGradeName = null, TMP_Text highGradeName = null)
        {
            lowGradeNameText = lowGradeName;
            highGradeNameText = highGradeName;
            characterNameText = characterName;
            epithetText = epithet;
            portraitImage = portrait;
        }

        public TMP_Text CharacterNameText => characterNameText;

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
            if (characterNameText != null) characterNameText.text = character != null ? character.DisplayName : string.Empty;
            if (epithetText != null) epithetText.text = character != null ? character.Epithet : string.Empty;
            if (portraitImage != null && character != null && character.Portrait != null) portraitImage.sprite = character.Portrait;
            StandingPortraitView.Apply(portraitImage, character);
            if (lowGradeNameText != null && !string.IsNullOrWhiteSpace(character?.LowGradeSkillName))
                lowGradeNameText.text = character.LowGradeSkillName;
            if (highGradeNameText != null && !string.IsNullOrWhiteSpace(character?.HighGradeSkillName))
                highGradeNameText.text = character.HighGradeSkillName;
            progress = null;
            lookupInFlight = false;
            viewVersion++;
            gameObject.SetActive(true);
            if (titleText != null) titleText.text = $"{character?.DisplayName ?? "캐릭터"} 강화";
            LoadProgress();
        }

        public void Hide()
        {
            viewVersion++;
            lookupInFlight = false;
            character = null;
            progress = null;
            gameObject.SetActive(false);
        }

        // Local-first: the screen shows this PC's progress at once. A registered profile then asks the server in
        // the background and quietly re-renders with the confirmed cache plus anything still queued.
        public void LoadProgress()
        {
            if (character == null) return;
            ApplyProgress(LocalProgression.Current(character.CharacterId), SelectMessage);
            RefreshFromServer();
        }

        private void RefreshFromServer()
        {
            IGameApiClient client = profileClient ?? ApiClient.Instance;
            // Offline-0: a nickname kept only on this PC is never looked up on the server.
            if (!LocalProfile.HasServerAccount || client == null || lookupInFlight) return;

            int version = viewVersion;
            int writes = localWrites;
            lookupInFlight = true;
            client.GetUser(LocalProfile.Nickname,
                response =>
                {
                    lookupInFlight = false;
                    // An upgrade made or sent during the lookup is newer than this answer.
                    if (version != viewVersion || writes != localWrites || PendingSync.IsFlushing) return;
                    CharacterProgressDto found = response?.data?.characterProgress?
                        .FirstOrDefault(item => item != null && item.characterId == character.CharacterId);
                    if (found != null) PlayerProgressClient.StoreConfirmedProgress(LocalProfile.Nickname, found);
                    RefreshShownProgress();
                },
                _ => lookupInFlight = false);
        }

        public void UpgradeLowGrade() => Upgrade(SkillType.LowGrade);
        public void UpgradeHighGrade() => Upgrade(SkillType.HighGrade);

        // Local-first (Offline-1 queue): the upgrade applies on this PC right away and joins the queue, so the
        // server receives it after the Runs that gave its point. The target level makes a resend apply only once.
        private void Upgrade(SkillType skillType)
        {
            if (progress == null || character == null) return;
            int currentLevel = skillType == SkillType.LowGrade
                ? progress.lowGradeSkillLevel : progress.highGradeSkillLevel;
            if (currentLevel >= ResolveMaxSkillLevel())
            {
                statusText.text = MaximumLevelMessage;
                return;
            }
            if (progress.skillPoints <= 0)
            {
                statusText.text = NotEnoughPointsMessage;
                return;
            }

            CharacterProgressDto updated = LocalProgression.QueueSkillUpgrade(character.CharacterId, skillType);
            if (updated == null)
            {
                statusText.text = progress.skillPoints <= 0 ? NotEnoughPointsMessage : MaximumLevelMessage;
                RenderProgress();
                return;
            }

            localWrites++;
            ApplyProgress(updated, UpgradedMessage);
            TrySendPending();
        }

        // A registered profile sends the queue in the background; the title retries on the next launch otherwise.
        // A flush already running picks up the new upgrade because it reads the queue again after each write.
        private void TrySendPending()
        {
            if (!LocalProfile.HasServerAccount) return;
            IGameApiClient runClient = profileClient ?? ApiClient.Instance;
            ISkillProgressApiClient upgradeClient = skillClient ?? ApiClient.Instance;
            if (runClient == null || upgradeClient == null) return;

            int version = viewVersion;
            PendingSync.Flush(runClient, upgradeClient, outcome =>
            {
                if (version != viewVersion || character == null) return;
                if (outcome == PendingSyncOutcome.Completed) RefreshShownProgress(ServerAdjustedMessage);
            });
        }

        // Re-renders after the server answered, keeping focus and the status line. After a flush the numbers
        // change only when the server refused a queued upgrade (its value wins), which gets its own notice.
        private void RefreshShownProgress(string messageIfChanged = null)
        {
            CharacterProgressDto latest = LocalProgression.Current(character.CharacterId);
            bool changed = progress != null && !SameProgress(progress, latest);
            progress = latest;
            if (changed && messageIfChanged != null) statusText.text = messageIfChanged;
            RenderProgress();
        }

        private static bool SameProgress(CharacterProgressDto a, CharacterProgressDto b) =>
            a.level == b.level && a.experience == b.experience && a.skillPoints == b.skillPoints &&
            a.lowGradeSkillLevel == b.lowGradeSkillLevel && a.highGradeSkillLevel == b.highGradeSkillLevel;

        private void ApplyProgress(CharacterProgressDto updated, string message)
        {
            progress = updated;
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
            SetButtonsInteractable(true);
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

        private int ResolveMaxSkillLevel() => progress != null && progress.maxSkillLevel > 0
            ? progress.maxSkillLevel : SkillProgressionRules.MaximumLevel;

        private Selectable FirstAvailableAction() => lowGradeUpgradeButton.interactable
            ? lowGradeUpgradeButton
            : highGradeUpgradeButton.interactable ? highGradeUpgradeButton : backButton;

        private void RequestBack() => OnBackRequested?.Invoke();

        private static void Focus(Selectable selectable)
        {
            if (selectable != null && selectable.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
