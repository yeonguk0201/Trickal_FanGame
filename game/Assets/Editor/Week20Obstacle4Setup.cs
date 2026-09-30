using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week20Obstacle4Setup
    {
        public const int RoomContentVersion = 5;
        public const float SpecialRoomChance = 0.4f;
        public const float MarieDropChance = 0.2f;
        public const string VariantFolder = "Assets/Rooms/ObstacleVariants";
        public const string MarieDropTablePath = Week17Resource3Setup.DropTableFolder +
                                                 "/obstacle-marie-bomb-box-drop-table.asset";
        public const string MarieVariantPath = VariantFolder + "/marie-bomb-box.asset";
        public const string FairyKingdomVariantTablePath = VariantFolder +
                                                           "/fairy-kingdom-obstacle-variants.asset";

        public static readonly (string dropId, int weight)[] MarieDropWeights =
        {
            ("bomb", 60), ("heart", 10), ("sp", 10), ("elif", 10), ("key", 8), ("pit", 2),
        };

        public sealed class LayoutSpec
        {
            public LayoutSpec(string templateId, string prefabName, string profileId, string profilePath,
                Vector2 roomSize, string sourcePrefabPath, Vector2[] spawnPoints, Vector2[] cells)
            {
                TemplateId = templateId;
                PrefabName = prefabName;
                ProfileId = profileId;
                ProfilePath = profilePath;
                RoomSize = roomSize;
                SourcePrefabPath = sourcePrefabPath;
                SpawnPoints = spawnPoints;
                Cells = cells;
            }

            public string TemplateId { get; }
            public string PrefabName { get; }
            public string ProfileId { get; }
            public string ProfilePath { get; }
            public Vector2 RoomSize { get; }
            public string SourcePrefabPath { get; }
            public Vector2[] SpawnPoints { get; }
            public Vector2[] Cells { get; }
            public string TemplatePath => $"{Week14Room1Setup.TemplateFolder}/{TemplateId}.asset";
            public string PrefabPath => $"{Week8GridFloorSetup.PrefabFolder}/room-{TemplateId}.prefab";
        }

        public static readonly LayoutSpec SmallOffsetCover = new(
            "small-offset-cover", "Room Small Offset Cover",
            Week14Room3Setup.SmallProfileId, Week14Room3Setup.SmallProfilePath, Week14Room3Setup.SmallSize,
            Week14Room3Setup.SmallPrefabPath,
            new[] { new Vector2(-3f, 1.5f), new Vector2(3f, -1.5f), Vector2.zero },
            new[]
            {
                new Vector2(-2.5f, 0.5f), new Vector2(-2.5f, -0.5f),
                new Vector2(2.5f, 0.5f), new Vector2(2.5f, -0.5f),
            });

        public static readonly LayoutSpec BasicDiamondCover = new(
            "basic-diamond-cover", "Room Basic Diamond Cover",
            Week14Room1Setup.BasicProfileId, Week14Room1Setup.BasicProfilePath, RoomLayout.RoomSize,
            Week8GridFloorSetup.PrefabPath,
            new[] { new Vector2(-4f, 2f), new Vector2(4f, -2f), Vector2.zero },
            new[]
            {
                new Vector2(-3.5f, 0.5f), new Vector2(-2.5f, 2.5f),
                new Vector2(3.5f, -0.5f), new Vector2(2.5f, -2.5f),
            });

        public static readonly LayoutSpec WideTwinCover = new(
            "wide-twin-cover", "Room Wide Twin Cover",
            Week14Room3Setup.WideProfileId, Week14Room3Setup.WideProfilePath, Week14Room3Setup.WideSize,
            Week14Room3Setup.WidePrefabPath,
            new[] { new Vector2(-8f, 2f), new Vector2(8f, -2f), Vector2.zero },
            Block(-5f, 0f, 2, 2).Concat(Block(5f, 0f, 2, 2)).ToArray());

        public static readonly LayoutSpec TallSidePockets = new(
            "tall-side-pockets", "Room Tall Side Pockets",
            Week14Room6Setup.TallProfileId, Week14Room6Setup.TallProfilePath, Week14Room6Setup.TallSize,
            Week14Room6Setup.TallPrefabPath,
            new[] { new Vector2(-5f, 4f), new Vector2(5f, -4f), Vector2.zero },
            Block(-3f, 2.5f, 2, 2).Concat(Block(3f, -2.5f, 2, 2)).ToArray());

        public static readonly LayoutSpec[] Layouts =
        {
            SmallOffsetCover, BasicDiamondCover, WideTwinCover, TallSidePockets,
        };

        [MenuItem("Trickal Fan Game/Week 20/Setup Obstacle-4 Fairy Kingdom Obstacles and Layouts")]
        public static void Setup()
        {
            Week18Obstacle2Setup.OpenGameScene();
            DestructibleObstacle obstaclePrefab = Week18Obstacle1Setup.EnsurePrefab();
            ObstacleVariantTable variantTable = EnsureVariantAssets();

            List<RoomTemplateDefinition> added = new();
            foreach (LayoutSpec spec in Layouts)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(spec.SourcePrefabPath);
                if (source == null)
                    throw new InvalidOperationException($"Obstacle-4 is missing source Prefab {spec.SourcePrefabPath}.");

                Week14Room3Setup.ConfigureLayout(spec.ProfileId, spec.TemplateId, spec.ProfilePath,
                    spec.TemplatePath, spec.PrefabPath, spec.PrefabName, spec.RoomSize, spec.SpawnPoints, source);
                PlaceObstacles(spec.PrefabPath, spec.Cells, obstaclePrefab.gameObject, variantTable);
                RoomTemplateDefinition template = AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(spec.TemplatePath);
                template.ConfigureLayoutDifficultyModifier(1);
                EditorUtility.SetDirty(template);
                if (!template.TryValidate(out string error) || !template.TryValidateLayout(out error))
                    throw new InvalidOperationException($"Obstacle-4 Layout '{spec.TemplateId}' is invalid. {error}");
                added.Add(template);
            }

            foreach (Week18Obstacle2Setup.LayoutSpec legacy in Week18Obstacle2Setup.Layouts)
                AttachVariantSlots(legacy.PrefabPath, variantTable);

            RegisterTemplates(added);
            Week19Encounter4Setup.Setup();
            AssetDatabase.SaveAssets();
            Debug.Log("Obstacle-4 ready: every obstacle room uses a room-level 40% special roll with at most one " +
                      "Marie bomb box, and Small, Basic, Wide and Tall each add one authored obstacle Layout.");
        }

        public static string[] CreatedAssetPaths() => Layouts
            .SelectMany(spec => new[] { spec.TemplatePath, spec.PrefabPath })
            .Concat(new[] { MarieDropTablePath, MarieVariantPath, FairyKingdomVariantTablePath })
            .ToArray();

        private static ObstacleVariantTable EnsureVariantAssets()
        {
            EnsureFolder(VariantFolder);
            ResourceDropTable dropTable = Week17Resource3Setup.EnsureTable(MarieDropTablePath, MarieDropChance,
                MarieDropWeights, "Configure Marie bomb box drops");

            ObstacleVariantDefinition marie = AssetDatabase.LoadAssetAtPath<ObstacleVariantDefinition>(MarieVariantPath);
            if (marie == null)
            {
                marie = ScriptableObject.CreateInstance<ObstacleVariantDefinition>();
                AssetDatabase.CreateAsset(marie, MarieVariantPath);
            }

            Undo.RecordObject(marie, "Configure Marie bomb box obstacle variant");
            marie.Configure("marie-bomb-box", DestructibleObstacle.DefaultRequiredHits, dropTable,
                new Color(0.94f, 0.46f, 0.68f), new Color(0.52f, 0.16f, 0.3f));
            EditorUtility.SetDirty(marie);

            ObstacleVariantTable table =
                AssetDatabase.LoadAssetAtPath<ObstacleVariantTable>(FairyKingdomVariantTablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<ObstacleVariantTable>();
                AssetDatabase.CreateAsset(table, FairyKingdomVariantTablePath);
            }

            Undo.RecordObject(table, "Configure Fairy Kingdom obstacle variants");
            table.Configure(SpecialRoomChance, new[] { new ObstacleVariantEntry(marie, 100) });
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return table;
        }

        private static void PlaceObstacles(string prefabPath, IReadOnlyList<Vector2> cells,
            GameObject obstaclePrefab, ObstacleVariantTable table)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                RoomPrefab room = root.GetComponent<RoomPrefab>();
                Transform content = room?.Node?.ContentRoot != null ? room.Node.ContentRoot.transform : null;
                if (content == null) throw new InvalidOperationException($"{prefabPath} has no room content root.");
                Transform container = content.Find(Week18Obstacle2Setup.ObstacleContainerName);
                if (container == null)
                {
                    container = new GameObject(Week18Obstacle2Setup.ObstacleContainerName).transform;
                    container.SetParent(content, false);
                }

                ResourceDropTable basicTable =
                    AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week18Obstacle1Setup.DropTablePath);
                HashSet<GameObject> kept = new();
                for (int index = 0; index < cells.Count; index++)
                {
                    string id = Week18Obstacle2Setup.ObstacleId(index);
                    string objectName = $"Obstacle {id}";
                    Transform existing = container.Find(objectName);
                    GameObject instance = existing != null &&
                                          PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) ==
                                          obstaclePrefab
                        ? existing.gameObject
                        : (GameObject)PrefabUtility.InstantiatePrefab(obstaclePrefab, container);
                    instance.name = objectName;
                    instance.transform.localPosition = cells[index];
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    DestructibleObstacle obstacle = instance.GetComponent<DestructibleObstacle>();
                    obstacle.Configure(id, DestructibleObstacle.DefaultRequiredHits, basicTable,
                        instance.GetComponentInChildren<SpriteRenderer>(true));
                    GetOrAdd<RoomObstacleVariantSlot>(instance).Configure(table);
                    kept.Add(instance);
                }

                foreach (Transform child in container.Cast<Transform>().ToArray())
                    if (!kept.Contains(child.gameObject)) Object.DestroyImmediate(child.gameObject);
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    throw new InvalidOperationException($"Obstacle-4 could not save {prefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AttachVariantSlots(string prefabPath, ObstacleVariantTable table)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (DestructibleObstacle obstacle in root.GetComponentsInChildren<DestructibleObstacle>(true))
                    GetOrAdd<RoomObstacleVariantSlot>(obstacle.gameObject).Configure(table);
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    throw new InvalidOperationException($"Obstacle-4 could not update {prefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RegisterTemplates(IEnumerable<RoomTemplateDefinition> added)
        {
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null) throw new InvalidOperationException("Obstacle-4 requires the Game Scene generator.");
            RoomTemplateDefinition[] templates = generator.RoomTemplates.Concat(added)
                .Where(template => template != null)
                .GroupBy(template => template.TemplateId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderBy(template => template.TemplateId, StringComparer.Ordinal)
                .ToArray();
            Undo.RecordObject(generator, "Configure Obstacle-4 Layout catalog");
            generator.ConfigureTemplates(Math.Max(RoomContentVersion, generator.RoomContentVersion), templates);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Obstacle-4 setup.");
        }

        private static IEnumerable<Vector2> Block(float centerX, float centerY, int width, int height)
        {
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                yield return new Vector2(centerX - width * 0.5f + 0.5f + x,
                    centerY - height * 0.5f + 0.5f + y);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component =>
            target.TryGetComponent(out T component) ? component : target.AddComponent<T>();

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
