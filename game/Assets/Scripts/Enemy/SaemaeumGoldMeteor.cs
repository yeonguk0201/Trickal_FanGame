using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // One 새마음금고 황금 메테오: a shadow mark grows where a lump of gold will land, the lump drops in for the last part of
    // the warning, and the impact hurts and knocks back a player standing on the mark once and breaks every boss
    // obstacle it touches. Then it is gone; it leaves nothing behind.
    [DisallowMultipleComponent]
    public sealed class SaemaeumGoldMeteor : MonoBehaviour
    {
        public const string ObjectName = "Saemaeum Gold Meteor";
        // Placeholder until a drawn gold lump exists.
        public const string ArtworkId = "obstacle-gold-rock";
        public const float FallDuration = 0.4f;
        public const float FallHeight = 7f;
        public const float LumpWidth = 1.1f;

        private float shownAt;
        private float impactAt;
        private float radius;
        private EnemyDamageTier damageTier;
        private float knockbackSpeed;
        private float knockbackDuration;
        private GameObject source;
        private Transform target;
        private SpriteRenderer mark;
        private SpriteRenderer lump;

        public bool HasLanded { get; private set; }
        public bool TargetsPlayer { get; private set; }
        public float ImpactAt => impactAt;
        public float Radius => radius;
        public Vector2 Landing => transform.position;
        public SpriteRenderer Mark => mark;
        public SpriteRenderer Lump => lump;
        public int LastHitCount { get; private set; }
        public int LastBrokenObstacles { get; private set; }

        public static SaemaeumGoldMeteor Create(Vector2 landing, bool targetsPlayer, float shownAt, float impactAt,
            float radius, EnemyDamageTier damageTier, float knockbackSpeed, float knockbackDuration,
            GameObject source, Transform target, int sortingLayerId, int bodySortingOrder)
        {
            var host = new GameObject(ObjectName);
            host.transform.position = landing;
            var meteor = host.AddComponent<SaemaeumGoldMeteor>();
            meteor.TargetsPlayer = targetsPlayer;
            meteor.shownAt = shownAt;
            meteor.impactAt = Mathf.Max(shownAt, impactAt);
            meteor.radius = Mathf.Max(0.05f, radius);
            meteor.damageTier = damageTier;
            meteor.knockbackSpeed = Mathf.Max(0f, knockbackSpeed);
            meteor.knockbackDuration = Mathf.Max(0.01f, knockbackDuration);
            meteor.source = source;
            meteor.target = target;

            GameObject markObject = new("Mark");
            markObject.transform.SetParent(host.transform, false);
            markObject.transform.localScale = new Vector3(meteor.radius * 2f,
                meteor.radius * 2f * EnemyLobProjectile.MarkAspect, 1f);
            meteor.mark = markObject.AddComponent<SpriteRenderer>();
            meteor.mark.sprite = GroundShadow.SharedSprite;
            meteor.mark.sortingLayerID = sortingLayerId;
            meteor.mark.sortingOrder = SaemaeumGoldPillar.MarkSortingOrder;

            GameObject lumpObject = new("Gold Lump");
            lumpObject.transform.SetParent(host.transform, false);
            meteor.lump = lumpObject.AddComponent<SpriteRenderer>();
            meteor.lump.sprite = FairyKingdomArtwork.Fit(ArtworkId, LumpWidth);
            meteor.lump.sortingLayerID = sortingLayerId;
            // A falling lump draws over every body below it.
            meteor.lump.sortingOrder = bodySortingOrder + 2;
            meteor.Tick(shownAt);
            return meteor;
        }

        public void Tick(float now)
        {
            if (HasLanded) return;
            float warning = Mathf.Max(0.01f, impactAt - shownAt);
            float progress = Mathf.Clamp01((now - shownAt) / warning);
            if (mark != null)
            {
                mark.color = EnemyLobProjectile.MarkColor(progress);
                // The shadow tightens as the lump comes down.
                mark.transform.localScale = new Vector3(radius * 2f, radius * 2f * EnemyLobProjectile.MarkAspect, 1f) *
                                            Mathf.Lerp(0.55f, 1f, progress);
            }

            if (lump != null)
            {
                float fallStart = impactAt - Mathf.Min(FallDuration, warning);
                float fall = Mathf.Clamp01((now - fallStart) / Mathf.Max(0.01f, impactAt - fallStart));
                lump.enabled = now >= fallStart;
                // It speeds up all the way down.
                lump.transform.localPosition = Vector3.up * (FallHeight * (1f - fall * fall));
            }

            if (now >= impactAt) Land();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        private void Land()
        {
            HasLanded = true;
            LastBrokenObstacles = BossSummonedObstacle.BreakInCircle(transform.position, radius);
            HurtPlayerOnMark();
            if (lump != null && lump.sprite != null)
                LandingDustEffect.Play(transform.position, LumpWidth * 1.3f, null, lump);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void HurtPlayerOnMark()
        {
            LastHitCount = 0;
            Health health = target != null ? target.GetComponentInParent<Health>() : null;
            if (health == null || health.IsDead) return;
            Vector2 offset = EnemyLobProjectile.GroundPoint(health.transform) - (Vector2)transform.position;
            float halfHeight = radius * EnemyLobProjectile.MarkAspect;
            if (offset.x * offset.x / (radius * radius) + offset.y * offset.y / (halfHeight * halfHeight) > 1f) return;

            health.GetComponent<PlayerDeathReason>()?.SetReason("BOSS");
            health.TakeDamage(HealthUnits.CreateEnemyDamageContext(source != null ? source : gameObject,
                DamageSourceType.EnemyProjectile, damageTier));
            LastHitCount = 1;
            KnockbackReceiver receiver = health.GetComponent<KnockbackReceiver>();
            if (receiver == null || health.IsDead) return;
            Vector2 direction = (Vector2)health.transform.position - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.001f) direction = Vector2.down;
            receiver.Apply(direction, knockbackSpeed, knockbackDuration, 0f);
        }
    }
}
