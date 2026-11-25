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

    // Density-0 (docs/28 §10.2): a room size (Room Profile) with its own enemy counts, floor ranges and tier bands,
    // because a large room holds far more enemies than a small one and would otherwise always score Hard.
    [Serializable]
    public struct RoomSizeDifficulty
    {
        [SerializeField] private string profileId;
        [SerializeField, Min(0)] private int minimumRoomEnemies;
        [SerializeField, Min(0)] private int maximumRoomEnemies;
        [SerializeField, Min(0)] private int maximumWaveEnemies;
        [SerializeField, Min(0)] private int maximumWaveRearFiring;
        [SerializeField] private RoomDifficultyTierBand[] tierBands;
        [SerializeField] private FloorDifficultyRange[] floorRanges;

        public RoomSizeDifficulty(string configuredProfileId, int configuredMinimumRoomEnemies,
            int configuredMaximumRoomEnemies, int configuredMaximumWaveEnemies, int configuredMaximumWaveRearFiring,
            RoomDifficultyTierBand[] configuredTierBands, FloorDifficultyRange[] configuredFloorRanges)
        {
            profileId = configuredProfileId;
            minimumRoomEnemies = configuredMinimumRoomEnemies;
            maximumRoomEnemies = configuredMaximumRoomEnemies;
            maximumWaveEnemies = configuredMaximumWaveEnemies;
            maximumWaveRearFiring = configuredMaximumWaveRearFiring;
            tierBands = configuredTierBands ?? Array.Empty<RoomDifficultyTierBand>();
            floorRanges = configuredFloorRanges ?? Array.Empty<FloorDifficultyRange>();
        }

        public string ProfileId => profileId;
        public int MinimumRoomEnemies => minimumRoomEnemies;
        public int MaximumRoomEnemies => maximumRoomEnemies;
        public int MaximumWaveEnemies => maximumWaveEnemies;
        public int MaximumWaveRearFiring => maximumWaveRearFiring;
        public IReadOnlyList<RoomDifficultyTierBand> TierBands => tierBands ?? Array.Empty<RoomDifficultyTierBand>();
        public IReadOnlyList<FloorDifficultyRange> FloorRanges => floorRanges ?? Array.Empty<FloorDifficultyRange>();
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

        // Encounter size limits. Zero means the limit is not configured (content versions before 9).
        [SerializeField, Min(0)] private int minimumRoomEnemies;
        [SerializeField, Min(0)] private int maximumRoomEnemies;
        [SerializeField, Min(0)] private int maximumWaveEnemies;
        [SerializeField, Min(0)] private int maximumWaveRearFiring;

        // Density-0: per room size overrides. A size without an entry uses the table-wide values.
        [SerializeField] private RoomSizeDifficulty[] sizeDifficulties = Array.Empty<RoomSizeDifficulty>();

        // The table-wide limits; a room size with a RoomSizeDifficulty entry uses its own (SizeLimits).
        public int MinimumRoomEnemies => minimumRoomEnemies;
        public int MaximumRoomEnemies => maximumRoomEnemies;
        public int MaximumWaveEnemies => maximumWaveEnemies;
        public int MaximumWaveRearFiring => maximumWaveRearFiring;
        public bool HasEncounterLimits => maximumRoomEnemies > 0;
        public IReadOnlyList<RoomSizeDifficulty> SizeDifficulties =>
            sizeDifficulties ?? Array.Empty<RoomSizeDifficulty>();

        public void ConfigureEncounterLimits(int minimumRoom, int maximumRoom, int maximumWave, int maximumWaveRear)
        {
            minimumRoomEnemies = minimumRoom;
            maximumRoomEnemies = maximumRoom;
            maximumWaveEnemies = maximumWave;
            maximumWaveRearFiring = maximumWaveRear;
        }

        public void ConfigureSizeDifficulties(RoomSizeDifficulty[] configured)
        {
            sizeDifficulties = configured ?? Array.Empty<RoomSizeDifficulty>();
        }

        public bool TryGetSizeDifficulty(string profileId, out RoomSizeDifficulty size)
        {
            foreach (RoomSizeDifficulty candidate in SizeDifficulties)
            {
                if (!string.Equals(candidate.ProfileId, profileId, StringComparison.Ordinal)) continue;
                size = candidate;
                return true;
            }

            size = default;
            return false;
        }

        // The enemy count limits a room of the profile uses. False when neither the size nor the table sets any.
        public bool TryGetEncounterLimits(string profileId, out int minimumRoom, out int maximumRoom,
            out int maximumWave, out int maximumWaveRear)
        {
            if (TryGetSizeDifficulty(profileId, out RoomSizeDifficulty size))
            {
                minimumRoom = size.MinimumRoomEnemies;
                maximumRoom = size.MaximumRoomEnemies;
                maximumWave = size.MaximumWaveEnemies;
                maximumWaveRear = size.MaximumWaveRearFiring;
                return true;
            }

            minimumRoom = minimumRoomEnemies;
            maximumRoom = maximumRoomEnemies;
            maximumWave = maximumWaveEnemies;
            maximumWaveRear = maximumWaveRearFiring;
            return HasEncounterLimits;
        }

        // Every profile the Encounter allows must accept its size.
        public bool TryValidateEncounterSize(EncounterDefinition definition, out string error)
        {
            if (definition == null)
            {
                error = "Encounter size validation requires an Encounter.";
                return false;
            }

            if (definition.AllowedProfiles == null || definition.AllowedProfiles.Count == 0)
                return TryValidateEncounterSize(definition, minimumRoomEnemies, maximumRoomEnemies,
                    maximumWaveEnemies, maximumWaveRearFiring, HasEncounterLimits, out error);

            foreach (RoomProfile profile in definition.AllowedProfiles)
            {
                bool limited = TryGetEncounterLimits(profile != null ? profile.ProfileId : null, out int minimumRoom,
                    out int maximumRoom, out int maximumWave, out int maximumWaveRear);
                if (!TryValidateEncounterSize(definition, minimumRoom, maximumRoom, maximumWave, maximumWaveRear,
                        limited, out error))
                {
                    if (profile != null) error += $" (room size '{profile.ProfileId}')";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryValidateEncounterSize(EncounterDefinition definition, int minimumRoom,
            int maximumRoom, int maximumWave, int maximumWaveRear, bool limited, out string error)
        {
            if (!limited)
            {
                error = null;
                return true;
            }

            int total = 0;
            foreach (EncounterWaveDefinition wave in definition.Waves)
            {
                int waveCount = 0;
                int rearCount = 0;
                foreach (EncounterSpawnRule rule in wave.SpawnRules)
                {
                    waveCount += rule.Count;
                    // A rule counts as rear firing when any candidate can occupy a rear-firing slot.
                    if (rule.Candidates.Any(candidate =>
                            RoomTemplateDefinition.RequiredPlacementRole(candidate.EnemyRole) ==
                            SpawnPointPlacementRole.RearFiring))
                        rearCount += rule.Count;
                }

                if (waveCount > maximumWave || rearCount > maximumWaveRear)
                {
                    error = $"Encounter '{definition.EncounterId}' wave {wave.WaveNumber} has {waveCount} enemies " +
                            $"({rearCount} rear firing), above the {maximumWave} per wave " +
                            $"({maximumWaveRear} rear firing) limit.";
                    return false;
                }

                total += waveCount;
            }

            if (total < minimumRoom || total > maximumRoom)
            {
                error = $"Encounter '{definition.EncounterId}' has {total} enemies, outside the room range " +
                        $"{minimumRoom}~{maximumRoom}.";
                return false;
            }

            error = null;
            return true;
        }

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
            if (!TryValidateBands(tierBands, "Room difficulty table", out error) ||
                !TryValidateFloorRanges(floorRanges, "Room difficulty table", out error))
                return false;

            HashSet<string> sizeIds = new(StringComparer.Ordinal);
            foreach (RoomSizeDifficulty size in SizeDifficulties)
            {
                string owner = $"Room difficulty table size '{size.ProfileId}'";
                if (string.IsNullOrWhiteSpace(size.ProfileId) || !sizeIds.Add(size.ProfileId))
                {
                    error = $"{owner} has no profile ID or is duplicated.";
                    return false;
                }

                if (!TryValidateBands(size.TierBands, owner, out error) ||
                    !TryValidateFloorRanges(size.FloorRanges, owner, out error))
                    return false;
                if (!AreLimitsConsistent(size.MinimumRoomEnemies, size.MaximumRoomEnemies, size.MaximumWaveEnemies,
                        size.MaximumWaveRearFiring))
                {
                    error = $"{owner} has inconsistent Encounter size limits.";
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

            if (HasEncounterLimits && !AreLimitsConsistent(minimumRoomEnemies, maximumRoomEnemies,
                    maximumWaveEnemies, maximumWaveRearFiring))
            {
                error = "Room difficulty table has inconsistent Encounter size limits.";
                return false;
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

        private static bool AreLimitsConsistent(int minimumRoom, int maximumRoom, int maximumWave,
            int maximumWaveRear) =>
            minimumRoom >= 1 && maximumRoom >= minimumRoom && maximumWave >= 1 && maximumWave <= maximumRoom &&
            maximumWaveRear >= 1 && maximumWaveRear <= maximumWave;

        private static bool TryValidateBands(IReadOnlyList<RoomDifficultyTierBand> bands, string owner,
            out string error)
        {
            RoomDifficultyTier[] tiers = (RoomDifficultyTier[])Enum.GetValues(typeof(RoomDifficultyTier));
            if (bands == null || bands.Count != tiers.Length)
            {
                error = $"{owner} needs exactly one band per difficulty tier.";
                return false;
            }

            for (int index = 0; index < bands.Count; index++)
            {
                if (bands[index].Tier != tiers[index] ||
                    (index == 0 ? bands[index].MinimumScore != 0
                        : bands[index].MinimumScore <= bands[index - 1].MinimumScore))
                {
                    error = $"{owner} tier bands must follow tier order, start at 0, and strictly increase.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryValidateFloorRanges(IReadOnlyList<FloorDifficultyRange> ranges, string owner,
            out string error)
        {
            if (ranges == null || ranges.Count == 0)
            {
                error = $"{owner} needs at least one floor range.";
                return false;
            }

            HashSet<int> floors = new();
            foreach (FloorDifficultyRange range in ranges)
            {
                if (range.FloorNumber < 1 || range.MinimumScore < 0 || range.MaximumScore < range.MinimumScore ||
                    !floors.Add(range.FloorNumber))
                {
                    error = $"{owner} has an invalid or duplicated range for floor {range.FloorNumber}.";
                    return false;
                }
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

        // The table-wide bands. Generated rooms are classified with their size's bands (the profile overload).
        public RoomDifficultyTier Classify(int score) => Classify(score, tierBands);

        public RoomDifficultyTier Classify(int score, string profileId) =>
            TryGetSizeDifficulty(profileId, out RoomSizeDifficulty size)
                ? Classify(score, size.TierBands)
                : Classify(score, tierBands);

        private static RoomDifficultyTier Classify(int score, IReadOnlyList<RoomDifficultyTierBand> bands)
        {
            RoomDifficultyTier result = bands[0].Tier;
            foreach (RoomDifficultyTierBand band in bands)
            {
                if (score >= band.MinimumScore) result = band.Tier;
            }

            return result;
        }

        public bool TryGetFloorRange(int floorNumber, out FloorDifficultyRange range) =>
            TryGetFloorRange(floorNumber, floorRanges, out range);

        public bool TryGetFloorRange(int floorNumber, string profileId, out FloorDifficultyRange range) =>
            TryGetSizeDifficulty(profileId, out RoomSizeDifficulty size)
                ? TryGetFloorRange(floorNumber, size.FloorRanges, out range)
                : TryGetFloorRange(floorNumber, floorRanges, out range);

        private static bool TryGetFloorRange(int floorNumber, IReadOnlyList<FloorDifficultyRange> ranges,
            out FloorDifficultyRange range)
        {
            foreach (FloorDifficultyRange candidate in ranges ?? Array.Empty<FloorDifficultyRange>())
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

        // Every candidate regardless of floor.
        public bool TryComputeThreatRange(EncounterDefinition definition, out int minimum, out int maximum,
            out string error) => TryComputeThreatRange(definition, 0, out minimum, out maximum, out error);

        // A positive floorNumber counts only the candidates introduced by that floor (Curve-1).
        public bool TryComputeThreatRange(EncounterDefinition definition, int floorNumber, out int minimum,
            out int maximum, out string error)
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
                        if (floorNumber > 0 && !EncounterEnemyFloorPool.IsAvailable(candidate.EnemyRole, floorNumber))
                            continue;
                        if (!TryGetThreat(candidate.EnemyRole, out int score))
                        {
                            error = $"Encounter '{definition.EncounterId}' uses role '{candidate.EnemyRole}' " +
                                    "without a threat score.";
                            return false;
                        }

                        ruleMinimum = Math.Min(ruleMinimum, score);
                        ruleMaximum = Math.Max(ruleMaximum, score);
                    }

                    if (ruleMaximum == 0)
                    {
                        error = $"Encounter '{definition.EncounterId}' wave {wave.WaveNumber} has a spawn rule " +
                                $"with no enemy introduced by floor {floorNumber}.";
                        return false;
                    }

                    minimum += ruleMinimum * rule.Count;
                    maximum += ruleMaximum * rule.Count;
                }
            }

            error = null;
            return true;
        }

        // Variant-0: an elite counts its threat twice. Variant-1: a champion adds ChampionThreatBonus. This is the
        // extra on top of TryScore, which stays the Encounter's own score.
        public bool TryScoreVariants(IReadOnlyList<ResolvedEncounterSpawn[]> waves, out int extra, out string error)
        {
            extra = 0;
            foreach (ResolvedEncounterSpawn[] wave in waves ?? Array.Empty<ResolvedEncounterSpawn[]>())
            {
                foreach (ResolvedEncounterSpawn spawn in wave)
                {
                    if (TrickalFanGame.Enemy.EnemyVariant.IsChampionKind(spawn.Variant))
                    {
                        extra += TrickalFanGame.Enemy.EnemyVariant.ChampionThreatBonus;
                        continue;
                    }

                    if (spawn.Variant != TrickalFanGame.Enemy.EnemyVariantKind.Elite) continue;
                    if (!TryGetThreat(spawn.Role, out int threat))
                    {
                        error = $"Resolved enemy role '{spawn.Role}' has no threat score.";
                        return false;
                    }

                    extra += threat * (TrickalFanGame.Enemy.EnemyVariant.EliteThreatMultiplier - 1);
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
