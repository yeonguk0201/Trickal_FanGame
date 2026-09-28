using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Obstacle-0 movement rule: walk straight while the body-sized line to the goal is clear, otherwise follow an
    // A* path over a 0.5-unit grid of Environment-free cells. Rooms are small and hand-authored, so the grid is built
    // on demand around the enemy and its goal instead of baked per room.
    public sealed class EnemyObstacleNavigator
    {
        public const float CellSize = 0.5f;
        public const float RepathInterval = 0.25f;
        public const float SearchMargin = 5f;
        public const float ProjectileClearanceRadius = 0.1f;
        private const int MaximumCellsPerAxis = 96;
        // Casts slightly thinner than the body so sliding along an obstacle does not read as blocked.
        private const float CastRadiusScale = 0.9f;
        private const float WaypointReachedDistance = CellSize * 0.35f;

        private static readonly Vector2Int[] NeighborOffsets =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
        };

        private readonly List<Vector2> path = new();
        private int pathIndex;
        private float nextRepathTime = float.NegativeInfinity;
        private Vector2 plannedGoal;

        public IReadOnlyList<Vector2> Path => path;
        public bool IsFollowingPath => pathIndex < path.Count;

        public static int ObstacleMask => LayerMask.GetMask("Environment");

        public static float ResolveBodyRadius(GameObject owner)
        {
            Collider2D collider = owner != null ? owner.GetComponent<Collider2D>() : null;
            if (collider == null) return 0.25f;
            Vector3 extents = collider.bounds.extents;
            return Mathf.Max(0.05f, Mathf.Max(extents.x, extents.y));
        }

        // True when a body of the given radius can travel straight from one point to the other.
        public static bool HasClearPath(Vector2 from, Vector2 to, float bodyRadius)
        {
            Vector2 offset = to - from;
            float distance = offset.magnitude;
            if (distance <= 0.0001f) return true;
            return Physics2D.CircleCast(from, Mathf.Max(0.01f, bodyRadius * CastRadiusScale), offset / distance,
                distance, ObstacleMask).collider == null;
        }

        // True when an enemy projectile fired from one point would reach the other without hitting an obstacle.
        public static bool HasLineOfFire(Vector2 from, Vector2 to)
        {
            return HasClearPath(from, to, ProjectileClearanceRadius / CastRadiusScale);
        }

        public void Reset()
        {
            path.Clear();
            pathIndex = 0;
            nextRepathTime = float.NegativeInfinity;
        }

        // Unit direction to move this tick, or zero when already at the goal.
        public Vector2 GetMoveDirection(Vector2 from, Vector2 goal, float bodyRadius, float currentTime)
        {
            Vector2 direct = goal - from;
            if (direct.sqrMagnitude <= 0.0001f) return Vector2.zero;
            if (HasClearPath(from, goal, bodyRadius))
            {
                Reset();
                return direct.normalized;
            }

            if (!IsFollowingPath || currentTime >= nextRepathTime ||
                (goal - plannedGoal).sqrMagnitude > CellSize * CellSize)
            {
                nextRepathTime = currentTime + RepathInterval;
                plannedGoal = goal;
                if (!TryFindPath(from, goal, bodyRadius, path))
                {
                    path.Clear();
                }
                pathIndex = 0;
            }

            // Skip waypoints already reached, then aim at the farthest one still visible (string pulling).
            while (pathIndex < path.Count && (path[pathIndex] - from).sqrMagnitude <= WaypointReachedDistance *
                   WaypointReachedDistance)
            {
                pathIndex++;
            }

            while (pathIndex + 1 < path.Count && HasClearPath(from, path[pathIndex + 1], bodyRadius))
            {
                pathIndex++;
            }

            if (!IsFollowingPath)
            {
                // No route (for example a target sealed off): fall back to the straight line and let physics slide.
                return direct.normalized;
            }

            Vector2 toWaypoint = path[pathIndex] - from;
            return toWaypoint.sqrMagnitude > 0.0001f ? toWaypoint.normalized : direct.normalized;
        }

        public static bool TryFindPath(Vector2 from, Vector2 goal, float bodyRadius, List<Vector2> result)
        {
            result.Clear();
            Vector2 min = Vector2.Min(from, goal) - Vector2.one * SearchMargin;
            Vector2 max = Vector2.Max(from, goal) + Vector2.one * SearchMargin;
            int width = Mathf.Clamp(Mathf.CeilToInt((max.x - min.x) / CellSize), 1, MaximumCellsPerAxis);
            int height = Mathf.Clamp(Mathf.CeilToInt((max.y - min.y) / CellSize), 1, MaximumCellsPerAxis);
            Vector2 origin = (min + max) * 0.5f - new Vector2(width, height) * (CellSize * 0.5f);

            Vector2Int start = ToCell(from, origin, width, height);
            Vector2Int target = ToCell(goal, origin, width, height);
            int cellCount = width * height;
            bool[] blocked = new bool[cellCount];
            float clearance = bodyRadius * CastRadiusScale;
            int mask = ObstacleMask;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                blocked[y * width + x] = Physics2D.OverlapCircle(CellCenter(x, y, origin), clearance, mask) != null;
            }

            // The enemy may stand beside an obstacle and the player may hug one; both end cells stay usable.
            blocked[start.y * width + start.x] = false;
            blocked[target.y * width + target.x] = false;

            float[] cost = new float[cellCount];
            int[] parent = new int[cellCount];
            bool[] closed = new bool[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                cost[i] = float.PositiveInfinity;
                parent[i] = -1;
            }

            int startIndex = start.y * width + start.x;
            int targetIndex = target.y * width + target.x;
            MinHeap open = new(cellCount);
            cost[startIndex] = 0f;
            open.Push(startIndex, Heuristic(start, target));
            while (open.Count > 0)
            {
                int current = open.Pop();
                if (closed[current]) continue;
                if (current == targetIndex) break;
                closed[current] = true;
                int cx = current % width;
                int cy = current / width;
                foreach (Vector2Int offset in NeighborOffsets)
                {
                    int nx = cx + offset.x;
                    int ny = cy + offset.y;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int next = ny * width + nx;
                    if (blocked[next] || closed[next]) continue;
                    bool diagonal = offset.x != 0 && offset.y != 0;
                    // No corner cutting: a diagonal step needs both orthogonal neighbours open.
                    if (diagonal && (blocked[cy * width + nx] || blocked[ny * width + cx])) continue;
                    float nextCost = cost[current] + (diagonal ? 1.41421356f : 1f);
                    if (nextCost >= cost[next]) continue;
                    cost[next] = nextCost;
                    parent[next] = current;
                    open.Push(next, nextCost + Heuristic(new Vector2Int(nx, ny), target));
                }
            }

            if (parent[targetIndex] < 0 && targetIndex != startIndex)
            {
                return false;
            }

            for (int index = targetIndex; index != startIndex && index >= 0; index = parent[index])
            {
                result.Add(CellCenter(index % width, index / width, origin));
            }

            result.Reverse();
            if (result.Count > 0) result[result.Count - 1] = goal;
            return result.Count > 0;
        }

        private static Vector2Int ToCell(Vector2 point, Vector2 origin, int width, int height)
        {
            return new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt((point.x - origin.x) / CellSize), 0, width - 1),
                Mathf.Clamp(Mathf.FloorToInt((point.y - origin.y) / CellSize), 0, height - 1));
        }

        private static Vector2 CellCenter(int x, int y, Vector2 origin)
        {
            return origin + new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);
        }

        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return Mathf.Max(dx, dy) + 0.41421356f * Mathf.Min(dx, dy);
        }

        private sealed class MinHeap
        {
            private readonly List<(int index, float priority)> items;

            public MinHeap(int capacity) => items = new List<(int, float)>(Mathf.Min(capacity, 1024));
            public int Count => items.Count;

            public void Push(int index, float priority)
            {
                items.Add((index, priority));
                int child = items.Count - 1;
                while (child > 0)
                {
                    int parent = (child - 1) / 2;
                    if (items[parent].priority <= items[child].priority) break;
                    (items[parent], items[child]) = (items[child], items[parent]);
                    child = parent;
                }
            }

            public int Pop()
            {
                int result = items[0].index;
                int last = items.Count - 1;
                items[0] = items[last];
                items.RemoveAt(last);
                int parent = 0;
                while (true)
                {
                    int left = parent * 2 + 1;
                    if (left >= items.Count) break;
                    int right = left + 1;
                    int smallest = right < items.Count && items[right].priority < items[left].priority ? right : left;
                    if (items[parent].priority <= items[smallest].priority) break;
                    (items[parent], items[smallest]) = (items[smallest], items[parent]);
                    parent = smallest;
                }

                return result;
            }
        }
    }
}
