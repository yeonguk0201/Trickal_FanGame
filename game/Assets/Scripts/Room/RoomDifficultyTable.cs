using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Existing numeric values are serialized in the difficulty table. Append tiers only.
    public enum RoomDifficultyTier
    {
        Easy,
        Normal,
        Hard,
    }

    [Serializable]
    public struct EnemyThreatScore
    {
        [SerializeField] private EncounterEnemyRole role;
        [SerializeField, Min(1)] private int score;

        public EnemyThreatScore(EncounterEnemyRole configuredRole, int configuredScore)
        {
            role = configuredRole;
            score = configuredScore;
        }

        public EncounterEnemyRole Role => role;
        public int Score => score;
    }

    [Serializable]
    public struct RoomDifficultyTierBand
    {
        [SerializeField] private RoomDifficultyTier tier;
        [SerializeField, Min(0)] private int minimumScore;

        public RoomDifficultyTierBand(RoomDifficultyTier configuredTier, int configuredMinimumScore)
        {
            tier = configuredTier;
            minimumScore = configuredMinimumScore;
        }

        public RoomDifficultyTier Tier => tier;
        public int MinimumScore => minimumScore;
    }

    [Serializable]
    public struct FloorDifficultyRange
    {
        [SerializeField, Min(1)] private int floorNumber;
        [SerializeField, Min(0)] private int minimumScore;
        [SerializeField, Min(0)] private int maximumScore;

        public FloorDifficultyRange(int configuredFloorNumber, int configuredMinimumScore, int configuredMaximumScore)
        {
            floorNumber = configuredFloorNumber;
            minimumScore = configuredMinimumScore;
            maximumScore = configuredMaximumScore;
        }

        public int FloorNumber => floorNumber;
        public int MinimumScore => minimumScore;
        public int MaximumScore => maximumScore;
        public bool Contains(int score) => score >= minimumScore && score <= maximumScore;
    }

    // Target tier weights at the nearest (normalized distance 0) and farthest (1) combat room of a floor.
    [Serializable]
    public struct RoomDifficultyDistanceWeight
    {
        [SerializeField] private RoomDifficultyTier tier;
        [SerializeField, Min(0)] private int nearWeight;
        [SerializeField, Min(0)] private int farWeight;

        public RoomDifficultyDistanceWeight(RoomDifficultyTier configuredTier, int configuredNearWeight,
            int configuredFarWeight)
        {
            tier = configuredTier;
            nearWeight = configuredNearWeight;
            farWeight = configuredFarWeight;
        }

        public RoomDifficultyTier Tier => tier;
        public int NearWeight => nearWeight;
        public int FarWeight => farWeight;
    }

    // Stored on a generated combat room so revisits and tools read the same difficulty without re-rolling.
    public readonly struct GeneratedRoomDifficulty
    {
        public GeneratedRoomDifficulty(int distanceFromStart, int score, RoomDifficultyTier tier,
            RoomDifficultyTier targetTier, int availableTierMask)
        {
            DistanceFromStart = distanceFromStart;
            Score = score;
            Tier = tier;
            TargetTier = targetTier;
            AvailableTierMask = availableTierMask;
        }

        public int DistanceFromStart { get; }
        public int Score { get; }
        public RoomDifficultyTier Tier { get; }
        public RoomDifficultyTier TargetTier { get; }
        // Bit (1 << tier) is set for every tier a compatible, in-range Encounter could have provided.
        public int AvailableTierMask { get; }
        public bool IsTierAvailable(RoomDifficultyTier tier) => (AvailableTierMask & (1 << (int)tier)) != 0;
    }

    // Threat scores validate authored Encounters and classify generated rooms; they never fill rooms at runtime.
    [CreateAssetMenu(fileName = "RoomDifficultyTable", menuName = "Trickal Fan Game/Room Difficulty Table")]
    public sealed class RoomDifficultyTable : ScriptableObject
    {
        [SerializeField] private EnemyThreatScore[] threatScores = Array.Empty<EnemyThreatScore>();
        [SerializeField] private RoomDifficultyTierBand[] tierBands = Array.Empty<RoomDifficultyTierBand>();
        [SerializeField] private FloorDifficultyRange[] floorRanges = Array.Empty<FloorDifficultyRange>();
        [SerializeField] private RoomDifficultyDistanceWeight[] distanceWeights =
            Array.Empty<RoomDifficultyDistanceWeight>();

        public IReadOnlyList<EnemyThreatScore> ThreatScores => threatScores;
        public IReadOnlyList<RoomDifficultyTierBand> TierBands => tierBands;
        public IReadOnlyList<FloorDifficultyRange> FloorRanges => floorRanges;
        public IReadOnlyList<RoomDifficultyDistanceWeight> DistanceWeights => distanceWeights;

        public void Configure(EnemyThreatScore[] configuredThreatScores, RoomDifficultyTierBand[] configuredTierBands,
            FloorDifficultyRange[] configuredFloorRanges, RoomDifficultyDistanceWeight[] configuredDistanceWeights)
        {
            threatScores = configuredThreatScores ?? Array.Empty<EnemyThreatScore>();
            tierBands = configuredTierBands ?? Array.Empty<RoomDifficultyTierBand>();
            floorRanges = configuredFloorRanges ?? Array.Empty<FloorDifficultyRange>();
            distanceWeights = configuredDistanceWeights ?? Array.Empty<RoomDifficultyDistanceWeight>();
        }

        public bool TryValidate(out string error)
        {
            HashSet<EncounterEnemyRole> roles = new();
            foreach (EnemyThreatScore entry in threatScores ?? Array.Empty<EnemyThreatScore>())
            {
                if (!Enum.IsDefined(typeof(EncounterEnemyRole), entry.Role) || entry.Role == EncounterEnemyRole.Boss ||
                    entry.Score < 1 || !roles.Add(entry.Role))
                {
                    error = $"Room difficulty table has an invalid, boss, or duplicated threat score for '{entry.Role}'.";
                    return false;
                }
            }

            foreach (EncounterEnemyRole role in Enum.GetValues(typeof(EncounterEnemyRole)))
            {
                if (role != EncounterEnemyRole.Boss && !roles.Contains(role))
                {
                    error = $"Room difficulty table is missing a threat score for normal enemy role '{role}'.";
                    return false;
                }
            }

            RoomDifficultyTier[] tiers = (RoomDifficultyTier[])Enum.GetValues(typeof(RoomDifficultyTier));
            if (tierBands == null || tierBands.Length != tiers.Length)
            {
                error = "Room difficulty table needs exactly one band per difficulty tier.";
                return false;
            }

            for (int index = 0; index < tierBands.Length; index++)
            {
                if (tierBands[index].Tier != tiers[index] ||
                    (index == 0 ? tierBands[index].MinimumScore != 0
                        : tierBands[index].MinimumScore <= tierBands[index - 1].MinimumScore))
                {
                    error = "Room difficulty tier bands must follow tier order, start at 0, and strictly increase.";
                    return false;
                }
            }

            if (floorRanges == null || floorRanges.Length == 0)
            {
                error = "Room difficulty table needs at least one floor range.";
                return false;
            }

            HashSet<int> floors = new();
            foreach (FloorDifficultyRange range in floorRanges)
            {
                if (range.FloorNumber < 1 || range.MinimumScore < 0 || range.MaximumScore < range.MinimumScore ||
                    !floors.Add(range.FloorNumber))
                {
                    error = $"Room difficulty table has an invalid or duplicated range for floor {range.FloorNumber}.";
                    return false;
                }
            }

            if (distanceWeights == null || distanceWeights.Length != tiers.Length)
            {
                error = "Room difficulty table needs exactly one distance weight per difficulty tier.";
                return false;
            }

            for (int index = 0; index < distanceWeights.Length; index++)
            {
                if (distanceWeights[index].Tier != tiers[index] ||
                    distanceWeights[index].NearWeight < 0 || distanceWeights[index].FarWeight < 0)
                {
                    error = "Room difficulty distance weights must follow tier order and cannot be negative.";
                    return false;
                }
            }

            if (distanceWeights.Sum(weight => weight.NearWeight) <= 0 ||
                distanceWeights.Sum(weight => weight.FarWeight) <= 0)
            {
                error = "Room difficulty distance weights need a positive total at both ends of the curve.";
                return false;
            }

            error = null;
            return true;
        }

        public bool TryGetThreat(EncounterEnemyRole role, out int score)
        {
            foreach (EnemyThreatScore entry in threatScores ?? Array.Empty<EnemyThreatScore>())
            {
                if (entry.Role != role) continue;
                score = entry.Score;
                return true;
            }

            score = 0;
            return false;
        }

        public RoomDifficultyTier Classify(int score)
        {
            RoomDifficultyTier result = tierBands[0].Tier;
            foreach (RoomDifficultyTierBand band in tierBands)
            {
                if (score >= band.MinimumScore) result = band.Tier;
            }

            return result;
        }

        public bool TryGetFloorRange(int floorNumber, out FloorDifficultyRange range)
        {
            foreach (FloorDifficultyRange candidate in floorRanges ?? Array.Empty<FloorDifficultyRange>())
            {
                if (candidate.FloorNumber != floorNumber) continue;
                range = candidate;
                return true;
            }

            range = default;
            return false;
        }

        // Integer linear interpolation between the near and far weights. distanceStep is the room's distance above
        // the floor's nearest combat room and distanceSpan the farthest minus nearest; a zero span uses the midpoint.
        public int[] TierWeightsAt(int distanceStep, int distanceSpan)
        {
            if (distanceSpan < 0 || distanceStep < 0 || distanceStep > distanceSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(distanceStep),
                    $"Distance step {distanceStep} must be within span {distanceSpan}.");
            }

            int[] weights = new int[distanceWeights.Length];
            for (int index = 0; index < weights.Length; index++)
            {
                RoomDifficultyDistanceWeight weight = distanceWeights[index];
                weights[index] = distanceSpan == 0
                    ? weight.NearWeight + weight.FarWeight
                    : weight.NearWeight * (distanceSpan - distanceStep) + weight.FarWeight * distanceStep;
            }

            return weights;
        }

        public bool TryComputeThreatRange(EncounterDefinition definition, out int minimum, out int maximum,
            out string error)
        {
            minimum = 0;
            maximum = 0;
            if (definition == null)
            {
                error = "Threat range requires an Encounter.";
                return false;
            }

            foreach (EncounterWaveDefinition wave in definition.Waves)
            {
                foreach (EncounterSpawnRule rule in wave.SpawnRules)
                {
                    int ruleMinimum = int.MaxValue;
                    int ruleMaximum = 0;
                    foreach (EncounterEnemyCandidate candidate in rule.Candidates)
                    {
                        if (!TryGetThreat(candidate.EnemyRole, out int score))
                        {
                            error = $"Encounter '{definition.EncounterId}' uses role '{candidate.EnemyRole}' " +
                                    "without a threat score.";
                            return false;
                        }

                        ruleMinimum = Math.Min(ruleMinimum, score);
                        ruleMaximum = Math.Max(ruleMaximum, score);
                    }

                    minimum += ruleMinimum * rule.Count;
                    maximum += ruleMaximum * rule.Count;
                }
            }

            error = null;
            return true;
        }

        public bool TryScore(IReadOnlyList<ResolvedEncounterSpawn[]> waves, int layoutModifier, out int score,
            out string error)
        {
            score = layoutModifier;
            foreach (ResolvedEncounterSpawn[] wave in waves ?? Array.Empty<ResolvedEncounterSpawn[]>())
            {
                foreach (ResolvedEncounterSpawn spawn in wave)
                {
                    if (!TryGetThreat(spawn.Role, out int threat))
                    {
                        error = $"Resolved enemy role '{spawn.Role}' has no threat score.";
                        return false;
                    }

                    score += threat;
                }
            }

            error = null;
            return true;
        }
    }
}
