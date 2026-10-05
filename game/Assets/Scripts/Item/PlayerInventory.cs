using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [RequireComponent(typeof(PlayerStats), typeof(Health), typeof(PlayerSP))]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private RunProgress runProgress;

        private readonly List<AcquiredItem> acquiredItems = new();
        private readonly List<ItemDefinition> acquiredDefinitions = new();
        private readonly Dictionary<string, int> stackCounts = new();
        private readonly HashSet<string> recordedSingleUseInstanceIds = new(StringComparer.Ordinal);
        private PlayerStats stats;
        private Health health;
        private PlayerSP playerSP;

        public const string MultiShotItemId = "item-06";
        public const string PierceItemId = "item-11";

        public IReadOnlyList<AcquiredItem> AcquiredItems => acquiredItems;
        public IReadOnlyList<ItemDefinition> AcquiredDefinitions => acquiredDefinitions;
        public bool IsMultiShotPierceSynergyActive { get; private set; }
        public event Action<ItemDefinition, int> ItemAcquired;
        public event Action<string> SynergyActivated;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            health = GetComponent<Health>();
            playerSP = GetComponent<PlayerSP>();
            if (runProgress == null)
            {
                runProgress = GetComponent<RunProgress>();
            }
            if (runProgress == null)
            {
                runProgress = FindFirstObjectByType<RunProgress>();
            }
        }

        public bool TryAcquire(ItemDefinition definition)
        {
            if (definition == null || !definition.IsValid)
            {
                Debug.LogError("[PlayerInventory] Cannot acquire an invalid item definition.", this);
                return false;
            }

            if (definition.IsSingleUse)
            {
                Debug.LogError(
                    $"[PlayerInventory] {definition.ItemId} is single-use and must be held in the spell slot.", this);
                return false;
            }

            stackCounts.TryGetValue(definition.ItemId, out int currentStacks);
            if (definition.MaxStacks > 0 && currentStacks >= definition.MaxStacks)
            {
                Debug.Log($"[PlayerInventory] {definition.DisplayName} is already at its stack limit.", this);
                return false;
            }

            int newStackCount = currentStacks + 1;
            stackCounts[definition.ItemId] = newStackCount;
            if (currentStacks == 0)
            {
                acquiredDefinitions.Add(definition);
            }
            ApplyEffect(definition);
            EvaluateSynergies();

            RecordAcquisition(definition.ItemId);

            ItemAcquired?.Invoke(definition, newStackCount);
            Debug.Log($"[PlayerInventory] Acquired {definition.DisplayName} x{newStackCount}.", this);
            return true;
        }

        // A single-use item is recorded for the Run result once per pickup instance, when it first enters the slot.
        // Swapping it out and picking the same instance up again does not record it twice (Contract-0 §4.1).
        public bool TryRecordSingleUseAcquisition(ItemDefinition definition, string instanceId)
        {
            if (definition == null || !definition.IsValid || !definition.IsSingleUse ||
                string.IsNullOrWhiteSpace(instanceId))
            {
                Debug.LogError("[PlayerInventory] A single-use acquisition needs a valid definition and instance ID.",
                    this);
                return false;
            }

            if (!recordedSingleUseInstanceIds.Add(instanceId))
            {
                return false;
            }

            RecordAcquisition(definition.ItemId);
            return true;
        }

        private void RecordAcquisition(string itemId)
        {
            int floor = Mathf.Max(1, runProgress != null ? runProgress.CurrentFloor : 1);
            acquiredItems.Add(new AcquiredItem(
                itemId,
                floor,
                acquiredItems.Count + 1,
                Time.realtimeSinceStartup));
        }

        public int GetStackCount(string itemId)
        {
            return stackCounts.TryGetValue(itemId, out int count) ? count : 0;
        }

        private void ApplyEffect(ItemDefinition definition)
        {
            if (definition.Effects.Count == 0)
            {
                ApplyLegacyEffect(definition.EffectType, definition.EffectValue);
                return;
            }

            foreach (ItemEffectEntry effect in definition.Effects)
            {
                switch (effect.EffectType)
                {
                    case ItemEffectType.AttackDamage:
                        stats.AddAttackDamage(effect.Magnitude);
                        break;
                    case ItemEffectType.AttackDamagePercent:
                        stats.AddAttackDamagePercent(effect.Magnitude);
                        break;
                    case ItemEffectType.CriticalChance:
                        stats.AddCriticalChance(effect.Magnitude);
                        break;
                    case ItemEffectType.AttackSpeedPercent:
                        stats.AddAttackSpeedPercent(effect.Magnitude);
                        break;
                    case ItemEffectType.MoveSpeedPercent:
                        stats.AddMoveSpeedPercent(effect.Magnitude);
                        break;
                    case ItemEffectType.MoveSpeedPenaltyPercent:
                        stats.AddMoveSpeedPenaltyPercent(effect.Magnitude);
                        break;
                    case ItemEffectType.ProjectileSpeedPercent:
                        stats.AddProjectileSpeedPercent(effect.Magnitude);
                        break;
                    case ItemEffectType.ProjectileLifetimePercent:
                        stats.AddProjectileLifetimePercent(effect.Magnitude);
                        break;
                    case ItemEffectType.NextCombatRoomAttackDamagePercent:
                        GetOrCreateSpellEffects().AddNextCombatRoomAttackDamagePercent(effect.Magnitude);
                        break;
                    case ItemEffectType.BossRoomAttackSpeedPercent:
                        GetOrCreateSpellEffects().AddBossRoomAttackSpeedPercent(effect.Magnitude);
                        break;
                    case ItemEffectType.BossRoomMoveSpeedPercent:
                        GetOrCreateSpellEffects().AddBossRoomMoveSpeedPercent(effect.Magnitude);
                        break;
                    case ItemEffectType.MaxHealthDamageAura:
                        PlayerDamageAura aura = GetComponent<PlayerDamageAura>();
                        if (aura == null)
                        {
                            aura = gameObject.AddComponent<PlayerDamageAura>();
                        }
                        aura.AddStack(effect.Magnitude, effect.Radius, effect.IntervalSeconds);
                        break;
                    case ItemEffectType.AttackDamageAura:
                        PlayerDamageAura attackAura = GetComponent<PlayerDamageAura>();
                        if (attackAura == null)
                        {
                            attackAura = gameObject.AddComponent<PlayerDamageAura>();
                        }
                        attackAura.AddAttackDamageStack(effect.Magnitude, effect.Radius, effect.IntervalSeconds);
                        break;
                    case ItemEffectType.SkillDamagePercent:
                        stats.AddSkillDamagePercent(effect.Magnitude);
                        break;
                    case ItemEffectType.MaxHealth:
                    case ItemEffectType.MaxHealthFlat:
                        stats.AddMaxHealth(effect.Magnitude, true);
                        break;
                    case ItemEffectType.ShieldOnAcquireMaxHealthPercent:
                        if (health != null)
                        {
                            health.SetShield(Mathf.Max(
                                health.CurrentShield,
                                health.GetMaxHealthRatioAmount(effect.Magnitude)));
                        }
                        break;
                    case ItemEffectType.MoveSpeed:
                        stats.AddMoveSpeed(effect.Magnitude);
                        break;
                    case ItemEffectType.MoveSpeedPercentBelowHealth:
                        stats.AddMoveSpeedPercentBelowHealth(effect.Magnitude, effect.HealthThreshold);
                        break;
                    case ItemEffectType.MultiShot:
                        stats.AddProjectiles(effect.IntegerAmount);
                        break;
                    case ItemEffectType.Pierce:
                        stats.AddPierce(effect.IntegerAmount);
                        break;
                    case ItemEffectType.HealOnKill:
                        stats.AddHealOnKill(effect.Magnitude);
                        break;
                    case ItemEffectType.HealOnKillMaxHealthPercent:
                        stats.AddHealOnKillMaxHealthPercent(effect.Magnitude);
                        break;
                    case ItemEffectType.HealOnKillEveryN:
                        stats.AddPeriodicKillHeal(effect.Magnitude, effect.IntegerAmount);
                        break;
                    case ItemEffectType.DistanceDamage:
                        stats.AddDistanceDamage(
                            effect.Magnitude,
                            effect.MinimumDistance,
                            effect.MaximumDistance);
                        break;
                    case ItemEffectType.SplitAfterPierce:
                        stats.ConfigureProjectileSplit(
                            effect.IntegerAmount,
                            effect.SecondaryMagnitude,
                            effect.MaximumDistance,
                            effect.SpreadAngleDegrees,
                            effect.ScaleMultiplier);
                        break;
                    case ItemEffectType.MaxSP:
                        playerSP?.AddMaxSP(effect.IntegerAmount);
                        break;
                    case ItemEffectType.SkillProjectileBonusAtSP:
                        stats.AddLowerGradeSkillProjectileBonus(
                            effect.IntegerAmount,
                            Mathf.CeilToInt(effect.HealthThreshold));
                        break;
                    case ItemEffectType.HealOverTimeBelowHealthOnce:
                        PlayerLowHealthRecovery recovery = GetComponent<PlayerLowHealthRecovery>();
                        if (recovery == null)
                        {
                            recovery = gameObject.AddComponent<PlayerLowHealthRecovery>();
                        }
                        recovery.BindRunProgress(runProgress);
                        recovery.Configure(effect.HealthThreshold, effect.Magnitude, effect.DurationSeconds);
                        break;
                    case ItemEffectType.BasicAttackHitLightning:
                        PlayerBasicAttackLightning lightning = GetComponent<PlayerBasicAttackLightning>();
                        if (lightning == null)
                        {
                            lightning = gameObject.AddComponent<PlayerBasicAttackLightning>();
                        }
                        lightning.Configure(effect.IntegerAmount, effect.Magnitude);
                        break;
                    case ItemEffectType.Flight:
                        PlayerFlight flight = GetComponent<PlayerFlight>();
                        if (flight == null)
                        {
                            flight = gameObject.AddComponent<PlayerFlight>();
                        }
                        flight.TryStartFlying();
                        break;
                    default:
                        // G-2 through G-6 connect the remaining validated contract types to runtime systems.
                        break;
                }
            }
        }

        private void ApplyLegacyEffect(ItemEffectType effectType, float effectValue)
        {
            switch (effectType)
            {
                case ItemEffectType.AttackDamage:
                    stats.AddAttackDamage(effectValue);
                    break;
                case ItemEffectType.AttackDamagePercent:
                    stats.AddAttackDamagePercent(effectValue);
                    break;
                case ItemEffectType.SkillDamagePercent:
                    stats.AddSkillDamagePercent(effectValue);
                    break;
                case ItemEffectType.MaxHealth:
                    stats.AddMaxHealth(effectValue, true);
                    break;
                case ItemEffectType.MoveSpeed:
                    stats.AddMoveSpeed(effectValue);
                    break;
                case ItemEffectType.MultiShot:
                    stats.AddProjectiles(Mathf.RoundToInt(effectValue));
                    break;
                case ItemEffectType.Pierce:
                    stats.AddPierce(Mathf.RoundToInt(effectValue));
                    break;
                case ItemEffectType.HealOnKill:
                    stats.AddHealOnKill(effectValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effectType), effectType, null);
            }
        }

        private PlayerSpellEffects GetOrCreateSpellEffects()
        {
            PlayerSpellEffects spellEffects = GetComponent<PlayerSpellEffects>();
            if (spellEffects == null)
            {
                spellEffects = gameObject.AddComponent<PlayerSpellEffects>();
            }

            spellEffects.ConfigureRunProgress(runProgress);
            return spellEffects;
        }

        private void EvaluateSynergies()
        {
            if (IsMultiShotPierceSynergyActive ||
                GetStackCount(MultiShotItemId) == 0 ||
                GetStackCount(PierceItemId) == 0)
            {
                return;
            }

            IsMultiShotPierceSynergyActive = true;
            const string synergyName = "MULTI_SHOT + PIERCE";
            SynergyActivated?.Invoke(synergyName);
            Debug.Log(
                $"[PlayerInventory] Synergy activated: {synergyName}. All multi-shot projectiles now pierce.",
                this);
        }
    }
}
