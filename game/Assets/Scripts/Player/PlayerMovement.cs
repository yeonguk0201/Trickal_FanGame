using TrickalFanGame.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerActionState), typeof(DamageInvulnerability), typeof(KnockbackReceiver))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private void OnEnable() => TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);

        private Rigidbody2D body;
        private Health health;
        private PlayerStats stats;
        private PlayerActionState actionState;
        private KnockbackReceiver knockback;
        private SpriteRenderer spriteRenderer;
        private PlayerFeet feet;
        private Vector2 movement;

        public Vector2 FacingDirection { get; private set; } = Vector2.down;
        public bool IsAimingAttack { get; private set; }
        public Vector2 CurrentVelocity => body.linearVelocity;
        public float CurrentMoveSpeed => stats.MoveSpeed;
        public Vector2 MovementIntent => health != null && !health.IsDead ? movement : Vector2.zero;

        // Hitbox-0: the terrain collider. A fixture built without one falls back to the root.
        public PlayerFeet Feet
        {
            get
            {
                if (feet == null) feet = GetComponentInChildren<PlayerFeet>(true);
                return feet;
            }
        }

        public Vector2 FeetPosition => Feet != null ? Feet.WorldCenter : (Vector2)transform.position;

        public bool FeetOverlap(Collider2D other) => Feet == null || Feet.Overlaps(other);

        // Hitbox-1: where the root stands at size 1. A larger body grows upward from its feet, so its root sits
        // above this point; ground rules (placement, pits, bombs) use it instead of the root.
        public Vector2 StandingPosition =>
            Feet != null ? Feet.WorldCenter - PlayerFeet.LocalPosition : (Vector2)transform.position;

        public Vector2 RootPositionForStanding(Vector2 standingPosition) =>
            standingPosition + ((Vector2)transform.position - StandingPosition);

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
            if (GetComponent<PlayerHitPassThrough>() == null)
            {
                gameObject.AddComponent<PlayerHitPassThrough>();
            }
            // The Player layer does not collide with terrain, so a player without feet would walk through walls.
            PlayerFeet.Ensure(gameObject, out feet);
            if (GetComponent<PlayerBodySize>() == null)
            {
                gameObject.AddComponent<PlayerBodySize>();
            }
        }

        private void Update()
        {
            IsAimingAttack = false;
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
            PlayerProjectileAttack projectileAttack = GetComponent<PlayerProjectileAttack>();
            PlayerAttack meleeAttack = GetComponent<PlayerAttack>();
            bool hasAttack = (projectileAttack != null && projectileAttack.isActiveAndEnabled) ||
                             (meleeAttack != null && meleeAttack.isActiveAndEnabled);
            // Read the same input as the attacks, even between shots; movement must not override held aim.
            if (hasAttack && actionState.CanBasicAttack && PlayerAttack.TryReadAttackDirection(out Vector2 aim))
            {
                IsAimingAttack = true;
                SetFacingDirection(aim);
            }
            else if (movement.sqrMagnitude > 0.001f)
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
