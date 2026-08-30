using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class FloorAdvancePortal : MonoBehaviour
    {
        public static readonly Color LockedColor = new(0.12f, 0.4f, 0.46f, 0.55f);
        public static readonly Color UnlockedColor = new(0.25f, 0.9f, 1f, 0.9f);

        private RoomGraphAssembler assembler;
        private RoomController bossRoom;
        private int destinationFloor;
        private Collider2D portalTrigger;
        private SpriteRenderer indicator;

        public int DestinationFloor => destinationFloor;
        public bool IsUnlocked { get; private set; }
        public SpriteRenderer Indicator => indicator;

        public void Configure(
            RoomGraphAssembler configuredAssembler,
            RoomController configuredBossRoom,
            int configuredDestinationFloor,
            SpriteRenderer configuredIndicator)
        {
            if (bossRoom != null)
            {
                bossRoom.StateChanged -= OnBossRoomStateChanged;
            }

            assembler = configuredAssembler;
            bossRoom = configuredBossRoom;
            destinationFloor = Mathf.Max(1, configuredDestinationFloor);
            indicator = configuredIndicator;
            EnsureTrigger();

            if (bossRoom != null)
            {
                bossRoom.StateChanged += OnBossRoomStateChanged;
            }

            SetUnlocked(bossRoom != null && bossRoom.State == RoomState.Cleared);
        }

        private void Awake() => EnsureTrigger();

        private void OnDestroy()
        {
            if (bossRoom != null)
            {
                bossRoom.StateChanged -= OnBossRoomStateChanged;
            }
        }

        private void OnBossRoomStateChanged(RoomState state) => SetUnlocked(state == RoomState.Cleared);

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player != null) TryEnter(player);
        }

        public bool TryEnter(PlayerMovement player)
        {
            if (!IsUnlocked || player == null || assembler == null || bossRoom == null ||
                player.GetComponent<PlayerActionState>()?.CanTransition == false) return false;

            if (assembler.TryLoadFloor(destinationFloor, player, out string error))
            {
                Debug.Log($"[FloorAdvancePortal] Entered floor {destinationFloor}.", this);
                return true;
            }

            Debug.LogError($"[FloorAdvancePortal] Could not enter floor {destinationFloor}. {error}", this);
            return false;
        }

        private void EnsureTrigger()
        {
            if (portalTrigger == null)
            {
                portalTrigger = GetComponent<Collider2D>();
            }

            if (portalTrigger != null)
            {
                portalTrigger.isTrigger = true;
            }
        }

        private void SetUnlocked(bool unlocked)
        {
            IsUnlocked = unlocked;
            EnsureTrigger();
            if (portalTrigger != null)
            {
                portalTrigger.enabled = unlocked;
            }

            if (indicator != null)
            {
                indicator.color = unlocked
                    ? UnlockedColor
                    : LockedColor;
            }
        }
    }
}
