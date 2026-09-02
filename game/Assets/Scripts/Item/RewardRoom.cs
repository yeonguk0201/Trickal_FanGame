using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [RequireComponent(typeof(Collider2D), typeof(ItemDropSource))]
    public sealed class RewardRoom : MonoBehaviour
    {
        [SerializeField, Min(1)] private int floorNumber = 1;
        [SerializeField, Min(1)] private int roomNumber = 1;
        [SerializeField] private RunProgress runProgress;
        [SerializeField] private ItemDropSource dropSource;
        [SerializeField] private RoomController prerequisiteRoom;

        private bool isPlayerInside;
        private PlayerInventory playerInventory;
        private RoomRunState runState;

        public bool HasRewarded { get; private set; }

        public void BindRunState(RoomRunState configuredState)
        {
            runState = configuredState;
            HasRewarded = runState != null && runState.HasClaimedArtifact;
        }

        public void Configure(
            int configuredFloorNumber,
            int configuredRoomNumber,
            RunProgress configuredRunProgress,
            ItemDropSource configuredDropSource,
            RoomController configuredPrerequisiteRoom = null)
        {
            floorNumber = Mathf.Max(1, configuredFloorNumber);
            roomNumber = Mathf.Max(1, configuredRoomNumber);
            runProgress = configuredRunProgress;
            dropSource = configuredDropSource;
            prerequisiteRoom = configuredPrerequisiteRoom;
            dropSource?.ConfigureRewardContext(
                configuredRunProgress,
                $"{FloorGenerator.BuildRoomId(floorNumber, roomNumber)}:treasure");
        }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            if (dropSource == null)
            {
                dropSource = GetComponent<ItemDropSource>();
            }

            if (runProgress == null)
            {
                runProgress = FindFirstObjectByType<RunProgress>();
            }

            if (prerequisiteRoom != null)
            {
                prerequisiteRoom.StateChanged += OnPrerequisiteStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (prerequisiteRoom != null)
            {
                prerequisiteRoom.StateChanged -= OnPrerequisiteStateChanged;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerMovement>() == null)
            {
                return;
            }

            isPlayerInside = true;
            playerInventory = other.GetComponentInParent<PlayerInventory>();
            TryGrantReward();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerMovement>() != null)
            {
                isPlayerInside = false;
                playerInventory = null;
            }
        }

        private void OnPrerequisiteStateChanged(RoomState state)
        {
            if (state == RoomState.Cleared)
            {
                TryGrantReward();
            }
        }

        private void TryGrantReward()
        {
            if (HasRewarded || !isPlayerInside ||
                (prerequisiteRoom != null && prerequisiteRoom.State != RoomState.Cleared))
            {
                return;
            }

            runProgress?.RecordRoomEntry(floorNumber, roomNumber);
            HasRewarded = dropSource != null && dropSource.TryDrop(playerInventory);
            if (HasRewarded) runState?.MarkArtifactClaimed();
        }
    }
}
