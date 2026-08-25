using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public sealed class ContactDamage : MonoBehaviour
    {
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0f)] private float cooldown = 1f;

        private float nextDamageTime;

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (Time.time < nextDamageTime)
            {
                return;
            }

            Health target = collision.collider.GetComponentInParent<Health>();
            if (target == null || target.IsDead || target.GetComponent<PlayerMovement>() == null)
            {
                return;
            }

            target.GetComponent<PlayerDeathReason>()?.SetReason("ENEMY");
            target.TakeDamage(new DamageContext(gameObject, DamageSourceType.EnemyContact, damage));
            nextDamageTime = Time.time + cooldown;
        }
    }
}
