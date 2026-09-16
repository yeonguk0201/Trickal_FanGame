using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Room3Setup
    {
        public const string SmallProfileId = "small";
        public const string WideProfileId = "wide";
        public const string SmallTemplateId = "small-standard";
        public const string WideTemplateId = "wide-standard";
        public const string SmallProfilePath = Week14Room1Setup.ProfileFolder + "/small.asset";
        public const string WideProfilePath = Week14Room1Setup.ProfileFolder + "/wide.asset";
        public const string SmallTemplatePath = Week14Room1Setup.TemplateFolder + "/small-standard.asset";
        public const string WideTemplatePath = Week14Room1Setup.TemplateFolder + "/wide-standard.asset";
        public const string SmallPrefabPath = Week8GridFloorSetup.PrefabFolder + "/room-small-standard.prefab";
        public const string WidePrefabPath = Week8GridFloorSetup.PrefabFolder + "/room-wide-standard.prefab";

        public static readonly Vector2 SmallSize = new(12f, 6.75f);
        public static readonly Vector2 WideSize = new(24f, 9f);

        [MenuItem("Trickal Fan Game/Week 14/Setup Room-3 Small Basic Wide Templates")]
        public static void Setup()
        {
            Week14Room1Setup.Setup();
            GameObject basicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week8GridFloorSetup.PrefabPath);
            if (basicPrefab == null)
            {
                Debug.LogError("Room-3 requires the existing Basic room Prefab.");
                return;
            }

            ConfigureLayout(
                SmallProfileId,
                SmallTemplateId,
                SmallProfilePath,
                SmallTemplatePath,
                SmallPrefabPath,
                "Room Small Standard",
                SmallSize,
                new[] { new Vector2(-3f, 1.5f), new Vector2(3f, -1.5f), Vector2.zero },
                basicPrefab);
            ConfigureLayout(
                WideProfileId,
                WideTemplateId,
                WideProfilePath,
                WideTemplatePath,
                WidePrefabPath,
                "Room Wide Standard",
                WideSize,
                new[] { new Vector2(-6f, 2f), new Vector2(0f, -2f), new Vector2(6f, 2f) },
                basicPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log(
                "Week 14 Room-3 ready: Small 12x6.75, existing Basic 16x9, and Wide 24x9 " +
                "hand-authored Room Profiles, Prefabs, door passages, safe entries, and SpawnPoints are configured.");
        }

        public static RoomTemplateDoor[] BuildDoorContracts(Vector2 roomSize)
        {
            return Enum.GetValues(typeof(RoomDoorDirection))
                .Cast<RoomDoorDirection>()
                .Select(direction =>
                {
                    Vector2 normal = RoomLayout.Direction(direction);
                    float transitionCenter = TransitionCenter(roomSize, direction);
                    return new RoomTemplateDoor(
                        direction,
                        normal * transitionCenter,
                        normal * (transitionCenter - RoomLayout.EntryInsetFromTransition));
                })
                .ToArray();
        }

        public static Rect MovementBounds(Vector2 roomSize) =>
            new(-roomSize * 0.5f + Vector2.one * RoomLayout.WallThickness,
                roomSize - Vector2.one * RoomLayout.WallThickness * 2f);

        public static Rect EncounterBounds(Vector2 roomSize) =>
            new(-roomSize * 0.5f + Vector2.one * 0.75f, roomSize - Vector2.one * 1.5f);

        public static float TransitionCenter(Vector2 roomSize, RoomDoorDirection direction)
        {
            float dimension = RoomLayout.IsSideDoor(direction) ? roomSize.x : roomSize.y;
            return dimension * 0.5f - RoomLayout.WallThickness * 0.5f - RoomLayout.TransitionInset;
        }

        public static void ConfigureLayout(
            string profileId,
            string templateId,
            string profilePath,
            string templatePath,
            string prefabPath,
            string prefabName,
            Vector2 roomSize,
            Vector2[] spawnPoints,
            GameObject basicPrefab,
            RoomType[] allowedRoomTypes = null,
            int minimumFloor = 1,
            int maximumFloor = 99)
        {
            if (!RoomCameraFraming.TryCalculate(
                    roomSize,
                    RoomLayout.CameraOrthographicSize,
                    RoomCameraFraming.DesignAspectRatio,
                    out RoomCameraFrame frame,
                    out string error))
            {
                Debug.LogError($"Room-3 could not calculate {profileId} camera framing. {error}");
                return;
            }

            RoomProfile profile = LoadOrCreate<RoomProfile>(profilePath);
            profile.Configure(
                profileId,
                roomSize,
                MovementBounds(roomSize),
                EncounterBounds(roomSize),
                RoomLayout.CameraOrthographicSize,
                frame.CenterBounds,
                BuildSilhouette(roomSize));
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);

            GameObject prefab = CreateOrUpdatePrefab(
                basicPrefab,
                prefabPath,
                prefabName,
                roomSize,
                spawnPoints);
            if (prefab == null)
            {
                return;
            }

            RoomTemplateDefinition template = LoadOrCreate<RoomTemplateDefinition>(templatePath);
            template.Configure(
                templateId,
                profile,
                prefab,
                allowedRoomTypes ?? new[] { RoomType.Normal },
                BuildDoorContracts(roomSize),
                spawnPoints,
                minimumFloor,
                maximumFloor);
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssetIfDirty(template);
        }

        private static GameObject CreateOrUpdatePrefab(
            GameObject basicPrefab,
            string prefabPath,
            string prefabName,
            Vector2 roomSize,
            Vector2[] spawnPoints)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(basicPrefab));
            try
            {
                root.name = prefabName;
                RoomPrefab roomPrefab = root.GetComponent<RoomPrefab>();
                if (roomPrefab == null || roomPrefab.Node == null || roomPrefab.Controller == null)
                {
                    Debug.LogError($"Room-3 source Prefab is invalid for {prefabName}.");
                    return null;
                }

                Transform content = roomPrefab.Node.ContentRoot.transform;
                RemoveBasicOnlyArtwork(content);
                ConfigureWalls(content, roomSize);
                ConfigureDoors(roomPrefab, roomSize);

                BoxCollider2D encounter = roomPrefab.Controller.GetComponent<BoxCollider2D>();
                if (encounter == null || roomPrefab.Controller.SpawnPoints.Count != spawnPoints.Length)
                {
                    Debug.LogError($"Room-3 source Prefab Encounter is invalid for {prefabName}.");
                    return null;
                }

                encounter.size = EncounterBounds(roomSize).size;
                for (int index = 0; index < spawnPoints.Length; index++)
                {
                    roomPrefab.Controller.SpawnPoints[index].localPosition = spawnPoints[index];
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (saved == null)
                {
                    Debug.LogError($"Room-3 could not save {prefabPath}.");
                }

                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureWalls(Transform content, Vector2 roomSize)
        {
            float horizontalWallCenter = roomSize.x * 0.5f - RoomLayout.WallThickness * 0.5f;
            float verticalWallCenter = roomSize.y * 0.5f - RoomLayout.WallThickness * 0.5f;
            float horizontalSegmentLength = (roomSize.x - RoomLayout.DoorOpeningLength) * 0.5f;
            float verticalSegmentLength = (roomSize.y - RoomLayout.DoorOpeningLength) * 0.5f;
            float horizontalSegmentCenter =
                RoomLayout.DoorOpeningLength * 0.5f + horizontalSegmentLength * 0.5f;
            float verticalSegmentCenter =
                RoomLayout.DoorOpeningLength * 0.5f + verticalSegmentLength * 0.5f;

            SetWall(content, "Top Left Wall",
                new Vector2(-horizontalSegmentCenter, verticalWallCenter),
                new Vector2(horizontalSegmentLength, RoomLayout.WallThickness));
            SetWall(content, "Top Right Wall",
                new Vector2(horizontalSegmentCenter, verticalWallCenter),
                new Vector2(horizontalSegmentLength, RoomLayout.WallThickness));
            SetWall(content, "Bottom Left Wall",
                new Vector2(-horizontalSegmentCenter, -verticalWallCenter),
                new Vector2(horizontalSegmentLength, RoomLayout.WallThickness));
            SetWall(content, "Bottom Right Wall",
                new Vector2(horizontalSegmentCenter, -verticalWallCenter),
                new Vector2(horizontalSegmentLength, RoomLayout.WallThickness));
            SetWall(content, "Left Upper Wall",
                new Vector2(-horizontalWallCenter, verticalSegmentCenter),
                new Vector2(RoomLayout.WallThickness, verticalSegmentLength));
            SetWall(content, "Left Lower Wall",
                new Vector2(-horizontalWallCenter, -verticalSegmentCenter),
                new Vector2(RoomLayout.WallThickness, verticalSegmentLength));
            SetWall(content, "Right Upper Wall",
                new Vector2(horizontalWallCenter, verticalSegmentCenter),
                new Vector2(RoomLayout.WallThickness, verticalSegmentLength));
            SetWall(content, "Right Lower Wall",
                new Vector2(horizontalWallCenter, -verticalSegmentCenter),
                new Vector2(RoomLayout.WallThickness, verticalSegmentLength));
        }

        private static void SetWall(Transform content, string name, Vector2 position, Vector2 size)
        {
            Transform wall = content.Find(name);
            if (wall == null)
            {
                throw new InvalidOperationException($"Room-3 source Prefab is missing wall '{name}'.");
            }

            wall.localPosition = position;
            wall.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = wall.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        private static void RemoveBasicOnlyArtwork(Transform content)
        {
            Transform artwork = content.Find(Week14BasicRoomArtworkSetup.BackgroundObjectName);
            if (artwork != null)
            {
                UnityEngine.Object.DestroyImmediate(artwork.gameObject);
            }
        }

        private static void ConfigureDoors(RoomPrefab prefab, Vector2 roomSize)
        {
            foreach (RoomDoorSlot slot in prefab.DoorSlots)
            {
                Vector2 normal = RoomLayout.Direction(slot.Direction);
                slot.transform.localPosition = normal * TransitionCenter(roomSize, slot.Direction);
                slot.EntryPoint.localPosition = -normal * RoomLayout.EntryInsetFromTransition;
            }
        }

        private static Vector2[] BuildSilhouette(Vector2 roomSize)
        {
            Vector2 half = roomSize * 0.5f;
            return new[]
            {
                new Vector2(-half.x, -half.y), new Vector2(half.x, -half.y),
                new Vector2(half.x, half.y), new Vector2(-half.x, half.y),
            };
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
    }
}
