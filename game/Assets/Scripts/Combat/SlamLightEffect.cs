using UnityEngine;

namespace TrickalFanGame.Combat
{
    public sealed class SlamLightEffect : MonoBehaviour
    {
        public const float FallSeconds = 0.14f;
        public const float PeakHoldSeconds = 0.7f;
        public const float FadeSeconds = 0.28f;
        public const float Duration = FallSeconds + PeakHoldSeconds + FadeSeconds;
        public const float VerticalOffset = -0.25f;
        public const float RoomInset = 0.1f;
        public static float StrengthAt(float seconds)
        {
            if (seconds < 0f || seconds >= Duration) return 0f;
            if (seconds < FallSeconds) return Mathf.Clamp01(seconds / FallSeconds);
            return seconds <= FallSeconds + PeakHoldSeconds ? 1f :
                1f - (seconds - FallSeconds - PeakHoldSeconds) / FadeSeconds;
        }

        public static SlamLightEffect Play(Vector2 center, float length, float height, float angle,
            Transform source, Renderer sortingSource, Rect bounds, bool reverse = false)
        {
            GameObject owner = LayeredSlamEffect.Create(center, length, height, angle,
                source, sortingSource, bounds, reverse);
            return owner != null ? owner.AddComponent<SlamLightEffect>() : null;
        }

        public static SlamLightEffect PlayPath(LineRenderer warning, float height,
            Transform source, Renderer sortingSource, Rect bounds)
        {
            if (warning == null || warning.positionCount < 2) return null;
            Vector2 start = warning.GetPosition(0), end = warning.GetPosition(1);
            if (!TryGetFittedPathLayout(start, end, height, bounds, out Vector2 center, out float length,
                out float fittedHeight, out float angle, out bool reverse)) return null;
            return Play(center, length, fittedHeight, angle, source, sortingSource, bounds, reverse);
        }

        public static bool TryGetFittedPathLayout(Vector2 start, Vector2 end, float height, Rect bounds,
            out Vector2 center, out float length, out float fittedHeight, out float angle, out bool reverse)
        {
            fittedHeight = 0f;
            if (!TryGetPathLayout(start, end, out center, out length, out angle, out reverse) || height <= 0f)
                return false;
            Vector2 anchor = start + Vector2.up * VerticalOffset;
            Rect room = Rect.MinMaxRect(bounds.xMin+RoomInset, bounds.yMin+RoomInset,
                bounds.xMax-RoomInset, bounds.yMax-RoomInset);
            if (room.width <= 0f || room.height <= 0f || !room.Contains(anchor)) return false;
            // Fit only the ground path. Height stays dramatic even when the wall shortens the path.
            Vector2 direction = (end-start).normalized;
            if (direction.x > 0.00001f) length = Mathf.Min(length,(room.xMax-anchor.x)/direction.x);
            else if (direction.x < -0.00001f) length = Mathf.Min(length,(room.xMin-anchor.x)/direction.x);
            if (direction.y > 0.00001f) length = Mathf.Min(length,(room.yMax-anchor.y)/direction.y);
            else if (direction.y < -0.00001f) length = Mathf.Min(length,(room.yMin-anchor.y)/direction.y);
            fittedHeight = height;
            center = anchor + direction * length * 0.5f;
            return length > 0.001f && fittedHeight > 0.001f;
        }

        public static bool TryGetPathLayout(Vector2 start, Vector2 end, out Vector2 center,
            out float length, out float angle, out bool reverse)
        {
            Vector2 direction = end - start;
            center = (start + end) * 0.5f;
            length = direction.magnitude;
            angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            reverse = true;
            if (angle > 90f) { angle -= 180f; reverse = false; }
            else if (angle < -90f) { angle += 180f; reverse = false; }
            return length > 0.001f;
        }

    }
}
