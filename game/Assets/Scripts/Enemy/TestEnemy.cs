using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [RequireComponent(typeof(Health))]
    public sealed class TestEnemy : MonoBehaviour
    {
        [SerializeField] private bool allowsSPDrop = true;
        [SerializeField] private bool destroyOnBossCollision;

        private Health health;

        public bool AllowsSPDrop => allowsSPDrop;
        public bool DestroyOnBossCollision => destroyOnBossCollision;

        public void ConfigureReward(bool configuredAllowsSPDrop)
        {
            allowsSPDrop = configuredAllowsSPDrop;
        }

        public void ConfigureBossCollision(bool configuredDestroyOnBossCollision)
        {
            destroyOnBossCollision = configuredDestroyOnBossCollision;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health == null)
            {
                return;
            }

            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryBreakForBoss(collision.collider.GetComponentInParent<BossController>());
        }

        public bool TryBreakForBoss(BossController source)
        {
            if (!destroyOnBossCollision || source == null) return false;
            if (health == null) health = GetComponent<Health>();
            if (health == null || health.IsDead) return false;
            health.TakeDamage(new DamageContext(source.gameObject, DamageSourceType.EnemyContact,
                health.MaxHealth + health.CurrentShield));
            return true;
        }

        private void OnDamaged(float current, float maximum)
        {
            Debug.Log($"{name}: HP {current}/{maximum}");
        }

        private void OnDied()
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
