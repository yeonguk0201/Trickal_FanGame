using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // Default executor for the player's spell slot. Each single-use effect type is connected here; an item with any
    // effect that is not connected cannot be used, so it is never consumed without its effect.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(PlayerSP))]
    public sealed class PlayerSingleUseEffects : MonoBehaviour, ISingleUseItemExecutor
    {
        private const float TickTolerance = 0.0001f;

        private Health health;
        private PlayerSP playerSP;
        private PlayerStats playerStats;
        private PlayerSpellSlot spellSlot;

        // Meditation: SP half units over time. Game time only advances while unpaused, and the effect follows the
        // player across rooms and floors; death or the end of the Run stops it.
        private int regenerationHalvesPerTick;
        private float regenerationInterval;
        private int regenerationTotalTicks;
        private int regenerationDeliveredTicks;
        private float regenerationElapsed;

        // 저놈 잡아라: basic attack bonus bound to the room where it was used. Leaving that room, death or the end of
        // the Run removes it, and nothing brings it back on a revisit.
        private RunProgress roomAttackProgress;
        private string roomAttackRoomId;
        private float roomAttackDamagePercent;

        public bool IsRoomAttackBoostActive => !string.IsNullOrEmpty(roomAttackRoomId);
        public string RoomAttackBoostRoomId => roomAttackRoomId;
        public float RoomAttackDamagePercent => IsRoomAttackBoostActive ? roomAttackDamagePercent : 0f;

        public bool IsRegenerating => regenerationDeliveredTicks < regenerationTotalTicks;
        public int RegenerationTotalTicks => regenerationTotalTicks;
        public int RegenerationDeliveredTicks => regenerationDeliveredTicks;
        public float RegenerationRemainingSeconds => IsRegenerating
            ? Mathf.Max(0f, regenerationTotalTicks * regenerationInterval - regenerationElapsed)
            : 0f;

        private void Awake()
        {
            health = GetComponent<Health>();
            playerSP = GetComponent<PlayerSP>();
            playerStats = GetComponent<PlayerStats>();
            spellSlot = GetComponent<PlayerSpellSlot>();
        }

        private void Update()
        {
            AdvanceRegeneration(Time.deltaTime);
            RefreshRoomAttackBoost();
        }

        private void OnDisable()
        {
            ClearRoomAttackBoost();
        }

        public bool CanExecute(ItemDefinition definition, out string reason)
        {
            if (definition == null || !definition.IsSingleUse)
            {
                reason = "Only single-use items can be used from the spell slot.";
                return false;
            }

            EnsureReferences();
            if (definition.Effects.Count == 0)
            {
                reason = $"{definition.DisplayName} has no single-use effect connected yet.";
                return false;
            }

            foreach (ItemEffectEntry effect in definition.Effects)
            {
                if (!CanApply(effect, out reason))
                {
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        public void Execute(ItemDefinition definition)
        {
            if (!CanExecute(definition, out string reason))
            {
                throw new InvalidOperationException($"[PlayerSingleUseEffects] {definition?.ItemId}: {reason}");
            }

            foreach (ItemEffectEntry effect in definition.Effects)
            {
                Apply(effect);
            }
        }

        // Advances the meditation timer. Paused game time (time scale 0) never advances it.
        public int AdvanceRegeneration(float deltaSeconds)
        {
            if (!IsRegenerating)
            {
                return 0;
            }

            EnsureReferences();
            RunProgress progress = spellSlot != null ? spellSlot.Progress : null;
            if (health == null || health.IsDead || playerSP == null || progress == null ||
                progress.IsProgressionStopped)
            {
                StopRegeneration();
                return 0;
            }

            if (deltaSeconds <= 0f || Time.timeScale <= 0f)
            {
                return 0;
            }

            regenerationElapsed += deltaSeconds;
            int dueTicks = Mathf.Min(regenerationTotalTicks,
                Mathf.FloorToInt(regenerationElapsed / regenerationInterval + TickTolerance));
            int delivered = 0;
            while (regenerationDeliveredTicks < dueTicks)
            {
                regenerationDeliveredTicks++;
                delivered++;
                // A full gauge wastes the tick; the timer keeps running so spent SP can refill before it ends.
                for (int i = 0; i < regenerationHalvesPerTick; i++) playerSP.TryAddHalf();
            }

            return delivered;
        }

        // Ends the room attack bonus once the player is no longer in the room it was used in.
        public void RefreshRoomAttackBoost()
        {
            if (!IsRoomAttackBoostActive) return;

            EnsureReferences();
            if (health == null || health.IsDead || roomAttackProgress == null ||
                roomAttackProgress.IsProgressionStopped ||
                !string.Equals(CurrentRoomId(roomAttackProgress), roomAttackRoomId, StringComparison.Ordinal))
            {
                ClearRoomAttackBoost();
            }
        }

        private bool CanApply(ItemEffectEntry effect, out string reason)
        {
            if (effect == null)
            {
                reason = "Invalid single-use effect: missing.";
                return false;
            }

            if (!effect.TryValidate(out string error))
            {
                reason = "Invalid single-use effect: " + error;
                return false;
            }

            switch (effect.EffectType)
            {
                case ItemEffectType.RestoreAllSPWithOvercharge:
                    if (playerSP == null)
                    {
                        reason = "The player has no SP.";
                        return false;
                    }

                    if (playerSP.CurrentSP >= playerSP.MaxSP + effect.IntegerAmount)
                    {
                        reason = "SP is already full including overcharge.";
                        return false;
                    }

                    reason = string.Empty;
                    return true;
                case ItemEffectType.RegenerateSPHalvesOverTime:
                    if (playerSP == null)
                    {
                        reason = "The player has no SP.";
                        return false;
                    }

                    reason = string.Empty;
                    return true;
                case ItemEffectType.CurrentRoomBasicAttackDamagePercent:
                    if (playerStats == null)
                    {
                        reason = "The player has no stats.";
                        return false;
                    }

                    if (!IsInUnclearedCombatRoom(out reason)) return false;
                    reason = string.Empty;
                    return true;
                default:
                    reason = $"{effect.EffectType} is not a connected single-use effect.";
                    return false;
            }
        }

        private void Apply(ItemEffectEntry effect)
        {
            switch (effect.EffectType)
            {
                case ItemEffectType.RestoreAllSPWithOvercharge:
                    playerSP.TryFillWithOvercharge(effect.IntegerAmount);
                    break;
                case ItemEffectType.RegenerateSPHalvesOverTime:
                    // Using it again while active restarts the full duration instead of stacking.
                    regenerationHalvesPerTick = effect.IntegerAmount;
                    regenerationInterval = effect.IntervalSeconds;
                    regenerationTotalTicks = Mathf.Max(1,
                        Mathf.FloorToInt(effect.DurationSeconds / effect.IntervalSeconds + TickTolerance));
                    regenerationDeliveredTicks = 0;
                    regenerationElapsed = 0f;
                    Debug.Log($"[PlayerSingleUseEffects] SP regeneration started: {regenerationTotalTicks} ticks " +
                              $"every {regenerationInterval}s.", this);
                    break;
                case ItemEffectType.CurrentRoomBasicAttackDamagePercent:
                    StartRoomAttackBoost(effect.Magnitude);
                    break;
            }
        }

        // Another use in the same room adds to the bonus; it still ends when the room is left.
        private void StartRoomAttackBoost(float percent)
        {
            RunProgress progress = spellSlot.Progress;
            string roomId = CurrentRoomId(progress);
            if (!string.Equals(roomAttackRoomId, roomId, StringComparison.Ordinal)) ClearRoomAttackBoost();

            if (roomAttackProgress != progress)
            {
                if (roomAttackProgress != null) roomAttackProgress.RoomChanged -= HandleRoomChanged;
                roomAttackProgress = progress;
                roomAttackProgress.RoomChanged += HandleRoomChanged;
            }

            roomAttackRoomId = roomId;
            roomAttackDamagePercent += percent;
            playerStats.SetSingleUseRoomAttackDamagePercent(roomAttackDamagePercent);
            Debug.Log($"[PlayerSingleUseEffects] Basic attack +{roomAttackDamagePercent:P0} in {roomId}.", this);
        }

        private void HandleRoomChanged(int _, int __)
        {
            RefreshRoomAttackBoost();
        }

        private void ClearRoomAttackBoost()
        {
            if (roomAttackProgress != null) roomAttackProgress.RoomChanged -= HandleRoomChanged;
            bool wasActive = IsRoomAttackBoostActive;
            roomAttackProgress = null;
            roomAttackRoomId = null;
            roomAttackDamagePercent = 0f;
            if (playerStats != null) playerStats.SetSingleUseRoomAttackDamagePercent(0f);
            if (wasActive) Debug.Log("[PlayerSingleUseEffects] Room basic attack bonus ended.", this);
        }

        // Room-bound effects need a room that is still being fought; a safe or cleared room would waste the item.
        private bool IsInUnclearedCombatRoom(out string reason)
        {
            RunProgress progress = spellSlot != null ? spellSlot.Progress : null;
            GeneratedRoomNode node = FindCurrentNode(progress);
            if (node == null)
            {
                reason = "The player is not in a generated room.";
                return false;
            }

            if (node.Role is not (GeneratedRoomRole.Intermediate or GeneratedRoomRole.Boss))
            {
                reason = "It can only be used in a combat room.";
                return false;
            }

            RoomRunState state = progress.GetRoomState(node.RoomId);
            if (state == null || state.IsCleared)
            {
                reason = "This room is already cleared.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static string CurrentRoomId(RunProgress progress)
        {
            return progress != null && progress.CurrentFloor > 0 && progress.CurrentRoom > 0
                ? FloorGenerator.BuildRoomId(progress.CurrentFloor, progress.CurrentRoom)
                : null;
        }

        private static GeneratedRoomNode FindCurrentNode(RunProgress progress)
        {
            string roomId = CurrentRoomId(progress);
            if (roomId == null || progress.GeneratedGraph == null) return null;
            foreach (GeneratedRoomNode node in progress.GeneratedGraph.Nodes)
            {
                if (node != null && string.Equals(node.RoomId, roomId, StringComparison.Ordinal)) return node;
            }

            return null;
        }

        private void StopRegeneration()
        {
            regenerationDeliveredTicks = regenerationTotalTicks;
        }

        private void EnsureReferences()
        {
            if (health == null || playerSP == null || playerStats == null || spellSlot == null) Awake();
        }
    }
}
