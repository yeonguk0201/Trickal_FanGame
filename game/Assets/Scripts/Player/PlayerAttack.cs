using System.Collections.Generic;
using TrickalFanGame.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Player
{
    [RequireComponent(typeof(Health), typeof(PlayerCombatEvents), typeof(PlayerActionState))]
    public sealed class PlayerAttack : MonoBehaviour
    {
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0f)] private float attackRange = 1f;
        [SerializeField, Min(0f)] private float attackRadius = 0.5f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.35f;
        [SerializeField] private LayerMask targetLayers = ~0;

        private readonly Collider2D[] hitBuffer = new Collider2D[16];
        private Health health;
        private PlayerActionState actionState;
        private ContactFilter2D targetFilter;
        private float nextAttackTime;

        public Vector2 AimDirection { get; private set; } = Vector2.down;
        public bool CanAttack => !health.IsDead && actionState.CanBasicAttack;

        private void Awake()
        {
            health = GetComponent<Health>();
            actionState = GetComponent<PlayerActionState>();
            targetFilter = new ContactFilter2D { useLayerMask = true, layerMask = targetLayers };
        }

        private void Update()
        {
            if (!CanAttack || Time.time < nextAttackTime || !TryReadAttackDirection(out Vector2 direction))
            {
                return;
            }

            AimDirection = direction;
            nextAttackTime = Time.time + attackCooldown;
            DealDamage();
        }

        private void DealDamage()
        {
            Vector2 center = (Vector2)transform.position + AimDirection * attackRange;
            int hitCount = Physics2D.OverlapCircle(center, attackRadius, targetFilter, hitBuffer);
            var hitTargets = new HashSet<Health>();

            for (int index = 0; index < hitCount; index++)
            {
                Health target = hitBuffer[index].GetComponentInParent<Health>();
                if (target == null || target == health || !hitTargets.Add(target))
                {
                    continue;
                }

                target.TakeDamage(new DamageContext(gameObject, DamageSourceType.PlayerAttack, damage));
            }
        }

        private static bool TryReadAttackDirection(out Vector2 direction)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed)
                {
                    direction = Vector2.left;
                    return true;
                }

                if (keyboard.rightArrowKey.isPressed)
                {
                    direction = Vector2.right;
                    return true;
                }

                if (keyboard.downArrowKey.isPressed)
                {
                    direction = Vector2.down;
                    return true;
                }

                if (keyboard.upArrowKey.isPressed)
                {
                    direction = Vector2.up;
                    return true;
                }
            }

            direction = Vector2.zero;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere((Vector2)transform.position + AimDirection * attackRange, attackRadius);
        }
    }
}
