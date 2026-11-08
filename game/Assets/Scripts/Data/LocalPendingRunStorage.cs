using System;
using System.Collections.Generic;
using TrickalFanGame.Network;
using UnityEngine;

namespace TrickalFanGame.Data
{
    /// <summary>
    /// Offline-1: a skill upgrade made while the server could not take it. It waits in the same queue as the Runs
    /// and is sent after the Runs before it. The target level makes a repeated send apply only once.
    /// </summary>
    [Serializable]
    public sealed class PendingSkillUpgrade
    {
        public string upgradeId;
        public string ownerId;
        public string characterId;
        public SkillType skillType;
        public int targetLevel;
    }

    /// <summary>One queued write: a Run save or a skill upgrade.</summary>
    public sealed class PendingSyncItem
    {
        public PendingSyncItem(CreateRunRequest run, PendingSkillUpgrade skillUpgrade)
        {
            Run = run;
            SkillUpgrade = skillUpgrade;
        }

        public CreateRunRequest Run { get; }
        public PendingSkillUpgrade SkillUpgrade { get; }
        public bool IsRun => Run != null;
    }

    /// <summary>
    /// Persists failed Run save requests until the Backend confirms each clientRunId. Offline-1 keeps skill upgrades
    /// in the same queue so both reach the server in the order they happened (docs/30 §3.1 S4).
    /// </summary>
    public static class LocalPendingRunStorage
    {
        private const string KeyPendingRunQueue = "PendingRunQueue.v1";
        private const string KeyRunExperience = "PendingRunExperience.v1";
        private const string LegacyRequestKey = "PendingRunRequest";
        private const string LegacyTimestampKey = "PendingRunTimestamp";

        [Serializable]
        private sealed class PendingRunEntry
        {
            public CreateRunRequest request;
            public PendingSkillUpgrade skillUpgrade;
            public string savedAtUtc;

            // JsonUtility fills a missing object with an empty one, so the kind is told by which payload is valid.
            public bool IsRun => IsValid(request);
            public bool IsSkillUpgrade => !IsRun && IsValidSkillUpgrade(skillUpgrade);
        }

        [Serializable]
        private sealed class PendingRunQueue
        {
            public PendingRunEntry[] entries = Array.Empty<PendingRunEntry>();
        }

        [Serializable]
        private sealed class RunExperienceEntry
        {
            public string clientRunId;
            public int experience;
        }

        [Serializable]
        private sealed class RunExperienceLedger
        {
            public RunExperienceEntry[] entries = Array.Empty<RunExperienceEntry>();
        }

        public static bool HasPending => Count > 0;
        // Run save requests only. Skill upgrades are read through LoadQueue.
        public static int Count => ReadEntries().FindAll(entry => entry.IsRun).Count;

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
                if (!entry.IsRun || entry.request.clientRunId != request.clientRunId) continue;

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
                if (entry.IsRun && (string.IsNullOrWhiteSpace(userId) || entry.request.userId == userId))
                    return entry.request;
            }

            return null;
        }

        /// <summary>
        /// Every queued request, oldest first, optionally restricted to one local profile owner.
        /// </summary>
        public static List<CreateRunRequest> LoadAll(string userId = null)
        {
            var requests = new List<CreateRunRequest>();
            foreach (PendingRunEntry entry in ReadEntries())
            {
                if (entry.IsRun && (string.IsNullOrWhiteSpace(userId) || entry.request.userId == userId))
                    requests.Add(entry.request);
            }

            return requests;
        }

        public static bool Contains(string clientRunId)
        {
            if (string.IsNullOrWhiteSpace(clientRunId)) return false;

            foreach (PendingRunEntry entry in ReadEntries())
            {
                if (entry.IsRun && entry.request.clientRunId == clientRunId) return true;
            }

            return false;
        }

        /// <summary>
        /// Removes only the request confirmed by the Backend, with its locally calculated experience.
        /// </summary>
        public static bool Remove(string clientRunId)
        {
            if (string.IsNullOrWhiteSpace(clientRunId)) return false;

            ForgetRunExperience(clientRunId);
            List<PendingRunEntry> entries = ReadEntries();
            int removed = entries.RemoveAll(entry => entry.IsRun && entry.request.clientRunId == clientRunId);
            if (removed == 0) return false;

            WriteEntries(entries);
            Debug.Log($"[LocalPendingRunStorage] Removed confirmed Run request: {clientRunId}");
            return true;
        }

        /// <summary>
        /// Offline-0: Runs queued before the server registered the profile carry the device key as their owner.
        /// Once registration succeeds they move to the server user ID so the title's recovery sends them. The
        /// Backend finds the owner from the profile key header, and the userId it checks then matches. Offline-1
        /// moves the skill upgrades queued with them too.
        /// </summary>
        public static int ReassignOwner(string fromOwnerId, string toOwnerId)
        {
            if (string.IsNullOrWhiteSpace(fromOwnerId) || string.IsNullOrWhiteSpace(toOwnerId) ||
                fromOwnerId == toOwnerId)
                return 0;

            List<PendingRunEntry> entries = ReadEntries();
            int moved = 0;
            foreach (PendingRunEntry entry in entries)
            {
                if (entry.IsRun && entry.request.userId == fromOwnerId)
                {
                    entry.request.userId = toOwnerId;
                    moved++;
                }
                else if (entry.IsSkillUpgrade && entry.skillUpgrade.ownerId == fromOwnerId)
                {
                    entry.skillUpgrade.ownerId = toOwnerId;
                    moved++;
                }
            }

            if (moved == 0) return 0;
            WriteEntries(entries);
            Debug.Log($"[LocalPendingRunStorage] Moved {moved} pending write(s) to the registered profile.");
            return moved;
        }

        /// <summary>
        /// Offline-1: queues a skill upgrade behind every write already waiting. An upgradeId already queued is kept.
        /// </summary>
        public static bool SaveSkillUpgrade(PendingSkillUpgrade upgrade)
        {
            if (!IsValidSkillUpgrade(upgrade))
            {
                Debug.LogWarning("[LocalPendingRunStorage] Ignored an invalid pending skill upgrade.");
                return false;
            }

            List<PendingRunEntry> entries = ReadEntries();
            if (entries.Exists(entry => entry.IsSkillUpgrade && entry.skillUpgrade.upgradeId == upgrade.upgradeId))
                return true;

            entries.Add(new PendingRunEntry
            {
                skillUpgrade = upgrade,
                savedAtUtc = DateTime.UtcNow.ToString("o")
            });
            WriteEntries(entries);
            Debug.Log($"[LocalPendingRunStorage] Queued skill upgrade {upgrade.characterId} {upgrade.skillType} " +
                      $"Lv.{upgrade.targetLevel}: {upgrade.upgradeId}");
            return true;
        }

        public static bool RemoveSkillUpgrade(string upgradeId)
        {
            if (string.IsNullOrWhiteSpace(upgradeId)) return false;

            List<PendingRunEntry> entries = ReadEntries();
            int removed = entries.RemoveAll(entry =>
                entry.IsSkillUpgrade && entry.skillUpgrade.upgradeId == upgradeId);
            if (removed == 0) return false;

            WriteEntries(entries);
            Debug.Log($"[LocalPendingRunStorage] Removed pending skill upgrade: {upgradeId}");
            return true;
        }

        /// <summary>
        /// Every queued write (Runs and skill upgrades), oldest first, owned by one of the given IDs.
        /// </summary>
        public static List<PendingSyncItem> LoadQueue(params string[] ownerIds)
        {
            var owners = new HashSet<string>(StringComparer.Ordinal);
            if (ownerIds != null)
                foreach (string ownerId in ownerIds)
                    if (!string.IsNullOrWhiteSpace(ownerId)) owners.Add(ownerId);

            var items = new List<PendingSyncItem>();
            foreach (PendingRunEntry entry in ReadEntries())
            {
                if (entry.IsRun && owners.Contains(entry.request.userId))
                    items.Add(new PendingSyncItem(entry.request, null));
                else if (entry.IsSkillUpgrade && owners.Contains(entry.skillUpgrade.ownerId))
                    items.Add(new PendingSyncItem(null, entry.skillUpgrade));
            }

            return items;
        }

        public static bool HasPendingSkillUpgrades(params string[] ownerIds) =>
            LoadQueue(ownerIds).Exists(item => !item.IsRun);

        /// <summary>
        /// Offline-1: the experience the game calculated when a Run ended, kept until the server confirms the Run.
        /// The Frontend scene does not know item rarities, so the result screen and the offline progress read it here.
        /// </summary>
        public static void RecordRunExperience(string clientRunId, int experience)
        {
            if (string.IsNullOrWhiteSpace(clientRunId)) return;

            List<RunExperienceEntry> entries = ReadRunExperience();
            entries.RemoveAll(entry => entry.clientRunId == clientRunId);
            entries.Add(new RunExperienceEntry { clientRunId = clientRunId, experience = Mathf.Max(0, experience) });
            WriteRunExperience(entries);
        }

        public static bool TryGetRunExperience(string clientRunId, out int experience)
        {
            experience = 0;
            if (string.IsNullOrWhiteSpace(clientRunId)) return false;

            RunExperienceEntry found = ReadRunExperience().Find(entry => entry.clientRunId == clientRunId);
            if (found == null) return false;
            experience = found.experience;
            return true;
        }

        /// <summary>
        /// Clears every queued request. Intended only for explicit profile or test cleanup.
        /// </summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(KeyPendingRunQueue);
            PlayerPrefs.DeleteKey(KeyRunExperience);
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
                    if (entry != null && (entry.IsRun || entry.IsSkillUpgrade)) validEntries.Add(entry);
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

        private static void ForgetRunExperience(string clientRunId)
        {
            List<RunExperienceEntry> entries = ReadRunExperience();
            if (entries.RemoveAll(entry => entry.clientRunId == clientRunId) > 0) WriteRunExperience(entries);
        }

        private static List<RunExperienceEntry> ReadRunExperience()
        {
            string json = PlayerPrefs.GetString(KeyRunExperience, string.Empty);
            if (string.IsNullOrEmpty(json)) return new List<RunExperienceEntry>();

            try
            {
                RunExperienceLedger ledger = JsonUtility.FromJson<RunExperienceLedger>(json);
                var entries = new List<RunExperienceEntry>();
                if (ledger?.entries == null) return entries;
                foreach (RunExperienceEntry entry in ledger.entries)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.clientRunId)) entries.Add(entry);
                return entries;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalPendingRunStorage] Failed to parse Run experience records: {ex.Message}");
                return new List<RunExperienceEntry>();
            }
        }

        private static void WriteRunExperience(List<RunExperienceEntry> entries)
        {
            if (entries.Count == 0)
                PlayerPrefs.DeleteKey(KeyRunExperience);
            else
                PlayerPrefs.SetString(KeyRunExperience,
                    JsonUtility.ToJson(new RunExperienceLedger { entries = entries.ToArray() }));

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

        private static bool IsValidSkillUpgrade(PendingSkillUpgrade upgrade)
        {
            return upgrade != null &&
                   !string.IsNullOrWhiteSpace(upgrade.upgradeId) &&
                   !string.IsNullOrWhiteSpace(upgrade.ownerId) &&
                   !string.IsNullOrWhiteSpace(upgrade.characterId) &&
                   upgrade.targetLevel > 1;
        }
    }
}
