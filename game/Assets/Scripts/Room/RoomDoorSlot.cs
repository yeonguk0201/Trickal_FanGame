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
            Transform destinationEntryPoint, RoomController requiredClearedRoom,
            bool requiresKey = false, RoomRunState keyLockState = null, bool isSealed = false)
        {
            // A sealed hidden passage keeps its destination but stays a wall until it opens.
            bool connected = destination != null && !isSealed;
            if (doorway != null)
            {
                doorway.Configure(graph, source, destination, destinationEntryPoint, requiredClearedRoom,
                    false, requiresKey, keyLockState, blocker);
                doorway.gameObject.SetActive(connected);
            }
            if (blocker != null)
            {
                blocker.SetKeyLocked(connected && requiresKey && keyLockState?.IsKeyLockOpen != true);
                blocker.gameObject.SetActive(connected);
            }
            if (seal != null) seal.SetActive(!connected);
        }
    }
}
