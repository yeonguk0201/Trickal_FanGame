using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TrickalFanGame.Data;
using TrickalFanGame.Item;
using TrickalFanGame.Meta;
using TrickalFanGame.Network;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Offline-1 (docs/30 §3.1 S4): growth without the server. Checks that the game's experience, level and skill
    // point rules match the Backend (the same cases as backend/src/domain/meta-progression.spec.ts), that offline
    // Runs and skill upgrades raise the shown progress and wait in one queue keyed by the device key, that
    // registration moves both, and that sending them later goes oldest first, drops an upgrade the server refuses,
    // stops on a network failure, and ends with the server's values.
    public static class BetaOffline1Verification
    {
        private const string CharacterId = "erpin";
        private const string ServerUserId = "41000000-0000-4000-8000-000000000001";
        private const string QueueKey = "PendingRunQueue.v1";
        private const string ExperienceKey = "PendingRunExperience.v1";
        private static readonly string[] ProfileKeys = { "UserId", "Nickname", "NeedsNewNickname" };

        [MenuItem("Trickal Fan Game/Beta/Verify Offline-1 Growth")]
        public static void Verify()
        {
            string cacheKey = LocalProgression.CacheKey(CharacterId);
            var keys = new List<string>(ProfileKeys) { QueueKey, ExperienceKey, cacheKey };
            Dictionary<string, string> saved = new();
            foreach (string key in keys) saved[key] = PlayerPrefs.HasKey(key) ? ReadRaw(key) : null;
            try
            {
                VerifyRules();
                VerifyOfflineGrowthAndSync();
            }
            finally
            {
                foreach (string key in keys) RestoreRaw(key, saved[key]);
                PlayerPrefs.Save();
            }

            Debug.Log("Beta Offline-1 verification passed: experience, level and skill point rules match the Backend " +
                      "contract; offline Runs and skill upgrades raise the shown progress and wait in one queue; " +
                      "registration moves both to the server user; sending goes oldest first, drops an upgrade the " +
                      "server refuses, stops on a network failure, and leaves the server's values as the cache " +
                      "keyed by the device key.");
        }

        private static void VerifyRules()
        {
            // contract-drift-check: backend/src/contracts/meta-progression.ts is the reference.
            string backendPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "backend", "src",
                "contracts", "meta-progression.ts"));
            Assert(File.Exists(backendPath), $"The Backend progression contract is missing at {backendPath}.");
            string source = File.ReadAllText(backendPath, Encoding.UTF8);
            AssertBackendConstant(source, "CHARACTER_MAX_LEVEL", MetaProgressionRules.CharacterMaxLevel);
            AssertBackendConstant(source, "SKILL_MAX_LEVEL", MetaProgressionRules.SkillMaxLevel);
            AssertBackendConstant(source, "REACHED_FLOOR_EXPERIENCE", MetaProgressionRules.ReachedFloorExperience);
            AssertBackendConstant(source, "KILL_EXPERIENCE", MetaProgressionRules.KillExperience);

            Match curve = Regex.Match(source, @"EXPERIENCE_TO_NEXT_LEVEL\s*=\s*\[(?<values>[^\]]*)\]");
            Assert(curve.Success, "EXPERIENCE_TO_NEXT_LEVEL was not found in the Backend contract.");
            string backendCurve = Regex.Replace(curve.Groups["values"].Value, @"\s", string.Empty).TrimEnd(',');
            string unityCurve = string.Join(",", MetaProgressionRules.ExperienceToNextLevel);
            Assert(backendCurve == unityCurve,
                $"Unity experience curve '{unityCurve}' must match the Backend '{backendCurve}'.");
            Assert(MetaProgressionRules.ExperienceToNextLevel.Length == MetaProgressionRules.CharacterMaxLevel - 1,
                "The experience curve must cover every level below the maximum.");

            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
            {
                string name = rarity.ToString().ToUpperInvariant();
                Assert(Regex.IsMatch(source, $@"\b{name}:\s*{MetaProgressionRules.RarityExperience(rarity)},"),
                    $"ITEM_RARITY_EXPERIENCE.{name} must equal {MetaProgressionRules.RarityExperience(rarity)} " +
                    "in the Backend contract.");
            }

            // item-01 COMMON, item-02 RARE, item-04 UNCOMMON, item-15 EPIC (backend item catalog).
            Assert(MetaProgressionRules.CalculateRunExperience(2, 30,
                       new[] { ItemRarity.Common, ItemRarity.Common, ItemRarity.Rare }) == 400,
                "Floor, kill and rarity experience must match the Backend (400).");
            Assert(MetaProgressionRules.CalculateRunExperience(0, 0, new[]
                {
                    ItemRarity.Common, ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic
                }) == 120, "Every rarity tier must match the Backend (120).");

            AssertProgress(MetaProgressionRules.ApplyRunExperience(Progress(1, 398, 0), 1), 1, 399, 400, 0,
                "below the level boundary");
            AssertProgress(MetaProgressionRules.ApplyRunExperience(Progress(1, 399, 0), 26), 2, 25, 500, 1,
                "carry at the boundary");
            AssertProgress(MetaProgressionRules.ApplyRunExperience(Progress(1, 350, 2), 1100), 3, 550, 600, 4,
                "repeated level ups");
            CharacterProgressDto max = MetaProgressionRules.ApplyRunExperience(Progress(18, 2050, 7, 4, 6), 500);
            AssertProgress(max, 19, 0, 0, 8, "reaching Lv.19");
            Assert(max.lowGradeSkillLevel == 4 && max.highGradeSkillLevel == 6,
                "Reaching the maximum level must keep skill levels.");
            AssertProgress(MetaProgressionRules.ApplyRunExperience(Progress(19, 0, 18), 10000), 19, 0, 0, 18,
                "staying at Lv.19");

            CharacterProgressDto skills = Progress(1, 0, 1);
            Assert(!MetaProgressionRules.TryApplySkillUpgrade(skills, SkillType.LowGrade, 3) &&
                   MetaProgressionRules.TryApplySkillUpgrade(skills, SkillType.LowGrade, 2) &&
                   skills.lowGradeSkillLevel == 2 && skills.skillPoints == 0 &&
                   !MetaProgressionRules.TryApplySkillUpgrade(skills, SkillType.HighGrade, 2),
                "A skill upgrade must target the next level and spend one point, like the Backend.");
            CharacterProgressDto capped = Progress(5, 0, 3, 10, 1);
            Assert(!MetaProgressionRules.TryApplySkillUpgrade(capped, SkillType.LowGrade, 11),
                "A skill at Lv.10 must not upgrade.");
        }

        private static void VerifyOfflineGrowthAndSync()
        {
            LocalProfile.ClearProfile();
            LocalPendingRunStorage.Clear();
            PlayerPrefs.DeleteKey(LocalProgression.CacheKey(CharacterId));
            string deviceKey = LocalProfile.ClientProfileId;
            LocalProfile.SaveOfflineProfile("Offline1Tester");
            Assert(LocalProgression.CacheKey(CharacterId).Contains(deviceKey),
                "The local growth cache must be keyed by the device key (docs/30 §3.1 S3).");

            // Offline Run A: 450 XP → Lv.2 (50/500), 1 point.
            CreateRunRequest runA = Run("51000000-0000-4000-8000-000000000001", 2, 50);
            QueueRun(runA, 450);
            AssertProgress(LocalProgression.Current(CharacterId), 2, 50, 500, 1, "after offline Run A");

            CharacterProgressDto upgraded = LocalProgression.QueueSkillUpgrade(CharacterId, SkillType.LowGrade);
            Assert(upgraded != null && upgraded.lowGradeSkillLevel == 2 && upgraded.skillPoints == 0,
                "An offline upgrade must spend the point and raise the skill.");
            Assert(LocalProgression.QueueSkillUpgrade(CharacterId, SkillType.HighGrade) == null,
                "An offline upgrade without a point must be refused locally.");

            // A nickname change keeps the growth (device key).
            LocalProfile.SaveOfflineProfile("Offline1Renamed");
            CharacterProgressDto renamed = LocalProgression.Current(CharacterId);
            Assert(renamed.lowGradeSkillLevel == 2 && renamed.level == 2,
                "Renaming the offline profile must keep its growth.");

            // Offline Run B: 500 XP → Lv.3 (50/600), 1 point; then the high-grade skill.
            CreateRunRequest runB = Run("51000000-0000-4000-8000-000000000002", 3, 40);
            QueueRun(runB, 500);
            Assert(RunSubmission.NeedsOrderedDelivery(runB),
                "A Run behind a queued skill upgrade must be sent through the queue.");
            Assert(LocalProgression.QueueSkillUpgrade(CharacterId, SkillType.HighGrade) != null,
                "The point from Run B must allow the high-grade upgrade.");
            CharacterProgressDto offline = LocalProgression.Current(CharacterId);
            AssertProgress(offline, 3, 50, 600, 0, "after two offline Runs and two upgrades");
            Assert(offline.lowGradeSkillLevel == 2 && offline.highGradeSkillLevel == 2,
                "Both offline upgrades must be applied.");

            // Not registered: nothing is sent, the upgrade waits for the server account.
            var unregistered = new FakeServer();
            PendingSyncOutcome? outcome = null;
            PendingSync.Flush(unregistered, unregistered, result => outcome = result);
            Assert(outcome == PendingSyncOutcome.Stopped && unregistered.Calls.Count == 0,
                "Nothing may be sent before the server registers the profile.");

            // Registration moves Runs and upgrades to the server user ID.
            LocalProfile.SaveProfile(ServerUserId, "Offline1Renamed");
            ProfileRegistration.MovePendingRunsToServerAccount();
            List<PendingSyncItem> queue = LocalPendingRunStorage.LoadQueue(ServerUserId);
            Assert(queue.Count == 4 && LocalPendingRunStorage.LoadQueue(deviceKey).Count == 0 &&
                   queue[0].IsRun && !queue[1].IsRun && queue[2].IsRun && !queue[3].IsRun,
                "Registration must move every queued Run and upgrade, keeping their order.");

            // A network failure in the middle stops and keeps the rest.
            var flaky = new FakeServer { FailRunId = runB.clientRunId };
            PendingSync.Flush(flaky, flaky, result => outcome = result);
            Assert(outcome == PendingSyncOutcome.Stopped &&
                   string.Join(",", flaky.Calls) == $"run:{runA.clientRunId},skill:LowGrade:2,run:{runB.clientRunId}",
                "A network failure must stop the flush after the writes before it.");
            Assert(LocalPendingRunStorage.LoadQueue(ServerUserId).Count == 2 &&
                   LocalPendingRunStorage.Contains(runB.clientRunId),
                "Writes after the failure must stay queued.");
            Assert(!LocalPendingRunStorage.TryGetRunExperience(runA.clientRunId, out _) &&
                   LocalPendingRunStorage.TryGetRunExperience(runB.clientRunId, out _),
                "A confirmed Run's local experience must be forgotten; a queued one kept.");
            AssertProgress(LocalProgression.Current(CharacterId), 3, 50, 600, 0,
                "after a partial send (server values plus the rest)");

            // The server comes back, but gives Run B less experience than this PC: no point for the high upgrade.
            var server = new FakeServer { State = flaky.State, ExperienceOverride = { [runB.clientRunId] = 100 } };
            PendingSync.Flush(server, server, result => outcome = result);
            Assert(outcome == PendingSyncOutcome.Completed &&
                   string.Join(",", server.Calls) == $"run:{runB.clientRunId},skill:HighGrade:2",
                "The rest must be sent oldest first.");
            Assert(LocalPendingRunStorage.LoadQueue(ServerUserId).Count == 0,
                "A refused upgrade must be dropped so the queue empties.");
            CharacterProgressDto final = LocalProgression.Current(CharacterId);
            AssertProgress(final, 2, 150, 500, 0, "following the server after a refused upgrade");
            Assert(final.lowGradeSkillLevel == 2 && final.highGradeSkillLevel == 1 &&
                   JsonUtility.ToJson(final) == JsonUtility.ToJson(MetaProgressionRules.Normalize(CharacterId,
                       server.State)), "The local progress must equal the server value after sending.");

            // A repeated upgrade send is applied once (target level), like a lost response.
            server.State.skillPoints = 1;
            LocalProgression.StoreConfirmed(server.State);
            Assert(LocalProgression.QueueSkillUpgrade(CharacterId, SkillType.HighGrade) != null,
                "An upgrade must queue again after the server value changes.");
            server.Calls.Clear();
            PendingSync.Flush(server, server, result => outcome = result);
            server.State.skillPoints = 5;
            server.UpgradeSkill("Offline1Renamed", CharacterId, SkillType.HighGrade, 2, _ => { }, _ => { });
            Assert(outcome == PendingSyncOutcome.Completed && server.State.highGradeSkillLevel == 2 &&
                   server.State.skillPoints == 5, "Sending the same target level again must not apply twice.");
        }

        private static void AssertBackendConstant(string source, string name, int value)
        {
            Assert(Regex.IsMatch(source, $@"export const {name}\s*=\s*{value};"),
                $"{name} must equal {value} in the Backend contract.");
        }

        private static void QueueRun(CreateRunRequest request, int experience)
        {
            LocalPendingRunStorage.RecordRunExperience(request.clientRunId, experience);
            Assert(LocalPendingRunStorage.Save(request), $"Run {request.clientRunId} must be queued.");
        }

        private static CreateRunRequest Run(string clientRunId, int reachedFloor, int killCount) => new()
        {
            clientRunId = clientRunId,
            userId = LocalProfile.OwnerId,
            characterId = CharacterId,
            gameVersion = "offline-1",
            reachedFloor = reachedFloor,
            killCount = killCount,
            items = Array.Empty<RunItemDto>()
        };

        private static CharacterProgressDto Progress(int level, int experience, int skillPoints, int low = 1,
            int high = 1)
        {
            CharacterProgressDto progress = MetaProgressionRules.CreateDefault(CharacterId);
            progress.level = level;
            progress.experience = experience;
            progress.experienceToNextLevel = MetaProgressionRules.RequiredExperience(level);
            progress.skillPoints = skillPoints;
            progress.lowGradeSkillLevel = low;
            progress.highGradeSkillLevel = high;
            return progress;
        }

        private static void AssertProgress(CharacterProgressDto progress, int level, int experience, int toNext,
            int points, string label)
        {
            Assert(progress != null && progress.level == level && progress.experience == experience &&
                   progress.experienceToNextLevel == toNext && progress.skillPoints == points,
                $"Progress {label}: expected Lv.{level} {experience}/{toNext} points {points}, got " +
                (progress == null ? "null" : $"Lv.{progress.level} {progress.experience}/" +
                                             $"{progress.experienceToNextLevel} points {progress.skillPoints}") + ".");
        }

        private static string ReadRaw(string key)
        {
            // Profile flags are ints; everything else here is a string.
            return key == "NeedsNewNickname" ? "int:" + PlayerPrefs.GetInt(key) : "str:" + PlayerPrefs.GetString(key);
        }

        private static void RestoreRaw(string key, string value)
        {
            if (value == null) PlayerPrefs.DeleteKey(key);
            else if (value.StartsWith("int:", StringComparison.Ordinal)) PlayerPrefs.SetInt(key, int.Parse(value.Substring(4)));
            else PlayerPrefs.SetString(key, value.Substring(4));
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Beta Offline-1 verification failed: {message}");
        }

        // A server with the Backend's rules for one character. Runs give floor × 100 + kills × 5 unless overridden.
        private sealed class FakeServer : IGameApiClient, ISkillProgressApiClient
        {
            public readonly List<string> Calls = new();
            public readonly Dictionary<string, int> ExperienceOverride = new();
            public CharacterProgressDto State = MetaProgressionRules.CreateDefault(CharacterId);
            public string FailRunId;

            public void GetUser(string nickname, Action<UserProfileResponse> onSuccess, Action<string> onError) =>
                onError?.Invoke("not used");

            public void PostUser(CreateUserRequest request, Action<CreateUserResponse> onSuccess,
                Action<string> onError) => onError?.Invoke("not used");

            public void PostRun(CreateRunRequest request, Action<CreateRunResponse> onSuccess, Action<string> onError)
            {
                Calls.Add("run:" + request.clientRunId);
                if (!LocalProfile.HasServerAccount)
                {
                    onError?.Invoke("PROFILE_KEY_MISMATCH");
                    return;
                }
                if (request.clientRunId == FailRunId)
                {
                    onError?.Invoke("NETWORK_ERROR");
                    return;
                }

                int experience = ExperienceOverride.TryGetValue(request.clientRunId, out int overridden)
                    ? overridden
                    : MetaProgressionRules.CalculateRunExperience(request.reachedFloor, request.killCount, null);
                State = MetaProgressionRules.ApplyRunExperience(State, experience);
                onSuccess?.Invoke(new CreateRunResponse
                {
                    success = true,
                    data = new RunData { runId = "run-" + request.clientRunId, experienceGained = experience, progress = Copy() }
                });
            }

            public void UpgradeSkill(string nickname, string characterId, SkillType skillType, int targetLevel,
                Action<CharacterProgressDto> onSuccess, Action<SkillUpgradeError> onError)
            {
                Calls.Add($"skill:{skillType}:{targetLevel}");
                int current = skillType == SkillType.LowGrade ? State.lowGradeSkillLevel : State.highGradeSkillLevel;
                if (targetLevel == current)
                {
                    onSuccess?.Invoke(Copy());
                    return;
                }
                if (!MetaProgressionRules.TryApplySkillUpgrade(State, skillType, targetLevel))
                {
                    onError?.Invoke(SkillUpgradeError.FromApiError(422,
                        targetLevel != current + 1 ? "INVALID_SKILL_TARGET_LEVEL" : "SKILL_POINT_NOT_ENOUGH",
                        "refused"));
                    return;
                }
                onSuccess?.Invoke(Copy());
            }

            private CharacterProgressDto Copy() => MetaProgressionRules.Normalize(CharacterId, State);
        }
    }
}
