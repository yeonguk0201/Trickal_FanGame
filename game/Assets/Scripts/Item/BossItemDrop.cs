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
        [SerializeField] private ItemRewardSelectionSession rewardSelectionSession;
        [SerializeField] private ItemDefinition[] selectionItemPool = System.Array.Empty<ItemDefinition>();

        private bool rewardRequested;

        public bool IsFinalBoss => isFinalBoss;
        public bool UsesSelectionReward => !isFinalBoss && rewardSelectionSession != null;
        public string RewardId => dropSource != null ? dropSource.RewardId : string.Empty;

        public void Configure(bool configuredIsFinalBoss, ItemDropSource configuredDropSource)
        {
            Configure(configuredIsFinalBoss, configuredDropSource, null, null);
        }

        public void Configure(bool configuredIsFinalBoss, ItemDropSource configuredDropSource,
            ItemRewardSelectionSession configuredSelectionSession, ItemDefinition[] configuredSelectionItemPool)
        {
            UnsubscribeSelection();
            isFinalBoss = configuredIsFinalBoss;
            dropSource = configuredDropSource;
            rewardSelectionSession = configuredSelectionSession;
            selectionItemPool = configuredSelectionItemPool ?? System.Array.Empty<ItemDefinition>();
            boss = GetComponent<BossController>();
            rewardRequested = false;
            SubscribeSelection();
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
            SubscribeSelection();
        }

        private void OnDestroy()
        {
            ReleaseRuntimeBindings();
        }

        public void ReleaseRuntimeBindings()
        {
            if (boss != null)
            {
                boss.Died -= OnBossDied;
            }
            UnsubscribeSelection();
        }

        private void OnBossDied() => TryHandleBossDefeated();

        public bool TryHandleBossDefeated()
        {
            if (rewardRequested) return false;
            rewardRequested = true;
            if (isFinalBoss)
            {
                Debug.Log("[BossItemDrop] Final boss defeated; no growth item is dropped.", this);
                return true;
            }

            if (rewardSelectionSession == null) return dropSource != null && dropSource.TryDrop();
            ItemDefinition[] pool = selectionItemPool != null && selectionItemPool.Length > 0
                ? selectionItemPool
                : CopyPool(dropSource?.ItemPool);
            if (rewardSelectionSession.TryOpen(pool, RewardId, out string error)) return true;
            Debug.LogError($"[BossItemDrop] Could not open '{RewardId}'. {error}", this);
            return false;
        }

        private void OnSelectionCompleted(ItemRewardSelectionState selection, ItemRewardCandidate candidate)
        {
            if (string.Equals(selection?.RewardId, RewardId, System.StringComparison.Ordinal))
                dropSource?.MarkSelectionResolved(candidate);
        }

        private void SubscribeSelection()
        {
            if (rewardSelectionSession == null) return;
            rewardSelectionSession.Completed -= OnSelectionCompleted;
            rewardSelectionSession.Completed += OnSelectionCompleted;
        }

        private void UnsubscribeSelection()
        {
            if (rewardSelectionSession != null)
                rewardSelectionSession.Completed -= OnSelectionCompleted;
        }

        private static ItemDefinition[] CopyPool(System.Collections.Generic.IReadOnlyList<ItemDefinition> source)
        {
            if (source == null) return System.Array.Empty<ItemDefinition>();
            ItemDefinition[] result = new ItemDefinition[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }
}
