using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Room;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Item
{
    // The 교주의 권능 slot (Power-1, contract 23 §9). It holds one 권능 and its gauge. Winning a combat or boss room's
    // fight for the first time adds one charge up to the 권능's requirement; R uses a full gauge, empties it and keeps
    // the 권능. Touching another 권능 swaps: the held one drops with its gauge, and a new 권능 starts empty.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(PlayerInventory), typeof(PlayerPowerEffects))]
    public sealed class PlayerPowerSlot : MonoBehaviour
    {
        public const string UseKeyLabel = "R";
        private const string GeneratedInstancePrefix = "runtime-power-";

        [SerializeField] private RunProgress runProgress;
        [SerializeField] private ActivePowerPickup pickupPrefab;

        private Health health;
        private PlayerInventory inventory;
        private PlayerPowerEffects effects;
        private RunProgress subscribedProgress;
        private ItemDefinition heldDefinition;
        private string heldInstanceId;
        private int charge;
        private bool isUsing;
        private int generatedInstanceCount;

        public RunProgress Progress => runProgress;
        public ActivePowerPickup PickupPrefab => pickupPrefab;
        public ItemDefinition HeldDefinition => heldDefinition;
        public string HeldInstanceId => heldInstanceId;
        public bool HasPower => heldDefinition != null;
        public int Charge => charge;
        public int RequiredCharge => heldDefinition != null ? heldDefinition.ActiveChargeRooms : 0;
        public bool IsFullyCharged => HasPower && charge >= RequiredCharge;
        public event Action Changed;
        public event Action<ItemDefinition> PowerUsed;

        private void Awake()
        {
            health = GetComponent<Health>();
            inventory = GetComponent<PlayerInventory>();
            effects = GetComponent<PlayerPowerEffects>();
            if (runProgress == null) runProgress = FindFirstObjectByType<RunProgress>();
        }

        private void OnEnable() => BindProgress(runProgress);

        private void OnDisable() => BindProgress(null);

        private void Update()
        {
            if (Keyboard.current?.rKey.wasPressedThisFrame == true) TryUse();
        }

        public void Configure(RunProgress configuredProgress, ActivePowerPickup configuredPickupPrefab)
        {
            runProgress = configuredProgress;
            pickupPrefab = configuredPickupPrefab;
            if (isActiveAndEnabled) BindProgress(runProgress);
        }

        public bool CanCollect(ActivePowerPickup pickup)
        {
            EnsureReferences();
            return pickup != null && !pickup.IsCollected && pickup.Definition != null &&
                   pickup.Definition.IsValid && pickup.Definition.IsActivePower &&
                   health != null && !health.IsDead &&
                   runProgress != null && !runProgress.IsProgressionStopped &&
                   // A held 권능 is dropped on a swap, which needs the pickup Prefab; never lose it instead.
                   (!HasPower || pickupPrefab != null);
        }

        public bool TryCollect(ActivePowerPickup pickup)
        {
            if (!CanCollect(pickup)) return false;

            pickup.AssignInstanceId(GeneratedInstancePrefix + ++generatedInstanceCount);
            ItemDefinition previousDefinition = heldDefinition;
            string previousInstanceId = heldInstanceId;
            int previousCharge = charge;
            Transform dropParent = pickup.transform.parent;
            Vector3 dropPosition = pickup.transform.position;

            heldDefinition = pickup.Definition;
            heldInstanceId = pickup.InstanceId;
            charge = pickup.HasStoredCharge ? Mathf.Clamp(pickup.StoredCharge, 0, RequiredCharge) : 0;
            pickup.MarkCollected();
            inventory.TryRecordSingleUseAcquisition(heldDefinition, heldInstanceId);

            if (previousDefinition != null)
            {
                ActivePowerPickup dropped = Instantiate(pickupPrefab, dropPosition, Quaternion.identity, dropParent);
                dropped.name = $"Active Power ({previousDefinition.ItemId})";
                dropped.Configure(previousDefinition, previousInstanceId, previousCharge, true);
            }

            Debug.Log($"[PlayerPowerSlot] Holding {heldDefinition.DisplayName} ({charge}/{RequiredCharge}).", this);
            Changed?.Invoke();
            return true;
        }

        // Adds charge as if that many rooms had been cleared. Returns the charge actually added.
        public int AddCharge(int amount)
        {
            if (!HasPower || amount <= 0 || charge >= RequiredCharge) return 0;
            int previous = charge;
            charge = Mathf.Min(RequiredCharge, charge + amount);
            Changed?.Invoke();
            return charge - previous;
        }

        public PowerSlotUseResult GetUseState()
        {
            EnsureReferences();
            if (isUsing) return PowerSlotUseResult.AlreadyUsing;
            if (!HasPower) return PowerSlotUseResult.Empty;
            if (Time.timeScale <= 0f) return PowerSlotUseResult.Paused;
            if (health == null || health.IsDead) return PowerSlotUseResult.PlayerDead;
            if (runProgress == null || runProgress.IsProgressionStopped) return PowerSlotUseResult.RunStopped;
            if (runProgress.IsRewardSelectionPending) return PowerSlotUseResult.RewardSelectionOpen;
            if (!IsFullyCharged) return PowerSlotUseResult.NotCharged;
            return effects != null && effects.CanExecute(heldDefinition, out _)
                ? PowerSlotUseResult.Used
                : PowerSlotUseResult.ConditionNotMet;
        }

        public bool CanUse => GetUseState() == PowerSlotUseResult.Used;

        public PowerSlotUseResult TryUse()
        {
            PowerSlotUseResult state = GetUseState();
            if (state != PowerSlotUseResult.Used)
            {
                if (state == PowerSlotUseResult.ConditionNotMet && effects != null)
                {
                    effects.CanExecute(heldDefinition, out string reason);
                    Debug.Log($"[PlayerPowerSlot] {heldDefinition.DisplayName} was not used: {reason}", this);
                }
                return state;
            }

            // Empty the gauge before running the effect, so a re-entrant press never yields a second use.
            ItemDefinition used = heldDefinition;
            charge = 0;
            isUsing = true;
            try
            {
                effects.Execute(used);
            }
            finally
            {
                isUsing = false;
                Changed?.Invoke();
            }

            PowerUsed?.Invoke(used);
            Debug.Log($"[PlayerPowerSlot] Used {used.DisplayName}.", this);
            return PowerSlotUseResult.Used;
        }

        private void BindProgress(RunProgress progress)
        {
            if (subscribedProgress == progress) return;
            if (subscribedProgress != null) subscribedProgress.RoomCleared -= HandleRoomCleared;
            subscribedProgress = progress;
            if (subscribedProgress != null) subscribedProgress.RoomCleared += HandleRoomCleared;
        }

        // Only combat and boss rooms charge the gauge; the start, treasure, shop and secret rooms have no fight.
        private void HandleRoomCleared(string roomId)
        {
            if (!HasPower || health == null || health.IsDead || !IsCombatRoom(roomId)) return;
            if (AddCharge(1) > 0)
                Debug.Log($"[PlayerPowerSlot] {heldDefinition.DisplayName} charged {charge}/{RequiredCharge}.", this);
        }

        private bool IsCombatRoom(string roomId)
        {
            if (runProgress == null || runProgress.GeneratedGraph == null) return false;
            foreach (GeneratedRoomNode node in runProgress.GeneratedGraph.Nodes)
            {
                if (node != null && string.Equals(node.RoomId, roomId, StringComparison.Ordinal))
                    return node.Role is GeneratedRoomRole.Intermediate or GeneratedRoomRole.Boss;
            }

            return false;
        }

        private void EnsureReferences()
        {
            if (health == null || inventory == null || effects == null) Awake();
        }
    }
}
