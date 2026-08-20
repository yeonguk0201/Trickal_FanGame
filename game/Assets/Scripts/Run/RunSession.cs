using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Network;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Run
{
    public sealed class RunSession : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerDeathReason playerDeathReason;
        [SerializeField] private RunProgress runProgress;
        [SerializeField] private BossController boss;

        [Header("Run identity")]
        [SerializeField] private string userId = "00000000-0000-4000-8000-000000000001";
        [SerializeField] private string characterId = "character-a";

        [Header("Result status")]
        [SerializeField] private string statusMessage = "Run not started.";
        [SerializeField] private string lastRunId;

        private DateTime startedAt;
        private float startedRealtime;
        private bool hasEnded;

        public bool HasEnded => hasEnded;

        public void Configure(Health configuredPlayer, RunProgress configuredProgress, BossController configuredBoss)
        {
            playerHealth = configuredPlayer;
            runProgress = configuredProgress;
            boss = configuredBoss;
            playerDeathReason = playerHealth != null ? playerHealth.GetComponent<PlayerDeathReason>() : null;
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
        }

        private void Start()
        {
            startedAt = DateTime.UtcNow;
            startedRealtime = Time.realtimeSinceStartup;
            statusMessage = "Run in progress.";

            if (playerHealth != null)
            {
                playerHealth.Died += OnPlayerDied;
            }
            if (boss != null)
            {
                boss.Died += OnBossDied;
            }
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            if (boss != null) boss.Died -= OnBossDied;
        }

        private void OnPlayerDied() => EndRun(false, playerDeathReason != null ? playerDeathReason.CurrentReason : "UNKNOWN");
        private void OnBossDied() => EndRun(true, null);

        private void EndRun(bool isCleared, string deathReason)
        {
            if (hasEnded)
            {
                return;
            }

            hasEnded = true;
            runProgress?.StopProgression();
            var endedAt = DateTime.UtcNow;
            var request = new CreateRunRequest
            {
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
                items = Array.Empty<RunItemDto>()
            };

            statusMessage = isCleared ? "Run cleared. Saving result..." : "Run ended. Saving result...";
            if (ApiClient.Instance == null)
            {
                statusMessage = "Run ended, but ApiClient is unavailable.";
                Debug.LogError($"[RunSession] {statusMessage}");
                return;
            }

            ApiClient.Instance.PostRun(request, OnSaveSuccess, OnSaveFailure);
        }

        private void OnSaveSuccess(CreateRunResponse response)
        {
            lastRunId = response.data.runId;
            statusMessage = $"Saved run: {lastRunId}";
            Debug.Log($"[RunSession] {statusMessage}");
        }

        private void OnSaveFailure(string error)
        {
            statusMessage = $"Run ended; save failed: {error}";
            Debug.LogError($"[RunSession] {statusMessage}");
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 380, 45));
            GUILayout.Label(statusMessage, GUI.skin.box);
            GUILayout.EndArea();
        }
    }
}
