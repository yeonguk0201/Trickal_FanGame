using System;
using System.Linq;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DestructibleObstacle))]
    public sealed class RoomObstacleVariantSlot : MonoBehaviour
    {
        public const uint VariantSeedSalt = 0xA511E9B3u;

        [SerializeField] private ObstacleVariantTable variantTable;

        public ObstacleVariantTable VariantTable => variantTable;

        public void Configure(ObstacleVariantTable configuredTable) => variantTable = configuredTable;

        public bool TryValidate(out string error)
        {
            DestructibleObstacle obstacle = GetComponent<DestructibleObstacle>();
            if (obstacle == null)
            {
                error = "Obstacle variant slot needs a destructible obstacle.";
                return false;
            }

            if (!obstacle.TryValidate(out error))
            {
                error = $"Obstacle variant slot needs a valid destructible obstacle. {error}";
                return false;
            }

            if (variantTable == null || !variantTable.TryValidate(out error))
            {
                error = $"Obstacle '{obstacle.ObstacleId}' needs a valid variant table. {error}";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryResolveForRoom(RoomPrefab room, int roomContentSeed, out string error)
        {
            RoomObstacleVariantSlot[] slots = room != null
                ? room.GetComponentsInChildren<RoomObstacleVariantSlot>(true)
                    .OrderBy(slot => slot.GetComponent<DestructibleObstacle>().ObstacleId, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<RoomObstacleVariantSlot>();
            if (slots.Length == 0)
            {
                error = null;
                return true;
            }

            ObstacleVariantTable table = slots[0].variantTable;
            foreach (RoomObstacleVariantSlot slot in slots)
            {
                if (!slot.TryValidate(out error)) return false;
                if (slot.variantTable != table)
                {
                    error = $"Room '{room.name}' obstacle variant slots must share one room-level table.";
                    return false;
                }
            }

            uint state = unchecked((uint)FloorGenerator.DeriveSeed(roomContentSeed, slots.Length, VariantSeedSalt));
            if (NextUnit(ref state) >= table.SpecialRoomChance)
            {
                error = null;
                return true;
            }

            int slotIndex = Mathf.Min(slots.Length - 1, Mathf.FloorToInt(NextUnit(ref state) * slots.Length));
            ObstacleVariantDefinition variant = table.Select(NextUnit(ref state));
            slots[slotIndex].GetComponent<DestructibleObstacle>().ApplyVariant(variant);
            error = null;
            return true;
        }

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
