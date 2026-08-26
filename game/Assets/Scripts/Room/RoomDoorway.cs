using TrickalFanGame.Player;
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

        public RoomNode Source => source;
        public RoomNode Destination => destination;
        public Transform DestinationEntryPoint => destinationEntryPoint;
        public RoomController RequiredClearedRoom => requiredClearedRoom;
        public bool IsOpen => requiredClearedRoom == null || requiredClearedRoom.State == RoomState.Cleared;
        public bool AllowsOneWay => allowsOneWay;

        public void Configure(
            RoomGraphController configuredGraph,
            RoomNode configuredSource,
            RoomNode configuredDestination,
            Transform configuredDestinationEntryPoint,
            RoomController configuredRequiredClearedRoom = null,
            bool configuredAllowsOneWay = false)
        {
            graph = configuredGraph;
            source = configuredSource;
            destination = configuredDestination;
            destinationEntryPoint = configuredDestinationEntryPoint;
            requiredClearedRoom = configuredRequiredClearedRoom;
            allowsOneWay = configuredAllowsOneWay;
        }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            TryEnter(player);
        }

        public bool TryEnter(PlayerMovement player)
        {
            return IsOpen && player != null && graph != null &&
                   graph.TryTransition(source, destination, destinationEntryPoint, player);
        }
    }
}
