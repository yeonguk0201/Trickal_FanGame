using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Room1Verification
    {
        [MenuItem("Trickal Fan Game/Week 14/Verify Room-1 Profile and Template Contract")]
        public static void Verify()
        {
            RoomProfile profile = AssetDatabase.LoadAssetAtPath<RoomProfile>(Week14Room1Setup.BasicProfilePath);
            RoomTemplateDefinition template = AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(
                Week14Room1Setup.BasicTemplatePath);
            Assert(profile != null && template != null, "Run Room-1 Setup before verification.");
            Assert(RoomContractCatalog.TryValidate(new[] { profile }, new[] { template }, out string error), error);
            ValidateBasicContract(profile, template);
            ValidateFailureContracts(profile, template);
            ValidateRunStateSeparation(profile, template);
            Week14Room0RegressionVerification.Verify();

            Debug.Log(
                "Week 14 Room-1 verification passed: stable profile/template IDs, Basic geometry, movement, " +
                "Encounter, camera, minimap silhouette, RoomType compatibility, four doors, safe entries, " +
                "SpawnPoints, missing/duplicate rejection, Prefab/Scene GUIDs and Run-state separation are valid.");
        }

        public static void SetupAndVerifyBatch()
        {
            string prefabGuid = AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath);
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week14Room1Setup.Setup();
            string profileGuid = AssetDatabase.AssetPathToGUID(Week14Room1Setup.BasicProfilePath);
            string templateGuid = AssetDatabase.AssetPathToGUID(Week14Room1Setup.BasicTemplatePath);
            Week14Room1Setup.Setup();

            Assert(!string.IsNullOrWhiteSpace(profileGuid) && !string.IsNullOrWhiteSpace(templateGuid),
                "Room-1 Setup did not create both contract assets.");
            Assert(profileGuid == AssetDatabase.AssetPathToGUID(Week14Room1Setup.BasicProfilePath) &&
                   templateGuid == AssetDatabase.AssetPathToGUID(Week14Room1Setup.BasicTemplatePath),
                "Running Room-1 Setup twice changed a contract asset GUID.");
            Assert(prefabGuid == AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath) &&
                   sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Room-1 Setup changed the existing Basic Prefab or Game Scene GUID.");
            Verify();
        }

        private static void ValidateBasicContract(RoomProfile profile, RoomTemplateDefinition template)
        {
            Assert(profile.ProfileId == Week14Room1Setup.BasicProfileId &&
                   template.TemplateId == Week14Room1Setup.BasicTemplateId && template.Profile == profile,
                "The Basic profile/template stable IDs or binding changed.");
            Assert(Approximately(profile.InteriorSize, new Vector2(16f, 9f)) &&
                   Approximately(profile.MovementBounds, new Rect(-7.5f, -4f, 15f, 8f)) &&
                   Approximately(profile.EncounterBounds, new Rect(-7.25f, -3.75f, 14.5f, 7.5f)) &&
                   Mathf.Approximately(profile.CameraOrthographicSize, 5.25f) &&
                   Approximately(profile.CameraBounds, new Rect(0f, 0f, 0f, 0f)),
                "The Basic profile bounds changed.");
            Assert(profile.MinimapSilhouette.Count == 4,
                "The Basic minimap silhouette must remain a four-corner rectangle.");
            Vector2[] expectedSilhouette =
            {
                new(-8f, -4.5f), new(8f, -4.5f), new(8f, 4.5f), new(-8f, 4.5f),
            };
            for (int index = 0; index < expectedSilhouette.Length; index++)
            {
                Assert(Approximately(profile.MinimapSilhouette[index], expectedSilhouette[index]),
                    $"Basic minimap silhouette point {index + 1} changed.");
            }

            Assert(AssetDatabase.GetAssetPath(template.RoomPrefabAsset) == Week8GridFloorSetup.PrefabPath &&
                   template.AllowedRoomTypes.Count == 3 &&
                   template.AllowedRoomTypes.Contains(RoomType.Normal) &&
                   template.AllowedRoomTypes.Contains(RoomType.Reward) &&
                   template.AllowedRoomTypes.Contains(RoomType.Boss) &&
                   template.DoorSlots.Count == 4 && template.SpawnPoints.Count >= 3,
                "The Basic template Prefab, RoomType, door, or SpawnPoint contract changed.");
        }

        private static void ValidateFailureContracts(RoomProfile validProfile, RoomTemplateDefinition validTemplate)
        {
            GameObject prefab = validTemplate.RoomPrefabAsset;
            RoomTemplateDoor[] validDoors = validTemplate.DoorSlots.ToArray();
            Vector2[] validSpawns = validTemplate.SpawnPoints.ToArray();
            RoomType[] validTypes = validTemplate.AllowedRoomTypes.ToArray();
            RoomProfile invalidProfile = ScriptableObject.CreateInstance<RoomProfile>();
            RoomProfile duplicateProfile = ScriptableObject.CreateInstance<RoomProfile>();
            RoomTemplateDefinition invalidTemplateId = ScriptableObject.CreateInstance<RoomTemplateDefinition>();
            RoomTemplateDefinition missingProfile = ScriptableObject.CreateInstance<RoomTemplateDefinition>();
            RoomTemplateDefinition missingPrefab = ScriptableObject.CreateInstance<RoomTemplateDefinition>();
            RoomTemplateDefinition duplicateTemplate = ScriptableObject.CreateInstance<RoomTemplateDefinition>();
            try
            {
                invalidProfile.Configure(string.Empty, Vector2.one, new Rect(0f, 0f, 1f, 1f),
                    new Rect(0f, 0f, 1f, 1f), 1f, new Rect(),
                    new[] { Vector2.zero, Vector2.right, Vector2.up });
                Assert(!invalidProfile.TryValidate(out string error) && error.Contains("ID is required"),
                    "A Room Profile with a missing ID must be rejected.");

                duplicateProfile.Configure(validProfile.ProfileId, validProfile.InteriorSize,
                    validProfile.MovementBounds, validProfile.EncounterBounds,
                    validProfile.CameraOrthographicSize, validProfile.CameraBounds,
                    validProfile.MinimapSilhouette.ToArray());
                Assert(!RoomContractCatalog.TryValidate(
                           new[] { validProfile, duplicateProfile }, new[] { validTemplate }, out error) &&
                       error.Contains("duplicates profile ID"),
                    "Duplicate Room Profile IDs must be rejected.");

                invalidTemplateId.Configure(string.Empty, validProfile, prefab,
                    validTypes, validDoors, validSpawns);
                Assert(!invalidTemplateId.TryValidate(out error) && error.Contains("ID is required"),
                    "A Room Template with a missing ID must be rejected.");

                missingProfile.Configure("missing-profile", null, prefab, validTypes, validDoors, validSpawns);
                Assert(!missingProfile.TryValidate(out error) && error.Contains("missing profile"),
                    "A Room Template with a missing profile must be rejected.");

                missingPrefab.Configure("missing-prefab", validProfile, null,
                    validTypes, validDoors, validSpawns);
                Assert(!missingPrefab.TryValidate(out error) && error.Contains("missing RoomPrefab"),
                    "A Room Template with a missing Prefab must be rejected.");

                duplicateTemplate.Configure(validTemplate.TemplateId, validProfile, prefab,
                    validTypes, validDoors, validSpawns);
                Assert(!RoomContractCatalog.TryValidate(
                           new[] { validProfile }, new[] { validTemplate, duplicateTemplate }, out error) &&
                       error.Contains("duplicates template ID"),
                    "Duplicate Room Template IDs must be rejected.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidProfile);
                UnityEngine.Object.DestroyImmediate(duplicateProfile);
                UnityEngine.Object.DestroyImmediate(invalidTemplateId);
                UnityEngine.Object.DestroyImmediate(missingProfile);
                UnityEngine.Object.DestroyImmediate(missingPrefab);
                UnityEngine.Object.DestroyImmediate(duplicateTemplate);
            }
        }

        private static void ValidateRunStateSeparation(RoomProfile profile, RoomTemplateDefinition template)
        {
            Assert(!HasRunStateField(typeof(RoomProfile)) && !HasRunStateField(typeof(RoomTemplateDefinition)),
                "Room contract assets must not serialize mutable RoomRunState.");
            RoomRunState state = new("room-1-state-probe");
            state.MarkCleared();
            state.MarkArtifactClaimed();
            Assert(profile.TryValidate(out string error) && template.TryValidate(out error), error);
            Assert(state.HasVisited && state.IsCleared && state.HasClaimedArtifact,
                "Reading or validating a Room contract must not replace mutable Run state.");
        }

        private static bool HasRunStateField(Type type) => type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(field => field.FieldType == typeof(RoomRunState));

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
