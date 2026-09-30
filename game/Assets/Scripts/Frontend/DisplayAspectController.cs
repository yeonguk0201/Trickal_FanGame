using UnityEngine;

namespace TrickalFanGame.Frontend
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class DisplayAspectController : MonoBehaviour
    {
        public const float TargetAspect = 16f / 9f;

        private Camera targetCamera;
        private int lastWidth = -1;
        private int lastHeight = -1;

        public Camera TargetCamera => targetCamera != null ? targetCamera : targetCamera = GetComponent<Camera>();

        private void OnEnable() => ApplyViewport();

        private void LateUpdate()
        {
            if (lastWidth != Screen.width || lastHeight != Screen.height)
                ApplyViewport();
        }

        public void ApplyViewport()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            TargetCamera.rect = CalculateViewport(lastWidth, lastHeight);
        }

        public static Rect CalculateViewport(int width, int height)
        {
            if (width <= 0 || height <= 0) return new Rect(0f, 0f, 1f, 1f);

            float currentAspect = (float)width / height;
            if (Mathf.Approximately(currentAspect, TargetAspect))
                return new Rect(0f, 0f, 1f, 1f);

            if (currentAspect > TargetAspect)
            {
                float normalizedWidth = TargetAspect / currentAspect;
                return new Rect((1f - normalizedWidth) * 0.5f, 0f, normalizedWidth, 1f);
            }

            float normalizedHeight = currentAspect / TargetAspect;
            return new Rect(0f, (1f - normalizedHeight) * 0.5f, 1f, normalizedHeight);
        }
    }
}
