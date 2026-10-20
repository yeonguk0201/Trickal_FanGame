using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Grid-1: every Room Profile's movement bounds become the walkable floor the wall boundaries leave
    // (FairyVillageArtworkSetup.WalkableFloor), and its Encounter bounds become the same floor. Each room
    // Prefab's Encounter trigger follows its profile. Only those values change, so a rerun changes nothing.
    public static class Week27Grid1Setup
    {
        public const string ProfileFolder = Week14Room1Setup.ProfileFolder;
        public const string TemplateFolder = Week14Room1Setup.TemplateFolder;

        [MenuItem("Trickal Fan Game/Week 27/Setup Grid-1 Room Profile Walkable Bounds")]
        public static void Setup()
        {
            int profiles = 0;
            foreach (RoomProfile profile in Profiles())
            {
                Rect movement = Week14Room3Setup.MovementBounds(profile.InteriorSize);
                Rect encounter = Week14Room3Setup.EncounterBounds(profile.InteriorSize);
                if (profile.MovementBounds == movement && profile.EncounterBounds == encounter) continue;
                Undo.RecordObject(profile, "Grid-1 room profile bounds");
                profile.Configure(profile.ProfileId, profile.InteriorSize, movement, encounter,
                    profile.CameraOrthographicSize, profile.CameraBounds, profile.MinimapSilhouette.ToArray());
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
                profiles++;
            }

            int prefabs = 0;
            HashSet<string> visited = new();
            foreach (RoomTemplateDefinition template in Templates())
            {
                if (template.Profile == null || template.RoomPrefabAsset == null) continue;
                string path = AssetDatabase.GetAssetPath(template.RoomPrefabAsset);
                if (!visited.Add(path)) continue;
                if (SyncEncounter(path, template.Profile.EncounterBounds)) prefabs++;
            }

            int moved = Week20Obstacle4Setup.Layouts.Sum(spec => SyncObstacleCells(spec.PrefabPath, spec.Cells));
            AssetDatabase.SaveAssets();
            Debug.Log($"Grid-1 ready: {profiles} Room Profiles and {prefabs} room Prefab Encounter triggers updated " +
                      $"to the walkable floor, and {moved} Obstacle-4 obstacles moved to their cells.");
        }

        public static IEnumerable<RoomProfile> Profiles() =>
            AssetDatabase.FindAssets("t:RoomProfile", new[] { ProfileFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<RoomProfile>)
                .Where(profile => profile != null);

        public static IEnumerable<RoomTemplateDefinition> Templates() =>
            AssetDatabase.FindAssets("t:RoomTemplateDefinition", new[] { TemplateFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>)
                .Where(template => template != null);

        // Obstacle-4 Layouts keep their obstacles where the spec says; Grid-1 moved basic-diamond-cover's outer two
        // against the walls. Rerunning Obstacle-4 setup would rebuild the room from its source Prefab.
        private static int SyncObstacleCells(string prefabPath, IReadOnlyList<Vector2> cells)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform container = root.GetComponent<RoomPrefab>()?.Node?.ContentRoot?.transform
                    .Find(Week18Obstacle2Setup.ObstacleContainerName);
                if (container == null)
                {
                    Debug.LogError($"Grid-1: {prefabPath} has no obstacle container.");
                    return 0;
                }

                int moved = 0;
                for (int index = 0; index < cells.Count; index++)
                {
                    Transform obstacle = container.Find($"Obstacle {Week18Obstacle2Setup.ObstacleId(index)}");
                    if (obstacle == null)
                    {
                        Debug.LogError($"Grid-1: {prefabPath} is missing {Week18Obstacle2Setup.ObstacleId(index)}.");
                        continue;
                    }

                    if (((Vector2)obstacle.localPosition - cells[index]).sqrMagnitude < 1e-8f) continue;
                    obstacle.localPosition = new Vector3(cells[index].x, cells[index].y, obstacle.localPosition.z);
                    moved++;
                }

                if (moved > 0) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return moved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The trigger is sized in its own scale, centered on the room like the profile's bounds.
        private static bool SyncEncounter(string prefabPath, Rect encounterBounds)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                RoomPrefab roomPrefab = root.GetComponent<RoomPrefab>();
                BoxCollider2D encounter = roomPrefab != null && roomPrefab.Controller != null
                    ? roomPrefab.Controller.GetComponent<BoxCollider2D>()
                    : null;
                if (encounter == null)
                {
                    Debug.LogError($"Grid-1: {prefabPath} has no Encounter trigger.");
                    return false;
                }

                Vector3 scale = encounter.transform.lossyScale;
                Vector2 size = new(encounterBounds.width / Mathf.Abs(scale.x),
                    encounterBounds.height / Mathf.Abs(scale.y));
                if ((encounter.size - size).sqrMagnitude < 1e-8f) return false;
                encounter.size = size;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
