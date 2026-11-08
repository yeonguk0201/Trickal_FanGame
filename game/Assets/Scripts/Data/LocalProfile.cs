using System;
using UnityEngine;

namespace TrickalFanGame.Data
{
    // Offline-0 (docs/30 §3.1): a profile can exist on this PC before the server knows it. Nickname alone means
    // "can play"; UserId is set only once the server registered the profile. Until then OwnerId (the device key)
    // owns pending Runs, and they move to the server user ID when registration succeeds.
    public static class LocalProfile
    {
        private const string KeyClientProfileId = "ClientProfileId";
        private const string KeyUserId = "UserId";
        private const string KeyNickname = "Nickname";
        private const string KeyNeedsNewNickname = "NeedsNewNickname";

        public static string ClientProfileId
        {
            get
            {
                string id = PlayerPrefs.GetString(KeyClientProfileId, string.Empty);
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString();
                    PlayerPrefs.SetString(KeyClientProfileId, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        // Server user ID. Empty while the profile exists only on this PC.
        public static string UserId
        {
            get => PlayerPrefs.GetString(KeyUserId, string.Empty);
            private set
            {
                PlayerPrefs.SetString(KeyUserId, value);
                PlayerPrefs.Save();
            }
        }

        public static string Nickname
        {
            get => PlayerPrefs.GetString(KeyNickname, string.Empty);
            private set
            {
                PlayerPrefs.SetString(KeyNickname, value);
                PlayerPrefs.Save();
            }
        }

        // A local profile exists, so the player can play (with or without the server).
        public static bool IsRegistered => !string.IsNullOrEmpty(Nickname);

        public static bool HasServerAccount => IsRegistered && !string.IsNullOrEmpty(UserId);

        // Owner written into Run save requests: the server user ID, or the device key before registration.
        public static string OwnerId => HasServerAccount ? UserId : ClientProfileId;

        // The server answered that the offline nickname is taken; the title asks for a new one.
        public static bool NeedsNewNickname => IsRegistered && !HasServerAccount &&
                                               PlayerPrefs.GetInt(KeyNeedsNewNickname, 0) == 1;

        public static void SaveProfile(string userId, string nickname)
        {
            UserId = userId;
            Nickname = nickname;
            PlayerPrefs.DeleteKey(KeyNeedsNewNickname);
            PlayerPrefs.Save();
        }

        // Starts (or renames) a profile that the server has not registered yet.
        public static void SaveOfflineProfile(string nickname)
        {
            PlayerPrefs.DeleteKey(KeyUserId);
            PlayerPrefs.DeleteKey(KeyNeedsNewNickname);
            Nickname = nickname;
        }

        public static void MarkNicknameTaken()
        {
            if (!IsRegistered || HasServerAccount) return;
            PlayerPrefs.SetInt(KeyNeedsNewNickname, 1);
            PlayerPrefs.Save();
        }

        // Development only: forget the device key too, so this PC acts as a new player. The server keeps the old
        // profile, but it can no longer be written to from here.
        public static void ResetDeviceKey()
        {
            ClearProfile();
            PlayerPrefs.DeleteKey(KeyClientProfileId);
            PlayerPrefs.Save();
        }

        public static void ClearProfile()
        {
            PlayerPrefs.DeleteKey(KeyUserId);
            PlayerPrefs.DeleteKey(KeyNickname);
            PlayerPrefs.DeleteKey(KeyNeedsNewNickname);
            PlayerPrefs.Save();
        }
    }
}
