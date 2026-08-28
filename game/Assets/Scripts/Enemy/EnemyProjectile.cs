using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private Rigidbody2D body;
        private GameObject owner;
        private DamageContext damageContext;
        private float expiresAt;

        public bool IsLaunched { get; private set; }
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public DamageContext DamageContext => damageContext;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        public static EnemyProjectile Create(
            Vector2 position,
            Vector2 direction,
            GameObject source,
            float attackDamage,
            float speed,
            float lifetime,
            Sprite sprite)
        {
            GameObject projectileObject = new("Enemy Projectile");
            if (source != null && source.transform.parent != null)
            {
                projectileObject.transform.SetParent(source.transform.parent);
            }

            projectileObject.transform.position = position;
            projectileObject.transform.localScale = Vector3.one * 0.3f;
            SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.2f, 0.8f, 1f);

            Rigidbody2D projectileBody = projectileObject.AddComponent<Rigidbody2D>();
            projectileBody.bodyType = RigidbodyType2D.Kinematic;
            projectileBody.gravityScale = 0f;
            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;

            EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
            projectile.Launch(direction, source, attackDamage, speed, lifetime);
            return projectile;
        }

        public void Launch(
            Vector2 direction,
            GameObject source,
            float attackDamage,
            float speed,
            float lifetime)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            owner = source;
            damageContext = new DamageContext(source, DamageSourceType.EnemyProjectile, Mathf.Max(0.01f, attackDamage));
            body.linearVelocity = direction.sqrMagnitude > 0.001f
                ? direction.normalized * Mathf.Max(0f, speed)
                : Vector2.zero;
            expiresAt = Time.time + Mathf.Max(0.01f, lifetime);
            IsLaunched = true;
        }

        private void Update()
        {
            TickLifetime(Time.time);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHit(other);
        }

        public void TickLifetime(float currentTime)
        {
            if (IsLaunched && currentTime >= expiresAt)
            {
                StopAtBoundary();
            }
        }

        public bool TryHit(Collider2D other)
        {
            if (!IsLaunched || other == null ||
                (owner != null && other.transform.IsChildOf(owner.transform)))
            {
                return false;
            }

            if (other.GetComponentInParent<DoorController>() != null ||
                other.gameObject.layer == LayerMask.NameToLayer("Environment"))
            {
                StopAtBoundary();
                return true;
            }

            Health target = other.GetComponentInParent<Health>();
            if (target == null || target.IsDead || target.GetComponent<PlayerMovement>() == null)
            {
                return false;
            }

            target.GetComponent<PlayerDeathReason>()?.SetReason("ENEMY");
            target.TakeDamage(damageContext);
            StopAtBoundary();
            return true;
        }

        public void StopAtBoundary()
        {
            if (!IsLaunched)
            {
                return;
            }

            IsLaunched = false;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
