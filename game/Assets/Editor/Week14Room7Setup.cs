using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week14Room7Setup
    {
        public const int RoomContentVersion = 3;
        public const int EncounterContentVersion = 4;
        public const string TemplateId = "large-central-pillar";
        public const string EncounterId = "pillar-crossfire";
        public const string ObstacleId = "central-pillar";
        public const string PillarObjectName = "Central Pillar";
        public const string TemplatePath = Week14Room1Setup.TemplateFolder + "/large-central-pillar.asset";
        public const string PrefabPath = Week8GridFloorSetup.PrefabFolder + "/room-large-central-pillar.prefab";
        public const string EncounterPath = Week14Encounter1Setup.EncounterFolder + "/pillar-crossfire.asset";
        public static readonly Vector2 PillarSize = new(3f, 3f);

        private static readonly Vector2[] SpawnPoints =
        {
            new(-6f, 2.75f),
            new(6f, 2.75f),
            new(0f, -2.75f),
        };

        [MenuItem("Trickal Fan Game/Week 14/Setup Room-7 Fixed Pillar Layout")]
        public static void Setup()
        {
            Week14Room6Setup.Setup();
            GameObject largePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week14Room6Setup.LargePrefabPath);
            if (largePrefab == null)
                throw new InvalidOperationException("Room-7 requires the verified Large room Prefab.");

            Week14Room3Setup.ConfigureLayout(
                Week14Room6Setup.LargeProfileId,
                TemplateId,
                Week14Room6Setup.LargeProfilePath,
                TemplatePath,
                PrefabPath,
                "Room Large Central Pillar",
                Week14Room6Setup.LargeSize,
                SpawnPoints,
                largePrefab);
            ConfigurePillarPrefab();

            RoomProfile largeProfile = Load<RoomProfile>(Week14Room6Setup.LargeProfilePath);
            EncounterDefinition pillarEncounter = LoadOrCreate<EncounterDefinition>(EncounterPath);
            pillarEncounter.Configure(EncounterId, new[] { largeProfile }, 1, 3,
                Week14Encounter1Setup.MinimumPlayerDistance,
                Week14Encounter1Setup.MinimumDoorDistance,
                new[]
                {
                    new EncounterWaveDefinition(1, EncounterWaveStartCondition.RoomEntered,
                        EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated, new[]
                        {
                            new EncounterSpawnRule(EncounterEnemyRole.Chaser, 1, null, "all"),
                            new EncounterSpawnRule(EncounterEnemyRole.Ranged, 1, null, "all"),
                            new EncounterSpawnRule(EncounterEnemyRole.Charging, 1, null, "all"),
                        }),
                }, EncounterClearCondition.AllWavesCleared);
            EditorUtility.SetDirty(pillarEncounter);

            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null || largeProfile == null)
                throw new InvalidOperationException("Room-7 could not load its profile or generator.");

            RoomTemplateDefinition[] templates = Week14Room6Setup.TemplatePaths()
                .Append(TemplatePath)
                .Select(Load<RoomTemplateDefinition>)
                .ToArray();
            EncounterDefinition[] encounters = generator.EncounterDefinitions
                .Append(pillarEncounter)
                .GroupBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();
            if (templates.Any(template => template == null) || encounters.Length != 5)
                throw new InvalidOperationException("Room-7 requires nine Room Templates and five Encounters.");

            Undo.RecordObject(generator, "Configure Room-7 pillar content catalogs");
            generator.ConfigureTemplates(RoomContentVersion, templates);
            generator.ConfigureEncounters(EncounterContentVersion, encounters);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Room-7 setup.");
            AssetDatabase.SaveAssets();
            Debug.Log("Week 14 Room-7 ready: one fixed, indestructible, no-drop central pillar Layout and a " +
                      "Large-profile chaser/ranged/charging Encounter are registered without procedural placement.");
        }

        private static void ConfigurePillarPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                RoomPrefab room = root.GetComponent<RoomPrefab>();
                if (room == null || room.Node == null || room.Node.ContentRoot == null)
                    throw new InvalidOperationException("Room-7 pillar Prefab is missing its room content root.");

                Transform content = room.Node.ContentRoot.transform;
                Transform existing = content.Find(PillarObjectName);
                GameObject pillar = existing != null ? existing.gameObject : new GameObject(PillarObjectName);
                pillar.transform.SetParent(content, false);
                pillar.transform.localPosition = Vector3.zero;
                pillar.transform.localRotation = Quaternion.identity;
                pillar.transform.localScale = new Vector3(PillarSize.x, PillarSize.y, 1f);
                pillar.layer = LayerMask.NameToLayer("Environment");

                SpriteRenderer renderer = pillar.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = pillar.AddComponent<SpriteRenderer>();
                SpriteRenderer wallRenderer = content.Find("Top Left Wall")?.GetComponent<SpriteRenderer>();
                renderer.sprite = wallRenderer != null ? wallRenderer.sprite : null;
                renderer.color = new Color(0.32f, 0.28f, 0.38f, 1f);
                renderer.sortingOrder = 1;

                BoxCollider2D collider = pillar.GetComponent<BoxCollider2D>();
                if (collider == null) collider = pillar.AddComponent<BoxCollider2D>();
                collider.isTrigger = false;
                collider.offset = Vector2.zero;
                collider.size = Vector2.one;

                RoomStaticObstacle obstacle = pillar.GetComponent<RoomStaticObstacle>();
                if (obstacle == null) obstacle = pillar.AddComponent<RoomStaticObstacle>();
                obstacle.Configure(ObstacleId);
                if (!obstacle.TryValidate(out string error))
                    throw new InvalidOperationException(error);

                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Room-7 could not save its pillar Prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static string[] CreatedAssetPaths() => new[] { TemplatePath, PrefabPath, EncounterPath };

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = Load<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
