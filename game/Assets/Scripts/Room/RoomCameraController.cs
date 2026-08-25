using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RoomCameraController : MonoBehaviour
    {
        [SerializeField] private CameraFollow followCamera;

        public void Configure(CameraFollow configuredFollowCamera)
        {
            followCamera = configuredFollowCamera;
        }

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
    }
}
