using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [SerializeField] private ItemRewardSelectionSession rewardSelectionSession;
        [SerializeField] private ItemDefinition[] selectionItemPool = System.Array.Empty<ItemDefinition>();
        [SerializeField] private GameObject interactionMarker;
        [SerializeField] private GameObject interactionPrompt;

        private bool isPlayerInside;
        private PlayerInventory playerInventory;
        private RoomRunState runState;

        public bool HasRewarded { get; private set; }
        public string RewardId => $"{FloorGenerator.BuildRoomId(floorNumber, roomNumber)}:treasure";
        public bool CanInteract => !HasRewarded && isPlayerInside &&
                                   (prerequisiteRoom == null || prerequisiteRoom.State == RoomState.Cleared) &&
                                   rewardSelectionSession?.IsOpen != true;
        public GameObject InteractionMarker => interactionMarker;
        public GameObject InteractionPrompt => interactionPrompt;

        public void BindRunState(RoomRunState configuredState)
        {
            runState = configuredState;
            HasRewarded = runState != null && runState.HasClaimedArtifact;
            SynchronizeCompletedSelection();
        }

        public void Configure(
            int configuredFloorNumber,
            int configuredRoomNumber,
            RunProgress configuredRunProgress,
            ItemDropSource configuredDropSource,
            RoomController configuredPrerequisiteRoom = null,
            ItemRewardSelectionSession configuredSelectionSession = null,
            ItemDefinition[] configuredSelectionItemPool = null)
        {
            UnsubscribeSelection();
            floorNumber = Mathf.Max(1, configuredFloorNumber);
            roomNumber = Mathf.Max(1, configuredRoomNumber);
            runProgress = configuredRunProgress;
            dropSource = configuredDropSource;
            prerequisiteRoom = configuredPrerequisiteRoom;
            rewardSelectionSession = configuredSelectionSession;
            selectionItemPool = configuredSelectionItemPool ?? System.Array.Empty<ItemDefinition>();
            dropSource?.ConfigureRewardContext(
                configuredRunProgress,
                RewardId);
            SubscribeSelection();
            SynchronizeCompletedSelection();
            RefreshInteractionVisuals();
        }

        public void ConfigureInteractionVisuals(GameObject configuredMarker, GameObject configuredPrompt)
        {
            interactionMarker = configuredMarker;
            interactionPrompt = configuredPrompt;
            if (TryGetComponent(out BoxCollider2D interactionZone))
            {
                interactionZone.isTrigger = true;
                interactionZone.size = new Vector2(2.6f, 2.6f);
            }
            RefreshInteractionVisuals();
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
            SubscribeSelection();
            SynchronizeCompletedSelection();
            RefreshInteractionVisuals();
        }

        private void OnEnable()
        {
            SubscribeSelection();
            RefreshInteractionVisuals();
        }

        private void OnDisable() => UnsubscribeSelection();

        private void OnDestroy()
        {
            ReleaseRuntimeBindings();
        }

        public void ReleaseRuntimeBindings()
        {
            if (prerequisiteRoom != null)
            {
                prerequisiteRoom.StateChanged -= OnPrerequisiteStateChanged;
            }
            UnsubscribeSelection();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerMovement>() == null)
            {
                return;
            }

            isPlayerInside = true;
            playerInventory = other.GetComponentInParent<PlayerInventory>();
            RefreshInteractionVisuals();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerMovement>() != null)
            {
                isPlayerInside = false;
                playerInventory = null;
                RefreshInteractionVisuals();
            }
        }

        private void Update()
        {
            if (CanInteract && Keyboard.current?.eKey.wasPressedThisFrame == true)
                TryInteract();
        }

        private void OnPrerequisiteStateChanged(RoomState state)
        {
            if (state == RoomState.Cleared)
            {
                RefreshInteractionVisuals();
            }
        }

        public bool TryInteract() => CanInteract && TryOpenRewardSelection(playerInventory);

        public bool TryOpenRewardSelection(PlayerInventory inventory)
        {
            if (HasRewarded || !isPlayerInside ||
                (prerequisiteRoom != null && prerequisiteRoom.State != RoomState.Cleared))
            {
                return false;
            }

            runProgress?.RecordRoomEntry(floorNumber, roomNumber);
            if (rewardSelectionSession == null)
            {
                HasRewarded = dropSource != null && dropSource.TryDrop(inventory);
                if (HasRewarded) runState?.MarkArtifactClaimed();
                return HasRewarded;
            }

            ItemDefinition[] pool = selectionItemPool != null && selectionItemPool.Length > 0
                ? selectionItemPool
                : CopyPool(dropSource?.ItemPool);
            if (rewardSelectionSession.TryOpen(pool, RewardId, out string error))
            {
                RefreshInteractionVisuals();
                return true;
            }
            SynchronizeCompletedSelection();
            if (!HasRewarded && !string.IsNullOrWhiteSpace(error))
                Debug.LogError($"[RewardRoom] Could not open '{RewardId}'. {error}", this);
            return HasRewarded;
        }

        public void SetPlayerPresenceForVerification(bool inside, PlayerInventory inventory)
        {
            isPlayerInside = inside;
            playerInventory = inside ? inventory : null;
            RefreshInteractionVisuals();
        }

        private void OnSelectionCompleted(ItemRewardSelectionState selection, ItemRewardCandidate candidate)
        {
            if (!string.Equals(selection?.RewardId, RewardId, System.StringComparison.Ordinal)) return;
            CompleteReward(candidate);
        }

        private void SynchronizeCompletedSelection()
        {
            ItemRewardSelectionState selection = runProgress?.GetRewardSelection(RewardId);
            if (selection == null || !selection.IsCompleted) return;
            ItemRewardCandidate selected = null;
            for (int index = 0; index < selection.Candidates.Count; index++)
                if (selection.Candidates[index].StableId == selection.SelectedCandidateId)
                    selected = selection.Candidates[index];
            CompleteReward(selected);
        }

        private void CompleteReward(ItemRewardCandidate selected)
        {
            if (HasRewarded) return;
            HasRewarded = true;
            runState?.MarkArtifactClaimed();
            if (selected != null) dropSource?.MarkSelectionResolved(selected);
            RefreshInteractionVisuals();
        }

        private void SubscribeSelection()
        {
            if (rewardSelectionSession != null)
            {
                rewardSelectionSession.Completed -= OnSelectionCompleted;
                rewardSelectionSession.Completed += OnSelectionCompleted;
                rewardSelectionSession.Cancelled -= OnSelectionCancelled;
                rewardSelectionSession.Cancelled += OnSelectionCancelled;
            }
        }

        private void UnsubscribeSelection()
        {
            if (rewardSelectionSession != null)
            {
                rewardSelectionSession.Completed -= OnSelectionCompleted;
                rewardSelectionSession.Cancelled -= OnSelectionCancelled;
            }
        }

        private void OnSelectionCancelled(ItemRewardSelectionState selection)
        {
            if (string.Equals(selection?.RewardId, RewardId, System.StringComparison.Ordinal))
                RefreshInteractionVisuals();
        }

        private void RefreshInteractionVisuals()
        {
            if (interactionMarker != null) interactionMarker.SetActive(!HasRewarded);
            if (interactionPrompt != null) interactionPrompt.SetActive(CanInteract);
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
