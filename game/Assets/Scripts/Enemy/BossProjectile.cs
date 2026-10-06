using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public sealed class BossProjectile : MonoBehaviour
    {
        private Vector2 velocity;
        private DamageContext damageContext;
        private float expiresAt;
        private bool isLaunched;

        public bool IsLaunched => isLaunched;

        public static BossProjectile Create(
            Vector2 position,
            Vector2 initialVelocity,
            GameObject source,
            EnemyDamageTier damageTier,
            Sprite sprite)
        {
            GameObject projectile = new GameObject("Boss Projectile");
            projectile.name = "Boss Projectile";
            projectile.transform.position = position;
            SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.9f, 0.2f, 0.25f);
            Rigidbody2D body = projectile.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            CircleCollider2D collider = projectile.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            ProjectileSizing.Apply(projectile.transform, collider, ProjectileSizing.BossScale);
            BossProjectile controller = projectile.AddComponent<BossProjectile>();
            controller.velocity = initialVelocity;
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

        private void Update()
        {
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
