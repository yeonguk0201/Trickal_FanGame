using System;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week14Room5Setup
    {
        [MenuItem("Trickal Fan Game/Week 14/Setup Room-5 Runtime Room Transitions")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Room-5 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Week14Room4Setup.Setup();
            PlayerMovement player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            Rigidbody2D playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
            RoomCameraController roomCamera = UnityEngine.Object.FindFirstObjectByType<RoomCameraController>();
            Camera camera = roomCamera != null ? roomCamera.RoomCamera : null;
            if (playerBody == null || camera == null)
                throw new InvalidOperationException(
                    "Room-5 requires the configured Player Rigidbody2D and room Camera.");

            Undo.RecordObject(playerBody, "Configure Room-5 player interpolation");
            playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            EditorUtility.SetDirty(playerBody);
            Undo.RecordObject(camera, "Configure opaque room camera clear");
            camera.clearFlags = CameraClearFlags.SolidColor;
            Color background = camera.backgroundColor;
            background.a = 1f;
            camera.backgroundColor = background;
            EditorUtility.SetDirty(camera);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Room-5 setup.");
            Debug.Log(
                "Week 14 Room-5 ready: generated nodes now instantiate their selected Small, Basic, or Wide " +
                "Prefab and drive safe entry placement, profile-aware camera framing, RunProgress, and minimap " +
                "silhouettes. Player Rigidbody interpolation keeps render-frame camera tracking smooth, and " +
                "opaque viewport clearing prevents previous-room pixels from surviving a horizontal camera jump.");
        }
    }
}
