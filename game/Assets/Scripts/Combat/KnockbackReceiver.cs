using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Passive-0 §4.6: a pattern movement (dash, lunge, boss pattern) that a basic attack push must not move.
    public interface IKnockbackPushBlocker
    {
        bool BlocksKnockbackPush { get; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class KnockbackReceiver : MonoBehaviour
    {
        private const float KinematicPushSkin = 0.02f;

        private static readonly List<RaycastHit2D> PushHits = new();

        [SerializeField, Min(0.01f)] private float defaultDuration = 0.2f;
        [SerializeField, Min(0f)] private float defaultStunDuration = 0.4f;
        [Tooltip("기본 공격 넉백 전용 무게입니다(Rigidbody 질량과 별개). 클수록 덜 밀립니다.")]
        [SerializeField, Min(0.01f)] private float knockbackWeight = 1f;

        private Rigidbody2D body;
        private Health health;
        private IKnockbackPushBlocker[] pushBlockers;
        private float knockbackEndTime;
        private float stunEndTime;
        private Vector2 pushVelocity;
        private float pushEndTime;

        public bool IsKnockedBack { get; private set; }
        public bool IsStunned { get; private set; }
        public bool IsActive => IsKnockedBack || IsStunned;
        // A basic attack push. It moves the body but is not part of IsActive, so it never suppresses actions.
        public bool IsPushed { get; private set; }
        public Vector2 PushVelocity => IsPushed ? pushVelocity : Vector2.zero;
        public float KnockbackWeight => knockbackWeight;
        public Vector2 CurrentVelocity => body != null ? body.linearVelocity : Vector2.zero;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            pushBlockers = GetComponents<IKnockbackPushBlocker>();
        }

        private void FixedUpdate()
        {
            Tick(Time.time);
            TickPush(Time.time, Time.fixedDeltaTime);
        }

        public void ConfigureWeight(float configuredWeight)
        {
            knockbackWeight = Mathf.Max(0.01f, configuredWeight);
        }

        public void Tick(float currentTime)
        {
            if (!IsActive)
            {
                return;
            }

            if (health.IsDead)
            {
                Stop();
                return;
            }

            if (IsKnockedBack && currentTime >= knockbackEndTime)
            {
                IsKnockedBack = false;
                body.linearVelocity = Vector2.zero;
                IsStunned = currentTime < stunEndTime;
            }

            if (IsStunned && currentTime >= stunEndTime)
            {
                Stop();
            }
        }

        public bool Apply(Vector2 direction, float speed, float duration = -1f, float stunDuration = -1f)
        {
            if (health == null)
            {
                Awake();
            }

            if (health.IsDead || direction.sqrMagnitude <= 0.001f || speed <= 0f)
            {
                return false;
            }

            float resolvedDuration = duration > 0f ? duration : defaultDuration;
            float resolvedStunDuration = stunDuration >= 0f ? stunDuration : defaultStunDuration;
            IsKnockedBack = true;
            IsStunned = false;
            IsPushed = false;
            knockbackEndTime = Time.time + resolvedDuration;
            stunEndTime = knockbackEndTime + resolvedStunDuration;
            body.linearVelocity = direction.normalized * speed;
            return true;
        }

        // Passive-0 §4.6: the basic attack knockback. It only displaces the body: no stun, and the enemy's wind-up,
        // dash or boss pattern keeps running. A new push replaces the current one instead of adding to it.
        public bool ApplyPush(Vector2 direction, float speed, float duration)
        {
            if (health == null)
            {
                Awake();
            }

            if (health.IsDead || IsKnockedBack || direction.sqrMagnitude <= 0.001f || speed <= 0f ||
                duration <= 0f || IsPushBlocked())
            {
                return false;
            }

            pushVelocity = direction.normalized * speed;
            pushEndTime = Time.time + duration;
            IsPushed = true;
            return true;
        }

        // Returns the displacement requested for this physics step.
        public Vector2 TickPush(float currentTime, float deltaTime)
        {
            if (!IsPushed)
            {
                return Vector2.zero;
            }

            if (health.IsDead || IsKnockedBack || currentTime >= pushEndTime || IsPushBlocked())
            {
                IsPushed = false;
                return Vector2.zero;
            }

            Vector2 step = pushVelocity * deltaTime;
            // A Kinematic body (bosses) ignores walls when moved, so the push stops at solid scenery itself.
            if (body.bodyType == RigidbodyType2D.Kinematic && IsKinematicPushObstructed(step))
            {
                return Vector2.zero;
            }

            // MovePosition replaces the body's own velocity for the step, so that movement is added back.
            body.MovePosition(body.position + body.linearVelocity * deltaTime + step);
            return step;
        }

        public void Stop()
        {
            IsKnockedBack = false;
            IsStunned = false;
            IsPushed = false;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private bool IsPushBlocked()
        {
            foreach (IKnockbackPushBlocker blocker in pushBlockers)
            {
                if (blocker != null && blocker.BlocksKnockbackPush)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsKinematicPushObstructed(Vector2 step)
        {
            PushHits.Clear();
            body.Cast(step.normalized, PushHits, step.magnitude + KinematicPushSkin);
            foreach (RaycastHit2D hit in PushHits)
            {
                if (hit.collider == null || hit.collider.isTrigger ||
                    hit.collider.GetComponentInParent<Health>() != null)
                {
                    continue;
                }

                if (Vector2.Dot(hit.normal, step) < 0f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
