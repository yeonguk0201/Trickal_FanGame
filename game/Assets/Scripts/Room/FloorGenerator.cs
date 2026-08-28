using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public enum GeneratedRoomRole
    {
        Start,
        Intermediate,
        Boss,
    }

    public sealed class GeneratedRoomNode
    {
        private readonly List<string> connectedRoomIds = new();

        public GeneratedRoomNode(
            string roomId,
            int floorNumber,
            int roomNumber,
            GeneratedRoomRole role,
            RoomDefinition definition)
        {
            RoomId = roomId;
            FloorNumber = floorNumber;
            RoomNumber = roomNumber;
            Role = role;
            Definition = definition;
        }

        public string RoomId { get; }
        public int FloorNumber { get; }
        public int RoomNumber { get; }
        public GeneratedRoomRole Role { get; }
        public RoomDefinition Definition { get; }
        public IReadOnlyList<string> ConnectedRoomIds => connectedRoomIds;

        internal void ConnectTo(string destinationRoomId)
        {
            if (!connectedRoomIds.Contains(destinationRoomId))
            {
                connectedRoomIds.Add(destinationRoomId);
            }
        }
    }

    public sealed class GeneratedFloorGraph
    {
        public GeneratedFloorGraph(GeneratedRoomNode[] nodes, string startingRoomId)
        {
            Nodes = nodes ?? Array.Empty<GeneratedRoomNode>();
            StartingRoomId = startingRoomId;
        }

        public IReadOnlyList<GeneratedRoomNode> Nodes { get; }
        public string StartingRoomId { get; }

        public bool TryValidate(out string error)
        {
            if (Nodes.Count == 0)
            {
                error = "Generated graph has no rooms.";
                return false;
            }

            Dictionary<string, GeneratedRoomNode> byId = new(StringComparer.Ordinal);
            HashSet<(int Floor, int Room)> addresses = new();
            foreach (GeneratedRoomNode node in Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.RoomId) ||
                    !byId.TryAdd(node.RoomId, node))
                {
                    error = $"Generated room ID '{node?.RoomId}' is empty or duplicated.";
                    return false;
                }

                if (!addresses.Add((node.FloorNumber, node.RoomNumber)))
                {
                    error = $"Generated address ({node.FloorNumber}, {node.RoomNumber}) is duplicated.";
                    return false;
                }

                if (node.Definition == null)
                {
                    error = $"Room {node.RoomId} has a missing definition.";
                    return false;
                }

                if (!node.Definition.TryValidate(out error))
                {
                    error = $"Room {node.RoomId} has an invalid definition. {error}";
                    return false;
                }
            }

            if (!byId.ContainsKey(StartingRoomId))
            {
                error = "Generated graph starting room does not exist.";
                return false;
            }

            foreach (GeneratedRoomNode node in Nodes)
            {
                foreach (string destinationId in node.ConnectedRoomIds)
                {
                    if (!byId.ContainsKey(destinationId) || destinationId == node.RoomId)
                    {
                        error = $"Room {node.RoomId} has an invalid connection to '{destinationId}'.";
                        return false;
                    }
                }
            }

            HashSet<string> visited = new(StringComparer.Ordinal);
            Queue<string> pending = new();
            pending.Enqueue(StartingRoomId);
            while (pending.Count > 0)
            {
                string roomId = pending.Dequeue();
                if (!visited.Add(roomId))
                {
                    continue;
                }

                foreach (string destinationId in byId[roomId].ConnectedRoomIds)
                {
                    pending.Enqueue(destinationId);
                }
            }

            if (visited.Count != Nodes.Count)
            {
                error = $"Generated graph is disconnected: reached {visited.Count} of {Nodes.Count} rooms.";
                return false;
            }

            error = null;
            return true;
        }
    }

    public sealed class FloorGenerator : MonoBehaviour
    {
        [SerializeField] private int seed = 20260828;
        [SerializeField, Min(1)] private int floorCount = 3;
        [SerializeField, Min(3)] private int roomsPerFloor = 3;
        [SerializeField] private RoomDefinition[] roomDefinitions = Array.Empty<RoomDefinition>();

        public int Seed => seed;
        public int FloorCount => floorCount;
        public int RoomsPerFloor => roomsPerFloor;
        public IReadOnlyList<RoomDefinition> RoomDefinitions => roomDefinitions;

        public void Configure(
            int configuredSeed,
            int configuredFloorCount,
            int configuredRoomsPerFloor,
            RoomDefinition[] configuredDefinitions)
        {
            seed = configuredSeed;
            floorCount = configuredFloorCount;
            roomsPerFloor = configuredRoomsPerFloor;
            roomDefinitions = configuredDefinitions ?? Array.Empty<RoomDefinition>();
        }

        public bool TryGenerate(out GeneratedFloorGraph graph, out string error)
        {
            graph = null;
            if (floorCount < 1 || roomsPerFloor < 3)
            {
                error = "Floor count must be positive and every floor must contain at least three rooms.";
                return false;
            }

            if (!TryBuildDefinitionPools(
                    out List<RoomDefinition> normalDefinitions,
                    out List<RoomDefinition> intermediateDefinitions,
                    out List<RoomDefinition> bossDefinitions,
                    out error))
            {
                return false;
            }

            StableRandom random = new(unchecked((uint)seed));
            List<GeneratedRoomNode> nodes = new(floorCount * roomsPerFloor);
            GeneratedRoomNode[,] floorRooms = new GeneratedRoomNode[floorCount, roomsPerFloor];
            for (int floorIndex = 0; floorIndex < floorCount; floorIndex++)
            {
                for (int roomIndex = 0; roomIndex < roomsPerFloor; roomIndex++)
                {
                    GeneratedRoomRole role = roomIndex == 0
                        ? GeneratedRoomRole.Start
                        : roomIndex == roomsPerFloor - 1
                            ? GeneratedRoomRole.Boss
                            : GeneratedRoomRole.Intermediate;
                    List<RoomDefinition> pool = role switch
                    {
                        GeneratedRoomRole.Start => normalDefinitions,
                        GeneratedRoomRole.Boss => bossDefinitions,
                        _ => intermediateDefinitions,
                    };
                    RoomDefinition definition = pool[random.NextIndex(pool.Count)];
                    GeneratedRoomNode node = new(
                        BuildRoomId(floorIndex + 1, roomIndex + 1),
                        floorIndex + 1,
                        roomIndex + 1,
                        role,
                        definition);
                    floorRooms[floorIndex, roomIndex] = node;
                    nodes.Add(node);
                }
            }

            for (int floorIndex = 0; floorIndex < floorCount; floorIndex++)
            {
                for (int roomIndex = 0; roomIndex < roomsPerFloor - 1; roomIndex++)
                {
                    GeneratedRoomNode current = floorRooms[floorIndex, roomIndex];
                    GeneratedRoomNode next = floorRooms[floorIndex, roomIndex + 1];
                    current.ConnectTo(next.RoomId);
                    next.ConnectTo(current.RoomId);
                }

                if (floorIndex < floorCount - 1)
                {
                    floorRooms[floorIndex, roomsPerFloor - 1]
                        .ConnectTo(floorRooms[floorIndex + 1, 0].RoomId);
                }
            }

            graph = new GeneratedFloorGraph(nodes.ToArray(), floorRooms[0, 0].RoomId);
            return graph.TryValidate(out error);
        }

        public static string BuildRoomId(int floorNumber, int roomNumber)
        {
            if (floorNumber < 1 || roomNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(floorNumber),
                    "Floor and room numbers must both be positive.");
            }

            return $"floor-{floorNumber:00}-room-{roomNumber:00}";
        }

        private bool TryBuildDefinitionPools(
            out List<RoomDefinition> normalDefinitions,
            out List<RoomDefinition> intermediateDefinitions,
            out List<RoomDefinition> bossDefinitions,
            out string error)
        {
            normalDefinitions = new List<RoomDefinition>();
            intermediateDefinitions = new List<RoomDefinition>();
            bossDefinitions = new List<RoomDefinition>();
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (RoomDefinition definition in roomDefinitions)
            {
                if (definition == null)
                {
                    error = "FloorGenerator has a missing room definition.";
                    return false;
                }

                if (!definition.TryValidate(out error))
                {
                    error = $"FloorGenerator has an invalid room definition. {error}";
                    return false;
                }

                if (!ids.Add(definition.RoomDefinitionId))
                {
                    error = $"Room definition ID '{definition.RoomDefinitionId}' is duplicated.";
                    return false;
                }

                switch (definition.RoomType)
                {
                    case RoomType.Normal:
                        normalDefinitions.Add(definition);
                        intermediateDefinitions.Add(definition);
                        break;
                    case RoomType.Reward:
                        intermediateDefinitions.Add(definition);
                        break;
                    case RoomType.Boss:
                        bossDefinitions.Add(definition);
                        break;
                }
            }

            if (normalDefinitions.Count == 0 || bossDefinitions.Count == 0 ||
                intermediateDefinitions.Count == normalDefinitions.Count)
            {
                error = "FloorGenerator needs at least one normal, reward, and boss room definition.";
                return false;
            }

            error = null;
            return true;
        }

        private struct StableRandom
        {
            private uint state;

            public StableRandom(uint configuredSeed)
            {
                state = configuredSeed == 0 ? 0x6D2B79F5u : configuredSeed;
            }

            public int NextIndex(int count)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (int)(state % (uint)count);
            }
        }
    }
}
