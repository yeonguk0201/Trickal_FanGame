using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RoomCameraController : MonoBehaviour
    {
        [SerializeField] private CameraFollow followCamera;
        [SerializeField] private Camera roomCamera;
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float followSpeed = 8f;

        private Transform roomAnchor;
        private RoomProfile activeProfile;
        private RoomCameraFrame activeFrame;

        public Camera RoomCamera => roomCamera;
        public RoomCameraTrackingMode TrackingMode => activeProfile != null
            ? activeFrame.TrackingMode
            : RoomCameraTrackingMode.Fixed;
        public Rect ActiveCenterBounds => activeFrame.CenterBounds;

        public void Configure(CameraFollow configuredFollowCamera)
        {
            followCamera = configuredFollowCamera;
            if (followCamera != null)
            {
                target = followCamera.Target;
            }
            ApplyOpaqueFrameClear();
            ApplyLayoutFraming();
        }

        private void Awake()
        {
            ApplyOpaqueFrameClear();
            ApplyLayoutFraming();
        }

        public void ShowRoom(Transform cameraAnchor)
        {
            if (cameraAnchor == null)
            {
                return;
            }

            ApplyOpaqueFrameClear();

            if (followCamera != null)
            {
                followCamera.enabled = false;
            }

            roomAnchor = cameraAnchor;
            activeProfile = null;

            Vector3 position = cameraAnchor.position;
            position.z = transform.position.z;
            transform.position = position;
        }

        public bool ShowRoom(
            Transform cameraAnchor,
            RoomProfile profile,
            Transform followTarget,
            out string error)
        {
            if (cameraAnchor == null || profile == null)
            {
                error = "A room camera anchor and Room Profile are required.";
                return false;
            }

            if (roomCamera == null)
            {
                roomCamera = GetComponent<Camera>();
            }

            if (roomCamera == null || !roomCamera.orthographic)
            {
                error = "Room camera framing requires an orthographic Camera.";
                return false;
            }

            ApplyOpaqueFrameClear();

            if (!profile.TryCalculateCameraFrame(roomCamera.aspect, out RoomCameraFrame frame, out error))
            {
                return false;
            }

            if (followCamera != null)
            {
                followCamera.enabled = false;
            }

            roomAnchor = cameraAnchor;
            activeProfile = profile;
            activeFrame = frame;
            target = followTarget != null ? followTarget : target;
            roomCamera.orthographicSize = profile.CameraOrthographicSize;
            SnapToTarget();
            error = null;
            return true;
        }

        public void SnapToTarget()
        {
            if (roomAnchor == null || activeProfile == null)
            {
                return;
            }

            transform.position = CalculateDesiredWorldPosition();
        }

        private void LateUpdate()
        {
            if (roomAnchor == null || activeProfile == null)
            {
                return;
            }

            Vector3 desiredPosition = CalculateDesiredWorldPosition();
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }

        private Vector3 CalculateDesiredWorldPosition()
        {
            Vector2 desiredLocalCenter = target != null
                ? roomAnchor.InverseTransformPoint(target.position)
                : Vector2.zero;
            Vector2 localCenter = activeFrame.Clamp(desiredLocalCenter);
            Vector3 worldCenter = roomAnchor.TransformPoint(localCenter);
            worldCenter.z = transform.position.z;
            return worldCenter;
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

        private void ApplyOpaqueFrameClear()
        {
            if (roomCamera == null)
            {
                roomCamera = GetComponent<Camera>();
            }

            if (roomCamera == null)
            {
                return;
            }

            roomCamera.clearFlags = CameraClearFlags.SolidColor;
            Color background = roomCamera.backgroundColor;
            background.a = 1f;
            roomCamera.backgroundColor = background;
        }

    }
}
