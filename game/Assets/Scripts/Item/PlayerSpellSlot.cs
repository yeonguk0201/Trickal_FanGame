using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Room;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Item
{
    // The one slot shared by single-use spells and jjangsem spells. Left Shift uses the held item; a successful use
    // consumes it exactly once, and a blocked or failed use keeps it in the slot without running its effect.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(PlayerInventory), typeof(PlayerSingleUseEffects))]
    public sealed class PlayerSpellSlot : MonoBehaviour
    {
        public const string UseKeyLabel = "Shift";
        private const string GeneratedInstancePrefix = "runtime-single-use-";

        [SerializeField] private RunProgress runProgress;
        [SerializeField] private SingleUseItemPickup pickupPrefab;

        private Health health;
        private PlayerInventory inventory;
        private ISingleUseItemExecutor executor;
        private ItemDefinition heldDefinition;
        private string heldInstanceId;
        private bool isUsing;
        private int generatedInstanceCount;

        public RunProgress Progress => runProgress;
        public SingleUseItemPickup PickupPrefab => pickupPrefab;
        public ItemDefinition HeldDefinition => heldDefinition;
        public string HeldInstanceId => heldInstanceId;
        public bool HasItem => heldDefinition != null;
        public event Action Changed;
        public event Action<ItemDefinition> ItemUsed;

        private void Awake()
        {
            health = GetComponent<Health>();
            inventory = GetComponent<PlayerInventory>();
            executor ??= GetComponent<ISingleUseItemExecutor>();
            if (runProgress == null) runProgress = FindFirstObjectByType<RunProgress>();
        }

        private void Update()
        {
            if (Keyboard.current?.leftShiftKey.wasPressedThisFrame == true) TryUse();
        }

        public void Configure(RunProgress configuredProgress, SingleUseItemPickup configuredPickupPrefab)
        {
            runProgress = configuredProgress;
            pickupPrefab = configuredPickupPrefab;
        }

        // Verification seam: replaces the component executor with a test double.
        public void ConfigureExecutor(ISingleUseItemExecutor configuredExecutor)
        {
            executor = configuredExecutor;
        }

        public bool CanCollect(SingleUseItemPickup pickup)
        {
            EnsureReferences();
            return pickup != null && !pickup.IsCollected && pickup.Definition != null &&
                   pickup.Definition.IsValid && pickup.Definition.IsSingleUse &&
                   health != null && !health.IsDead &&
                   runProgress != null && !runProgress.IsProgressionStopped &&
                   // A full slot drops the held item, which needs the pickup Prefab; never lose it instead.
                   (!HasItem || pickupPrefab != null);
        }

        public bool TryCollect(SingleUseItemPickup pickup)
        {
            if (!CanCollect(pickup)) return false;

            pickup.AssignInstanceId(GeneratedInstancePrefix + ++generatedInstanceCount);
            ItemDefinition previousDefinition = heldDefinition;
            string previousInstanceId = heldInstanceId;
            Transform dropParent = pickup.transform.parent;
            Vector3 dropPosition = pickup.transform.position;

            heldDefinition = pickup.Definition;
            heldInstanceId = pickup.InstanceId;
            pickup.MarkCollected();
            inventory.TryRecordSingleUseAcquisition(heldDefinition, heldInstanceId);

            if (previousDefinition != null)
            {
                SingleUseItemPickup dropped = Instantiate(pickupPrefab, dropPosition, Quaternion.identity, dropParent);
                dropped.name = $"Single Use Item ({previousDefinition.ItemId})";
                dropped.Configure(previousDefinition, previousInstanceId, true);
            }

            Changed?.Invoke();
            return true;
        }

        public SpellSlotUseResult GetUseState()
        {
            EnsureReferences();
            if (isUsing) return SpellSlotUseResult.AlreadyUsing;
            if (!HasItem) return SpellSlotUseResult.Empty;
            if (Time.timeScale <= 0f) return SpellSlotUseResult.Paused;
            if (health == null || health.IsDead) return SpellSlotUseResult.PlayerDead;
            if (runProgress == null || runProgress.IsProgressionStopped) return SpellSlotUseResult.RunStopped;
            if (runProgress.IsRewardSelectionPending) return SpellSlotUseResult.RewardSelectionOpen;
            return executor != null && executor.CanExecute(heldDefinition, out _)
                ? SpellSlotUseResult.Used
                : SpellSlotUseResult.ConditionNotMet;
        }

        public bool CanUse => GetUseState() == SpellSlotUseResult.Used;

        public SpellSlotUseResult TryUse()
        {
            SpellSlotUseResult state = GetUseState();
            if (state != SpellSlotUseResult.Used)
            {
                if (state == SpellSlotUseResult.ConditionNotMet && executor != null)
                {
                    executor.CanExecute(heldDefinition, out string reason);
                    Debug.Log($"[PlayerSpellSlot] {heldDefinition.DisplayName} was not used: {reason}", this);
                }
                return state;
            }

            // Consume before running the effect, so a re-entrant press or an exception never yields a second use.
            ItemDefinition used = heldDefinition;
            heldDefinition = null;
            heldInstanceId = null;
            isUsing = true;
            try
            {
                executor.Execute(used);
            }
            finally
            {
                isUsing = false;
                Changed?.Invoke();
            }

            ItemUsed?.Invoke(used);
            Debug.Log($"[PlayerSpellSlot] Used {used.DisplayName}.", this);
            return SpellSlotUseResult.Used;
        }

        private void EnsureReferences()
        {
            if (health == null || inventory == null) Awake();
        }
    }
}
