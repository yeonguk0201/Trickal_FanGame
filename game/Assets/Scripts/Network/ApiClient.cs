using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TrickalFanGame.Network
{
    public class ApiClient : MonoBehaviour, IGameApiClient, IUserRegistrationClient, ISkillProgressApiClient
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

        public void PostUser(CreateUserRequest request, Action<CreateUserResponse> onSuccess, Action<string> onError)
        {
            StartCoroutine(PostUserCoroutine(request, onSuccess, onError));
        }

        public void UpgradeSkill(
            string nickname,
            string characterId,
            SkillType skillType,
            int targetLevel,
            Action<CharacterProgressDto> onSuccess,
            Action<SkillUpgradeError> onError)
        {
            StartCoroutine(UpgradeSkillCoroutine(
                nickname, characterId, skillType, targetLevel, onSuccess, onError));
        }

        private System.Collections.IEnumerator UpgradeSkillCoroutine(
            string nickname,
            string characterId,
            SkillType skillType,
            int targetLevel,
            Action<CharacterProgressDto> onSuccess,
            Action<SkillUpgradeError> onError)
        {
            string skillPath = skillType == SkillType.LowGrade ? "LOW_GRADE" : "HIGH_GRADE";
            string url = $"{baseUrl}/users/{UnityWebRequest.EscapeURL(nickname)}/characters/" +
                $"{UnityWebRequest.EscapeURL(characterId)}/skills/{skillPath}";
            string json = JsonUtility.ToJson(new UpgradeSkillRequest { targetLevel = targetLevel });
            using var webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT);
            webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.timeout = Mathf.Max(1, Mathf.CeilToInt(timeout));

            yield return webRequest.SendWebRequest();

            string responseText = webRequest.downloadHandler?.text ?? string.Empty;
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(SkillUpgradeError.FromResponse(
                    webRequest.responseCode, webRequest.result, webRequest.error, responseText));
                yield break;
            }

            try
            {
                CharacterProgressResponse response = JsonUtility.FromJson<CharacterProgressResponse>(responseText);
                if (response != null && response.success && response.data != null)
                    onSuccess?.Invoke(response.data);
                else
                    onError?.Invoke(SkillUpgradeError.FromApiError(
                        webRequest.responseCode, response?.error?.code, response?.error?.message));
            }
            catch (Exception exception)
            {
                onError?.Invoke(SkillUpgradeError.ParseFailure(exception.Message));
            }
        }

        public void PostUserWithErrorInfo(
            CreateUserRequest request,
            Action<CreateUserResponse> onSuccess,
            Action<RegistrationError> onError)
        {
            StartCoroutine(PostUserWithErrorInfoCoroutine(request, onSuccess, onError));
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

        private System.Collections.IEnumerator PostUserCoroutine(
            CreateUserRequest request,
            Action<CreateUserResponse> onSuccess,
            Action<string> onError)
        {
            string url = $"{baseUrl}/users";
            string json = JsonUtility.ToJson(request);

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
                var response = JsonUtility.FromJson<CreateUserResponse>(responseText);
                if (response.success)
                {
                    Debug.Log($"[ApiClient] User created/retrieved: {response.data.nickname}");
                    onSuccess?.Invoke(response);
                }
                else
                {
                    string error = response.error != null
                        ? $"{response.error.code}: {response.error.message}"
                        : "User creation failed.";
                    onError?.Invoke(error);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ApiClient] Parse error: {e.Message}");
                onError?.Invoke($"Parse error: {e.Message}");
            }
        }

        private System.Collections.IEnumerator PostUserWithErrorInfoCoroutine(
            CreateUserRequest request,
            Action<CreateUserResponse> onSuccess,
            Action<RegistrationError> onError)
        {
            string url = $"{baseUrl}/users";
            string json = JsonUtility.ToJson(request);

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
            long httpStatus = webRequest.responseCode;

            if (enableLogging)
            {
                Debug.Log($"[ApiClient] Response ({httpStatus}): {responseText}");
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                var registrationError = BuildRegistrationError(webRequest, responseText);
                Debug.LogError($"[ApiClient] Registration error: {registrationError.Type} - {registrationError.Message}");
                onError?.Invoke(registrationError);
                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<CreateUserResponse>(responseText);
                if (response.success)
                {
                    Debug.Log($"[ApiClient] User created/retrieved: {response.data.nickname}");
                    onSuccess?.Invoke(response);
                }
                else
                {
                    string code = response.error?.code ?? "UNKNOWN";
                    string message = response.error?.message ?? "User creation failed.";
                    var registrationError = RegistrationError.FromApiError(httpStatus, code, message);
                    onError?.Invoke(registrationError);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ApiClient] Parse error: {e.Message}");
                onError?.Invoke(RegistrationError.ParseFailure(e.Message));
            }
        }

        private static RegistrationError BuildRegistrationError(UnityWebRequest webRequest, string responseText)
        {
            long httpStatus = webRequest.responseCode;

            if (webRequest.result == UnityWebRequest.Result.ConnectionError)
            {
                return RegistrationError.NetworkFailure(webRequest.error);
            }

            if (string.IsNullOrEmpty(responseText))
            {
                return RegistrationError.FromApiError(httpStatus, "HTTP_ERROR", webRequest.error);
            }

            try
            {
                ApiErrorResponse response = JsonUtility.FromJson<ApiErrorResponse>(responseText);
                string code = response?.error?.code ?? "UNKNOWN";
                string message = response?.error?.message ?? webRequest.error ?? "Unknown error";
                return RegistrationError.FromApiError(httpStatus, code, message);
            }
            catch
            {
                return RegistrationError.FromApiError(httpStatus, "HTTP_ERROR", webRequest.error);
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
