using System;
using UnityEngine;

namespace TrickalFanGame.Data
{
    public static class LocalProfile
    {
        private const string KeyClientProfileId = "ClientProfileId";
        private const string KeyUserId = "UserId";
        private const string KeyNickname = "Nickname";

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

        public static bool IsRegistered => !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(Nickname);

        public static void SaveProfile(string userId, string nickname)
        {
            UserId = userId;
            Nickname = nickname;
        }

        public static void ClearProfile()
        {
            PlayerPrefs.DeleteKey(KeyUserId);
            PlayerPrefs.DeleteKey(KeyNickname);
            PlayerPrefs.Save();
        }
    }
}
