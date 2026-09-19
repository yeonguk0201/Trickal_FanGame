using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEngine;

namespace TrickalFanGame.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCombatEvents))]
    public sealed class PlayerSPDropper : MonoBehaviour
    {
        [SerializeField] private SPPickup pickupPrefab;
        [SerializeField, Range(0f, 1f)] private float dropChance = 0.25f;

        private PlayerCombatEvents combatEvents;

        public float DropChance => dropChance;

        private void Awake()
        {
            combatEvents = GetComponent<PlayerCombatEvents>();
        }

        private void OnEnable()
        {
            if (combatEvents == null)
            {
                combatEvents = GetComponent<PlayerCombatEvents>();
            }

            combatEvents.EnemyKilled -= OnEnemyKilled;
            combatEvents.EnemyKilled += OnEnemyKilled;
        }

        private void OnDisable()
        {
            if (combatEvents != null)
            {
                combatEvents.EnemyKilled -= OnEnemyKilled;
            }
        }

        public void Configure(SPPickup configuredPrefab, float configuredDropChance = 0.25f)
        {
            pickupPrefab = configuredPrefab;
            dropChance = Mathf.Clamp01(configuredDropChance);
        }

        public bool ShouldDrop(float randomValue)
        {
            return randomValue >= 0f && (dropChance >= 1f || randomValue < dropChance);
        }

        public SPPickup TrySpawnPickup(Vector3 position, Transform parent, float randomValue)
        {
            if (pickupPrefab == null || !ShouldDrop(randomValue))
            {
                return null;
            }

            return Instantiate(pickupPrefab, position, Quaternion.identity, parent);
        }

        public bool CanDropFrom(Health target)
        {
            TestEnemy enemy = target != null ? target.GetComponent<TestEnemy>() : null;
            return target != null && (enemy == null || enemy.AllowsSPDrop);
        }

        private void OnEnemyKilled(PlayerEnemyKilledEvent killEvent)
        {
            if (!CanDropFrom(killEvent.Target)) return;
            Transform dropParent = killEvent.Target.transform.parent;
            TrySpawnPickup(killEvent.Target.transform.position, dropParent, Random.value);
        }
    }
}
