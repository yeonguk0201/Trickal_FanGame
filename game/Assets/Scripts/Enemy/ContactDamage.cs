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
        [Tooltip("켜면 등급과 층에 관계없이 항상 반 칸 피해를 줍니다(쥬비).")]
        [SerializeField] private bool halfHeartDamage;

        private float nextDamageTime;

        public EnemyDamageTier DamageTier => damageTier;
        public bool HalfHeartDamage => halfHeartDamage;
        public float Cooldown => cooldown;
        public float NextDamageTime => nextDamageTime;

        public void Configure(EnemyDamageTier configuredDamageTier, float configuredCooldown)
        {
            damageTier = configuredDamageTier;
            cooldown = Mathf.Max(0f, configuredCooldown);
        }

        // Enemy-6: a fixed half heart on every floor instead of the tier table.
        public void ConfigureHalfHeart(bool configuredHalfHeartDamage) => halfHeartDamage = configuredHalfHeartDamage;

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
            target.TakeDamage(halfHeartDamage
                ? new DamageContext(gameObject, DamageSourceType.EnemyContact, HealthUnits.HalfHeartDamageUnits,
                    allowsHalfHeart: true)
                : HealthUnits.CreateEnemyDamageContext(gameObject, DamageSourceType.EnemyContact, damageTier));
            nextDamageTime = currentTime + cooldown;
            return true;
        }
    }
}
