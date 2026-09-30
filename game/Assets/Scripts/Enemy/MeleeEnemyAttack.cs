using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum MeleeEnemyAttackState
    {
        Idle,
        Telegraph,
        Active,
        Recovery,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyChase), typeof(EnemyBehaviorContext), typeof(EnemyAttackPresentation))]
    public sealed class MeleeEnemyAttack : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float attackRange = 1.15f;
        [SerializeField, Min(0.01f)] private float telegraphDuration = 0.4f;
        [SerializeField, Min(0.01f)] private float activeDuration = 0.12f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.65f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.2f;
        [SerializeField, Min(0.01f)] private float attackDamage = 2f;

        private EnemyChase chase;
        private EnemyBehaviorContext behavior;
        private EnemyAttackPresentation presentation;
        private float stateEndsAt;
        private float nextAttackTime;
        private bool hitApplied;

        public MeleeEnemyAttackState State { get; private set; }
        public float AttackRange => attackRange;
        public float AttackDamage => attackDamage;
        public float TelegraphDuration => telegraphDuration;
        public float ActiveDuration => activeDuration;
        public float RecoveryDuration => recoveryDuration;

        public event Action<MeleeEnemyAttackState> StateChanged;

        private void Awake()
        {
            CacheComponents();
            SetState(MeleeEnemyAttackState.Idle);
        }

        private void FixedUpdate()
        {
            TickAttack(Time.time);
        }

        private void OnDisable()
        {
            CancelAttack(Time.time);
        }

        public void Configure(
            float configuredRange,
            float configuredTelegraphDuration,
            float configuredActiveDuration,
            float configuredRecoveryDuration,
            float configuredCooldown,
            float configuredDamage)
        {
            attackRange = Mathf.Max(0.01f, configuredRange);
            telegraphDuration = Mathf.Max(0.01f, configuredTelegraphDuration);
            activeDuration = Mathf.Max(0.01f, configuredActiveDuration);
            recoveryDuration = Mathf.Max(0f, configuredRecoveryDuration);
            attackCooldown = Mathf.Max(0f, configuredCooldown);
            attackDamage = Mathf.Max(0.01f, configuredDamage);
        }

        public void TickAttack(float currentTime)
        {
            if (behavior == null || chase == null || presentation == null)
            {
                CacheComponents();
            }

            if (!behavior.TryAcquireOrAlert(chase.DetectionRange) || behavior.IsActionSuppressed)
            {
                CancelAttack(currentTime);
                return;
            }

            switch (State)
            {
                case MeleeEnemyAttackState.Idle:
                    behavior.SetControllerMovementSuppressed(false);
                    if (currentTime >= nextAttackTime && IsTargetInRange())
                    {
                        hitApplied = false;
                        stateEndsAt = currentTime + telegraphDuration;
                        behavior.SetControllerMovementSuppressed(true);
                        SetState(MeleeEnemyAttackState.Telegraph);
                    }

                    break;
                case MeleeEnemyAttackState.Telegraph:
                    behavior.SetControllerMovementSuppressed(true);
                    if (currentTime >= stateEndsAt)
                    {
                        stateEndsAt = currentTime + activeDuration;
                        SetState(MeleeEnemyAttackState.Active);
                        TryResolveActiveHit();
                    }

                    break;
                case MeleeEnemyAttackState.Active:
                    behavior.SetControllerMovementSuppressed(true);
                    TryResolveActiveHit();
                    if (currentTime >= stateEndsAt)
                    {
                        stateEndsAt = currentTime + recoveryDuration;
                        nextAttackTime = stateEndsAt + attackCooldown;
                        SetState(MeleeEnemyAttackState.Recovery);
                    }

                    break;
                case MeleeEnemyAttackState.Recovery:
                    behavior.SetControllerMovementSuppressed(true);
                    if (currentTime >= stateEndsAt)
                    {
                        behavior.SetControllerMovementSuppressed(false);
                        SetState(MeleeEnemyAttackState.Idle);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public bool TryResolveActiveHit()
        {
            if (State != MeleeEnemyAttackState.Active || hitApplied ||
                !behavior.HasLivingTarget || !IsTargetInRange())
            {
                return false;
            }

            Health targetHealth = behavior.TargetHealth;
            if (targetHealth.GetComponent<PlayerMovement>() == null)
            {
                return false;
            }

            hitApplied = true;
            targetHealth.GetComponent<PlayerDeathReason>()?.SetReason("ENEMY");
            targetHealth.TakeDamage(new DamageContext(
                gameObject,
                DamageSourceType.EnemyMelee,
                attackDamage));
            return true;
        }

        private bool IsTargetInRange()
        {
            if (!behavior.HasLivingTarget)
            {
                return false;
            }

            Vector2 offset = behavior.Target.position - transform.position;
            return offset.sqrMagnitude <= attackRange * attackRange;
        }

        private void CancelAttack(float currentTime)
        {
            if (behavior != null)
            {
                behavior.SetControllerMovementSuppressed(false);
                behavior.StopForSuppression();
            }

            if (State != MeleeEnemyAttackState.Idle)
            {
                nextAttackTime = Mathf.Max(nextAttackTime, currentTime + attackCooldown);
            }

            hitApplied = false;
            SetState(MeleeEnemyAttackState.Idle);
        }

        private void SetState(MeleeEnemyAttackState state)
        {
            bool changed = State != state;
            State = state;
            if (presentation != null)
            {
                presentation.SetPhase(state switch
                {
                    MeleeEnemyAttackState.Telegraph => EnemyAttackPhase.Telegraph,
                    MeleeEnemyAttackState.Active => EnemyAttackPhase.Active,
                    MeleeEnemyAttackState.Recovery => EnemyAttackPhase.Recovery,
                    _ => EnemyAttackPhase.Idle,
                });
            }

            if (changed)
            {
                StateChanged?.Invoke(State);
            }
        }

        private void CacheComponents()
        {
            chase = GetComponent<EnemyChase>();
            behavior = GetComponent<EnemyBehaviorContext>();
            presentation = GetComponent<EnemyAttackPresentation>();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
