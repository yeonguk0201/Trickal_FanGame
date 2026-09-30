using System;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RoomNode : MonoBehaviour
    {
        [SerializeField] private string roomId;
        [SerializeField, Min(1)] private int floorNumber = 1;
        [SerializeField, Min(1)] private int roomNumber = 1;
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private Transform defaultEntryPoint;
        [SerializeField] private RoomDoorway[] doorways = Array.Empty<RoomDoorway>();
        [SerializeField] private RoomDefinition definition;

        private RoomProfile profile;

        public string RoomId => roomId;
        public int FloorNumber => floorNumber;
        public int RoomNumber => roomNumber;
        public GameObject ContentRoot => contentRoot;
        public Transform CameraAnchor => cameraAnchor;
        public Transform DefaultEntryPoint => defaultEntryPoint;
        public RoomDoorway[] Doorways => doorways;
        public RoomDefinition Definition => definition;
        public RoomProfile Profile => profile;
        public bool IsVisible => contentRoot != null && contentRoot.activeSelf;
        public bool HasBeenVisited { get; private set; }

        public void Configure(
            string configuredRoomId,
            int configuredFloorNumber,
            int configuredRoomNumber,
            GameObject configuredContentRoot,
            Transform configuredCameraAnchor,
            Transform configuredDefaultEntryPoint,
            RoomDoorway[] configuredDoorways)
        {
            roomId = configuredRoomId;
            floorNumber = Mathf.Max(1, configuredFloorNumber);
            roomNumber = Mathf.Max(1, configuredRoomNumber);
            contentRoot = configuredContentRoot;
            cameraAnchor = configuredCameraAnchor;
            defaultEntryPoint = configuredDefaultEntryPoint;
            doorways = configuredDoorways ?? Array.Empty<RoomDoorway>();
        }

        public void SetDoorways(RoomDoorway[] configuredDoorways)
        {
            doorways = configuredDoorways ?? Array.Empty<RoomDoorway>();
        }

        public void ApplyGeneratedDefinition(RoomDefinition configuredDefinition)
        {
            definition = configuredDefinition;
        }

        public void ApplyRoomProfile(RoomProfile configuredProfile)
        {
            profile = configuredProfile;
        }

        public void SetVisible(bool visible)
        {
            if (contentRoot != null && contentRoot.activeSelf != visible)
            {
                contentRoot.SetActive(visible);
            }
        }

        public void MarkVisited()
        {
            HasBeenVisited = true;
        }

        public bool HasConnectionTo(RoomNode destination)
        {
            if (destination == null)
            {
                return false;
            }

            foreach (RoomDoorway doorway in doorways)
            {
                if (doorway != null && doorway.Destination == destination)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
