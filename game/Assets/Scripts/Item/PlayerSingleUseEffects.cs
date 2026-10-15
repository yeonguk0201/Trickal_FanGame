using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
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
        // 깜짝상자: an index in [0, count) among the enemies not picked yet. Replaced in verification.
        private static Func<int, int> enemyPickProvider = DefaultEnemyPick;

        [SerializeField] private RoomGraphController roomGraph;
        [SerializeField] private HealthPickup heartPickupPrefab;
        // 멜룬카드: copied chests roll their contents from this table.
        [SerializeField] private ChestContentTable chestContentTable;

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

        // Verification seam: replaces the current room's enemy list (깜짝상자, 파스텔빛).
        private Func<IEnumerable<Health>> enemySourceOverride;

        // 파스텔빛: pulses bound to the room it was used in. Clearing or leaving that room, death or the end of the
        // Run ends them; the first pulse of each kind comes one interval after use.
        private string pulseRoomId;
        private float pulseElapsed;
        private float pulseDamageShare;
        private float pulseDamageInterval;
        private int pulseDamageTotalTicks;
        private int pulseDamageDeliveredTicks;
        private int pulseHealUnits;
        private float pulseHealInterval;
        private int pulseHealTotalTicks;
        private int pulseHealDeliveredTicks;
        private int pulseSPHalves;
        private float pulseSPInterval;
        private int pulseSPTotalTicks;
        private int pulseSPDeliveredTicks;

        // 수상한 물약: the effect sits on the room, not on its enemies. Its timer runs in game time wherever the
        // player is, and enemy hits are reduced only while the player is in that room.
        private string enemyDamageReductionRoomId;
        private float enemyDamageReductionShare;
        private float enemyDamageReductionRemainingSeconds;

        // 이성의 끈: a final damage multiplier bound to the room it was used in, like the room bonuses above.
        private string roomDamageMultiplierRoomId;
        private float roomDamageMultiplier = 1f;

        private readonly List<Health> lastSurpriseTargets = new();

        private struct PendingRecovery
        {
            public float RemainingSeconds;
            public float Amount;
        }

        public RoomGraphController RoomGraph => roomGraph;
        public HealthPickup HeartPickupPrefab => heartPickupPrefab;
        public ChestContentTable ChestContentTable => chestContentTable;
        public RoomDuplicationResult LastDuplication { get; private set; }
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

        public bool IsPulseActive => !string.IsNullOrEmpty(pulseRoomId);
        public string PulseRoomId => pulseRoomId;
        public float PulseRemainingSeconds => IsPulseActive
            ? Mathf.Max(0f, Mathf.Max(pulseDamageTotalTicks * pulseDamageInterval,
                pulseHealTotalTicks * pulseHealInterval, pulseSPTotalTicks * pulseSPInterval) - pulseElapsed)
            : 0f;
        public int PulseDamageDeliveredTicks => pulseDamageDeliveredTicks;
        public int PulseHealDeliveredTicks => pulseHealDeliveredTicks;
        public int PulseSPDeliveredTicks => pulseSPDeliveredTicks;
        public bool IsEnemyDamageReductionActive => enemyDamageReductionRemainingSeconds > 0f;
        public string EnemyDamageReductionRoomId => IsEnemyDamageReductionActive ? enemyDamageReductionRoomId : null;
        public float EnemyDamageReductionRemainingSeconds => Mathf.Max(0f, enemyDamageReductionRemainingSeconds);
        public bool IsRoomDamageMultiplierActive => !string.IsNullOrEmpty(roomDamageMultiplierRoomId);
        public string RoomDamageMultiplierRoomId => roomDamageMultiplierRoomId;
        public float RoomDamageMultiplier => IsRoomDamageMultiplierActive ? roomDamageMultiplier : 1f;
        public IReadOnlyList<Health> LastSurpriseTargets => lastSurpriseTargets;

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

        public void ConfigureChestContents(ChestContentTable configuredChestContentTable)
        {
            chestContentTable = configuredChestContentTable;
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

        public static void SetEnemyPickProviderForTesting(Func<int, int> provider)
        {
            enemyPickProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public static void ResetEnemyPickProvider()
        {
            enemyPickProvider = DefaultEnemyPick;
        }

        private static int DefaultEnemyPick(int count) => UnityEngine.Random.Range(0, count);

        public void ConfigureEnemySourceForVerification(Func<IEnumerable<Health>> source)
        {
            enemySourceOverride = source;
        }

        private void Update()
        {
            AdvanceRegeneration(Time.deltaTime);
            AdvanceDamageRecovery(Time.deltaTime);
            RefreshRoomBoost();
            AdvanceJjangsemRoomEffects(Time.deltaTime);
        }

        private void OnDisable()
        {
            ClearRoomBoost();
            StopDamageRecovery();
            StopPulse();
            StopEnemyDamageReduction();
            ClearRoomDamageMultiplier();
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
                case ItemEffectType.DuplicateRoomChestsAndPickups:
                    if (!TryResolveDuplicationRoom(out Transform contentRoot, out RoomChestSite site, out _,
                            out reason))
                    {
                        return false;
                    }

                    // A room with nothing to copy would waste the card.
                    if (RoomDuplication.CollectTargets(contentRoot, site != null).Count == 0)
                    {
                        reason = "There is no unopened chest or floor consumable to copy in this room.";
                        return false;
                    }

                    reason = string.Empty;
                    return true;
                case ItemEffectType.FreeCurrentShopOffers:
                    // Usable in any shop, even a sold-out or already free one (D5): the card is then simply spent.
                    return TryResolveCurrentShop(out _, out reason);
                case ItemEffectType.CurrentRoomPulseDamage:
                case ItemEffectType.CurrentRoomPulseHeal:
                case ItemEffectType.CurrentRoomPulseSP:
                    if (health == null || health.IsDead || playerStats == null || playerSP == null)
                    {
                        reason = "The player cannot use it now.";
                        return false;
                    }

                    if (!IsInUnclearedCombatRoom(false, out reason)) return false;
                    if (IsPulseActive && string.Equals(pulseRoomId, CurrentRoomId(spellSlot.Progress),
                            StringComparison.Ordinal))
                    {
                        reason = "It is already running in this room.";
                        return false;
                    }

                    return true;
                case ItemEffectType.CurrentRoomEnemyDamageReduction:
                    if (health == null || health.IsDead)
                    {
                        reason = "The player has no HP to protect.";
                        return false;
                    }

                    return IsInUnclearedCombatRoom(false, out reason);
                case ItemEffectType.CurrentRoomEnemyPoison:
                    if (playerStats == null)
                    {
                        reason = "The player has no stats.";
                        return false;
                    }

                    return IsInUnclearedCombatRoom(false, out reason);
                case ItemEffectType.CurrentRoomDamageMultiplier:
                    if (playerStats == null)
                    {
                        reason = "The player has no stats.";
                        return false;
                    }

                    if (!IsInUnclearedCombatRoom(false, out reason)) return false;
                    if (IsRoomDamageMultiplierActive && string.Equals(roomDamageMultiplierRoomId,
                            CurrentRoomId(spellSlot.Progress), StringComparison.Ordinal))
                    {
                        reason = "It is already active in this room.";
                        return false;
                    }

                    return true;
                case ItemEffectType.StunAndCutRandomEnemies:
                    if (health == null || health.IsDead)
                    {
                        reason = "The player cannot use it now.";
                        return false;
                    }

                    if (CollectRoomEnemies().Count == 0)
                    {
                        reason = "There is no enemy in this room.";
                        return false;
                    }

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
                case ItemEffectType.DuplicateRoomChestsAndPickups:
                    DuplicateCurrentRoom();
                    break;
                case ItemEffectType.FreeCurrentShopOffers:
                    if (!TryResolveCurrentShop(out ShopStockState shop, out string shopReason))
                        throw new InvalidOperationException("[PlayerSingleUseEffects] Membership refused: " +
                                                            shopReason);
                    bool changed = shop.TryMakeFree();
                    Debug.Log($"[PlayerSingleUseEffects] Membership card used in {shop.ShopId}: " +
                              (changed ? "remaining offers are free." : "it was already free."), this);
                    break;
                case ItemEffectType.CurrentRoomPulseDamage:
                    BeginPulseInCurrentRoom();
                    pulseDamageShare = effect.Magnitude;
                    pulseDamageInterval = effect.IntervalSeconds;
                    pulseDamageTotalTicks = PulseTicks(effect);
                    pulseDamageDeliveredTicks = 0;
                    Debug.Log($"[PlayerSingleUseEffects] Pastel pulse damage in {pulseRoomId}: " +
                              $"{pulseDamageTotalTicks} pulses every {pulseDamageInterval}s.", this);
                    break;
                case ItemEffectType.CurrentRoomPulseHeal:
                    BeginPulseInCurrentRoom();
                    pulseHealUnits = effect.IntegerAmount;
                    pulseHealInterval = effect.IntervalSeconds;
                    pulseHealTotalTicks = PulseTicks(effect);
                    pulseHealDeliveredTicks = 0;
                    break;
                case ItemEffectType.CurrentRoomPulseSP:
                    BeginPulseInCurrentRoom();
                    pulseSPHalves = effect.IntegerAmount;
                    pulseSPInterval = effect.IntervalSeconds;
                    pulseSPTotalTicks = PulseTicks(effect);
                    pulseSPDeliveredTicks = 0;
                    break;
                case ItemEffectType.CurrentRoomEnemyPoison:
                    PoisonRoomEnemies(effect);
                    break;
                case ItemEffectType.CurrentRoomEnemyDamageReduction:
                    // Using it again restarts the timer; using it in another room moves the effect there.
                    enemyDamageReductionRoomId = CurrentRoomId(spellSlot.Progress);
                    enemyDamageReductionShare = effect.Magnitude;
                    enemyDamageReductionRemainingSeconds = effect.DurationSeconds;
                    ApplyEnemyDamageReduction();
                    Debug.Log($"[PlayerSingleUseEffects] Suspicious potion in {enemyDamageReductionRoomId}: enemy " +
                              $"hits -{enemyDamageReductionShare:P0} for {enemyDamageReductionRemainingSeconds}s.", this);
                    break;
                case ItemEffectType.CurrentRoomDamageMultiplier:
                    roomDamageMultiplierRoomId = CurrentRoomId(spellSlot.Progress);
                    roomDamageMultiplier = effect.Magnitude;
                    playerStats.SetSingleUseRoomDamageMultiplier(roomDamageMultiplier);
                    Debug.Log($"[PlayerSingleUseEffects] Thread of reason in {roomDamageMultiplierRoomId}: player " +
                              $"damage x{roomDamageMultiplier}.", this);
                    break;
                case ItemEffectType.StunAndCutRandomEnemies:
                    StunAndCutRandomEnemies(effect);
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

        // Advances 파스텔빛 and 수상한 물약 and ends the room-bound jjangsem effects once their room is no longer the
        // player's. Paused game time (time scale 0) never advances a timer.
        public void AdvanceJjangsemRoomEffects(float deltaSeconds)
        {
            if (!IsPulseActive && !IsEnemyDamageReductionActive && !IsRoomDamageMultiplierActive) return;

            EnsureReferences();
            RunProgress progress = spellSlot != null ? spellSlot.Progress : null;
            if (health == null || health.IsDead || progress == null || progress.IsProgressionStopped)
            {
                StopPulse();
                StopEnemyDamageReduction();
                ClearRoomDamageMultiplier();
                return;
            }

            string roomId = CurrentRoomId(progress);
            if (IsRoomDamageMultiplierActive &&
                !string.Equals(roomDamageMultiplierRoomId, roomId, StringComparison.Ordinal))
                ClearRoomDamageMultiplier();

            bool advances = deltaSeconds > 0f && Time.timeScale > 0f;
            if (IsEnemyDamageReductionActive && advances)
            {
                enemyDamageReductionRemainingSeconds -= deltaSeconds;
                if (enemyDamageReductionRemainingSeconds <= TickTolerance) StopEnemyDamageReduction();
            }

            ApplyEnemyDamageReduction();
            if (IsPulseActive) AdvancePulse(progress, roomId, advances ? deltaSeconds : 0f);
        }

        private void AdvancePulse(RunProgress progress, string roomId, float deltaSeconds)
        {
            RoomRunState state = progress.GetRoomState(pulseRoomId);
            if (!string.Equals(pulseRoomId, roomId, StringComparison.Ordinal) || state == null || state.IsCleared)
            {
                StopPulse();
                return;
            }

            if (deltaSeconds <= 0f) return;

            pulseElapsed += deltaSeconds;
            int dueHeals = DueTicks(pulseHealInterval, pulseHealTotalTicks);
            while (pulseHealDeliveredTicks < dueHeals)
            {
                pulseHealDeliveredTicks++;
                health.Heal(pulseHealUnits);
            }

            int dueSP = DueTicks(pulseSPInterval, pulseSPTotalTicks);
            while (pulseSPDeliveredTicks < dueSP)
            {
                pulseSPDeliveredTicks++;
                for (int i = 0; i < pulseSPHalves; i++) playerSP.TryAddHalf();
            }

            int dueDamage = DueTicks(pulseDamageInterval, pulseDamageTotalTicks);
            while (pulseDamageDeliveredTicks < dueDamage)
            {
                pulseDamageDeliveredTicks++;
                // Enemies that appeared after use and bosses are hit too.
                float damage = playerStats.AttackDamage * pulseDamageShare;
                foreach (Health enemy in CollectRoomEnemies())
                    enemy.TakeDamage(new DamageContext(gameObject, DamageSourceType.PlayerSpell, damage,
                        deliveryType: DamageDeliveryType.Periodic));
            }

            if (pulseHealDeliveredTicks >= pulseHealTotalTicks && pulseDamageDeliveredTicks >= pulseDamageTotalTicks &&
                pulseSPDeliveredTicks >= pulseSPTotalTicks)
                StopPulse();
        }

        private int DueTicks(float interval, int total) => interval <= 0f
            ? 0
            : Mathf.Min(total, Mathf.FloorToInt(pulseElapsed / interval + TickTolerance));

        private static int PulseTicks(ItemEffectEntry effect) =>
            Mathf.Max(1, Mathf.FloorToInt(effect.DurationSeconds / effect.IntervalSeconds + TickTolerance));

        // Every 파스텔빛 effect shares one room and one clock.
        private void BeginPulseInCurrentRoom()
        {
            string roomId = CurrentRoomId(spellSlot.Progress);
            if (string.Equals(pulseRoomId, roomId, StringComparison.Ordinal)) return;
            StopPulse();
            pulseRoomId = roomId;
        }

        private void StopPulse()
        {
            if (IsPulseActive) Debug.Log("[PlayerSingleUseEffects] Pastel pulse ended.", this);
            pulseRoomId = null;
            pulseElapsed = 0f;
            pulseDamageShare = 0f;
            pulseDamageInterval = 0f;
            pulseDamageTotalTicks = 0;
            pulseDamageDeliveredTicks = 0;
            pulseHealUnits = 0;
            pulseHealInterval = 0f;
            pulseHealTotalTicks = 0;
            pulseHealDeliveredTicks = 0;
            pulseSPHalves = 0;
            pulseSPInterval = 0f;
            pulseSPTotalTicks = 0;
            pulseSPDeliveredTicks = 0;
        }

        private void ApplyEnemyDamageReduction()
        {
            if (health == null) return;
            bool inRoom = IsEnemyDamageReductionActive && spellSlot != null && string.Equals(
                enemyDamageReductionRoomId, CurrentRoomId(spellSlot.Progress), StringComparison.Ordinal);
            health.SetEnemyDamageReduction(inRoom ? enemyDamageReductionShare : 0f);
        }

        private void StopEnemyDamageReduction()
        {
            enemyDamageReductionRoomId = null;
            enemyDamageReductionShare = 0f;
            enemyDamageReductionRemainingSeconds = 0f;
            if (health != null) health.SetEnemyDamageReduction(0f);
        }

        private void ClearRoomDamageMultiplier()
        {
            if (IsRoomDamageMultiplierActive) Debug.Log("[PlayerSingleUseEffects] Thread of reason ended.", this);
            roomDamageMultiplierRoomId = null;
            roomDamageMultiplier = 1f;
            if (playerStats != null) playerStats.SetSingleUseRoomDamageMultiplier(1f);
        }

        // 수상한 물약: the enemies in the room at use are poisoned at once, from the attack damage at use. Enemies that
        // appear later are not.
        private void PoisonRoomEnemies(ItemEffectEntry effect)
        {
            PoisonSettings settings = new(1f, effect.Magnitude, effect.DurationSeconds, effect.IntervalSeconds,
                effect.IntegerAmount);
            float attackDamage = playerStats.AttackDamage;
            List<Health> enemies = CollectRoomEnemies();
            foreach (Health enemy in enemies)
                for (int stack = 0; stack < effect.IntegerAmount; stack++)
                    EnemyStatusEffects.TryApplyPoison(enemy, gameObject, attackDamage, settings, 0f);
            Debug.Log($"[PlayerSingleUseEffects] Suspicious potion poisoned {enemies.Count} enemies " +
                      $"x{effect.IntegerAmount}.", this);
        }

        // 깜짝상자: two different enemies when there are two or more. The stun comes first, so a boss drops the
        // pattern it was running before its health falls. The cut shows like a hit from the player.
        private void StunAndCutRandomEnemies(ItemEffectEntry effect)
        {
            List<Health> candidates = CollectRoomEnemies();
            lastSurpriseTargets.Clear();
            int count = Mathf.Min(effect.IntegerAmount, candidates.Count);
            for (int picked = 0; picked < count; picked++)
            {
                int index = Mathf.Clamp(enemyPickProvider(candidates.Count), 0, candidates.Count - 1);
                Health enemy = candidates[index];
                candidates.RemoveAt(index);
                lastSurpriseTargets.Add(enemy);
                if (enemy.TryGetComponent(out KnockbackReceiver receiver)) receiver.ApplyStun(effect.DurationSeconds);
                bool isBoss = enemy.GetComponent<TrickalFanGame.Enemy.BossController>() != null;
                float share = isBoss && effect.SecondaryMagnitude > 0f ? effect.SecondaryMagnitude : effect.Magnitude;
                float removed = enemy.CutHealth(share,
                    new DamageContext(gameObject, DamageSourceType.PlayerSpell, 0f));
                Debug.Log($"[PlayerSingleUseEffects] Surprise box hit {enemy.name}: stunned " +
                          $"{effect.DurationSeconds}s, health -{removed:0.#}.", this);
            }
        }

        public List<Health> CollectRoomEnemies()
        {
            IEnumerable<Health> overridden = enemySourceOverride?.Invoke();
            return overridden != null
                ? CurrentRoomEnemies.Filter(overridden)
                : CurrentRoomEnemies.Collect(ResolveRoomGraph());
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

        // 멜룬카드: the original list is fixed inside RoomDuplication before the first copy is made.
        private void DuplicateCurrentRoom()
        {
            if (!TryResolveDuplicationRoom(out Transform contentRoot, out RoomChestSite site, out int roomSeed,
                    out string reason))
                throw new InvalidOperationException("[PlayerSingleUseEffects] Duplication refused: " + reason);

            LastDuplication = RoomDuplication.Duplicate(contentRoot, site != null ? chestContentTable : null, site,
                roomSeed);
            foreach (string error in LastDuplication.Errors)
                Debug.LogWarning("[PlayerSingleUseEffects] " + error, this);
            Debug.Log($"[PlayerSingleUseEffects] Melune card copied {LastDuplication.CopiedChests.Count} chest(s) " +
                      $"and {LastDuplication.CopiedPickups.Count} pickup(s).", this);
        }

        // The room the graph shows the player in. Chests need its generated Template, Run state and the chest table;
        // without them only the floor pickups can be copied (site is null).
        private bool TryResolveDuplicationRoom(out Transform contentRoot, out RoomChestSite site, out int roomSeed,
            out string reason)
        {
            contentRoot = null;
            site = null;
            roomSeed = 0;
            RunProgress progress = spellSlot != null ? spellSlot.Progress : null;
            RoomGraphController graph = ResolveRoomGraph();
            RoomNode node = graph != null ? graph.CurrentNode : null;
            if (progress == null || node == null || node.ContentRoot == null ||
                (graph.Progress != null && graph.Progress != progress))
            {
                reason = "The player is not in a room.";
                return false;
            }

            contentRoot = node.ContentRoot.transform;
            GeneratedRoomNode generated = FindCurrentNode(progress);
            RoomPrefab room = node.GetComponent<RoomPrefab>();
            RoomRunState state = progress.GetRoomState(node.RoomId);
            if (generated != null && string.Equals(generated.RoomId, node.RoomId, StringComparison.Ordinal))
            {
                roomSeed = generated.ContentSeed;
                if (chestContentTable != null && generated.Template != null && room != null && state != null)
                    site = new RoomChestSite(room, generated.Template, state, progress, transform);
            }

            reason = string.Empty;
            return true;
        }

        // 멤버십카드: the stock of the shop room the player is in. Outside a shop the card is refused and kept.
        private bool TryResolveCurrentShop(out ShopStockState stock, out string reason)
        {
            stock = null;
            RunProgress progress = spellSlot != null ? spellSlot.Progress : null;
            GeneratedRoomNode node = FindCurrentNode(progress);
            if (node == null || (node.Role != GeneratedRoomRole.Shop && !node.IsGoldiShop))
            {
                reason = "It can only be used in a shop.";
                return false;
            }

            stock = progress.GetShopStock(ShopStockBuilder.BuildShopId(node.RoomId));
            if (stock == null)
            {
                reason = "This shop has no stock.";
                return false;
            }

            reason = string.Empty;
            return true;
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
