using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public sealed class ContactDamage : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float damage = 1f;
        [SerializeField, Min(0f)] private float cooldown = 1f;

        private float nextDamageTime;

        public float Damage => damage;
        public float Cooldown => cooldown;
        public float NextDamageTime => nextDamageTime;

        public void Configure(float configuredDamage, float configuredCooldown)
        {
            damage = Mathf.Max(0.01f, configuredDamage);
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
            target.TakeDamage(new DamageContext(gameObject, DamageSourceType.EnemyContact, damage));
            nextDamageTime = currentTime + cooldown;
            return true;
        }
    }
}
