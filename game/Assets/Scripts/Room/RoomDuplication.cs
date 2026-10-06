using System;
using System.Collections.Generic;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // What one 멜룬카드 use found to copy in a room, listed before anything is copied.
    public sealed class RoomDuplicationTargets
    {
        internal RoomDuplicationTargets(List<TreasureChest> chests, List<GameObject> pickups)
        {
            Chests = chests;
            Pickups = pickups;
        }

        public IReadOnlyList<TreasureChest> Chests { get; }
        public IReadOnlyList<GameObject> Pickups { get; }
        public int Count => Chests.Count + Pickups.Count;
    }

    public sealed class RoomDuplicationResult
    {
        internal RoomDuplicationResult(RoomDuplicationTargets targets, List<TreasureChest> chests,
            List<GameObject> pickups, List<string> errors)
        {
            Targets = targets;
            CopiedChests = chests;
            CopiedPickups = pickups;
            Errors = errors;
        }

        public RoomDuplicationTargets Targets { get; }
        public IReadOnlyList<TreasureChest> CopiedChests { get; }
        public IReadOnlyList<GameObject> CopiedPickups { get; }
        // Chests that found no place in the room; everything else was copied.
        public IReadOnlyList<string> Errors { get; }
    }

    // Jjangsem-1 (멜룬카드, D4): copies each unopened chest and each floor consumable (heart, SP, gold, key, bomb) of
    // the current room once. The targets are listed first, so a copy made by this use is never copied again by the same
    // use, while copies from an earlier use are ordinary targets of the next one. A copied chest is a separate chest:
    // its own stable ID in the room's Run state, its own content roll from the room seed and the opening cost of its
    // kind (key, bomb). A copied pickup is a new floor pickup collected on its own. Opened chests, wallet amounts, shop
    // stalls, artifacts and single-use items are never targets.
    public static class RoomDuplication
    {
        public const string CopyChestPrefix = "melune-chest-";
        public const uint CopySeedSalt = 0x2E5BE93Du;
        // A copied pickup lands next to its original, at the first free point of a ring around it.
        public const float PickupCopyDistance = ChestPlacement.MaximumPickupRadius * 2f + 0.1f;

        private const int PickupRingSteps = 8;

        public static int PickupBlockMask =>
            LayerMask.GetMask(TreasureChest.LayerName, RoomPit.LayerName, HealthPickup.LayerName);

        // Chests count only with a site to put their copies in.
        public static RoomDuplicationTargets CollectTargets(Transform contentRoot, bool includeChests)
        {
            List<TreasureChest> chests = new();
            List<GameObject> pickups = new();
            if (contentRoot == null) return new RoomDuplicationTargets(chests, pickups);

            if (includeChests)
                foreach (TreasureChest chest in contentRoot.GetComponentsInChildren<TreasureChest>(false))
                    if (chest.IsBound && chest.IsClosed)
                        chests.Add(chest);

            foreach (Transform child in contentRoot.GetComponentsInChildren<Transform>(false))
                if (IsFloorConsumable(child.gameObject))
                    pickups.Add(child.gameObject);
            return new RoomDuplicationTargets(chests, pickups);
        }

        public static bool IsFloorConsumable(GameObject target)
        {
            if (target == null || !target.activeInHierarchy) return false;
            if (target.TryGetComponent(out HealthPickup heart)) return !heart.IsCollected;
            if (target.TryGetComponent(out SPPickup sp)) return !sp.IsCollected;
            return target.TryGetComponent(out RunResourcePickup resource) && !resource.IsCollected;
        }

        // site and table may be null in a room without chest support; its pickups are still copied.
        public static RoomDuplicationResult Duplicate(Transform contentRoot, ChestContentTable table,
            RoomChestSite site, int roomContentSeed)
        {
            RoomDuplicationTargets targets = CollectTargets(contentRoot, table != null && site != null);
            List<TreasureChest> copiedChests = new();
            List<GameObject> copiedPickups = new();
            List<string> errors = new();

            foreach (TreasureChest original in targets.Chests)
            {
                TreasureChest copy = CopyChest(table, site, original, roomContentSeed, out string error);
                if (copy != null) copiedChests.Add(copy);
                else errors.Add($"Chest '{original.ChestId}' was not copied. {error}");
            }

            // Pickup copies avoid the chests just copied, which physics only sees once synced.
            if (targets.Pickups.Count > 0) Physics2D.SyncTransforms();
            Rect? bounds = site != null ? site.Template.Profile.MovementBounds : null;
            Transform room = site != null ? site.Room.transform : null;
            List<Vector2> taken = new();
            foreach (GameObject original in targets.Pickups)
            {
                Vector2 position = FindPickupPoint(original.transform.position, room, bounds, taken);
                taken.Add(position);
                GameObject copy = UnityEngine.Object.Instantiate(original, position, original.transform.rotation,
                    original.transform.parent);
                copy.name = CopyName(original.name);
                copiedPickups.Add(copy);
            }

            return new RoomDuplicationResult(targets, copiedChests, copiedPickups, errors);
        }

        public static TreasureChest CopyChest(ChestContentTable table, RoomChestSite site, TreasureChest original,
            int roomContentSeed, out string error)
        {
            if (table == null || site == null || original == null || !original.IsClosed)
            {
                error = "Only an unopened chest in a room with chest support can be copied.";
                return null;
            }

            int number = 1;
            while (site.State.GetChest(CopyChestId(number)) != null) number++;
            Transform room = site.Room.transform;
            Vector2 preferred = room.InverseTransformPoint(original.transform.position);
            List<Vector2> occupied = RoomChestSpawner.OccupiedLocal(site);
            if (!ChestPlacement.TryFindSafeLocalPosition(site.Template, site.Room.gameObject, preferred, occupied,
                    site.AvoidLocal, ChestPlacement.MinimumChestSpacing, out Vector2 local, out _) &&
                !ChestPlacement.TryFindSafeLocalPosition(site.Template, site.Room.gameObject, preferred, occupied,
                    site.AvoidLocal, ChestPlacement.ChestWorldSize, out local, out error))
            {
                return null;
            }

            return RoomChestSpawner.Spawn(table, site, CopyChestId(number), original.Kind,
                FloorGenerator.DeriveSeed(roomContentSeed, number, CopySeedSalt), local, out error);
        }

        public static string CopyChestId(int number) => $"{CopyChestPrefix}{number:00}";

        private static string CopyName(string originalName)
        {
            const string prefix = "Melune Copy ";
            return originalName.StartsWith(prefix, StringComparison.Ordinal) ? originalName : prefix + originalName;
        }

        // The first ring point clear of chests, obstacles, pits, other pickups, the movement bounds and copies placed
        // by this use; the original's own spot when none is free, where physics separates the two.
        private static Vector2 FindPickupPoint(Vector2 origin, Transform room, Rect? bounds, List<Vector2> taken)
        {
            float clearance = ChestPlacement.MaximumPickupRadius;
            Rect? inner = bounds.HasValue ? RoomObstacleLayout.Expand(bounds.Value, -clearance) : null;
            for (int step = 0; step < PickupRingSteps; step++)
            {
                float angle = step * Mathf.PI * 2f / PickupRingSteps;
                Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * PickupCopyDistance;
                if (inner.HasValue && room != null && !inner.Value.Contains(room.InverseTransformPoint(candidate)))
                    continue;
                if (Physics2D.OverlapCircle(candidate, clearance, PickupBlockMask) != null) continue;
                bool blocked = false;
                foreach (Vector2 other in taken)
                    blocked |= (candidate - other).sqrMagnitude < 4f * clearance * clearance;
                if (!blocked) return candidate;
            }

            return origin;
        }
    }
}
