using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // Passive-4 (샤샤의 항아리, Passive-0 §4.5): the basic attack is a straight water stream. While the attack key
    // is held it fires once per attack cooldown, like a shot, and each burst shows only for a moment. A burst
    // reaches the room's wall or door whatever the shot range is, passes over obstacles while hitting them, and
    // hits every enemy on it. One hit equals one basic attack shot: same damage, critical hit, distance damage,
    // basic attack hit event, status effects and (weaker) knockback.
    //
    // Shot modifiers do not carry over by themselves (Passive-0 §9). The ones defined for the stream:
    // - 다중 투사체: that many parallel streams.
    // - 칸나의 대포: a wider stream.
    // - 다야의 다이아몬드 커터: every hit sends four short streams up, down, left and right from the enemy.
    // - 칸타의 팽이: every hit jumps to nearby enemies over a short range.
    // - Range (장난감 망원경) and shot speed (실라의 바람살) do nothing.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(PlayerStats), typeof(PlayerCombatEvents))]
    public sealed class PlayerWaterStream : MonoBehaviour
    {
        public const float MaximumLength = 40f;
        // Distance between the centre lines of parallel streams.
        public const float BeamSpacing = 0.4f;
        public const float SplitLength = 1.5f;
        public const float BounceRange = 2f;

        private const int EnemyLayerMask = 1 << 6;
        private const float FlashSeconds = 0.08f;
        // How long one burst stays on screen, and the largest share of the attack interval it may fill.
        private const float BurstSeconds = 0.12f;
        private const float BurstIntervalShare = 0.5f;

        private static readonly Vector2[] SplitDirections = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        private static readonly Color StreamColor = new(0.35f, 0.75f, 1f, 0.85f);

        private readonly HashSet<Object> beamTargets = new();
        private readonly HashSet<Health> chainTargets = new();
        private readonly List<LineRenderer> beamVisuals = new();
        private Health health;
        private PlayerStats stats;
        private PlayerActionState actionState;
        private PlayerProjectileAttack projectileAttack;
        private Material streamMaterial;
        private float width;
        private float knockbackShare;
        private float nextFireTime = float.NegativeInfinity;
        private float burstHideTime;

        public bool IsConfigured => width > 0f;
        // True while the attack direction is held, between bursts too.
        public bool IsFiring { get; private set; }
        public int BurstCount { get; private set; }
        // Length of the first stream of the latest burst.
        public float LastLength { get; private set; }
        public float BeamWidth => width * (stats != null ? stats.ProjectileSizeMultiplier : 1f);
        public int BeamCount => stats != null ? Mathf.Max(1, stats.ProjectileCount) : 1;
        public float HitInterval => projectileAttack != null
            ? projectileAttack.AttackInterval
            : 0.35f / (stats != null ? stats.AttackSpeed : 1f);

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            EnsureReferences();
            bool canFire = IsConfigured && !health.IsDead && (actionState == null || actionState.CanBasicAttack);
            Vector2 direction = Vector2.zero;
            Tick(canFire && PlayerAttack.TryReadAttackDirection(out direction) ? direction : (Vector2?)null, Time.time);
            if (Time.time >= burstHideTime) HideBeams();
        }

        private void OnDisable()
        {
            StopFiring();
        }

        public void Configure(float configuredWidth, float configuredKnockbackShare)
        {
            if (IsConfigured || configuredWidth <= 0f)
            {
                return;
            }

            width = configuredWidth;
            knockbackShare = Mathf.Clamp01(configuredKnockbackShare);
            EnsureReferences();
        }

        // Holds the stream in the given direction; null releases it. Returns true when a burst fired: the first
        // one at once, the following ones an attack cooldown apart. Releasing does not shorten the cooldown.
        public bool Tick(Vector2? direction, float currentTime)
        {
            if (!IsConfigured || !direction.HasValue || direction.Value.sqrMagnitude <= 0.0001f)
            {
                IsFiring = false;
                return false;
            }

            EnsureReferences();
            IsFiring = true;
            if (currentTime < nextFireTime)
            {
                return false;
            }

            float interval = HitInterval;
            nextFireTime = currentTime + interval;
            burstHideTime = currentTime + Mathf.Min(BurstSeconds, interval * BurstIntervalShare);
            BurstCount++;
            Vector2 forward = direction.Value.normalized;
            Vector2 side = new(-forward.y, forward.x);
            int beamCount = BeamCount;
            float halfWidth = BeamWidth * 0.5f;
            for (int beam = 0; beam < beamCount; beam++)
            {
                Vector2 origin = (Vector2)transform.position + side * ((beam - (beamCount - 1) * 0.5f) * BeamSpacing);
                float length = ResolveLength(origin, forward, MaximumLength);
                if (beam == 0) LastLength = length;
                HitAlong(origin, forward, length, halfWidth);
                ShowBeam(beam, origin, origin + forward * length, halfWidth * 2f);
            }

            for (int index = beamCount; index < beamVisuals.Count; index++)
            {
                beamVisuals[index].enabled = false;
            }
            for (int index = beamCount; index < artworkBeams.Count; index++) artworkBeams[index].enabled = false;

            return true;
        }

        // Distance from the origin to the first wall or door. Obstacles, chests, pits and enemies do not stop it.
        public static float ResolveLength(Vector2 origin, Vector2 direction, float maximumLength)
        {
            float length = maximumLength;
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(origin, direction, maximumLength))
            {
                if (hit.collider != null && hit.distance < length && IsStreamBlocker(hit.collider))
                {
                    length = hit.distance;
                }
            }

            return length;
        }

        public static bool IsStreamBlocker(Collider2D collider)
        {
            if (collider.GetComponentInParent<DoorController>() != null)
            {
                return true;
            }

            return !collider.isTrigger &&
                   collider.gameObject.layer == LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName) &&
                   collider.GetComponentInParent<DestructibleObstacle>() == null &&
                   collider.GetComponentInParent<RoomStaticObstacle>() == null &&
                   collider.GetComponentInParent<TreasureChest>() == null;
        }

        // Parallel streams hit independently: an enemy crossed by two of them is hit twice in one burst.
        private void HitAlong(Vector2 origin, Vector2 forward, float length, float halfWidth)
        {
            beamTargets.Clear();
            foreach (RaycastHit2D hit in Physics2D.CircleCastAll(origin, halfWidth, forward, length))
            {
                if (hit.collider == null) continue;
                DestructibleObstacle obstacle = hit.collider.GetComponentInParent<DestructibleObstacle>();
                if (obstacle != null)
                {
                    if (beamTargets.Add(obstacle))
                    {
                        DestructibleObstacle.TryHitCollider(hit.collider);
                    }

                    continue;
                }

                Health target = hit.collider.GetComponentInParent<Health>();
                if (!IsEnemy(target) || !beamTargets.Add(target))
                {
                    continue;
                }

                HitEnemy(target, forward);
            }
        }

        private void HitEnemy(Health target, Vector2 forward)
        {
            ApplyHit(target, forward, 1f);
            if (stats.ProjectileSplitSettings.IsEnabled)
            {
                SplitFrom(target);
            }

            if (stats.ProjectileBounce.IsEnabled)
            {
                BounceFrom(target);
            }
        }

        private void ApplyHit(Health target, Vector2 direction, float damageScale)
        {
            float impactDistance = Vector2.Distance(transform.position, target.transform.position);
            DamageContext context = stats
                .CreateDirectDamageContext(gameObject, DamageSourceType.PlayerProjectile, damageScale)
                .WithImpactDistance(impactDistance);
            float healthBeforeHit = target.CurrentHealth + target.CurrentShield;
            target.TakeDamage(context);
            if (target.IsDead)
            {
                return;
            }

            ProjectileHitEffects hitEffects = stats.BasicAttackHitEffects;
            if (healthBeforeHit - (target.CurrentHealth + target.CurrentShield) > 0f)
            {
                EnemyStatusEffects.TryApplyPoison(target, gameObject, stats.AttackDamage, hitEffects.Poison,
                    Random.value, hitEffects.StatusTickDamageMultiplier);
                EnemyStatusEffects.TryApplyBurn(target, gameObject, stats.AttackDamage, hitEffects.Burn,
                    Random.value, hitEffects.StatusTickDamageMultiplier);
                EnemyStatusEffects.TryApplyShock(target, hitEffects.Shock, Random.value);
            }

            // The stream uses the reference shot speed: shot speed items do not change it.
            BasicAttackKnockback.TryApply(target, direction * BasicAttackKnockback.ReferenceProjectileSpeed,
                hitEffects.KnockbackBonus, knockbackShare);
        }

        // 다야의 다이아몬드 커터: four short streams from the enemy that was hit. They do not hit that enemy and
        // do not split or bounce again.
        private void SplitFrom(Health source)
        {
            Vector2 center = source.transform.position;
            float damageScale = stats.ProjectileSplitSettings.DamageMultiplier;
            float halfWidth = BeamWidth * 0.5f;
            chainTargets.Clear();
            chainTargets.Add(source);
            foreach (Vector2 direction in SplitDirections)
            {
                float length = ResolveLength(center, direction, SplitLength);
                ShowFlash(center, center + direction * length, halfWidth * 2f);
                foreach (RaycastHit2D hit in Physics2D.CircleCastAll(center, halfWidth, direction, length,
                             EnemyLayerMask))
                {
                    Health target = hit.collider != null ? hit.collider.GetComponentInParent<Health>() : null;
                    if (IsEnemy(target) && chainTargets.Add(target))
                    {
                        ApplyHit(target, direction, damageScale);
                    }
                }
            }
        }

        // 칸타의 팽이: the hit jumps to the nearest enemy not yet in this chain, once per bounce.
        private void BounceFrom(Health source)
        {
            chainTargets.Clear();
            chainTargets.Add(source);
            Health current = source;
            for (int bounce = 0; bounce < stats.ProjectileBounce.BounceCount; bounce++)
            {
                Health next = FindNearestEnemy(current.transform.position, BounceRange, chainTargets);
                if (next == null)
                {
                    return;
                }

                chainTargets.Add(next);
                Vector2 offset = next.transform.position - current.transform.position;
                ShowFlash(current.transform.position, next.transform.position, BeamWidth);
                ApplyHit(next, offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.right, 1f);
                current = next;
            }
        }

        private Health FindNearestEnemy(Vector2 center, float range, HashSet<Health> excluded)
        {
            Health nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, range, EnemyLayerMask))
            {
                Health candidate = hit.GetComponentInParent<Health>();
                if (!IsEnemy(candidate) || excluded.Contains(candidate))
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

        private bool IsEnemy(Health target)
        {
            return target != null && target != health && !target.IsDead && target.gameObject.activeInHierarchy &&
                   target.GetComponent<PlayerCombatEvents>() == null;
        }

        private void StopFiring()
        {
            IsFiring = false;
            HideBeams();
        }

        private void HideBeams()
        {
            foreach (SpriteRenderer sprite in artworkBeams) if (sprite != null) sprite.enabled = false;
            foreach (LineRenderer visual in beamVisuals)
            {
                if (visual != null) visual.enabled = false;
            }
        }

        private void EnsureReferences()
        {
            if (health == null) health = GetComponent<Health>();
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (actionState == null) actionState = GetComponent<PlayerActionState>();
            if (projectileAttack == null) projectileAttack = GetComponent<PlayerProjectileAttack>();
        }

        private readonly List<SpriteRenderer> artworkBeams = new();

        private void ShowBeam(int beam, Vector2 start, Vector2 end, float lineWidth)
        {
            if (!Application.isPlaying) return;
            if (TrickalFanGame.Frontend.FairyKingdomArtwork.Catalog != null)
            {
                while (artworkBeams.Count <= beam)
                    artworkBeams.Add(CreateArtworkBeam(transform));
                SetArtworkBeam(artworkBeams[beam], start, end, lineWidth);
                return;
            }
            while (beamVisuals.Count <= beam)
            {
                beamVisuals.Add(CreateLine($"Water Stream {beamVisuals.Count}", transform));
            }

            SetLine(beamVisuals[beam], start, end, lineWidth);
        }

        private void ShowFlash(Vector2 start, Vector2 end, float lineWidth)
        {
            if (!Application.isPlaying) return;
            if (TrickalFanGame.Frontend.FairyKingdomArtwork.Catalog != null)
            {
                SpriteRenderer flashArtwork = CreateArtworkBeam(null);
                SetArtworkBeam(flashArtwork, start, end, lineWidth);
                Destroy(flashArtwork.gameObject, FlashSeconds);
                return;
            }
            LineRenderer flash = CreateLine("Water Stream Flash", null);
            SetLine(flash, start, end, lineWidth);
            Destroy(flash.gameObject, FlashSeconds);
        }

        private SpriteRenderer CreateArtworkBeam(Transform parent)
        {
            GameObject owner = new("Water Stream Artwork");
            owner.transform.SetParent(parent, false);
            SpriteRenderer renderer = owner.AddComponent<SpriteRenderer>();
            renderer.sprite = TrickalFanGame.Frontend.FairyKingdomArtwork.Fit("effect-water-stream", 1f);
            SpriteRenderer body = GetComponent<SpriteRenderer>();
            if (body != null) renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = (body != null ? body.sortingOrder : 0) - 1;
            return renderer;
        }

        private static void SetArtworkBeam(SpriteRenderer renderer, Vector2 start, Vector2 end, float width)
        {
            if (renderer == null || renderer.sprite == null) return;
            Vector2 direction = end - start;
            renderer.enabled = direction.sqrMagnitude > 0.0001f;
            renderer.transform.SetPositionAndRotation((start + end) * 0.5f,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
            // Set world dimensions independently of the player's current transform scale.
            renderer.transform.localScale = Vector3.one;
            Vector3 inherited = renderer.transform.lossyScale;
            Vector2 size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(direction.magnitude / size.x / Mathf.Abs(inherited.x),
                width / size.y / Mathf.Abs(inherited.y), 1f);
        }

        private LineRenderer CreateLine(string lineName, Transform parent)
        {
            GameObject owner = new(lineName);
            owner.transform.SetParent(parent, false);
            LineRenderer line = owner.AddComponent<LineRenderer>();
            if (streamMaterial == null)
            {
                streamMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            line.sharedMaterial = streamMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startColor = StreamColor;
            line.endColor = StreamColor;
            line.numCapVertices = 4;
            SpriteRenderer body = GetComponentInChildren<SpriteRenderer>();
            if (body != null)
            {
                line.sortingLayerID = body.sortingLayerID;
                line.sortingOrder = body.sortingOrder - 1;
            }

            return line;
        }

        private static void SetLine(LineRenderer line, Vector2 start, Vector2 end, float lineWidth)
        {
            line.enabled = true;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }
}
