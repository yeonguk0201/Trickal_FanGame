using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [CreateAssetMenu(fileName = "RoomProfile", menuName = "Trickal Fan Game/Room Profile")]
    public sealed class RoomProfile : ScriptableObject
    {
        [SerializeField] private string profileId;
        [SerializeField] private Vector2 interiorSize;
        [SerializeField] private Rect movementBounds;
        [SerializeField] private Rect encounterBounds;
        [SerializeField] private float cameraOrthographicSize;
        [SerializeField] private Rect cameraBounds;
        [SerializeField] private Vector2[] minimapSilhouette = Array.Empty<Vector2>();

        public string ProfileId => profileId;
        public Vector2 InteriorSize => interiorSize;
        public Rect MovementBounds => movementBounds;
        public Rect EncounterBounds => encounterBounds;
        public float CameraOrthographicSize => cameraOrthographicSize;
        public Rect CameraBounds => cameraBounds;
        public IReadOnlyList<Vector2> MinimapSilhouette => minimapSilhouette;

        public bool TryCalculateCameraFrame(
            float aspectRatio,
            out RoomCameraFrame frame,
            out string error)
        {
            return RoomCameraFraming.TryCalculate(
                interiorSize,
                cameraOrthographicSize,
                aspectRatio,
                out frame,
                out error);
        }

        public void Configure(
            string configuredProfileId,
            Vector2 configuredInteriorSize,
            Rect configuredMovementBounds,
            Rect configuredEncounterBounds,
            float configuredCameraOrthographicSize,
            Rect configuredCameraBounds,
            Vector2[] configuredMinimapSilhouette)
        {
            profileId = configuredProfileId;
            interiorSize = configuredInteriorSize;
            movementBounds = configuredMovementBounds;
            encounterBounds = configuredEncounterBounds;
            cameraOrthographicSize = configuredCameraOrthographicSize;
            cameraBounds = configuredCameraBounds;
            minimapSilhouette = configuredMinimapSilhouette ?? Array.Empty<Vector2>();
        }

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(profileId, "Room profile", out error))
            {
                return false;
            }

            if (interiorSize.x <= 0f || interiorSize.y <= 0f)
            {
                error = $"Room profile '{profileId}' needs a positive interior size.";
                return false;
            }

            Rect interiorBounds = new(-interiorSize * 0.5f, interiorSize);
            if (!IsPositive(movementBounds) || !Contains(interiorBounds, movementBounds))
            {
                error = $"Room profile '{profileId}' has invalid movement bounds.";
                return false;
            }

            if (!IsPositive(encounterBounds) || !Contains(movementBounds, encounterBounds))
            {
                error = $"Room profile '{profileId}' has invalid Encounter bounds.";
                return false;
            }

            if (!TryCalculateCameraFrame(
                    RoomCameraFraming.DesignAspectRatio,
                    out RoomCameraFrame designFrame,
                    out error) ||
                cameraBounds.width < 0f || cameraBounds.height < 0f ||
                !Contains(interiorBounds, cameraBounds) ||
                !Approximately(cameraBounds, designFrame.CenterBounds))
            {
                error = $"Room profile '{profileId}' has invalid camera bounds.";
                return false;
            }

            if (minimapSilhouette == null || minimapSilhouette.Length < 3)
            {
                error = $"Room profile '{profileId}' needs at least three minimap silhouette points.";
                return false;
            }

            HashSet<Vector2> uniquePoints = new();
            foreach (Vector2 point in minimapSilhouette)
            {
                if (!Contains(interiorBounds, new Rect(point, Vector2.zero)) || !uniquePoints.Add(point))
                {
                    error = $"Room profile '{profileId}' has an outside or duplicated minimap silhouette point.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool IsPositive(Rect bounds) => bounds.width > 0f && bounds.height > 0f;

        private static bool Contains(Rect outer, Rect inner)
        {
            const float tolerance = 0.0001f;
            return inner.xMin >= outer.xMin - tolerance && inner.xMax <= outer.xMax + tolerance &&
                   inner.yMin >= outer.yMin - tolerance && inner.yMax <= outer.yMax + tolerance;
        }

        private static bool Approximately(Rect first, Rect second)
        {
            return (first.position - second.position).sqrMagnitude < 0.0001f &&
                   (first.size - second.size).sqrMagnitude < 0.0001f;
        }
    }

    internal static class StableRoomId
    {
        public static bool TryValidate(string value, string label, out string error)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"{label} ID is required.";
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if ((character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') && character != '-')
                {
                    error = $"{label} ID '{value}' must use lowercase ASCII letters, digits, or hyphens.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
