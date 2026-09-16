using System;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Room2Verification
    {
        private const float OrthographicSize = 5.25f;

        [MenuItem("Trickal Fan Game/Week 14/Verify Room-2 Axis Camera Bounds")]
        public static void Verify()
        {
            ValidateFrame(new Vector2(12f, 6f), RoomCameraTrackingMode.Fixed, Vector2.zero);
            ValidateFrame(new Vector2(16f, 9f), RoomCameraTrackingMode.Fixed, Vector2.zero);
            ValidateFrame(
                new Vector2(OrthographicSize * 2f * RoomCameraFraming.DesignAspectRatio, OrthographicSize * 2f),
                RoomCameraTrackingMode.Fixed,
                Vector2.zero);
            ValidateFrame(new Vector2(24f, 9f), RoomCameraTrackingMode.Horizontal,
                new Vector2((24f - OrthographicSize * 2f * RoomCameraFraming.DesignAspectRatio) * 0.5f, 0f));
            ValidateFrame(new Vector2(16f, 15f), RoomCameraTrackingMode.Vertical,
                new Vector2(0f, (15f - OrthographicSize * 2f) * 0.5f));
            ValidateFrame(new Vector2(24f, 15f), RoomCameraTrackingMode.Both,
                new Vector2(
                    (24f - OrthographicSize * 2f * RoomCameraFraming.DesignAspectRatio) * 0.5f,
                    (15f - OrthographicSize * 2f) * 0.5f));
            ValidateInvalidInputs();
            ValidateRuntimeController();
            Week14Room1Verification.Verify();

            Debug.Log(
                "Week 14 Room-2 verification passed: Small/Basic cameras stay fixed, Wide/Tall/Large " +
                "track only their available axes, extreme target positions are clamped inside room bounds, " +
                "invalid inputs are rejected, and the runtime controller uses the common framing rule.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week14Room1Setup.Setup();
            Verify();
        }

        private static void ValidateFrame(
            Vector2 roomSize,
            RoomCameraTrackingMode expectedMode,
            Vector2 expectedExtents)
        {
            Assert(RoomCameraFraming.TryCalculate(
                roomSize,
                OrthographicSize,
                RoomCameraFraming.DesignAspectRatio,
                out RoomCameraFrame frame,
                out string error), error);
            Assert(frame.TrackingMode == expectedMode,
                $"Room {roomSize} expected {expectedMode} tracking but got {frame.TrackingMode}.");
            Assert(Approximately(frame.CenterBounds.center, Vector2.zero) &&
                   Approximately(frame.CenterBounds.min, -expectedExtents) &&
                   Approximately(frame.CenterBounds.max, expectedExtents),
                $"Room {roomSize} produced incorrect camera center bounds {frame.CenterBounds}.");

            Vector2 low = frame.Clamp(new Vector2(-1000f, -1000f));
            Vector2 high = frame.Clamp(new Vector2(1000f, 1000f));
            Assert(Approximately(low, -expectedExtents) && Approximately(high, expectedExtents),
                $"Room {roomSize} did not clamp extreme target positions.");
            AssertTrackedAxesStayInside(roomSize, frame, low);
            AssertTrackedAxesStayInside(roomSize, frame, high);
        }

        private static void AssertTrackedAxesStayInside(
            Vector2 roomSize,
            RoomCameraFrame frame,
            Vector2 cameraCenter)
        {
            const float tolerance = 0.0001f;
            Vector2 halfViewport = frame.ViewportSize * 0.5f;
            if (frame.CenterBounds.width > 0f)
            {
                Assert(cameraCenter.x - halfViewport.x >= -roomSize.x * 0.5f - tolerance &&
                       cameraCenter.x + halfViewport.x <= roomSize.x * 0.5f + tolerance,
                    "A tracked horizontal extreme exposes space outside the room.");
            }

            if (frame.CenterBounds.height > 0f)
            {
                Assert(cameraCenter.y - halfViewport.y >= -roomSize.y * 0.5f - tolerance &&
                       cameraCenter.y + halfViewport.y <= roomSize.y * 0.5f + tolerance,
                    "A tracked vertical extreme exposes space outside the room.");
            }
        }

        private static void ValidateInvalidInputs()
        {
            Assert(!RoomCameraFraming.TryCalculate(
                       Vector2.zero,
                       OrthographicSize,
                       RoomCameraFraming.DesignAspectRatio,
                       out _,
                       out string error) && error.Contains("positive room size"),
                "A zero-sized room must be rejected.");
            Assert(!RoomCameraFraming.TryCalculate(
                       Vector2.one,
                       0f,
                       RoomCameraFraming.DesignAspectRatio,
                       out _,
                       out error) && error.Contains("orthographic size"),
                "A zero orthographic size must be rejected.");
            Assert(!RoomCameraFraming.TryCalculate(
                       Vector2.one,
                       OrthographicSize,
                       0f,
                       out _,
                       out error) && error.Contains("aspect ratio"),
                "A zero aspect ratio must be rejected.");
        }

        private static void ValidateRuntimeController()
        {
            GameObject cameraObject = new("Room-2 Camera");
            GameObject anchorObject = new("Room-2 Anchor");
            GameObject targetObject = new("Room-2 Target");
            RoomProfile wideProfile = ScriptableObject.CreateInstance<RoomProfile>();
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = RoomCameraFraming.DesignAspectRatio;
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                RoomCameraController controller = cameraObject.AddComponent<RoomCameraController>();
                anchorObject.transform.position = new Vector3(30f, -12f, 0f);
                targetObject.transform.position = new Vector3(1000f, 1000f, 0f);

                Assert(RoomCameraFraming.TryCalculate(
                    new Vector2(24f, 9f),
                    OrthographicSize,
                    RoomCameraFraming.DesignAspectRatio,
                    out RoomCameraFrame frame,
                    out string error), error);
                wideProfile.Configure(
                    "wide-verification",
                    new Vector2(24f, 9f),
                    new Rect(-11.5f, -4f, 23f, 8f),
                    new Rect(-11f, -3.75f, 22f, 7.5f),
                    OrthographicSize,
                    frame.CenterBounds,
                    new[]
                    {
                        new Vector2(-12f, -4.5f), new Vector2(12f, -4.5f),
                        new Vector2(12f, 4.5f), new Vector2(-12f, 4.5f),
                    });
                Assert(wideProfile.TryValidate(out error), error);
                Assert(controller.ShowRoom(
                    anchorObject.transform,
                    wideProfile,
                    targetObject.transform,
                    out error), error);
                Assert(controller.TrackingMode == RoomCameraTrackingMode.Horizontal &&
                       Mathf.Approximately(camera.orthographicSize, OrthographicSize),
                    "The runtime controller did not apply the Wide profile framing.");
                Vector2 expected = (Vector2)anchorObject.transform.position + frame.CenterBounds.max;
                Assert(Approximately(cameraObject.transform.position, expected) &&
                       Mathf.Approximately(cameraObject.transform.position.z, -10f),
                    "The runtime controller did not clamp the target in room-local space.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wideProfile);
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(anchorObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static bool Approximately(Vector2 first, Vector2 second) =>
            (first - second).sqrMagnitude < 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
