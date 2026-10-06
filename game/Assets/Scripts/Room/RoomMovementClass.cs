using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Flight-0: the movement block classes a walking or flying body meets.
    // - Wall: room walls, doors, chests and any future high obstacle (tree). Nothing walks or flies through them.
    // - Low obstacle: every obstacle in the game today (DestructibleObstacle, RoomStaticObstacle). They stay on the
    //   Environment layer, so walking bodies, projectiles and lines of fire still stop at them; only a flying player
    //   passes over them.
    // - Pit: the Pit layer (Terrain-0). Walking bodies stop at it, projectiles and a flying player pass over it.
    public static class RoomMovementClass
    {
        public const string EnvironmentLayerName = "Environment";
        public const float NearestWalkableStep = 0.25f;
        public const int NearestWalkableDirections = 16;

        private static readonly List<Collider2D> PointHits = new();

        public static int EnvironmentMask => LayerMask.GetMask(EnvironmentLayerName);
        public static int PitMask => LayerMask.GetMask(RoomPit.LayerName);

        // A solid obstacle collider a flying player passes over. Walls, doors and chests are not low obstacles.
        public static bool IsLowObstacle(Collider2D collider)
        {
            return collider != null && !collider.isTrigger &&
                   (collider.TryGetComponent(out DestructibleObstacle _) ||
                    collider.TryGetComponent(out RoomStaticObstacle _));
        }

        public static bool IsOverPit(Vector2 point) => Physics2D.OverlapPoint(point, PitMask) != null;

        public static bool IsOverLowObstacle(Vector2 point)
        {
            ContactFilter2D filter = new() { useLayerMask = true, layerMask = EnvironmentMask, useTriggers = false };
            Physics2D.OverlapPoint(point, filter, PointHits);
            foreach (Collider2D hit in PointHits)
                if (IsLowObstacle(hit)) return true;
            return false;
        }

        // True when a body of this radius fits at the point without touching a wall, obstacle or pit.
        public static bool IsWalkable(Vector2 point, float clearance) =>
            Physics2D.OverlapCircle(point, Mathf.Max(0.01f, clearance), RoomPit.MovementBlockMask) == null;

        // The closest walkable point on rings of NearestWalkableStep around the origin, checked in a fixed direction
        // order so the same layout always gives the same point.
        public static bool TryFindNearestWalkablePoint(Vector2 origin, float clearance, float maximumDistance,
            out Vector2 point)
        {
            if (IsWalkable(origin, clearance))
            {
                point = origin;
                return true;
            }

            for (float distance = NearestWalkableStep; distance <= maximumDistance + 0.0001f;
                 distance += NearestWalkableStep)
            {
                for (int index = 0; index < NearestWalkableDirections; index++)
                {
                    float angle = index * Mathf.PI * 2f / NearestWalkableDirections;
                    Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    if (!IsWalkable(candidate, clearance)) continue;
                    point = candidate;
                    return true;
                }
            }

            point = origin;
            return false;
        }
    }
}
