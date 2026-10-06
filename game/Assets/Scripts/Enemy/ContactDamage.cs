using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public sealed class ContactDamage : MonoBehaviour
    {
        [SerializeField] private EnemyDamageTier damageTier = EnemyDamageTier.Light;
        [SerializeField, Min(0f)] private float cooldown = 1f;

        private float nextDamageTime;

        public EnemyDamageTier DamageTier => damageTier;
        public float Cooldown => cooldown;
        public float NextDamageTime => nextDamageTime;

        public void Configure(EnemyDamageTier configuredDamageTier, float configuredCooldown)
        {
            damageTier = configuredDamageTier;
            cooldown = Mathf.Max(0f, configuredCooldown);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            Health target = collision.collider.GetComponentInParent<Health>();
            TryApplyDamage(target, Time.time);
        }

        public bool TryApplyDamage(Health target, float currentTime)
        {
            if (currentTime < nextDamageTime || target == null || target.IsDead ||
                target.GetComponent<PlayerMovement>() == null)
            {
                return false;
            }

            target.GetComponent<PlayerDeathReason>()?.SetReason("ENEMY");
            target.TakeDamage(HealthUnits.CreateEnemyDamageContext(gameObject, DamageSourceType.EnemyContact, damageTier));
            nextDamageTime = currentTime + cooldown;
            return true;
        }
    }
}
