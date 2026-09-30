using System;
using System.Security.Cryptography;
using TrickalFanGame.Combat;
using TrickalFanGame.Data;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Meta;
using TrickalFanGame.Network;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Run
{
    [DefaultExecutionOrder(-300)]
    public sealed class RunSession : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerDeathReason playerDeathReason;
        [SerializeField] private RunProgress runProgress;
        [SerializeField] private BossController boss;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerProgressClient playerProgressClient;
        [SerializeField] private GameRunResultTransition resultTransition;

        [Header("Run identity")]
        [SerializeField] private string userId = "00000000-0000-4000-8000-000000000001";
        [SerializeField] private string characterId = "erpin";
        [SerializeField] private bool waitForCharacterSelection;

        [Header("Result status")]
        [SerializeField] private string statusMessage = "Run not started.";
        [SerializeField] private string lastRunId;

        private DateTime startedAt;
        private float startedRealtime;
        private bool hasStarted;
        private bool hasEnded;
        private bool shouldSaveResult = true;
        private bool isSaveInFlight;
        private bool canRetrySave;
        private string clientRunId;
        private CreateRunRequest pendingRequest;
        private IGameApiClient apiClient;
        private static int? lastGeneratedRunSeed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development panel only: the next Run starts with this seed instead of a random one, then it clears.
        public static int? DevelopmentSeedOverride { get; set; }
#endif

        public bool HasStarted => hasStarted;
        public bool HasEnded => hasEnded;
        public bool IsCleared { get; private set; }
        public string UserId => userId;
        public string CharacterId => characterId;
        public string ClientRunId => clientRunId;
        public CreateRunRequest PendingRequest => pendingRequest;
        public bool IsSaveInFlight => isSaveInFlight;
        public bool CanRetrySave => canRetrySave;
        public string StatusMessage => statusMessage;
        public string LastRunId => lastRunId;
        public RunProgress Progress => runProgress;
        public GameRunResultTransition ResultTransition => resultTransition;
        public int RunSeed => runProgress != null && runProgress.HasRunSeed ? runProgress.RunSeed : 0;

        public void Configure(Health configuredPlayer, RunProgress configuredProgress, BossController configuredBoss)
        {
            playerHealth = configuredPlayer;
            runProgress = configuredProgress;
            boss = configuredBoss;
            playerDeathReason = playerHealth != null ? playerHealth.GetComponent<PlayerDeathReason>() : null;
        }

        public void SetWaitForCharacterSelection(bool shouldWait)
        {
            waitForCharacterSelection = shouldWait;
        }

        public void SetResultSavingEnabled(bool enabled)
        {
            shouldSaveResult = enabled;
        }

        public void ConfigureMetaProgression(PlayerProgressClient configuredProgressClient)
        {
            playerProgressClient = configuredProgressClient;
        }

        public void ConfigureInventory(PlayerInventory configuredInventory)
        {
            inventory = configuredInventory;
        }

        public void ConfigureResultTransition(GameRunResultTransition configuredTransition)
        {
            resultTransition = configuredTransition;
        }

        public void SetApiClient(IGameApiClient configuredApiClient)
        {
            apiClient = configuredApiClient;
        }

        public bool ConfigureLaunchIdentity(string configuredUserId, string configuredNickname,
            string configuredCharacterId)
        {
            if (hasStarted || hasEnded || string.IsNullOrWhiteSpace(configuredUserId) ||
                string.IsNullOrWhiteSpace(configuredNickname) || string.IsNullOrWhiteSpace(configuredCharacterId))
            {
                return false;
            }

            userId = configuredUserId;
            characterId = configuredCharacterId;
            waitForCharacterSelection = false;
            playerProgressClient?.Configure(configuredNickname, null, null);
            return true;
        }

        private void Awake()
        {
            if (playerHealth == null)
            {
                playerHealth = FindFirstObjectByType<PlayerMovement>()?.GetComponent<Health>();
            }

            if (runProgress == null)
            {
                runProgress = FindFirstObjectByType<RunProgress>();
            }

            if (playerHealth != null && playerDeathReason == null)
            {
                playerDeathReason = playerHealth.GetComponent<PlayerDeathReason>();
                if (playerDeathReason == null)
                {
                    playerDeathReason = playerHealth.gameObject.AddComponent<PlayerDeathReason>();
                }
            }

            if (playerHealth != null && inventory == null)
            {
                inventory = playerHealth.GetComponent<PlayerInventory>();
            }

            if (playerProgressClient == null)
            {
                playerProgressClient = FindFirstObjectByType<PlayerProgressClient>();
            }

            apiClient ??= ApiClient.Instance;

            EnsureRunSeed();
        }

        private void Start()
        {
            if (waitForCharacterSelection)
            {
                statusMessage = "Select a character to begin.";
                return;
            }

            PrepareAndBeginRun(characterId, null);
        }

        public bool PrepareAndBeginRun(string selectedCharacterId, Action<bool> onCompleted)
        {
            if (hasStarted || hasEnded || string.IsNullOrWhiteSpace(selectedCharacterId))
            {
                onCompleted?.Invoke(false);
                return false;
            }

            if (playerProgressClient == null)
            {
                bool started = BeginRun(selectedCharacterId);
                onCompleted?.Invoke(started);
                return started;
            }

            statusMessage = $"Loading progression: {selectedCharacterId}.";
            return playerProgressClient.LoadAndApply(
                selectedCharacterId,
                _ =>
                {
                    if (!string.IsNullOrWhiteSpace(playerProgressClient.ResolvedUserId))
                    {
                        userId = playerProgressClient.ResolvedUserId;
                    }

                    bool started = BeginRun(selectedCharacterId);
                    onCompleted?.Invoke(started);
                });
        }

        public bool BeginRun(string selectedCharacterId)
        {
            if (hasStarted || hasEnded || string.IsNullOrWhiteSpace(selectedCharacterId))
            {
                return false;
            }

            if (!EnsureRunSeed())
            {
                return false;
            }

            if (playerProgressClient != null && !playerProgressClient.IsAppliedFor(selectedCharacterId))
            {
                playerProgressClient.ApplyFallback(
                    selectedCharacterId,
                    "The Run started without a completed online lookup.");
            }

            characterId = selectedCharacterId;
            clientRunId = Guid.NewGuid().ToString();
            startedAt = DateTime.UtcNow;
            startedRealtime = Time.realtimeSinceStartup;
            hasStarted = true;
            statusMessage = $"Run in progress: {characterId}.";

            if (playerHealth != null)
            {
                playerHealth.Died += OnPlayerDied;
            }
            if (boss != null)
            {
                boss.Died += OnBossDied;
            }
            if (runProgress != null)
            {
                runProgress.FinalBossCleared += OnFinalBossCleared;
            }

            Debug.Log($"[RunSession] Started with character {characterId} and seed {RunSeed}.", this);
            return true;
        }

        private bool EnsureRunSeed()
        {
            if (runProgress == null)
            {
                statusMessage = "Run cannot start without RunProgress.";
                Debug.LogError($"[RunSession] {statusMessage}", this);
                return false;
            }

            if (runProgress.HasRunSeed)
            {
                return true;
            }

            int generatedSeed = CreateRunSeed();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevelopmentSeedOverride.HasValue)
            {
                generatedSeed = DevelopmentSeedOverride.Value;
                DevelopmentSeedOverride = null;
            }
#endif
            if (runProgress.TryInitializeRunSeed(generatedSeed, out string error))
            {
                return true;
            }

            statusMessage = $"Run seed initialization failed: {error}";
            Debug.LogError($"[RunSession] {statusMessage}", this);
            return false;
        }

        private static int CreateRunSeed()
        {
            byte[] bytes = new byte[sizeof(int)];
            int generatedSeed;
            do
            {
                using RandomNumberGenerator generator = RandomNumberGenerator.Create();
                generator.GetBytes(bytes);
                generatedSeed = BitConverter.ToInt32(bytes, 0);
            }
            while (lastGeneratedRunSeed.HasValue && generatedSeed == lastGeneratedRunSeed.Value);

            lastGeneratedRunSeed = generatedSeed;
            return generatedSeed;
        }

        private void OnDestroy()
        {
            UnbindRunEvents();
        }

        private void OnPlayerDied() => EndRun(false, playerDeathReason != null ? playerDeathReason.CurrentReason : "UNKNOWN");
        private void OnBossDied()
        {
            runProgress?.RecordKill();
            EndRun(true, null);
        }
        private void OnFinalBossCleared() => EndRun(true, null);

        private void EndRun(bool isCleared, string deathReason)
        {
            if (!hasStarted || hasEnded)
            {
                return;
            }

            hasEnded = true;
            IsCleared = isCleared;
            runProgress?.StopProgression();
            var endedAt = DateTime.UtcNow;
            pendingRequest = new CreateRunRequest
            {
                clientRunId = clientRunId,
                userId = userId,
                characterId = characterId,
                gameVersion = Application.version,
                startedAt = startedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                endedAt = endedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                playTime = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - startedRealtime)),
                reachedFloor = Mathf.Max(1, runProgress != null ? runProgress.CurrentFloor : 1),
                isCleared = isCleared,
                killCount = runProgress != null ? runProgress.KillCount : 0,
                deathReason = deathReason,
                items = BuildRunItems(endedAt)
            };

            if (!shouldSaveResult)
            {
                statusMessage = isCleared ? "Run cleared." : "Run ended.";
                Debug.Log($"[RunSession] {statusMessage} Result saving is disabled.", this);
                return;
            }

            statusMessage = isCleared ? "Run cleared. Saving result..." : "Run ended. Saving result...";
            if (resultTransition != null && resultTransition.TryTransition(
                    pendingRequest, playerProgressClient != null ? playerProgressClient.AppliedProgress : null,
                    GetValidApiClient()))
            {
                return;
            }
            SubmitPendingRun();
        }

        public bool RetrySave()
        {
            if (!hasEnded || pendingRequest == null || isSaveInFlight || !canRetrySave)
            {
                return false;
            }

            SubmitPendingRun();
            return true;
        }

        public bool TryAbandonToHome()
        {
            if (!hasStarted || hasEnded || isSaveInFlight || resultTransition == null ||
                !resultTransition.CanLoadFrontend())
            {
                return false;
            }

            hasEnded = true;
            shouldSaveResult = false;
            canRetrySave = false;
            pendingRequest = null;
            statusMessage = "Run abandoned. Returning home.";
            runProgress?.StopProgression();
            UnbindRunEvents();
            return resultTransition.TryReturnHomeWithoutResult();
        }

        public bool TryRestartRun()
        {
            string nickname = playerProgressClient != null ? playerProgressClient.UserNickname : null;
            if (!hasStarted || hasEnded || isSaveInFlight || resultTransition == null ||
                !resultTransition.CanReloadGame() || string.IsNullOrWhiteSpace(nickname))
            {
                return false;
            }

            hasEnded = true;
            shouldSaveResult = false;
            canRetrySave = false;
            pendingRequest = null;
            statusMessage = $"Restarting Run with {characterId}.";
            runProgress?.StopProgression();
            UnbindRunEvents();
            return resultTransition.TryRestartRun(userId, nickname, characterId, GetValidApiClient());
        }

        private void SubmitPendingRun()
        {
            IGameApiClient client = GetValidApiClient();
            if (client == null)
            {
                OnSaveFailure("ApiClient is unavailable.");
                return;
            }

            isSaveInFlight = true;
            canRetrySave = false;
            client.PostRun(pendingRequest, OnSaveSuccess, OnSaveFailure);
        }

        private RunItemDto[] BuildRunItems(DateTime endedAt)
        {
            if (inventory == null || inventory.AcquiredItems.Count == 0)
            {
                return Array.Empty<RunItemDto>();
            }

            RunItemDto[] result = new RunItemDto[inventory.AcquiredItems.Count];
            for (int index = 0; index < result.Length; index++)
            {
                AcquiredItem item = inventory.AcquiredItems[index];
                double elapsedSeconds = Math.Max(0d, item.AcquiredRealtime - startedRealtime);
                DateTime acquiredAt = startedAt.AddSeconds(elapsedSeconds);
                if (acquiredAt > endedAt)
                {
                    acquiredAt = endedAt;
                }

                result[index] = new RunItemDto
                {
                    itemId = item.ItemId,
                    floor = item.Floor,
                    order = item.Order,
                    acquiredAt = acquiredAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };
            }

            return result;
        }

        private IGameApiClient GetValidApiClient()
        {
            // Unity objects destroyed at runtime report as == null but not ReferenceEquals null
            if (apiClient != null && (apiClient is not UnityEngine.Object obj || obj != null))
            {
                return apiClient;
            }
            return ApiClient.Instance;
        }

        private void OnSaveSuccess(CreateRunResponse response)
        {
            isSaveInFlight = false;
            canRetrySave = false;

            if (response?.data == null)
            {
                OnSaveFailure("Run response data is unavailable.");
                return;
            }

            LocalPendingRunStorage.Remove(pendingRequest.clientRunId);
            lastRunId = response.data.runId;
            playerProgressClient?.ShowRunResult(response.data);
            CharacterProgressDto progress = response.data.progress;
            statusMessage = progress == null
                ? $"Saved run: {lastRunId}"
                : $"Saved {lastRunId}: +{response.data.experienceGained} XP, " +
                  $"Lv.{progress.level}, XP {progress.experience}/{progress.experienceToNextLevel}, " +
                  $"points {progress.skillPoints}, skills " +
                  $"{progress.lowGradeSkillLevel}/{progress.highGradeSkillLevel}.";
            Debug.Log($"[RunSession] {statusMessage}");
        }

        private void OnSaveFailure(string error)
        {
            isSaveInFlight = false;
            canRetrySave = true;
            LocalPendingRunStorage.Save(pendingRequest);
            statusMessage = $"Run ended; save failed: {error}";
            Debug.LogWarning($"[RunSession] {statusMessage}");
        }

        private void UnbindRunEvents()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            if (boss != null) boss.Died -= OnBossDied;
            if (runProgress != null) runProgress.FinalBossCleared -= OnFinalBossCleared;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 520, canRetrySave ? 76 : 45));
            GUILayout.Label(statusMessage, GUI.skin.box);
            if (canRetrySave && GUILayout.Button("Retry save with the same Run ID"))
            {
                RetrySave();
            }
            GUILayout.EndArea();
        }
    }
}
