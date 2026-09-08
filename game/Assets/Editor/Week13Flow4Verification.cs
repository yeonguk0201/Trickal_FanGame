using System;
using System.Linq;
using TrickalFanGame.Character;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Network;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week13Flow4Verification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify Flow-4 Run Launch")]
        public static void Verify()
        {
            ValidateFrontend(EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single));
            ValidateGame(EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single));
            EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            Debug.Log("Week 13 Flow-4 verification passed: launch bridge, Game bootstrap, references and legacy selector isolation.");
        }

        public static void SetupAndVerifyBatch()
        {
            string frontendGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath);
            string gameGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Flow4Setup.Setup();
            Week13Flow4Setup.Setup();
            Assert(frontendGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath),
                "Flow-4 setup changed the Frontend Scene GUID.");
            Assert(gameGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Flow-4 setup changed the Game Scene GUID.");
            Verify();
            Week13FrontendVerification.Verify();
            Debug.Log("Week 13 Flow-4 batch verification passed: setup twice, stable Scene GUIDs and complete Frontend regression.");
        }

        private static void ValidateFrontend(Scene scene)
        {
            FrontendRunLauncher[] launchers = FindAll<FrontendRunLauncher>(scene);
            Assert(launchers.Length == 1, "Frontend Scene requires exactly one FrontendRunLauncher.");
            Assert(launchers[0].HomeView != null && launchers[0].GameScenePath == Week13FrontendSetup.GameScenePath,
                "FrontendRunLauncher has invalid references.");
            Assert(SceneUtility.GetBuildIndexByScenePath(launchers[0].GameScenePath) >= 0,
                "The Flow-4 Game Scene must be enabled in Build Settings.");
        }

        private static void ValidateGame(Scene scene)
        {
            GameRunBootstrap[] bootstraps = FindAll<GameRunBootstrap>(scene);
            Assert(bootstraps.Length == 1, "Game Scene requires exactly one GameRunBootstrap.");
            GameRunBootstrap bootstrap = bootstraps[0];
            Assert(bootstrap.Session != null && bootstrap.ProgressClient != null &&
                bootstrap.LegacyCharacterSelection != null,
                "GameRunBootstrap has missing launch references.");
            Assert(bootstrap.Session.gameObject == bootstrap.gameObject,
                "GameRunBootstrap must run beside RunSession before its Awake.");
            Assert(FindAll<RunSession>(scene).Length == 1 && FindAll<RunProgress>(scene).Length == 1 &&
                FindAll<RoomGraphAssembler>(scene).Length == 1 && FindAll<PlayerInventory>(scene).Length == 1,
                "Game Scene requires one Run state, graph assembler and player inventory.");
        }

        private static T[] FindAll<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    [InitializeOnLoad]
    public static class Week13Flow4PlayVerification
    {
        private const string PendingKey = "Week13Flow4PlayVerification.Pending";
        private static bool hadProfile;
        private static string previousUserId;
        private static string previousNickname;
        private static double started;
        private static int phase;
        private static Flow4ApiClient apiClient;

        static Week13Flow4PlayVerification()
        {
            EditorApplication.update += Tick;
        }

        public static void RunBatch()
        {
            hadProfile = LocalProfile.IsRegistered;
            previousUserId = LocalProfile.UserId;
            previousNickname = LocalProfile.Nickname;
            LocalProfile.SaveProfile("flow4-local-user", "Flow4Tester");
            RunLaunchContext.Clear();
            apiClient = new Flow4ApiClient();
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
                if (EditorApplication.timeSinceStartup - started > 120) Finish(1, "Flow-4 Play Mode timeout.");
                return;
            }
            if (Time.frameCount < 8) return;

            try
            {
                if (phase == 0)
                {
                    FrontendTitleView frontend = Object.FindFirstObjectByType<FrontendTitleView>();
                    frontend.RequestStart();
                    FrontendCharacterSelectionView selection = frontend.HomeView.CharacterSelectionView;
                    frontend.HomeView.GameStartButton.onClick.Invoke();
                    if (selection.Cards.Count != 1 || selection.Cards[0].Character.CharacterId != "erpin")
                        throw new InvalidOperationException("Flow-4 could not select Erpin from Frontend data.");
                    selection.Cards[0].Button.onClick.Invoke();
                    phase = 1;
                    selection.ConfirmButton.onClick.Invoke();
                    return;
                }

                if (SceneManager.GetActiveScene().path != Week13FrontendSetup.GameScenePath) return;
                RunSession session = Object.FindFirstObjectByType<RunSession>();
                if (session == null || !session.HasStarted) return;
                GameRunBootstrap bootstrap = Object.FindFirstObjectByType<GameRunBootstrap>();
                RunProgress progress = Object.FindFirstObjectByType<RunProgress>();
                RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
                PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
                CharacterSelectionUI legacy = Object.FindFirstObjectByType<CharacterSelectionUI>(FindObjectsInactive.Include);

                if (bootstrap == null || !bootstrap.HasAppliedLaunch || bootstrap.AppliedUserId != "flow4-local-user" ||
                    bootstrap.AppliedCharacterId != "erpin" || session.UserId != "flow4-local-user" ||
                    session.CharacterId != "erpin" || !Guid.TryParse(session.ClientRunId, out _) ||
                    session.HasEnded || session.PendingRequest != null)
                    throw new InvalidOperationException("Flow-4 did not initialize one Run with the selected local identity.");
                if (progress == null || !progress.HasRunSeed || progress.GeneratedGraph == null ||
                    progress.CurrentFloor != 1 || progress.CurrentRoom != 1 || assembler == null ||
                    !assembler.HasAppliedRuntimeGraph || assembler.AppliedRunSeed != progress.RunSeed)
                    throw new InvalidOperationException("Flow-4 did not initialize seed, generated rooms and the first room.");
                if (inventory == null || inventory.AcquiredItems.Count != 0)
                    throw new InvalidOperationException("A new Flow-4 Run did not start with an empty inventory.");
                if (legacy == null || legacy.gameObject.activeInHierarchy || Time.timeScale != 1f)
                    throw new InvalidOperationException("The legacy Game Scene selector or paused time leaked into Flow-4.");
                if (apiClient == null || apiClient.GetUserCount != 1 || apiClient.PostRunCount != 0)
                    throw new InvalidOperationException("Flow-4 must load progression once and must not create a Backend Run record.");
                string clientRunId = session.ClientRunId;
                if (session.BeginRun("erpin") || session.ClientRunId != clientRunId || RunLaunchContext.HasPending)
                    throw new InvalidOperationException("Repeated Flow-4 initialization changed or duplicated the Run.");

                Finish(0, "Week 13 Flow-4 Play Mode verification passed: Frontend identity transfer, one Run/clientRunId, fresh seed, generated first room, empty inventory and no early POST /runs.");
            }
            catch (Exception exception)
            {
                Finish(1, exception.ToString());
            }
        }

        private static void Finish(int code, string message)
        {
            SessionState.SetBool(PendingKey, false);
            RunLaunchContext.Clear();
            if (hadProfile) LocalProfile.SaveProfile(previousUserId, previousNickname);
            else LocalProfile.ClearProfile();
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(code);
        }

        private sealed class Flow4ApiClient : IGameApiClient
        {
            public int GetUserCount { get; private set; }
            public int PostRunCount { get; private set; }

            public void GetUser(string nickname, Action<UserProfileResponse> onSuccess, Action<string> onError)
            {
                GetUserCount++;
                onSuccess?.Invoke(new UserProfileResponse
                {
                    success = true,
                    data = new UserProfileData
                    {
                        id = "flow4-local-user",
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

            public void PostUser(CreateUserRequest request, Action<CreateUserResponse> onSuccess, Action<string> onError)
            {
                throw new InvalidOperationException("Flow-4 must not register a user.");
            }

            public void PostRun(CreateRunRequest request, Action<CreateRunResponse> onSuccess, Action<string> onError)
            {
                PostRunCount++;
            }
        }
    }
}
