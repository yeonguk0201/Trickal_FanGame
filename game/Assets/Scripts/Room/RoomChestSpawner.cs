using System;
using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // The built room a chest goes into: its Template for placement, its Run state and the content root that is
    // destroyed with the floor.
    public sealed class RoomChestSite
    {
        public RoomChestSite(RoomPrefab room, RoomTemplateDefinition template, RoomRunState state, RunProgress progress,
            Transform player = null)
        {
            Player = player;
            Room = room != null ? room : throw new ArgumentNullException(nameof(room));
            Template = template != null ? template : throw new ArgumentNullException(nameof(template));
            State = state ?? throw new ArgumentNullException(nameof(state));
            Progress = progress != null ? progress : throw new ArgumentNullException(nameof(progress));
        }

        public RoomPrefab Room { get; }
        public RoomTemplateDefinition Template { get; }
        public RoomRunState State { get; }
        public RunProgress Progress { get; }
        // A new chest never appears on top of the player, who is usually still in the room when it clears.
        public Transform Player { get; }
        public Vector2? AvoidLocal => Player != null ? Room.transform.InverseTransformPoint(Player.position) : null;
        public Transform Parent => Room.Node != null && Room.Node.ContentRoot != null
            ? Room.Node.ContentRoot.transform
            : Room.transform;

        // The old clear drop fell at the room controller; chests prefer the same point.
        public Vector2 PreferredLocal => Room.Controller != null
            ? (Vector2)Room.transform.InverseTransformPoint(Room.Controller.transform.position)
            : Vector2.zero;
    }

    // Chest-1: puts a chest into a room at a safe position and, when it opens, drops its rolled contents at free points
    // around its solid body as ordinary floor pickups (Chest-2: a golden special reward is an ordinary ItemPickup). The first position is stored in the chest's Run state, so a
    // rebuilt room puts the chest back on the same spot. An opened chest from an earlier build never drops again.
    public static class RoomChestSpawner
    {
        public const string DevelopmentChestPrefix = "dev-chest-";
        public const uint DevelopmentSeedSalt = 0x7A3C51E9u;

        public static TreasureChest Spawn(ChestContentTable table, RoomChestSite site, string chestId, ChestKind kind,
            int contentSeed, out string error) =>
            Spawn(table, site, chestId, kind, contentSeed, null, out error);

        // placedLocal skips the placement search for a chest placed for the first time (Jjangsem-1 copies pick their
        // own spot); a chest with a recorded position always returns there.
        public static TreasureChest Spawn(ChestContentTable table, RoomChestSite site, string chestId, ChestKind kind,
            int contentSeed, Vector2? placedLocal, out string error)
        {
            if (table == null)
            {
                error = "A chest requires a chest content table.";
                return null;
            }

            if (!table.TryValidate(out error)) return null;

            if (site == null)
            {
                error = "A chest requires a room site.";
                return null;
            }

            ChestRunState existing = site.State.GetChest(chestId);
            if (existing != null && existing.IsDiscarded)
            {
                error = $"Chest '{chestId}' left with its floor.";
                return null;
            }

            Vector2 local;
            if (existing != null && existing.HasPosition) local = existing.LocalPosition;
            else if (placedLocal.HasValue) local = placedLocal.Value;
            else if (!ChestPlacement.TryFindSafeLocalPosition(site.Template, site.Room.gameObject, site.PreferredLocal,
                         OccupiedLocal(site), site.AvoidLocal, out local, out error))
            {
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(table.ChestPrefab.gameObject,
                site.Room.transform.TransformPoint(local), Quaternion.identity, site.Parent);
            instance.name = $"Chest {kind} - {site.State.RoomId} {chestId}";
            TreasureChest chest = instance.GetComponent<TreasureChest>();
            try
            {
                chest.Configure(chestId, kind);
                bool isClosed = chest.Bind(site.State, site.Progress);
                site.State.TryRecordChestPosition(chestId, local);
                site.State.TryRecordChestContentSeed(chestId, contentSeed);
                if (isClosed)
                {
                    ChestContents contents = table.RollContents(contentSeed, kind);
                    chest.Opened += (opened, _) => SpawnContents(table, opened, contents, site);
                }
            }
            catch (Exception exception)
            {
                DestroyObject(instance);
                error = exception.Message;
                return null;
            }

            error = null;
            return chest;
        }

        // Development panel: an extra chest of a chosen kind in the current room, recorded in the room state like
        // any chest, so it opens once and leaves with the floor.
        public static TreasureChest SpawnDevelopmentChest(ChestContentTable table, RoomChestSite site,
            int roomContentSeed, ChestKind kind, out string error)
        {
            if (site == null)
            {
                error = "A development chest requires a room site.";
                return null;
            }

            int number = 1;
            while (site.State.GetChest($"{DevelopmentChestPrefix}{number:00}") != null) number++;
            return Spawn(table, site, $"{DevelopmentChestPrefix}{number:00}", kind,
                FloorGenerator.DeriveSeed(roomContentSeed, number, DevelopmentSeedSalt), out error);
        }

        public static List<GameObject> SpawnContents(ChestContentTable table, TreasureChest chest,
            ChestContents contents, RoomChestSite site)
        {
            List<GameObject> spawned = new();
            if (table == null || chest == null || contents == null || site == null) return spawned;
            // Chest-2: the special reward skips artifacts the opening player already holds at their stack limit.
            ItemDefinition special = table.ResolveSpecialReward(contents,
                site.Player != null ? site.Player.GetComponentInParent<PlayerInventory>() : null);
            int total = contents.Consumables.Count + (contents.Spell != null ? 1 : 0) + (special != null ? 1 : 0);
            Transform room = site.Room.transform;
            RoomObstacleLayout.TryCollectFootprints(site.Room.gameObject, out List<RoomObstacleFootprint> footprints,
                out _);
            List<Vector2> otherChests = new();
            foreach (TreasureChest other in site.Parent.GetComponentsInChildren<TreasureChest>(false))
                if (other != chest) otherChests.Add(room.InverseTransformPoint(other.transform.position));
            ChestPlacement.TryFindContentPositions(site.Template.Profile.MovementBounds, footprints,
                room.InverseTransformPoint(chest.transform.position), otherChests, total,
                out List<Vector2> positions);
            for (int index = 0; index < total; index++)
            {
                Vector3 position = room.TransformPoint(positions[index]);
                if (index < contents.Consumables.Count)
                {
                    ResourceDropEntry entry = contents.Consumables[index];
                    GameObject pickup = UnityEngine.Object.Instantiate(entry.Prefab, position, Quaternion.identity,
                        site.Parent);
                    pickup.name = $"Chest Drop {entry.DropId} - {site.State.RoomId} {chest.ChestId}";
                    if (pickup.TryGetComponent(out RunResourcePickup resourcePickup))
                        resourcePickup.BindRunProgress(site.Progress);
                    spawned.Add(pickup);
                    continue;
                }

                if (special != null && index == total - 1)
                {
                    ItemPickup artifact = UnityEngine.Object.Instantiate(table.ArtifactPickupPrefab, position,
                        Quaternion.identity, site.Parent);
                    artifact.name = $"Chest Artifact {special.ItemId} - {site.State.RoomId} {chest.ChestId}";
                    artifact.Configure(special);
                    spawned.Add(artifact.gameObject);
                    continue;
                }

                SingleUseItemPickup spell = UnityEngine.Object.Instantiate(table.SpellPickupPrefab, position,
                    Quaternion.identity, site.Parent);
                spell.name = $"Chest Spell {contents.Spell.ItemId} - {site.State.RoomId} {chest.ChestId}";
                // The player is touching the chest, so a full slot does not swap until they step off and back on.
                spell.Configure(contents.Spell, $"{site.State.RoomId}-{chest.ChestId}-spell", true);
                spawned.Add(spell.gameObject);
            }

            return spawned;
        }

        internal static List<Vector2> OccupiedLocal(RoomChestSite site)
        {
            List<Vector2> occupied = new();
            foreach (TreasureChest other in site.Parent.GetComponentsInChildren<TreasureChest>(false))
                occupied.Add(site.Room.transform.InverseTransformPoint(other.transform.position));
            return occupied;
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
