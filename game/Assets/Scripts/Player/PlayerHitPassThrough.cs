using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Player
{
    // Corner-0: while the post-hit invulnerability window lasts, the player body ignores enemy bodies and the sprite
    // blinks. A boss body cannot be pushed aside, so this is how a player backed into a wall or corner gets out.
    // The high-grade dash keeps its collisions so its impact still lands.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(DamageInvulnerability))]
    public sealed class PlayerHitPassThrough : MonoBehaviour
    {
        public const float BlinkInterval = 0.07f;
        public const float BlinkAlpha = 0.35f;

        private Rigidbody2D body;
        private Health health;
        private DamageInvulnerability invulnerability;
        private PlayerActionState actionState;
        private SpriteRenderer spriteRenderer;
        private int enemyMask;
        private float solidAlpha = 1f;
        private bool blinking;

        public bool IsPassingThrough { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            invulnerability = GetComponent<DamageInvulnerability>();
            actionState = GetComponent<PlayerActionState>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            enemyMask = LayerMask.GetMask("Enemy");
        }

        private void FixedUpdate()
        {
            Tick(Time.time);
        }

        private void Update()
        {
            RenderBlink(Time.time);
        }

        private void OnDisable()
        {
            SetPassingThrough(false);
            SetBlinkAlpha(false);
        }

        public void Tick(float currentTime)
        {
            if (body == null) Awake();
            SetPassingThrough(!health.IsDead && invulnerability.IsRecoveringFromHitAt(currentTime) &&
                              (actionState == null || !actionState.IsDashing));
        }

        public void RenderBlink(float currentTime)
        {
            if (body == null) Awake();
            bool recovering = !health.IsDead && invulnerability.IsRecoveringFromHitAt(currentTime);
            SetBlinkAlpha(recovering && Mathf.FloorToInt(currentTime / BlinkInterval) % 2 == 0);
        }

        private void SetPassingThrough(bool passing)
        {
            if (IsPassingThrough == passing || body == null) return;
            IsPassingThrough = passing;
            // Only the Enemy bit is owned here; flight owns the Pit bit of the same mask.
            if (passing) body.excludeLayers |= enemyMask;
            else body.excludeLayers &= ~enemyMask;
        }

        private void SetBlinkAlpha(bool dimmed)
        {
            if (spriteRenderer == null || blinking == dimmed) return;
            Color color = spriteRenderer.color;
            if (dimmed) solidAlpha = color.a;
            color.a = dimmed ? solidAlpha * BlinkAlpha : solidAlpha;
            spriteRenderer.color = color;
            blinking = dimmed;
        }
    }
}
