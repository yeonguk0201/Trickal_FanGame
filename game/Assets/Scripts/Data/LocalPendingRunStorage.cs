using System;
using System.Collections.Generic;
using TrickalFanGame.Network;
using UnityEngine;

namespace TrickalFanGame.Data
{
    /// <summary>
    /// Persists failed Run save requests until the Backend confirms each clientRunId.
    /// </summary>
    public static class LocalPendingRunStorage
    {
        private const string KeyPendingRunQueue = "PendingRunQueue.v1";
        private const string LegacyRequestKey = "PendingRunRequest";
        private const string LegacyTimestampKey = "PendingRunTimestamp";

        [Serializable]
        private sealed class PendingRunEntry
        {
            public CreateRunRequest request;
            public string savedAtUtc;
        }

        [Serializable]
        private sealed class PendingRunQueue
        {
            public PendingRunEntry[] entries = Array.Empty<PendingRunEntry>();
        }

        public static bool HasPending => Count > 0;
        public static int Count => ReadEntries().Count;

        /// <summary>
        /// Adds a request once. A clientRunId already in the queue keeps its original payload.
        /// </summary>
        public static bool Save(CreateRunRequest request)
        {
            if (!IsValid(request))
            {
                Debug.LogWarning("[LocalPendingRunStorage] Ignored an invalid pending Run request.");
                return false;
            }

            List<PendingRunEntry> entries = ReadEntries();
            foreach (PendingRunEntry entry in entries)
            {
                if (entry.request.clientRunId != request.clientRunId) continue;

                if (JsonUtility.ToJson(entry.request) != JsonUtility.ToJson(request))
                {
                    Debug.LogError($"[LocalPendingRunStorage] Conflicting payload for clientRunId {request.clientRunId}. The original request was preserved.");
                    return false;
                }

                return true;
            }

            entries.Add(new PendingRunEntry
            {
                request = request,
                savedAtUtc = DateTime.UtcNow.ToString("o")
            });
            WriteEntries(entries);
            Debug.Log($"[LocalPendingRunStorage] Queued pending Run request: {request.clientRunId}");
            return true;
        }

        /// <summary>
        /// Returns the oldest request, optionally restricted to one local profile owner.
        /// </summary>
        public static CreateRunRequest Load(string userId = null)
        {
            foreach (PendingRunEntry entry in ReadEntries())
            {
                if (string.IsNullOrWhiteSpace(userId) || entry.request.userId == userId)
                    return entry.request;
            }

            return null;
        }

        public static bool Contains(string clientRunId)
        {
            if (string.IsNullOrWhiteSpace(clientRunId)) return false;

            foreach (PendingRunEntry entry in ReadEntries())
            {
                if (entry.request.clientRunId == clientRunId) return true;
            }

            return false;
        }

        /// <summary>
        /// Removes only the request confirmed by the Backend.
        /// </summary>
        public static bool Remove(string clientRunId)
        {
            if (string.IsNullOrWhiteSpace(clientRunId)) return false;

            List<PendingRunEntry> entries = ReadEntries();
            int removed = entries.RemoveAll(entry => entry.request.clientRunId == clientRunId);
            if (removed == 0) return false;

            WriteEntries(entries);
            Debug.Log($"[LocalPendingRunStorage] Removed confirmed Run request: {clientRunId}");
            return true;
        }

        /// <summary>
        /// Clears every queued request. Intended only for explicit profile or test cleanup.
        /// </summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(KeyPendingRunQueue);
            PlayerPrefs.DeleteKey(LegacyRequestKey);
            PlayerPrefs.DeleteKey(LegacyTimestampKey);
            PlayerPrefs.Save();
            Debug.Log("[LocalPendingRunStorage] Cleared all pending Run requests.");
        }

        private static List<PendingRunEntry> ReadEntries()
        {
            MigrateLegacyRequest();
            string json = PlayerPrefs.GetString(KeyPendingRunQueue, string.Empty);
            if (string.IsNullOrEmpty(json)) return new List<PendingRunEntry>();

            try
            {
                PendingRunQueue queue = JsonUtility.FromJson<PendingRunQueue>(json);
                var validEntries = new List<PendingRunEntry>();
                if (queue?.entries == null) return validEntries;

                foreach (PendingRunEntry entry in queue.entries)
                {
                    if (entry != null && IsValid(entry.request)) validEntries.Add(entry);
                }

                return validEntries;
            }
            catch (Exception ex)
            {
                // Preserve the raw PlayerPrefs value for diagnosis instead of deleting unsent data.
                Debug.LogWarning($"[LocalPendingRunStorage] Failed to parse pending Run queue: {ex.Message}");
                return new List<PendingRunEntry>();
            }
        }

        private static void WriteEntries(List<PendingRunEntry> entries)
        {
            if (entries.Count == 0)
                PlayerPrefs.DeleteKey(KeyPendingRunQueue);
            else
                PlayerPrefs.SetString(KeyPendingRunQueue,
                    JsonUtility.ToJson(new PendingRunQueue { entries = entries.ToArray() }));

            PlayerPrefs.Save();
        }

        private static void MigrateLegacyRequest()
        {
            if (PlayerPrefs.HasKey(KeyPendingRunQueue) || !PlayerPrefs.HasKey(LegacyRequestKey)) return;

            string json = PlayerPrefs.GetString(LegacyRequestKey, string.Empty);
            try
            {
                CreateRunRequest request = JsonUtility.FromJson<CreateRunRequest>(json);
                if (!IsValid(request)) return;

                string savedAt = PlayerPrefs.GetString(LegacyTimestampKey, DateTime.UtcNow.ToString("o"));
                WriteEntries(new List<PendingRunEntry>
                {
                    new PendingRunEntry { request = request, savedAtUtc = savedAt }
                });
                PlayerPrefs.DeleteKey(LegacyRequestKey);
                PlayerPrefs.DeleteKey(LegacyTimestampKey);
                PlayerPrefs.Save();
                Debug.Log("[LocalPendingRunStorage] Migrated the legacy pending Run request.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalPendingRunStorage] Failed to migrate legacy pending Run request: {ex.Message}");
            }
        }

        private static bool IsValid(CreateRunRequest request)
        {
            return request != null &&
                   !string.IsNullOrWhiteSpace(request.clientRunId) &&
                   !string.IsNullOrWhiteSpace(request.userId) &&
                   !string.IsNullOrWhiteSpace(request.characterId);
        }
    }
}
