using System.Collections.Generic;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // 새마음금고 황금 메테오 (phase two): after the boss's short cast, gold lumps keep falling for a while on their own,
    // so the boss can use its other patterns meanwhile. The first few come quickly one after another, then one
    // lands at a steady interval. Every few lumps one aims at where the player stands when its shadow appears, and
    // (2026-11-21) the lump right after it leads the player: it lands where their current movement carries them by
    // the impact. The rest land at random in the arena. The rain stops when its time is up or its boss is gone.
    [DisallowMultipleComponent]
    public sealed class SaemaeumGoldRain : MonoBehaviour
    {
        public const string ObjectName = "Saemaeum Gold Rain";

        public readonly struct Settings
        {
            public Settings(float duration, int openingCount, float openingInterval, float interval,
                int targetEvery, float warning, float radius, EnemyDamageTier damageTier, float knockbackSpeed,
                float knockbackDuration, bool leadsPlayer = false)
            {
                LeadsPlayer = leadsPlayer;
                Duration = Mathf.Max(0.01f, duration);
                OpeningCount = Mathf.Max(0, openingCount);
                OpeningInterval = Mathf.Max(0.01f, openingInterval);
                Interval = Mathf.Max(0.01f, interval);
                TargetEvery = Mathf.Max(1, targetEvery);
                Warning = Mathf.Max(0.01f, warning);
                Radius = Mathf.Max(0.05f, radius);
                DamageTier = damageTier;
                KnockbackSpeed = Mathf.Max(0f, knockbackSpeed);
                KnockbackDuration = Mathf.Max(0.01f, knockbackDuration);
            }

            public float Duration { get; }
            public int OpeningCount { get; }
            public float OpeningInterval { get; }
            public float Interval { get; }
            public int TargetEvery { get; }
            public float Warning { get; }
            public float Radius { get; }
            public EnemyDamageTier DamageTier { get; }
            public float KnockbackSpeed { get; }
            public float KnockbackDuration { get; }
            public bool LeadsPlayer { get; }
        }

        private readonly List<SaemaeumGoldMeteor> meteors = new();
        private Settings settings;
        private BossController boss;
        private Health bossHealth;
        private Transform target;
        private Rect area;
        private uint randomState;
        private int sortingLayerId;
        private int bodySortingOrder;
        private float startedAt;
        private float nextShadowAt;

        public int SpawnedCount { get; private set; }
        public float StartedAt => startedAt;
        // No new shadow appears at or after this time.
        public float SpawnEndsAt => startedAt + settings.Duration;
        public bool IsFinished { get; private set; }
        public IReadOnlyList<SaemaeumGoldMeteor> Meteors => meteors;

        public static SaemaeumGoldRain Begin(BossController boss, Transform target, Rect area, float now, uint seed,
            Settings settings, int sortingLayerId, int bodySortingOrder)
        {
            var host = new GameObject(ObjectName);
            var rain = host.AddComponent<SaemaeumGoldRain>();
            rain.boss = boss;
            rain.bossHealth = boss != null ? boss.GetComponent<Health>() : null;
            rain.target = target;
            rain.area = area;
            rain.randomState = seed == 0u ? 0x9E3779B9u : seed;
            rain.settings = settings;
            rain.sortingLayerId = sortingLayerId;
            rain.bodySortingOrder = bodySortingOrder;
            rain.startedAt = now;
            rain.nextShadowAt = now;
            boss?.RegisterOwnedObject(host);
            rain.Tick(now);
            return rain;
        }

        // When the shadow of the given lump (0-based) appears, counted from the start of the rain: the opening lumps
        // are openingInterval apart, and every lump after the last opening one follows the one before by interval.
        public static float ShadowOffset(int index, int openingCount, float openingInterval, float interval)
        {
            index = Mathf.Max(0, index);
            if (openingCount <= 0) return index * interval;
            int opening = Mathf.Min(index, openingCount - 1);
            return opening * openingInterval + (index - opening) * interval;
        }

        public void Tick(float now)
        {
            if (IsFinished) return;
            if (boss == null || (bossHealth != null && bossHealth.IsDead))
            {
                Stop();
                return;
            }

            int safety = 0;
            while (now >= nextShadowAt && nextShadowAt < SpawnEndsAt && safety++ < 64)
            {
                SpawnMeteor(nextShadowAt);
                SpawnedCount++;
                nextShadowAt = startedAt + ShadowOffset(SpawnedCount, settings.OpeningCount,
                    settings.OpeningInterval, settings.Interval);
            }

            meteors.RemoveAll(item => item == null);
            if (nextShadowAt >= SpawnEndsAt && meteors.Count == 0) Stop();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        private void SpawnMeteor(float shownAt)
        {
            bool aimed = SpawnedCount % settings.TargetEvery == 0 && target != null;
            bool leads = !aimed && IsLeadSlot(SpawnedCount, settings.TargetEvery, settings.LeadsPlayer) &&
                         target != null;
            Vector2 landing = aimed ? ClampToArea(EnemyLobProjectile.GroundPoint(target))
                : leads ? ClampToArea(EnemyLobProjectile.GroundPoint(target) + TargetVelocity() * settings.Warning)
                : RandomPoint();
            SaemaeumGoldMeteor meteor = SaemaeumGoldMeteor.Create(landing, aimed, shownAt,
                shownAt + settings.Warning, settings.Radius, settings.DamageTier, settings.KnockbackSpeed,
                settings.KnockbackDuration, boss != null ? boss.gameObject : null, target, sortingLayerId,
                bodySortingOrder);
            meteor.LeadsPlayer = leads;
            meteors.Add(meteor);
            boss?.RegisterOwnedObject(meteor.gameObject);
        }

        // The lump right after each aimed one, when the cycle has room for a random lump as well.
        public static bool IsLeadSlot(int index, int targetEvery, bool leadsPlayer) =>
            leadsPlayer && targetEvery >= 2 && index % targetEvery == 1;

        private Vector2 TargetVelocity()
        {
            if (target == null) return Vector2.zero;
            Player.PlayerMovement movement = target.GetComponentInParent<Player.PlayerMovement>();
            if (movement != null) return movement.CurrentVelocity;
            Rigidbody2D body = target.GetComponentInParent<Rigidbody2D>();
            return body != null ? body.linearVelocity : Vector2.zero;
        }

        private void Stop()
        {
            IsFinished = true;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private Vector2 RandomPoint() => new(Mathf.Lerp(area.xMin, area.xMax, NextRandom01()),
            Mathf.Lerp(area.yMin, area.yMax, NextRandom01()));

        private Vector2 ClampToArea(Vector2 point) =>
            new(Mathf.Clamp(point.x, area.xMin, area.xMax), Mathf.Clamp(point.y, area.yMin, area.yMax));

        private float NextRandom01()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (randomState % 10000u) / 10000f;
        }
    }
}
