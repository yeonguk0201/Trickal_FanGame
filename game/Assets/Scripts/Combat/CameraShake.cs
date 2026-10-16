using UnityEngine;

namespace TrickalFanGame.Combat
{
    // A weak, short shake added on top of whatever places the camera. The offset is taken back off before the
    // camera movers run, so their smoothing never follows the shake.
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class CameraShake : MonoBehaviour
    {
        public const float Amplitude = 0.06f;
        public const float Duration = 0.14f;

        private Vector3 offset;
        private Vector3 shakenPosition;
        private float startedAt = float.NegativeInfinity;

        public static void Play()
        {
            if (!Application.isPlaying) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            if (!camera.TryGetComponent(out CameraShake shake)) shake = camera.gameObject.AddComponent<CameraShake>();
            shake.startedAt = Time.unscaledTime;
        }

        // seconds since the shake began. Deterministic, strongest at the start and zero from Duration on.
        public static Vector2 OffsetAt(float seconds)
        {
            if (seconds < 0f || seconds >= Duration) return Vector2.zero;
            float strength = Amplitude * 0.7f * (1f - seconds / Duration);
            return new Vector2(Mathf.Cos(seconds * 95f), Mathf.Sin(seconds * 131f + 1.3f)) * strength;
        }

        private void Update() => RemoveOffset();

        private void LateUpdate()
        {
            RemoveOffset();
            // Unscaled, so the shake keeps moving through the hit stop.
            offset = OffsetAt(Time.unscaledTime - startedAt);
            if (offset == Vector3.zero) return;
            transform.position += offset;
            shakenPosition = transform.position;
        }

        private void OnDisable() => RemoveOffset();

        private void RemoveOffset()
        {
            // If something else placed the camera since the shake wrote it, that position is already clean.
            if (offset != Vector3.zero && transform.position == shakenPosition) transform.position -= offset;
            offset = Vector3.zero;
        }
    }
}
