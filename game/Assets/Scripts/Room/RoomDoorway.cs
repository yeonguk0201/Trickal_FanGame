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

        public RoomNode Source => source;
        public RoomNode Destination => destination;
        public RoomGraphController Graph => graph;
        public Transform DestinationEntryPoint => destinationEntryPoint;
        public RoomController RequiredClearedRoom => requiredClearedRoom;
        public bool RequiresKey => requiresKey;
        public bool IsKeyLockOpen => !requiresKey || keyLockState?.IsKeyLockOpen == true;
        public bool IsOpen => (requiredClearedRoom == null || requiredClearedRoom.State == RoomState.Cleared) &&
                              IsKeyLockOpen;
        public bool AllowsOneWay => allowsOneWay;

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

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryEnterFromContact(other.GetComponentInParent<PlayerMovement>());
        }

        // Stay keeps checking so a player who first grazes the trigger edge passes once centered.
        private void OnTriggerStay2D(Collider2D other)
        {
            TryEnterFromContact(other.GetComponentInParent<PlayerMovement>());
        }

        public bool TryEnterFromContact(PlayerMovement player)
        {
            return player != null && ContainsPassageCenter(player.transform.position) && TryEnter(player);
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
            if (player == null || graph == null ||
                (requiredClearedRoom != null && requiredClearedRoom.State != RoomState.Cleared))
            {
                return false;
            }

            if (!IsKeyLockOpen && !TryUnlockWithKey())
            {
                return false;
            }

            return IsOpen &&
                   graph.TryTransition(source, destination, destinationEntryPoint, player);
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
