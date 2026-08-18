using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    [RequireComponent(typeof(Health))]
    public sealed class TestEnemy : MonoBehaviour
    {
        private Health health;

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

        private void OnDamaged(int current, int maximum)
        {
            Debug.Log($"{name}: HP {current}/{maximum}");
        }

        private void OnDied()
        {
            Destroy(gameObject);
        }
    }
}
