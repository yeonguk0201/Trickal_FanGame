using System;
using System.Collections.Generic;
using TrickalFanGame.Audio;
using TrickalFanGame.Combat;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Resource
{
    [DisallowMultipleComponent]
    public sealed class PlacedBomb : MonoBehaviour
    {
        private void OnEnable() => TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);

        public const float DefaultFuseDuration = 0.75f;
        // 2.5 until Obstacle-5 (2026-10-06 decision): exploding obstacles share this explosion.
        public const float DefaultExplosionRadius = 2f;
        public const float DefaultEnemyDamage = 30f;
        public const float DefaultSelfDamage = 2f;
        public const float ExplosionVisualDuration = 0.15f;

        [SerializeField, Min(0f)] private float fuseDuration = DefaultFuseDuration;
        [SerializeField, Min(0.01f)] private float explosionRadius = DefaultExplosionRadius;
        [SerializeField, Min(0f)] private float enemyDamage = DefaultEnemyDamage;
        [SerializeField, Min(0f)] private float selfDamage = DefaultSelfDamage;
        [SerializeField] private LayerMask enemyLayers;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Color armedColor = new(0.18f, 0.18f, 0.22f);
        [SerializeField] private Color explosionColor = new(1f, 0.48f, 0.08f, 0.7f);

        private GameObject owner;
        private Health ownerHealth;
        private float explodeAt;
        private float destroyAt;
        private bool configured;
        private bool hasExploded;
        private bool isHidden;
        private Vector3 armedScale;
        private BombFuseFlash fuseFlash;

        public float FuseDuration => fuseDuration;
        public float ExplosionRadius => explosionRadius;
        public float EnemyDamage => enemyDamage;
        public float SelfDamage => selfDamage;
        public LayerMask EnemyLayers => enemyLayers;
        public bool HasExploded => hasExploded;
        // A fuseless obstacle explosion waiting on its chain delay shows nothing, not a bomb, until it goes off.
        public bool IsHidden => isHidden;
        public float ExplodeAt => explodeAt;
        public event Action<PlacedBomb> Exploded;

        public void Configure(GameObject configuredOwner, Health configuredOwnerHealth, float currentTime)
        {
            owner = configuredOwner;
            ownerHealth = configuredOwnerHealth;
            explodeAt = currentTime + fuseDuration;
            configured = owner != null && ownerHealth != null;
            armedScale = transform.localScale;
            if (visual != null) visual.color = armedColor;
            StartFuseFlash(currentTime);
        }

        // Obstacle-5: an explosion nobody placed (an exploding obstacle). It is the same explosion as a placed bomb
        // and hurts every player in range the way a placed bomb hurts its owner. This one shows the bomb and fuse.
        public void ConfigureUnowned(float currentTime, float configuredFuseDuration)
        {
            owner = null;
            ownerHealth = null;
            explodeAt = currentTime + Mathf.Max(0f, configuredFuseDuration);
            configured = true;
            armedScale = transform.localScale;
            if (visual != null) visual.color = armedColor;
            StartFuseFlash(currentTime);
        }

        // Bomb-Chain: the same unowned explosion without a bomb or fuse. Without a delay it explodes in this call;
        // with one (an obstacle caught in another explosion) it stays hidden until the delay ends, so chained
        // obstacles go off one after another.
        public void ConfigureUnownedBlast(float currentTime, float configuredDelay)
        {
            owner = null;
            ownerHealth = null;
            explodeAt = currentTime + Mathf.Max(0f, configuredDelay);
            configured = true;
            armedScale = transform.localScale;
            isHidden = true;
            if (visual != null) visual.enabled = false;
            if (configuredDelay <= 0f) Explode(currentTime);
        }

        // Motion-0: the red blinking that speeds up until the explosion. Play only, like the ground shadow.
        private void StartFuseFlash(float currentTime)
        {
            if (Application.isPlaying) fuseFlash = BombFuseFlash.Attach(visual, currentTime, explodeAt - currentTime);
        }

        public void ConfigureValues(float configuredFuseDuration, float configuredExplosionRadius,
            float configuredEnemyDamage, float configuredSelfDamage, LayerMask configuredEnemyLayers,
            SpriteRenderer configuredVisual)
        {
            fuseDuration = Mathf.Max(0f, configuredFuseDuration);
            explosionRadius = Mathf.Max(0.01f, configuredExplosionRadius);
            enemyDamage = Mathf.Max(0f, configuredEnemyDamage);
            selfDamage = Mathf.Max(0f, configuredSelfDamage);
            enemyLayers = configuredEnemyLayers;
            visual = configuredVisual;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        public bool Tick(float currentTime)
        {
            if (!configured) return false;
            if (!hasExploded)
            {
                if (currentTime < explodeAt) return false;
                Explode(currentTime);
                return true;
            }

            if (currentTime >= destroyAt) Destroy(gameObject);
            return false;
        }

        private void Explode(float currentTime)
        {
            ApplyExplosion();
            destroyAt = currentTime + ExplosionVisualDuration;
        }

        public bool ApplyExplosion()
        {
            if (!configured || hasExploded) return false;
            hasExploded = true;
            // Sound-0: the player's bomb and an exploding box both end up
            // here, and the user wanted one blast sound for both.
            CombatSfx.Play(CombatSfxCue.BombExplosion);
            Vector2 center = transform.position;

            HashSet<Health> damaged = new();
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, explosionRadius, enemyLayers))
            {
                Health target = hit != null ? hit.GetComponentInParent<Health>() : null;
                if (target == null || target == ownerHealth || target.IsDead || !damaged.Add(target)) continue;
                target.TakeDamage(new DamageContext(owner != null ? owner : gameObject,
                    DamageSourceType.PlayerBomb, enemyDamage));
            }

            if (ownerHealth != null)
            {
                DamagePlayer(ownerHealth, center);
            }
            else
            {
                foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, explosionRadius,
                             LayerMask.GetMask("Player")))
                {
                    Health player = hit != null ? hit.GetComponentInParent<Health>() : null;
                    if (player != null && damaged.Add(player)) DamagePlayer(player, center);
                }
            }

            DestructibleObstacle.DestroyByBombInCircle(center, explosionRadius);
            RoomTrap.RemoveWebsByBombInCircle(center, explosionRadius);
            SecretPassageWall.OpenByBombInCircle(center, explosionRadius);
            TreasureChest.OpenByBombInCircle(center, explosionRadius);
            if (fuseFlash != null) fuseFlash.Stop();
            fuseFlash = null;
            isHidden = false;
            if (visual != null)
            {
                visual.enabled = true;
                visual.color = explosionColor;
                float armedDiameter = visual.sprite != null
                    ? visual.sprite.bounds.size.x * Mathf.Abs(armedScale.x)
                    : 1f;
                float diameterScale = explosionRadius * 2f / Mathf.Max(0.01f, armedDiameter);
                transform.localScale = armedScale * diameterScale;
            }
            BombBurstEffect.Play(center, explosionRadius, owner != null ? owner.transform : null, visual);

            Exploded?.Invoke(this);
            return true;
        }

        private void DamagePlayer(Health player, Vector2 center)
        {
            if (!player.IsDead &&
                ((Vector2)player.transform.position - center).sqrMagnitude <= explosionRadius * explosionRadius)
            {
                player.TakeDamage(new DamageContext(gameObject, DamageSourceType.PlayerBomb, selfDamage));
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.48f, 0.08f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
