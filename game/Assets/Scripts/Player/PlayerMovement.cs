using TrickalFanGame.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;

        private Rigidbody2D body;
        private Health health;
        private Vector2 movement;
        private float moveSpeedBonus;

        public Vector2 FacingDirection { get; private set; } = Vector2.down;
        public Vector2 CurrentVelocity => body.linearVelocity;
        public float CurrentMoveSpeed => moveSpeed + moveSpeedBonus;

        public void AddMoveSpeedBonus(float amount)
        {
            moveSpeedBonus = Mathf.Max(0f, moveSpeedBonus + amount);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (health.IsDead)
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
            body.linearVelocity = health.IsDead ? Vector2.zero : movement * CurrentMoveSpeed;
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
