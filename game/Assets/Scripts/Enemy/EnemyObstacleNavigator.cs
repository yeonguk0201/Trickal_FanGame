using System.Collections.Generic;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Obstacle-0 movement rule: walk straight while the body-sized line to the goal is clear, otherwise follow an
    // A* path over a 0.5-unit grid of Environment-free cells. Rooms are small and hand-authored, so the grid is built
    // on demand around the enemy and its goal instead of baked per room. Terrain-0: walking also avoids pits, while
    // lines of fire only check Environment because projectiles pass over pits. Flight-0 (D3): when the goal cannot be
    // reached on foot (a flying player over a pit or obstacle, or a sealed-off target), the enemy walks to the reachable
    // cell nearest the goal and holds there instead of pushing into the edge.
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
        public const float RetreatProbeDistance = 0.75f;
        private static readonly float[] RetreatAngles = { 0f, 45f, -45f };

        private static readonly Vector2Int[] NeighborOffsets =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
        };

        private readonly List<Vector2> path = new();
        private int pathIndex;
        private float nextRepathTime = float.NegativeInfinity;
        private Vector2 plannedGoal;
        private bool plannedGoalReachable = true;
        private bool hasNearestPoint;
        private Vector2 nearestPoint;
        private Vector2 nearestPointGoal;

        public IReadOnlyList<Vector2> Path => path;
        public bool IsFollowingPath => pathIndex < path.Count;
        // False while the last plan could only get near the goal.
        public bool IsGoalReachable => plannedGoalReachable;

        // What a walking enemy cannot cross.
        public static int ObstacleMask => RoomPit.MovementBlockMask;
        // What stops a projectile.
        public static int LineOfFireMask => LayerMask.GetMask("Environment");

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
            return IsCastClear(from, to, bodyRadius * CastRadiusScale, ObstacleMask);
        }

        // True when an enemy projectile fired from one point would reach the other without hitting an obstacle.
        public static bool HasLineOfFire(Vector2 from, Vector2 to)
        {
            return IsCastClear(from, to, ProjectileClearanceRadius, LineOfFireMask);
        }

        private static bool IsCastClear(Vector2 from, Vector2 to, float radius, int mask)
        {
            Vector2 offset = to - from;
            float distance = offset.magnitude;
            if (distance <= 0.0001f) return true;
            return Physics2D.CircleCast(from, Mathf.Max(0.01f, radius), offset / distance, distance, mask)
                .collider == null;
        }

        // Obstacle-3: backing away takes the straight line or a 45-degree turn from it, whichever is clear for a short
        // step; with every option blocked the caller holds position instead of pushing into the obstacle.
        public static bool TryFindRetreatDirection(Vector2 from, Vector2 away, float bodyRadius, out Vector2 direction)
        {
            direction = Vector2.zero;
            if (away.sqrMagnitude <= 0.0001f) return false;
            away.Normalize();
            foreach (float angle in RetreatAngles)
            {
                Vector2 candidate = Rotate(away, angle);
                if (HasClearPath(from, from + candidate * RetreatProbeDistance, bodyRadius))
                {
                    direction = candidate;
                    return true;
                }
            }

            return false;
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        public void Reset()
        {
            path.Clear();
            pathIndex = 0;
            nextRepathTime = float.NegativeInfinity;
            plannedGoalReachable = true;
            hasNearestPoint = false;
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
                pathIndex = 0;
                if (hasNearestPoint && (goal - nearestPointGoal).sqrMagnitude <= CellSize * CellSize &&
                    FindPathOrNearest(from, nearestPoint, bodyRadius, path))
                {
                    // Two sides of a pit can be almost equally near, and the grid shifts with the enemy, so choosing
                    // again on every repath would flip between them. The chosen point stays until the player moves on.
                    plannedGoalReachable = false;
                }
                else
                {
                    plannedGoalReachable = FindPathOrNearest(from, goal, bodyRadius, path);
                    hasNearestPoint = !plannedGoalReachable;
                    nearestPoint = path.Count > 0 ? path[path.Count - 1] : from;
                    nearestPointGoal = goal;
                }
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
                // At the goal's last cell the enemy closes the remaining gap; at the point nearest an unreachable
                // goal it holds position.
                return plannedGoalReachable ? direct.normalized : Vector2.zero;
            }

            Vector2 toWaypoint = path[pathIndex] - from;
            return toWaypoint.sqrMagnitude > 0.0001f ? toWaypoint.normalized : direct.normalized;
        }

        // True with the full path when the goal is reachable; false with an empty list otherwise.
        public static bool TryFindPath(Vector2 from, Vector2 goal, float bodyRadius, List<Vector2> result)
        {
            if (FindPathOrNearest(from, goal, bodyRadius, result) && result.Count > 0) return true;
            result.Clear();
            return false;
        }

        // Returns whether the goal is reachable. When it is not, the result leads to the reachable cell nearest the
        // goal (empty when the enemy already stands there).
        public static bool FindPathOrNearest(Vector2 from, Vector2 goal, float bodyRadius, List<Vector2> result)
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

            bool reachable = targetIndex == startIndex || parent[targetIndex] >= 0;
            int endIndex = targetIndex;
            if (!reachable)
            {
                // Every cell reachable from the start is closed now; take the one nearest the goal, preferring the
                // cheaper walk on a tie so an enemy already at the edge stays put.
                endIndex = startIndex;
                float bestDistance = (CellCenter(start.x, start.y, origin) - goal).sqrMagnitude;
                for (int index = 0; index < cellCount; index++)
                {
                    if (!closed[index]) continue;
                    float distance = (CellCenter(index % width, index / width, origin) - goal).sqrMagnitude;
                    if (distance < bestDistance - 0.0001f ||
                        (Mathf.Abs(distance - bestDistance) <= 0.0001f && cost[index] < cost[endIndex]))
                    {
                        bestDistance = distance;
                        endIndex = index;
                    }
                }

                // The grid is laid out around the enemy, so cell centers shift as it walks. Once walking cannot get
                // at least half a cell closer, it holds instead of chasing the shifting cell.
                if ((from - goal).magnitude <= Mathf.Sqrt(bestDistance) + CellSize * 0.5f) return false;
            }

            for (int index = endIndex; index != startIndex && index >= 0; index = parent[index])
            {
                result.Add(CellCenter(index % width, index / width, origin));
            }

            result.Reverse();
            if (reachable && result.Count > 0) result[result.Count - 1] = goal;
            return reachable;
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
