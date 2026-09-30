using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Meta;
using TrickalFanGame.Network;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class PhaseHMetaProgressionVerification
    {
        [MenuItem("Trickal Fan Game/Verify Phase H-5 Meta Progression")]
        public static void Verify()
        {
            ValidateProgressionLoadingAndBoundaries();
            ValidateDeathSubmissionRetryAndResult();
            ValidateClearAndConflictPaths();
            Debug.Log(
                "Phase H-5 verification passed: online, cached, and Lv.1 fallback progression overwrite skill " +
                "snapshots; Lv.1/Lv.10 effects match the contract; clear/death requests preserve one clientRunId " +
                "and ordered Run data; duplicate endings do not post twice; retries reuse the exact request; and " +
                "success, network failure, and idempotency conflict states remain observable.");
        }

        private static void ValidateProgressionLoadingAndBoundaries()
        {
            RuntimeHarness harness = CreateHarness();
            string cacheKey = harness.CacheKey("erpin");
            string defaultCacheKey = harness.CacheKey("offline-new");
            PlayerPrefs.DeleteKey(cacheKey);
            PlayerPrefs.DeleteKey(defaultCacheKey);
            try
            {
                harness.Api.Profile = CreateProfile("erpin", 10, 10);
                Assert(harness.ProgressClient.LoadAndApply("erpin", null),
                    "An online progression lookup must start.");
                Assert(harness.Skill.ProgressionLevel == 10 &&
                       harness.Skill.ProgressionProjectileBonus == 1 &&
                       Approximately(harness.Skill.ProgressionDamageMultiplier, 1.9f),
                    "Lower-grade Lv.10 must apply 190% level damage and one base projectile bonus.");
                Assert(harness.Ultimate.ProgressionLevel == 10 &&
                       Approximately(harness.Ultimate.ProgressionDamageMultiplier, 1.9f) &&
                       Approximately(harness.Ultimate.Cooldown, 25.5f),
                    "High-grade Lv.10 must apply 190% level damage and the 85% cooldown multiplier.");

                harness.Api.Profile = CreateProfile("erpin", 1, 1);
                Assert(harness.ProgressClient.LoadAndApply("erpin", null),
                    "A new Run must be able to refresh the same character progression.");
                Assert(harness.Skill.ProgressionLevel == 1 &&
                       harness.Skill.ProgressionProjectileBonus == 0 &&
                       Approximately(harness.Skill.ProgressionDamageMultiplier, 1f) &&
                       harness.Ultimate.ProgressionLevel == 1 &&
                       Approximately(harness.Ultimate.Cooldown, 30f),
                    "Applying Lv.1 after Lv.10 must overwrite all prior meta-skill state.");

                harness.Api.Profile = null;
                harness.Api.GetError = "network unavailable";
                Assert(harness.ProgressClient.LoadAndApply("erpin", null) &&
                       harness.ProgressClient.IsOfflineFallback &&
                       harness.ProgressClient.AppliedProgress.lowGradeSkillLevel == 1,
                    "A lookup failure must use the last successful per-character cache.");

                harness.ProgressClient.ApplyFallback("offline-new", "network unavailable");
                Assert(harness.ProgressClient.IsOfflineFallback &&
                       harness.ProgressClient.AppliedProgress.level == 1 &&
                       harness.Skill.ProgressionLevel == 1 &&
                       harness.Ultimate.ProgressionLevel == 1 &&
                       harness.ProgressClient.StatusMessage.Contains("Offline default progression"),
                    "A character without a cache must visibly fall back to the Lv.1 contract.");
            }
            finally
            {
                PlayerPrefs.DeleteKey(cacheKey);
                PlayerPrefs.DeleteKey(defaultCacheKey);
                UnityEngine.Object.DestroyImmediate(harness.Root);
            }
        }

        private static void ValidateDeathSubmissionRetryAndResult()
        {
            RuntimeHarness harness = CreateHarness();
            string cacheKey = harness.CacheKey("erpin");
            PlayerPrefs.DeleteKey(cacheKey);
            try
            {
                harness.ProgressClient.ApplyFallback("erpin", "verification default");
                harness.Api.PostOutcomes.Enqueue(PostOutcome.NetworkFailure);
                harness.Api.PostOutcomes.Enqueue(PostOutcome.Success);
                Assert(harness.Session.BeginRun("erpin"), "The verification Run must start.");
                string clientRunId = harness.Session.ClientRunId;
                Assert(Guid.TryParse(clientRunId, out _),
                    "RunSession must create a valid clientRunId exactly once at Run start.");

                harness.Progress.RecordRoomEntry(2, 3);
                harness.Progress.RecordKill();
                harness.Progress.RecordKill();
                AddAcquiredItem(harness.Inventory, "item-01", 1, 1);
                AddAcquiredItem(harness.Inventory, "item-02", 2, 2);

                InvokeEndRun(harness.Session, false, "ENEMY");
                Assert(harness.Api.PostedRuns.Count == 1 && harness.Progress.IsProgressionStopped,
                    "A death must stop progression and begin exactly one save request.");
                CreateRunRequest firstRequest = harness.Api.PostedRuns[0];
                Assert(firstRequest.clientRunId == clientRunId &&
                       firstRequest.characterId == "erpin" &&
                       firstRequest.reachedFloor == 2 &&
                       firstRequest.killCount == 2 &&
                       !firstRequest.isCleared && firstRequest.deathReason == "ENEMY" &&
                       firstRequest.items.Length == 2 &&
                       firstRequest.items[0].itemId == "item-01" && firstRequest.items[0].order == 1 &&
                       firstRequest.items[1].itemId == "item-02" && firstRequest.items[1].order == 2,
                    "The death DTO must contain the actual character, ordered artifacts, floor, kills, and reason.");
                Assert(harness.Session.CanRetrySave,
                    "A network failure must leave the immutable request available for retry.");

                InvokeEndRun(harness.Session, false, "UNKNOWN");
                Assert(harness.Api.PostedRuns.Count == 1,
                    "A duplicate Run ending must not start another request.");
                Assert(harness.Session.RetrySave() && harness.Api.PostedRuns.Count == 2 &&
                       ReferenceEquals(firstRequest, harness.Api.PostedRuns[1]) &&
                       harness.Api.PostedRuns[1].clientRunId == clientRunId,
                    "A retry must submit the exact same request and clientRunId.");
                Assert(harness.Session.LastRunId == "run-success" &&
                       harness.ProgressClient.LastExperienceGained == 840 &&
                       harness.ProgressClient.LastResultProgress.level == 2 &&
                       harness.ProgressClient.StatusMessage.Contains("+840 XP"),
                    "A successful response must expose XP, level, next XP, points, and both skill levels.");
                Assert(!harness.Session.RetrySave(),
                    "A completed save must not be retransmitted again from the result screen.");
            }
            finally
            {
                PlayerPrefs.DeleteKey(cacheKey);
                UnityEngine.Object.DestroyImmediate(harness.Root);
            }
        }

        private static void ValidateClearAndConflictPaths()
        {
            RuntimeHarness clearHarness = CreateHarness();
            RuntimeHarness conflictHarness = CreateHarness();
            try
            {
                clearHarness.ProgressClient.ApplyFallback("erpin", "verification default");
                clearHarness.Api.PostOutcomes.Enqueue(PostOutcome.Success);
                Assert(clearHarness.Session.BeginRun("erpin"), "The clear verification Run must start.");
                clearHarness.Progress.RecordRoomEntry(3, 8);
                InvokeEndRun(clearHarness.Session, true, null);
                Assert(clearHarness.Api.PostedRuns.Count == 1 &&
                       clearHarness.Api.PostedRuns[0].isCleared &&
                       clearHarness.Api.PostedRuns[0].deathReason == null &&
                       clearHarness.Api.PostedRuns[0].reachedFloor == 3,
                    "A clear must send one request with no deathReason and the actual reached floor.");

                conflictHarness.ProgressClient.ApplyFallback("erpin", "verification default");
                conflictHarness.Api.PostOutcomes.Enqueue(PostOutcome.Conflict);
                Assert(conflictHarness.Session.BeginRun("erpin"),
                    "The conflict verification Run must start.");
                InvokeEndRun(conflictHarness.Session, false, "BOSS");
                Assert(conflictHarness.Session.CanRetrySave &&
                       conflictHarness.Session.StatusMessage.Contains("RUN_IDEMPOTENCY_CONFLICT") &&
                       conflictHarness.Api.PostedRuns[0] == conflictHarness.Session.PendingRequest,
                    "An idempotency conflict must be visible without replacing the original request.");
            }
            finally
            {
                PlayerPrefs.DeleteKey(clearHarness.CacheKey("erpin"));
                PlayerPrefs.DeleteKey(conflictHarness.CacheKey("erpin"));
                UnityEngine.Object.DestroyImmediate(clearHarness.Root);
                UnityEngine.Object.DestroyImmediate(conflictHarness.Root);
            }
        }

        private static RuntimeHarness CreateHarness()
        {
            GameObject root = new($"Phase H-5 Verification {Guid.NewGuid():N}");
            GameObject player = new("Player");
            player.transform.SetParent(root.transform);
            Health health = player.AddComponent<Health>();
            player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerSP>();
            player.AddComponent<PlayerCombatEvents>();
            PlayerSkill skill = player.AddComponent<PlayerSkill>();
            PlayerUltimate ultimate = player.AddComponent<PlayerUltimate>();
            PlayerInventory inventory = player.AddComponent<PlayerInventory>();

            RunProgress progress = root.AddComponent<RunProgress>();
            PlayerProgressClient progressClient = root.AddComponent<PlayerProgressClient>();
            string nickname = $"phase-h5-{Guid.NewGuid():N}";
            progressClient.Configure(nickname, skill, ultimate);
            FakeApiClient api = new();
            progressClient.SetApiClient(api);

            RunSession session = root.AddComponent<RunSession>();
            session.Configure(health, progress, null);
            session.ConfigureInventory(inventory);
            session.ConfigureMetaProgression(progressClient);
            session.SetApiClient(api);

            return new RuntimeHarness(
                root,
                nickname,
                skill,
                ultimate,
                inventory,
                progress,
                progressClient,
                session,
                api);
        }

        private static UserProfileResponse CreateProfile(
            string characterId,
            int lowerGradeLevel,
            int highGradeLevel)
        {
            return new UserProfileResponse
            {
                success = true,
                data = new UserProfileData
                {
                    id = "00000000-0000-4000-8000-000000000001",
                    nickname = "test-player",
                    characterProgress = new[]
                    {
                        new CharacterProgressDto
                        {
                            characterId = characterId,
                            level = 10,
                            experience = 250,
                            experienceToNextLevel = 1300,
                            skillPoints = 3,
                            lowGradeSkillLevel = lowerGradeLevel,
                            highGradeSkillLevel = highGradeLevel
                        }
                    }
                }
            };
        }

        private static CreateRunResponse CreateSuccessResponse()
        {
            return new CreateRunResponse
            {
                success = true,
                data = new RunData
                {
                    runId = "run-success",
                    experienceGained = 840,
                    progress = new CharacterProgressDto
                    {
                        characterId = "erpin",
                        level = 2,
                        experience = 440,
                        experienceToNextLevel = 500,
                        skillPoints = 1,
                        lowGradeSkillLevel = 1,
                        highGradeSkillLevel = 1
                    }
                }
            };
        }

        private static void AddAcquiredItem(
            PlayerInventory inventory,
            string itemId,
            int floor,
            int order)
        {
            FieldInfo field = typeof(PlayerInventory).GetField(
                "acquiredItems",
                BindingFlags.Instance | BindingFlags.NonPublic);
            List<AcquiredItem> items = field?.GetValue(inventory) as List<AcquiredItem>;
            Assert(items != null,
                "PlayerInventory acquired item storage must remain available to the verification.");
            items.Add(new AcquiredItem(itemId, floor, order, Time.realtimeSinceStartup));
        }

        private static void InvokeEndRun(RunSession session, bool isCleared, string deathReason)
        {
            MethodInfo method = typeof(RunSession).GetMethod(
                "EndRun",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, "RunSession.EndRun must remain available to its runtime event handlers.");
            method.Invoke(session, new object[] { isCleared, deathReason });
        }

        private static bool Approximately(float left, float right)
        {
            return Mathf.Abs(left - right) <= 0.0001f;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private enum PostOutcome
        {
            Success,
            NetworkFailure,
            Conflict,
        }

        private sealed class FakeApiClient : IGameApiClient
        {
            public readonly List<CreateRunRequest> PostedRuns = new();
            public readonly Queue<PostOutcome> PostOutcomes = new();
            public UserProfileResponse Profile { get; set; }
            public string GetError { get; set; }

            public void GetUser(
                string nickname,
                Action<UserProfileResponse> onSuccess,
                Action<string> onError)
            {
                if (!string.IsNullOrEmpty(GetError))
                {
                    onError?.Invoke(GetError);
                    return;
                }

                onSuccess?.Invoke(Profile);
            }

            public void PostRun(
                CreateRunRequest request,
                Action<CreateRunResponse> onSuccess,
                Action<string> onError)
            {
                PostedRuns.Add(request);
                PostOutcome outcome = PostOutcomes.Count > 0
                    ? PostOutcomes.Dequeue()
                    : PostOutcome.Success;
                switch (outcome)
                {
                    case PostOutcome.Success:
                        onSuccess?.Invoke(CreateSuccessResponse());
                        break;
                    case PostOutcome.NetworkFailure:
                        onError?.Invoke("HTTP 0: network unavailable");
                        break;
                    case PostOutcome.Conflict:
                        onError?.Invoke("RUN_IDEMPOTENCY_CONFLICT: changed payload");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public void PostUser(
                CreateUserRequest request,
                Action<CreateUserResponse> onSuccess,
                Action<string> onError)
            {
                // Not used in Phase H verification
                onError?.Invoke("PostUser not implemented in FakeApiClient");
            }
        }

        private sealed class RuntimeHarness
        {
            public RuntimeHarness(
                GameObject root,
                string nickname,
                PlayerSkill skill,
                PlayerUltimate ultimate,
                PlayerInventory inventory,
                RunProgress progress,
                PlayerProgressClient progressClient,
                RunSession session,
                FakeApiClient api)
            {
                Root = root;
                Nickname = nickname;
                Skill = skill;
                Ultimate = ultimate;
                Inventory = inventory;
                Progress = progress;
                ProgressClient = progressClient;
                Session = session;
                Api = api;
            }

            public GameObject Root { get; }
            public string Nickname { get; }
            public PlayerSkill Skill { get; }
            public PlayerUltimate Ultimate { get; }
            public PlayerInventory Inventory { get; }
            public RunProgress Progress { get; }
            public PlayerProgressClient ProgressClient { get; }
            public RunSession Session { get; }
            public FakeApiClient Api { get; }

            public string CacheKey(string characterId)
            {
                return $"TrickalFanGame.CharacterProgress.{Nickname}.{characterId}";
            }
        }
    }
}
