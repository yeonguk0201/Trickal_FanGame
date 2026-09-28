using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week14Room6Setup
    {
        public const int ContentVersion = 2;
        public const string TallProfileId = "tall";
        public const string LargeProfileId = "large";
        public const string TallTemplateId = "tall-standard";
        public const string LargeTemplateId = "large-standard";
        public const string BossFloor1ProfileId = "boss-floor-1";
        public const string BossFloor2ProfileId = "boss-floor-2";
        public const string BossFloor3ProfileId = "boss-floor-3";
        public const string BossFloor1TemplateId = "boss-floor-1-wide";
        public const string BossFloor2TemplateId = "boss-floor-2-tall";
        public const string BossFloor3TemplateId = "boss-floor-3-large";

        public const string TallProfilePath = Week14Room1Setup.ProfileFolder + "/tall.asset";
        public const string LargeProfilePath = Week14Room1Setup.ProfileFolder + "/large.asset";
        public const string TallTemplatePath = Week14Room1Setup.TemplateFolder + "/tall-standard.asset";
        public const string LargeTemplatePath = Week14Room1Setup.TemplateFolder + "/large-standard.asset";
        public const string TallPrefabPath = Week8GridFloorSetup.PrefabFolder + "/room-tall-standard.prefab";
        public const string LargePrefabPath = Week8GridFloorSetup.PrefabFolder + "/room-large-standard.prefab";

        public const string BossFloor1ProfilePath = Week14Room1Setup.ProfileFolder + "/boss-floor-1.asset";
        public const string BossFloor2ProfilePath = Week14Room1Setup.ProfileFolder + "/boss-floor-2.asset";
        public const string BossFloor3ProfilePath = Week14Room1Setup.ProfileFolder + "/boss-floor-3.asset";
        public const string BossFloor1TemplatePath = Week14Room1Setup.TemplateFolder + "/boss-floor-1-wide.asset";
        public const string BossFloor2TemplatePath = Week14Room1Setup.TemplateFolder + "/boss-floor-2-tall.asset";
        public const string BossFloor3TemplatePath = Week14Room1Setup.TemplateFolder + "/boss-floor-3-large.asset";
        public const string BossFloor1PrefabPath = Week8GridFloorSetup.PrefabFolder + "/room-boss-floor-1-wide.prefab";
        public const string BossFloor2PrefabPath = Week8GridFloorSetup.PrefabFolder + "/room-boss-floor-2-tall.prefab";
        public const string BossFloor3PrefabPath = Week8GridFloorSetup.PrefabFolder + "/room-boss-floor-3-large.prefab";

        public static readonly Vector2 TallSize = new(16f, 13.5f);
        public static readonly Vector2 LargeSize = new(24f, 13.5f);

        [MenuItem("Trickal Fan Game/Week 14/Setup Room-6 Tall Large and Boss Profiles")]
        public static void Setup()
        {
            Week14Encounter3Setup.Setup();
            GameObject basicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week8GridFloorSetup.PrefabPath);
            if (basicPrefab == null)
                throw new InvalidOperationException("Room-6 requires the verified Basic room Prefab.");

            ConfigureNormalLayouts(basicPrefab);
            ConfigureBossLayouts(basicPrefab);
            AssetDatabase.SaveAssets();

            RoomProfile[] encounterProfiles =
            {
                Load<RoomProfile>(Week14Room3Setup.SmallProfilePath),
                Load<RoomProfile>(Week14Room1Setup.BasicProfilePath),
                Load<RoomProfile>(Week14Room3Setup.WideProfilePath),
                Load<RoomProfile>(TallProfilePath),
                Load<RoomProfile>(LargeProfilePath),
            };
            RoomTemplateDefinition[] templates = TemplatePaths()
                .Select(Load<RoomTemplateDefinition>)
                .ToArray();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null || encounterProfiles.Any(profile => profile == null) ||
                templates.Any(template => template == null))
                throw new InvalidOperationException("Room-6 could not load its generated profiles, templates, or generator.");

            foreach (EncounterDefinition definition in generator.EncounterDefinitions)
            {
                definition.Configure(definition.EncounterId, encounterProfiles,
                    definition.MinimumFloor, definition.MaximumFloor,
                    definition.MinimumPlayerDistance, definition.MinimumDoorDistance,
                    definition.Waves.ToArray(), definition.ClearCondition);
                EditorUtility.SetDirty(definition);
            }

            Undo.RecordObject(generator, "Configure Room-6 expanded template catalog");
            // Keep Layouts that later setups registered (pillar, obstacle Layouts) and never lower the version.
            templates = templates
                .Concat(generator.RoomTemplates.Where(existing => existing != null &&
                    templates.All(template => template.TemplateId != existing.TemplateId)))
                .ToArray();
            generator.ConfigureTemplates(Math.Max(ContentVersion, generator.RoomContentVersion), templates);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Room-6 setup.");
            AssetDatabase.SaveAssets();
            Debug.Log("Week 14 Room-6 ready: Tall and Large normal rooms plus floor-specific basic, tall, " +
                      "and large boss room profile candidates use the shared camera, door, spawn, and spacing contracts.");
        }

        private static void ConfigureNormalLayouts(GameObject basicPrefab)
        {
            Week14Room3Setup.ConfigureLayout(TallProfileId, TallTemplateId,
                TallProfilePath, TallTemplatePath, TallPrefabPath, "Room Tall Standard", TallSize,
                new[] { new Vector2(-3f, 3.5f), new Vector2(3f, -3.5f), Vector2.zero }, basicPrefab);
            Week14Room3Setup.ConfigureLayout(LargeProfileId, LargeTemplateId,
                LargeProfilePath, LargeTemplatePath, LargePrefabPath, "Room Large Standard", LargeSize,
                new[] { new Vector2(-6f, 3.5f), new Vector2(0f, -3.5f), new Vector2(6f, 3.5f) }, basicPrefab);
        }

        private static void ConfigureBossLayouts(GameObject basicPrefab)
        {
            RoomType[] bossOnly = { RoomType.Boss };
            Week14Room3Setup.ConfigureLayout(BossFloor1ProfileId, BossFloor1TemplateId,
                BossFloor1ProfilePath, BossFloor1TemplatePath, BossFloor1PrefabPath,
                "Room Boss Floor 1 Basic Size", RoomLayout.RoomSize,
                new[]
                {
                    RoomLayout.SpawnPosition(0, 3),
                    RoomLayout.SpawnPosition(1, 3),
                    RoomLayout.SpawnPosition(2, 3),
                },
                basicPrefab, bossOnly, 1, 1);
            Week14Room3Setup.ConfigureLayout(BossFloor2ProfileId, BossFloor2TemplateId,
                BossFloor2ProfilePath, BossFloor2TemplatePath, BossFloor2PrefabPath,
                "Room Boss Floor 2 Tall", TallSize,
                new[] { new Vector2(-3f, 3.5f), Vector2.zero, new Vector2(3f, -3.5f) },
                basicPrefab, bossOnly, 2, 2);
            Week14Room3Setup.ConfigureLayout(BossFloor3ProfileId, BossFloor3TemplateId,
                BossFloor3ProfilePath, BossFloor3TemplatePath, BossFloor3PrefabPath,
                "Room Boss Floor 3 Large", LargeSize,
                new[] { new Vector2(-6f, 3.5f), Vector2.zero, new Vector2(6f, -3.5f) },
                basicPrefab, bossOnly, 3, 3);
        }

        public static string[] TemplatePaths() => new[]
        {
            Week14Room1Setup.BasicTemplatePath,
            Week14Room3Setup.SmallTemplatePath,
            Week14Room3Setup.WideTemplatePath,
            TallTemplatePath,
            LargeTemplatePath,
            BossFloor1TemplatePath,
            BossFloor2TemplatePath,
            BossFloor3TemplatePath,
        };

        public static string[] CreatedAssetPaths() => new[]
        {
            TallProfilePath, LargeProfilePath, TallTemplatePath, LargeTemplatePath,
            TallPrefabPath, LargePrefabPath,
            BossFloor1ProfilePath, BossFloor2ProfilePath, BossFloor3ProfilePath,
            BossFloor1TemplatePath, BossFloor2TemplatePath, BossFloor3TemplatePath,
            BossFloor1PrefabPath, BossFloor2PrefabPath, BossFloor3PrefabPath,
        };

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
