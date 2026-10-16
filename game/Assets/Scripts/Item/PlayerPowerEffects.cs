using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // Runs the 교주의 권능 effects (Power-1, plan 26 §4.4). Timed effects use game time, so pausing stops them, and
    // they follow the player across rooms and floors; death or the end of the Run ends them all.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(PlayerStats), typeof(PlayerSP))]
    public sealed class PlayerPowerEffects : MonoBehaviour
    {
        private const float TickTolerance = 0.0001f;
        // 애착 담요: a reduced hit still costs at least half a heart (1 HP unit).
        public const float BlanketDamageFloor = 1f;

        [SerializeField] private RoomGraphController roomGraph;
        // Verification seam: replaces the current room's enemy list.
        private Func<IEnumerable<Health>> enemySourceOverride;

        private Health health;
        private PlayerStats stats;
        private PlayerSP playerSP;
        private PlayerUltimate ultimate;
        private PlayerPowerSlot slot;

        // 팝핀 스타: player damage bonus from the enemies the last blast hit.
        private float blastDamageBonus;
        private float blastRemainingSeconds;

        // 애착 담요
        private float blanketRemainingSeconds;
        private float blanketHealInterval;
        private float blanketHealAmount;
        private float blanketElapsed;
        private int blanketTotalTicks;
        private int blanketDeliveredTicks;

        // 급발진
        private float overdriveRemainingSeconds;
        private float overdriveAttackSpeed;
        private float overdriveMoveSpeed;
        private float overdriveCooldownReduction;

        public RoomGraphController RoomGraph => roomGraph;
        public float BlastDamageBonus => blastRemainingSeconds > 0f ? blastDamageBonus : 0f;
        public float BlastRemainingSeconds => Mathf.Max(0f, blastRemainingSeconds);
        public bool IsBlanketActive => blanketRemainingSeconds > 0f;
        public float BlanketRemainingSeconds => Mathf.Max(0f, blanketRemainingSeconds);
        public int BlanketDeliveredTicks => blanketDeliveredTicks;
        public bool IsOverdriveActive => overdriveRemainingSeconds > 0f;
        public float OverdriveRemainingSeconds => Mathf.Max(0f, overdriveRemainingSeconds);
        public int LastHitCount { get; private set; }
        public Health LastSmiteTarget { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            stats = GetComponent<PlayerStats>();
            playerSP = GetComponent<PlayerSP>();
            ultimate = GetComponent<PlayerUltimate>();
            slot = GetComponent<PlayerPowerSlot>();
        }

        public void Configure(RoomGraphController configuredRoomGraph)
        {
            roomGraph = configuredRoomGraph;
        }

        public void ConfigureEnemySourceForVerification(Func<IEnumerable<Health>> source)
        {
            enemySourceOverride = source;
        }

        private void Update() => Advance(Time.deltaTime);

        private void OnDisable() => StopAll();

        public bool CanExecute(ItemDefinition definition, out string reason)
        {
            if (definition == null || !definition.IsActivePower)
            {
                reason = "Only a 권능 can be used from the 권능 slot.";
                return false;
            }

            EnsureReferences();
            if (definition.Effects.Count == 0)
            {
                reason = $"{definition.DisplayName} has no effect connected yet.";
                return false;
            }

            foreach (ItemEffectEntry effect in definition.Effects)
            {
                if (!CanApply(effect, out reason)) return false;
            }

            reason = string.Empty;
            return true;
        }

        public void Execute(ItemDefinition definition)
        {
            if (!CanExecute(definition, out string reason))
                throw new InvalidOperationException($"[PlayerPowerEffects] {definition?.ItemId}: {reason}");

            foreach (ItemEffectEntry effect in definition.Effects) Apply(effect);
        }

        // Advances every timed effect. Paused game time (time scale 0) never advances them.
        public void Advance(float deltaSeconds)
        {
            if (blastRemainingSeconds <= 0f && !IsBlanketActive && !IsOverdriveActive) return;

            EnsureReferences();
            RunProgress progress = slot != null ? slot.Progress : null;
            if (health == null || health.IsDead || progress == null || progress.IsProgressionStopped)
            {
                StopAll();
                return;
            }

            if (deltaSeconds <= 0f || Time.timeScale <= 0f) return;

            if (blastRemainingSeconds > 0f)
            {
                blastRemainingSeconds -= deltaSeconds;
                if (blastRemainingSeconds <= TickTolerance) EndBlast();
            }

            if (IsBlanketActive) AdvanceBlanket(deltaSeconds);

            if (IsOverdriveActive)
            {
                float step = Mathf.Min(deltaSeconds, overdriveRemainingSeconds);
                // The cooldown runs at 1 / (1 - reduction) speed: the extra share is advanced here.
                if (ultimate != null && overdriveCooldownReduction > 0f)
                    ultimate.AdvanceCooldown(step * (1f / (1f - overdriveCooldownReduction) - 1f));
                overdriveRemainingSeconds -= deltaSeconds;
                if (overdriveRemainingSeconds <= TickTolerance) EndOverdrive();
            }
        }

        private bool CanApply(ItemEffectEntry effect, out string reason)
        {
            if (effect == null)
            {
                reason = "Invalid 권능 effect: missing.";
                return false;
            }

            if (!effect.TryValidate(out string error))
            {
                reason = "Invalid 권능 effect: " + error;
                return false;
            }

            if (health == null || health.IsDead)
            {
                reason = "The player is dead.";
                return false;
            }

            switch (effect.EffectType)
            {
                // Effects aimed at enemies would waste the gauge in an empty room.
                case ItemEffectType.PowerRoomBlast:
                case ItemEffectType.PowerSmite:
                case ItemEffectType.PowerStunRoomEnemies:
                case ItemEffectType.PowerRoomEnemyVulnerability:
                    if (CollectRoomEnemies().Count == 0)
                    {
                        reason = "There is no enemy in this room.";
                        return false;
                    }

                    reason = string.Empty;
                    return true;
                case ItemEffectType.PowerGainShield:
                    if (health.HealthAndShieldLimit > 0f &&
                        health.CurrentHealth + health.CurrentShield >= health.HealthAndShieldLimit)
                    {
                        reason = "Health and shield are already at the limit.";
                        return false;
                    }

                    reason = string.Empty;
                    return true;
                case ItemEffectType.PowerProtectiveBlanket:
                case ItemEffectType.PowerOverdrive:
                    reason = string.Empty;
                    return true;
                case ItemEffectType.PowerRestoreSP:
                    if (playerSP == null)
                    {
                        reason = "The player has no SP.";
                        return false;
                    }

                    reason = string.Empty;
                    return true;
                default:
                    reason = $"{effect.EffectType} is not a connected 권능 effect.";
                    return false;
            }
        }

        private void Apply(ItemEffectEntry effect)
        {
            switch (effect.EffectType)
            {
                case ItemEffectType.PowerRoomBlast:
                    ApplyBlast(effect);
                    break;
                case ItemEffectType.PowerProtectiveBlanket:
                    // Using it again while active restarts the window.
                    blanketRemainingSeconds = effect.DurationSeconds;
                    blanketHealInterval = effect.IntervalSeconds;
                    blanketHealAmount = effect.IntegerAmount;
                    blanketElapsed = 0f;
                    blanketDeliveredTicks = 0;
                    blanketTotalTicks = Mathf.Max(1,
                        Mathf.FloorToInt(effect.DurationSeconds / effect.IntervalSeconds + TickTolerance));
                    health.SetPowerDamageReduction(effect.Magnitude, BlanketDamageFloor);
                    Debug.Log($"[PlayerPowerEffects] Blanket for {blanketRemainingSeconds}s.", this);
                    break;
                case ItemEffectType.PowerSmite:
                    ApplySmite(effect);
                    break;
                case ItemEffectType.PowerStunRoomEnemies:
                    foreach (Health enemy in CollectRoomEnemies())
                        enemy.GetComponent<KnockbackReceiver>()?.ApplyStun(effect.DurationSeconds);
                    break;
                case ItemEffectType.PowerRoomEnemyVulnerability:
                    foreach (Health enemy in CollectRoomEnemies())
                        enemy.ApplyIncomingDamageBonus(effect.Magnitude, effect.DurationSeconds);
                    break;
                case ItemEffectType.PowerGainShield:
                    float gained = health.GainShield(effect.Magnitude);
                    Debug.Log($"[PlayerPowerEffects] Shield +{gained}.", this);
                    break;
                case ItemEffectType.PowerOverdrive:
                    overdriveRemainingSeconds = effect.DurationSeconds;
                    overdriveAttackSpeed = effect.Magnitude;
                    overdriveMoveSpeed = effect.SecondaryMagnitude;
                    overdriveCooldownReduction = effect.ScaleMultiplier;
                    ApplyStatBonuses();
                    Debug.Log($"[PlayerPowerEffects] Overdrive for {overdriveRemainingSeconds}s.", this);
                    break;
                case ItemEffectType.PowerRestoreSP:
                    playerSP.TryAdd(effect.IntegerAmount);
                    break;
            }
        }

        // 팝핀 스타: the damage uses the attack before this blast's own bonus is applied.
        private void ApplyBlast(ItemEffectEntry effect)
        {
            List<Health> enemies = CollectRoomEnemies();
            float baseDamage = stats.AttackDamage * effect.Magnitude;
            int hits = 0;
            foreach (Health enemy in enemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                float damage = IsBoss(enemy) ? baseDamage : baseDamage * effect.ScaleMultiplier;
                enemy.TakeDamage(new DamageContext(gameObject, DamageSourceType.PlayerPower, damage));
                hits++;
            }

            LastHitCount = hits;
            float bonus = Mathf.Min(hits, effect.IntegerAmount) * effect.SecondaryMagnitude;
            // A second blast while one is running keeps the larger bonus and restarts the timer.
            blastDamageBonus = Mathf.Max(BlastDamageBonus, bonus);
            blastRemainingSeconds = effect.DurationSeconds;
            ApplyStatBonuses();
            Debug.Log($"[PlayerPowerEffects] Poppin star hit {hits} enemies: damage +{blastDamageBonus:P0} for " +
                      $"{blastRemainingSeconds}s.", this);
        }

        // 찐짜천벌: a boss is chosen first; otherwise the enemy with the most current health.
        private void ApplySmite(ItemEffectEntry effect)
        {
            Health target = null;
            foreach (Health enemy in CollectRoomEnemies())
            {
                if (target == null) target = enemy;
                else if (IsBoss(enemy) != IsBoss(target))
                {
                    if (IsBoss(enemy)) target = enemy;
                }
                else if (enemy.CurrentHealth > target.CurrentHealth) target = enemy;
            }

            LastSmiteTarget = target;
            if (target == null) return;

            // Shock first, so items that reward hits on shocked enemies count the lightning.
            ShockSettings shock = new(1f, effect.SecondaryMagnitude, effect.DurationSeconds, effect.IntegerAmount);
            for (int stack = 0; stack < effect.IntegerAmount; stack++) EnemyStatusEffects.TryApplyShock(target, shock, 0f);
            target.TakeDamage(new DamageContext(gameObject, DamageSourceType.PlayerPower,
                stats.AttackDamage * effect.Magnitude));
            Debug.Log($"[PlayerPowerEffects] Divine punishment struck {target.name}.", this);
        }

        private void AdvanceBlanket(float deltaSeconds)
        {
            blanketElapsed += deltaSeconds;
            int dueTicks = Mathf.Min(blanketTotalTicks,
                Mathf.FloorToInt(blanketElapsed / blanketHealInterval + TickTolerance));
            while (blanketDeliveredTicks < dueTicks)
            {
                blanketDeliveredTicks++;
                health.Heal(blanketHealAmount);
            }

            blanketRemainingSeconds -= deltaSeconds;
            if (blanketRemainingSeconds <= TickTolerance) EndBlanket();
        }

        // The alive enemies of the room the player is in. Enemies of other rooms are never touched.
        public List<Health> CollectRoomEnemies()
        {
            IEnumerable<Health> overridden = enemySourceOverride?.Invoke();
            if (overridden != null) return CurrentRoomEnemies.Filter(overridden);
            if (roomGraph == null) roomGraph = FindFirstObjectByType<RoomGraphController>();
            return CurrentRoomEnemies.Collect(roomGraph);
        }

        private static bool IsBoss(Health enemy) => enemy != null && enemy.GetComponent<BossController>() != null;

        private void ApplyStatBonuses()
        {
            if (stats == null) return;
            stats.SetPowerBonuses(BlastDamageBonus,
                IsOverdriveActive ? overdriveAttackSpeed : 0f,
                IsOverdriveActive ? overdriveMoveSpeed : 0f);
        }

        private void EndBlast()
        {
            blastRemainingSeconds = 0f;
            blastDamageBonus = 0f;
            ApplyStatBonuses();
        }

        private void EndBlanket()
        {
            blanketRemainingSeconds = 0f;
            health?.SetPowerDamageReduction(0f, 0f);
        }

        private void EndOverdrive()
        {
            overdriveRemainingSeconds = 0f;
            overdriveAttackSpeed = 0f;
            overdriveMoveSpeed = 0f;
            overdriveCooldownReduction = 0f;
            ApplyStatBonuses();
        }

        private void StopAll()
        {
            EndBlast();
            EndBlanket();
            EndOverdrive();
        }

        private void EnsureReferences()
        {
            if (health == null || stats == null || playerSP == null || slot == null) Awake();
        }
    }
}
