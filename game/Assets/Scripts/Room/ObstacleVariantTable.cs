using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [Serializable]
    public sealed class ObstacleVariantEntry
    {
        [SerializeField] private ObstacleVariantDefinition variant;
        [SerializeField, Min(1)] private int weight = 1;

        public ObstacleVariantEntry(ObstacleVariantDefinition configuredVariant, int configuredWeight)
        {
            variant = configuredVariant;
            weight = configuredWeight;
        }

        public ObstacleVariantDefinition Variant => variant;
        public int Weight => weight;
    }

    [CreateAssetMenu(fileName = "ObstacleVariantTable", menuName = "Trickal Fan Game/Obstacle Variant Table")]
    public sealed class ObstacleVariantTable : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float specialRoomChance = 0.4f;
        [SerializeField] private ObstacleVariantEntry[] entries = Array.Empty<ObstacleVariantEntry>();

        public float SpecialRoomChance => specialRoomChance;
        public IReadOnlyList<ObstacleVariantEntry> Entries => entries;

        public void Configure(float configuredRoomChance, ObstacleVariantEntry[] configuredEntries)
        {
            specialRoomChance = Mathf.Clamp01(configuredRoomChance);
            entries = configuredEntries ?? Array.Empty<ObstacleVariantEntry>();
        }

        public bool TryValidate(out string error)
        {
            if (specialRoomChance < 0f || specialRoomChance > 1f)
            {
                error = $"{name} special room chance must be within 0..1.";
                return false;
            }

            if (entries == null || entries.Length == 0)
            {
                error = $"{name} needs at least one obstacle variant.";
                return false;
            }

            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (ObstacleVariantEntry entry in entries)
            {
                if (entry == null || entry.Variant == null)
                {
                    error = $"{name} needs valid obstacle variants.";
                    return false;
                }

                if (!entry.Variant.TryValidate(out error))
                {
                    error = $"{name} contains an invalid variant. {error}";
                    return false;
                }

                if (!ids.Add(entry.Variant.VariantId))
                {
                    error = $"{name} duplicates variant ID '{entry.Variant.VariantId}'.";
                    return false;
                }

                if (entry.Weight <= 0)
                {
                    error = $"{name} variant '{entry.Variant.VariantId}' needs a positive weight.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public ObstacleVariantDefinition Select(float unit)
        {
            int totalWeight = 0;
            foreach (ObstacleVariantEntry entry in entries) totalWeight += entry.Weight;
            int pick = Mathf.Min(totalWeight - 1, Mathf.FloorToInt(Mathf.Clamp01(unit) * totalWeight));
            foreach (ObstacleVariantEntry entry in entries)
            {
                if (pick < entry.Weight) return entry.Variant;
                pick -= entry.Weight;
            }

            return entries[^1].Variant;
        }
    }
}
