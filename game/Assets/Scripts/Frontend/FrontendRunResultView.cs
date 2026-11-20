using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TrickalFanGame.Audio;
using TrickalFanGame.Data;
using TrickalFanGame.Item;
using TrickalFanGame.Meta;
using TrickalFanGame.Network;
using TrickalFanGame.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class FrontendRunResultView : MonoBehaviour
    {
        // Offline-1: a Run the server could not take still shows the growth calculated on this PC.
        public const string OfflineSaveStatus = "오프라인 기록 · 서버에 연결되면 저장돼요";

        [SerializeField] private FrontendTitleView titleView;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text characterName;
        [SerializeField] private TMP_Text characterReaction;
        [SerializeField] private TMP_Text speechText;
        [SerializeField] private TMP_Text stageTitle;
        [SerializeField] private TMP_Text stageBody;
        [SerializeField] private Slider experienceBar;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private TMP_Text saveStatus;
        [SerializeField] private GameObject actionRoot;
        [SerializeField] private Button retryRunButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private AudioSource audioSource;

        private RunResultPayload payload;
        private CreateRunResponse savedResponse;
        private int resultExperience;
        private CharacterProgressDto resultProgress;
        private Coroutine sequence;
        private bool submissionInFlight;
        private bool stageAnimating;
        private bool finishCurrentStage;
        private bool skipToSummary;
        private bool hasUsedFastForward;
        private AudioClip levelCue;
        private RunResultStageView recordView;
        private RunResultStageView levelUpView;
        private RunResultStageView skillPointsView;
        private RunResultStageView artifactsView;
        private RunResultStageView summaryView;
        private RunResultStageView activeStage;
        private RunResultBadgeView badge;
        private bool ExperienceInterrupted => finishCurrentStage || skipToSummary;

        public GameObject ResultPanel => resultPanel;
        public TMP_Text StageTitle => activeStage != null ? activeStage.Title : stageTitle;
        public TMP_Text StageBody => stageBody;
        public TMP_Text SaveStatus => saveStatus;
        public Slider ExperienceBar => activeStage == recordView && recordView != null
            ? recordView.ExperienceBar : experienceBar;
        public TMP_Text ExperienceSource => recordView != null ? recordView.ExperienceSource : null;
        public RunResultStageView SummaryView => summaryView;
        public bool IsAnimatingExperience => stageAnimating;
        public Button RetryRunButton => retryRunButton;
        public Button HomeButton => homeButton;
        public bool IsShowing => resultPanel != null && resultPanel.activeInHierarchy;
        public bool IsSubmissionInFlight => submissionInFlight;
        public bool IsSummaryVisible => actionRoot != null && actionRoot.activeInHierarchy;
        public int ResultExperience => resultExperience;
        public CharacterProgressDto ResultProgress => resultProgress;

        public void Configure(FrontendTitleView configuredTitleView, GameObject panel, TMP_Text heading,
            TMP_Text configuredCharacterName, TMP_Text reaction, TMP_Text speech, TMP_Text configuredStageTitle,
            TMP_Text body, Slider xpBar, TMP_Text xpText, TMP_Text configuredSaveStatus,
            GameObject configuredActionRoot, Button runRetry, Button home, AudioSource configuredAudioSource)
        {
            titleView = configuredTitleView;
            resultPanel = panel;
            resultTitle = heading;
            characterName = configuredCharacterName;
            characterReaction = reaction;
            speechText = speech;
            stageTitle = configuredStageTitle;
            stageBody = body;
            experienceBar = xpBar;
            experienceText = xpText;
            saveStatus = configuredSaveStatus;
            actionRoot = configuredActionRoot;
            retryRunButton = runRetry;
            homeButton = home;
            audioSource = configuredAudioSource;
        }

        private void OnEnable()
        {
            retryRunButton?.onClick.AddListener(ReturnToCharacterSelection);
            homeButton?.onClick.AddListener(ReturnHome);
        }

        private void Start()
        {
            if (!RunResultContext.TryPeek(out payload))
            {
                resultPanel?.SetActive(false);
                return;
            }

            ShowPendingResult();
            SubmitResult();
        }

        private void Update()
        {
            if (!IsShowing || sequence == null) return;
            bool confirm = Keyboard.current?.enterKey.wasPressedThisFrame == true ||
                           Keyboard.current?.spaceKey.wasPressedThisFrame == true ||
                           Mouse.current?.leftButton.wasPressedThisFrame == true;
            if (confirm) RequestAdvance();
        }

        private void LateUpdate()
        {
            if (!IsSummaryVisible || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(actionRoot.transform)) return;
            Focus(retryRunButton);
        }

        private void OnDisable()
        {
            retryRunButton?.onClick.RemoveListener(ReturnToCharacterSelection);
            homeButton?.onClick.RemoveListener(ReturnHome);
        }

        public void RequestAdvance()
        {
            if (sequence == null) return;
            if (stageAnimating && !hasUsedFastForward)
            {
                hasUsedFastForward = true;
                finishCurrentStage = true;
                return;
            }
            skipToSummary = true;
        }

        public void SubmitResult()
        {
            if (payload == null || submissionInFlight || savedResponse?.data != null) return;
            IGameApiClient client = payload.ApiClient ?? ApiClient.Instance;
            if (client == null)
            {
                ShowSaveFailure("API 연결을 사용할 수 없습니다.");
                return;
            }

            submissionInFlight = true;
            saveStatus.text = "";
            stageTitle.text = "결과를 정리하고 있어요";
            stageBody.text = "잠시만 기다려 주세요...";
            RunSubmission.Post(client, payload.Request, LocalProfile.Nickname, OnSaveSuccess, ShowSaveFailure);
        }

        private void OnSaveSuccess(CreateRunResponse response)
        {
            submissionInFlight = false;
            if (response?.data?.progress == null)
            {
                ShowSaveFailure("저장 응답에 성장 정보가 없습니다.");
                return;
            }

            savedResponse = response;
            resultExperience = response.data.experienceGained;
            resultProgress = response.data.progress;
            LocalPendingRunStorage.Remove(payload.Request.clientRunId);
            PlayerProgressClient.StoreConfirmedProgress(LocalProfile.Nickname, response.data.progress);
            PlayerProgressClient.StoreConfirmedElifMeta(LocalProfile.Nickname, response.data.meta);
            saveStatus.text = "저장 완료";
            if (sequence != null) StopCoroutine(sequence);
            sequence = StartCoroutine(PlaySequence());
        }

        private void ShowSaveFailure(string error)
        {
            submissionInFlight = false;
            Debug.Log($"[FrontendRunResultView] Save failed (offline mode): {error}");
            LocalPendingRunStorage.Save(payload.Request);
            resultExperience = LocalProgression.ExperienceOf(payload.Request);
            resultProgress = PlayerProgressClient.IsLocalProfileNickname(LocalProfile.Nickname)
                ? LocalProgression.Current(payload.Request.characterId)
                : MetaProgressionRules.ApplyRunExperience(StartingProgress(), resultExperience);
            saveStatus.text = OfflineSaveStatus;
            if (sequence != null) StopCoroutine(sequence);
            sequence = StartCoroutine(PlaySequence());
        }

        private void ShowPendingResult()
        {
            Time.timeScale = 0f;
            titleView.ShowTitleScreen();
            titleView.TitlePanel.SetActive(false);
            titleView.HomeView.Hide();
            resultPanel.SetActive(true);
            actionRoot.SetActive(false);
            saveStatus.gameObject.SetActive(false);
            experienceBar.gameObject.SetActive(false);
            experienceText.gameObject.SetActive(false);
            BuildPresentation();
            bool cleared = payload.Request.isCleared;
            var character = titleView.HomeView.CharacterSelectionView?.FindCharacter(payload.Request.characterId);
            Sprite portrait = character != null ? character.Portrait : null;
            if (!cleared && payload.Request.characterId == "erpin")
                portrait = UserArtwork.Load("run-result-erpin-defeat") ?? portrait;
            badge.SetPortrait(portrait);
            badge.SetOutcome(cleared);
            badge.SetDialogue(speechText.font, cleared ? "해냈어! 역시 나야!" : "아무래도 이건 좀 아닌 것 같아!");
            SetLayout(badge.transform, 0f, 0f, 1.245f);
            badge.SetCrestLayout(new Vector2(0f, 46f), 1f);
            badge.SetPortraitLayout(cleared ? new Vector2(8f, -68f) : new Vector2(7f, -23f),
                cleared ? 1.322f : 1.313f);
            OffsetLayout(badge.transform, "Outcome Effects", 0f, 25f, 1.21f);
            OffsetLayout(badge.transform, "Outcome Wordmark", 0f, 34f, 1.15f);
            OffsetLayout(badge.transform, "Dialogue Bubble", 76f, -52f, 0.97f);
        }

        private void BuildPresentation()
        {
            if (recordView != null) return;

            Image background = resultPanel.GetComponent<Image>();
            background.sprite = UserArtwork.Load("run-result-background");
            if (background.sprite != null) background.color = Color.white;

            RectTransform characterPanel = (RectTransform)characterReaction.transform.parent;
            characterPanel.anchoredPosition = new Vector2(-485, 12);
            characterPanel.GetComponent<Image>().enabled = false;
            badge = RunResultBadgeView.CreateVictory(characterPanel, null);
            badge.transform.SetAsFirstSibling();
            characterName.gameObject.SetActive(false);
            characterReaction.gameObject.SetActive(false);
            resultTitle.gameObject.SetActive(false);
            speechText.transform.parent.gameObject.SetActive(false);

            RectTransform stagePanel = (RectTransform)stageTitle.transform.parent;
            stagePanel.anchoredPosition = new Vector2(445, 35);
            stagePanel.GetComponent<Image>().enabled = false;
            saveStatus.rectTransform.anchoredPosition = new Vector2(62, 330);
            TMP_FontAsset font = stageTitle.font;
            recordView = RunResultStageView.CreateRecord(stagePanel, font);
            levelUpView = RunResultStageView.CreateLevelUp(stagePanel, font);
            skillPointsView = RunResultStageView.CreateSkillPoints(stagePanel, font);
            artifactsView = RunResultStageView.CreateArtifacts(stagePanel, font);
            summaryView = RunResultStageView.CreateSummary(stagePanel, font);
            // Buttons use the screen center independently of the summary's scale.
            actionRoot.transform.SetParent(stagePanel.parent, false);
            SetLayout(actionRoot.transform, 0f, 0f, 1f);
            SetLayout(retryRunButton.transform, 217f, -341f, 1.093f);
            SetLayout(homeButton.transform, 640f, -341f, 1.093f);
            ApplyStageLayouts();
            RunResultButton.Apply(retryRunButton, true);
            RunResultButton.Apply(homeButton, false);
            saveStatus.transform.SetAsLastSibling();
        }

        private void ApplyStageLayouts()
        {
            SetLayout(recordView.transform, 0f, 0f, 1f);
            SetLayout(levelUpView.transform, 0f, 0f, 1.098f);
            SetLayout(skillPointsView.transform, 0f, 0f, 1.184f);
            SetLayout(artifactsView.transform, 0f, 0f, 1.193f);
            SetLayout(summaryView.transform, 0f, 0f, 1.047f);

            Transform record = recordView.transform;
            OffsetLayout(record, "Title", 0f, 0f, 1.79f);
            foreach (string name in new[] { "ReachedFloor", "KillCount", "PlayTime", "FloorSeparator", "TimeSeparator" })
                OffsetLayout(record, name, 0f, 0f, 1.2f);
            OffsetLayout(record, "DeathReason", 0f, -22f, 1.273f);
            OffsetLayout(record, "CurrentLevel", -25f, 0f, 1.176f);
            OffsetLayout(record, "ExperienceSource", 0f, 23f, 1.113f);

            Transform levelUp = levelUpView.transform;
            OffsetLayout(levelUp, "LevelUpLight", 8f, 21f, 3f);
            OffsetLayout(levelUp, "LevelUpCrown", 0f, 49f, 1.004f);
            OffsetLayout(levelUp, "LevelUpWordmark", 0f, 20f, 1.5f);
            OffsetLayout(levelUp, "LevelUpLeftLeaf", -85f, 0f, 1.224f);
            OffsetLayout(levelUp, "LevelUpRightLeaf", 85f, 0f, 1.224f);

            Transform points = skillPointsView.transform;
            OffsetLayout(points, "Title", 0f, 0f, 1.509f);
            OffsetLayout(points, "SkillToken", 23f, 0f, 1f);
            OffsetLayout(points, "PointsGained", -35f, 0f, 0.954f);
            OffsetLayout(points, "PointsBalance", 0f, 0f, 1.089f);

            OffsetLayout(artifactsView.transform, "Title", 0f, 0f, 1.509f);

            Transform summary = summaryView.transform;
            OffsetLayout(summary, "Title", 0f, 0f, 1.509f);
            foreach (string name in new[] { "ReachedFloor", "KillCount", "PlayTime" })
                OffsetLayout(summary, name, 0f, 0f, 1.05f);
            OffsetLayout(summary, "DeathReason", 0f, -13f, 1.073f);
            OffsetLayout(summary, "SkillPointsBalance", 0f, -17f, 1.051f);
        }

        private static void SetLayout(Transform target, float x, float y, float scale)
        {
            RectTransform rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.localScale = new Vector3(scale, scale, 1f);
        }

        private static void OffsetLayout(Transform root, string name, float dx, float dy, float scale)
        {
            if (root.Find(name) is not RectTransform rect) return;
            rect.anchoredPosition += new Vector2(dx, dy);
            rect.localScale = Vector3.Scale(rect.localScale, new Vector3(scale, scale, 1f));
        }

        private static void ApplyRewardCountLayout(RunResultStageView stage, float y, float scale)
        {
            Transform items = stage.transform.Find("RewardSlots/Items");
            for (int i = 0; i < Mathf.Min(2, items.childCount); i++)
                OffsetLayout(items.GetChild(i), "Count", 0f, y, scale);
        }

        private IEnumerator PlaySequence()
        {
            skipToSummary = false;
            finishCurrentStage = false;
            hasUsedFastForward = false;
            stageTitle.gameObject.SetActive(false);
            stageBody.gameObject.SetActive(false);

            CharacterProgressDto start = StartingProgress();
            CharacterProgressDto end = resultProgress;
            recordView.SetRecord(payload.Request, payload.DeathDescription);
            ShowStage(recordView);
            yield return AnimateExperience(start, end);

            if (!skipToSummary && end.level > start.level)
            {
                levelUpView.SetLevelUp(start.level, end.level);
                ShowStage(levelUpView);
                CombatSfx.Play(CombatSfxCue.LevelUp);
                yield return WaitOnStage(RunResultStageView.LevelUpLightPeriod);
            }

            if (!skipToSummary)
            {
                skillPointsView.SetSkillPoints(Mathf.Max(0, end.skillPoints - start.skillPoints), end.skillPoints);
                ShowStage(skillPointsView);
                yield return WaitOnStage(1f);
            }

            if (!skipToSummary)
            {
                artifactsView.SetRewards(payload);
                ApplyRewardCountLayout(artifactsView, -12f, 1.149f);
                ShowStage(artifactsView);
                yield return WaitOnStage(1.35f);
            }

            ShowSummary();
        }

        private void ShowStage(RunResultStageView stage)
        {
            if (activeStage != null) activeStage.gameObject.SetActive(false);
            activeStage = stage;
            activeStage.gameObject.SetActive(true);
        }

        private IEnumerator WaitOnStage(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && !skipToSummary)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator AnimateExperience(CharacterProgressDto start, CharacterProgressDto end)
        {
            TMP_Text source = recordView.ExperienceSource;
            int level = start.level;
            int current = start.experience;
            recordView.SetExperience(level, current, MetaProgressionRules.RequiredExperience(level));
            stageAnimating = true;
            yield return null;

            foreach (var (label, amount) in BuildExperienceSources())
            {
                if (ExperienceInterrupted) break;

                source.text = $"{label} <color=#38F5CA>+{amount}</color>";
                yield return FadeExperienceSource(0f, 1f, 0.16f);
                int remaining = amount;
                while (remaining > 0 && !ExperienceInterrupted && level <= end.level)
                {
                    int required = MetaProgressionRules.RequiredExperience(level);
                    if (required == 0) break;

                    int target = level == end.level ? end.experience : required;
                    int gain = Mathf.Min(remaining, target - current);
                    if (gain <= 0) break;

                    yield return AnimateExperienceSegment(level, current, current + gain, required);
                    current += gain;
                    remaining -= gain;
                    if (current == required && level < end.level && !ExperienceInterrupted)
                    {
                        PlayLevelCue();
                        level++;
                        current = 0;
                        recordView.SetExperience(level, current, MetaProgressionRules.RequiredExperience(level));
                    }
                }
                yield return FadeExperienceSource(1f, 0f, 0.20f);
            }

            SetSourceOpacity(0f);
            recordView.SetExperience(end.level, end.experience, end.experienceToNextLevel);
            stageAnimating = false;
            finishCurrentStage = false;
            yield return WaitOnStage(0.45f);
        }

        private IEnumerable<(string label, int amount)> BuildExperienceSources()
        {
            CreateRunRequest request = payload.Request;
            int remaining = Mathf.Max(0, resultExperience);
            int floor = Mathf.Min(remaining, Mathf.Max(0, request.reachedFloor) * MetaProgressionRules.ReachedFloorExperience);
            if (floor > 0)
            {
                yield return ($"{request.reachedFloor}층 도달", floor);
                remaining -= floor;
            }

            int kills = Mathf.Min(remaining, Mathf.Max(0, request.killCount) * MetaProgressionRules.KillExperience);
            if (kills > 0)
            {
                yield return ($"적 {request.killCount}마리 처치", kills);
                remaining -= kills;
            }

            var artifacts = (request.items ?? System.Array.Empty<RunItemDto>())
                .Where(item => item != null && RunResultStageView.IsArtifact(item.itemId))
                .GroupBy(item => payload.ItemRarities.TryGetValue(item.itemId, out ItemRarity rarity)
                    ? rarity : ItemRarity.Common)
                .OrderBy(group => group.Key);

            foreach (var group in artifacts)
            {
                int amount = group.Count() * MetaProgressionRules.RarityExperience(group.Key);
                if (amount <= 0 || amount > remaining) continue;

                string rarityName = group.Key switch
                {
                    ItemRarity.Uncommon => "고급",
                    ItemRarity.Rare => "희귀",
                    ItemRarity.Epic => "전설",
                    _ => "일반"
                };
                yield return ($"{rarityName} 아티팩트 {group.Count()}개", amount);
                remaining -= amount;
            }

            if (remaining > 0) yield return ("기타 획득 경험치", remaining);
        }

        private IEnumerator FadeExperienceSource(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && !ExperienceInterrupted)
            {
                SetSourceOpacity(Mathf.Lerp(from, to, elapsed / duration));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            SetSourceOpacity(ExperienceInterrupted ? 0f : to);
        }

        private void SetSourceOpacity(float alpha) => recordView.ExperienceSourceFade.alpha = alpha;

        private IEnumerator AnimateExperienceSegment(int level, int from, int to, int required)
        {
            float elapsed = 0f;
            const float duration = 0.34f;
            while (elapsed < duration && !ExperienceInterrupted)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                int value = Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
                recordView.SetExperience(level, value, required);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            recordView.SetExperience(level, to, required);
        }

        private void ShowSummary()
        {
            sequence = null;
            stageAnimating = false;
            summaryView.SetSummary(payload, resultProgress);
            ApplyRewardCountLayout(summaryView, -11f, 1f);
            ShowStage(summaryView);
            saveStatus.gameObject.SetActive(true);
            actionRoot.SetActive(true);
            Focus(retryRunButton);
        }

        private CharacterProgressDto StartingProgress()
        {
            return payload.StartingProgress ?? MetaProgressionRules.CreateDefault(payload.Request.characterId);
        }

        private void PlayLevelCue()
        {
            if (audioSource == null) return;
            if (levelCue == null)
            {
                const int rate = 22050;
                const int samples = 2646;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float fade = 1f - (float)i / samples;
                    data[i] = Mathf.Sin(2f * Mathf.PI * 880f * i / rate) * fade * 0.12f;
                }
                levelCue = AudioClip.Create("Run Result Level Cue", samples, 1, rate, false);
                levelCue.SetData(data, 0);
            }
            audioSource.PlayOneShot(levelCue);
        }

        private void ReturnToCharacterSelection()
        {
            ExitResult();
            titleView.ShowHomeScreen();
            titleView.HomeView.RequestCharacterSelection();
        }

        private void ReturnHome()
        {
            ExitResult();
            titleView.ShowHomeScreen();
        }

        private void ExitResult()
        {
            if (sequence != null) StopCoroutine(sequence);
            sequence = null;
            RunResultContext.Clear();
            Time.timeScale = 1f;
            resultPanel.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private static void Focus(Selectable selectable)
        {
            if (selectable != null && selectable.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
