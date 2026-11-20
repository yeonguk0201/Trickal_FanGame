using TrickalFanGame.Audio;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // 새마음금고 황금 기둥: an ellipse mark warns where a gold pillar will burst out of the floor, like an ambush enemy.
    // When it bursts, a player standing on the mark is hurt and knocked back once, and the pillar stays as a solid
    // BossSummonedObstacle. It blocks the boss's shots, breaks after a fixed number of player hits and is never an
    // enemy (no Health). The obstacle parts are added only when it bursts, so nothing can hit or break a pillar that
    // is still under the floor.
    [DisallowMultipleComponent]
    public sealed class SaemaeumGoldPillar : MonoBehaviour
    {
        public const string ObjectName = "Saemaeum Gold Pillar";
        public const string MarkName = "Saemaeum Gold Pillar Mark";
        public const string ObstacleId = "saemaeum-gold-pillar";
        // Retain the existing logical kind; this summon has dedicated artwork rather than the room gold rock.
        public const string ArtworkVariantId = "gold-rock";
        public const float BodyRadius = 0.42f;
        public const float VisualWidth = 1f;
        public const float RiseDuration = 0.18f;
        // Over the room floor and scorch marks, under ground shadows and every body, like the slime landing marks.
        public const int MarkSortingOrder = BossSlimePuddle.SortingOrder + 1;

        private float markShownAt;
        private float emergeAt;
        private float hitRadius;
        private EnemyDamageTier damageTier;
        private float knockbackSpeed;
        private float knockbackDuration;
        private int requiredHits;
        private GameObject source;
        private Transform target;
        private SpriteRenderer mark;
        private Transform visual;
        private SpriteRenderer drawing;
        private DestructibleObstacle destructible;
        private int sortingLayerId;

        public bool HasEmerged { get; private set; }
        public float EmergeAt => emergeAt;
        public float HitRadius => hitRadius;
        public int RequiredHits => requiredHits;
        // Rises under where the player stood when its mark appeared.
        public bool TargetsPlayer { get; private set; }
        public SpriteRenderer Mark => mark;
        public BossSummonedObstacle Obstacle { get; private set; }
        // How many players the burst hurt (0 or 1).
        public int LastHitCount { get; private set; }

        public static SaemaeumGoldPillar Create(Vector2 position, float markShownAt, float emergeAt, float hitRadius,
            EnemyDamageTier damageTier, float knockbackSpeed, float knockbackDuration, int requiredHits,
            GameObject source, Transform target, int sortingLayerId, bool targetsPlayer = false)
        {
            var host = new GameObject(ObjectName);
            host.transform.position = position;
            var pillar = host.AddComponent<SaemaeumGoldPillar>();
            pillar.markShownAt = markShownAt;
            pillar.emergeAt = Mathf.Max(markShownAt, emergeAt);
            pillar.hitRadius = Mathf.Max(0.05f, hitRadius);
            pillar.damageTier = damageTier;
            pillar.knockbackSpeed = Mathf.Max(0f, knockbackSpeed);
            pillar.knockbackDuration = Mathf.Max(0.01f, knockbackDuration);
            pillar.requiredHits = Mathf.Max(1, requiredHits);
            pillar.source = source;
            pillar.target = target;
            pillar.sortingLayerId = sortingLayerId;
            pillar.TargetsPlayer = targetsPlayer;

            GameObject markObject = new(MarkName);
            markObject.transform.SetParent(host.transform, false);
            // The shared shadow is a unit disc, so the scale is the size of the ellipse that hurts.
            markObject.transform.localScale = new Vector3(pillar.hitRadius * 2f,
                pillar.hitRadius * 2f * EnemyLobProjectile.MarkAspect, 1f);
            pillar.mark = markObject.AddComponent<SpriteRenderer>();
            pillar.mark.sprite = GroundShadow.SharedSprite;
            pillar.mark.color = EnemyLobProjectile.MarkColor(0f);
            pillar.mark.sortingLayerID = sortingLayerId;
            pillar.mark.sortingOrder = MarkSortingOrder;
            return pillar;
        }

        public void Tick(float now)
        {
            if (!HasEmerged)
            {
                float warning = Mathf.Max(0.01f, emergeAt - markShownAt);
                if (mark != null) mark.color = EnemyLobProjectile.MarkColor(Mathf.Clamp01((now - markShownAt) / warning));
                if (now >= emergeAt) Emerge();
                return;
            }

            if (visual == null) return;
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - emergeAt) / RiseDuration));
            visual.localPosition = new Vector3(0f, -0.2f + Mathf.Lerp(-0.35f, 0f, rise), 0f);
            visual.localScale = new Vector3(1f, Mathf.Lerp(0.35f, 1f, rise), 1f);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        private void LateUpdate() => RefreshArtwork();

        public void RefreshArtwork()
        {
            if (drawing == null || destructible == null || destructible.IsBroken) return;
            drawing.sprite = destructible.HitsTaken > 0
                ? SaemaeumGoldPillarArtwork.Frame(0) : SaemaeumGoldPillarArtwork.Intact;
            drawing.color = Color.white;
        }

        private void OnBroken(DestructibleObstacle _)
        {
            if (!Application.isPlaying || drawing == null) return;
            // Freeze the standing pose even when broken during its rise. Room/boss cleanup owns the remnants.
            Tick(emergeAt + RiseDuration);
            Transform room = source != null ? source.transform.parent : transform.parent;
            SaemaeumGoldPillarBreakEffect effect = SaemaeumGoldPillarBreakEffect.Create(drawing, room);
            source?.GetComponent<BossController>()?.RegisterOwnedObject(effect.gameObject);
        }

        private void OnDestroy()
        {
            if (destructible != null) destructible.Broken -= OnBroken;
        }

        private void Emerge()
        {
            HasEmerged = true;
            // Sound-2: each pillar is heard on its own as it bursts out.
            CombatSfx.Play(CombatSfxCue.VaultPillarEmerge);
            if (mark != null)
            {
                if (Application.isPlaying) Destroy(mark.gameObject);
                else DestroyImmediate(mark.gameObject);
                mark = null;
            }

            gameObject.layer = LayerMask.NameToLayer("Environment");
            GameObject visualObject = new("Visual");
            visualObject.transform.SetParent(transform, false);
            visual = visualObject.transform;
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            drawing = renderer;
            renderer.sprite = SaemaeumGoldPillarArtwork.Intact;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = 0;

            CircleCollider2D body = gameObject.AddComponent<CircleCollider2D>();
            body.radius = BodyRadius;
            DestructibleObstacle obstacle = gameObject.AddComponent<DestructibleObstacle>();
            obstacle.Configure(ObstacleId, requiredHits, null, renderer);
            obstacle.ConfigureFixedKind(ArtworkVariantId, Color.white, Color.white, false, 0, Color.white);
            destructible = obstacle;
            obstacle.Broken += OnBroken;
            Obstacle = gameObject.AddComponent<BossSummonedObstacle>();
            Obstacle.Configure(true);
            source?.GetComponent<BossController>()?.RegisterOwnedObject(gameObject);
            Tick(emergeAt);

            HurtPlayerOnMark();
            if (renderer.sprite != null)
                LandingDustEffect.Play((Vector2)transform.position + Vector2.down * 0.2f,
                    VisualWidth * 0.9f, transform, renderer);
        }

        private void HurtPlayerOnMark()
        {
            LastHitCount = 0;
            if (target == null) return;
            Health health = target.GetComponentInParent<Health>();
            if (health == null || health.IsDead) return;
            Vector2 offset = EnemyLobProjectile.GroundPoint(health.transform) - (Vector2)transform.position;
            float halfHeight = hitRadius * EnemyLobProjectile.MarkAspect;
            if (offset.x * offset.x / (hitRadius * hitRadius) + offset.y * offset.y / (halfHeight * halfHeight) > 1f)
                return;

            health.GetComponent<PlayerDeathReason>()?.SetReason("BOSS");
            health.TakeDamage(HealthUnits.CreateEnemyDamageContext(source != null ? source : gameObject,
                DamageSourceType.EnemyMelee, damageTier));
            LastHitCount = 1;
            KnockbackReceiver receiver = health.GetComponent<KnockbackReceiver>();
            if (receiver == null || health.IsDead) return;
            Vector2 direction = (Vector2)health.transform.position - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.001f) direction = Vector2.down;
            receiver.Apply(direction, knockbackSpeed, knockbackDuration, 0f);
        }
    }
}
