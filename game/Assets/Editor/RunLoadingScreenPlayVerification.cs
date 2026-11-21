using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using TrickalFanGame.Meta;
using TrickalFanGame.Network;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // One focused Play check for the four scene transitions touched by Loading-0; no content regressions/setup.
    [InitializeOnLoad]
    public static class RunLoadingScreenPlayVerification
    {
        private const string Pending = "RunLoadingScreenPlayVerification.Pending";
        private const string Nickname = "LoadingScreenVerifier";
        private static readonly Dictionary<string, string> PreviousStrings = new();
        private static bool hadNicknameFlag;
        private static int nicknameFlag;
        private static double started;
        private static int phase, transitions;
        private static bool sawLoading, sawComplete;
        private static string firstRunId, resultRunId;
        private static LoadingApiClient api;

        static RunLoadingScreenPlayVerification() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            RunLoadingScreenVerification.Verify();
            // Save and restore the exact keys, including the distinction between missing and empty.
            foreach (string key in new[] { "UserId", "Nickname", LocalProgression.CacheKey("erpin"),
                $"TrickalFanGame.CharacterProgress.{Nickname}.erpin" })
                PreviousStrings[key] = PlayerPrefs.HasKey(key) ? "1" + PlayerPrefs.GetString(key) : "0";
            hadNicknameFlag = PlayerPrefs.HasKey("NeedsNewNickname");
            nicknameFlag = PlayerPrefs.GetInt("NeedsNewNickname");
            LocalProfile.SaveProfile("loading-verify-user", Nickname);
            RunLaunchContext.Clear();
            RunResultContext.Clear();
            FrontendEntryContext.Clear();
            api = new LoadingApiClient();
            RunLaunchContext.SetVerificationApiClient(api);
            phase = transitions = 0;
            sawLoading = sawComplete = false;
            started = EditorApplication.timeSinceStartup;
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (EditorApplication.timeSinceStartup - started > 150)
            {
                Finish(1, $"Loading Play verification timed out at phase {phase}.");
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 8) return;
            try
            {
                if (RunSceneLoader.IsLoading)
                {
                    sawLoading = true;
                    Require(RunSceneLoader.Current.View.IsVisible && Time.timeScale == 0f,
                        "Scene transition lost its visible overlay or resumed game time early.");
                    Require(!RunSceneLoader.TryLoad(0), "A second transition was accepted while loading.");
                    if (RunSceneLoader.Current.View.Progress == 1f) sawComplete = true;
                    // Result animation is still waiting even if the offline response already arrived.
                    FrontendRunResultView waiting = Object.FindFirstObjectByType<FrontendRunResultView>();
                    if (phase == 5 && waiting != null && waiting.IsShowing)
                        Require(!waiting.IsAnimatingExperience, "Result animation played behind loading.");
                    return;
                }
                switch (phase)
                {
                    case 0:
                        LaunchFromHome();
                        phase++;
                        break;
                    case 1:
                        RunSession first = Session();
                        if (first == null) return;
                        Completed(1f);
                        firstRunId = first.ClientRunId;
                        Require(first.TryRestartRun(), "In-game restart did not start loading.");
                        phase++;
                        break;
                    case 2:
                        RunSession restarted = Session();
                        if (restarted == null) return;
                        Completed(1f);
                        Require(restarted.ClientRunId != firstRunId && api.PostCount == 0,
                            "Restart reused a Run ID or saved an abandoned Run.");
                        Require(restarted.TryAbandonToHome(), "Leave Run did not start loading.");
                        phase++;
                        break;
                    case 3:
                        if (SceneManager.GetActiveScene().path != Week13FrontendSetup.ScenePath) return;
                        Completed(1f);
                        FrontendTitleView home = Object.FindFirstObjectByType<FrontendTitleView>();
                        Require(home.HomeView.HomePanel.activeInHierarchy && !RunResultContext.HasPending,
                            "Leaving Run did not return to the clean home screen.");
                        LaunchFromHome();
                        phase++;
                        break;
                    case 4:
                        RunSession last = Session();
                        if (last == null) return;
                        Completed(1f);
                        resultRunId = "loading-verify-" + Guid.NewGuid();
                        var request = new CreateRunRequest
                        {
                            clientRunId = resultRunId, userId = "loading-verify-user", characterId = "erpin",
                            startedAt = DateTime.UtcNow.AddMinutes(-1).ToString("o"),
                            endedAt = DateTime.UtcNow.ToString("o"), gameVersion = Application.version,
                            reachedFloor = 1, killCount = 0, items = Array.Empty<RunItemDto>()
                        };
                        Require(last.ResultTransition.TryTransition(request, MetaProgressionRules.CreateDefault("erpin"), api),
                            "Game-to-result transition did not start loading.");
                        phase++;
                        break;
                    case 5:
                        if (SceneManager.GetActiveScene().path != Week13FrontendSetup.ScenePath) return;
                        Completed(0f);
                        FrontendRunResultView result = Object.FindFirstObjectByType<FrontendRunResultView>();
                        Require(result != null && result.IsShowing && api.PostCount == 1 && RunResultContext.HasPending,
                            "Result transition lost its payload or submitted the same Run more than once.");
                        Require(transitions == 5, "Not all requested scene transitions were observed.");
                        Finish(0, "Run loading Play verification passed: launch, restart, abandon-to-home, relaunch and " +
                            "result return; visible overlay/100%, blocked duplicate loads, restored input/time, fresh Run ID, " +
                            "one result submission and deferred result animation.");
                        break;
                }
            }
            catch (Exception exception) { Finish(1, exception.ToString()); }
        }

        private static RunSession Session()
        {
            if (SceneManager.GetActiveScene().path != Week13FrontendSetup.GameScenePath) return null;
            RunSession session = Object.FindFirstObjectByType<RunSession>();
            return session != null && session.HasStarted ? session : null;
        }

        private static void LaunchFromHome()
        {
            FrontendTitleView title = Object.FindFirstObjectByType<FrontendTitleView>();
            Require(title != null, "No Frontend title for launch.");
            title.RequestStart();
            title.HomeView.GameStartButton.onClick.Invoke();
            FrontendCharacterSelectionView selection = title.HomeView.CharacterSelectionView;
            selection.Cards.First(c => c.Character.CharacterId == "erpin").Button.onClick.Invoke();
            selection.ConfirmButton.onClick.Invoke();
            Require(RunSceneLoader.IsLoading, "Frontend confirmation did not open the loading screen.");
        }

        private static void Completed(float expectedTime)
        {
            Require(sawLoading && sawComplete, "Loading never displayed its overlay and 100% completion.");
            Require(!RunSceneLoader.Current.View.IsVisible && Time.timeScale == expectedTime &&
                EventSystem.current != null && EventSystem.current.enabled, "Loading did not restore input/time.");
            Require(Object.FindObjectsByType<RunSceneLoader>(FindObjectsSortMode.None).Length == 1,
                "Scene reload duplicated the persistent loader.");
            transitions++;
            sawLoading = sawComplete = false;
        }

        private static void Finish(int code, string message)
        {
            SessionState.SetBool(Pending, false);
            RunLaunchContext.Clear();
            RunResultContext.Clear();
            FrontendEntryContext.Clear();
            if (resultRunId != null) LocalPendingRunStorage.Remove(resultRunId);
            foreach (var pair in PreviousStrings)
            {
                if (pair.Value.StartsWith("1")) PlayerPrefs.SetString(pair.Key, pair.Value.Substring(1));
                else PlayerPrefs.DeleteKey(pair.Key);
            }
            if (hadNicknameFlag) PlayerPrefs.SetInt("NeedsNewNickname", nicknameFlag);
            else PlayerPrefs.DeleteKey("NeedsNewNickname");
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(code);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class LoadingApiClient : IGameApiClient
        {
            public int PostCount;
            public void GetUser(string nickname, Action<UserProfileResponse> success, Action<string> error) =>
                success?.Invoke(new UserProfileResponse
                {
                    success = true, data = new UserProfileData { id = "loading-verify-user", nickname = nickname,
                        characterProgress = new[] { MetaProgressionRules.CreateDefault("erpin") } }
                });
            public void PostRun(CreateRunRequest request, Action<CreateRunResponse> success, Action<string> error)
            {
                PostCount++;
                error?.Invoke("NETWORK_ERROR: loading screen verification (intentional offline response)");
            }
            public void PostUser(CreateUserRequest request, Action<CreateUserResponse> success, Action<string> error) =>
                throw new InvalidOperationException("Loading verification must not register a user.");
        }
    }
}
