using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
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
        // 아멜리아의 러브레터: hearts land this far from the player, at the first free points around them.
        public const float HeartDropDistance = 1.1f;
        private const float HeartDropClearance = 0.35f;
        private static readonly Vector2[] HeartDropDirections =
        {
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(1f, 1f).normalized, new Vector2(-1f, 1f).normalized,
            new Vector2(1f, -1f).normalized, new Vector2(-1f, -1f).normalized,
        };

        // 랜덤코인: inclusive minimum and maximum. Replaced in verification to pin the amount.
        private static Func<int, int, int> goldRollProvider = DefaultGoldRoll;

        [SerializeField] private RoomGraphController roomGraph;
        [SerializeField] private HealthPickup heartPickupPrefab;

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

        // 저놈 잡아라 and 막판 스퍼트: bonuses bound to the room where they were used. Leaving that room, death or the
        // end of the Run removes them all, and nothing brings them back on a revisit.
        private RunProgress roomBoostProgress;
        private string roomBoostRoomId;
        private float roomAttackDamagePercent;
        private float roomAttackSpeedPercent;
        private float roomMoveSpeedPercent;
        private float roomCriticalDamagePercent;
        private float roomCriticalChancePercent;

        // 빅우드의 열매: while the window runs every hit is reduced, and each hit that still costs HP is healed back
        // after a delay with a bonus. Game time only, kept across rooms and floors. Hits after the window are not
        // healed, heals already pending still arrive, and death or the end of the Run drops everything.
        private float recoveryRemainingSeconds;
        private float recoveryDelaySeconds;
        private int recoveryBonusUnits;
        private bool isRecoverySubscribed;
        private readonly List<PendingRecovery> pendingRecoveries = new();

        private struct PendingRecovery
        {
            public float RemainingSeconds;
            public float Amount;
        }

        public RoomGraphController RoomGraph => roomGraph;
        public HealthPickup HeartPickupPrefab => heartPickupPrefab;
        public int LastGoldGained { get; private set; }
        public float RoomCriticalDamagePercent => IsRoomBoostActive ? roomCriticalDamagePercent : 0f;
        public float RoomCriticalChancePercent => IsRoomBoostActive ? roomCriticalChancePercent : 0f;
        public bool IsDamageRecoveryActive => recoveryRemainingSeconds > 0f;
        public float DamageRecoveryRemainingSeconds => Mathf.Max(0f, recoveryRemainingSeconds);
        public int PendingRecoveryCount => pendingRecoveries.Count;
        public bool IsRoomBoostActive => !string.IsNullOrEmpty(roomBoostRoomId);
        public string RoomBoostRoomId => roomBoostRoomId;
        public float RoomAttackDamagePercent => IsRoomBoostActive ? roomAttackDamagePercent : 0f;
        public float RoomAttackSpeedPercent => IsRoomBoostActive ? roomAttackSpeedPercent : 0f;
        public float RoomMoveSpeedPercent => IsRoomBoostActive ? roomMoveSpeedPercent : 0f;

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

        public void Configure(RoomGraphController configuredRoomGraph)
        {
            roomGraph = configuredRoomGraph;
        }

        public void ConfigureHeartPickup(HealthPickup configuredHeartPickupPrefab)
        {
            heartPickupPrefab = configuredHeartPickupPrefab;
        }

        public static void SetGoldRollProviderForTesting(Func<int, int, int> provider)
        {
            goldRollProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public static void ResetGoldRollProvider()
        {
            goldRollProvider = DefaultGoldRoll;
        }

        private static int DefaultGoldRoll(int minimum, int maximum) =>
            UnityEngine.Random.Range(minimum, maximum + 1);

        private void Update()
        {
            AdvanceRegeneration(Time.deltaTime);
            AdvanceDamageRecovery(Time.deltaTime);
            RefreshRoomBoost();
        }

        private void OnDisable()
        {
            ClearRoomBoost();
            StopDamageRecovery();
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

        // Advances the 빅우드의 열매 window and its pending heals. Returns the HP healed by this step.
        public float AdvanceDamageRecovery(float deltaSeconds)
        {
            if (!IsDamageRecoveryActive && pendingRecoveries.Count == 0)
            {
                return 0f;
            }

            EnsureReferences();
            RunProgress progress = spellSlot != null ? spellSlot.Progress : null;
            if (health == null || health.IsDead || progress == null || progress.IsProgressionStopped)
            {
                StopDamageRecovery();
                return 0f;
            }

            if (deltaSeconds <= 0f || Time.timeScale <= 0f)
            {
                return 0f;
            }

            if (IsDamageRecoveryActive)
            {
                recoveryRemainingSeconds -= deltaSeconds;
                if (recoveryRemainingSeconds <= TickTolerance) EndDamageRecoveryWindow();
            }

            float healed = 0f;
            for (int index = pendingRecoveries.Count - 1; index >= 0; index--)
            {
                PendingRecovery pending = pendingRecoveries[index];
                pending.RemainingSeconds -= deltaSeconds;
                if (pending.RemainingSeconds > TickTolerance)
                {
                    pendingRecoveries[index] = pending;
                    continue;
                }

                pendingRecoveries.RemoveAt(index);
                healed += health.Heal(pending.Amount);
            }

            return healed;
        }

        // Ends the room bonuses once the player is no longer in the room they were used in.
        public void RefreshRoomBoost()
        {
            if (!IsRoomBoostActive) return;

            EnsureReferences();
            if (health == null || health.IsDead || roomBoostProgress == null ||
                roomBoostProgress.IsProgressionStopped ||
                !string.Equals(CurrentRoomId(roomBoostProgress), roomBoostRoomId, StringComparison.Ordinal))
            {
                ClearRoomBoost();
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

                    return IsInUnclearedCombatRoom(false, out reason);
                case ItemEffectType.CurrentBossRoomSpeedPercent:
                    if (playerStats == null)
                    {
                        reason = "The player has no stats.";
                        return false;
                    }

                    return IsInUnclearedCombatRoom(true, out reason);
                case ItemEffectType.EscapeToFloorStartRoom:
                    return CanEscape(out reason, out _, out _);
                case ItemEffectType.ReduceAndRecoverDamageTaken:
                case ItemEffectType.GainShield:
                    if (health == null || health.IsDead)
                    {
                        reason = "The player has no HP to protect.";
                        return false;
                    }

                    reason = string.Empty;
                    return true;
                case ItemEffectType.SpawnHealthPickups:
                    if (heartPickupPrefab == null)
                    {
                        reason = "No heart pickup is configured.";
                        return false;
                    }

                    reason = string.Empty;
                    return true;
                case ItemEffectType.GainRandomGold:
                    // A full wallet would waste the coin.
                    if (spellSlot == null || spellSlot.Progress == null ||
                        !spellSlot.Progress.CanAcceptResource(RunResourceType.Gold))
                    {
                        reason = "Gold is already full.";
                        return false;
                    }

                    reason = string.Empty;
                    return true;
                case ItemEffectType.CurrentRoomCriticalBonus:
                    if (playerStats == null)
                    {
                        reason = "The player has no stats.";
                        return false;
                    }

                    return IsInUnclearedCombatRoom(false, out reason);
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
                    StartRoomBoost(effect.Magnitude, 0f, 0f, 0f, 0f);
                    break;
                case ItemEffectType.CurrentBossRoomSpeedPercent:
                    StartRoomBoost(0f, effect.Magnitude, effect.SecondaryMagnitude, 0f, 0f);
                    break;
                case ItemEffectType.CurrentRoomCriticalBonus:
                    StartRoomBoost(0f, 0f, 0f, effect.Magnitude, effect.SecondaryMagnitude);
                    break;
                case ItemEffectType.GainShield:
                    health.AddShield(effect.Magnitude);
                    break;
                case ItemEffectType.SpawnHealthPickups:
                    SpawnHeartsAroundPlayer(effect.IntegerAmount);
                    break;
                case ItemEffectType.GainRandomGold:
                    int maximum = Mathf.RoundToInt(effect.Magnitude);
                    int rolled = Mathf.Clamp(goldRollProvider(effect.IntegerAmount, maximum), effect.IntegerAmount,
                        maximum);
                    // The wallet limit clips the gain.
                    LastGoldGained = spellSlot.Progress.TryAddResource(RunResourceType.Gold, rolled);
                    Debug.Log($"[PlayerSingleUseEffects] Random coin rolled {rolled}, gained {LastGoldGained} gold.",
                        this);
                    break;
                case ItemEffectType.EscapeToFloorStartRoom:
                    EscapeToStartRoom();
                    break;
                case ItemEffectType.ReduceAndRecoverDamageTaken:
                    // Using it again while active restarts the full window; heals already pending stay.
                    recoveryRemainingSeconds = effect.DurationSeconds;
                    recoveryDelaySeconds = effect.IntervalSeconds;
                    recoveryBonusUnits = effect.IntegerAmount;
                    health.SetIncomingDamageReduction(effect.Magnitude);
                    if (!isRecoverySubscribed)
                    {
                        health.DamageApplied += HandleRecoveryDamage;
                        isRecoverySubscribed = true;
                    }

                    Debug.Log($"[PlayerSingleUseEffects] Damage recovery started for {recoveryRemainingSeconds}s.",
                        this);
                    break;
            }
        }

        // Hearts go to free points around the player, in the current room's content so they leave with the floor.
        // When fewer free points exist than hearts, the rest use the blocked points in order.
        private void SpawnHeartsAroundPlayer(int count)
        {
            RoomGraphController graph = ResolveRoomGraph();
            RoomNode node = graph != null ? graph.CurrentNode : null;
            Transform parent = node != null && node.ContentRoot != null ? node.ContentRoot.transform : null;
            int blockMask = LayerMask.GetMask("Environment", RoomPit.LayerName, HealthPickup.LayerName);
            Vector2 origin = transform.position;

            List<Vector2> points = new();
            List<Vector2> blocked = new();
            foreach (Vector2 direction in HeartDropDirections)
            {
                Vector2 point = origin + direction * HeartDropDistance;
                if (Physics2D.OverlapCircle(point, HeartDropClearance, blockMask) == null) points.Add(point);
                else blocked.Add(point);
            }

            points.AddRange(blocked);
            for (int index = 0; index < count; index++)
            {
                HealthPickup heart = Instantiate(heartPickupPrefab, points[index % points.Count], Quaternion.identity,
                    parent);
                heart.name = $"Love Letter Heart {index + 1}";
            }
        }

        // Only HP actually lost is healed back: a hit the shield absorbed whole leaves nothing to recover.
        private void HandleRecoveryDamage(DamageContext _, float healthDamage, float currentHealth)
        {
            if (!IsDamageRecoveryActive || healthDamage <= 0f || currentHealth <= 0f) return;
            pendingRecoveries.Add(new PendingRecovery
            {
                RemainingSeconds = recoveryDelaySeconds,
                Amount = healthDamage + recoveryBonusUnits,
            });
        }

        private void EndDamageRecoveryWindow()
        {
            recoveryRemainingSeconds = 0f;
            if (health == null) return;
            health.SetIncomingDamageReduction(0f);
            if (!isRecoverySubscribed) return;
            health.DamageApplied -= HandleRecoveryDamage;
            isRecoverySubscribed = false;
        }

        private void StopDamageRecovery()
        {
            EndDamageRecoveryWindow();
            pendingRecoveries.Clear();
        }

        // 그건 내 잔상: move first, then reset the escaped room, so a refused move never leaves a reset room behind.
        private void EscapeToStartRoom()
        {
            if (!CanEscape(out string reason, out RoomNode source, out RoomController sourceRoom))
                throw new InvalidOperationException("[PlayerSingleUseEffects] Escape refused: " + reason);

            RoomNode start = roomGraph.StartingNode;
            if (!roomGraph.TryTeleport(source, start, start.InitialSpawnPosition, GetComponent<PlayerMovement>()))
                throw new InvalidOperationException($"[PlayerSingleUseEffects] Escape to {start.RoomId} failed.");
            if (!sourceRoom.TryAbandonCombat())
                Debug.LogError($"[PlayerSingleUseEffects] {source.RoomId} could not reset after the escape.", this);
            Debug.Log($"[PlayerSingleUseEffects] Escaped {source.RoomId} to {start.RoomId}.", this);
        }

        // Escaping needs a combat or boss room whose fight is running, on the floor the graph is showing.
        private bool CanEscape(out string reason, out RoomNode source, out RoomController sourceRoom)
        {
            source = null;
            sourceRoom = null;
            RunProgress progress = spellSlot != null ? spellSlot.Progress : null;
            RoomGraphController graph = ResolveRoomGraph();
            PlayerMovement player = GetComponent<PlayerMovement>();
            if (graph == null || player == null || progress == null ||
                (graph.Progress != null && graph.Progress != progress))
            {
                reason = "The player is not in a room graph.";
                return false;
            }

            source = graph.CurrentNode;
            RoomNode start = graph.StartingNode;
            GeneratedRoomNode node = FindCurrentNode(progress);
            if (source == null || start == null || node == null ||
                !string.Equals(source.RoomId, node.RoomId, StringComparison.Ordinal))
            {
                reason = "The player is not in a generated room.";
                return false;
            }

            if (source == start || node.Role is not (GeneratedRoomRole.Intermediate or GeneratedRoomRole.Boss))
            {
                reason = "It can only be used in a combat room.";
                return false;
            }

            sourceRoom = FindRoomController(source);
            if (sourceRoom == null || sourceRoom.State != RoomState.Combat || sourceRoom.IsProgressionStopped)
            {
                reason = "It can only be used during combat.";
                return false;
            }

            if (!graph.CanTeleport(source, start, player))
            {
                reason = "The player cannot move between rooms right now.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private RoomGraphController ResolveRoomGraph()
        {
            if (roomGraph == null) roomGraph = FindFirstObjectByType<RoomGraphController>();
            return roomGraph;
        }

        private static RoomController FindRoomController(RoomNode node)
        {
            RoomPrefab prefab = node.GetComponent<RoomPrefab>();
            if (prefab != null && prefab.Controller != null) return prefab.Controller;
            return node.ContentRoot != null ? node.ContentRoot.GetComponentInChildren<RoomController>(true) : null;
        }

        // Another use in the same room adds to the bonuses; they still end when the room is left.
        private void StartRoomBoost(float attackDamage, float attackSpeed, float moveSpeed, float criticalDamage,
            float criticalChance)
        {
            RunProgress progress = spellSlot.Progress;
            string roomId = CurrentRoomId(progress);
            if (!string.Equals(roomBoostRoomId, roomId, StringComparison.Ordinal)) ClearRoomBoost();

            if (roomBoostProgress != progress)
            {
                if (roomBoostProgress != null) roomBoostProgress.RoomChanged -= HandleRoomChanged;
                roomBoostProgress = progress;
                roomBoostProgress.RoomChanged += HandleRoomChanged;
            }

            roomBoostRoomId = roomId;
            roomAttackDamagePercent += attackDamage;
            roomAttackSpeedPercent += attackSpeed;
            roomMoveSpeedPercent += moveSpeed;
            roomCriticalDamagePercent += criticalDamage;
            roomCriticalChancePercent += criticalChance;
            playerStats.SetSingleUseRoomAttackDamagePercent(roomAttackDamagePercent);
            playerStats.SetSingleUseRoomSpeedPercent(roomAttackSpeedPercent, roomMoveSpeedPercent);
            playerStats.SetSingleUseRoomCritical(roomCriticalDamagePercent, roomCriticalChancePercent);
            Debug.Log($"[PlayerSingleUseEffects] Room bonus in {roomId}: basic attack +{roomAttackDamagePercent:P0}, " +
                      $"attack speed +{roomAttackSpeedPercent:P0}, move speed +{roomMoveSpeedPercent:P0}, " +
                      $"critical damage +{roomCriticalDamagePercent:P0}, critical chance " +
                      $"+{roomCriticalChancePercent:P0}.", this);
        }

        private void HandleRoomChanged(int _, int __)
        {
            RefreshRoomBoost();
        }

        private void ClearRoomBoost()
        {
            if (roomBoostProgress != null) roomBoostProgress.RoomChanged -= HandleRoomChanged;
            bool wasActive = IsRoomBoostActive;
            roomBoostProgress = null;
            roomBoostRoomId = null;
            roomAttackDamagePercent = 0f;
            roomAttackSpeedPercent = 0f;
            roomMoveSpeedPercent = 0f;
            roomCriticalDamagePercent = 0f;
            roomCriticalChancePercent = 0f;
            if (playerStats != null)
            {
                playerStats.SetSingleUseRoomAttackDamagePercent(0f);
                playerStats.SetSingleUseRoomSpeedPercent(0f, 0f);
                playerStats.SetSingleUseRoomCritical(0f, 0f);
            }

            if (wasActive) Debug.Log("[PlayerSingleUseEffects] Room bonuses ended.", this);
        }

        // Room-bound effects need a room that is still being fought; a safe or cleared room would waste the item.
        // Boss-only effects (막판 스퍼트) also refuse ordinary combat rooms.
        private bool IsInUnclearedCombatRoom(bool bossOnly, out string reason)
        {
            RunProgress progress = spellSlot != null ? spellSlot.Progress : null;
            GeneratedRoomNode node = FindCurrentNode(progress);
            if (node == null)
            {
                reason = "The player is not in a generated room.";
                return false;
            }

            if (bossOnly && node.Role != GeneratedRoomRole.Boss)
            {
                reason = "It can only be used in a boss room.";
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
