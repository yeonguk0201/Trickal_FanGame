using System;
using System.Linq;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using TrickalFanGame.Network;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud7CVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-7C Leave Run")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GamePauseArtifactView view = FindAll<GamePauseArtifactView>(scene).Single();
            RunSession session = FindAll<RunSession>(scene).Single();
            Assert(view.RunSession == session && view.LeaveRunButton != null && view.LeaveConfirmation != null &&
                   view.ConfirmLeaveButton != null && view.CancelLeaveButton != null &&
                   view.ConfirmationFocusScope != null,
                "HUD-7C runtime references are missing.");
            Assert(view.LeaveRunButton.transform.IsChildOf(view.FocusScope) &&
                   view.ConfirmationFocusScope.transform.IsChildOf(view.LeaveConfirmation.transform),
                "HUD-7C controls are outside their required focus scopes.");
            Assert(view.LeaveRunButton.GetComponentInChildren<TMPro.TMP_Text>().text == "홈으로 나가기" &&
                   view.ConfirmLeaveButton.GetComponentInChildren<TMPro.TMP_Text>().text == "Run 끝내기" &&
                   view.CancelLeaveButton.GetComponentInChildren<TMPro.TMP_Text>().text == "계속 플레이",
                "HUD-7C destructive and safe actions are not labeled distinctly.");
            Assert(view.LeaveConfirmation.alpha == 0f && !view.LeaveConfirmation.interactable &&
                   !view.LeaveConfirmation.blocksRaycasts,
                "HUD-7C confirmation must start hidden and non-interactive.");
            Assert(session.ResultTransition != null && session.ResultTransition.CanLoadFrontend(),
                "HUD-7C requires the configured Frontend transition from Flow-5.");
            Debug.Log("Week 13 HUD-7C scene verification passed: confirmation, focus scopes and clean-return transition are configured.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Hud7CSetup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud7CSetup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-7C setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-7C setup changed the Game Scene GUID.");
            Verify();
            Week13Hud7BVerification.ValidateScene(EditorSceneManager.GetActiveScene());
            Week13Flow5Verification.Verify();
            Debug.Log("Week 13 HUD-7C batch verification passed: setup twice, stable Scene GUID and HUD-7B/Flow-5 regressions.");
        }

        internal static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static int CountTransforms(Scene scene) => scene.GetRootGameObjects()
            .Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
    }

    [InitializeOnLoad]
    public static class Week13Hud7CPlayVerification
    {
        private const string PendingKey = "Week13Hud7CPlayVerification.Pending";
        private static bool hadProfile;
        private static string previousUserId;
        private static string previousNickname;
        private static double started;
        private static int phase;
        private static string activeClientRunId;
        private static Hud7CApiClient apiClient;

        static Week13Hud7CPlayVerification()
        {
            EditorApplication.update += Tick;
        }

        public static void RunBatch()
        {
            hadProfile = LocalProfile.IsRegistered;
            previousUserId = LocalProfile.UserId;
            previousNickname = LocalProfile.Nickname;
            LocalProfile.SaveProfile("hud7c-local-user", "Hud7CTester");
            RunLaunchContext.Clear();
            RunResultContext.Clear();
            FrontendEntryContext.Clear();
            apiClient = new Hud7CApiClient();
            RunLaunchContext.SetVerificationApiClient(apiClient);
            phase = 0;
            started = EditorApplication.timeSinceStartup;
            SessionState.SetBool(PendingKey, true);
            EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                if (EditorApplication.timeSinceStartup - started > 120) Finish(1, "HUD-7C Play Mode timeout.");
                return;
            }
            if (Time.frameCount < 8) return;

            try
            {
                switch (phase)
                {
                    case 0: LaunchRun(); break;
                    case 1: OpenAndCancelConfirmation(); break;
                    case 2: ConfirmAndReturnHome(); break;
                    case 3: VerifyCleanHome(); break;
                }
            }
            catch (Exception exception)
            {
                Finish(1, exception.ToString());
            }
        }

        private static void LaunchRun()
        {
            FrontendTitleView frontend = Object.FindFirstObjectByType<FrontendTitleView>();
            if (frontend == null) return;
            frontend.RequestStart();
            frontend.HomeView.GameStartButton.onClick.Invoke();
            FrontendCharacterSelectionView selection = frontend.HomeView.CharacterSelectionView;
            selection.Cards[0].Button.onClick.Invoke();
            selection.ConfirmButton.onClick.Invoke();
            phase++;
        }

        private static void OpenAndCancelConfirmation()
        {
            if (SceneManager.GetActiveScene().path != Week13FrontendSetup.GameScenePath) return;
            RunSession session = Object.FindFirstObjectByType<RunSession>();
            GamePauseArtifactView view = Object.FindFirstObjectByType<GamePauseArtifactView>();
            if (session == null || view == null || !session.HasStarted) return;
            activeClientRunId = session.ClientRunId;
            Week13Hud7CVerification.Assert(view.TryPause(), "HUD-7C could not pause the active Run.");
            Week13Hud7CVerification.Assert(view.ShowLeaveConfirmation(),
                "HUD-7C could not open leave confirmation from pause.");
            Week13Hud7CVerification.Assert(view.IsPaused && view.IsConfirmingLeave &&
                Mathf.Approximately(Time.timeScale, 0f) &&
                EventSystem.current.currentSelectedGameObject == view.CancelLeaveButton.gameObject,
                "HUD-7C confirmation did not preserve pause or focus the safe action.");
            view.CancelLeaveButton.onClick.Invoke();
            Week13Hud7CVerification.Assert(view.IsPaused && !view.IsConfirmingLeave &&
                session.HasStarted && !session.HasEnded && session.ClientRunId == activeClientRunId &&
                session.PendingRequest == null && apiClient.PostRunCount == 0 &&
                Mathf.Approximately(Time.timeScale, 0f),
                "Cancelling HUD-7C confirmation changed or saved the active Run.");
            phase++;
        }

        private static void ConfirmAndReturnHome()
        {
            GamePauseArtifactView view = Object.FindFirstObjectByType<GamePauseArtifactView>();
            if (view == null) return;
            Week13Hud7CVerification.Assert(view.ShowLeaveConfirmation(),
                "HUD-7C confirmation could not be reopened after cancellation.");
            view.ConfirmLeaveButton.onClick.Invoke();
            phase++;
        }

        private static void VerifyCleanHome()
        {
            if (SceneManager.GetActiveScene().path != Week13FrontendSetup.ScenePath) return;
            FrontendTitleView frontend = Object.FindFirstObjectByType<FrontendTitleView>();
            if (frontend == null || !frontend.HomeView.HomePanel.activeInHierarchy) return;
            Week13Hud7CVerification.Assert(apiClient.PostRunCount == 0 && !RunResultContext.HasPending &&
                !RunLaunchContext.HasPending && !FrontendEntryContext.HasHomeRequest &&
                Object.FindFirstObjectByType<RunSession>() == null &&
                Object.FindFirstObjectByType<RunProgress>() == null &&
                Mathf.Approximately(Time.timeScale, 1f) &&
                frontend.HomeView.CurrentDestination == null &&
                EventSystem.current.currentSelectedGameObject == frontend.HomeView.GameStartButton.gameObject,
                "HUD-7C home return leaked Run state, created a result, or failed to restore Frontend input.");
            Finish(0, "Week 13 HUD-7C Play Mode verification passed: cancel preserves the Run; confirm returns home without result save or reward.");
        }

        private static void Finish(int code, string message)
        {
            SessionState.SetBool(PendingKey, false);
            RunLaunchContext.Clear();
            RunResultContext.Clear();
            FrontendEntryContext.Clear();
            Time.timeScale = 1f;
            PlayerPrefs.DeleteKey("TrickalFanGame.CharacterProgress.Hud7CTester.erpin");
            PlayerPrefs.Save();
            if (hadProfile) LocalProfile.SaveProfile(previousUserId, previousNickname);
            else LocalProfile.ClearProfile();
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(code);
        }

        private sealed class Hud7CApiClient : IGameApiClient
        {
            public int PostRunCount { get; private set; }

            public void GetUser(string nickname, Action<UserProfileResponse> onSuccess, Action<string> onError)
            {
                onSuccess?.Invoke(new UserProfileResponse
                {
                    success = true,
                    data = new UserProfileData
                    {
                        id = "hud7c-local-user",
                        nickname = nickname,
                        characterProgress = new[]
                        {
                            new CharacterProgressDto
                            {
                                characterId = "erpin", level = 1, experience = 0,
                                experienceToNextLevel = 400, skillPoints = 0,
                                lowGradeSkillLevel = 1, highGradeSkillLevel = 1
                            }
                        }
                    }
                });
            }

            public void PostRun(CreateRunRequest request, Action<CreateRunResponse> onSuccess, Action<string> onError)
            {
                PostRunCount++;
                onError?.Invoke("HUD-7C must not submit a Run result.");
            }

            public void PostUser(CreateUserRequest request, Action<CreateUserResponse> onSuccess, Action<string> onError)
            {
                throw new InvalidOperationException("HUD-7C must not register a user.");
            }
        }
    }
}
