using UnityEngine;

namespace TrickalFanGame.Room
{
    public enum RoomCameraTrackingMode
    {
        Fixed,
        Horizontal,
        Vertical,
        Both,
    }

    public readonly struct RoomCameraFrame
    {
        public RoomCameraFrame(
            Vector2 viewportSize,
            Rect centerBounds,
            RoomCameraTrackingMode trackingMode)
        {
            ViewportSize = viewportSize;
            CenterBounds = centerBounds;
            TrackingMode = trackingMode;
        }

        public Vector2 ViewportSize { get; }
        public Rect CenterBounds { get; }
        public RoomCameraTrackingMode TrackingMode { get; }

        public Vector2 Clamp(Vector2 desiredCenter)
        {
            return new Vector2(
                Mathf.Clamp(desiredCenter.x, CenterBounds.xMin, CenterBounds.xMax),
                Mathf.Clamp(desiredCenter.y, CenterBounds.yMin, CenterBounds.yMax));
        }
    }

    public static class RoomCameraFraming
    {
        public const float DesignAspectRatio = 16f / 9f;
        private const float AxisTolerance = 0.0001f;
        // World units of floor shown beyond the room's outer edge when a room is zoomed to fill the screen.
        public const float FillMargin = 0.25f;

        // A room smaller than the profile's view is zoomed in until it fills the screen (with FillMargin around
        // it), so small rooms leave no wide empty border. Larger rooms keep the profile size and scroll.
        public static float FitOrthographicSize(Vector2 roomSize, float maximumSize, float aspectRatio)
        {
            if (roomSize.x <= 0f || roomSize.y <= 0f || maximumSize <= 0f || aspectRatio <= 0f) return maximumSize;
            float fit = Mathf.Max(roomSize.y * 0.5f + FillMargin, (roomSize.x + FillMargin * 2f) / (2f * aspectRatio));
            return Mathf.Min(maximumSize, fit);
        }

        public static bool TryCalculate(
            Vector2 roomSize,
            float orthographicSize,
            float aspectRatio,
            out RoomCameraFrame frame,
            out string error)
        {
            frame = default;
            if (roomSize.x <= 0f || roomSize.y <= 0f ||
                float.IsNaN(roomSize.x) || float.IsInfinity(roomSize.x) ||
                float.IsNaN(roomSize.y) || float.IsInfinity(roomSize.y))
            {
                error = "Room camera framing requires a positive room size.";
                return false;
            }

            if (orthographicSize <= 0f || aspectRatio <= 0f ||
                float.IsNaN(orthographicSize) || float.IsInfinity(orthographicSize) ||
                float.IsNaN(aspectRatio) || float.IsInfinity(aspectRatio))
            {
                error = "Room camera framing requires a finite positive orthographic size and aspect ratio.";
                return false;
            }

            Vector2 viewportSize = new(orthographicSize * 2f * aspectRatio, orthographicSize * 2f);
            float horizontalDifference = roomSize.x - viewportSize.x;
            float verticalDifference = roomSize.y - viewportSize.y;
            float horizontalTravel = horizontalDifference > AxisTolerance ? horizontalDifference : 0f;
            float verticalTravel = verticalDifference > AxisTolerance ? verticalDifference : 0f;
            Rect centerBounds = new(
                -horizontalTravel * 0.5f,
                -verticalTravel * 0.5f,
                horizontalTravel,
                verticalTravel);

            bool tracksHorizontally = horizontalTravel > 0f;
            bool tracksVertically = verticalTravel > 0f;
            RoomCameraTrackingMode mode = tracksHorizontally
                ? tracksVertically ? RoomCameraTrackingMode.Both : RoomCameraTrackingMode.Horizontal
                : tracksVertically ? RoomCameraTrackingMode.Vertical : RoomCameraTrackingMode.Fixed;

            frame = new RoomCameraFrame(viewportSize, centerBounds, mode);
            error = null;
            return true;
        }
    }
}
