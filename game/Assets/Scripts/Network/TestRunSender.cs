using System;
using UnityEngine;

namespace TrickalFanGame.Network
{
    public class TestRunSender : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private string testUserId = "00000000-0000-4000-8000-000000000001";
        [SerializeField] private string testCharacterId = "character-a";
        [SerializeField] private bool sendAsCleared = false;

        [Header("Status")]
        [SerializeField] private string lastRunId;
        [SerializeField] private string lastError;

        [ContextMenu("Send Death Run")]
        public void SendDeathRun()
        {
            SendTestRun(false);
        }

        [ContextMenu("Send Clear Run")]
        public void SendClearRun()
        {
            SendTestRun(true);
        }

        public void SendTestRun(bool isCleared)
        {
            if (ApiClient.Instance == null)
            {
                Debug.LogError("[TestRunSender] ApiClient not found. Add ApiClient to the scene.");
                return;
            }

            var now = DateTime.UtcNow;
            var startedAt = now.AddMinutes(-5);

            var request = new CreateRunRequest
            {
                userId = testUserId,
                characterId = testCharacterId,
                gameVersion = Application.version,
                startedAt = startedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endedAt = now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                playTime = 300,
                reachedFloor = isCleared ? 10 : 3,
                isCleared = isCleared,
                killCount = isCleared ? 50 : 15,
                deathReason = isCleared ? null : "ENEMY",
                items = Array.Empty<RunItemDto>()
            };

            Debug.Log($"[TestRunSender] Sending {(isCleared ? "Clear" : "Death")} Run...");

            ApiClient.Instance.PostRun(
                request,
                response =>
                {
                    lastRunId = response.data.runId;
                    lastError = null;
                    Debug.Log($"[TestRunSender] Success! RunId: {lastRunId}");
                },
                error =>
                {
                    lastRunId = null;
                    lastError = error;
                    Debug.LogError($"[TestRunSender] Failed: {error}");
                }
            );
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, 150));
            GUILayout.Label("Test Run Sender", GUI.skin.box);

            if (GUILayout.Button("Send Death Run"))
            {
                SendDeathRun();
            }

            if (GUILayout.Button("Send Clear Run"))
            {
                SendClearRun();
            }

            if (!string.IsNullOrEmpty(lastRunId))
            {
                GUILayout.Label($"Last: {lastRunId.Substring(0, 8)}...");
            }

            if (!string.IsNullOrEmpty(lastError))
            {
                GUILayout.Label($"Error: {lastError}", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.red } });
            }

            GUILayout.EndArea();
        }
#endif
    }
}
