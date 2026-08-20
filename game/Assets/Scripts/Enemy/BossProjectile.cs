using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public sealed class BossProjectile : MonoBehaviour
    {
        private Vector2 velocity;
        private int damage;
        private float expiresAt;

        public static void Create(Vector2 position, Vector2 initialVelocity, int attackDamage, Sprite sprite)
        {
            GameObject projectile = new GameObject("Boss Projectile");
            projectile.name = "Boss Projectile";
            projectile.transform.position = position;
            projectile.transform.localScale = Vector3.one * 0.35f;
            SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.9f, 0.2f, 0.25f);
            BossProjectile controller = projectile.AddComponent<BossProjectile>();
            controller.velocity = initialVelocity;
            controller.damage = attackDamage;
            controller.expiresAt = Time.time + 4f;
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
            player.GetComponent<Health>()?.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
