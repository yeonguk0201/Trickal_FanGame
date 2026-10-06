using System;
using System.Collections.Generic;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week19Door1Setup
    {
        public const string RoomPrefabFolder = "Assets/Rooms/Prefabs";

        [MenuItem("Trickal Fan Game/Week 19/Setup Door-1 Doorway Passage")]
        public static void Setup()
        {
            int updatedTriggers = 0;
            foreach (string path in FindRoomPrefabPaths())
            {
                updatedTriggers += ConfigurePrefab(path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Door-1 ready: {updatedTriggers} doorway transition triggers use width " +
                      $"{RoomLayout.TransitionLength} of the {RoomLayout.DoorOpeningLength} opening, and the room " +
                      "graph grants passage invulnerability and blocks the entered doorway briefly.");
        }

        public static IReadOnlyList<string> FindRoomPrefabPaths()
        {
            List<string> paths = new();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RoomPrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<RoomPrefab>() != null)
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        public static Vector2 TransitionSize(RoomDoorDirection direction) =>
            RoomLayout.IsSideDoor(direction)
                ? new Vector2(RoomLayout.TransitionThickness, RoomLayout.TransitionLength)
                : new Vector2(RoomLayout.TransitionLength, RoomLayout.TransitionThickness);

        private static int ConfigurePrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RoomPrefab roomPrefab = root.GetComponent<RoomPrefab>();
                int updated = 0;
                bool changed = false;
                foreach (RoomDoorSlot slot in roomPrefab.DoorSlots)
                {
                    BoxCollider2D trigger = slot != null && slot.Doorway != null
                        ? slot.Doorway.GetComponent<BoxCollider2D>()
                        : null;
                    if (trigger == null)
                    {
                        throw new InvalidOperationException($"Door-1 found a door slot without a transition box in '{path}'.");
                    }

                    Vector2 size = TransitionSize(slot.Direction);
                    if (trigger.size != size || trigger.offset != Vector2.zero || !trigger.isTrigger)
                    {
                        trigger.size = size;
                        trigger.offset = Vector2.zero;
                        trigger.isTrigger = true;
                        changed = true;
                    }

                    updated++;
                }

                if (changed && PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException($"Door-1 could not save Prefab '{path}'.");
                }

                return updated;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
