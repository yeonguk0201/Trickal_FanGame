using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Player
{
    public enum UltimateEndReason
    {
        None,
        Impact,
        Timeout,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(PlayerStats), typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerActionState), typeof(PlayerCombatEvents))]
    public sealed class PlayerUltimate : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float cooldown = 30f;
        [SerializeField, Min(0.01f)] private float maximumDuration = 10f;
        [SerializeField, Min(1f)] private float dashSpeedMultiplier = 2f;
        [SerializeField, Min(0f)] private float impactRecoveryDuration = 0.4f;
        [SerializeField, Min(0f)] private float coastRecoveryDuration = 0.25f;

        [Header("Impact")]
        [SerializeField, Min(0.01f)] private float damageMultiplier = 2f;
        [SerializeField, Min(0.01f)] private float impactRadius = 1.5f;
        [SerializeField, Min(0f)] private float knockbackSpeed = 8f;
        [SerializeField, Range(0f, 1f)] private float bossKnockbackMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float enemyStunDuration = 0.4f;
        [SerializeField, Min(0f)] private float bossStunDuration = 0.15f;
        [SerializeField] private LayerMask targetLayers = 1 << 6;

        private readonly HashSet<Health> impactedTargets = new();
        private Health health;
        private PlayerStats stats;
        private PlayerMovement movement;
        private PlayerActionState actionState;
        private float dashEndTime;
        private float recoveryEndTime;
        private float nextReadyTime;
        private bool hasImpacted;
        private float configuredBaseCooldown = 30f;
        private int progressionLevel = SkillProgressionRules.MinimumLevel;

        public float Cooldown => cooldown;
        public int ProgressionLevel => progressionLevel;
        public float ProgressionDamageMultiplier =>
            SkillProgressionRules.DamageMultiplier(progressionLevel);
        public float MaximumDuration => maximumDuration;
        public float ImpactRecoveryDuration => impactRecoveryDuration;
        public float CoastRecoveryDuration => coastRecoveryDuration;
        public float NextReadyTime => nextReadyTime;
        public bool IsDashing => actionState != null && actionState.IsDashing;
        public bool IsReady => IsReadyAt(Time.time);
        public float CooldownRemaining => GetCooldownRemaining(Time.time);
        public UltimateEndReason LastEndReason { get; private set; }

        public event Action<UltimateEndReason> DashEnded;

        private void Awake()
        {
            configuredBaseCooldown = Mathf.Max(0.01f, cooldown);
            cooldown = configuredBaseCooldown *
                SkillProgressionRules.HighGradeCooldownMultiplier(progressionLevel);
            health = GetComponent<Health>();
            stats = GetComponent<PlayerStats>();
            movement = GetComponent<PlayerMovement>();
            actionState = GetComponent<PlayerActionState>();
            health.Died -= OnPlayerDied;
            health.Died += OnPlayerDied;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnPlayerDied;
            }

            CleanupState();
        }

        private void OnDisable()
        {
            CleanupState();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                TryActivate(Time.time);
            }

            Tick(Time.time);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryImpact(collision.collider.GetComponentInParent<Health>(), Time.time);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryImpact(other.GetComponentInParent<Health>(), Time.time);
        }

        public void Configure(
            float configuredCooldown,
            float configuredMaximumDuration,
            float configuredDashSpeedMultiplier,
            float configuredImpactRecoveryDuration,
            float configuredCoastRecoveryDuration,
            float configuredImpactRadius,
            float configuredKnockbackSpeed,
            float configuredBossKnockbackMultiplier,
            float configuredEnemyStunDuration,
            float configuredBossStunDuration,
            LayerMask configuredTargetLayers)
        {
            configuredBaseCooldown = Mathf.Max(0.01f, configuredCooldown);
            cooldown = configuredBaseCooldown *
                SkillProgressionRules.HighGradeCooldownMultiplier(progressionLevel);
            maximumDuration = Mathf.Max(0.01f, configuredMaximumDuration);
            dashSpeedMultiplier = Mathf.Max(1f, configuredDashSpeedMultiplier);
            impactRecoveryDuration = Mathf.Max(0f, configuredImpactRecoveryDuration);
            coastRecoveryDuration = Mathf.Max(0f, configuredCoastRecoveryDuration);
            impactRadius = Mathf.Max(0.01f, configuredImpactRadius);
            knockbackSpeed = Mathf.Max(0f, configuredKnockbackSpeed);
            bossKnockbackMultiplier = Mathf.Clamp01(configuredBossKnockbackMultiplier);
            enemyStunDuration = Mathf.Max(0f, configuredEnemyStunDuration);
            bossStunDuration = Mathf.Max(0f, configuredBossStunDuration);
            targetLayers = configuredTargetLayers;
        }

        public void ApplyProgressionLevel(int level)
        {
            progressionLevel = SkillProgressionRules.ClampLevel(level);
            cooldown = configuredBaseCooldown *
                SkillProgressionRules.HighGradeCooldownMultiplier(progressionLevel);
        }

        public bool IsReadyAt(float currentTime)
        {
            return health != null && !health.IsDead && actionState != null &&
                   actionState.CanStartUltimate && currentTime >= nextReadyTime;
        }

        public float GetCooldownRemaining(float currentTime)
        {
            return Mathf.Max(0f, nextReadyTime - currentTime);
        }

        public bool TryActivate(float currentTime)
        {
            if (!IsReadyAt(currentTime) ||
                !actionState.TryBeginUltimate(movement.FacingDirection, dashSpeedMultiplier))
            {
                return false;
            }

            hasImpacted = false;
            LastEndReason = UltimateEndReason.None;
            dashEndTime = currentTime + maximumDuration;
            health.SetInvulnerable(true);
            Debug.Log("[PlayerUltimate] Dash started.", this);
            return true;
        }

        public void Tick(float currentTime)
        {
            if (actionState == null)
            {
                return;
            }

            if (actionState.IsDashing && currentTime >= dashEndTime)
            {
                EndDash(currentTime, UltimateEndReason.Timeout);
            }
            else if (actionState.IsCoastRecovering)
            {
                float remaining = coastRecoveryDuration <= 0f
                    ? 0f
                    : Mathf.Clamp01((recoveryEndTime - currentTime) / coastRecoveryDuration);
                actionState.TryUpdateCoastSpeed(dashSpeedMultiplier * remaining);
                if (currentTime >= recoveryEndTime)
                {
                    CompleteRecovery();
                }
            }
            else if (actionState.IsImpactRecovering && currentTime >= recoveryEndTime)
            {
                CompleteRecovery();
            }
        }

        public bool TryImpact(Health contactedTarget, float currentTime)
        {
            if (!IsDashing || hasImpacted || !IsEnemy(contactedTarget))
            {
                return false;
            }

            hasImpacted = true;
            ApplyImpact(contactedTarget);
            EndDash(currentTime, UltimateEndReason.Impact);
            return true;
        }

        private void ApplyImpact(Health contactedTarget)
        {
            impactedTargets.Clear();
            ApplyToTarget(contactedTarget);
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, impactRadius, targetLayers))
            {
                ApplyToTarget(hit.GetComponentInParent<Health>());
            }
        }

        private void ApplyToTarget(Health target)
        {
            if (!IsEnemy(target) || !impactedTargets.Add(target))
            {
                return;
            }

            DamageContext context = stats.CreateDirectDamageContext(
                gameObject,
                DamageSourceType.PlayerUltimateImpact,
                damageMultiplier * SkillProgressionRules.DamageMultiplier(progressionLevel) *
                stats.SkillDamageMultiplier);
            target.TakeDamage(context);

            KnockbackReceiver receiver = target.GetComponent<KnockbackReceiver>();
            if (receiver == null)
            {
                return;
            }

            Vector2 direction = target.transform.position - transform.position;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = actionState.DashDirection;
            }

            float multiplier = target.GetComponent<BossController>() != null
                ? bossKnockbackMultiplier
                : 1f;
            float stunDuration = target.GetComponent<BossController>() != null
                ? bossStunDuration
                : enemyStunDuration;
            receiver.Apply(direction, knockbackSpeed * multiplier, -1f, stunDuration);
        }

        private bool IsEnemy(Health target)
        {
            return target != null && target != health && !target.IsDead &&
                   (targetLayers.value & (1 << target.gameObject.layer)) != 0;
        }

        private void EndDash(float currentTime, UltimateEndReason reason)
        {
            bool beganRecovery = reason == UltimateEndReason.Impact
                ? actionState != null && actionState.TryBeginImpactRecovery()
                : actionState != null && actionState.TryBeginCoastRecovery();
            if (!beganRecovery)
            {
                return;
            }

            LastEndReason = reason;
            float duration = reason == UltimateEndReason.Impact
                ? impactRecoveryDuration
                : coastRecoveryDuration;
            recoveryEndTime = currentTime + duration;
            nextReadyTime = currentTime + cooldown;
            if (reason == UltimateEndReason.Impact)
            {
                movement.StopImmediately();
                health.SetInvulnerable(true);
            }
            else
            {
                health.SetInvulnerable(false);
            }

            DashEnded?.Invoke(reason);
            Debug.Log($"[PlayerUltimate] Dash ended by {reason}; cooldown started.", this);

            if (duration <= 0f)
            {
                CompleteRecovery();
            }
        }

        private void CompleteRecovery()
        {
            health.SetInvulnerable(false);
            movement.StopImmediately();
            actionState.TryCompleteRecovery();
        }

        private void OnPlayerDied()
        {
            CleanupState();
        }

        private void CleanupState()
        {
            health?.SetInvulnerable(false);
            movement?.StopImmediately();
            actionState?.ForceNormal();
            hasImpacted = false;
            LastEndReason = UltimateEndReason.None;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, impactRadius);
        }
    }
}
