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
            EnemyDamageTier damageTier,
            float speed,
            float lifetime,
            Sprite sprite,
            bool authoredArtwork = false)
        {
            GameObject projectileObject = new("Enemy Projectile");
            if (source != null && source.transform.parent != null)
            {
                projectileObject.transform.SetParent(source.transform.parent);
            }

            projectileObject.transform.position = position;
            SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = authoredArtwork ? Color.white : new Color(0.2f, 0.8f, 1f);
            // Authored projectile points left; only rotate the artwork, leaving the circular hitbox unchanged.
            if (authoredArtwork && direction.sqrMagnitude > 0.001f)
                renderer.transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 180f);

            Rigidbody2D projectileBody = projectileObject.AddComponent<Rigidbody2D>();
            projectileBody.bodyType = RigidbodyType2D.Kinematic;
            projectileBody.gravityScale = 0f;
            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            ProjectileSizing.Apply(projectileObject.transform, collider, ProjectileSizing.RangedEnemyScale);

            EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
            projectile.Launch(direction, source, damageTier, speed, lifetime);
            return projectile;
        }

        public void Launch(
            Vector2 direction,
            GameObject source,
            EnemyDamageTier damageTier,
            float speed,
            float lifetime)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            owner = source;
            damageContext = HealthUnits.CreateEnemyDamageContext(source, DamageSourceType.EnemyProjectile, damageTier);
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
                Expire();
            }
        }

        // Range-0: travel distance = lifetime × speed per enemy. Reaching the end of the range is kept apart from
        // hits so an end-of-range visual asset can attach here later. For now it disappears immediately.
        private void Expire()
        {
            StopAtBoundary();
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
