using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Enemy-12 (곡사형, user decisions 2026-10-30): a thrown projectile that arcs from the thrower to where the
    // player's shadow lay when it was thrown. In the air it has no collider, so obstacles, walls and the player never
    // stop it. A red ellipse on that spot, a little larger than the player's shadow, shows for the whole flight and
    // deepens as it falls, like the boss slime lob (BossSlimeBlob). On landing it hurts every player whose shadow
    // (feet) centre is inside the ellipse once and is gone; nothing else is hit.
    // With a drawing (저혈당 요정-광기's crowbar) it spins as it flies; it leaves the thrower's hand above the ground
    // and the parabola is added on top of that start height, which falls to the ground by the landing.
    [DisallowMultipleComponent]
    public sealed class EnemyLobProjectile : MonoBehaviour
    {
        public const string ObjectName = "Enemy Lob Projectile";
        public const string MarkName = "Enemy Lob Landing Mark";
        public const string PlayerLayerName = "Player";
        public const string DeathReason = "ENEMY";
        // Under actors, over the floor, like the self-destruct circle.
        private const int MarkSortingOrder = 1;
        // In the air the lump draws over the actors it passes.
        private const int AirSortingOffset = 10;
        private const float LumpDiameter = 0.4f;
        // The longest side of a drawn projectile, and how fast it turns (degrees per second).
        public const float DrawnSize = 0.8f;
        public const float SpinSpeed = 720f;
        // The mark is an ellipse on the floor: Radius wide on each side and half as tall, about 1.5x the player's
        // shadow (0.98 x 0.35), so it shows around the shadow.
        public const float MarkAspect = 0.5f;
        // How far around the landing to look for players before the shadow test.
        private const float SearchMargin = 1.5f;

        private static readonly Color LumpColor = new(0.95f, 0.78f, 0.55f, 1f);
        // Redder than the self-destruct warning (user request, 2026-10-30).
        private static readonly Color MarkStartColor = new(1f, 0.12f, 0.08f, 0.4f);
        private static readonly Color MarkEndColor = new(0.95f, 0.02f, 0.02f, 0.75f);

        public static Color MarkColor(float progress) =>
            Color.Lerp(MarkStartColor, MarkEndColor, Mathf.Clamp01(progress));

        // Where a body stands on the floor: its drawn shadow, else the player's feet, else its root.
        public static Vector2 GroundPoint(Transform body)
        {
            if (body == null) return Vector2.zero;
            GroundShadow shadow = body.GetComponent<GroundShadow>();
            if (shadow != null && shadow.Visual != null && shadow.Visual.enabled)
                return shadow.Visual.transform.position;
            PlayerFeet feet = body.GetComponentInChildren<PlayerFeet>();
            if (feet != null && feet.Collider != null) return feet.WorldCenter;
            return body.position;
        }

        private readonly HashSet<Health> damaged = new();
        private Vector2 start;
        private DamageContext damageContext;
        private float launchTime;
        private float flightDuration;
        private float arcHeight;
        private float startHeight;
        private float spinDirection;
        private bool spins;
        private Transform body;

        public Vector2 Destination { get; private set; }
        public float Radius { get; private set; }
        public float HalfHeight => Radius * MarkAspect;
        public float LandAt => launchTime + flightDuration;
        public bool HasLanded { get; private set; }
        public bool IsCancelled { get; private set; }
        public int DamagedPlayerCount => damaged.Count;
        public DamageContext DamageContext => damageContext;
        public SpriteRenderer Lump { get; private set; }
        public SpriteRenderer Mark { get; private set; }
        public float Height => body != null ? body.localPosition.y : 0f;
        public float StartHeight => startHeight;
        public float Rotation => body != null ? body.localEulerAngles.z : 0f;

        public static EnemyLobProjectile Launch(GameObject source, Vector2 from, Vector2 to, float now,
            float duration, float height, float radius, EnemyDamageTier damageTier, Sprite sprite = null,
            float launchHeight = 0f)
        {
            GameObject host = new(ObjectName);
            // Room cleanup removes it with the room, like the straight enemy projectile.
            if (source != null && source.transform.parent != null) host.transform.SetParent(source.transform.parent);
            host.transform.position = from;
            EnemyLobProjectile lob = host.AddComponent<EnemyLobProjectile>();
            lob.start = from;
            lob.Destination = to;
            lob.launchTime = now;
            lob.flightDuration = Mathf.Max(0.05f, duration);
            lob.arcHeight = Mathf.Max(0f, height);
            lob.Radius = Mathf.Max(0.05f, radius);
            lob.startHeight = Mathf.Max(0f, launchHeight);
            // Thrown to the right it turns clockwise, to the left counter-clockwise.
            lob.spinDirection = to.x >= from.x ? -1f : 1f;
            // The floor's damage is fixed when thrown, so it still lands right after the thrower dies.
            lob.damageContext = HealthUnits.CreateEnemyDamageContext(source, DamageSourceType.EnemyProjectile,
                damageTier);

            SpriteRenderer sourceRenderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
            int sortingLayer = sourceRenderer != null ? sourceRenderer.sortingLayerID : 0;
            int sortingOrder = sourceRenderer != null ? sourceRenderer.sortingOrder : 0;

            // The lump is drawn on a child so its height above the ground moves only the picture.
            GameObject lump = new("Lump");
            lump.transform.SetParent(host.transform, false);
            lob.Lump = lump.AddComponent<SpriteRenderer>();
            if (sprite != null)
            {
                Vector2 size = sprite.bounds.size;
                lump.transform.localScale = Vector3.one * (DrawnSize / Mathf.Max(0.0001f, Mathf.Max(size.x, size.y)));
                lob.Lump.sprite = sprite;
                lob.Lump.color = Color.white;
                lob.spins = true;
            }
            else
            {
                lump.transform.localScale = Vector3.one * LumpDiameter;
                lob.Lump.sprite = GroundShadow.SharedSprite;
                lob.Lump.color = LumpColor;
            }
            lob.Lump.sortingLayerID = sortingLayer;
            lob.Lump.sortingOrder = sortingOrder + AirSortingOffset;
            lob.body = lump.transform;

            // Not a child: the mark stays on the landing spot while the lump travels.
            GameObject mark = new(MarkName);
            mark.transform.SetParent(host.transform.parent, false);
            mark.transform.position = to;
            // The shared mask is a unit square, so the scale is the size.
            mark.transform.localScale = new Vector3(lob.Radius * 2f, lob.HalfHeight * 2f, 1f);
            lob.Mark = mark.AddComponent<SpriteRenderer>();
            lob.Mark.sprite = GroundShadow.SharedSprite;
            lob.Mark.color = MarkColor(0f);
            lob.Mark.sortingOrder = MarkSortingOrder;

            lob.Tick(now);
            return lob;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        public void Tick(float now)
        {
            if (HasLanded || IsCancelled) return;
            float flight = Mathf.Clamp01((now - launchTime) / flightDuration);
            // A lob: straight along the ground with a parabola above it.
            transform.position = Vector2.Lerp(start, Destination, flight);
            if (body != null)
            {
                body.localPosition = Vector3.up * (startHeight * (1f - flight) + arcHeight * 4f * flight * (1f - flight));
                if (spins)
                    body.localRotation = Quaternion.Euler(0f, 0f, spinDirection * SpinSpeed * (now - launchTime));
            }
            if (Mark != null) Mark.color = MarkColor(flight);
            if (flight < 1f) return;
            Land();
        }

        // Room transitions remove thrown lumps without landing them.
        public void Cancel()
        {
            if (HasLanded || IsCancelled) return;
            IsCancelled = true;
            Release();
        }

        private void Land()
        {
            HasLanded = true;
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(Destination, Radius + SearchMargin,
                         LayerMask.GetMask(PlayerLayerName)))
            {
                Health player = hit != null ? hit.GetComponentInParent<Health>() : null;
                if (player == null || player.IsDead || player.IsInvulnerable || damaged.Contains(player) ||
                    !Covers(GroundPoint(player.transform)))
                    continue;
                damaged.Add(player);
                player.GetComponent<PlayerDeathReason>()?.SetReason(DeathReason);
                player.TakeDamage(damageContext);
            }

            Release();
        }

        public bool Covers(Vector2 groundPoint)
        {
            Vector2 offset = groundPoint - Destination;
            float x = offset.x / Radius, y = offset.y / HalfHeight;
            return x * x + y * y <= 1f;
        }

        private void OnDestroy()
        {
            if (Mark != null) CombatEffectArtwork.Release(Mark.gameObject);
        }

        private void Release()
        {
            if (Mark != null) CombatEffectArtwork.Release(Mark.gameObject);
            Mark = null;
            CombatEffectArtwork.Release(gameObject);
        }
    }
}
