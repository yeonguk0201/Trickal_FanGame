using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public readonly struct RoomObstacleFootprint
    {
        public RoomObstacleFootprint(string obstacleId, Rect bounds, bool isDestructible)
        {
            ObstacleId = obstacleId;
            Bounds = bounds;
            IsDestructible = isDestructible;
        }

        public string ObstacleId { get; }
        // Room-local collider rectangle.
        public Rect Bounds { get; }
        public bool IsDestructible { get; }
    }

    // Static checks for an authored obstacle Layout. Obstacles are treated as solid even when destructible, so a
    // Layout never needs attacks or bombs to keep its doors connected, its SpawnPoints reachable, or its ranged
    // SpawnPoints able to fire into the combat area.
    public static class RoomObstacleLayout
    {
        public const float ActorRadius = 0.5f;
        public const float ProjectileClearance = 0.1f;
        public const float ReachabilityCellSize = 0.25f;
        public const float SightSampleSpacing = 1f;
        public const float MinimumSightCoverage = 0.35f;
        public const float GridStep = 0.5f;
        private const float Tolerance = 0.0001f;

        public static bool TryCollectFootprints(GameObject roomPrefab, out List<RoomObstacleFootprint> footprints,
            out string error)
        {
            footprints = new List<RoomObstacleFootprint>();
            if (roomPrefab == null)
            {
                error = "Obstacle Layout requires a Room Prefab.";
                return false;
            }

            Transform root = roomPrefab.transform;
            foreach (DestructibleObstacle obstacle in roomPrefab.GetComponentsInChildren<DestructibleObstacle>(true))
            {
                if (!TryReadBox(root, obstacle.gameObject, obstacle.ObstacleId, out Rect bounds, out error))
                    return false;
                footprints.Add(new RoomObstacleFootprint(obstacle.ObstacleId, bounds, true));
            }

            foreach (RoomStaticObstacle obstacle in roomPrefab.GetComponentsInChildren<RoomStaticObstacle>(true))
            {
                if (!TryReadBox(root, obstacle.gameObject, obstacle.ObstacleId, out Rect bounds, out error))
                    return false;
                footprints.Add(new RoomObstacleFootprint(obstacle.ObstacleId, bounds, false));
            }

            error = null;
            return true;
        }

        public static bool TryValidate(
            string layoutId,
            Rect movementBounds,
            Rect encounterBounds,
            IReadOnlyList<RoomTemplateDoor> doors,
            IReadOnlyList<Vector2> spawnPoints,
            IReadOnlyList<RoomObstacleFootprint> footprints,
            out string error)
        {
            if (footprints == null || footprints.Count == 0)
            {
                error = null;
                return true;
            }

            if (!TryValidateFootprints(layoutId, movementBounds, footprints, out error)) return false;

            foreach (RoomTemplateDoor door in doors)
            {
                Rect passage = Expand(RoomTemplateGeometry.RequiredDoorPassageBounds(door), ActorRadius);
                foreach (RoomObstacleFootprint footprint in footprints)
                {
                    if (Overlaps(passage, footprint.Bounds))
                    {
                        error = $"Layout '{layoutId}' obstacle '{footprint.ObstacleId}' blocks the {door.Direction} " +
                                "required door passage.";
                        return false;
                    }
                }
            }

            for (int index = 0; index < spawnPoints.Count; index++)
            {
                foreach (RoomObstacleFootprint footprint in footprints)
                {
                    if (Distance(spawnPoints[index], footprint.Bounds) < ActorRadius - Tolerance)
                    {
                        error = $"Layout '{layoutId}' SpawnPoint {index + 1} overlaps obstacle " +
                                $"'{footprint.ObstacleId}'.";
                        return false;
                    }
                }
            }

            ReachabilityGrid grid = new(movementBounds, footprints);
            if (doors.Count == 0)
            {
                error = $"Layout '{layoutId}' has no door to anchor its required passages.";
                return false;
            }

            bool[] reachable = grid.FloodFrom(doors[0].SafeEntryPosition);
            foreach (RoomTemplateDoor door in doors)
            {
                if (!grid.IsReachable(reachable, door.SafeEntryPosition))
                {
                    error = $"Layout '{layoutId}' obstacles cut the {door.Direction} safe entry off from the " +
                            $"{doors[0].Direction} safe entry.";
                    return false;
                }
            }

            for (int index = 0; index < spawnPoints.Count; index++)
            {
                if (!grid.IsReachable(reachable, spawnPoints[index]))
                {
                    error = $"Layout '{layoutId}' SpawnPoint {index + 1} is not reachable from the doors.";
                    return false;
                }
            }

            List<Vector2> samples = new();
            for (float x = encounterBounds.xMin + SightSampleSpacing * 0.5f; x < encounterBounds.xMax; x += SightSampleSpacing)
            for (float y = encounterBounds.yMin + SightSampleSpacing * 0.5f; y < encounterBounds.yMax; y += SightSampleSpacing)
            {
                Vector2 sample = new(x, y);
                if (grid.IsReachable(reachable, sample)) samples.Add(sample);
            }

            if (samples.Count == 0)
            {
                error = $"Layout '{layoutId}' has no reachable combat area.";
                return false;
            }

            for (int index = 0; index < spawnPoints.Count; index++)
            {
                float coverage = SightCoverage(spawnPoints[index], samples, footprints);
                if (coverage < MinimumSightCoverage)
                {
                    error = $"Layout '{layoutId}' SpawnPoint {index + 1} sees only {coverage:P0} of the combat area; " +
                            $"ranged enemies need at least {MinimumSightCoverage:P0}.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        // Whether an actor-sized circle centered on a point can stand there and walk to it from origin, on the same
        // grid TryValidate uses for doors and SpawnPoints.
        public static Func<Vector2, bool> CreateReachability(Rect movementBounds, Vector2 origin,
            IReadOnlyList<RoomObstacleFootprint> footprints)
        {
            IReadOnlyList<RoomObstacleFootprint> solid = footprints ?? Array.Empty<RoomObstacleFootprint>();
            ReachabilityGrid grid = new(movementBounds, solid);
            bool[] reached = grid.FloodFrom(origin);
            Rect inner = Expand(movementBounds, -ActorRadius + Tolerance);
            return point =>
            {
                if (!inner.Contains(point) || !grid.IsReachable(reached, point)) return false;
                foreach (RoomObstacleFootprint footprint in solid)
                    if (Distance(point, footprint.Bounds) < ActorRadius - Tolerance) return false;
                return true;
            };
        }

        public static float SightCoverage(Vector2 origin, IReadOnlyList<Vector2> samples,
            IReadOnlyList<RoomObstacleFootprint> footprints)
        {
            if (samples.Count == 0) return 0f;
            int visible = 0;
            foreach (Vector2 sample in samples)
            {
                bool blocked = false;
                foreach (RoomObstacleFootprint footprint in footprints)
                {
                    if (SegmentIntersects(origin, sample, Expand(footprint.Bounds, ProjectileClearance)))
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked) visible++;
            }

            return visible / (float)samples.Count;
        }

        private static bool TryValidateFootprints(string layoutId, Rect movementBounds,
            IReadOnlyList<RoomObstacleFootprint> footprints, out string error)
        {
            HashSet<string> ids = new(StringComparer.Ordinal);
            for (int index = 0; index < footprints.Count; index++)
            {
                RoomObstacleFootprint footprint = footprints[index];
                if (!ids.Add(footprint.ObstacleId ?? string.Empty))
                {
                    error = $"Layout '{layoutId}' duplicates obstacle ID '{footprint.ObstacleId}'.";
                    return false;
                }

                Rect bounds = footprint.Bounds;
                if (bounds.xMin < movementBounds.xMin - Tolerance || bounds.xMax > movementBounds.xMax + Tolerance ||
                    bounds.yMin < movementBounds.yMin - Tolerance || bounds.yMax > movementBounds.yMax + Tolerance)
                {
                    error = $"Layout '{layoutId}' obstacle '{footprint.ObstacleId}' leaves the movement bounds.";
                    return false;
                }

                if (footprint.IsDestructible &&
                    (!Approximately(bounds.size, Vector2.one) || !OnGrid(bounds.center.x) || !OnGrid(bounds.center.y)))
                {
                    error = $"Layout '{layoutId}' destructible obstacle '{footprint.ObstacleId}' must be a 1x1 unit " +
                            $"on the {GridStep} grid.";
                    return false;
                }

                for (int other = 0; other < index; other++)
                {
                    if (Overlaps(bounds, footprints[other].Bounds))
                    {
                        error = $"Layout '{layoutId}' obstacles '{footprints[other].ObstacleId}' and " +
                                $"'{footprint.ObstacleId}' overlap.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private static bool TryReadBox(Transform root, GameObject owner, string obstacleId, out Rect bounds,
            out string error)
        {
            bounds = default;
            BoxCollider2D box = owner.GetComponent<BoxCollider2D>();
            if (box == null || box.isTrigger)
            {
                error = $"Layout obstacle '{obstacleId}' needs a solid BoxCollider2D.";
                return false;
            }

            if (Quaternion.Angle(Quaternion.identity, Quaternion.Inverse(root.rotation) * owner.transform.rotation) > 0.01f)
            {
                error = $"Layout obstacle '{obstacleId}' must stay axis-aligned.";
                return false;
            }

            Vector2 half = box.size * 0.5f;
            Vector2 first = root.InverseTransformPoint(owner.transform.TransformPoint(box.offset - half));
            Vector2 second = root.InverseTransformPoint(owner.transform.TransformPoint(box.offset + half));
            bounds = Rect.MinMaxRect(Mathf.Min(first.x, second.x), Mathf.Min(first.y, second.y),
                Mathf.Max(first.x, second.x), Mathf.Max(first.y, second.y));
            error = null;
            return true;
        }

        public static Rect Expand(Rect rect, float amount) =>
            Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);

        // Touching edges do not count; obstacles may sit side by side.
        public static bool Overlaps(Rect first, Rect second) =>
            first.xMin < second.xMax - Tolerance && second.xMin < first.xMax - Tolerance &&
            first.yMin < second.yMax - Tolerance && second.yMin < first.yMax - Tolerance;

        private static float Distance(Vector2 point, Rect rect)
        {
            float dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // Liang-Barsky clip of the segment against the rectangle.
        private static bool SegmentIntersects(Vector2 from, Vector2 to, Rect rect)
        {
            float enter = 0f;
            float exit = 1f;
            Vector2 delta = to - from;
            return Clip(-delta.x, from.x - rect.xMin, ref enter, ref exit) &&
                   Clip(delta.x, rect.xMax - from.x, ref enter, ref exit) &&
                   Clip(-delta.y, from.y - rect.yMin, ref enter, ref exit) &&
                   Clip(delta.y, rect.yMax - from.y, ref enter, ref exit);
        }

        private static bool Clip(float direction, float distance, ref float enter, ref float exit)
        {
            if (Mathf.Abs(direction) < 1e-7f) return distance >= 0f;
            float t = distance / direction;
            if (direction < 0f) enter = Mathf.Max(enter, t);
            else exit = Mathf.Min(exit, t);
            return enter <= exit;
        }

        private static bool OnGrid(float value) =>
            Mathf.Abs(value / GridStep - Mathf.Round(value / GridStep)) < Tolerance;

        private static bool Approximately(Vector2 first, Vector2 second) =>
            (first - second).sqrMagnitude < Tolerance;

        // Cells whose centers keep an actor-sized circle clear of every obstacle and inside the movement bounds.
        private sealed class ReachabilityGrid
        {
            private readonly Rect bounds;
            private readonly int width;
            private readonly int height;
            private readonly bool[] free;

            public ReachabilityGrid(Rect movementBounds, IReadOnlyList<RoomObstacleFootprint> footprints)
            {
                bounds = movementBounds;
                width = Mathf.Max(1, Mathf.RoundToInt(movementBounds.width / ReachabilityCellSize));
                height = Mathf.Max(1, Mathf.RoundToInt(movementBounds.height / ReachabilityCellSize));
                free = new bool[width * height];
                Rect inner = Expand(movementBounds, -ActorRadius + Tolerance);
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Vector2 center = Center(x, y);
                    bool clear = inner.Contains(center);
                    for (int index = 0; clear && index < footprints.Count; index++)
                    {
                        clear = Distance(center, footprints[index].Bounds) >= ActorRadius - Tolerance;
                    }

                    free[y * width + x] = clear;
                }
            }

            public bool[] FloodFrom(Vector2 start)
            {
                bool[] reached = new bool[free.Length];
                int first = Index(start);
                if (!free[first]) return reached;
                Stack<int> pending = new();
                reached[first] = true;
                pending.Push(first);
                while (pending.Count > 0)
                {
                    int cell = pending.Pop();
                    int x = cell % width;
                    int y = cell / width;
                    Visit(x + 1, y, reached, pending);
                    Visit(x - 1, y, reached, pending);
                    Visit(x, y + 1, reached, pending);
                    Visit(x, y - 1, reached, pending);
                }

                return reached;
            }

            public bool IsReachable(bool[] reached, Vector2 point) => reached[Index(point)];

            private void Visit(int x, int y, bool[] reached, Stack<int> pending)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return;
                int cell = y * width + x;
                if (reached[cell] || !free[cell]) return;
                reached[cell] = true;
                pending.Push(cell);
            }

            private Vector2 Center(int x, int y) =>
                new(bounds.xMin + (x + 0.5f) * ReachabilityCellSize, bounds.yMin + (y + 0.5f) * ReachabilityCellSize);

            private int Index(Vector2 point)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt((point.x - bounds.xMin) / ReachabilityCellSize), 0, width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt((point.y - bounds.yMin) / ReachabilityCellSize), 0, height - 1);
                return y * width + x;
            }
        }
    }
}
