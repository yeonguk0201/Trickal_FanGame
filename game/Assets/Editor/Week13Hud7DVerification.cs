using System;
using System.Linq;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
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
    public static class Week13Hud7DVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-7D Restart Run")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GamePauseArtifactView view = FindAll<GamePauseArtifactView>(scene).Single();
            Assert(view.RestartRunButton != null && view.ConfirmationTitle != null &&
                   view.ConfirmationWarning != null && view.ConfirmationActionLabel != null,
                "HUD-7D runtime references are missing.");
            Assert(view.RestartRunButton.transform.IsChildOf(view.FocusScope) &&
                   view.RestartRunButton.GetComponentInChildren<TMPro.TMP_Text>().text == "처음부터 다시하기",
                "HUD-7D restart action is missing from the pause focus scope.");
            Assert(view.RunSession != null && view.RunSession.ResultTransition != null &&
                   view.RunSession.ResultTransition.CanReloadGame(),
                "HUD-7D requires a reloadable Game Scene transition.");
            Debug.Log("Week 13 HUD-7D scene verification passed: restart action and shared safe confirmation are configured.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Hud7DSetup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud7DSetup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-7D setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-7D setup changed the Game Scene GUID.");
            Verify();
            Week13Hud7CVerification.Verify();
            Debug.Log("Week 13 HUD-7D batch verification passed: setup twice, stable Scene GUID and HUD-7C regression.");
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
    public static class Week13Hud7DPlayVerification
    {
        private const string PendingKey = "Week13Hud7DPlayVerification.Pending";
        private static bool hadProfile;
        private static string previousUserId;
        private static string previousNickname;
        private static double started;
        private static int phase;
        private static string previousClientRunId;
        private static int previousSeed;
        private static Hud7DApiClient apiClient;

        static Week13Hud7DPlayVerification()
        {
            EditorApplication.update += Tick;
        }

        public static void RunBatch()
        {
            hadProfile = LocalProfile.IsRegistered;
            previousUserId = LocalProfile.UserId;
            previousNickname = LocalProfile.Nickname;
            LocalProfile.SaveProfile("hud7d-local-user", "Hud7DTester");
            RunLaunchContext.Clear();
            RunResultContext.Clear();
            FrontendEntryContext.Clear();
            apiClient = new Hud7DApiClient();
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
                if (EditorApplication.timeSinceStartup - started > 120) Finish(1, "HUD-7D Play Mode timeout.");
                return;
            }
            if (Time.frameCount < 8) return;

            try
            {
                switch (phase)
                {
                    case 0: LaunchRun(); break;
                    case 1: OpenAndCancelRestart(); break;
                    case 2: ConfirmRestart(); break;
                    case 3: VerifyFreshRun(); break;
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

        private static void OpenAndCancelRestart()
        {
            if (SceneManager.GetActiveScene().path != Week13FrontendSetup.GameScenePath) return;
            RunSession session = Object.FindFirstObjectByType<RunSession>();
            GamePauseArtifactView view = Object.FindFirstObjectByType<GamePauseArtifactView>();
            if (session == null || view == null || !session.HasStarted) return;
            previousClientRunId = session.ClientRunId;
            previousSeed = session.RunSeed;
            Week13Hud7DVerification.Assert(view.TryPause() && view.ShowRestartConfirmation(),
                "HUD-7D could not open restart confirmation from an active Run.");
            Week13Hud7DVerification.Assert(view.IsConfirmingRestart && view.IsPaused &&
                view.ConfirmationTitle.text == "처음부터 다시 시작할까요?" &&
                view.ConfirmationWarning.text.Contains("같은 캐릭터") &&
                view.ConfirmationActionLabel.text == "다시 시작" &&
                EventSystem.current.currentSelectedGameObject == view.CancelLeaveButton.gameObject,
                "HUD-7D confirmation did not show restart guidance or focus the safe action.");
            view.CancelLeaveButton.onClick.Invoke();
            Week13Hud7DVerification.Assert(view.IsPaused && !view.IsConfirmingRestart &&
                session.HasStarted && !session.HasEnded && session.ClientRunId == previousClientRunId &&
                session.RunSeed == previousSeed && session.PendingRequest == null &&
                apiClient.PostRunCount == 0 && Mathf.Approximately(Time.timeScale, 0f),
                "Cancelling HUD-7D restart changed or saved the active Run.");
            phase++;
        }

        private static void ConfirmRestart()
        {
            GamePauseArtifactView view = Object.FindFirstObjectByType<GamePauseArtifactView>();
            if (view == null) return;
            Week13Hud7DVerification.Assert(view.ShowRestartConfirmation(),
                "HUD-7D restart confirmation could not be reopened after cancellation.");
            view.ConfirmLeaveButton.onClick.Invoke();
            phase++;
        }

        private static void VerifyFreshRun()
        {
            if (SceneManager.GetActiveScene().path != Week13FrontendSetup.GameScenePath) return;
            RunSession session = Object.FindFirstObjectByType<RunSession>();
            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
            if (session == null || !session.HasStarted || inventory == null) return;
            Week13Hud7DVerification.Assert(session.UserId == "hud7d-local-user" &&
                session.CharacterId == "erpin" && session.ClientRunId != previousClientRunId &&
                session.RunSeed != previousSeed && !session.HasEnded && session.PendingRequest == null &&
                session.Progress.CurrentFloor == 1 && session.Progress.CurrentRoom == 1 &&
                session.Progress.KillCount == 0 && inventory.AcquiredItems.Count == 0 &&
                apiClient.PostRunCount == 0 && !RunLaunchContext.HasPending &&
                !RunResultContext.HasPending && Mathf.Approximately(Time.timeScale, 1f),
                "HUD-7D did not create a clean same-character Run with new identity and seed.");
            Finish(0, "Week 13 HUD-7D Play Mode verification passed: cancel preserves the Run; confirm starts a clean same-character Run without result save or reward.");
        }

        private static void Finish(int code, string message)
        {
            SessionState.SetBool(PendingKey, false);
            RunLaunchContext.Clear();
            RunResultContext.Clear();
            FrontendEntryContext.Clear();
            Time.timeScale = 1f;
            PlayerPrefs.DeleteKey("TrickalFanGame.CharacterProgress.Hud7DTester.erpin");
            PlayerPrefs.Save();
            if (hadProfile) LocalProfile.SaveProfile(previousUserId, previousNickname);
            else LocalProfile.ClearProfile();
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(code);
        }

        private sealed class Hud7DApiClient : IGameApiClient
        {
            public int PostRunCount { get; private set; }

            public void GetUser(string nickname, Action<UserProfileResponse> onSuccess, Action<string> onError)
            {
                onSuccess?.Invoke(new UserProfileResponse
                {
                    success = true,
                    data = new UserProfileData
                    {
                        id = "hud7d-local-user",
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
                onError?.Invoke("HUD-7D must not submit a Run result.");
            }

            public void PostUser(CreateUserRequest request, Action<CreateUserResponse> onSuccess, Action<string> onError)
            {
                throw new InvalidOperationException("HUD-7D must not register a user.");
            }
        }
    }
}
