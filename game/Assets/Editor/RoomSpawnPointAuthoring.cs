using System;
using System.Collections.Generic;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Keeps a room Prefab's "Spawn N" transforms and the RoomController SpawnPoint list in step with an authored
    // point list. Existing transforms are reused in order so their file IDs stay stable; extras beyond the list are
    // removed and missing ones are created next to "Spawn 1".
    public static class RoomSpawnPointAuthoring
    {
        public static string SpawnPointName(int index) => $"Spawn {index + 1}";

        public static void Sync(RoomController controller, IReadOnlyList<Vector2> points)
        {
            if (controller == null || points == null || points.Count == 0 || controller.SpawnPoints.Count == 0 ||
                controller.SpawnPoints[0] == null)
            {
                throw new InvalidOperationException("SpawnPoint authoring requires a controller with Spawn 1.");
            }

            Transform parent = controller.SpawnPoints[0].parent;
            Transform[] result = new Transform[points.Count];
            for (int index = 0; index < points.Count; index++)
            {
                Transform point = index < controller.SpawnPoints.Count ? controller.SpawnPoints[index] : null;
                if (point == null) point = parent.Find(SpawnPointName(index));
                if (point == null)
                {
                    point = new GameObject(SpawnPointName(index)).transform;
                    point.SetParent(parent, false);
                }

                point.localPosition = points[index];
                result[index] = point;
            }

            for (int index = points.Count; index < controller.SpawnPoints.Count; index++)
            {
                if (controller.SpawnPoints[index] != null)
                    UnityEngine.Object.DestroyImmediate(controller.SpawnPoints[index].gameObject, true);
            }

            SerializedObject serialized = new(controller);
            SerializedProperty list = serialized.FindProperty("spawnPoints");
            list.arraySize = result.Length;
            for (int index = 0; index < result.Length; index++)
                list.GetArrayElementAtIndex(index).objectReferenceValue = result[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
