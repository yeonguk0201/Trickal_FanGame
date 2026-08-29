using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public enum GeneratedRoomRole { Start, Intermediate, Treasure, Boss }
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
        public GeneratedRoomConnection(RoomDoorDirection direction, string destinationRoomId)
        { Direction = direction; DestinationRoomId = destinationRoomId; }
        public RoomDoorDirection Direction { get; }
        public string DestinationRoomId { get; }
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
        internal void ConnectTo(RoomDoorDirection direction, string destinationRoomId)
        {
            if (TryGetConnection(direction, out _))
                throw new InvalidOperationException($"Room {RoomId} already has a {direction} connection.");
            connections.Add(new GeneratedRoomConnection(direction, destinationRoomId));
        }
    }

    public sealed class GeneratedFloor
    {
        public GeneratedFloor(int floorNumber, int floorSeed, int topologySeed, int contentSeed,
            string startingRoomId, string bossRoomId, GeneratedRoomNode[] nodes)
        {
            FloorNumber = floorNumber; FloorSeed = floorSeed; TopologySeed = topologySeed;
            ContentSeed = contentSeed; StartingRoomId = startingRoomId; BossRoomId = bossRoomId;
            Nodes = nodes ?? Array.Empty<GeneratedRoomNode>();
        }
        public int FloorNumber { get; }
        public int FloorSeed { get; }
        public int TopologySeed { get; }
        public int ContentSeed { get; }
        public string StartingRoomId { get; }
        public string BossRoomId { get; }
        public IReadOnlyList<GeneratedRoomNode> Nodes { get; }
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
            Dictionary<string, GeneratedRoomNode> byId = new(StringComparer.Ordinal);
            List<RoomGridPosition> positions = new(); int starts = 0, bosses = 0, treasures = 0;
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
                bool validRoleType = node.Role switch
                {
                    GeneratedRoomRole.Start => node.RoomType == RoomType.Normal,
                    GeneratedRoomRole.Intermediate => node.RoomType == RoomType.Normal,
                    GeneratedRoomRole.Treasure => node.RoomType == RoomType.Reward,
                    GeneratedRoomRole.Boss => node.RoomType == RoomType.Boss,
                    _ => false,
                };
                if (!validRoleType)
                { error = $"Room {node.RoomId} role {node.Role} does not match definition type {node.RoomType}."; return false; }
            }
            if (starts != 1 || bosses != 1 || treasures < 1 ||
                !byId.ContainsKey(floor.StartingRoomId) || !byId.ContainsKey(floor.BossRoomId))
            { error = $"Floor {floor.FloorNumber} must have one start, one boss, and at least one treasure room."; return false; }
            foreach (GeneratedRoomNode node in floor.Nodes)
            {
                HashSet<string> destinations = new(StringComparer.Ordinal); HashSet<RoomDoorDirection> directions = new();
                foreach (GeneratedRoomConnection connection in node.DirectionalConnections)
                {
                    if (!destinations.Add(connection.DestinationRoomId) || !directions.Add(connection.Direction) ||
                        connection.DestinationRoomId == node.RoomId || !byId.TryGetValue(connection.DestinationRoomId, out GeneratedRoomNode destination))
                    { error = $"Room {node.RoomId} has a duplicate, self, or missing connection."; return false; }
                    if (!destination.GridPosition.Equals(node.GridPosition.Offset(connection.Direction)) ||
                        !destination.TryGetConnection(Opposite(connection.Direction), out GeneratedRoomConnection reverse) || reverse.DestinationRoomId != node.RoomId)
                    { error = $"Room {node.RoomId} has a direction or reverse-link mismatch at {connection.Direction}."; return false; }
                }
            }
            Dictionary<string, int> distances = Distances(floor.StartingRoomId, byId);
            if (distances.Count != floor.Nodes.Count || !distances.TryGetValue(floor.BossRoomId, out int bossDistance) ||
                bossDistance < MinimumBossDistance || byId[floor.BossRoomId].DirectionalConnections.Count != 1)
            { error = $"Floor {floor.FloorNumber} is disconnected or its boss is not an end room at distance {MinimumBossDistance}+."; return false; }
            error = null; return true;
        }

        private static Dictionary<string, int> Distances(string start, IReadOnlyDictionary<string, GeneratedRoomNode> byId)
        {
            Dictionary<string, int> result = new(StringComparer.Ordinal) { [start] = 0 }; Queue<string> queue = new(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (GeneratedRoomConnection connection in byId[current].DirectionalConnections)
                    if (result.TryAdd(connection.DestinationRoomId, result[current] + 1)) queue.Enqueue(connection.DestinationRoomId);
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
            ContentSalt = 0xAD90777Du, AttemptSalt = 0x7E95761Eu;
        [SerializeField, Min(1)] private int floorCount = 3;
        [SerializeField, Min(3)] private int minimumRoomsPerFloor = 6;
        [SerializeField, Min(3)] private int maximumRoomsPerFloor = 8;
        [SerializeField, Min(1)] private int minimumBossDistance = 3;
        [SerializeField, Min(1)] private int generationRetryLimit = 32;
        [SerializeField] private RoomDefinition[] roomDefinitions = Array.Empty<RoomDefinition>();
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

        public void Configure(int floors, int rooms, RoomDefinition[] definitions) => Configure(floors, rooms, rooms, 2, 32, definitions);
        public void Configure(int floors, int minRooms, int maxRooms, int bossDistance, int retries, RoomDefinition[] definitions)
        { floorCount = floors; minimumRoomsPerFloor = minRooms; maximumRoomsPerFloor = maxRooms;
          minimumBossDistance = bossDistance; generationRetryLimit = retries; roomDefinitions = definitions ?? Array.Empty<RoomDefinition>();
          if (!Application.isPlaying) { runSeed = 0; hasRunSeed = false; } }

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
            if (!Pools(out List<RoomDefinition> normal, out List<RoomDefinition> reward, out List<RoomDefinition> boss, out error)) return false;
            GeneratedFloor[] floors = new GeneratedFloor[floorCount];
            for (int i = 0; i < floorCount; i++)
            {
                int floorSeed = DeriveSeed(seed, i + 1, FloorSalt);
                if (!TryFloor(i + 1, floorSeed, normal, reward, boss, out floors[i], out string failure))
                { error = $"Floor generation failed after {generationRetryLimit} attempts for run seed {seed}, floor seed {floorSeed}, floor {i + 1}. {failure}"; return false; }
            }
            graph = new GeneratedFloorGraph(floors, minimumBossDistance);
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
            IReadOnlyList<RoomDefinition> reward, IReadOnlyList<RoomDefinition> boss,
            out GeneratedFloor floor, out string error)
        {
            int topologySeed = DeriveSeed(floorSeed, 0, TopologySalt), contentSeed = DeriveSeed(floorSeed, 0, ContentSalt);
            for (int attempt = 0; attempt < generationRetryLimit; attempt++)
            {
                StableRandom random = new(unchecked((uint)DeriveSeed(topologySeed, attempt, AttemptSalt)));
                List<MutableRoom> rooms = Grow(random.NextInclusive(minimumRoomsPerFloor, maximumRoomsPerFloor), ref random);
                if (rooms == null) continue;
                Dictionary<int, int> distance = MutableDistances(rooms);
                int bossIndex = BossIndex(rooms, distance, ref random);
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
                floor = new GeneratedFloor(floorNumber, floorSeed, topologySeed, contentSeed,
                    nodes[0].RoomId, nodes[bossIndex].RoomId, nodes); error = null; return true;
            }
            floor = null; error = $"Could not place an end-room boss at distance {minimumBossDistance}+ and a separate treasure room."; return false;
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
        private int BossIndex(IReadOnlyList<MutableRoom> rooms, IReadOnlyDictionary<int, int> distances, ref StableRandom random)
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
            out List<RoomDefinition> boss, out string error)
        {
            normal = new(); reward = new(); boss = new(); HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (RoomDefinition definition in roomDefinitions)
            {
                if (definition == null) { error = "FloorGenerator has a missing room definition."; return false; }
                if (!definition.TryValidate(out error)) { error = $"FloorGenerator has an invalid room definition. {error}"; return false; }
                if (!ids.Add(definition.RoomDefinitionId)) { error = $"Room definition ID '{definition.RoomDefinitionId}' is duplicated."; return false; }
                if (definition.RoomType == RoomType.Normal) normal.Add(definition);
                else if (definition.RoomType == RoomType.Reward) reward.Add(definition); else if (definition.RoomType == RoomType.Boss) boss.Add(definition);
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
