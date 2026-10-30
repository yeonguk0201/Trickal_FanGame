using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public sealed class BossProjectile : MonoBehaviour
    {
        // Drawn boss shots (부스러기 cream) are shown at this world scale; the hitbox keeps BossScale.
        public const float AuthoredArtworkWorldScale = 0.75f;
        public const float AuthoredFramesPerSecond = 10f;

        private Vector2 velocity;
        private SpriteRenderer artwork;
        private Sprite[] frames = System.Array.Empty<Sprite>();
        private float launchedAt;
        private DamageContext damageContext;
        private float expiresAt;
        private bool isLaunched;

        public bool IsLaunched => isLaunched;
        public SpriteRenderer Artwork => artwork;
        public int FrameCount => frames.Length;

        public static BossProjectile Create(
            Vector2 position,
            Vector2 initialVelocity,
            GameObject source,
            EnemyDamageTier damageTier,
            Sprite sprite,
            Sprite[] authoredFrames = null)
        {
            GameObject projectile = new GameObject("Boss Projectile");
            projectile.name = "Boss Projectile";
            projectile.transform.position = position;
            bool authored = authoredFrames != null && authoredFrames.Length > 0 &&
                            System.Array.TrueForAll(authoredFrames, frame => frame != null);
            SpriteRenderer renderer;
            if (authored)
            {
                // The drawing points right with its pivot on the cream head. Rotating a child leaves the hitbox alone.
                GameObject art = new("Artwork");
                art.transform.SetParent(projectile.transform, false);
                art.transform.localScale = Vector3.one * (AuthoredArtworkWorldScale / ProjectileSizing.BossScale);
                if (initialVelocity.sqrMagnitude > 0.0001f)
                    art.transform.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(initialVelocity.y, initialVelocity.x) * Mathf.Rad2Deg);
                renderer = art.AddComponent<SpriteRenderer>();
                renderer.sprite = authoredFrames[0];
                renderer.color = Color.white;
            }
            else
            {
                renderer = projectile.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color(0.9f, 0.2f, 0.25f);
            }
            Rigidbody2D body = projectile.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            CircleCollider2D collider = projectile.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            ProjectileSizing.Apply(projectile.transform, collider, ProjectileSizing.BossScale);
            BossProjectile controller = projectile.AddComponent<BossProjectile>();
            controller.velocity = initialVelocity;
            if (authored)
            {
                controller.artwork = renderer;
                controller.frames = (Sprite[])authoredFrames.Clone();
            }

            controller.launchedAt = Time.time;
            controller.damageContext = HealthUnits.CreateEnemyDamageContext(
                source,
                DamageSourceType.EnemyProjectile,
                damageTier);
            controller.expiresAt = Time.time + 4f;
            controller.isLaunched = true;
            source?.GetComponent<BossController>()?.RegisterOwnedObject(projectile);
            return controller;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<DoorController>() != null)
            {
                StopAtBoundary();
            }
        }

        public void StopAtBoundary()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        // Loops the drawn frames; returns the shown frame index, or -1 without authored artwork.
        public int RenderAt(float elapsed)
        {
            if (artwork == null || frames.Length == 0) return -1;
            int index = Mathf.FloorToInt(Mathf.Max(0f, elapsed) * AuthoredFramesPerSecond) % frames.Length;
            artwork.sprite = frames[index];
            return index;
        }

        private void Update()
        {
            RenderAt(Time.time - launchedAt);
            transform.position += (Vector3)(velocity * Time.deltaTime);
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player == null || Vector2.Distance(transform.position, player.transform.position) > 0.45f) return;
            player.GetComponent<PlayerDeathReason>()?.SetReason("BOSS");
            player.GetComponent<Health>()?.TakeDamage(damageContext);
            Destroy(gameObject);
        }
    }
}
