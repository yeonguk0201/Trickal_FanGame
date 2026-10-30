using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Variant-1 (docs/28 §8): the purple volley a 죽으면 4방향 탄 champion leaves where it died. A short purple flash
    // warns first, then plain enemy shots fly up, down, left and right (8 ways for 저혈당 요정-활발) at the light
    // tier of the champion's floor. The volley is its own object carrying the floor, because the dead enemy is
    // destroyed at once. Room transitions remove a volley that has not fired yet.
    [DisallowMultipleComponent]
    public sealed class EnemyChampionVolley : MonoBehaviour
    {
        public const string ObjectName = "Champion Death Volley";
        // Implementation defaults (docs/28 §8.6).
        public const float TelegraphDuration = 0.3f;
        public const float ProjectileSpeed = 5f;
        public const float ProjectileLifetime = 1.2f;
        public const EnemyDamageTier DamageTier = EnemyDamageTier.Light;
        public const float FlashDiameter = 1f;
        // Under actors, over the floor (as the death blast telegraph).
        private const int FlashSortingOrder = 1;
        // The object stays until its shots expire, so their damage source stays a living object.
        private const float LingerAfterFire = ProjectileLifetime + 0.1f;

        public static readonly Color ShotColor = new(0.72f, 0.36f, 1f, 1f);
        private static readonly Color FlashColor = new(0.72f, 0.36f, 1f, 0.55f);
        private static readonly List<EnemyChampionVolley> live = new();

        private Vector2[] directions;
        private SpriteRenderer flash;
        private float destroyAt;

        public static IReadOnlyList<EnemyChampionVolley> Live
        {
            get
            {
                live.RemoveAll(item => item == null);
                return live;
            }
        }

        public float FireAt { get; private set; }
        public bool HasFired { get; private set; }
        public bool HasEnded { get; private set; }
        public int DirectionCount => directions?.Length ?? 0;
        public IReadOnlyList<EnemyProjectile> Shots { get; private set; } = System.Array.Empty<EnemyProjectile>();

        public static EnemyChampionVolley Spawn(Vector3 position, Transform parent, int floorNumber,
            IReadOnlyList<Vector2> configuredDirections, float now)
        {
            GameObject host = new(ObjectName);
            host.transform.SetParent(parent, false);
            host.transform.position = position;
            // The projectiles read their damage floor from their source.
            EnemyFloorLevel.Apply(host, Mathf.Max(1, floorNumber));
            EnemyChampionVolley volley = host.AddComponent<EnemyChampionVolley>();
            volley.directions = (configuredDirections ?? EnemyChampionDeath.FourWays).ToArray();
            volley.FireAt = now + TelegraphDuration;

            GameObject ring = new("Flash");
            ring.transform.SetParent(host.transform, false);
            volley.flash = ring.AddComponent<SpriteRenderer>();
            volley.flash.sprite = GroundShadow.SharedSprite;
            volley.flash.sortingOrder = FlashSortingOrder;
            volley.ApplyFlash(now);
            if (!live.Contains(volley)) live.Add(volley);
            return volley;
        }

        public static void ClearAll()
        {
            foreach (EnemyChampionVolley volley in Live.ToArray()) volley.End();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        public void Tick(float now)
        {
            if (HasEnded) return;
            if (!HasFired)
            {
                if (now < FireAt)
                {
                    ApplyFlash(now);
                    return;
                }

                Fire();
                destroyAt = now + LingerAfterFire;
                return;
            }

            if (now >= destroyAt) End();
        }

        public bool Fire()
        {
            if (HasFired) return false;
            HasFired = true;
            if (flash != null) flash.enabled = false;
            EnemyProjectile[] shots = new EnemyProjectile[directions.Length];
            for (int index = 0; index < directions.Length; index++)
            {
                shots[index] = EnemyProjectile.Create(transform.position, directions[index], gameObject, DamageTier,
                    ProjectileSpeed, ProjectileLifetime, GroundShadow.SharedSprite);
                // EnemyProjectile parents itself to the source's parent: the room, not this short-lived object.
                if (shots[index].TryGetComponent(out SpriteRenderer renderer)) renderer.color = ShotColor;
            }

            Shots = shots;
            return true;
        }

        private void ApplyFlash(float now)
        {
            if (flash == null) return;
            float progress = TelegraphDuration > 0f ? Mathf.Clamp01((now - (FireAt - TelegraphDuration)) /
                                                                    TelegraphDuration) : 1f;
            flash.transform.localScale = Vector3.one * (FlashDiameter * Mathf.Lerp(0.4f, 1f, progress));
            Color color = FlashColor;
            color.a *= Mathf.Lerp(0.4f, 1f, progress);
            flash.color = color;
        }

        public void End()
        {
            if (HasEnded) return;
            HasEnded = true;
            live.Remove(this);
            CombatEffectArtwork.Release(gameObject);
        }

        private void OnDestroy() => live.Remove(this);
    }
}
