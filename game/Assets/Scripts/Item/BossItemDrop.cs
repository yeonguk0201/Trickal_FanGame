using TrickalFanGame.Enemy;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [RequireComponent(typeof(BossController), typeof(ItemDropSource))]
    public sealed class BossItemDrop : MonoBehaviour
    {
        [SerializeField] private bool isFinalBoss;
        [SerializeField] private BossController boss;
        [SerializeField] private ItemDropSource dropSource;

        public void Configure(bool configuredIsFinalBoss, ItemDropSource configuredDropSource)
        {
            isFinalBoss = configuredIsFinalBoss;
            dropSource = configuredDropSource;
            boss = GetComponent<BossController>();
        }

        private void Awake()
        {
            if (boss == null)
            {
                boss = GetComponent<BossController>();
            }

            if (dropSource == null)
            {
                dropSource = GetComponent<ItemDropSource>();
            }

            boss.Died += OnBossDied;
        }

        private void OnDestroy()
        {
            if (boss != null)
            {
                boss.Died -= OnBossDied;
            }
        }

        private void OnBossDied()
        {
            if (isFinalBoss)
            {
                Debug.Log("[BossItemDrop] Final boss defeated; no growth item is dropped.", this);
                return;
            }

            dropSource?.TryDrop();
        }
    }
}
