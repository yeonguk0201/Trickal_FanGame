using System;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Variant-1 (docs/28 §8): a champion's death property. Whatever killed it, the property leaves its own object
    // where it fell (a puddle, a volley or the 쫄따구 blast), so the dead enemy is released from its room at once and
    // never holds a clear back. Subscribed from EnemyVariant.Apply, which runs before the room registers the enemy.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyChampionDeath : MonoBehaviour
    {
        // 저혈당 요정-활발 already fires four ways, so its champion volley adds the diagonals.
        public static readonly Vector2[] FourWays = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        public static readonly Vector2[] EightWays =
        {
            Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(1f, 1f).normalized, new Vector2(1f, -1f).normalized,
            new Vector2(-1f, 1f).normalized, new Vector2(-1f, -1f).normalized,
        };

        [SerializeField] private EnemyVariantKind kind;

        private Health health;
        private bool triggered;

        public EnemyVariantKind Kind => kind;
        public bool HasTriggered => triggered;
        public GameObject LastEffect { get; private set; }
        public event Action<GameObject> EffectSpawned;

        public static Vector2[] VolleyDirectionsFor(GameObject enemy) =>
            enemy != null && enemy.GetComponent<FourWayShooterController>() != null ? EightWays : FourWays;

        public void Initialize(EnemyVariantKind configuredKind)
        {
            kind = configuredKind;
            if (health == null) health = GetComponent<Health>();
            health.Died -= OnDied;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (triggered) return;
            triggered = true;
            float now = Time.time;
            int floor = EnemyFloorLevel.Resolve(gameObject);
            Transform parent = transform.parent;
            Vector3 position = transform.position;
            LastEffect = kind switch
            {
                EnemyVariantKind.ChampionPuddle => EnemyChampionPuddle.Spawn(position, parent, floor, now)?.gameObject,
                EnemyVariantKind.ChampionShots => EnemyChampionVolley.Spawn(position, parent, floor,
                    VolleyDirectionsFor(gameObject), now)?.gameObject,
                EnemyVariantKind.ChampionBlast => SpawnBlast(now),
                _ => null,
            };
            if (LastEffect != null) EffectSpawned?.Invoke(LastEffect);
        }

        // The 쫄따구 blast as it is (EnemyDeathExplosion defaults): 0.6 s telegraph, radius 1.2, one heart on every
        // floor, player only.
        private GameObject SpawnBlast(float now)
        {
            TrickalFanGame.Frontend.FairyKingdomArtworkView artwork =
                GetComponent<TrickalFanGame.Frontend.FairyKingdomArtworkView>();
            SpriteRenderer visual = artwork != null && artwork.Visual != null
                ? artwork.Visual
                : GetComponentInChildren<SpriteRenderer>();
            EnemyDeathBlast blast = EnemyDeathBlast.Spawn(transform.position, transform.parent, gameObject, visual,
                EnemyDeathExplosion.DefaultTelegraphDuration, EnemyDeathExplosion.DefaultRadius,
                EnemyDeathExplosion.DefaultDamageUnits, now);
            return blast != null ? blast.gameObject : null;
        }
    }
}
