using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0.01f)] private float lifetime = 2f;

        private Rigidbody2D body;
        private Health owner;
        private readonly HashSet<Health> damagedTargets = new();
        private int remainingPierces;
        private Action targetKilled;

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
            int configuredDamage = -1,
            int configuredPierces = 0,
            Action configuredTargetKilled = null)
        {
            owner = projectileOwner;
            if (configuredDamage > 0)
            {
                damage = configuredDamage;
            }
            remainingPierces = Mathf.Max(0, configuredPierces);
            targetKilled = configuredTargetKilled;
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
                bool wasAlive = !target.IsDead;
                damagedTargets.Add(target);
                target.TakeDamage(damage);
                if (wasAlive && target.IsDead)
                {
                    targetKilled?.Invoke();
                }

                IgnoreTargetColliders(target);
                if (remainingPierces > 0)
                {
                    remainingPierces--;
                    return;
                }
            }

            Destroy(gameObject);
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
