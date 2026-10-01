using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [Serializable]
    public sealed class FloorGenerationSettings
    {
        [SerializeField] private int minimumTotalRooms;
        [SerializeField] private int maximumTotalRooms;
        [SerializeField] private int minimumBossDistance;

        public FloorGenerationSettings(int minimum, int maximum, int bossDistance)
        { minimumTotalRooms = minimum; maximumTotalRooms = maximum; minimumBossDistance = bossDistance; }
        public int MinimumTotalRooms => minimumTotalRooms;
        public int MaximumTotalRooms => maximumTotalRooms;
        public int MinimumBossDistance => minimumBossDistance;

        // Reserve one secret room and one shop. The remaining regular rooms must fit the boss route.
        public bool TryValidate(out string error)
        {
            if (minimumTotalRooms < 5 || maximumTotalRooms < minimumTotalRooms || maximumTotalRooms > 99 ||
                minimumBossDistance < 1 || minimumBossDistance > minimumTotalRooms - 3)
            { error = "Floor settings need 5~99 total rooms and a boss route that fits after reserving two special rooms."; return false; }
            error = null; return true;
        }
    }

    public enum GeneratedRoomRole { Start, Intermediate, Treasure, Boss, Secret, Shop }
    public enum RoomDoorDirection { Left, Right, Up, Down }

    public readonly struct RoomGridPosition : IEquatable<RoomGridPosition>
    {
        public RoomGridPosition(int x, int y) { X = x; Y = y; }
        public int X { get; }
        public int Y { get; }
        public RoomGridPosition Offset(RoomDoorDirection direction) => direction switch
        {
            RoomDoorDirection.Left => new RoomGridPosition(X - 1, Y),
            RoomDoorDirection.Right => new RoomGridPosition(X + 1, Y),
            RoomDoorDirection.Up => new RoomGridPosition(X, Y + 1),
            RoomDoorDirection.Down => new RoomGridPosition(X, Y - 1),
            _ => this,
        };
        public bool Equals(RoomGridPosition other) => X == other.X && Y == other.Y;
        public override string ToString() => $"({X}, {Y})";
    }

    public readonly struct GeneratedRoomConnection
    {
        public GeneratedRoomConnection(RoomDoorDirection direction, string destinationRoomId, bool isSecret = false)
        { Direction = direction; DestinationRoomId = destinationRoomId; IsSecret = isSecret; }
        public RoomDoorDirection Direction { get; }
        public string DestinationRoomId { get; }
        // A hidden passage to or from the floor's secret room. It stays a wall until a bomb or an entry opens it,
        // so required-route checks (boss distance, key-lock bypass, difficulty distance) ignore it.
        public bool IsSecret { get; }
    }

    public sealed class GeneratedRoomNode
    {
        private readonly List<GeneratedRoomConnection> connections = new();
        public GeneratedRoomNode(string roomId, int floorNumber, int roomNumber,
            GeneratedRoomRole role, RoomDefinition definition)
            : this(roomId, floorNumber, roomNumber, new RoomGridPosition(roomNumber - 1, 0),
                role, 0, definition) { }
        public GeneratedRoomNode(string roomId, int floorNumber, int roomNumber,
            RoomGridPosition gridPosition, GeneratedRoomRole role, int contentSeed, RoomDefinition definition)
        {
            RoomId = roomId; FloorNumber = floorNumber; RoomNumber = roomNumber;
            GridPosition = gridPosition; Role = role; ContentSeed = contentSeed; Definition = definition;
        }
        public string RoomId { get; }
        public int FloorNumber { get; }
        public int RoomNumber { get; }
        public RoomGridPosition GridPosition { get; }
        public GeneratedRoomRole Role { get; }
        public int ContentSeed { get; }
        public RoomDefinition Definition { get; }
        public RoomTemplateDefinition Template { get; private set; }
        public string TemplateId => Template != null ? Template.TemplateId : null;
        public EncounterDefinition Encounter { get; private set; }
        public string EncounterId => Encounter != null ? Encounter.EncounterId : null;
        public IReadOnlyList<ResolvedEncounterSpawn[]> ResolvedEncounterWaves { get; private set; } =
            Array.Empty<ResolvedEncounterSpawn[]>();
        public bool HasDifficulty { get; private set; }
        public GeneratedRoomDifficulty Difficulty { get; private set; }
        public bool RequiresKey { get; private set; }
        public RoomType RoomType => Definition != null ? Definition.RoomType : RoomType.Normal;
        public IReadOnlyList<GeneratedRoomConnection> DirectionalConnections => connections;
        public IReadOnlyList<string> ConnectedRoomIds
        {
            get
            {
                string[] ids = new string[connections.Count];
                for (int i = 0; i < connections.Count; i++) ids[i] = connections[i].DestinationRoomId;
                return ids;
            }
        }
        public bool TryGetConnection(RoomDoorDirection direction, out GeneratedRoomConnection connection)
        {
            foreach (GeneratedRoomConnection candidate in connections)
                if (candidate.Direction == direction) { connection = candidate; return true; }
            connection = default; return false;
        }
        internal void ConnectTo(RoomDoorDirection direction, string destinationRoomId, bool isSecret = false)
        {
            if (TryGetConnection(direction, out _))
                throw new InvalidOperationException($"Room {RoomId} already has a {direction} connection.");
            connections.Add(new GeneratedRoomConnection(direction, destinationRoomId, isSecret));
        }

        internal void AssignTemplate(RoomTemplateDefinition template)
        {
            Template = template;
        }

        internal void AssignKeyRequirement(bool requiresKey)
        {
            if (requiresKey && Role is not (GeneratedRoomRole.Treasure or GeneratedRoomRole.Shop))
                throw new InvalidOperationException($"Only treasure and shop rooms can require a key: {RoomId}.");
            RequiresKey = requiresKey;
        }

        internal void AssignEncounter(EncounterDefinition encounter,
            ResolvedEncounterSpawn[][] resolvedWaves = null)
        {
            Encounter = encounter;
            ResolvedEncounterWaves = resolvedWaves ?? Array.Empty<ResolvedEncounterSpawn[]>();
            HasDifficulty = false;
            Difficulty = default;
        }

        internal void AssignEncounter(EncounterDefinition encounter, ResolvedEncounterSpawn[][] resolvedWaves,
            GeneratedRoomDifficulty difficulty)
        {
            AssignEncounter(encounter, resolvedWaves);
            HasDifficulty = true;
            Difficulty = difficulty;
        }
    }

    public sealed class GeneratedFloor
    {
        public GeneratedFloor(int floorNumber, int floorSeed, int topologySeed, int contentSeed,
            string startingRoomId, string bossRoomId, GeneratedRoomNode[] nodes,
            FloorGenerationSettings settings = null)
        {
            FloorNumber = floorNumber; FloorSeed = floorSeed; TopologySeed = topologySeed;
            ContentSeed = contentSeed; StartingRoomId = startingRoomId; BossRoomId = bossRoomId;
            Nodes = nodes ?? Array.Empty<GeneratedRoomNode>();
            Settings = settings;
        }
        public int FloorNumber { get; }
        public int FloorSeed { get; }
        public int TopologySeed { get; }
        public int ContentSeed { get; }
        public string StartingRoomId { get; }
        public string BossRoomId { get; }
        public IReadOnlyList<GeneratedRoomNode> Nodes { get; }
        public FloorGenerationSettings Settings { get; }
    }

    public sealed class GeneratedFloorGraph
    {
        public GeneratedFloorGraph(GeneratedRoomNode[] nodes, string startingRoomId)
            : this(new[] { new GeneratedFloor(1, 0, 0, 0, startingRoomId,
                FindBossId(nodes), nodes) }, 1) { }

        public GeneratedFloorGraph(GeneratedFloor[] floors, int minimumBossDistance)
        {
            Floors = floors ?? Array.Empty<GeneratedFloor>(); MinimumBossDistance = minimumBossDistance;
            List<GeneratedRoomNode> nodes = new();
            foreach (GeneratedFloor floor in Floors) nodes.AddRange(floor.Nodes);
            Nodes = nodes; StartingRoomId = Floors.Count > 0 ? Floors[0].StartingRoomId : null;
        }
        public IReadOnlyList<GeneratedFloor> Floors { get; }
        public IReadOnlyList<GeneratedRoomNode> Nodes { get; }
        public string StartingRoomId { get; }
        public int MinimumBossDistance { get; }
        private static string FindBossId(IReadOnlyList<GeneratedRoomNode> nodes)
        {
            if (nodes != null) foreach (GeneratedRoomNode node in nodes)
                if (node != null && node.Role == GeneratedRoomRole.Boss) return node.RoomId;
            return null;
        }
        public GeneratedFloor FindFloor(int number)
        { foreach (GeneratedFloor floor in Floors) if (floor.FloorNumber == number) return floor; return null; }

        public bool TryValidate(out string error)
        {
            if (Floors.Count == 0) { error = "Generated graph has no floors."; return false; }
            HashSet<string> globalIds = new(StringComparer.Ordinal);
            foreach (GeneratedFloor floor in Floors)
                if (!TryValidateFloor(floor, globalIds, out error)) return false;
            error = null; return true;
        }

        private bool TryValidateFloor(GeneratedFloor floor, HashSet<string> globalIds, out string error)
        {
            if (floor.Settings != null)
            {
                if (!floor.Settings.TryValidate(out error)) return false;
                if (floor.Nodes.Count < floor.Settings.MinimumTotalRooms || floor.Nodes.Count > floor.Settings.MaximumTotalRooms)
                { error = $"Floor {floor.FloorNumber} is outside its total room range."; return false; }
            }
            Dictionary<string, GeneratedRoomNode> byId = new(StringComparer.Ordinal);
            List<RoomGridPosition> positions = new(); int starts = 0, bosses = 0, treasures = 0, secrets = 0, shops = 0;
            foreach (GeneratedRoomNode node in floor.Nodes)
            {
                if (node == null || node.FloorNumber != floor.FloorNumber ||
                    string.IsNullOrWhiteSpace(node.RoomId) || !globalIds.Add(node.RoomId) || !byId.TryAdd(node.RoomId, node))
                { error = $"Floor {floor.FloorNumber} has a missing, duplicated, or misplaced room ID '{node?.RoomId}'."; return false; }
                if (positions.Contains(node.GridPosition))
                { error = $"Floor {floor.FloorNumber} duplicates grid coordinate {node.GridPosition}."; return false; }
                positions.Add(node.GridPosition);
                if (node.RoomId != FloorGenerator.BuildRoomId(node.FloorNumber, node.RoomNumber))
                { error = $"Room {node.RoomId} does not match the stable room ID contract."; return false; }
                if (node.Definition == null)
                { error = $"Room {node.RoomId} has a missing definition."; return false; }
                if (!node.Definition.TryValidate(out error))
                { error = $"Room {node.RoomId} has an invalid definition. {error}"; return false; }
                starts += node.Role == GeneratedRoomRole.Start ? 1 : 0;
                bosses += node.Role == GeneratedRoomRole.Boss ? 1 : 0;
                treasures += node.Role == GeneratedRoomRole.Treasure ? 1 : 0;
                secrets += node.Role == GeneratedRoomRole.Secret ? 1 : 0;
                shops += node.Role == GeneratedRoomRole.Shop ? 1 : 0;
                bool validRoleType = node.Role switch
                {
                    GeneratedRoomRole.Start => node.RoomType == RoomType.Normal,
                    GeneratedRoomRole.Intermediate => node.RoomType == RoomType.Normal,
                    GeneratedRoomRole.Treasure => node.RoomType == RoomType.Reward,
                    GeneratedRoomRole.Boss => node.RoomType == RoomType.Boss,
                    GeneratedRoomRole.Secret => node.RoomType == RoomType.Reward,
                    GeneratedRoomRole.Shop => node.RoomType == RoomType.Shop,
                    _ => false,
                };
                if (!validRoleType)
                { error = $"Room {node.RoomId} role {node.Role} does not match definition type {node.RoomType}."; return false; }
                if (node.RequiresKey && node.Role is not (GeneratedRoomRole.Treasure or GeneratedRoomRole.Shop))
                { error = $"Room {node.RoomId} requires a key but is not a treasure or shop room."; return false; }
                if (node.Template != null &&
                    (!node.Template.SupportsRoomType(node.RoomType) ||
                     !node.Template.SupportsConnections(node.DirectionalConnections)))
                { error = $"Room {node.RoomId} has an incompatible template '{node.TemplateId}'."; return false; }
                if (node.Encounter != null &&
                    (node.Role != GeneratedRoomRole.Intermediate || node.Template == null ||
                     !node.Encounter.TryValidateFor(node.Template, node.FloorNumber,
                         node.DirectionalConnections, out error)))
                { error = $"Room {node.RoomId} has an incompatible Encounter '{node.EncounterId}'. {error}"; return false; }
            }
            if (secrets > 1 || shops > 1)
            { error = $"Floor {floor.FloorNumber} has {secrets} secret and {shops} shop rooms; at most one each is allowed."; return false; }
            if (starts != 1 || bosses != 1 || treasures != 1 ||
                !byId.ContainsKey(floor.StartingRoomId) || !byId.ContainsKey(floor.BossRoomId))
            { error = $"Floor {floor.FloorNumber} must have one start, one boss, and one treasure room."; return false; }
            foreach (GeneratedRoomNode node in floor.Nodes)
            {
                HashSet<string> destinations = new(StringComparer.Ordinal); HashSet<RoomDoorDirection> directions = new();
                foreach (GeneratedRoomConnection connection in node.DirectionalConnections)
                {
                    if (!destinations.Add(connection.DestinationRoomId) || !directions.Add(connection.Direction) ||
                        connection.DestinationRoomId == node.RoomId || !byId.TryGetValue(connection.DestinationRoomId, out GeneratedRoomNode destination))
                    { error = $"Room {node.RoomId} has a duplicate, self, or missing connection."; return false; }
                    if (!destination.GridPosition.Equals(node.GridPosition.Offset(connection.Direction)) ||
                        !destination.TryGetConnection(Opposite(connection.Direction), out GeneratedRoomConnection reverse) || reverse.DestinationRoomId != node.RoomId ||
                        reverse.IsSecret != connection.IsSecret)
                    { error = $"Room {node.RoomId} has a direction or reverse-link mismatch at {connection.Direction}."; return false; }
                    bool touchesSecret = node.Role == GeneratedRoomRole.Secret || destination.Role == GeneratedRoomRole.Secret;
                    if (connection.IsSecret != touchesSecret ||
                        (touchesSecret && (node.Role is GeneratedRoomRole.Start or GeneratedRoomRole.Boss ||
                                           destination.Role is GeneratedRoomRole.Start or GeneratedRoomRole.Boss)))
                    { error = $"Room {node.RoomId} has an invalid secret passage at {connection.Direction}."; return false; }
                }
            }
            Dictionary<string, int> distances = Distances(floor.StartingRoomId, byId);
            int secretRooms = 0;
            foreach (GeneratedRoomNode node in floor.Nodes)
            {
                if (node.Role != GeneratedRoomRole.Secret) continue;
                secretRooms++;
                if (node.DirectionalConnections.Count == 0)
                { error = $"Secret room {node.RoomId} needs at least one hidden passage."; return false; }
            }
            // Secret rooms are reachable only through hidden passages; every other room needs a regular route.
            int requiredBossDistance = floor.Settings?.MinimumBossDistance ?? MinimumBossDistance;
            if (distances.Count != floor.Nodes.Count - secretRooms ||!distances.TryGetValue(floor.BossRoomId, out int bossDistance) ||
                bossDistance < requiredBossDistance || byId[floor.BossRoomId].DirectionalConnections.Count != 1)
            { error = $"Floor {floor.FloorNumber} is disconnected or its boss is not an end room at distance {requiredBossDistance}+."; return false; }
            foreach (GeneratedRoomNode node in floor.Nodes)
            {
                if (node.Role == GeneratedRoomRole.Shop && !IsValidShop(node, byId, out error)) return false;
                if (node.RequiresKey && !CanReachWithoutRoomForGeneration(
                        floor.StartingRoomId, floor.BossRoomId, node.RoomId, byId))
                {
                    error = $"Locked treasure room {node.RoomId} blocks the required boss route.";
                    return false;
                }
            }
            error = null; return true;
        }

        // Special-4: the shop is a key-locked end room behind one regular door from the start or an intermediate
        // room, so it never sits on the boss route and no hidden passage can bypass its lock.
        private static bool IsValidShop(GeneratedRoomNode shop, IReadOnlyDictionary<string, GeneratedRoomNode> byId,
            out string error)
        {
            if (!shop.RequiresKey || shop.DirectionalConnections.Count != 1 || shop.DirectionalConnections[0].IsSecret ||
                byId[shop.DirectionalConnections[0].DestinationRoomId].Role is not
                    (GeneratedRoomRole.Start or GeneratedRoomRole.Intermediate))
            {
                error = $"Shop room {shop.RoomId} must be a key-locked end room behind a start or intermediate room.";
                return false;
            }

            error = null;
            return true;
        }

        internal static bool CanReachWithoutRoomForGeneration(string start, string destination, string excluded,
            IReadOnlyDictionary<string, GeneratedRoomNode> byId)
        {
            if (start == excluded || destination == excluded) return false;
            HashSet<string> visited = new(StringComparer.Ordinal) { start };
            Queue<string> queue = new();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (current == destination) return true;
                foreach (GeneratedRoomConnection connection in byId[current].DirectionalConnections)
                {
                    if (!connection.IsSecret && connection.DestinationRoomId != excluded &&
                        visited.Add(connection.DestinationRoomId))
                        queue.Enqueue(connection.DestinationRoomId);
                }
            }
            return false;
        }

        private static Dictionary<string, int> Distances(string start, IReadOnlyDictionary<string, GeneratedRoomNode> byId)
        {
            Dictionary<string, int> result = new(StringComparer.Ordinal) { [start] = 0 }; Queue<string> queue = new(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (GeneratedRoomConnection connection in byId[current].DirectionalConnections)
                    if (!connection.IsSecret && result.TryAdd(connection.DestinationRoomId, result[current] + 1))
                        queue.Enqueue(connection.DestinationRoomId);
            }
            return result;
        }

        public static RoomDoorDirection Opposite(RoomDoorDirection direction) => direction switch
        {
            RoomDoorDirection.Left => RoomDoorDirection.Right, RoomDoorDirection.Right => RoomDoorDirection.Left,
            RoomDoorDirection.Up => RoomDoorDirection.Down, RoomDoorDirection.Down => RoomDoorDirection.Up,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    public sealed class FloorGenerator : MonoBehaviour
    {
        private const uint FloorSalt = 0xA341316Cu, TopologySalt = 0xC8013EA4u,
            ContentSalt = 0xAD90777Du, AttemptSalt = 0x7E95761Eu, TreasureLockSalt = 0x4B455931u,
            SecretRoomSalt = 0x53435254u, ShopRoomSalt = 0x53484F50u, FloorSizeSalt = 0x53495A45u;
        public const int TreasureLockPercent = 50;
        public const int SecretRoomPercent = 50;
        public const int ShopRoomPercent = 60;
        [SerializeField, Min(1)] private int floorCount = 3;
        [SerializeField, Min(3)] private int minimumRoomsPerFloor = 6;
        [SerializeField, Min(3)] private int maximumRoomsPerFloor = 8;
        [SerializeField, Min(1)] private int minimumBossDistance = 3;
        [SerializeField, Min(1)] private int generationRetryLimit = 32;
        [SerializeField] private RoomDefinition[] roomDefinitions = Array.Empty<RoomDefinition>();
        [SerializeField, Min(1)] private int roomContentVersion = 1;
        [SerializeField] private RoomTemplateDefinition[] roomTemplates = Array.Empty<RoomTemplateDefinition>();
        [SerializeField] private EncounterDefinition[] encounterDefinitions = Array.Empty<EncounterDefinition>();
        [SerializeField, Min(1)] private int encounterContentVersion = 1;
        [SerializeField] private RoomDifficultyTable difficultyTable;
        // Empty keeps the legacy regular-room range. Otherwise one entry is required for every active floor.
        [SerializeField] private FloorGenerationSettings[] floorSettings = Array.Empty<FloorGenerationSettings>();
        private int runSeed; private bool hasRunSeed;
        public int RunSeed => runSeed;
        public bool HasRunSeed => hasRunSeed;
        public int FloorCount => floorCount;
        public int MinimumRoomsPerFloor => minimumRoomsPerFloor;
        public int MaximumRoomsPerFloor => maximumRoomsPerFloor;
        public int RoomsPerFloor => minimumRoomsPerFloor == maximumRoomsPerFloor ? minimumRoomsPerFloor : maximumRoomsPerFloor;
        public int MinimumBossDistance => minimumBossDistance;
        public int GenerationRetryLimit => generationRetryLimit;
        public IReadOnlyList<RoomDefinition> RoomDefinitions => roomDefinitions;
        public int RoomContentVersion => roomContentVersion;
        public IReadOnlyList<RoomTemplateDefinition> RoomTemplates => roomTemplates;
        public IReadOnlyList<EncounterDefinition> EncounterDefinitions => encounterDefinitions;
        public int EncounterContentVersion => encounterContentVersion;
        public RoomDifficultyTable DifficultyTable => difficultyTable;
        public IReadOnlyList<FloorGenerationSettings> FloorSettings => floorSettings;

        public void ConfigureFloorSettings(FloorGenerationSettings[] settings)
        { floorSettings = settings == null ? Array.Empty<FloorGenerationSettings>() : (FloorGenerationSettings[])settings.Clone(); }

        public void Configure(int floors, int rooms, RoomDefinition[] definitions) => Configure(floors, rooms, rooms, 2, 32, definitions);
        public void Configure(int floors, int minRooms, int maxRooms, int bossDistance, int retries, RoomDefinition[] definitions)
        { floorCount = floors; minimumRoomsPerFloor = minRooms; maximumRoomsPerFloor = maxRooms;
          minimumBossDistance = bossDistance; generationRetryLimit = retries; roomDefinitions = definitions ?? Array.Empty<RoomDefinition>();
          if (!Application.isPlaying) { runSeed = 0; hasRunSeed = false; } }

        public void ConfigureTemplates(int contentVersion, RoomTemplateDefinition[] templates)
        {
            roomContentVersion = Mathf.Max(1, contentVersion);
            roomTemplates = templates ?? Array.Empty<RoomTemplateDefinition>();
        }

        public void ConfigureEncounters(int contentVersion, EncounterDefinition[] definitions)
        {
            encounterContentVersion = Mathf.Max(1, contentVersion);
            encounterDefinitions = definitions ?? Array.Empty<EncounterDefinition>();
        }

        public void ConfigureDifficulty(RoomDifficultyTable table)
        {
            difficultyTable = table;
        }

        public bool TryInitializeRunSeed(int seed, out string error)
        {
            if (hasRunSeed) { error = runSeed == seed ? null : $"FloorGenerator is already initialized for run seed {runSeed}."; return runSeed == seed; }
            runSeed = seed; hasRunSeed = true; error = null; return true;
        }
        public bool TryGenerate(out GeneratedFloorGraph graph, out string error)
        {
            if (!hasRunSeed) { graph = null; error = "FloorGenerator needs a Run seed before runtime generation."; return false; }
            return TryGenerateForSeed(runSeed, out graph, out error);
        }
        public bool TryGenerateForSeed(int seed, out GeneratedFloorGraph graph, out string error)
        {
            graph = null;
            if (floorCount < 1 || minimumRoomsPerFloor < 3 || maximumRoomsPerFloor < minimumRoomsPerFloor ||
                maximumRoomsPerFloor > 99 || minimumBossDistance < 1 || generationRetryLimit < 1)
            { error = $"Invalid generation settings for seed {seed}: floor count, room range, boss distance, or retry limit."; return false; }
            if (floorSettings.Length != 0)
            {
                if (floorSettings.Length != floorCount)
                { error = "Floor settings must have exactly one entry for every active floor."; return false; }
                for (int i = 0; i < floorSettings.Length; i++)
                {
                    if (floorSettings[i] == null)
                    { error = $"Floor {i + 1} settings are missing."; return false; }
                    if (!floorSettings[i].TryValidate(out error))
                    { error = $"Floor {i + 1}: {error}"; return false; }
                }
            }
            if (!Pools(out List<RoomDefinition> normal, out List<RoomDefinition> reward, out List<RoomDefinition> boss,
                    out List<RoomDefinition> shop, out error)) return false;
            GeneratedFloor[] floors = new GeneratedFloor[floorCount];
            for (int i = 0; i < floorCount; i++)
            {
                int floorSeed = DeriveSeed(seed, i + 1, FloorSalt);
                if (!TryFloor(i + 1, floorSeed, normal, reward, boss, shop, out floors[i], out string failure))
                { error = $"Floor generation failed after {generationRetryLimit} attempts for run seed {seed}, floor seed {floorSeed}, floor {i + 1}. {failure}"; return false; }
            }
            graph = new GeneratedFloorGraph(floors, minimumBossDistance);
            if (!graph.TryValidate(out error)) { error = $"Generated graph validation failed for run seed {seed}. {error}"; graph = null; return false; }
            if (roomTemplates.Length > 0 && !RoomTemplateSelector.TryAssign(
                    graph,
                    roomTemplates,
                    roomContentVersion,
                    new Vector2(RoomLayout.RoomSpacingX, RoomLayout.RoomSpacingY),
                    out error))
            {
                error = $"Room template selection failed for run seed {seed}. {error}";
                graph = null;
                return false;
            }
            if (encounterDefinitions.Length > 0 && !EncounterSelector.TryAssign(
                    graph,
                    encounterDefinitions,
                    encounterContentVersion,
                    difficultyTable,
                    out error))
            {
                error = $"Encounter selection failed for run seed {seed}. {error}";
                graph = null;
                return false;
            }
            if (!graph.TryValidate(out error)) { error = $"Generated graph validation failed for run seed {seed}. {error}"; graph = null; return false; }
            return true;
        }

        public static int DeriveSeed(int parentSeed, int discriminator, uint salt)
        {
            uint value = unchecked((uint)parentSeed) ^ salt ^ unchecked((uint)discriminator) * 0x9E3779B9u;
            value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15; value *= 0x846CA68Bu; value ^= value >> 16;
            return unchecked((int)value);
        }
        public static string BuildRoomId(int floor, int room)
        {
            if (floor < 1 || room < 1) throw new ArgumentOutOfRangeException(nameof(floor), "Floor and room must be positive.");
            return $"floor-{floor:00}-room-{room:00}";
        }

        private bool TryFloor(int floorNumber, int floorSeed, IReadOnlyList<RoomDefinition> normal,
            IReadOnlyList<RoomDefinition> reward, IReadOnlyList<RoomDefinition> boss, IReadOnlyList<RoomDefinition> shop,
            out GeneratedFloor floor, out string error)
        {
            int topologySeed = DeriveSeed(floorSeed, 0, TopologySalt), contentSeed = DeriveSeed(floorSeed, 0, ContentSalt);
            FloorGenerationSettings settings = floorSettings.Length == 0 ? null : floorSettings[floorNumber - 1];
            int requiredBossDistance = settings?.MinimumBossDistance ?? minimumBossDistance;
            int totalRooms = 0, reservedSpecialRooms = 0;
            if (settings != null)
            {
                StableRandom sizeRandom = new(unchecked((uint)DeriveSeed(floorSeed, 0, FloorSizeSalt)));
                totalRooms = sizeRandom.NextInclusive(settings.MinimumTotalRooms, settings.MaximumTotalRooms);
                reservedSpecialRooms = (RollSpecialRoom(contentSeed, SecretRoomSalt, SecretRoomPercent) ? 1 : 0) +
                    (shop.Count > 0 && RollSpecialRoom(contentSeed, ShopRoomSalt, ShopRoomPercent) ? 1 : 0);
            }
            for (int attempt = 0; attempt < generationRetryLimit; attempt++)
            {
                StableRandom random = new(unchecked((uint)DeriveSeed(topologySeed, attempt, AttemptSalt)));
                List<MutableRoom> rooms = settings == null
                    ? Grow(random.NextInclusive(minimumRoomsPerFloor, maximumRoomsPerFloor), ref random)
                    : GrowWithBossRoute(totalRooms - reservedSpecialRooms, requiredBossDistance, ref random);
                if (rooms == null) continue;
                Dictionary<int, int> distance = MutableDistances(rooms);
                int bossIndex = BossIndex(rooms, distance, requiredBossDistance, ref random);
                if (bossIndex < 0) continue;
                int treasureIndex = TreasureIndex(rooms, distance, bossIndex, ref random);
                if (treasureIndex < 0) continue;
                GeneratedRoomNode[] nodes = new GeneratedRoomNode[rooms.Count];
                for (int i = 0; i < rooms.Count; i++)
                {
                    GeneratedRoomRole role = i == 0 ? GeneratedRoomRole.Start : i == bossIndex ? GeneratedRoomRole.Boss :
                        i == treasureIndex ? GeneratedRoomRole.Treasure : GeneratedRoomRole.Intermediate;
                    IReadOnlyList<RoomDefinition> pool = role == GeneratedRoomRole.Boss ? boss : role == GeneratedRoomRole.Treasure ? reward : normal;
                    int roomSeed = DeriveSeed(contentSeed, i + 1, ContentSalt); StableRandom contentRandom = new(unchecked((uint)roomSeed));
                    nodes[i] = new GeneratedRoomNode(BuildRoomId(floorNumber, i + 1), floorNumber, i + 1,
                        rooms[i].Position, role, roomSeed, pool[contentRandom.NextIndex(pool.Count)]);
                }
                for (int i = 0; i < rooms.Count; i++) foreach (MutableConnection c in rooms[i].Connections)
                    nodes[i].ConnectTo(c.Direction, nodes[c.Destination].RoomId);
                Dictionary<string, GeneratedRoomNode> byId = new(StringComparer.Ordinal);
                foreach (GeneratedRoomNode node in nodes) byId.Add(node.RoomId, node);
                GeneratedRoomNode treasure = nodes[treasureIndex];
                bool optionalRoute = GeneratedFloorGraph.CanReachWithoutRoomForGeneration(
                    nodes[0].RoomId, nodes[bossIndex].RoomId, treasure.RoomId, byId);
                uint lockRoll = unchecked((uint)DeriveSeed(contentSeed, treasure.RoomNumber, TreasureLockSalt));
                treasure.AssignKeyRequirement(optionalRoute && lockRoll % 100u < TreasureLockPercent);
                nodes = AppendSecretRoom(floorNumber, contentSeed, nodes, reward);
                nodes = AppendShopRoom(floorNumber, contentSeed, nodes, shop);
                // Retry topology if a reserved special room had no valid cell; never change the size or special rolls.
                if (settings != null && nodes.Length != totalRooms) continue;
                floor = new GeneratedFloor(floorNumber, floorSeed, topologySeed, contentSeed,
                    nodes[0].RoomId, nodes[bossIndex].RoomId, nodes, settings); error = null; return true;
            }
            floor = null; error = $"Could not place an end-room boss at distance {requiredBossDistance}+, a separate treasure room, and the reserved special rooms."; return false;
        }

        private static bool RollSpecialRoom(int contentSeed, uint salt, int percent)
        {
            StableRandom random = new(unchecked((uint)DeriveSeed(contentSeed, 0, salt)));
            return random.NextIndex(100) < percent;
        }

        // Special-3: an independent floor-content roll adds at most one secret room after the regular rooms, so it
        // never changes existing room numbers, roles, positions, or seeds. It takes the empty grid cell touching the
        // most rooms (ties by seed) that is not next to the start or boss room, and links every touching room through
        // a hidden passage. A Basic 16x9 room fits beside every profile on the 20x13 grid, so template selection
        // cannot run out of space for it.
        private static GeneratedRoomNode[] AppendSecretRoom(int floorNumber, int contentSeed, GeneratedRoomNode[] nodes,
            IReadOnlyList<RoomDefinition> reward)
        {
            StableRandom random = new(unchecked((uint)DeriveSeed(contentSeed, 0, SecretRoomSalt)));
            if (random.NextIndex(100) >= SecretRoomPercent) return nodes;

            Dictionary<RoomGridPosition, GeneratedRoomNode> byPosition = new();
            foreach (GeneratedRoomNode node in nodes) byPosition.Add(node.GridPosition, node);
            RoomDoorDirection[] directions =
                { RoomDoorDirection.Left, RoomDoorDirection.Right, RoomDoorDirection.Up, RoomDoorDirection.Down };
            List<RoomGridPosition> best = new();
            HashSet<RoomGridPosition> considered = new();
            int bestNeighbors = 0;
            foreach (GeneratedRoomNode node in nodes)
            {
                foreach (RoomDoorDirection outward in directions)
                {
                    RoomGridPosition cell = node.GridPosition.Offset(outward);
                    if (byPosition.ContainsKey(cell) || !considered.Add(cell)) continue;
                    int neighbors = 0;
                    bool forbidden = false;
                    foreach (RoomDoorDirection direction in directions)
                    {
                        if (!byPosition.TryGetValue(cell.Offset(direction), out GeneratedRoomNode neighbor)) continue;
                        neighbors++;
                        forbidden |= neighbor.Role is GeneratedRoomRole.Start or GeneratedRoomRole.Boss;
                    }

                    if (forbidden || neighbors < bestNeighbors) continue;
                    if (neighbors > bestNeighbors) { bestNeighbors = neighbors; best.Clear(); }
                    best.Add(cell);
                }
            }
            if (best.Count == 0) return nodes;

            RoomGridPosition position = best[random.NextIndex(best.Count)];
            int roomNumber = nodes.Length + 1;
            GeneratedRoomNode secret = new(BuildRoomId(floorNumber, roomNumber), floorNumber, roomNumber, position,
                GeneratedRoomRole.Secret, DeriveSeed(contentSeed, roomNumber, ContentSalt),
                reward[random.NextIndex(reward.Count)]);
            foreach (RoomDoorDirection direction in directions)
            {
                if (!byPosition.TryGetValue(position.Offset(direction), out GeneratedRoomNode neighbor)) continue;
                secret.ConnectTo(direction, neighbor.RoomId, true);
                neighbor.ConnectTo(GeneratedFloorGraph.Opposite(direction), secret.RoomId, true);
            }

            GeneratedRoomNode[] result = new GeneratedRoomNode[nodes.Length + 1];
            Array.Copy(nodes, result, nodes.Length);
            result[nodes.Length] = secret;
            return result;
        }

        // Special-4: an independent floor-content roll adds at most one shop after the secret room, so it changes no
        // existing room number, role, position, seed, or hidden passage. It hangs off the start or an intermediate
        // room as a key-locked end room on an empty cell; the secret room was placed before it and never links to it.
        // Without a shop Room Definition (older verification configurations) floors simply have no shop.
        private static GeneratedRoomNode[] AppendShopRoom(int floorNumber, int contentSeed, GeneratedRoomNode[] nodes,
            IReadOnlyList<RoomDefinition> shop)
        {
            if (shop.Count == 0) return nodes;
            StableRandom random = new(unchecked((uint)DeriveSeed(contentSeed, 0, ShopRoomSalt)));
            if (random.NextIndex(100) >= ShopRoomPercent) return nodes;

            HashSet<RoomGridPosition> occupied = new();
            foreach (GeneratedRoomNode node in nodes) occupied.Add(node.GridPosition);
            RoomDoorDirection[] directions =
                { RoomDoorDirection.Left, RoomDoorDirection.Right, RoomDoorDirection.Up, RoomDoorDirection.Down };
            List<(GeneratedRoomNode Parent, RoomDoorDirection Direction)> candidates = new();
            foreach (GeneratedRoomNode node in nodes)
            {
                if (node.Role is not (GeneratedRoomRole.Start or GeneratedRoomRole.Intermediate)) continue;
                foreach (RoomDoorDirection direction in directions)
                    if (!occupied.Contains(node.GridPosition.Offset(direction))) candidates.Add((node, direction));
            }
            if (candidates.Count == 0) return nodes;

            (GeneratedRoomNode parent, RoomDoorDirection outward) = candidates[random.NextIndex(candidates.Count)];
            int roomNumber = nodes.Length + 1;
            GeneratedRoomNode shopRoom = new(BuildRoomId(floorNumber, roomNumber), floorNumber, roomNumber,
                parent.GridPosition.Offset(outward), GeneratedRoomRole.Shop,
                DeriveSeed(contentSeed, roomNumber, ContentSalt), shop[random.NextIndex(shop.Count)]);
            shopRoom.AssignKeyRequirement(true);
            parent.ConnectTo(outward, shopRoom.RoomId);
            shopRoom.ConnectTo(GeneratedFloorGraph.Opposite(outward), parent.RoomId);

            GeneratedRoomNode[] result = new GeneratedRoomNode[nodes.Length + 1];
            Array.Copy(nodes, result, nodes.Length);
            result[nodes.Length] = shopRoom;
            return result;
        }

        private static List<MutableRoom> Grow(int count, ref StableRandom random)
        {
            List<MutableRoom> rooms = new() { new MutableRoom(new RoomGridPosition(0, 0)) };
            List<RoomGridPosition> occupied = new() { rooms[0].Position };
            RoomDoorDirection[] directions = { RoomDoorDirection.Left, RoomDoorDirection.Right, RoomDoorDirection.Up, RoomDoorDirection.Down };
            int remainingSteps = count * 64;
            while (rooms.Count < count && remainingSteps-- > 0)
            {
                int source = random.NextIndex(rooms.Count), firstDirection = random.NextIndex(4);
                for (int offset = 0; offset < 4; offset++)
                {
                    RoomDoorDirection direction = directions[(firstDirection + offset) % 4];
                    RoomGridPosition position = rooms[source].Position.Offset(direction);
                    if (occupied.Contains(position)) continue;
                    occupied.Add(position);
                    int destination = rooms.Count; MutableRoom added = new(position); rooms.Add(added);
                    rooms[source].Connections.Add(new MutableConnection(direction, destination));
                    added.Connections.Add(new MutableConnection(GeneratedFloorGraph.Opposite(direction), source)); break;
                }
            }
            return rooms.Count == count ? rooms : null;
        }
        // Build a self-avoiding required route first, then branch without extending its protected end room.
        // This prevents small floors with a long minimum route from failing through unlucky shallow growth.
        private static List<MutableRoom> GrowWithBossRoute(int count, int bossDistance, ref StableRandom random)
        {
            List<MutableRoom> rooms = new() { new MutableRoom(new RoomGridPosition(0, 0)) };
            HashSet<RoomGridPosition> occupied = new() { rooms[0].Position };
            RoomDoorDirection[] directions = { RoomDoorDirection.Left, RoomDoorDirection.Right, RoomDoorDirection.Up, RoomDoorDirection.Down };
            while (rooms.Count <= bossDistance)
            {
                int source = rooms.Count - 1;
                int first = random.NextIndex(4);
                bool added = false;
                for (int offset = 0; offset < 4; offset++)
                {
                    RoomDoorDirection direction = directions[(first + offset) % 4];
                    RoomGridPosition position = rooms[source].Position.Offset(direction);
                    if (!occupied.Add(position)) continue;
                    AddRoom(rooms, source, direction, position);
                    added = true;
                    break;
                }
                if (!added) return null;
            }
            int protectedEnd = bossDistance;
            while (rooms.Count < count)
            {
                List<(int Source, RoomDoorDirection Direction, RoomGridPosition Position)> candidates = new();
                for (int source = 0; source < rooms.Count; source++)
                {
                    if (source == protectedEnd) continue;
                    foreach (RoomDoorDirection direction in directions)
                    {
                        RoomGridPosition position = rooms[source].Position.Offset(direction);
                        if (!occupied.Contains(position)) candidates.Add((source, direction, position));
                    }
                }
                if (candidates.Count == 0) return null;
                var chosen = candidates[random.NextIndex(candidates.Count)];
                occupied.Add(chosen.Position);
                AddRoom(rooms, chosen.Source, chosen.Direction, chosen.Position);
            }
            return rooms;
        }

        private static void AddRoom(List<MutableRoom> rooms, int source, RoomDoorDirection direction, RoomGridPosition position)
        {
            int destination = rooms.Count;
            MutableRoom added = new(position);
            rooms.Add(added);
            rooms[source].Connections.Add(new MutableConnection(direction, destination));
            added.Connections.Add(new MutableConnection(GeneratedFloorGraph.Opposite(direction), source));
        }

        private static int BossIndex(IReadOnlyList<MutableRoom> rooms, IReadOnlyDictionary<int, int> distances,
            int minimumBossDistance, ref StableRandom random)
        {
            List<int> choices = new(); int greatest = minimumBossDistance - 1;
            for (int i = 1; i < rooms.Count; i++)
            {
                if (rooms[i].Connections.Count != 1 || distances[i] < minimumBossDistance) continue;
                if (distances[i] > greatest) { greatest = distances[i]; choices.Clear(); }
                if (distances[i] == greatest) choices.Add(i);
            }
            return choices.Count == 0 ? -1 : choices[random.NextIndex(choices.Count)];
        }
        private static int TreasureIndex(IReadOnlyList<MutableRoom> rooms, IReadOnlyDictionary<int, int> distances,
            int bossIndex, ref StableRandom random)
        {
            List<int> ends = new(), fallback = new(); int farthest = -1;
            for (int i = 1; i < rooms.Count; i++)
            {
                if (i == bossIndex) continue; fallback.Add(i);
                if (rooms[i].Connections.Count != 1) continue;
                if (distances[i] > farthest) { farthest = distances[i]; ends.Clear(); }
                if (distances[i] == farthest) ends.Add(i);
            }
            IReadOnlyList<int> choices = ends.Count > 0 ? ends : fallback;
            return choices.Count == 0 ? -1 : choices[random.NextIndex(choices.Count)];
        }
        private static Dictionary<int, int> MutableDistances(IReadOnlyList<MutableRoom> rooms)
        {
            Dictionary<int, int> result = new() { [0] = 0 }; Queue<int> queue = new(); queue.Enqueue(0);
            while (queue.Count > 0) { int current = queue.Dequeue(); foreach (MutableConnection c in rooms[current].Connections)
                if (result.TryAdd(c.Destination, result[current] + 1)) queue.Enqueue(c.Destination); }
            return result;
        }
        private bool Pools(out List<RoomDefinition> normal, out List<RoomDefinition> reward,
            out List<RoomDefinition> boss, out List<RoomDefinition> shop, out string error)
        {
            normal = new(); reward = new(); boss = new(); shop = new(); HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (RoomDefinition definition in roomDefinitions)
            {
                if (definition == null) { error = "FloorGenerator has a missing room definition."; return false; }
                if (!definition.TryValidate(out error)) { error = $"FloorGenerator has an invalid room definition. {error}"; return false; }
                if (!ids.Add(definition.RoomDefinitionId)) { error = $"Room definition ID '{definition.RoomDefinitionId}' is duplicated."; return false; }
                if (definition.RoomType == RoomType.Normal) normal.Add(definition);
                else if (definition.RoomType == RoomType.Reward) reward.Add(definition); else if (definition.RoomType == RoomType.Boss) boss.Add(definition);
                else if (definition.RoomType == RoomType.Shop) shop.Add(definition);
            }
            if (normal.Count == 0 || reward.Count == 0 || boss.Count == 0)
            { error = "FloorGenerator needs at least one normal, reward, and boss room definition."; return false; }
            error = null; return true;
        }

        private sealed class MutableRoom
        { public MutableRoom(RoomGridPosition position) { Position = position; } public RoomGridPosition Position { get; }
          public List<MutableConnection> Connections { get; } = new(); }
        private readonly struct MutableConnection
        { public MutableConnection(RoomDoorDirection direction, int destination) { Direction = direction; Destination = destination; }
          public RoomDoorDirection Direction { get; } public int Destination { get; } }
        private struct StableRandom
        {
            private uint state; public StableRandom(uint seed) { state = seed == 0 ? 0x6D2B79F5u : seed; }
            public int NextIndex(int count) { if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
                state ^= state << 13; state ^= state >> 17; state ^= state << 5; return (int)(state % (uint)count); }
            public int NextInclusive(int minimum, int maximum) => minimum + NextIndex(maximum - minimum + 1);
        }
    }
}
