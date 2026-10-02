using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Chest-0: picks a chest position in a room. The result is the Layout grid point nearest the preferred point that
    // the player can stand on and walk to from the doors, clear of every obstacle (destructible ones count as intact),
    // of each required door passage including the safe entry point, of chests already placed and of the avoided point
    // (the player when a room clears). The search only reads the Template and the room's authored obstacles.
    // A solid chest drops its contents outside its body: FindContentPositions picks free points on two rings around it.
    public static class ChestPlacement
    {
        public const float ChestWorldSize = 0.8f;
        // The largest floor pickup (bomb) has a 0.25 world radius.
        public const float MaximumPickupRadius = 0.25f;
        // Content rings: the first clears the chest body's corners (half size × √2) by a pickup, the second takes what
        // does not fit.
        public const float InnerContentRadius = ChestWorldSize * 0.5f * 1.4143f + MaximumPickupRadius + 0.05f;
        public const float OuterContentRadius = InnerContentRadius + MaximumPickupRadius * 2f;
        public const int MaximumContentCount = 7;
        // Room for a pickup between two chests.
        public const float MinimumChestSpacing = ChestWorldSize + MaximumPickupRadius * 2f + 0.2f;
        // The chest body never appears on top of the avoided point (the player's center).
        public const float AvoidClearance = ChestWorldSize * 0.5f + RoomObstacleLayout.ActorRadius + 0.1f;

        private const int RingSteps = 12;
        // Spreads consecutive picks around the ring instead of filling one side first.
        private static readonly int[] RingOrder = { 0, 6, 3, 9, 1, 7, 4, 10, 2, 8, 5, 11 };

        public static float ChestHalfSize => ChestWorldSize * 0.5f;

        public static bool TryFindSafeLocalPosition(RoomTemplateDefinition template, GameObject room,
            Vector2 preferredLocal, IReadOnlyList<Vector2> occupiedLocal, out Vector2 position, out string error) =>
            TryFindSafeLocalPosition(template, room, preferredLocal, occupiedLocal, null, out position, out error);

        public static bool TryFindSafeLocalPosition(RoomTemplateDefinition template, GameObject room,
            Vector2 preferredLocal, IReadOnlyList<Vector2> occupiedLocal, Vector2? avoidLocal, out Vector2 position,
            out string error)
        {
            position = default;
            if (template == null || template.Profile == null)
            {
                error = "Chest placement requires a Room Template with a profile.";
                return false;
            }

            if (template.DoorSlots.Count == 0)
            {
                error = $"Template '{template.TemplateId}' has no door to reach a chest from.";
                return false;
            }

            if (!RoomObstacleLayout.TryCollectFootprints(room, out List<RoomObstacleFootprint> footprints, out error))
                return false;

            Rect bounds = template.Profile.MovementBounds;
            Func<Vector2, bool> isReachable = RoomObstacleLayout.CreateReachability(bounds,
                template.DoorSlots[0].SafeEntryPosition, footprints);
            List<Rect> doorPassages = new();
            foreach (RoomTemplateDoor door in template.DoorSlots)
            {
                doorPassages.Add(RoomObstacleLayout.Expand(RoomTemplateGeometry.RequiredDoorPassageBounds(door),
                    RoomObstacleLayout.ActorRadius + ChestHalfSize));
            }

            float step = RoomObstacleLayout.GridStep;
            bool found = false;
            float bestDistance = float.MaxValue;
            for (float y = Mathf.Ceil(bounds.yMin / step) * step; y <= bounds.yMax; y += step)
            for (float x = Mathf.Ceil(bounds.xMin / step) * step; x <= bounds.xMax; x += step)
            {
                Vector2 candidate = new(x, y);
                float distance = (candidate - preferredLocal).sqrMagnitude;
                if (distance >= bestDistance || !IsSafe(candidate, isReachable, doorPassages, occupiedLocal) ||
                    (avoidLocal.HasValue &&
                     (candidate - avoidLocal.Value).sqrMagnitude < AvoidClearance * AvoidClearance))
                {
                    continue;
                }

                position = candidate;
                bestDistance = distance;
                found = true;
            }

            error = found ? null : $"Template '{template.TemplateId}' has no safe chest position left.";
            return found;
        }

        public static bool IsSafe(Vector2 candidate, Func<Vector2, bool> isReachable, IReadOnlyList<Rect> doorPassages,
            IReadOnlyList<Vector2> occupiedLocal)
        {
            if (!isReachable(candidate)) return false;
            foreach (Rect passage in doorPassages)
                if (passage.Contains(candidate)) return false;
            if (occupiedLocal == null) return true;
            foreach (Vector2 occupied in occupiedLocal)
                if ((candidate - occupied).sqrMagnitude < MinimumChestSpacing * MinimumChestSpacing) return false;
            return true;
        }

        // Room-local drop points around a chest: free of walls, obstacles, other chest bodies and each other, inner
        // ring first. Returns false when fewer than count points are free; the missing ones repeat the first point
        // (or the top of the inner ring), where physics separates the pickups.
        public static bool TryFindContentPositions(Rect movementBounds, IReadOnlyList<RoomObstacleFootprint> footprints,
            Vector2 chestLocal, IReadOnlyList<Vector2> otherChestsLocal, int count, out List<Vector2> positions)
        {
            positions = new List<Vector2>();
            if (count <= 0) return true;
            Rect inner = RoomObstacleLayout.Expand(movementBounds, -MaximumPickupRadius);
            float chestReach = ChestHalfSize * Mathf.Sqrt(2f) + MaximumPickupRadius;
            foreach (float radius in new[] { InnerContentRadius, OuterContentRadius })
            foreach (int step in RingOrder)
            {
                if (positions.Count == count) return true;
                float angle = Mathf.PI * 0.5f + step * Mathf.PI * 2f / RingSteps;
                Vector2 candidate = chestLocal + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!inner.Contains(candidate) || !IsClear(candidate, footprints, MaximumPickupRadius)) continue;
                bool blocked = false;
                if (otherChestsLocal != null)
                    foreach (Vector2 other in otherChestsLocal)
                        blocked |= (candidate - other).sqrMagnitude < chestReach * chestReach;
                foreach (Vector2 chosen in positions)
                    blocked |= (candidate - chosen).sqrMagnitude < 4f * MaximumPickupRadius * MaximumPickupRadius;
                if (!blocked) positions.Add(candidate);
            }

            bool enough = positions.Count == count;
            Vector2 fallback = positions.Count > 0 ? positions[0] : chestLocal + Vector2.up * InnerContentRadius;
            while (positions.Count < count) positions.Add(fallback);
            return enough;
        }

        public static bool IsClear(Vector2 point, IReadOnlyList<RoomObstacleFootprint> footprints, float clearance)
        {
            foreach (RoomObstacleFootprint footprint in footprints)
            {
                Rect rect = footprint.Bounds;
                float dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
                float dy = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
                if (dx * dx + dy * dy < clearance * clearance - 0.0001f) return false;
            }

            return true;
        }
    }
}
