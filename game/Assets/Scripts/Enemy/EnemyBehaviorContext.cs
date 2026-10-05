using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// Owns the shared target, alert, and action-suppression contract used by normal enemies.
    /// Detection may start combat, but never ends an alert during the same encounter.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver))]
    public sealed class EnemyBehaviorContext : MonoBehaviour
    {
        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver knockback;
        private Transform target;
        private Health targetHealth;
        private bool controllerMovementSuppressed;
        private bool controllerVelocityOwned;

        public bool IsAlerted { get; private set; }
        public Transform Target => target;
        public Health TargetHealth => targetHealth;
        public bool HasLivingTarget => target != null && targetHealth != null && !targetHealth.IsDead;
        public bool IsActionSuppressed => health == null || health.IsDead || !IsAlerted || !HasLivingTarget ||
                                          knockback == null || knockback.IsActive;
        public bool IsMovementSuppressed => IsActionSuppressed || controllerMovementSuppressed;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void OnDisable()
        {
            Disengage();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            SetTargetInternal(null);
        }

        public void SetTarget(Transform configuredTarget)
        {
            SetTargetInternal(configuredTarget);
        }

        public void Initialize()
        {
            CacheComponents();
            Subscribe();
        }

        public void BeginCombat(Transform combatTarget)
        {
            if (combatTarget != null)
            {
                SetTargetInternal(combatTarget);
            }

            if (HasLivingTarget && health != null && !health.IsDead)
            {
                IsAlerted = true;
            }
        }

        public bool TryAcquireOrAlert(float detectionRange)
        {
            if (!HasLivingTarget)
            {
                PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
                SetTargetInternal(player != null ? player.transform : null);
            }

            if (!HasLivingTarget || health == null || health.IsDead)
            {
                return false;
            }

            if (!IsAlerted)
            {
                float range = Mathf.Max(0f, detectionRange);
                Vector2 offset = target.position - transform.position;
                if (offset.sqrMagnitude > range * range)
                {
                    return false;
                }

                IsAlerted = true;
            }

            return true;
        }

        public void SetControllerMovementSuppressed(bool suppressed)
        {
            controllerMovementSuppressed = suppressed;
            if (suppressed && body != null && (knockback == null || !knockback.IsKnockedBack))
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        // A controller that moves the body itself during an action (a melee lunge) owns the velocity, so suppressed
        // movement components do not zero it between that controller's ticks.
        public void SetControllerVelocityOwned(bool owned)
        {
            controllerVelocityOwned = owned;
        }

        public void StopForSuppression()
        {
            if (controllerVelocityOwned && !IsActionSuppressed)
            {
                return;
            }

            if (body != null && (knockback == null || !knockback.IsKnockedBack))
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        public void Disengage()
        {
            IsAlerted = false;
            controllerMovementSuppressed = false;
            controllerVelocityOwned = false;
            knockback?.Stop();
            StopForSuppression();
        }

        private void CacheComponents()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
        }

        private void Subscribe()
        {
            if (health == null)
            {
                return;
            }

            health.DamageApplied -= OnDamageApplied;
            health.DamageApplied += OnDamageApplied;
            health.Died -= OnDied;
            health.Died += OnDied;
        }

        private void Unsubscribe()
        {
            if (health != null)
            {
                health.DamageApplied -= OnDamageApplied;
                health.Died -= OnDied;
            }
        }

        private void OnDamageApplied(DamageContext context, float appliedDamage, float remainingHealth)
        {
            if (appliedDamage <= 0f || remainingHealth <= 0f)
            {
                return;
            }

            PlayerMovement attackingPlayer = context.Source != null
                ? context.Source.GetComponentInParent<PlayerMovement>()
                : null;
            if (attackingPlayer != null)
            {
                BeginCombat(attackingPlayer.transform);
                return;
            }

            TryAcquireOrAlert(float.PositiveInfinity);
        }

        private void OnDied()
        {
            Disengage();
        }

        private void OnTargetDied()
        {
            Disengage();
        }

        private void SetTargetInternal(Transform configuredTarget)
        {
            if (targetHealth != null)
            {
                targetHealth.Died -= OnTargetDied;
            }

            target = configuredTarget;
            targetHealth = target != null ? target.GetComponent<Health>() : null;
            if (targetHealth != null)
            {
                targetHealth.Died += OnTargetDied;
            }
        }
    }
}
