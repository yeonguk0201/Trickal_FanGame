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
            SplitsOnHit = false;
        }

        private ProjectileSplitSettings(float damageMultiplier, float maximumDistance, float scaleMultiplier)
        {
            ProjectileCount = OnHitDirections.Length;
            DamageMultiplier = Mathf.Max(0f, damageMultiplier);
            MaximumDistance = Mathf.Max(0f, maximumDistance);
            SpreadAngleDegrees = 0f;
            ScaleMultiplier = Mathf.Max(0f, scaleMultiplier);
            SplitsOnHit = true;
        }

        // Passive-5 (다야의 다이아몬드 커터): split shots leave the enemy that was hit toward the screen's up, down,
        // left and right.
        public static readonly Vector2[] OnHitDirections = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        // Every hit of the shot on an enemy splits, without a pierce. The scale is a share of the base basic
        // attack shot, whatever the shot's own size is.
        public static ProjectileSplitSettings OnHit(float damageMultiplier, float maximumDistance,
            float scaleMultiplier) => new(damageMultiplier, maximumDistance, scaleMultiplier);

        public int ProjectileCount { get; }
        public float DamageMultiplier { get; }
        public float MaximumDistance { get; }
        public float SpreadAngleDegrees { get; }
        public float ScaleMultiplier { get; }
        // False = the legacy split of effect 16: once, after the first pierce, as a fan behind the enemy.
        public bool SplitsOnHit { get; }
        public bool IsEnabled => ProjectileCount > 0 && DamageMultiplier > 0f && MaximumDistance > 0f &&
                                 (SplitsOnHit || SpreadAngleDegrees > 0f) && ScaleMultiplier > 0f;
    }

    public static class ProjectileSizing
    {
        public const float BaseColliderRadius = 0.5f;
        public const float PlayerBasicScale = 0.4f;
        public const float PlayerSkillScale = 0.28f;
        public const float RangedEnemyScale = 0.3f;
        public const float BossScale = 0.35f;
        // Passive-0 §4.1: a grown basic attack shot must still pass a one-tile corridor.
        public const float MaximumPlayerBasicRadius = 0.45f;
        public static float MaximumPlayerBasicSizeMultiplier =>
            MaximumPlayerBasicRadius / WorldCollisionRadius(PlayerBasicScale);

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
        private const int EnemyLayerMask = 1 << 6;
        private const float BounceContactDistance = 0.02f;

        // How often this shot has hit each enemy. Only a bounce (Passive-0 §4.4) hits the same enemy again.
        private readonly Dictionary<Health, int> hitCounts = new();
        private int remainingPierces;
        private ProjectileBounceSettings bounceSettings;
        private int remainingBounces;
        // The enemy the shot flies to after a bounce.
        private Health bounceTarget;
        // With no other enemy near, the shot waits on the enemy it just hit and hits it again.
        private Health repeatTarget;
        private float repeatHitDelay;
        private Vector2 flightVelocity;
        private Vector2 travelOrigin;
        private ProjectileSplitSettings splitSettings;
        private Vector2 launchPosition;
        private float maximumTravelDistance;
        private bool isSplitProjectile;
        private ProjectileHitEffects hitEffects;
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
        public int RemainingBounces => remainingBounces;
        public Health BounceTarget => bounceTarget;
        public Health RepeatTarget => repeatTarget;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            resolvedLifetime = lifetime;
            remainingLifetime = lifetime;
        }

        private void FixedUpdate()
        {
            if (TickLifetime(Time.fixedDeltaTime) || TickBounce(Time.fixedDeltaTime))
            {
                return;
            }

            if (maximumTravelDistance > 0f &&
                ((Vector2)transform.position - travelOrigin).sqrMagnitude >=
                maximumTravelDistance * maximumTravelDistance)
            {
                StopAtBoundary();
            }
        }

        // Returns true when the shot is waiting on an enemy or was used up by the hit it just made.
        public bool TickBounce(float deltaTime)
        {
            if (repeatTarget != null)
            {
                if (repeatTarget.IsDead || !repeatTarget.gameObject.activeInHierarchy)
                {
                    repeatTarget = null;
                    body.linearVelocity = flightVelocity;
                    return false;
                }

                repeatHitDelay -= Mathf.Max(0f, deltaTime);
                if (repeatHitDelay <= 0f)
                {
                    Health target = repeatTarget;
                    body.linearVelocity = flightVelocity;
                    HitTarget(target);
                }

                return true;
            }

            if (bounceTarget == null)
            {
                return false;
            }

            if (bounceTarget.IsDead || !bounceTarget.gameObject.activeInHierarchy)
            {
                bounceTarget = null;
                return false;
            }

            Vector2 offset = bounceTarget.transform.position - transform.position;
            if (offset.sqrMagnitude > 0.0001f)
            {
                SetFlightVelocity(offset.normalized * flightVelocity.magnitude);
            }

            Collider2D ownCollider = GetComponent<Collider2D>();
            Collider2D targetCollider = bounceTarget.GetComponentInChildren<Collider2D>();
            if (offset.sqrMagnitude <= BounceContactDistance * BounceContactDistance ||
                (ownCollider != null && targetCollider != null &&
                 ownCollider.Distance(targetCollider).distance <= BounceContactDistance))
            {
                HitTarget(bounceTarget);
            }

            return isSpent;
        }

        public void Launch(
            Vector2 velocity,
            Health projectileOwner,
            DamageContext configuredDamageContext,
            int configuredPierces = 0,
            ProjectileSplitSettings configuredSplitSettings = default,
            bool configuredAsSplitProjectile = false,
            float configuredMaximumTravelDistance = 0f,
            float configuredLifetime = 0f,
            ProjectileHitEffects configuredHitEffects = default,
            ProjectileBounceSettings configuredBounce = default)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            // Passive-0 §4.6: a shot never shoves what it touches. Enemies move only by the knockback formula.
            foreach (Collider2D projectileCollider in GetComponentsInChildren<Collider2D>())
            {
                projectileCollider.isTrigger = true;
            }

            owner = projectileOwner;
            damageContext = configuredDamageContext;
            remainingPierces = Mathf.Max(0, configuredPierces);
            splitSettings = configuredAsSplitProjectile ? default : configuredSplitSettings;
            isSplitProjectile = configuredAsSplitProjectile;
            hitEffects = configuredHitEffects;
            bounceSettings = configuredBounce;
            remainingBounces = configuredBounce.IsEnabled ? configuredBounce.BounceCount : 0;
            bounceTarget = null;
            repeatTarget = null;
            flightVelocity = velocity;
            maximumTravelDistance = Mathf.Max(0f, configuredMaximumTravelDistance);
            launchPosition = transform.position;
            travelOrigin = launchPosition;
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
            if (target == owner || (target != null && target != bounceTarget && hitCounts.ContainsKey(target)))
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
                HitTarget(target);
                return;
            }

            DestroyProjectile();
        }

        // One hit on an enemy (Passive-0 §3): damage, hit effects, then the first course that applies of
        // split → bounce → pierce → vanish.
        private void HitTarget(Health target)
        {
            bounceTarget = null;
            repeatTarget = null;
            hitCounts.TryGetValue(target, out int earlierHits);
            hitCounts[target] = earlierHits + 1;
            // A bounce that returns to an enemy deals less each time; a first hit is always whole.
            DamageContext context = earlierHits > 0
                ? damageContext.ScaleMultiplier(Mathf.Pow(bounceSettings.RepeatDamageRatio, earlierHits))
                : damageContext;
            float impactDistance = Vector2.Distance(launchPosition, transform.position);
            float healthBeforeHit = target.CurrentHealth + target.CurrentShield;
            target.TakeDamage(context.WithImpactDistance(impactDistance));
            ApplyHitEffects(target, healthBeforeHit - (target.CurrentHealth + target.CurrentShield));

            IgnoreTargetColliders(target);
            if (!isSplitProjectile && splitSettings.IsEnabled)
            {
                if (splitSettings.SplitsOnHit)
                {
                    // The split does not use up the shot: it goes on to bounce, pierce or vanish.
                    SplitOnHit(target);
                }
                else if (remainingPierces > 0)
                {
                    SplitAfterFirstPierce(target);
                    DestroyProjectile();
                    return;
                }
            }

            if (TryBounce(target))
            {
                return;
            }

            if (remainingPierces > 0)
            {
                remainingPierces--;
                return;
            }

            DestroyProjectile();
        }

        private bool TryBounce(Health from)
        {
            if (remainingBounces <= 0 || !bounceSettings.IsEnabled || flightVelocity.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Health next = FindBounceTarget(from);
            if (next == null)
            {
                // No other enemy: the shot hits the same enemy again, unless this hit killed it.
                if (from.IsDead)
                {
                    return false;
                }

                remainingBounces--;
                repeatTarget = from;
                repeatHitDelay = bounceSettings.SameTargetDelaySeconds;
                remainingLifetime = Mathf.Max(remainingLifetime, repeatHitDelay + Time.fixedDeltaTime);
                body.linearVelocity = Vector2.zero;
                return true;
            }

            remainingBounces--;
            bounceTarget = next;
            IgnoreTargetColliders(next, false);
            Vector2 offset = next.transform.position - transform.position;
            float distance = offset.magnitude;
            float speed = flightVelocity.magnitude;
            if (distance > 0.0001f)
            {
                SetFlightVelocity(offset / distance * speed);
            }

            // The shot must reach the enemy it bounces to.
            remainingLifetime = Mathf.Max(remainingLifetime, distance / speed + Time.fixedDeltaTime);
            travelOrigin = transform.position;
            if (maximumTravelDistance > 0f)
            {
                maximumTravelDistance = Mathf.Max(maximumTravelDistance, distance + 1f);
            }

            return true;
        }

        private Health FindBounceTarget(Health from)
        {
            Vector2 center = from.transform.position;
            Health nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, bounceSettings.SearchRadius,
                         EnemyLayerMask))
            {
                Health candidate = hit.GetComponentInParent<Health>();
                if (candidate == null || candidate == from || candidate == owner || candidate.IsDead ||
                    !candidate.gameObject.activeInHierarchy ||
                    candidate.GetComponent<PlayerCombatEvents>() != null)
                {
                    continue;
                }

                float distance = ((Vector2)candidate.transform.position - center).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private void SetFlightVelocity(Vector2 velocity)
        {
            flightVelocity = velocity;
            body.linearVelocity = velocity;
        }

        // Passive-0 §3: after the direct damage a living target gets status effects, then knockback.
        private void ApplyHitEffects(Health target, float appliedDamage)
        {
            if (target.IsDead)
            {
                return;
            }

            if (appliedDamage > 0f)
            {
                // Poison, burn and shock are rolled independently, in that order.
                EnemyStatusEffects.TryApplyPoison(target, damageContext.Source, damageContext.BaseDamage,
                    hitEffects.Poison, Random.value, hitEffects.StatusTickDamageMultiplier);
                EnemyStatusEffects.TryApplyBurn(target, damageContext.Source, damageContext.BaseDamage,
                    hitEffects.Burn, Random.value, hitEffects.StatusTickDamageMultiplier);
                EnemyStatusEffects.TryApplyShock(target, hitEffects.Shock, Random.value);
            }

            if (hitEffects.AppliesKnockback)
            {
                BasicAttackKnockback.TryApply(target, flightVelocity, hitEffects.KnockbackBonus);
            }
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
                    configuredLifetime: resolvedLifetime,
                    configuredHitEffects: hitEffects,
                    // Passive-0 §4.4: a split shot inherits one bounce.
                    configuredBounce: bounceSettings.IsEnabled ? bounceSettings.WithBounceCount(1) : default);
                splitProjectile.hitCounts[firstTarget] = 1;
                splitProjectile.IgnoreTargetColliders(firstTarget);
            }
        }

        // 다야의 다이아몬드 커터: four small shots leave the enemy that was hit. They inherit the shot's remaining
        // pierces. Without a pierce they cannot hit that enemy; with one they start inside it and hit it again.
        private void SplitOnHit(Health hitTarget)
        {
            float speed = flightVelocity.magnitude;
            if (speed <= 0.0001f)
            {
                return;
            }

            foreach (Vector2 direction in ProjectileSplitSettings.OnHitDirections)
            {
                Projectile splitProjectile = Instantiate(this, hitTarget.transform.position, Quaternion.identity);
                splitProjectile.transform.localScale =
                    Vector3.one * (ProjectileSizing.PlayerBasicScale * splitSettings.ScaleMultiplier);
                splitProjectile.Launch(
                    direction * speed,
                    owner,
                    damageContext.ScaleMultiplier(splitSettings.DamageMultiplier),
                    configuredPierces: remainingPierces,
                    configuredSplitSettings: default,
                    configuredAsSplitProjectile: true,
                    configuredMaximumTravelDistance: splitSettings.MaximumDistance,
                    configuredLifetime: resolvedLifetime,
                    configuredHitEffects: hitEffects,
                    configuredBounce: bounceSettings.IsEnabled ? bounceSettings.WithBounceCount(1) : default);
                if (remainingPierces <= 0)
                {
                    splitProjectile.hitCounts[hitTarget] = 1;
                    splitProjectile.IgnoreTargetColliders(hitTarget);
                }
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

        private void IgnoreTargetColliders(Health target, bool ignore = true)
        {
            foreach (Collider2D targetCollider in target.GetComponentsInChildren<Collider2D>())
            {
                foreach (Collider2D projectileCollider in GetComponentsInChildren<Collider2D>())
                {
                    Physics2D.IgnoreCollision(projectileCollider, targetCollider, ignore);
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
