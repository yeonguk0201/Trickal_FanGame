using System.Collections.Generic;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class HomingSkillProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float speed = 7f;
        [SerializeField, Min(0f)] private float turnSpeedDegrees = 360f;
        [SerializeField, Min(0.01f)] private float lifetime = 2.5f;
        [SerializeField, Min(0.01f)] private float explosionRadius = 1.25f;
        [SerializeField] private LayerMask targetLayers = 1 << 6;

        private readonly HashSet<Health> damagedTargets = new();
        private Rigidbody2D body;
        private Health owner;
        private Health target;
        private DamageContext damageContext;
        private Vector2 direction = Vector2.down;
        private float remainingLifetime;
        private bool hasAssignedTarget;
        private bool isFinished;

        public Health Target => target;
        public DamageContext DamageContext => damageContext;
        public Vector2 Direction => direction;
        public bool DidExplode { get; private set; }
        public bool IsLaunched => owner != null;
        public bool HasAssignedTarget => hasAssignedTarget;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            Collider2D trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
            remainingLifetime = lifetime;
        }

        public void Launch(
            Vector2 initialDirection,
            Health configuredOwner,
            Health configuredTarget,
            DamageContext context,
            LayerMask configuredTargetLayers)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            owner = configuredOwner;
            target = configuredTarget;
            hasAssignedTarget = configuredTarget != null;
            damageContext = context;
            targetLayers = configuredTargetLayers;
            direction = initialDirection.sqrMagnitude > 0.001f ? initialDirection.normalized : Vector2.down;
            remainingLifetime = lifetime;
            body.linearVelocity = direction * speed;

            if (owner != null)
            {
                foreach (Collider2D ownerCollider in owner.GetComponentsInChildren<Collider2D>())
                {
                    Physics2D.IgnoreCollision(GetComponent<Collider2D>(), ownerCollider, true);
                }
            }
        }

        private void FixedUpdate()
        {
            if (isFinished)
            {
                return;
            }

            if (target != null && !target.IsDead)
            {
                Vector2 desired = (target.transform.position - transform.position).normalized;
                float currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                float desiredAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
                float nextAngle = Mathf.MoveTowardsAngle(currentAngle, desiredAngle, turnSpeedDegrees * Time.fixedDeltaTime);
                direction = new Vector2(
                    Mathf.Cos(nextAngle * Mathf.Deg2Rad),
                    Mathf.Sin(nextAngle * Mathf.Deg2Rad));
            }

            body.linearVelocity = direction * speed;
            remainingLifetime -= Time.fixedDeltaTime;
            if (remainingLifetime <= 0f)
            {
                ExpireWithoutExplosion();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Health hit = other.GetComponentInParent<Health>();
            if (hit == owner)
            {
                return;
            }

            if (hit != null && !hit.IsDead &&
                (targetLayers.value & (1 << hit.gameObject.layer)) != 0)
            {
                ExplodeNow();
                return;
            }

            bool isPortalDoor = other.GetComponentInParent<DoorController>() != null;
            bool isSolidBoundary = !other.isTrigger;
            if (!hasAssignedTarget && (isPortalDoor || isSolidBoundary))
            {
                StopAtBoundary();
            }
        }

        public void ExplodeNow()
        {
            if (isFinished)
            {
                return;
            }

            isFinished = true;
            DidExplode = true;
            body.linearVelocity = Vector2.zero;
            damagedTargets.Clear();
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(
                         transform.position,
                         explosionRadius,
                         targetLayers))
            {
                Health health = hit.GetComponentInParent<Health>();
                if (health == null || health == owner || health.IsDead ||
                    health.GetComponent<PlayerCombatEvents>() != null || !damagedTargets.Add(health))
                {
                    continue;
                }

                health.TakeDamage(damageContext);
            }

            DestroyProjectile();
        }

        public bool ExpireWithoutExplosion()
        {
            if (isFinished)
            {
                return DidExplode;
            }

            isFinished = true;
            body.linearVelocity = Vector2.zero;
            DestroyProjectile();
            return false;
        }

        public void StopAtBoundary()
        {
            ExpireWithoutExplosion();
        }

        private void DestroyProjectile()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
