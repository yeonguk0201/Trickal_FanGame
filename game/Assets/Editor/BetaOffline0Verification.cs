using System;
using System.Collections.Generic;
using TrickalFanGame.Data;
using TrickalFanGame.Meta;
using TrickalFanGame.Network;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Offline-0 (docs/30 §3.1 S3): registration never blocks play. Checks that an unreachable server keeps the
    // profile on this PC, that the offline nickname is never looked up on the server, that a taken nickname asks
    // for a new one, and that registering later moves the queued Runs to the server user ID.
    public static class BetaOffline0Verification
    {
        private const string ServerUserId = "40000000-0000-4000-8000-000000000001";
        private const string QueueKey = "PendingRunQueue.v1";
        private static readonly string[] ProfileKeys = { "UserId", "Nickname", "NeedsNewNickname" };

        [MenuItem("Trickal Fan Game/Beta/Verify Offline-0 Registration")]
        public static void Verify()
        {
            Dictionary<string, string> savedStrings = new();
            foreach (string key in ProfileKeys) savedStrings[key] = PlayerPrefs.GetString(key, null);
            int savedNeedsNew = PlayerPrefs.GetInt("NeedsNewNickname", 0);
            bool hadNeedsNew = PlayerPrefs.HasKey("NeedsNewNickname");
            string savedQueue = PlayerPrefs.HasKey(QueueKey) ? PlayerPrefs.GetString(QueueKey) : null;
            try
            {
                Run();
            }
            finally
            {
                foreach (string key in new[] { "UserId", "Nickname" })
                {
                    if (savedStrings[key] == null) PlayerPrefs.DeleteKey(key);
                    else PlayerPrefs.SetString(key, savedStrings[key]);
                }
                if (hadNeedsNew) PlayerPrefs.SetInt("NeedsNewNickname", savedNeedsNew);
                else PlayerPrefs.DeleteKey("NeedsNewNickname");
                if (savedQueue == null) PlayerPrefs.DeleteKey(QueueKey);
                else PlayerPrefs.SetString(QueueKey, savedQueue);
                PlayerPrefs.Save();
            }

            Debug.Log("Beta Offline-0 verification passed: an unreachable server (network or 5xx) keeps the profile " +
                      "on this PC and play starts, the offline nickname is never looked up on the server, a taken " +
                      "nickname asks for a new one (renaming offline also works), and registering later moves the " +
                      "queued Runs to the server user ID. NICKNAME_ALREADY_EXISTS is read as a taken nickname.");
        }

        private static void Run()
        {
            LocalProfile.ClearProfile();
            PlayerPrefs.DeleteKey(QueueKey);
            string deviceKey = LocalProfile.ClientProfileId;

            // The Backend's real code for a taken nickname.
            Assert(RegistrationError.FromApiError(409, "NICKNAME_ALREADY_EXISTS", "taken").Type ==
                   RegistrationErrorType.NicknameExists, "NICKNAME_ALREADY_EXISTS must read as a taken nickname.");

            // An invalid nickname is refused and nothing is kept.
            ProfileRegistrationOutcome? outcome = null;
            ProfileRegistration.Register(FakeClient.Fail(RegistrationError.FromApiError(422, "VALIDATION_ERROR", "bad")),
                "Bad", (result, _) => outcome = result);
            Assert(outcome == ProfileRegistrationOutcome.Rejected && !LocalProfile.IsRegistered,
                "A refused nickname must not create a local profile.");

            // A 5xx and a network failure both start offline.
            ProfileRegistration.Register(FakeClient.Fail(RegistrationError.FromApiError(503, "HTTP_ERROR", "down")),
                "Offline5xx", (result, _) => outcome = result);
            Assert(outcome == ProfileRegistrationOutcome.SavedOffline && LocalProfile.Nickname == "Offline5xx",
                "A server error must keep the profile offline.");
            LocalProfile.ClearProfile();

            var network = FakeClient.Fail(RegistrationError.NetworkFailure("offline"));
            ProfileRegistration.Register(network, "Erpin", (result, _) => outcome = result);
            Assert(network.Requests.Count == 1 && network.Requests[0].clientProfileId == deviceKey,
                "Registration must send the device key.");
            Assert(outcome == ProfileRegistrationOutcome.SavedOffline && LocalProfile.IsRegistered &&
                   !LocalProfile.HasServerAccount && LocalProfile.Nickname == "Erpin" &&
                   LocalProfile.OwnerId == deviceKey,
                "An unreachable server must keep the nickname on this PC with the device key as owner.");
            Assert(PlayerProgressClient.IsUnregisteredLocalProfile("Erpin"),
                "An offline nickname must not be looked up on the server (it may be someone else's).");

            // A Run played offline is queued under the device key.
            Assert(LocalPendingRunStorage.Save(new CreateRunRequest
            {
                clientRunId = "50000000-0000-4000-8000-000000000001",
                userId = LocalProfile.OwnerId,
                characterId = "erpin"
            }), "An offline Run must be queued.");

            // Nothing to sync while the server stays unreachable; the profile stays playable.
            ProfileRegistrationOutcome? sync = null;
            Assert(ProfileRegistration.TrySyncOfflineProfile(network, result => sync = result) &&
                   sync == ProfileRegistrationOutcome.SavedOffline && LocalProfile.IsRegistered &&
                   !LocalProfile.HasServerAccount, "A failed sync must keep the offline profile.");

            // The server reports the nickname as taken: ask for a new one and stop syncing until then.
            var taken = FakeClient.Fail(RegistrationError.FromApiError(409, "NICKNAME_ALREADY_EXISTS", "taken"));
            Assert(ProfileRegistration.TrySyncOfflineProfile(taken, result => sync = result) &&
                   sync == ProfileRegistrationOutcome.NicknameTaken && LocalProfile.NeedsNewNickname &&
                   LocalProfile.IsRegistered, "A taken nickname must ask for a new one and keep the profile.");
            Assert(!ProfileRegistration.TrySyncOfflineProfile(taken, _ => { }) && taken.Requests.Count == 1,
                "Sync must wait for the new nickname.");

            // The new nickname is chosen while the server is down again: renamed offline.
            ProfileRegistration.Register(network, "Erpin2", (result, _) => outcome = result);
            Assert(outcome == ProfileRegistrationOutcome.SavedOffline && LocalProfile.Nickname == "Erpin2" &&
                   !LocalProfile.NeedsNewNickname && !LocalProfile.HasServerAccount,
                "Renaming while offline must keep playing under the new nickname.");

            // The server comes back: the title sync registers and moves the queued Run to the server user.
            var success = FakeClient.Succeed(ServerUserId);
            Assert(ProfileRegistration.TrySyncOfflineProfile(success, result => sync = result) &&
                   sync == ProfileRegistrationOutcome.Registered, "The title sync must register the offline profile.");
            Assert(success.Requests.Count == 1 && success.Requests[0].nickname == "Erpin2" &&
                   success.Requests[0].clientProfileId == deviceKey, "The sync must register the current nickname.");
            Assert(LocalProfile.HasServerAccount && LocalProfile.UserId == ServerUserId &&
                   LocalProfile.OwnerId == ServerUserId && !PlayerProgressClient.IsUnregisteredLocalProfile("Erpin2"),
                "Registration must store the server user ID.");
            List<CreateRunRequest> moved = LocalPendingRunStorage.LoadAll(ServerUserId);
            Assert(moved.Count == 1 && LocalPendingRunStorage.LoadAll(deviceKey).Count == 0,
                "Queued offline Runs must move to the server user ID.");
            Assert(!ProfileRegistration.TrySyncOfflineProfile(success, _ => { }),
                "A registered profile must not register again.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Beta Offline-0 verification failed: {message}");
        }

        private sealed class FakeClient : IUserRegistrationClient
        {
            public readonly List<CreateUserRequest> Requests = new();
            private RegistrationError? error;
            private string userId;

            public static FakeClient Fail(RegistrationError failure) => new() { error = failure };
            public static FakeClient Succeed(string id) => new() { userId = id };

            public void PostUserWithErrorInfo(CreateUserRequest request, Action<CreateUserResponse> onSuccess,
                Action<RegistrationError> onError)
            {
                Requests.Add(new CreateUserRequest
                {
                    clientProfileId = request.clientProfileId,
                    nickname = request.nickname
                });
                if (error.HasValue)
                {
                    onError?.Invoke(error.Value);
                    return;
                }

                onSuccess?.Invoke(new CreateUserResponse
                {
                    success = true,
                    data = new CreateUserData
                    {
                        id = userId,
                        clientProfileId = request.clientProfileId,
                        nickname = request.nickname,
                        characterProgress = Array.Empty<CreateUserCharacterProgressDto>()
                    }
                });
            }
        }
    }
}
