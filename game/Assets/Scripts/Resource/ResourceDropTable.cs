using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Resource
{
    [Serializable]
    public sealed class ResourceDropEntry
    {
        [Tooltip("드롭 후보의 안정 ID입니다. 기존 ID의 의미를 바꾸거나 재사용하지 않습니다.")]
        [SerializeField] private string dropId;
        [Tooltip("비워 두면 기능이 생기기 전까지 자리만 차지하는 후보로, 선택되어도 아무것도 떨어뜨리지 않습니다.")]
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(1)] private int weight = 1;

        public ResourceDropEntry(string configuredDropId, GameObject configuredPrefab, int configuredWeight)
        {
            dropId = configuredDropId;
            prefab = configuredPrefab;
            weight = configuredWeight;
        }

        public string DropId => dropId;
        public GameObject Prefab => prefab;
        public int Weight => weight;
    }

    // One seeded roll: first whether anything drops, then one weighted candidate. Each drop source (room clear,
    // later obstacle kinds) owns its own table so chances and candidates can be tuned without code changes.
    [CreateAssetMenu(menuName = "Trickal Fan Game/Resource Drop Table", fileName = "ResourceDropTable")]
    public sealed class ResourceDropTable : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float dropChance;
        [SerializeField] private ResourceDropEntry[] entries = Array.Empty<ResourceDropEntry>();

        public float DropChance => dropChance;
        public IReadOnlyList<ResourceDropEntry> Entries => entries;

        public void Configure(float configuredDropChance, ResourceDropEntry[] configuredEntries)
        {
            dropChance = Mathf.Clamp01(configuredDropChance);
            entries = configuredEntries ?? Array.Empty<ResourceDropEntry>();
        }

        public bool TryValidate(out string error)
        {
            if (dropChance < 0f || dropChance > 1f)
            {
                error = $"{name} drop chance must be within 0..1.";
                return false;
            }

            if (entries == null || entries.Length == 0)
            {
                error = $"{name} needs at least one drop candidate.";
                return false;
            }

            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (ResourceDropEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.DropId) || !ids.Add(entry.DropId))
                {
                    error = $"{name} drop candidates need unique stable IDs.";
                    return false;
                }

                if (entry.Weight <= 0)
                {
                    error = $"{name} candidate '{entry.DropId}' needs a positive weight.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public bool TryRoll(int seed, out ResourceDropEntry entry) => TryRoll(seed, null, out entry);

        // Unavailable candidates (such as the secret pit on a floor without a secret room) leave the weighted pick,
        // so the drop chance stays the same and the remaining candidates share their weight.
        public bool TryRoll(int seed, Predicate<ResourceDropEntry> isAvailable, out ResourceDropEntry entry)
        {
            entry = null;
            if (!TryValidate(out _))
            {
                return false;
            }

            uint state = unchecked((uint)seed);
            if (NextUnit(ref state) >= dropChance)
            {
                return false;
            }

            return TryPick(ref state, isAvailable, out entry);
        }

        // Obstacle-5: one weighted candidate without the drop chance, for the extra pickups of a source that already
        // passed its chance roll.
        public bool TryPick(int seed, Predicate<ResourceDropEntry> isAvailable, out ResourceDropEntry entry)
        {
            entry = null;
            if (!TryValidate(out _)) return false;
            uint state = unchecked((uint)seed);
            return TryPick(ref state, isAvailable, out entry);
        }

        private bool TryPick(ref uint state, Predicate<ResourceDropEntry> isAvailable, out ResourceDropEntry entry)
        {
            entry = null;
            int totalWeight = 0;
            foreach (ResourceDropEntry candidate in entries)
                if (isAvailable == null || isAvailable(candidate)) totalWeight += candidate.Weight;
            if (totalWeight <= 0) return false;
            int pick = Mathf.Min(totalWeight - 1, (int)(NextUnit(ref state) * totalWeight));
            foreach (ResourceDropEntry candidate in entries)
            {
                if (isAvailable != null && !isAvailable(candidate)) continue;
                if (pick < candidate.Weight)
                {
                    entry = candidate;
                    return true;
                }

                pick -= candidate.Weight;
            }

            return false;
        }

        // SplitMix32 step mapped to [0, 1); independent of UnityEngine.Random so seeds replay exactly.
        private static float NextUnit(ref uint state)
        {
            unchecked
            {
                state += 0x9E3779B9u;
                uint value = state;
                value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
                value = (value ^ (value >> 13)) * 0xC2B2AE35u;
                value ^= value >> 16;
                return (value >> 8) * (1f / 16777216f);
            }
        }
    }
}
