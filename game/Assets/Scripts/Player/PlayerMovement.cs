using TrickalFanGame.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerActionState), typeof(DamageInvulnerability), typeof(KnockbackReceiver))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private Rigidbody2D body;
        private Health health;
        private PlayerStats stats;
        private PlayerActionState actionState;
        private KnockbackReceiver knockback;
        private SpriteRenderer spriteRenderer;
        private Vector2 movement;

        public Vector2 FacingDirection { get; private set; } = Vector2.down;
        public Vector2 CurrentVelocity => body.linearVelocity;
        public float CurrentMoveSpeed => stats.MoveSpeed;
        public Vector2 MovementIntent => health != null && !health.IsDead ? movement : Vector2.zero;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            stats = GetComponent<PlayerStats>();
            actionState = GetComponent<PlayerActionState>();
            knockback = GetComponent<KnockbackReceiver>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && GetComponent<PlayerWalkAnimator>() == null)
            {
                gameObject.AddComponent<PlayerWalkAnimator>();
            }
            if (GetComponent<DamageInvulnerability>() == null)
            {
                gameObject.AddComponent<DamageInvulnerability>();
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (health.IsDead)
            {
                movement = Vector2.zero;
                return;
            }

            if (actionState.IsDashing)
            {
                Vector2 dashInput = ReadMovement();
                if (dashInput.sqrMagnitude > 0.001f)
                {
                    actionState.TryUpdateDashDirection(dashInput);
                    SetFacingDirection(dashInput);
                }

                movement = actionState.DashDirection;
                return;
            }

            if (actionState.IsCoastRecovering)
            {
                movement = actionState.DashDirection;
                return;
            }

            if (!actionState.CanMove)
            {
                movement = Vector2.zero;
                return;
            }

            movement = ReadMovement();
            if (movement.sqrMagnitude > 0.001f)
            {
                SetFacingDirection(movement);
            }
        }

        public void SetFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.001f) return;
            FacingDirection = direction.normalized;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.001f)
                spriteRenderer.flipX = direction.x > 0f;
        }

        private void FixedUpdate()
        {
            if (knockback != null && knockback.IsActive) return;
            float speedMultiplier = actionState.IsDashing || actionState.IsCoastRecovering
                ? actionState.DashSpeedMultiplier
                : 1f;
            body.linearVelocity = health.IsDead
                ? Vector2.zero
                : movement * CurrentMoveSpeed * speedMultiplier;
        }

        public void StopImmediately()
        {
            movement = Vector2.zero;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        public bool IsMoving => movement.sqrMagnitude > 0.001f && health != null && !health.IsDead;

        private static Vector2 ReadMovement()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            Vector2 input = Vector2.zero;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed) input.y += 1f;
            return input.normalized;
        }
    }
}
