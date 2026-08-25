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

        public RoomNode Source => source;
        public RoomNode Destination => destination;
        public Transform DestinationEntryPoint => destinationEntryPoint;
        public bool IsOpen => requiredClearedRoom == null || requiredClearedRoom.State == RoomState.Cleared;

        public void Configure(
            RoomGraphController configuredGraph,
            RoomNode configuredSource,
            RoomNode configuredDestination,
            Transform configuredDestinationEntryPoint,
            RoomController configuredRequiredClearedRoom = null)
        {
            graph = configuredGraph;
            source = configuredSource;
            destination = configuredDestination;
            destinationEntryPoint = configuredDestinationEntryPoint;
            requiredClearedRoom = configuredRequiredClearedRoom;
        }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsOpen)
            {
                return;
            }

            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player != null)
            {
                graph?.TryTransition(source, destination, destinationEntryPoint, player);
            }
        }
    }
}
