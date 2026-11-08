using System;
using TrickalFanGame.Data;
using TrickalFanGame.Network;

namespace TrickalFanGame.Meta
{
    public enum ProfileRegistrationOutcome
    {
        Registered,      // The server registered the profile.
        SavedOffline,    // The server could not be reached; the profile was kept on this PC.
        NicknameTaken,   // The server answered that the nickname belongs to someone else.
        Rejected         // Any other refusal (invalid nickname, profile conflict, bad response).
    }

    // Offline-0 (docs/30 §3.1 S3): registration never blocks play. When the server cannot be reached the nickname
    // is kept on this PC and the player starts right away. The title retries on each launch; once the server
    // registers the profile, pending Runs move to the server user ID and are sent. A taken nickname asks the player
    // for a new one at the title.
    public static class ProfileRegistration
    {
        // From the nickname screen: a new profile, or a new nickname after the server reported a taken one.
        public static void Register(IUserRegistrationClient client, string nickname,
            Action<ProfileRegistrationOutcome, RegistrationError?> onDone)
        {
            if (client == null)
            {
                SaveOffline(nickname);
                onDone?.Invoke(ProfileRegistrationOutcome.SavedOffline, null);
                return;
            }

            client.PostUserWithErrorInfo(BuildRequest(nickname),
                response => onDone?.Invoke(Complete(response), null),
                error =>
                {
                    if (IsServerUnreachable(error))
                    {
                        SaveOffline(nickname);
                        onDone?.Invoke(ProfileRegistrationOutcome.SavedOffline, error);
                        return;
                    }

                    onDone?.Invoke(error.Type == RegistrationErrorType.NicknameExists
                        ? ProfileRegistrationOutcome.NicknameTaken
                        : ProfileRegistrationOutcome.Rejected, error);
                });
        }

        // From the title: registers a profile that so far exists only on this PC. Nothing to do otherwise.
        public static bool TrySyncOfflineProfile(IUserRegistrationClient client,
            Action<ProfileRegistrationOutcome> onDone)
        {
            if (client == null || !LocalProfile.IsRegistered || LocalProfile.HasServerAccount ||
                LocalProfile.NeedsNewNickname)
                return false;

            client.PostUserWithErrorInfo(BuildRequest(LocalProfile.Nickname),
                response => onDone?.Invoke(Complete(response)),
                error =>
                {
                    if (IsServerUnreachable(error))
                    {
                        onDone?.Invoke(ProfileRegistrationOutcome.SavedOffline);
                        return;
                    }

                    if (error.Type == RegistrationErrorType.NicknameExists)
                    {
                        LocalProfile.MarkNicknameTaken();
                        onDone?.Invoke(ProfileRegistrationOutcome.NicknameTaken);
                        return;
                    }

                    onDone?.Invoke(ProfileRegistrationOutcome.Rejected);
                });
            return true;
        }

        // Runs queued before registration (or by a Run launched before it finished) belong to the device key.
        public static int MovePendingRunsToServerAccount()
        {
            return LocalProfile.HasServerAccount
                ? LocalPendingRunStorage.ReassignOwner(LocalProfile.ClientProfileId, LocalProfile.UserId)
                : 0;
        }

        public static bool IsServerUnreachable(RegistrationError error) =>
            error.Type == RegistrationErrorType.NetworkError || error.Type == RegistrationErrorType.ServerError;

        private static CreateUserRequest BuildRequest(string nickname) => new CreateUserRequest
        {
            clientProfileId = LocalProfile.ClientProfileId,
            nickname = nickname
        };

        private static void SaveOffline(string nickname)
        {
            LocalProfile.SaveOfflineProfile(nickname);
        }

        private static ProfileRegistrationOutcome Complete(CreateUserResponse response)
        {
            CreateUserData data = response?.data;
            if (data == null || string.IsNullOrWhiteSpace(data.id) || string.IsNullOrWhiteSpace(data.nickname))
                return ProfileRegistrationOutcome.Rejected;

            LocalProfile.SaveProfile(data.id, data.nickname);
            MovePendingRunsToServerAccount();
            return ProfileRegistrationOutcome.Registered;
        }
    }
}
