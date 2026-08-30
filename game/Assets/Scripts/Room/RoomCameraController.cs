using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RoomCameraController : MonoBehaviour
    {
        [SerializeField] private CameraFollow followCamera;
        [SerializeField] private Camera roomCamera;

        public Camera RoomCamera => roomCamera;

        public void Configure(CameraFollow configuredFollowCamera)
        {
            followCamera = configuredFollowCamera;
            ApplyLayoutFraming();
        }

        private void Awake() => ApplyLayoutFraming();

        public void ShowRoom(Transform cameraAnchor)
        {
            if (cameraAnchor == null)
            {
                return;
            }

            if (followCamera != null)
            {
                followCamera.enabled = false;
            }

            Vector3 position = cameraAnchor.position;
            position.z = transform.position.z;
            transform.position = position;
        }

        private void ApplyLayoutFraming()
        {
            if (roomCamera == null)
            {
                roomCamera = GetComponent<Camera>();
            }

            if (roomCamera != null && roomCamera.orthographic)
            {
                roomCamera.orthographicSize = RoomLayout.CameraOrthographicSize;
            }
        }
    }
}
