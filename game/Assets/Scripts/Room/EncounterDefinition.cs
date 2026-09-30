using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Existing numeric values are serialized in Encounter assets. Append variants only.
    public enum EncounterEnemyRole
    {
        Chaser,
        Ranged,
        Charging,
        Boss,
        FastChaser,
        Sniper,
    }
    public enum EncounterWaveStartCondition { RoomEntered, PreviousWaveCleared }
    public enum EncounterWaveCompletionCondition { AllRequiredEnemiesDefeated }
    public enum EncounterClearCondition { AllWavesCleared }

    public readonly struct ResolvedEncounterSpawn
    {
        public ResolvedEncounterSpawn(EncounterEnemyRole role, int spawnPointIndex)
        {
            Role = role;
            SpawnPointIndex = spawnPointIndex;
        }

        public EncounterEnemyRole Role { get; }
        public int SpawnPointIndex { get; }
    }

    [Serializable]
    public struct EncounterSpawnRule
    {
        [SerializeField] private EncounterEnemyRole enemyRole;
        [SerializeField, Min(1)] private int count;
        [SerializeField] private string spawnPointId;
        [SerializeField] private string spawnGroupId;

        public EncounterSpawnRule(EncounterEnemyRole configuredRole, int configuredCount,
            string configuredSpawnPointId, string configuredSpawnGroupId)
        {
            enemyRole = configuredRole;
            count = configuredCount;
            spawnPointId = configuredSpawnPointId;
            spawnGroupId = configuredSpawnGroupId;
        }

        public EncounterEnemyRole EnemyRole => enemyRole;
        public int Count => count;
        public string SpawnPointId => spawnPointId;
        public string SpawnGroupId => spawnGroupId;
    }

    [Serializable]
    public struct EncounterWaveDefinition
    {
        [SerializeField, Min(1)] private int waveNumber;
        [SerializeField] private EncounterWaveStartCondition startCondition;
        [SerializeField] private EncounterWaveCompletionCondition completionCondition;
        [SerializeField] private EncounterSpawnRule[] spawnRules;

        public EncounterWaveDefinition(int configuredWaveNumber,
            EncounterWaveStartCondition configuredStartCondition,
            EncounterWaveCompletionCondition configuredCompletionCondition,
            EncounterSpawnRule[] configuredSpawnRules)
        {
            waveNumber = configuredWaveNumber;
            startCondition = configuredStartCondition;
            completionCondition = configuredCompletionCondition;
            spawnRules = configuredSpawnRules ?? Array.Empty<EncounterSpawnRule>();
        }

        public int WaveNumber => waveNumber;
        public EncounterWaveStartCondition StartCondition => startCondition;
        public EncounterWaveCompletionCondition CompletionCondition => completionCondition;
        public IReadOnlyList<EncounterSpawnRule> SpawnRules => spawnRules ?? Array.Empty<EncounterSpawnRule>();
    }

    [CreateAssetMenu(fileName = "Encounter", menuName = "Trickal Fan Game/Encounter Definition")]
    public sealed class EncounterDefinition : ScriptableObject
    {
        [SerializeField] private string encounterId;
        [SerializeField] private RoomProfile[] allowedProfiles = Array.Empty<RoomProfile>();
        [SerializeField, Min(1)] private int minimumFloor = 1;
        [SerializeField, Min(1)] private int maximumFloor = 3;
        [SerializeField, Min(0f)] private float minimumPlayerDistance = 1.5f;
        [SerializeField, Min(0f)] private float minimumDoorDistance = 2f;
        [SerializeField] private EncounterWaveDefinition[] waves = Array.Empty<EncounterWaveDefinition>();
        [SerializeField] private EncounterClearCondition clearCondition = EncounterClearCondition.AllWavesCleared;

        public string EncounterId => encounterId;
        public IReadOnlyList<RoomProfile> AllowedProfiles => allowedProfiles;
        public int MinimumFloor => minimumFloor;
        public int MaximumFloor => maximumFloor;
        public float MinimumPlayerDistance => minimumPlayerDistance;
        public float MinimumDoorDistance => minimumDoorDistance;
        public IReadOnlyList<EncounterWaveDefinition> Waves => waves;
        public EncounterClearCondition ClearCondition => clearCondition;

        public void Configure(string configuredId, RoomProfile[] configuredProfiles, int configuredMinimumFloor,
            int configuredMaximumFloor, float configuredMinimumPlayerDistance, float configuredMinimumDoorDistance,
            EncounterWaveDefinition[] configuredWaves, EncounterClearCondition configuredClearCondition)
        {
            encounterId = configuredId;
            allowedProfiles = configuredProfiles ?? Array.Empty<RoomProfile>();
            minimumFloor = configuredMinimumFloor;
            maximumFloor = configuredMaximumFloor;
            minimumPlayerDistance = configuredMinimumPlayerDistance;
            minimumDoorDistance = configuredMinimumDoorDistance;
            waves = configuredWaves ?? Array.Empty<EncounterWaveDefinition>();
            clearCondition = configuredClearCondition;
        }

        public bool Supports(RoomProfile profile, int floorNumber) =>
            profile != null && floorNumber >= minimumFloor && floorNumber <= maximumFloor &&
            allowedProfiles != null && allowedProfiles.Any(allowed => allowed != null &&
                string.Equals(allowed.ProfileId, profile.ProfileId, StringComparison.Ordinal));

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(encounterId, "Encounter", out error)) return false;
            if (allowedProfiles == null || allowedProfiles.Length == 0)
            { error = $"Encounter '{encounterId}' needs at least one allowed Room Profile."; return false; }
            HashSet<string> profileIds = new(StringComparer.Ordinal);
            foreach (RoomProfile profile in allowedProfiles)
            {
                if (profile == null || !profile.TryValidate(out error) || !profileIds.Add(profile.ProfileId))
                { error = $"Encounter '{encounterId}' has a missing, invalid, or duplicated Room Profile."; return false; }
            }
            if (minimumFloor < 1 || maximumFloor < minimumFloor)
            { error = $"Encounter '{encounterId}' has an invalid floor range."; return false; }
            if (minimumPlayerDistance < 0f || minimumDoorDistance < 0f)
            { error = $"Encounter '{encounterId}' safety distances cannot be negative."; return false; }
            if (waves == null || waves.Length == 0)
            { error = $"Encounter '{encounterId}' needs at least one wave."; return false; }
            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                EncounterWaveDefinition wave = waves[waveIndex];
                if (wave.WaveNumber != waveIndex + 1 ||
                    wave.StartCondition != (waveIndex == 0
                        ? EncounterWaveStartCondition.RoomEntered
                        : EncounterWaveStartCondition.PreviousWaveCleared) ||
                    wave.CompletionCondition != EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated ||
                    wave.SpawnRules.Count == 0)
                { error = $"Encounter '{encounterId}' wave {waveIndex + 1} has invalid order or conditions."; return false; }
                foreach (EncounterSpawnRule rule in wave.SpawnRules)
                {
                    bool point = !string.IsNullOrWhiteSpace(rule.SpawnPointId);
                    bool group = !string.IsNullOrWhiteSpace(rule.SpawnGroupId);
                    if (rule.Count < 1 || point == group ||
                        (point && !StableRoomId.TryValidate(rule.SpawnPointId, "SpawnPoint", out _)) ||
                        (group && !StableRoomId.TryValidate(rule.SpawnGroupId, "SpawnGroup", out _)))
                    { error = $"Encounter '{encounterId}' has an invalid spawn rule in wave {wave.WaveNumber}."; return false; }
                }
            }
            error = null;
            return true;
        }

        public bool TryValidateFor(RoomTemplateDefinition template, int floorNumber, out string error)
        {
            return TryValidateFor(template, floorNumber, null, out error);
        }

        public bool TryValidateFor(RoomTemplateDefinition template, int floorNumber,
            IReadOnlyList<GeneratedRoomConnection> connections, out string error)
        {
            if (!TryValidate(out error)) return false;
            if (template == null || template.Profile == null || !Supports(template.Profile, floorNumber))
            { error = $"Encounter '{encounterId}' does not support the selected Room Profile or floor {floorNumber}."; return false; }
            if (!template.TryValidate(out error))
            { error = $"Encounter '{encounterId}' received an invalid Room Template. {error}"; return false; }

            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                if (!TryResolveWave(template, floorNumber, connections, waveIndex, out _, out error)) return false;
            }
            error = null;
            return true;
        }

        public bool TryResolveWave(RoomTemplateDefinition template, int floorNumber,
            IReadOnlyList<GeneratedRoomConnection> connections, int waveIndex,
            out ResolvedEncounterSpawn[] resolved, out string error)
        {
            resolved = Array.Empty<ResolvedEncounterSpawn>();
            if (!TryValidate(out error)) return false;
            if (template == null || template.Profile == null || !Supports(template.Profile, floorNumber))
            { error = $"Encounter '{encounterId}' does not support the selected Room Profile or floor {floorNumber}."; return false; }
            if (!template.TryValidate(out error))
            { error = $"Encounter '{encounterId}' received an invalid Room Template. {error}"; return false; }
            if (waveIndex < 0 || waveIndex >= waves.Length)
            { error = $"Encounter '{encounterId}' has no wave index {waveIndex}."; return false; }

            List<RoomTemplateDoor> activeDoors = ResolveActiveDoors(template, connections);
            HashSet<int> occupied = new();
            List<ResolvedEncounterSpawn> result = new();
            EncounterWaveDefinition wave = waves[waveIndex];
            foreach (EncounterSpawnRule rule in wave.SpawnRules)
            {
                int[] candidates;
                if (!string.IsNullOrWhiteSpace(rule.SpawnPointId))
                {
                    if (!template.TryResolveSpawnReference(rule.SpawnPointId, null, rule.Count,
                            out candidates, out error))
                    { error = $"Encounter '{encounterId}' wave {wave.WaveNumber} cannot resolve a spawn reference. {error}"; return false; }
                }
                else
                {
                    if (!template.TryResolveSpawnReference(null, rule.SpawnGroupId,
                            template.SpawnPoints.Count, out candidates, out error))
                    { error = $"Encounter '{encounterId}' wave {wave.WaveNumber} cannot resolve a spawn group. {error}"; return false; }
                }

                IEnumerable<int> available = candidates.Where(index => !occupied.Contains(index) &&
                    IsSafe(template.SpawnPoints[index], activeDoors));
                bool prefersCloseSpawn = rule.EnemyRole == EncounterEnemyRole.Chaser ||
                                         rule.EnemyRole == EncounterEnemyRole.FastChaser;
                available = prefersCloseSpawn
                    ? available.OrderBy(index => template.SpawnPoints[index].sqrMagnitude).ThenBy(index => index)
                    : available.OrderByDescending(index => template.SpawnPoints[index].sqrMagnitude).ThenBy(index => index);
                int[] selected = available.Take(rule.Count).ToArray();
                if (selected.Length != rule.Count)
                {
                    error = $"Encounter '{encounterId}' wave {wave.WaveNumber} cannot place {rule.Count} " +
                            $"{rule.EnemyRole} enemies without reusing a SpawnPoint or violating entry/door safety distance.";
                    return false;
                }

                foreach (int index in selected)
                {
                    occupied.Add(index);
                    result.Add(new ResolvedEncounterSpawn(rule.EnemyRole, index));
                }
            }

            resolved = result.ToArray();
            error = null;
            return true;
        }

        private bool IsSafe(Vector2 point, IReadOnlyList<RoomTemplateDoor> doors)
        {
            foreach (RoomTemplateDoor door in doors)
                if (Vector2.Distance(point, door.SafeEntryPosition) < minimumPlayerDistance ||
                    Vector2.Distance(point, door.SlotPosition) < minimumDoorDistance)
                    return false;
            return true;
        }

        private static List<RoomTemplateDoor> ResolveActiveDoors(RoomTemplateDefinition template,
            IReadOnlyList<GeneratedRoomConnection> connections)
        {
            if (connections == null) return template.DoorSlots.ToList();
            HashSet<RoomDoorDirection> directions = new(connections.Select(connection => connection.Direction));
            return template.DoorSlots.Where(door => directions.Contains(door.Direction)).ToList();
        }
    }

    public static class EncounterContractCatalog
    {
        public static bool TryValidate(IReadOnlyList<EncounterDefinition> definitions, out string error)
        {
            if (definitions == null || definitions.Count == 0)
            { error = "Encounter catalog requires at least one definition."; return false; }
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (EncounterDefinition definition in definitions)
            {
                if (definition == null)
                { error = "Encounter catalog contains a missing definition."; return false; }
                if (!definition.TryValidate(out error))
                { error = $"Encounter catalog contains an invalid definition. {error}"; return false; }
                if (!ids.Add(definition.EncounterId))
                { error = $"Encounter catalog duplicates Encounter ID '{definition.EncounterId}'."; return false; }
            }
            error = null;
            return true;
        }
    }

    public static class EncounterSelector
    {
        private const uint EncounterSalt = 0x68E31DA4u;

        public static bool TryAssign(GeneratedFloorGraph graph,
            IReadOnlyList<EncounterDefinition> definitions, int contentVersion, out string error)
        {
            if (graph == null || contentVersion < 1)
            { error = "Encounter selection requires a graph and positive content version."; return false; }
            if (!EncounterContractCatalog.TryValidate(definitions, out error)) return false;
            EncounterDefinition[] ordered = definitions.OrderBy(definition => definition.EncounterId,
                StringComparer.Ordinal).ToArray();
            foreach (GeneratedRoomNode node in graph.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate))
            {
                if (node.Template == null)
                { error = $"Room {node.RoomId} requires a selected Room Template before Encounter selection."; return false; }
                List<EncounterDefinition> candidates = new();
                string firstRejection = null;
                foreach (EncounterDefinition definition in ordered)
                {
                    if (definition.TryValidateFor(node.Template, node.FloorNumber,
                            node.DirectionalConnections, out string rejection))
                        candidates.Add(definition);
                    else if (firstRejection == null)
                        firstRejection = rejection;
                }
                if (candidates.Count == 0)
                { error = $"Room {node.RoomId} has no Encounter compatible with profile '{node.Template.Profile.ProfileId}' and floor {node.FloorNumber}. First rejection: {firstRejection}"; return false; }
                int seed = FloorGenerator.DeriveSeed(node.ContentSeed, contentVersion, EncounterSalt);
                node.AssignEncounter(candidates[(int)(unchecked((uint)seed) % (uint)candidates.Count)]);
            }
            error = null;
            return true;
        }
    }
}
