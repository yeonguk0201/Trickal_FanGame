using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Variant-1 (docs/28 §8): the poison puddle a 죽으면 장판 champion leaves where it died, drawn with the 독 장판
    // trap art. It spreads for a moment without hurting, then hurts the player whose feet stand in it (light tier on
    // the champion's floor, at most once per second), and fades out. It has no collider and no Health, so bodies, shots and room progress ignore it.
    // A flying player passes over it. Room transitions remove it.
    [DisallowMultipleComponent]
    public sealed class EnemyChampionPuddle : MonoBehaviour
    {
        public const string ObjectName = "Champion Death Puddle";
        public const string PlayerLayerName = "Player";
        public const string DeathReason = "ENEMY";
        public const string ArtworkId = "trap-damage-puddle";
        // Implementation defaults (docs/28 §8.6). The art is drawn this wide (its height follows the art, about
        // 1.03); the harmful ellipse sits just inside the drawn liquid, so the glowing rim never hurts.
        public const float ArtWidth = 2.8f;
        public const float HalfWidth = 1.3f;
        public const float HalfHeight = 0.48f;
        public const float ArmDuration = 0.5f;
        public const float Duration = 7f;
        public const float FadeDuration = 0.6f;
        public const float HitInterval = 1f;
        public const EnemyDamageTier DamageTier = EnemyDamageTier.Light;
        // Over the room floor and scorch marks, under ground shadows and every body (as the boss slime puddle).
        public const int SortingOrder = BossSlimePuddle.SortingOrder;

        private static readonly List<EnemyChampionPuddle> live = new();
        // Faint while spreading (harmless), full once it hurts.
        private static readonly Color ArmingColor = new(1f, 1f, 1f, 0.4f);
        private static readonly Color ActiveColor = Color.white;
        // Without the art (a build missing the catalog) a plain ellipse in the art's colour.
        private static readonly Color FallbackTint = new(0.73f, 0.36f, 0.72f, 1f);

        private Vector2 baseSize = Vector2.one;
        private Color tint = Color.white;

        private float nextHitTime;

        public static IReadOnlyList<EnemyChampionPuddle> Live
        {
            get
            {
                live.RemoveAll(item => item == null);
                return live;
            }
        }

        public float StartTime { get; private set; }
        public float ArmedAt => StartTime + ArmDuration;
        public float FadeAt => StartTime + Duration - FadeDuration;
        public float EndTime => StartTime + Duration;
        public int DamageUnits { get; private set; }
        public int HitCount { get; private set; }
        public bool HasEnded { get; private set; }
        public SpriteRenderer Visual { get; private set; }

        public bool IsHarmful(float now) => !HasEnded && now >= ArmedAt && now < FadeAt;

        public static EnemyChampionPuddle Spawn(Vector3 position, Transform parent, int floorNumber, float now)
        {
            GameObject host = new(ObjectName);
            host.transform.SetParent(parent, false);
            host.transform.position = position;
            EnemyChampionPuddle puddle = host.AddComponent<EnemyChampionPuddle>();
            puddle.StartTime = now;
            puddle.nextHitTime = now + ArmDuration;
            puddle.DamageUnits = HealthUnits.GetEnemyDamageUnits(DamageTier, Mathf.Max(1, floorNumber));

            GameObject mark = new("Puddle");
            mark.transform.SetParent(host.transform, false);
            puddle.Visual = mark.AddComponent<SpriteRenderer>();
            Sprite art = FairyKingdomArtwork.Fit(ArtworkId, ArtWidth);
            if (art != null)
            {
                // Fitted to ArtWidth already: scale 1 draws it at full size.
                puddle.Visual.sprite = art;
            }
            else
            {
                puddle.Visual.sprite = GroundShadow.SharedSprite;
                puddle.baseSize = new Vector2(HalfWidth * 2f, HalfHeight * 2f);
                puddle.tint = FallbackTint;
            }

            puddle.Visual.sortingOrder = SortingOrder;
            puddle.ApplyVisual(now);
            if (!live.Contains(puddle)) live.Add(puddle);
            return puddle;
        }

        public static void ClearAll()
        {
            foreach (EnemyChampionPuddle puddle in Live.ToArray()) puddle.End();
        }

        // Feet on the floor ellipse.
        public bool Covers(Vector2 groundPoint)
        {
            Vector2 offset = groundPoint - (Vector2)transform.position;
            float x = offset.x / HalfWidth, y = offset.y / HalfHeight;
            return x * x + y * y <= 1f;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        public void Tick(float now)
        {
            if (HasEnded) return;
            if (now >= EndTime)
            {
                End();
                return;
            }

            ApplyVisual(now);
            if (!IsHarmful(now) || now < nextHitTime) return;
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, HalfWidth + 0.5f,
                         LayerMask.GetMask(PlayerLayerName)))
            {
                PlayerMovement player = hit != null ? hit.GetComponentInParent<PlayerMovement>() : null;
                Health target = player != null ? player.GetComponent<Health>() : null;
                if (target == null || target.IsDead || target.IsInvulnerable || IsFlying(player) ||
                    !Covers(player.FeetPosition))
                    continue;
                target.GetComponent<PlayerDeathReason>()?.SetReason(DeathReason);
                target.TakeDamage(new DamageContext(gameObject, DamageSourceType.EnemyContact, DamageUnits));
                HitCount++;
                nextHitTime = now + HitInterval;
                return;
            }
        }

        private void ApplyVisual(float now)
        {
            if (Visual == null) return;
            // Spreads from half size while arming, then holds and fades at the end.
            float spread = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01((now - StartTime) / ArmDuration));
            Visual.transform.localScale = new Vector3(baseSize.x * spread, baseSize.y * spread, 1f);
            Color color = (now < ArmedAt ? ArmingColor : ActiveColor) * tint;
            if (now >= FadeAt) color.a *= Mathf.Clamp01((EndTime - now) / FadeDuration);
            Visual.color = color;
        }

        private static bool IsFlying(PlayerMovement body) =>
            body.TryGetComponent(out PlayerFlight flight) && flight.IsFlying;

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
