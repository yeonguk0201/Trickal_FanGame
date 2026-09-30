using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Room1Setup
    {
        public const string ProfileFolder = "Assets/Rooms/Profiles";
        public const string TemplateFolder = "Assets/Rooms/Templates";
        public const string BasicProfilePath = ProfileFolder + "/basic.asset";
        public const string BasicTemplatePath = TemplateFolder + "/basic-standard.asset";
        public const string BasicProfileId = "basic";
        public const string BasicTemplateId = "basic-standard";

        [MenuItem("Trickal Fan Game/Week 14/Setup Room-1 Profile and Template Contract")]
        public static void Setup()
        {
            EnsureFolder(ProfileFolder);
            EnsureFolder(TemplateFolder);

            RoomProfile profile = LoadOrCreate<RoomProfile>(BasicProfilePath);
            profile.Configure(
                BasicProfileId,
                RoomLayout.RoomSize,
                new Rect(-7.5f, -4f, 15f, 8f),
                new Rect(-RoomLayout.EncounterSize * 0.5f, RoomLayout.EncounterSize),
                RoomLayout.CameraOrthographicSize,
                new Rect(Vector2.zero, Vector2.zero),
                new[]
                {
                    new Vector2(-RoomLayout.Width * 0.5f, -RoomLayout.Height * 0.5f),
                    new Vector2(RoomLayout.Width * 0.5f, -RoomLayout.Height * 0.5f),
                    new Vector2(RoomLayout.Width * 0.5f, RoomLayout.Height * 0.5f),
                    new Vector2(-RoomLayout.Width * 0.5f, RoomLayout.Height * 0.5f),
                });
            EditorUtility.SetDirty(profile);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week8GridFloorSetup.PrefabPath);
            RoomTemplateDefinition template = LoadOrCreate<RoomTemplateDefinition>(BasicTemplatePath);
            template.Configure(
                BasicTemplateId,
                profile,
                prefab,
                // Special-4 shops use the Basic room like treasure and secret rooms.
                new[] { RoomType.Normal, RoomType.Reward, RoomType.Boss, RoomType.Shop },
                BuildDoorContracts(),
                new[]
                {
                    RoomLayout.SpawnPosition(0, 3),
                    RoomLayout.SpawnPosition(1, 3),
                    RoomLayout.SpawnPosition(2, 3),
                });
            EditorUtility.SetDirty(template);

            AssetDatabase.SaveAssetIfDirty(profile);
            AssetDatabase.SaveAssetIfDirty(template);
            AssetDatabase.SaveAssets();
            Debug.Log("Week 14 Room-1 ready: Basic Room Profile and Template assets are configured without modifying the existing Prefab or Scene.");
        }

        private static RoomTemplateDoor[] BuildDoorContracts()
        {
            RoomTemplateDoor[] result = new RoomTemplateDoor[4];
            RoomDoorDirection[] directions =
            {
                RoomDoorDirection.Left,
                RoomDoorDirection.Right,
                RoomDoorDirection.Up,
                RoomDoorDirection.Down,
            };
            for (int index = 0; index < directions.Length; index++)
            {
                RoomDoorDirection direction = directions[index];
                Vector2 normal = RoomLayout.Direction(direction);
                result[index] = new RoomTemplateDoor(
                    direction,
                    normal * RoomLayout.TransitionCenter(direction),
                    normal * (RoomLayout.TransitionCenter(direction) - RoomLayout.EntryInsetFromTransition));
            }

            return result;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
