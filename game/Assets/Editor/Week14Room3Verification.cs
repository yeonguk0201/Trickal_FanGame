using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Room3Verification
    {
        [MenuItem("Trickal Fan Game/Week 14/Verify Room-3 Small Basic Wide Templates")]
        public static void Verify()
        {
            RoomProfile basicProfile = Load<RoomProfile>(Week14Room1Setup.BasicProfilePath);
            RoomProfile smallProfile = Load<RoomProfile>(Week14Room3Setup.SmallProfilePath);
            RoomProfile wideProfile = Load<RoomProfile>(Week14Room3Setup.WideProfilePath);
            RoomTemplateDefinition basicTemplate = Load<RoomTemplateDefinition>(
                Week14Room1Setup.BasicTemplatePath);
            RoomTemplateDefinition smallTemplate = Load<RoomTemplateDefinition>(
                Week14Room3Setup.SmallTemplatePath);
            RoomTemplateDefinition wideTemplate = Load<RoomTemplateDefinition>(
                Week14Room3Setup.WideTemplatePath);

            RoomProfile[] profiles = { smallProfile, basicProfile, wideProfile };
            RoomTemplateDefinition[] templates = { smallTemplate, basicTemplate, wideTemplate };
            Assert(profiles.All(profile => profile != null) && templates.All(template => template != null),
                "Run Room-3 Setup before verification.");
            Assert(RoomContractCatalog.TryValidate(profiles, templates, out string error), error);

            ValidateProfile(smallProfile, Week14Room3Setup.SmallProfileId,
                Week14Room3Setup.SmallSize, RoomCameraTrackingMode.Fixed);
            ValidateProfile(basicProfile, Week14Room1Setup.BasicProfileId,
                RoomLayout.RoomSize, RoomCameraTrackingMode.Fixed);
            ValidateProfile(wideProfile, Week14Room3Setup.WideProfileId,
                Week14Room3Setup.WideSize, RoomCameraTrackingMode.Horizontal);
            ValidateTemplate(smallTemplate, Week14Room3Setup.SmallTemplateId,
                Week14Room3Setup.SmallPrefabPath, Week14Room3Setup.SmallSize, true);
            ValidateTemplate(basicTemplate, Week14Room1Setup.BasicTemplateId,
                Week8GridFloorSetup.PrefabPath, RoomLayout.RoomSize, false);
            ValidateTemplate(wideTemplate, Week14Room3Setup.WideTemplateId,
                Week14Room3Setup.WidePrefabPath, Week14Room3Setup.WideSize, true);
            ValidateBlockedPassageRejection(smallTemplate);
            Week14Room2Verification.Verify();

            Debug.Log(
                "Week 14 Room-3 verification passed: Small 12x6.75, Basic 16x9, and Wide 24x9 " +
                "Profiles/Templates use stable IDs, valid four-way walls and doors, safe entries, camera bounds, " +
                "Encounter bounds, and hand-authored SpawnPoints outside every required door passage.");
        }

        public static void SetupAndVerifyBatch()
        {
            string basicPrefabGuid = AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath);
            string gameSceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week14Room3Setup.Setup();
            Dictionary<string, string> firstGuids = AssetPaths().ToDictionary(
                path => path,
                AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Week14Room3Setup.Setup();

            foreach (KeyValuePair<string, string> pair in firstGuids)
            {
                Assert(!string.IsNullOrWhiteSpace(pair.Value) &&
                       pair.Value == AssetDatabase.AssetPathToGUID(pair.Key),
                    $"Running Room-3 Setup twice changed or lost the GUID for {pair.Key}.");
            }

            Assert(basicPrefabGuid == AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath) &&
                   gameSceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Room-3 Setup changed the existing Basic Prefab or Game Scene GUID.");
            Verify();
        }

        private static void ValidateProfile(
            RoomProfile profile,
            string expectedId,
            Vector2 expectedSize,
            RoomCameraTrackingMode expectedMode)
        {
            Assert(profile.ProfileId == expectedId && Approximately(profile.InteriorSize, expectedSize),
                $"Room Profile '{expectedId}' has an incorrect stable ID or size.");
            Assert(Approximately(profile.MovementBounds, Week14Room3Setup.MovementBounds(expectedSize)) &&
                   Approximately(profile.EncounterBounds, Week14Room3Setup.EncounterBounds(expectedSize)),
                $"Room Profile '{expectedId}' has incorrect movement or Encounter bounds.");
            Assert(profile.TryCalculateCameraFrame(
                       RoomCameraFraming.DesignAspectRatio,
                       out RoomCameraFrame frame,
                       out string error), error);
            Assert(frame.TrackingMode == expectedMode && Approximately(frame.CenterBounds, profile.CameraBounds),
                $"Room Profile '{expectedId}' has incorrect camera bounds or tracking mode.");
        }

        private static void ValidateTemplate(
            RoomTemplateDefinition template,
            string expectedId,
            string expectedPrefabPath,
            Vector2 roomSize,
            bool normalOnly)
        {
            Assert(template.TemplateId == expectedId &&
                   AssetDatabase.GetAssetPath(template.RoomPrefabAsset) == expectedPrefabPath,
                $"Room Template '{expectedId}' has an incorrect stable ID or Prefab binding.");
            Assert(!normalOnly || template.AllowedRoomTypes.Count == 1 &&
                   template.AllowedRoomTypes[0] == RoomType.Normal,
                $"Room Template '{expectedId}' must initially allow only normal combat rooms.");
            Assert(template.DoorSlots.Count == 4 && template.SpawnPoints.Count >= 3,
                $"Room Template '{expectedId}' must have four doors and at least three SpawnPoints.");

            RoomPrefab prefab = template.RoomPrefabAsset.GetComponent<RoomPrefab>();
            string error = null;
            Assert(prefab != null && prefab.TryValidate(out error),
                error ?? $"Room Template '{expectedId}' has no RoomPrefab component.");
            ValidateWalls(prefab.Node.ContentRoot.transform, roomSize);
            foreach (RoomTemplateDoor door in template.DoorSlots)
            {
                RoomDoorSlot slot = prefab.FindSlot(door.Direction);
                Assert(slot != null && Approximately(slot.transform.localPosition, door.SlotPosition) &&
                       Approximately((Vector2)slot.transform.localPosition +
                                     (Vector2)slot.EntryPoint.localPosition, door.SafeEntryPosition),
                    $"Room Template '{expectedId}' {door.Direction} door or safe entry is misplaced.");
                Assert(template.Profile.MovementBounds.Contains(door.SafeEntryPosition),
                    $"Room Template '{expectedId}' {door.Direction} safe entry is outside movement bounds.");
            }

            foreach (Vector2 spawn in template.SpawnPoints)
            {
                Assert(template.Profile.EncounterBounds.Contains(spawn),
                    $"Room Template '{expectedId}' has a SpawnPoint outside its Encounter bounds.");
                foreach (RoomTemplateDoor door in template.DoorSlots)
                {
                    Assert(!RoomTemplateGeometry.IsInsideRequiredDoorPassage(door, spawn),
                        $"Room Template '{expectedId}' has a SpawnPoint inside its {door.Direction} passage.");
                }
            }
        }

        private static void ValidateWalls(Transform content, Vector2 roomSize)
        {
            float horizontalWallCenter = roomSize.x * 0.5f - RoomLayout.WallThickness * 0.5f;
            float verticalWallCenter = roomSize.y * 0.5f - RoomLayout.WallThickness * 0.5f;
            float horizontalLength = (roomSize.x - RoomLayout.DoorOpeningLength) * 0.5f;
            float verticalLength = (roomSize.y - RoomLayout.DoorOpeningLength) * 0.5f;
            float horizontalCenter = RoomLayout.DoorOpeningLength * 0.5f + horizontalLength * 0.5f;
            float verticalCenter = RoomLayout.DoorOpeningLength * 0.5f + verticalLength * 0.5f;

            AssertWall(content, "Top Left Wall", new Vector2(-horizontalCenter, verticalWallCenter),
                new Vector2(horizontalLength, RoomLayout.WallThickness));
            AssertWall(content, "Top Right Wall", new Vector2(horizontalCenter, verticalWallCenter),
                new Vector2(horizontalLength, RoomLayout.WallThickness));
            AssertWall(content, "Bottom Left Wall", new Vector2(-horizontalCenter, -verticalWallCenter),
                new Vector2(horizontalLength, RoomLayout.WallThickness));
            AssertWall(content, "Bottom Right Wall", new Vector2(horizontalCenter, -verticalWallCenter),
                new Vector2(horizontalLength, RoomLayout.WallThickness));
            AssertWall(content, "Left Upper Wall", new Vector2(-horizontalWallCenter, verticalCenter),
                new Vector2(RoomLayout.WallThickness, verticalLength));
            AssertWall(content, "Left Lower Wall", new Vector2(-horizontalWallCenter, -verticalCenter),
                new Vector2(RoomLayout.WallThickness, verticalLength));
            AssertWall(content, "Right Upper Wall", new Vector2(horizontalWallCenter, verticalCenter),
                new Vector2(RoomLayout.WallThickness, verticalLength));
            AssertWall(content, "Right Lower Wall", new Vector2(horizontalWallCenter, -verticalCenter),
                new Vector2(RoomLayout.WallThickness, verticalLength));
        }

        private static void AssertWall(
            Transform content,
            string wallName,
            Vector2 expectedPosition,
            Vector2 expectedSize)
        {
            Transform wall = content.Find(wallName);
            BoxCollider2D collider = wall != null ? wall.GetComponent<BoxCollider2D>() : null;
            Assert(wall != null && collider != null && Approximately(wall.localPosition, expectedPosition) &&
                   Approximately(Vector2.Scale(collider.size, wall.localScale), expectedSize),
                $"Room-3 wall '{wallName}' does not preserve its opening or boundary.");
        }

        private static void ValidateBlockedPassageRejection(RoomTemplateDefinition source)
        {
            GameObject instance = UnityEngine.Object.Instantiate(source.RoomPrefabAsset);
            RoomTemplateDefinition blocked = ScriptableObject.CreateInstance<RoomTemplateDefinition>();
            try
            {
                RoomPrefab prefab = instance.GetComponent<RoomPrefab>();
                RoomTemplateDoor leftDoor = source.DoorSlots.First(
                    door => door.Direction == RoomDoorDirection.Left);
                Vector2 blockedPoint = (leftDoor.SlotPosition + leftDoor.SafeEntryPosition) * 0.5f;
                prefab.Controller.SpawnPoints[0].position = instance.transform.TransformPoint(blockedPoint);
                Vector2[] spawns = source.SpawnPoints.ToArray();
                spawns[0] = blockedPoint;
                blocked.Configure(
                    "blocked-passage-probe",
                    source.Profile,
                    instance,
                    source.AllowedRoomTypes.ToArray(),
                    source.DoorSlots.ToArray(),
                    spawns);
                Assert(!blocked.TryValidate(out string error) && error.Contains("required door passage"),
                    "A SpawnPoint inside a required door passage must be rejected.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(blocked);
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static IEnumerable<string> AssetPaths()
        {
            yield return Week14Room3Setup.SmallProfilePath;
            yield return Week14Room3Setup.WideProfilePath;
            yield return Week14Room3Setup.SmallTemplatePath;
            yield return Week14Room3Setup.WideTemplatePath;
            yield return Week14Room3Setup.SmallPrefabPath;
            yield return Week14Room3Setup.WidePrefabPath;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);

        private static bool Approximately(Vector2 first, Vector2 second) =>
            (first - second).sqrMagnitude < 0.0001f;

        private static bool Approximately(Rect first, Rect second) =>
            Approximately(first.position, second.position) && Approximately(first.size, second.size);

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
