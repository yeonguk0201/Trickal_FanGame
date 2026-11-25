using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using TrickalFanGame.Meta;
using TrickalFanGame.Item;
using TrickalFanGame.Network;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week13Flow5Verification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify Flow-5 Run Result")]
        public static void Verify()
        {
            Scene frontend = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendRunResultView[] resultViews = FindAll<FrontendRunResultView>(frontend);
            Assert(resultViews.Length == 1, "Frontend Scene requires exactly one Run result view.");
            FrontendRunResultView result = resultViews[0];
            Assert(result.ResultPanel != null && result.StageTitle != null && result.StageBody != null &&
                   result.SaveStatus != null && result.ExperienceBar != null &&
                   result.RetryRunButton != null && result.HomeButton != null,
                "Run result view has missing references.");
            Assert(result.ResultPanel.transform.Find("SafeArea/SequenceStage/RetrySaveButton") == null,
                "Offline result UI must not retain the obsolete manual save retry button.");
            ImageAssertOpaque(result.ResultPanel);
            Assert(FindAll<RunSession>(frontend).Length == 0 && FindAll<RunProgress>(frontend).Length == 0 &&
                   FindAll<PlayerInventory>(frontend).Length == 0,
                "Frontend result state must not contain live Game Run state.");

            Scene game = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RunSession[] sessions = FindAll<RunSession>(game);
            GameRunResultTransition[] transitions = FindAll<GameRunResultTransition>(game);
            Assert(sessions.Length == 1 && transitions.Length == 1 && sessions[0].ResultTransition == transitions[0],
                "Game Scene requires one configured Run result transition.");
            Assert(transitions[0].FrontendScenePath == Week13FrontendSetup.ScenePath,
                "Run result transition points to the wrong Frontend Scene.");
            EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            VerifyDefeatingEnemyName();
            Debug.Log("Week 13 Flow-5 verification passed: full-screen result UI, transition and Scene isolation.");
        }

        private static void VerifyDefeatingEnemyName()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/QuickRangedFairy.prefab");
            Assert(prefab != null, "The projectile attribution verification needs the QuickRangedFairy prefab.");
            GameObject enemy = Object.Instantiate(prefab);
            DamageContext hit;
            try
            {
                hit = new DamageContext(enemy, DamageSourceType.EnemyProjectile, 1f);
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }

            Assert(PlayerDeathReason.Describe(hit.WithImpactDistance(3f)) == "산사모-순수",
                "A projectile must retain its enemy name after its owner is destroyed.");
        }

        public static void SetupAndVerifyBatch()
        {
            string frontendGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath);
            string gameGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Flow5Setup.Setup();
            Week13Flow5Setup.Setup();
            Assert(frontendGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath),
                "Flow-5 setup changed the Frontend Scene GUID.");
            Assert(gameGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Flow-5 setup changed the Game Scene GUID.");
            VerifyPendingRunStorage();
            Verify();
            Week13Flow4Verification.Verify();
            Debug.Log("Week 13 Flow-5 batch verification passed: setup twice, stable Scene GUIDs and Flow-4 regression.");
        }

        private static void VerifyPendingRunStorage()
        {
            string firstId = $"flow5-storage-{Guid.NewGuid()}";
            string secondId = $"flow5-storage-{Guid.NewGuid()}";
            int initialCount = LocalPendingRunStorage.Count;
            CreateRunRequest first = CreateStorageRequest(firstId, 1);
            CreateRunRequest second = CreateStorageRequest(secondId, 2);

            try
            {
                Assert(LocalPendingRunStorage.Save(first) && LocalPendingRunStorage.Save(second),
                    "Pending Run queue rejected valid requests.");
                Assert(LocalPendingRunStorage.Count == initialCount + 2 &&
                       LocalPendingRunStorage.Contains(firstId) && LocalPendingRunStorage.Contains(secondId),
                    "Pending Run queue did not preserve multiple requests.");
                Assert(LocalPendingRunStorage.Save(first) && LocalPendingRunStorage.Count == initialCount + 2,
                    "Pending Run queue duplicated an existing clientRunId.");

                CreateRunRequest conflict = CreateStorageRequest(firstId, 99);
                Assert(!LocalPendingRunStorage.Save(conflict) && LocalPendingRunStorage.Count == initialCount + 2,
                    "Pending Run queue replaced a stable clientRunId with conflicting data.");
                Assert(LocalPendingRunStorage.Remove(firstId) && !LocalPendingRunStorage.Contains(firstId) &&
                       LocalPendingRunStorage.Contains(secondId) && LocalPendingRunStorage.Count == initialCount + 1,
                    "Removing one confirmed Run deleted another queued request.");
            }
            finally
            {
                LocalPendingRunStorage.Remove(firstId);
                LocalPendingRunStorage.Remove(secondId);
            }
        }

        private static CreateRunRequest CreateStorageRequest(string clientRunId, int killCount)
        {
            return new CreateRunRequest
            {
                clientRunId = clientRunId,
                userId = "flow5-storage-user",
                characterId = "erpin",
                gameVersion = Application.version,
                startedAt = DateTime.UtcNow.AddMinutes(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                endedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                reachedFloor = 1,
                killCount = killCount,
                items = Array.Empty<RunItemDto>()
            };
        }

        private static void ImageAssertOpaque(GameObject panel)
        {
            UnityEngine.UI.Image image = panel.GetComponent<UnityEngine.UI.Image>();
            RectTransform rect = panel.GetComponent<RectTransform>();
            Assert(image != null && image.color.a >= 0.999f && image.raycastTarget,
                "Result background must be opaque and block clicks to hidden screens.");
            Assert(rect != null && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one,
                "Result screen must fill the 1920x1080 reference frame.");
        }

        private static T[] FindAll<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        internal static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    [InitializeOnLoad]
    public static class Week13Flow5PlayVerification
    {
        private const string PendingKey = "Week13Flow5PlayVerification.Pending";
        private const string SavedCacheKey = "Week13Flow5PlayVerification.SavedProgressCache";
        private static bool hadProfile;
        private static string previousUserId;
        private static string previousNickname;
        private static double started;
        private static double phaseStarted;
        private static int phase;
        private static string endedClientRunId;
        private static Flow5ApiClient apiClient;
        private static readonly List<string> VisibleStages = new();
        private static bool sawGroupedArtifactExperience;

        static Week13Flow5PlayVerification()
        {
            EditorApplication.update += Tick;
        }

        public static void RunBatch()
        {
            hadProfile = LocalProfile.IsRegistered;
            previousUserId = LocalProfile.UserId;
            previousNickname = LocalProfile.Nickname;
            LocalProfile.SaveProfile("flow5-local-user", "Flow5Tester");
            // Offline-1: growth is cached by the device key; keep the real profile's cache aside.
            string cacheKey = LocalProgression.CacheKey("erpin");
            SessionState.SetString(SavedCacheKey, PlayerPrefs.HasKey(cacheKey) ? "1" + PlayerPrefs.GetString(cacheKey) : "0");
            PlayerPrefs.DeleteKey(cacheKey);
            RunLaunchContext.Clear();
            RunResultContext.Clear();
            apiClient = new Flow5ApiClient();
            RunLaunchContext.SetVerificationApiClient(apiClient);
            phase = 0;
            started = phaseStarted = EditorApplication.timeSinceStartup;
            SessionState.SetBool(PendingKey, true);
            EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                if (EditorApplication.timeSinceStartup - started > 150) Finish(1, "Flow-5 Play Mode timeout.");
                return;
            }
            if (Time.frameCount < 8) return;
            if (RunSceneLoader.IsLoading) return;

            try
            {
                switch (phase)
                {
                    case 0: LaunchRun(); break;
                    case 1: EndRun(); break;
                    case 2: VerifyOfflineResultAndRecovery(); break;
                    case 3: PrepareConfirmedResult(); break;
                    case 4: FastForwardExperience(); break;
                    case 5: SkipToSummary(); break;
                    case 6: ReturnToSelection(); break;
                    case 7: PrepareHomeResult(); break;
                    case 8: ReturnHome(); break;
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
            NextPhase();
        }

        private static void EndRun()
        {
            if (SceneManager.GetActiveScene().path != Week13FrontendSetup.GameScenePath) return;
            RunSession session = Object.FindFirstObjectByType<RunSession>();
            if (session == null || !session.HasStarted) return;
            endedClientRunId = session.ClientRunId;
            Health player = session.GetComponent<GameRunResultTransition>() != null
                ? Object.FindObjectsByType<Health>(FindObjectsSortMode.None).First(health => health.GetComponent<PlayerInventory>() != null)
                : null;
            Week13Flow5Verification.Assert(player != null, "Flow-5 could not find the active player Health.");
            player.TakeDamage(player.MaxHealth + player.CurrentShield + 9999f);
            NextPhase();
        }

        private static void VerifyOfflineResultAndRecovery()
        {
            // UX-0: a death waits on the GAME OVER confirm before the result screen opens.
            if (SceneManager.GetActiveScene().path == Week13FrontendSetup.GameScenePath)
            {
                GameOverSequence gameOver = Object.FindFirstObjectByType<GameOverSequence>();
                if (gameOver != null && gameOver.IsActive && gameOver.CurrentPhase != GameOverSequence.Phase.Confirmed)
                {
                    // Balance-1: the save and the result scene both start while GAME OVER plays.
                    Week13Flow5Verification.Assert(apiClient.PostRunCount == 1 &&
                        apiClient.Requests[0].clientRunId == endedClientRunId &&
                        RunSceneLoader.PreloadedBuildIndex ==
                        SceneUtility.GetBuildIndexByScenePath(Week13FrontendSetup.ScenePath),
                        "GAME OVER must send the Run save and preload the result scene before the confirm.");
                    gameOver.SkipToConfirm();
                    gameOver.Confirm();
                }
                return;
            }
            if (SceneManager.GetActiveScene().path != Week13FrontendSetup.ScenePath) return;
            FrontendRunResultView view = Object.FindFirstObjectByType<FrontendRunResultView>();
            if (view == null || !view.IsShowing || !view.IsSummaryVisible) return;
            Week13Flow5Verification.Assert(apiClient.PostRunCount == 1 &&
                apiClient.Requests[0].clientRunId == endedClientRunId,
                "Initial result save did not preserve the ended Run clientRunId.");
            Week13Flow5Verification.Assert(LocalPendingRunStorage.Contains(endedClientRunId) &&
                view.SaveStatus.text == FrontendRunResultView.OfflineSaveStatus && view.StageTitle.text == "RUN 결과" &&
                view.SummaryView.RewardCount == 0,
                "Offline result did not immediately show local Run data and queue the request.");

            view.RetryRunButton.onClick.Invoke();
            FrontendTitleView frontend = Object.FindFirstObjectByType<FrontendTitleView>();
            frontend.RecoverPendingRuns(apiClient);
            Week13Flow5Verification.Assert(apiClient.PostRunCount == 2 &&
                apiClient.Requests[1].clientRunId == endedClientRunId &&
                !LocalPendingRunStorage.Contains(endedClientRunId),
                "Pending Run recovery did not confirm and remove the exact clientRunId.");
            string cachedProgress = PlayerPrefs.GetString(LocalProgression.CacheKey("erpin"), string.Empty);
            Week13Flow5Verification.Assert(cachedProgress.Contains("\"level\":3"),
                "Recovered Run progress was not stored in the local cache.");
            NextPhase();
        }

        private static void PrepareConfirmedResult()
        {
            CreateRunRequest request = CreateRequest(Guid.NewGuid().ToString());
            Week13Flow5Verification.Assert(RunResultContext.TryPrepare(request, StartingProgress(), apiClient),
                "Could not prepare the confirmed result sequence verification.");
            apiClient.FailNextRun = false;
            SceneManager.LoadScene(SceneUtility.GetBuildIndexByScenePath(Week13FrontendSetup.ScenePath));
            NextPhase();
        }

        private static void FastForwardExperience()
        {
            FrontendRunResultView view = Object.FindFirstObjectByType<FrontendRunResultView>();
            if (view == null || !view.IsAnimatingExperience || string.IsNullOrEmpty(view.ExperienceSource.text)) return;
            Week13Flow5Verification.Assert(view.StageTitle.text == "RUN 기록" &&
                view.ExperienceBar.gameObject.activeInHierarchy && view.ExperienceSource.text.Contains("층 도달"),
                "The first experience source and gauge must appear within the Run record stage.");
            view.RequestAdvance();
            NextPhase();
        }

        private static void SkipToSummary()
        {
            if (EditorApplication.timeSinceStartup - phaseStarted < 0.05d) return;
            FrontendRunResultView view = Object.FindFirstObjectByType<FrontendRunResultView>();
            view.RequestAdvance();
            NextPhase();
        }

        private static void ReturnToSelection()
        {
            FrontendRunResultView view = Object.FindFirstObjectByType<FrontendRunResultView>();
            if (view == null || !view.IsSummaryVisible) return;
            Week13Flow5Verification.Assert(view.ResultProgress.level == 3 && view.SummaryView.ExperienceBar.gameObject.activeInHierarchy &&
                view.SummaryView.RewardCount == 1 && view.SaveStatus.text == "저장 완료",
                "Final summary lost confirmed progression, artifacts or save state.");
            view.RetryRunButton.onClick.Invoke();
            FrontendTitleView frontend = Object.FindFirstObjectByType<FrontendTitleView>();
            Week13Flow5Verification.Assert(!RunResultContext.HasPending && Time.timeScale == 1f &&
                frontend.HomeView.CurrentDestination == FrontendDestination.CharacterSelection &&
                Object.FindFirstObjectByType<RunSession>() == null &&
                Object.FindFirstObjectByType<RunProgress>() == null &&
                Object.FindFirstObjectByType<PlayerInventory>() == null,
                "Retry return leaked Run state or failed to restore input/time state.");
            NextPhase();
        }

        private static void PrepareHomeResult()
        {
            CreateRunRequest request = CreateRequest(Guid.NewGuid().ToString());
            request.isCleared = true;
            request.items = new[]
            {
                request.items[0],
                new RunItemDto { itemId = "item-01", floor = 2, order = 2, acquiredAt = request.endedAt }
            };
            var rarities = new Dictionary<string, ItemRarity> { ["item-01"] = ItemRarity.Rare };
            VisibleStages.Clear();
            sawGroupedArtifactExperience = false;
            Week13Flow5Verification.Assert(RunResultContext.TryPrepare(request, StartingProgress(), apiClient, rarities),
                "Could not prepare the second result return verification.");
            apiClient.FailNextRun = false;
            SceneManager.LoadScene(SceneUtility.GetBuildIndexByScenePath(Week13FrontendSetup.ScenePath));
            NextPhase();
        }

        private static void ReturnHome()
        {
            FrontendRunResultView view = Object.FindFirstObjectByType<FrontendRunResultView>();
            if (view == null || !view.IsShowing) return;
            string stage = view.StageTitle.text;
            if (VisibleStages.Count == 0 || VisibleStages[VisibleStages.Count - 1] != stage)
                VisibleStages.Add(stage);

            if (!view.IsSummaryVisible)
            {
                if (stage == "RUN 기록" && view.ExperienceSource.text.Contains("희귀 아티팩트 2개") &&
                    view.ExperienceSource.text.Contains("+60"))
                    sawGroupedArtifactExperience = true;
                return;
            }

            string[] resultStages = { "RUN 기록", "LEVEL UP!", "스킬 포인트", "획득 아티팩트", "RUN 결과" };
            Week13Flow5Verification.Assert(sawGroupedArtifactExperience &&
                VisibleStages.Where(title => resultStages.Contains(title)).SequenceEqual(resultStages) &&
                view.SummaryView.RewardCount == 1 && view.SummaryView.ExperienceValue.text == "50 / 600",
                "Full clear result did not show grouped rarity XP, the ordered stages, or confirmed final data.");
            view.HomeButton.onClick.Invoke();
            FrontendTitleView frontend = Object.FindFirstObjectByType<FrontendTitleView>();
            Week13Flow5Verification.Assert(!RunResultContext.HasPending && Time.timeScale == 1f &&
                frontend.HomeView.HomePanel.activeInHierarchy && frontend.HomeView.CurrentDestination == null &&
                EventSystem.current.currentSelectedGameObject == frontend.HomeView.GameStartButton.gameObject,
                "Home return did not restore clean Frontend state and first focus.");
            Finish(0, "Week 13 Flow-5 Play Mode verification passed: offline queue, exact-ID recovery, confirmed progress cache, result sequence and clean returns.");
        }

        private static CreateRunRequest CreateRequest(string clientRunId)
        {
            return new CreateRunRequest
            {
                clientRunId = clientRunId,
                userId = "flow5-local-user",
                characterId = "erpin",
                gameVersion = Application.version,
                startedAt = DateTime.UtcNow.AddMinutes(-2).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                endedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                playTime = 125,
                reachedFloor = 2,
                isCleared = false,
                killCount = 7,
                deathReason = "ENEMY_PROJECTILE",
                items = new[]
                {
                    new RunItemDto { itemId = "item-01", floor = 1, order = 1, acquiredAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
                }
            };
        }

        private static CharacterProgressDto StartingProgress()
        {
            return new CharacterProgressDto
            {
                characterId = "erpin", level = 1, experience = 350,
                experienceToNextLevel = 400, skillPoints = 0,
                lowGradeSkillLevel = 1, highGradeSkillLevel = 1
            };
        }

        private static void NextPhase()
        {
            phase++;
            phaseStarted = EditorApplication.timeSinceStartup;
        }

        private static void Finish(int code, string message)
        {
            SessionState.SetBool(PendingKey, false);
            RunLaunchContext.Clear();
            RunResultContext.Clear();
            if (!string.IsNullOrWhiteSpace(endedClientRunId)) LocalPendingRunStorage.Remove(endedClientRunId);
            Time.timeScale = 1f;
            PlayerPrefs.DeleteKey("TrickalFanGame.CharacterProgress.Flow5Tester.erpin");
            string savedCache = SessionState.GetString(SavedCacheKey, "0");
            if (savedCache.StartsWith("1")) PlayerPrefs.SetString(LocalProgression.CacheKey("erpin"), savedCache.Substring(1));
            else PlayerPrefs.DeleteKey(LocalProgression.CacheKey("erpin"));
            PlayerPrefs.Save();
            if (hadProfile) LocalProfile.SaveProfile(previousUserId, previousNickname);
            else LocalProfile.ClearProfile();
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(code);
        }

        private sealed class Flow5ApiClient : IGameApiClient
        {
            public readonly System.Collections.Generic.List<CreateRunRequest> Requests = new();
            public int PostRunCount => Requests.Count;
            public bool FailNextRun { get; set; } = true;

            public void GetUser(string nickname, Action<UserProfileResponse> onSuccess, Action<string> onError)
            {
                onSuccess?.Invoke(new UserProfileResponse
                {
                    success = true,
                    data = new UserProfileData
                    {
                        id = "flow5-local-user", nickname = nickname,
                        characterProgress = new[] { StartingProgress() }
                    }
                });
            }

            public void PostRun(CreateRunRequest request, Action<CreateRunResponse> onSuccess, Action<string> onError)
            {
                Requests.Add(request);
                if (FailNextRun)
                {
                    FailNextRun = false;
                    onError?.Invoke("NETWORK_ERROR: Flow-5 retry verification");
                    return;
                }
                onSuccess?.Invoke(new CreateRunResponse
                {
                    success = true,
                    data = new RunData
                    {
                        runId = "flow5-run", experienceGained = 600,
                        progress = new CharacterProgressDto
                        {
                            characterId = "erpin", level = 3, experience = 50,
                            experienceToNextLevel = 600, skillPoints = 2,
                            lowGradeSkillLevel = 1, highGradeSkillLevel = 1
                        }
                    }
                });
            }

            public void PostUser(CreateUserRequest request, Action<CreateUserResponse> onSuccess, Action<string> onError)
            {
                throw new InvalidOperationException("Flow-5 must not register a user.");
            }
        }
    }
}
