using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Density-0 (docs/28 §10.2, user decision 2026-11-23): big rooms need more SpawnPoints than their Layouts mark,
    // so the missing ones are generated until the user places them by hand. The authored points stay first and keep
    // their roles; the generated ones follow them, take melee and rear firing, and are chosen without a seed so the
    // same room always gets the same points.
    //
    // A generated point keeps clear of walls, obstacles, pits, chests and floor traps, stays far enough from every
    // door that it is safe whichever doors a generated room opens, must be reachable and see enough of the room, and
    // is spread out by farthest-point selection from the points already there.
    public static class RoomSpawnPointDensity
    {
        // Part of the Layout importer's content hash. Raise it when the same room would get different points.
        public const int Revision = 3;
        public const float WallMargin = 0.75f;
        public const float FootprintClearance = RoomObstacleLayout.ActorRadius + 0.25f;
        // Floor traps block nothing, so a point only has to keep the body off them (the Layout rule).
        public const float FloorTrapClearance = RoomObstacleLayout.ActorRadius;
        public const float MinimumSpacing = 1.5f;
        // Above the Encounter safety distances, so a point never depends on which doors are open.
        public const float DoorEntryClearance = Week14Encounter1Setup.MinimumPlayerDistance + 0.5f;
        public const float DoorSlotClearance = Week14Encounter1Setup.MinimumDoorDistance + 0.5f;
        // Above RoomObstacleLayout.MinimumSightCoverage, because the samples here are an approximation of its own.
        public const float MinimumSightCoverage = RoomObstacleLayout.MinimumSightCoverage + 0.05f;

        // The authored points followed by generated ones up to targetCount. Fewer authored points than the target
        // is the usual case; as many or more returns the authored points unchanged.
        public static Vector2[] Extend(string layoutId, Rect movementBounds, Rect encounterBounds,
            IReadOnlyList<RoomTemplateDoor> doors, IReadOnlyList<Vector2> authored,
            IReadOnlyList<RoomObstacleFootprint> footprints, IReadOnlyList<RoomObstacleFootprint> floorTraps,
            int targetCount)
        {
            List<Vector2> points = new(authored ?? Array.Empty<Vector2>());
            if (points.Count >= targetCount) return points.ToArray();
            if (doors == null || doors.Count == 0)
                throw new InvalidOperationException($"Room '{layoutId}' needs doors before SpawnPoints are generated.");

            IReadOnlyList<RoomObstacleFootprint> solid = footprints ?? Array.Empty<RoomObstacleFootprint>();
            IReadOnlyList<RoomObstacleFootprint> traps = floorTraps ?? Array.Empty<RoomObstacleFootprint>();
            Func<Vector2, bool> standable =
                RoomObstacleLayout.CreateReachability(movementBounds, doors[0].SafeEntryPosition, solid);
            List<RoomObstacleFootprint> remaining = RoomObstacleLayout.AfterDestruction(solid);
            Func<Vector2, bool> open =
                RoomObstacleLayout.CreateReachability(movementBounds, doors[0].SafeEntryPosition, remaining);

            List<Vector2> samples = new();
            for (float x = encounterBounds.xMin + RoomObstacleLayout.SightSampleSpacing * 0.5f;
                 x < encounterBounds.xMax; x += RoomObstacleLayout.SightSampleSpacing)
            for (float y = encounterBounds.yMin + RoomObstacleLayout.SightSampleSpacing * 0.5f;
                 y < encounterBounds.yMax; y += RoomObstacleLayout.SightSampleSpacing)
            {
                Vector2 sample = new(x, y);
                if (open(sample)) samples.Add(sample);
            }

            Rect inner = RoomObstacleLayout.Expand(encounterBounds, -WallMargin);
            List<Vector2> candidates = new();
            float step = RoomObstacleLayout.GridStep;
            for (float y = Mathf.Floor(inner.yMax / step) * step; y >= inner.yMin; y -= step)
            for (float x = Mathf.Ceil(inner.xMin / step) * step; x <= inner.xMax; x += step)
            {
                Vector2 point = new(x, y);
                if (IsCandidate(point, doors, solid, traps, standable) &&
                    RoomObstacleLayout.SightCoverage(point, samples, remaining) >= MinimumSightCoverage)
                    candidates.Add(point);
            }

            while (points.Count < targetCount)
            {
                Vector2 best = default;
                float bestDistance = -1f;
                foreach (Vector2 candidate in candidates)
                {
                    float nearest = points.Count == 0
                        ? float.MaxValue
                        : points.Min(point => Vector2.Distance(point, candidate));
                    // Strictly greater keeps the first candidate (top row, then left) on ties.
                    if (nearest < MinimumSpacing || nearest <= bestDistance) continue;
                    best = candidate;
                    bestDistance = nearest;
                }

                if (bestDistance < 0f)
                {
                    throw new InvalidOperationException(
                        $"Room '{layoutId}' has room for only {points.Count} SpawnPoints; Density-0 needs " +
                        $"{targetCount}. Mark more SpawnPoints in its Layout or open up the floor.");
                }

                points.Add(best);
            }

            return points.ToArray();
        }

        private static bool IsCandidate(Vector2 point, IReadOnlyList<RoomTemplateDoor> doors,
            IReadOnlyList<RoomObstacleFootprint> solid, IReadOnlyList<RoomObstacleFootprint> traps,
            Func<Vector2, bool> standable)
        {
            foreach (RoomTemplateDoor door in doors)
            {
                if (RoomTemplateGeometry.IsInsideRequiredDoorPassage(door, point) ||
                    Vector2.Distance(point, door.SafeEntryPosition) < DoorEntryClearance ||
                    Vector2.Distance(point, door.SlotPosition) < DoorSlotClearance)
                    return false;
            }

            foreach (RoomObstacleFootprint footprint in solid)
                if (Distance(point, footprint.Bounds) < FootprintClearance) return false;
            foreach (RoomObstacleFootprint trap in traps)
                if (Distance(point, trap.Bounds) < FloorTrapClearance - 0.0001f) return false;
            return standable(point);
        }

        private static float Distance(Vector2 point, Rect rect)
        {
            float dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
