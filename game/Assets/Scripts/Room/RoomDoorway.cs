using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class RoomDoorway : MonoBehaviour
    {
        [SerializeField] private RoomGraphController graph;
        [SerializeField] private RoomNode source;
        [SerializeField] private RoomNode destination;
        [SerializeField] private Transform destinationEntryPoint;
        [SerializeField] private RoomController requiredClearedRoom;
        [SerializeField] private bool allowsOneWay;

        private bool requiresKey;
        private RoomRunState keyLockState;
        private DoorController doorVisual;
        private bool allowsCombatExit;

        public RoomNode Source => source;
        public RoomNode Destination => destination;
        public RoomGraphController Graph => graph;
        public Transform DestinationEntryPoint => destinationEntryPoint;
        public RoomController RequiredClearedRoom => requiredClearedRoom;
        public bool RequiresKey => requiresKey;
        public bool IsKeyLockOpen => !requiresKey || keyLockState?.IsKeyLockOpen == true;
        public bool IsOpen => IsRoomLockOpen && IsKeyLockOpen;
        public bool AllowsOneWay => allowsOneWay;
        public bool AllowsCombatExit => allowsCombatExit;

        private bool IsRoomLockOpen =>
            requiredClearedRoom == null || requiredClearedRoom.State == RoomState.Cleared ||
            (allowsCombatExit && !requiredClearedRoom.IsProgressionStopped);

        public void Configure(
            RoomGraphController configuredGraph,
            RoomNode configuredSource,
            RoomNode configuredDestination,
            Transform configuredDestinationEntryPoint,
            RoomController configuredRequiredClearedRoom = null,
            bool configuredAllowsOneWay = false,
            bool configuredRequiresKey = false,
            RoomRunState configuredKeyLockState = null,
            DoorController configuredDoorVisual = null)
        {
            graph = configuredGraph;
            source = configuredSource;
            destination = configuredDestination;
            destinationEntryPoint = configuredDestinationEntryPoint;
            requiredClearedRoom = configuredRequiredClearedRoom;
            allowsOneWay = configuredAllowsOneWay;
            requiresKey = configuredRequiresKey;
            keyLockState = configuredKeyLockState;
            doorVisual = configuredDoorVisual;
            doorVisual?.SetKeyLocked(requiresKey && keyLockState?.IsKeyLockOpen != true);
        }

        // An opened hidden passage lets the player leave mid-fight. The room left behind resets like any escaped
        // fight: its enemies leave and the next entry restarts the Encounter from the first wave.
        public void ConfigureCombatExit(bool configuredAllowsCombatExit)
        {
            allowsCombatExit = configuredAllowsCombatExit;
        }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryEnterFromTrigger(other);
        }

        // Stay keeps checking so a player who first grazes the trigger edge passes once centered.
        private void OnTriggerStay2D(Collider2D other)
        {
            TryEnterFromTrigger(other);
        }

        // Hitbox-0: the body circle reaches the trigger before the feet do, and further ahead the larger the body
        // is. Only feet standing in the trigger count as passing through.
        private void TryEnterFromTrigger(Collider2D other)
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player != null && player.FeetOverlap(GetComponent<Collider2D>())) TryEnterFromContact(player);
        }

        public bool TryEnterFromContact(PlayerMovement player)
        {
            return player != null && ContainsPassageCenter(player.FeetPosition) &&
                   IsMovingIntoPassage(player.MovementIntent) && TryEnter(player);
        }

        public bool IsMovingIntoPassage(Vector2 movementIntent)
        {
            if (source == null || movementIntent.sqrMagnitude < 0.001f) return false;
            Vector2 outward = transform.position - source.transform.position;
            // Use the door normal, not the player's facing or collision velocity. A player can
            // still face the exit while standing still, or have zero velocity against a barrier.
            outward = Mathf.Abs(outward.x) > Mathf.Abs(outward.y)
                ? new Vector2(Mathf.Sign(outward.x), 0f)
                : new Vector2(0f, Mathf.Sign(outward.y));
            return Vector2.Dot(movementIntent.normalized, outward) > 0.5f;
        }

        /// <summary>
        /// True when the point lies within the trigger's width along the door, so brushing the frame
        /// with the body edge does not count as passing through.
        /// </summary>
        public bool ContainsPassageCenter(Vector2 worldPoint)
        {
            if (!TryGetComponent(out BoxCollider2D box))
            {
                return true;
            }

            Vector2 local = (Vector2)transform.InverseTransformPoint(worldPoint) - box.offset;
            bool lateralIsX = box.size.x >= box.size.y;
            float lateralOffset = lateralIsX ? local.x : local.y;
            float halfWidth = (lateralIsX ? box.size.x : box.size.y) * 0.5f;
            return Mathf.Abs(lateralOffset) <= halfWidth;
        }

        public bool TryEnter(PlayerMovement player)
        {
            if (player == null || graph == null || !IsRoomLockOpen)
            {
                return false;
            }

            if (!IsKeyLockOpen && !TryUnlockWithKey())
            {
                return false;
            }

            if (!IsOpen || !graph.TryTransition(source, destination, destinationEntryPoint, player))
            {
                return false;
            }

            // Move first, then reset the room, so a refused transition never leaves a reset room behind.
            if (requiredClearedRoom != null && requiredClearedRoom.State == RoomState.Combat)
            {
                requiredClearedRoom.TryAbandonCombat();
            }

            return true;
        }

        public bool TryUnlockWithKey()
        {
            if (!requiresKey || keyLockState == null)
            {
                return !requiresKey;
            }

            if (keyLockState.IsKeyLockOpen)
            {
                doorVisual?.SetKeyLocked(false);
                return true;
            }

            RunProgress progress = graph != null ? graph.Progress : null;
            if (progress == null || !progress.TrySpendResource(RunResourceType.Key))
            {
                return false;
            }

            if (!keyLockState.TryOpenKeyLock())
            {
                // A single-threaded Unity frame cannot normally reach this branch, but keep the spend atomic.
                progress.TryAddResource(RunResourceType.Key, 1);
                return keyLockState.IsKeyLockOpen;
            }

            doorVisual?.SetKeyLocked(false);
            return true;
        }
    }
}
