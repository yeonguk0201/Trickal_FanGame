using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Enemy-7: the blast a dead enemy leaves (EnemyDeathExplosion). For the telegraph time the dead enemy's picture
    // stays where it stopped and turns red inside a marked circle, then it bursts and hurts every player inside
    // once, a fixed amount on every floor. It has no Health and no collider, so enemies, obstacles and room
    // progress ignore it.
    // Enemy-10 (자폭형): the blast can also hurt other enemies (not player damage, so no kill credit) and break
    // obstacles, webs, secret walls and chests the way a bomb does.
    [DisallowMultipleComponent]
    public sealed class EnemyDeathBlast : MonoBehaviour
    {
        public const string PlayerLayerName = "Player";
        public const string EnemyLayerName = "Enemy";
        public const string DeathReason = "ENEMY";
        // The dead enemy's own burst, shown with the explosion instead of at the moment of death.
        public const string BodyBurstArtwork = "effect-crumb-death";
        public const float BodyBurstDuration = 0.24f;
        // How long the blast object stays after bursting. The pink burst around the body art
        // (CrumbDeathBlastEffect) is its own object and runs its own length.
        public const float ExplosionVisualDuration = 0.4f;
        // Under actors, over the floor.
        private const int TelegraphSortingOrder = 1;

        public static readonly Color BodyEndColor = new(1f, 0.25f, 0.2f, 1f);
        private static readonly Color TelegraphStartColor = new(1f, 0.35f, 0.15f, 0.25f);
        private static readonly Color TelegraphEndColor = new(1f, 0.2f, 0.1f, 0.6f);

        private readonly HashSet<Health> damaged = new();
        private readonly HashSet<Health> damagedEnemies = new();
        private GameObject source;
        private SpriteRenderer telegraph;
        private float startTime;
        private float destroyAt;

        public float Radius { get; private set; }
        public int DamageUnits { get; private set; }
        public float ExplodeAt { get; private set; }
        public bool HasExploded { get; private set; }
        public bool HasEnded { get; private set; }
        public int DamagedPlayerCount => damaged.Count;
        public bool HitsEnemiesAndObstacles { get; private set; }
        public float EnemyDamage { get; private set; }
        public int DamagedEnemyCount => damagedEnemies.Count;
        public int BrokenObstacleCount { get; private set; }
        public SpriteRenderer Telegraph => telegraph;
        // The stopped copy of the dead enemy's picture. Null when the enemy had none.
        public SpriteRenderer Body { get; private set; }

        public static Color TelegraphColor(float progress) =>
            Color.Lerp(TelegraphStartColor, TelegraphEndColor, Mathf.Clamp01(progress));

        public static Color BodyColor(float progress) => Color.Lerp(Color.white, BodyEndColor, Mathf.Clamp01(progress));

        public static EnemyDeathBlast Spawn(Vector3 position, Transform parent, GameObject source,
            SpriteRenderer sourceVisual, float telegraphDuration, float radius, int damageUnits, float currentTime)
        {
            GameObject owner = new("Enemy Death Blast");
            owner.transform.SetParent(parent, false);
            owner.transform.position = position;
            EnemyDeathBlast blast = owner.AddComponent<EnemyDeathBlast>();
            blast.source = source;
            blast.Radius = Mathf.Max(0.01f, radius);
            blast.DamageUnits = Mathf.Max(1, damageUnits);
            blast.startTime = currentTime;
            blast.ExplodeAt = currentTime + Mathf.Max(0f, telegraphDuration);

            GameObject ring = new("Telegraph");
            ring.transform.SetParent(owner.transform, false);
            // The shared mask is a unit square, so the scale is the diameter.
            ring.transform.localScale = Vector3.one * (blast.Radius * 2f);
            blast.telegraph = ring.AddComponent<SpriteRenderer>();
            blast.telegraph.sprite = GroundShadow.SharedSprite;
            blast.telegraph.color = TelegraphStartColor;
            blast.telegraph.sortingOrder = TelegraphSortingOrder;

            if (sourceVisual != null && sourceVisual.sprite != null)
            {
                GameObject body = new("Body");
                body.transform.SetParent(owner.transform, false);
                body.transform.SetPositionAndRotation(sourceVisual.transform.position,
                    sourceVisual.transform.rotation);
                body.transform.localScale = sourceVisual.transform.lossyScale;
                blast.Body = body.AddComponent<SpriteRenderer>();
                blast.Body.sprite = sourceVisual.sprite;
                blast.Body.flipX = sourceVisual.flipX;
                blast.Body.flipY = sourceVisual.flipY;
                blast.Body.sharedMaterial = sourceVisual.sharedMaterial;
                blast.Body.sortingLayerID = sourceVisual.sortingLayerID;
                blast.Body.sortingOrder = sourceVisual.sortingOrder;
                blast.Body.color = Color.white;
            }

            return blast;
        }

        // Enemy-10: a telegraph that began before the enemy died keeps its colour progress from that start.
        public void ContinueTelegraphFrom(float telegraphStartTime)
        {
            if (HasExploded) return;
            startTime = Mathf.Min(startTime, telegraphStartTime);
            ApplyTelegraphProgress(Time.time);
        }

        public void ConfigureEnemyAndObstacleHits(float configuredEnemyDamage)
        {
            HitsEnemiesAndObstacles = true;
            EnemyDamage = Mathf.Max(0f, configuredEnemyDamage);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        public void Tick(float currentTime)
        {
            if (HasEnded) return;
            if (!HasExploded)
            {
                if (currentTime < ExplodeAt)
                {
                    ApplyTelegraphProgress(currentTime);
                    return;
                }

                Explode();
                destroyAt = currentTime + ExplosionVisualDuration;
                return;
            }

            if (currentTime < destroyAt) return;
            HasEnded = true;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void ApplyTelegraphProgress(float currentTime)
        {
            float progress = ExplodeAt > startTime ? Mathf.InverseLerp(startTime, ExplodeAt, currentTime) : 1f;
            if (telegraph != null) telegraph.color = TelegraphColor(progress);
            if (Body != null) Body.color = BodyColor(progress);
        }

        public bool Explode()
        {
            if (HasExploded) return false;
            HasExploded = true;
            Vector2 center = transform.position;
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, Radius, LayerMask.GetMask(PlayerLayerName)))
            {
                Health player = hit != null ? hit.GetComponentInParent<Health>() : null;
                if (player == null || player.IsDead || player.IsInvulnerable || !damaged.Add(player)) continue;
                player.GetComponent<PlayerDeathReason>()?.SetReason(DeathReason);
                player.TakeDamage(new DamageContext(source != null ? source : gameObject,
                    DamageSourceType.EnemyContact, DamageUnits));
            }

            if (HitsEnemiesAndObstacles) HitEnemiesAndObstacles(center);

            // Sorted from the picture before it hides, so the burst sits around the body art.
            CrumbDeathBlastEffect.Play(center, Radius, transform.parent, Body != null ? Body : telegraph);
            if (telegraph != null) telegraph.enabled = false;
            if (Body != null)
            {
                FairyKingdomSpriteEffect.Play(BodyBurstArtwork, Body.transform.position,
                    Mathf.Max(0.3f, Body.bounds.size.x * 1.2f), BodyBurstDuration, Body);
                Body.enabled = false;
            }

            return true;
        }

        // The same reach as the player damage. Environment damage, so a kill here is never credited to the player.
        private void HitEnemiesAndObstacles(Vector2 center)
        {
            if (EnemyDamage > 0f)
            {
                foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, Radius,
                             LayerMask.GetMask(EnemyLayerName)))
                {
                    Health enemy = hit != null ? hit.GetComponentInParent<Health>() : null;
                    if (enemy == null || enemy.IsDead || (source != null && enemy.gameObject == source) ||
                        !damagedEnemies.Add(enemy))
                        continue;
                    enemy.TakeDamage(new DamageContext(gameObject, DamageSourceType.Environment, EnemyDamage));
                }
            }

            BrokenObstacleCount = DestructibleObstacle.DestroyByBombInCircle(center, Radius);
            RoomTrap.RemoveWebsByBombInCircle(center, Radius);
            SecretPassageWall.OpenByBombInCircle(center, Radius);
            TreasureChest.OpenByBombInCircle(center, Radius);
        }
    }
}
