using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TrickalFanGame.Network
{
    public class ApiClient : MonoBehaviour, IGameApiClient
    {
        [Header("API Settings")]
        [SerializeField] private string baseUrl = "http://localhost:3001/api";
        [SerializeField] private float timeout = 10f;
        [SerializeField] private bool enableLogging = true;

        private static ApiClient _instance;
        public static ApiClient Instance => _instance != null ? _instance : null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void PostRun(CreateRunRequest request, Action<CreateRunResponse> onSuccess, Action<string> onError)
        {
            StartCoroutine(PostRunCoroutine(request, onSuccess, onError));
        }

        public void GetUser(string nickname, Action<UserProfileResponse> onSuccess, Action<string> onError)
        {
            StartCoroutine(GetUserCoroutine(nickname, onSuccess, onError));
        }

        private System.Collections.IEnumerator GetUserCoroutine(
            string nickname,
            Action<UserProfileResponse> onSuccess,
            Action<string> onError)
        {
            string url = $"{baseUrl}/users/{UnityWebRequest.EscapeURL(nickname)}";
            using var webRequest = UnityWebRequest.Get(url);
            webRequest.timeout = Mathf.Max(1, Mathf.CeilToInt(timeout));

            if (enableLogging)
            {
                Debug.Log($"[ApiClient] GET {url}");
            }

            yield return webRequest.SendWebRequest();

            string responseText = webRequest.downloadHandler?.text ?? "";
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(BuildRequestError(webRequest, responseText));
                yield break;
            }

            try
            {
                UserProfileResponse response = JsonUtility.FromJson<UserProfileResponse>(responseText);
                if (response != null && response.success && response.data != null)
                {
                    onSuccess?.Invoke(response);
                }
                else
                {
                    onError?.Invoke(FormatApiError(response?.error, "User progression lookup failed."));
                }
            }
            catch (Exception exception)
            {
                onError?.Invoke($"Parse error: {exception.Message}");
            }
        }

        private System.Collections.IEnumerator PostRunCoroutine(
            CreateRunRequest request,
            Action<CreateRunResponse> onSuccess,
            Action<string> onError)
        {
            string url = $"{baseUrl}/runs";
            string json = SerializeRunRequest(request);

            if (enableLogging)
            {
                Debug.Log($"[ApiClient] POST {url}");
                Debug.Log($"[ApiClient] Request Body: {json}");
            }

            using var webRequest = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.timeout = (int)timeout;

            yield return webRequest.SendWebRequest();

            string responseText = webRequest.downloadHandler?.text ?? "";

            if (enableLogging)
            {
                Debug.Log($"[ApiClient] Response ({webRequest.responseCode}): {responseText}");
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                string error = BuildRequestError(webRequest, responseText);
                Debug.LogError($"[ApiClient] Error: {error}");
                onError?.Invoke(error);
                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<CreateRunResponse>(responseText);
                if (response.success)
                {
                    Debug.Log($"[ApiClient] Run created: {response.data.runId}");
                    onSuccess?.Invoke(response);
                }
                else
                {
                    string error = response.error != null
                        ? $"{response.error.code}: {response.error.message}"
                        : "Run creation failed.";
                    onError?.Invoke(error);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ApiClient] Parse error: {e.Message}");
                onError?.Invoke($"Parse error: {e.Message}");
            }
        }

        private static string BuildRequestError(UnityWebRequest webRequest, string responseText)
        {
            string fallback = $"HTTP {webRequest.responseCode}: {webRequest.error}";
            if (string.IsNullOrEmpty(responseText))
            {
                return fallback;
            }

            try
            {
                ApiErrorResponse response = JsonUtility.FromJson<ApiErrorResponse>(responseText);
                return FormatApiError(response?.error, fallback);
            }
            catch
            {
                return fallback;
            }
        }

        private static string FormatApiError(ApiError error, string fallback)
        {
            return error != null && !string.IsNullOrEmpty(error.message)
                ? $"{error.code}: {error.message}"
                : fallback;
        }

        private static string SerializeRunRequest(CreateRunRequest request)
        {
            if (!request.isCleared || !string.IsNullOrEmpty(request.deathReason))
            {
                return JsonUtility.ToJson(request);
            }

            var clearRequest = new CreateClearRunRequest
            {
                clientRunId = request.clientRunId,
                userId = request.userId,
                characterId = request.characterId,
                gameVersion = request.gameVersion,
                startedAt = request.startedAt,
                endedAt = request.endedAt,
                playTime = request.playTime,
                reachedFloor = request.reachedFloor,
                isCleared = request.isCleared,
                killCount = request.killCount,
                items = request.items
            };

            return JsonUtility.ToJson(clearRequest);
        }

        [Serializable]
        private class CreateClearRunRequest
        {
            public string clientRunId;
            public string userId;
            public string characterId;
            public string gameVersion;
            public string startedAt;
            public string endedAt;
            public int playTime;
            public int reachedFloor;
            public bool isCleared;
            public int killCount;
            public RunItemDto[] items;
        }
    }
}
