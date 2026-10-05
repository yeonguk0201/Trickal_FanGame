using System.Collections.Generic;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    public readonly struct ProjectileSplitSettings
    {
        public ProjectileSplitSettings(
            int projectileCount,
            float damageMultiplier,
            float maximumDistance,
            float spreadAngleDegrees,
            float scaleMultiplier)
        {
            ProjectileCount = Mathf.Max(0, projectileCount);
            DamageMultiplier = Mathf.Max(0f, damageMultiplier);
            MaximumDistance = Mathf.Max(0f, maximumDistance);
            SpreadAngleDegrees = Mathf.Max(0f, spreadAngleDegrees);
            ScaleMultiplier = Mathf.Max(0f, scaleMultiplier);
        }

        public int ProjectileCount { get; }
        public float DamageMultiplier { get; }
        public float MaximumDistance { get; }
        public float SpreadAngleDegrees { get; }
        public float ScaleMultiplier { get; }
        public bool IsEnabled => ProjectileCount > 0 && DamageMultiplier > 0f && MaximumDistance > 0f &&
                                 SpreadAngleDegrees > 0f && ScaleMultiplier > 0f;
    }

    public static class ProjectileSizing
    {
        public const float BaseColliderRadius = 0.5f;
        public const float PlayerBasicScale = 0.4f;
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
        private ProjectileSplitSettings splitSettings;
        private Vector2 launchPosition;
        private float maximumTravelDistance;
        private bool isSplitProjectile;
        private float resolvedLifetime;
        private float remainingLifetime;
        private bool isSpent;

        public DamageContext DamageContext => damageContext;
        // True once the projectile is used up. Destroy only removes it at the end of the frame, so without this a
        // shot touching several overlapping enemies in one physics step would hit all of them.
        public bool IsSpent => isSpent;
        public bool IsLaunched => owner != null;
        public bool IsSplitProjectile => isSplitProjectile;
        public Vector2 LaunchPosition => launchPosition;
        public float MaximumTravelDistance => maximumTravelDistance;
        // Range-0: travel distance = flight time × shot speed. Launch may override the serialized lifetime.
        public float Lifetime => resolvedLifetime;
        public float RemainingLifetime => remainingLifetime;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            resolvedLifetime = lifetime;
            remainingLifetime = lifetime;
        }

        private void FixedUpdate()
        {
            if (TickLifetime(Time.fixedDeltaTime))
            {
                return;
            }

            if (maximumTravelDistance > 0f &&
                ((Vector2)transform.position - launchPosition).sqrMagnitude >=
                maximumTravelDistance * maximumTravelDistance)
            {
                StopAtBoundary();
            }
        }

        public void Launch(
            Vector2 velocity,
            Health projectileOwner,
            DamageContext configuredDamageContext,
            int configuredPierces = 0,
            ProjectileSplitSettings configuredSplitSettings = default,
            bool configuredAsSplitProjectile = false,
            float configuredMaximumTravelDistance = 0f,
            float configuredLifetime = 0f)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            owner = projectileOwner;
            damageContext = configuredDamageContext;
            remainingPierces = Mathf.Max(0, configuredPierces);
            splitSettings = configuredAsSplitProjectile ? default : configuredSplitSettings;
            isSplitProjectile = configuredAsSplitProjectile;
            maximumTravelDistance = Mathf.Max(0f, configuredMaximumTravelDistance);
            launchPosition = transform.position;
            resolvedLifetime = configuredLifetime > 0f ? configuredLifetime : lifetime;
            remainingLifetime = resolvedLifetime;
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
            if (collider == null || isSpent)
            {
                return;
            }

            if (collider.GetComponentInParent<DoorController>() != null)
            {
                StopAtBoundary();
                return;
            }

            if (collider.GetComponentInParent<DestructibleObstacle>() != null)
            {
                DestructibleObstacle.TryHitCollider(collider);
                DestroyProjectile();
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
                float impactDistance = Vector2.Distance(launchPosition, transform.position);
                target.TakeDamage(damageContext.WithImpactDistance(impactDistance));

                IgnoreTargetColliders(target);
                if (!isSplitProjectile && remainingPierces > 0 && splitSettings.IsEnabled)
                {
                    SplitAfterFirstPierce(target);
                    DestroyProjectile();
                    return;
                }

                if (remainingPierces > 0)
                {
                    remainingPierces--;
                    return;
                }
            }

            DestroyProjectile();
        }

        private void SplitAfterFirstPierce(Health firstTarget)
        {
            Vector2 forward = body != null && body.linearVelocity.sqrMagnitude > 0.001f
                ? body.linearVelocity.normalized
                : Vector2.right;
            float centerIndex = (splitSettings.ProjectileCount - 1) * 0.5f;
            for (int index = 0; index < splitSettings.ProjectileCount; index++)
            {
                float angle = (index - centerIndex) * splitSettings.SpreadAngleDegrees;
                Vector2 direction = Rotate(forward, angle);
                Projectile splitProjectile = Instantiate(this, transform.position, Quaternion.identity);
                splitProjectile.transform.localScale = transform.localScale * splitSettings.ScaleMultiplier;
                splitProjectile.Launch(
                    direction * body.linearVelocity.magnitude,
                    owner,
                    damageContext.ScaleMultiplier(splitSettings.DamageMultiplier),
                    configuredPierces: 0,
                    configuredSplitSettings: default,
                    configuredAsSplitProjectile: true,
                    configuredMaximumTravelDistance: splitSettings.MaximumDistance,
                    configuredLifetime: resolvedLifetime);
                splitProjectile.damagedTargets.Add(firstTarget);
                splitProjectile.IgnoreTargetColliders(firstTarget);
            }
        }

        // Returns true when the flight time ran out and the projectile expired.
        public bool TickLifetime(float deltaTime)
        {
            remainingLifetime -= Mathf.Max(0f, deltaTime);
            if (remainingLifetime > 0f)
            {
                return false;
            }

            Expire();
            return true;
        }

        // Range-0: reaching the end of the range is kept apart from hit and boundary removal so an end-of-range
        // visual asset can attach here later. For now it disappears immediately without a fade.
        private void Expire()
        {
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
            isSpent = true;
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

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sine = Mathf.Sin(radians);
            float cosine = Mathf.Cos(radians);
            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }
    }
}
