using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RoomDoorSlot : MonoBehaviour
    {
        [SerializeField] private RoomDoorDirection direction;
        [SerializeField] private RoomDoorway doorway;
        [SerializeField] private Transform entryPoint;
        [SerializeField] private DoorController blocker;
        [SerializeField] private GameObject seal;

        public RoomDoorDirection Direction => direction;
        public RoomDoorway Doorway => doorway;
        public Transform EntryPoint => entryPoint;
        public DoorController Blocker => blocker;
        public GameObject Seal => seal;
        public bool IsConnected => doorway != null && doorway.gameObject.activeSelf;

        public void Configure(RoomDoorDirection configuredDirection, RoomDoorway configuredDoorway,
            Transform configuredEntryPoint, DoorController configuredBlocker, GameObject configuredSeal)
        {
            direction = configuredDirection; doorway = configuredDoorway; entryPoint = configuredEntryPoint;
            blocker = configuredBlocker; seal = configuredSeal;
        }

        public void Bind(RoomGraphController graph, RoomNode source, RoomNode destination,
            Transform destinationEntryPoint, RoomController requiredClearedRoom)
        {
            bool connected = destination != null;
            if (doorway != null)
            {
                doorway.Configure(graph, source, destination, destinationEntryPoint, requiredClearedRoom);
                doorway.gameObject.SetActive(connected);
            }
            if (blocker != null) blocker.gameObject.SetActive(connected);
            if (seal != null) seal.SetActive(!connected);
        }
    }
}
