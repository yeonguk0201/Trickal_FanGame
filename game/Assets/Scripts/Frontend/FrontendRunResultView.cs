using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class FrontendRunResultView : MonoBehaviour
    {
        private static readonly int[] ExperienceCurve =
        {
            400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700,
            1800, 1900, 2000, 2100
        };

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
        private Coroutine sequence;
        private bool submissionInFlight;
        private bool stageAnimating;
        private bool finishCurrentStage;
        private bool skipToSummary;
        private bool hasUsedFastForward;
        private AudioClip levelCue;

        public GameObject ResultPanel => resultPanel;
        public TMP_Text StageTitle => stageTitle;
        public TMP_Text StageBody => stageBody;
        public TMP_Text SaveStatus => saveStatus;
        public Slider ExperienceBar => experienceBar;
        public Button RetryRunButton => retryRunButton;
        public Button HomeButton => homeButton;
        public bool IsShowing => resultPanel != null && resultPanel.activeInHierarchy;
        public bool IsSubmissionInFlight => submissionInFlight;
        public bool IsSummaryVisible => actionRoot != null && actionRoot.activeInHierarchy;

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
            client.PostRun(payload.Request, OnSaveSuccess, ShowSaveFailure);
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
            LocalPendingRunStorage.Remove(payload.Request.clientRunId);
            PlayerProgressClient.StoreConfirmedProgress(LocalProfile.Nickname, response.data.progress);
            saveStatus.text = "저장 완료";
            if (sequence != null) StopCoroutine(sequence);
            sequence = StartCoroutine(PlaySequence());
        }

        private void ShowSaveFailure(string error)
        {
            submissionInFlight = false;
            Debug.Log($"[FrontendRunResultView] Save failed (offline mode): {error}");
            LocalPendingRunStorage.Save(payload.Request);
            saveStatus.text = "";
            if (sequence != null) StopCoroutine(sequence);
            sequence = StartCoroutine(PlayOfflineSequence());
        }

        private void ShowPendingResult()
        {
            Time.timeScale = 0f;
            titleView.ShowTitleScreen();
            titleView.TitlePanel.SetActive(false);
            titleView.HomeView.Hide();
            resultPanel.SetActive(true);
            actionRoot.SetActive(false);
            experienceBar.gameObject.SetActive(false);
            experienceText.gameObject.SetActive(false);
            bool cleared = payload.Request.isCleared;
            resultTitle.text = cleared ? "RUN CLEAR" : "RUN FAILED";
            characterName.text = payload.Request.characterId == "erpin" ? "에르핀" : payload.Request.characterId;
            characterReaction.text = cleared ? "^_^" : "T_T";
            speechText.text = cleared ? "해냈어! 역시 내가 최고야!" : "으앙... 다음에는 꼭 이길 거야.";
        }

        private IEnumerator PlaySequence()
        {
            skipToSummary = false;
            finishCurrentStage = false;
            hasUsedFastForward = false;
            yield return ShowTextStage("RUN 기록", BuildRunRecord(), 1.25f);
            if (skipToSummary) { ShowSummary(); yield break; }

            yield return AnimateExperience();
            if (skipToSummary) { ShowSummary(); yield break; }

            CharacterProgressDto start = StartingProgress();
            CharacterProgressDto end = savedResponse.data.progress;
            if (end.level > start.level)
            {
                yield return ShowTextStage("LEVEL UP!", $"Lv. {start.level}  →  Lv. {end.level}", 1.15f);
                if (skipToSummary) { ShowSummary(); yield break; }
            }

            int gainedPoints = Mathf.Max(0, end.skillPoints - start.skillPoints);
            yield return ShowTextStage("스킬 포인트", $"+{gainedPoints}\n보유 {end.skillPoints} 포인트", 1.0f);
            if (skipToSummary) { ShowSummary(); yield break; }

            yield return ShowTextStage("획득 아티팩트", BuildArtifactReview(), 1.1f);
            ShowSummary();
        }

        private IEnumerator PlayOfflineSequence()
        {
            skipToSummary = false;
            finishCurrentStage = false;
            hasUsedFastForward = false;
            yield return ShowTextStage("RUN 기록", BuildRunRecord(), 1.25f);
            if (skipToSummary) { ShowOfflineSummary(); yield break; }

            yield return ShowTextStage("획득 아티팩트", BuildArtifactReview(), 1.1f);
            ShowOfflineSummary();
        }

        private void ShowOfflineSummary()
        {
            sequence = null;
            stageAnimating = false;
            stageTitle.text = "RUN 결과";
            stageBody.text = $"{BuildRunRecord()}\n{BuildArtifactReview()}";
            experienceBar.gameObject.SetActive(false);
            experienceText.gameObject.SetActive(false);
            actionRoot.SetActive(true);
            Focus(retryRunButton);
        }

        private IEnumerator ShowTextStage(string title, string body, float duration)
        {
            stageAnimating = false;
            finishCurrentStage = false;
            experienceBar.gameObject.SetActive(false);
            experienceText.gameObject.SetActive(false);
            stageTitle.text = title;
            stageBody.text = body;
            float elapsed = 0f;
            while (elapsed < duration && !skipToSummary)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator AnimateExperience()
        {
            CharacterProgressDto start = StartingProgress();
            CharacterProgressDto end = savedResponse.data.progress;
            stageTitle.text = $"경험치 +{savedResponse.data.experienceGained}";
            stageBody.text = "";
            experienceBar.gameObject.SetActive(true);
            experienceText.gameObject.SetActive(true);
            int level = start.level;
            int current = start.experience;
            bool firstSegment = true;

            while (level < end.level && !skipToSummary)
            {
                int required = RequiredExperience(level);
                yield return AnimateExperienceSegment(level, current, required, required,
                    firstSegment ? 0.72f : 0.3f);
                if (skipToSummary) break;
                level++;
                current = 0;
                firstSegment = false;
                PlayLevelCue();
                yield return PulseExperienceBar();
            }

            if (!skipToSummary)
            {
                int required = end.experienceToNextLevel > 0
                    ? end.experienceToNextLevel
                    : Mathf.Max(1, RequiredExperience(end.level));
                yield return AnimateExperienceSegment(end.level, current, end.experience, required, 0.5f);
            }

            SetExperience(end.level, end.experience,
                end.experienceToNextLevel > 0 ? end.experienceToNextLevel : Mathf.Max(1, end.experience));
            stageAnimating = false;
            finishCurrentStage = false;
        }

        private IEnumerator AnimateExperienceSegment(int level, int from, int to, int required, float duration)
        {
            stageAnimating = true;
            finishCurrentStage = false;
            float elapsed = 0f;
            while (elapsed < duration && !finishCurrentStage && !skipToSummary)
            {
                elapsed += Time.unscaledDeltaTime;
                float value = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                SetExperience(level, Mathf.RoundToInt(value), required);
                yield return null;
            }
            SetExperience(level, to, required);
            stageAnimating = false;
        }

        private IEnumerator PulseExperienceBar()
        {
            Transform target = experienceBar.transform;
            Vector3 original = Vector3.one;
            float elapsed = 0f;
            while (elapsed < 0.14f && !skipToSummary)
            {
                elapsed += Time.unscaledDeltaTime;
                float pulse = 1f + Mathf.Sin(elapsed / 0.14f * Mathf.PI) * 0.08f;
                target.localScale = original * pulse;
                yield return null;
            }
            target.localScale = original;
        }

        private void ShowSummary()
        {
            sequence = null;
            stageAnimating = false;
            CharacterProgressDto end = savedResponse.data.progress;
            stageTitle.text = "RUN 결과";
            stageBody.text = $"Lv. {end.level}  ·  EXP {end.experience}/{end.experienceToNextLevel}\n" +
                             $"스킬 포인트 {end.skillPoints}\n{BuildRunRecord()}\n{BuildArtifactReview()}";
            experienceBar.gameObject.SetActive(false);
            experienceText.gameObject.SetActive(false);
            actionRoot.SetActive(true);
            Focus(retryRunButton);
        }

        private CharacterProgressDto StartingProgress()
        {
            return payload.StartingProgress ?? new CharacterProgressDto
            {
                characterId = payload.Request.characterId,
                level = 1,
                experience = 0,
                experienceToNextLevel = ExperienceCurve[0],
                skillPoints = 0
            };
        }

        private string BuildRunRecord()
        {
            string time = $"{payload.Request.playTime / 60:00}:{payload.Request.playTime % 60:00}";
            string death = payload.Request.isCleared || string.IsNullOrWhiteSpace(payload.Request.deathReason)
                ? ""
                : $"\n사망 원인  {payload.Request.deathReason}";
            return $"도달 {payload.Request.reachedFloor}층  ·  처치 {payload.Request.killCount}\n플레이 시간 {time}{death}";
        }

        private string BuildArtifactReview()
        {
            RunItemDto[] items = payload.Request.items ?? System.Array.Empty<RunItemDto>();
            if (items.Length == 0) return "획득한 아티팩트 없음";
            IEnumerable<string> summary = items.GroupBy(item => item.itemId)
                .Select(group => group.Count() > 1 ? $"{group.Key} ×{group.Count()}" : group.Key);
            return string.Join("  ·  ", summary);
        }

        private void SetExperience(int level, int value, int required)
        {
            required = Mathf.Max(1, required);
            experienceBar.value = Mathf.Clamp01((float)value / required);
            experienceText.text = $"Lv. {level}    {value} / {required}";
        }

        private static int RequiredExperience(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, ExperienceCurve.Length - 1);
            return ExperienceCurve[index];
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
