using TrickalFanGame.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerActionState))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private Rigidbody2D body;
        private Health health;
        private PlayerStats stats;
        private PlayerActionState actionState;
        private Vector2 movement;

        public Vector2 FacingDirection { get; private set; } = Vector2.down;
        public Vector2 CurrentVelocity => body.linearVelocity;
        public float CurrentMoveSpeed => stats.MoveSpeed;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            stats = GetComponent<PlayerStats>();
            actionState = GetComponent<PlayerActionState>();
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
                    FacingDirection = dashInput;
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
                FacingDirection = movement.normalized;
            }
        }

        private void FixedUpdate()
        {
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
