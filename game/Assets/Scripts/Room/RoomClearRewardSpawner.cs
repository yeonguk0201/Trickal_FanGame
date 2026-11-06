using System;
using System.Collections.Generic;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Rolls the room's clear reward once per Run. The roll comes from the room content seed, and the room state records
    // that it happened, so a revisit or floor reload never rolls or drops again. Chest-1: with a chest content table
    // the reward is a chest instead of the old single-pickup drop (never both); a rebuilt room restores its chest.
    [DisallowMultipleComponent]
    public sealed class RoomClearRewardSpawner : MonoBehaviour
    {
        public const uint DropSeedSalt = 0x5BD1E995u;
        public const uint ChestSeedSalt = 0x1B873593u;
        public const string ClearChestId = "chest-clear";

        private ResourceDropTable dropTable;
        private Transform rewardPoint;
        private Transform rewardParent;
        private RoomRunState runState;
        private RunProgress runProgress;
        private int dropSeed;
        private ChestContentTable chestTable;
        private RoomChestSite chestSite;
        private int chestSeed;

        public bool HasRolled => runState != null && runState.HasGrantedClearReward;
        public GameObject LastSpawnedReward { get; private set; }
        public ResourceDropTable DropTable => dropTable;
        public int DropSeed => dropSeed;
        public ChestContentTable ChestTable => chestTable;
        public int ChestSeed => chestSeed;
        public TreasureChest LastSpawnedChest { get; private set; }
        // Drop-0: the key or bomb pickups the pity counter guaranteed on this room's clear.
        public IReadOnlyList<GameObject> LastPityDrops => lastPityDrops;
        private readonly List<GameObject> lastPityDrops = new();
        public const float PityDropSpacing = 0.9f;
        public event Action<GameObject> RewardSpawned;

        public static int DeriveDropSeed(int roomContentSeed)
        {
            return FloorGenerator.DeriveSeed(roomContentSeed, 0, DropSeedSalt);
        }

        public static int DeriveChestSeed(int roomContentSeed)
        {
            return FloorGenerator.DeriveSeed(roomContentSeed, 0, ChestSeedSalt);
        }

        public void Configure(ResourceDropTable configuredTable, Transform configuredPoint,
            Transform configuredParent, RoomRunState configuredState, int configuredDropSeed,
            RunProgress configuredProgress = null)
        {
            dropTable = configuredTable;
            rewardPoint = configuredPoint;
            rewardParent = configuredParent;
            runState = configuredState;
            dropSeed = configuredDropSeed;
            runProgress = configuredProgress;
            chestTable = null;
            chestSite = null;
            chestSeed = 0;
            LastSpawnedReward = null;
            LastSpawnedChest = null;
        }

        // Chest mode. A chest already rolled for this room in the Run is rebuilt as it was (opened or closed); a
        // chest that left with its floor stays gone.
        public void ConfigureChest(ChestContentTable configuredTable, RoomChestSite configuredSite,
            int configuredChestSeed)
        {
            dropTable = null;
            dropSeed = 0;
            chestTable = configuredTable;
            chestSite = configuredSite;
            chestSeed = configuredChestSeed;
            runState = configuredSite?.State;
            runProgress = configuredSite?.Progress;
            rewardParent = configuredSite?.Parent;
            rewardPoint = configuredSite?.Room.Controller != null ? configuredSite.Room.Controller.transform : null;
            LastSpawnedReward = null;
            LastSpawnedChest = null;

            if (chestTable == null || runState == null) return;
            // Jjangsem-1: chests added to the room later (멜룬카드 copies, development chests) come back with their own
            // recorded seed, the same way as the clear chest.
            foreach (ChestRunState existing in new List<ChestRunState>(runState.Chests))
            {
                bool isClearChest = existing.ChestId == ClearChestId;
                if (existing.IsDiscarded || (!isClearChest && !existing.HasContentSeed)) continue;
                TreasureChest restored = RoomChestSpawner.Spawn(chestTable, chestSite, existing.ChestId, existing.Kind,
                    isClearChest ? chestSeed : existing.ContentSeed, out string error);
                if (restored == null)
                    Debug.LogError($"Room {runState.RoomId} could not rebuild chest {existing.ChestId}. {error}", this);
            }
        }

        // Returns true only when this call spawned a drop; a roll that drops nothing still uses up the room's roll.
        public bool TrySpawn()
        {
            if (chestTable != null) return TrySpawnChest();
            if (dropTable == null || rewardPoint == null || runState == null ||
                !runState.TryMarkClearRewardGranted())
            {
                return false;
            }

            bool rolled = dropTable.TryRoll(dropSeed, out ResourceDropEntry entry) && entry.Prefab != null;
            SpawnPityDrops(dropTable.Entries);
            if (!rolled)
            {
                return false;
            }

            GameObject reward = Instantiate(entry.Prefab, rewardPoint.position, Quaternion.identity, rewardParent);
            reward.name = $"Clear Drop {entry.DropId} - {runState.RoomId}";
            if (runProgress != null && reward.TryGetComponent(out RunResourcePickup resourcePickup))
            {
                resourcePickup.BindRunProgress(runProgress);
                PermanentElif.TryConvertSpawnedGold(reward, runProgress.RunSeed, runProgress.HasRunSeed,
                    runState.RoomId, "clear-drop", 0);
            }

            LastSpawnedReward = reward;
            RewardSpawned?.Invoke(reward);
            return true;
        }

        private bool TrySpawnChest()
        {
            if (chestSite == null || runState == null || !runState.TryMarkClearRewardGranted()) return false;
            if (!chestTable.TryRollChest(chestSeed, out ChestKind kind))
            {
                SpawnPityDrops(chestTable.Consumables);
                return false;
            }

            TreasureChest chest = RoomChestSpawner.Spawn(chestTable, chestSite, ClearChestId, kind, chestSeed,
                out string error);
            // After the chest, so the guaranteed pickups keep clear of it.
            SpawnPityDrops(chestTable.Consumables);
            if (chest == null)
            {
                Debug.LogError($"Room {runState.RoomId} could not place its {kind} chest. {error}", this);
                return false;
            }

            LastSpawnedChest = chest;
            LastSpawnedReward = chest.gameObject;
            RewardSpawned?.Invoke(chest.gameObject);
            return true;
        }
        // Drop-0: counts this first clear for the pity counter and drops the key or bomb it guarantees. The pickup
        // Prefab is the source table's own key or bomb candidate. The room's clear reward is granted once, so a
        // revisit never counts or drops again.
        private void SpawnPityDrops(IReadOnlyList<ResourceDropEntry> candidates)
        {
            lastPityDrops.Clear();
            if (runProgress == null) return;
            runProgress.Pity.RecordCombatClear(out bool key, out bool bomb);
            if (key) SpawnPityDrop(candidates, RunResourceType.Key);
            if (bomb) SpawnPityDrop(candidates, RunResourceType.Bomb);
        }

        private void SpawnPityDrop(IReadOnlyList<ResourceDropEntry> candidates, RunResourceType type)
        {
            ResourceDropEntry entry = FindResourceEntry(candidates, type);
            if (entry == null)
            {
                Debug.LogError($"Room {runState.RoomId} has no {type} candidate for its guaranteed drop.", this);
                return;
            }

            Vector3 position = PityDropPosition();
            GameObject pickup = Instantiate(entry.Prefab, position, Quaternion.identity, rewardParent);
            pickup.name = $"Pity Drop {entry.DropId} - {runState.RoomId}";
            if (pickup.TryGetComponent(out RunResourcePickup resourcePickup)) resourcePickup.BindRunProgress(runProgress);
            lastPityDrops.Add(pickup);
        }

        public static ResourceDropEntry FindResourceEntry(IReadOnlyList<ResourceDropEntry> candidates,
            RunResourceType type)
        {
            if (candidates == null) return null;
            foreach (ResourceDropEntry candidate in candidates)
                if (candidate != null && candidate.Prefab != null &&
                    candidate.Prefab.TryGetComponent(out RunResourcePickup pickup) && pickup.ResourceType == type)
                    return candidate;
            return null;
        }

        // A safe spot away from the player, chests and earlier guaranteed pickups when the room has a chest site;
        // otherwise beside the old clear drop point.
        private Vector3 PityDropPosition()
        {
            if (chestSite != null)
            {
                List<Vector2> occupied = RoomChestSpawner.OccupiedLocal(chestSite);
                foreach (GameObject earlier in lastPityDrops)
                    occupied.Add(chestSite.Room.transform.InverseTransformPoint(earlier.transform.position));
                if (ChestPlacement.TryFindSafeLocalPosition(chestSite.Template, chestSite.Room.gameObject,
                        chestSite.PreferredLocal, occupied, chestSite.AvoidLocal, PityDropSpacing,
                        out Vector2 local, out _))
                    return chestSite.Room.transform.TransformPoint(local);
            }

            Vector3 origin = rewardPoint != null ? rewardPoint.position : transform.position;
            return origin + new Vector3(PityDropSpacing * (lastPityDrops.Count + 1), 0f, 0f);
        }
    }
}
