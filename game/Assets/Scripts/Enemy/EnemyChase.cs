using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class EnemyChase : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [SerializeField, Min(0f)] private float detectionRange = 6f;
        [SerializeField, Min(0f)] private float stopDistance = 0.8f;
        [SerializeField] private Transform target;

        private Rigidbody2D body;
        private Health health;
        private Health targetHealth;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
        }

        private void Start()
        {
            FindTargetIfNeeded();
        }

        private void FixedUpdate()
        {
            if (health.IsDead || !FindTargetIfNeeded() || targetHealth.IsDead)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 offset = target.position - transform.position;
            float distance = offset.magnitude;
            if (distance > detectionRange || distance <= stopDistance)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            body.linearVelocity = offset / distance * moveSpeed;
        }

        private bool FindTargetIfNeeded()
        {
            if (target != null && targetHealth != null)
            {
                return true;
            }

            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                return false;
            }

            target = player.transform;
            targetHealth = player.GetComponent<Health>();
            return targetHealth != null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, stopDistance);
        }
    }
}
