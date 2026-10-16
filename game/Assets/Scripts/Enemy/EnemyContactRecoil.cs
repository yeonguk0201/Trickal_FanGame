using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // 쥬비 (user decision, 2026-10-17): a rammer bounces off the player it touches and hangs back for a moment before
    // chasing again. It also bounces when it sits inside a player who passes through enemies after a hit, so a fast
    // enemy cannot ride along on top of the player, behind the point where the player's shots start.
    // The bounce is a knockback, so chase and contact wait on it like on any other knockback. It leaves at its peak
    // speed and slows down hard (quadratic ease-out) instead of gliding at one speed, so it reads as a bounce.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KnockbackReceiver), typeof(EnemyBehaviorContext))]
    public sealed class EnemyContactRecoil : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float distance = 1.75f;
        [SerializeField, Min(0.01f)] private float duration = 0.25f;
        [Tooltip("튕겨 나간 뒤 다시 추적하기 전에 멈춰 있는 시간입니다.")]
        [SerializeField, Min(0f)] private float pause = 0.5f;

        private Rigidbody2D body;
        private Collider2D ownCollider;
        private KnockbackReceiver knockback;
        private EnemyBehaviorContext behavior;

        public float Distance => distance;
        public float Duration => duration;
        public float Pause => pause;
        // The ease-out (1 - t)² covers a third of the peak speed × duration, so three times the average speed.
        public float PeakSpeed => 3f * distance / duration;
        public bool IsRecoiling { get; private set; }

        private Vector2 recoilDirection;
        private float recoilElapsed;

        public void Configure(float configuredDistance, float configuredDuration, float configuredPause)
        {
            distance = Mathf.Max(0f, configuredDistance);
            duration = Mathf.Max(0.01f, configuredDuration);
            pause = Mathf.Max(0f, configuredPause);
        }

        private void FixedUpdate()
        {
            Tick();
            TickRecoilVelocity(Time.fixedDeltaTime);
        }

        // How far the bounce has gone after the elapsed time: the integral of PeakSpeed × (1 - t)².
        public float DistanceCoveredAt(float elapsed)
        {
            float remaining = 1f - Mathf.Clamp01(elapsed / duration);
            return distance * (1f - remaining * remaining * remaining);
        }

        // Runs before each physics step and sets the velocity that covers this step's share of the curve, so the
        // whole bounce is exactly its distance however the steps fall. The knockback still owns when it ends and
        // the pause after it.
        public void TickRecoilVelocity(float deltaTime)
        {
            if (!IsRecoiling || deltaTime <= 0f) return;
            CacheComponents();
            if (!knockback.IsKnockedBack)
            {
                IsRecoiling = false;
                return;
            }

            float step = DistanceCoveredAt(recoilElapsed + deltaTime) - DistanceCoveredAt(recoilElapsed);
            recoilElapsed += deltaTime;
            if (body != null) body.linearVelocity = recoilDirection * (step / deltaTime);
        }

        // Bounces off a player passing through enemies while this body overlaps theirs. A player who is not passing
        // through collides with the body instead, and ContactDamage bounces it from the touch.
        public bool Tick()
        {
            CacheComponents();
            Transform target = behavior.Target;
            if (target == null) return false;
            PlayerHitPassThrough passThrough = target.GetComponent<PlayerHitPassThrough>();
            Collider2D targetBody = target.GetComponent<Collider2D>();
            if (passThrough == null || !passThrough.IsPassingThrough || ownCollider == null || targetBody == null ||
                !ownCollider.Distance(targetBody).isOverlapped)
            {
                return false;
            }

            return Recoil(target);
        }

        // Returns false while a knockback, stun or earlier bounce is still running.
        public bool Recoil(Transform from)
        {
            CacheComponents();
            if (from == null || knockback.IsActive || distance <= 0f) return false;
            Vector2 away = (Vector2)(transform.position - from.position);
            // Exactly on the player's center: back off the way it came.
            if (away.sqrMagnitude <= 0.0001f && body != null) away = -body.linearVelocity;
            if (away.sqrMagnitude <= 0.0001f) away = Vector2.up;
            if (!knockback.Apply(away, PeakSpeed, duration, pause)) return false;
            recoilDirection = away.normalized;
            recoilElapsed = 0f;
            IsRecoiling = true;
            return true;
        }

        private void CacheComponents()
        {
            if (knockback != null) return;
            body = GetComponent<Rigidbody2D>();
            ownCollider = GetComponent<Collider2D>();
            knockback = GetComponent<KnockbackReceiver>();
            behavior = GetComponent<EnemyBehaviorContext>();
        }
    }
}
