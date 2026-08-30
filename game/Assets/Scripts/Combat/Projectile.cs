using System.Collections.Generic;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    public static class ProjectileSizing
    {
        public const float BaseColliderRadius = 0.5f;
        public const float PlayerBasicScale = 0.5f;
        public const float PlayerSkillScale = 0.28f;
        public const float RangedEnemyScale = 0.3f;
        public const float BossScale = 0.35f;

        public static void Apply(Transform projectileTransform, CircleCollider2D collider, float scale)
        {
            projectileTransform.localScale = Vector3.one * scale;
            collider.radius = BaseColliderRadius;
        }

        public static float WorldCollisionRadius(float scale)
        {
            return BaseColliderRadius * scale;
        }
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float lifetime = 2f;

        private Rigidbody2D body;
        private Health owner;
        private DamageContext damageContext;
        private readonly HashSet<Health> damagedTargets = new();
        private int remainingPierces;

        public DamageContext DamageContext => damageContext;
        public bool IsLaunched => owner != null;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            Destroy(gameObject, lifetime);
        }

        public void Launch(
            Vector2 velocity,
            Health projectileOwner,
            DamageContext configuredDamageContext,
            int configuredPierces = 0)
        {
            owner = projectileOwner;
            damageContext = configuredDamageContext;
            remainingPierces = Mathf.Max(0, configuredPierces);
            body.linearVelocity = velocity;

            if (owner == null)
            {
                return;
            }

            foreach (Collider2D ownerCollider in owner.GetComponentsInChildren<Collider2D>())
            {
                foreach (Collider2D projectileCollider in GetComponentsInChildren<Collider2D>())
                {
                    Physics2D.IgnoreCollision(projectileCollider, ownerCollider);
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            Hit(collision.collider);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Hit(other);
        }

        private void Hit(Collider2D collider)
        {
            if (collider == null)
            {
                return;
            }

            if (collider.GetComponentInParent<DoorController>() != null)
            {
                StopAtBoundary();
                return;
            }

            Health target = collider.GetComponentInParent<Health>();
            if (target == owner || (target != null && damagedTargets.Contains(target)))
            {
                return;
            }

            // Room entry zones and other non-combat triggers should not consume projectiles.
            if (collider.isTrigger && target == null)
            {
                return;
            }

            if (target != null)
            {
                damagedTargets.Add(target);
                target.TakeDamage(damageContext);

                IgnoreTargetColliders(target);
                if (remainingPierces > 0)
                {
                    remainingPierces--;
                    return;
                }
            }

            DestroyProjectile();
        }

        public void StopAtBoundary()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            DestroyProjectile();
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

        private void IgnoreTargetColliders(Health target)
        {
            foreach (Collider2D targetCollider in target.GetComponentsInChildren<Collider2D>())
            {
                foreach (Collider2D projectileCollider in GetComponentsInChildren<Collider2D>())
                {
                    Physics2D.IgnoreCollision(projectileCollider, targetCollider);
                }
            }
        }
    }
}
