using UnityEngine;

namespace TrickalFanGame.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class KnockbackReceiver : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float defaultDuration = 0.2f;
        [SerializeField, Min(0f)] private float defaultStunDuration = 0.4f;

        private Rigidbody2D body;
        private Health health;
        private float knockbackEndTime;
        private float stunEndTime;

        public bool IsKnockedBack { get; private set; }
        public bool IsStunned { get; private set; }
        public bool IsActive => IsKnockedBack || IsStunned;
        public Vector2 CurrentVelocity => body != null ? body.linearVelocity : Vector2.zero;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
        }

        private void FixedUpdate()
        {
            Tick(Time.time);
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
            knockbackEndTime = Time.time + resolvedDuration;
            stunEndTime = knockbackEndTime + resolvedStunDuration;
            body.linearVelocity = direction.normalized * speed;
            return true;
        }

        public void Stop()
        {
            IsKnockedBack = false;
            IsStunned = false;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }
    }
}
