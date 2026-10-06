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
    // Authored Large-profile obstacle Layouts. Each Layout owns its own SpawnPoints and a set of 1x1 destructible
    // obstacles placed as nested DestructibleObstacle prefab instances, so the empty Large room, the fixed pillar room
    // and these rooms share one room size for playtest comparison.
    public static class Week18Obstacle2Setup
    {
        public const int RoomContentVersion = 4;
        public const string ObstacleContainerName = "Obstacles";

        public sealed class LayoutSpec
        {
            public LayoutSpec(string templateId, string prefabName, Vector2[] spawnPoints, Vector2[] cells)
            {
                TemplateId = templateId;
                PrefabName = prefabName;
                SpawnPoints = spawnPoints;
                Cells = cells;
            }

            public string TemplateId { get; }
            public string PrefabName { get; }
            public Vector2[] SpawnPoints { get; }
            // Centers of 1x1 obstacles in room-local space. Index i becomes obstacle-(i+1).
            public Vector2[] Cells { get; }
            public string TemplatePath => $"{Week14Room1Setup.TemplateFolder}/{TemplateId}.asset";
            public string PrefabPath => $"{Week8GridFloorSetup.PrefabFolder}/room-{TemplateId}.prefab";
        }

        // Four 2x2 cover blocks, one per quadrant.
        public static readonly LayoutSpec CoverBlocks = new(
            "large-cover-blocks",
            "Room Large Cover Blocks",
            new[] { new Vector2(-8.5f, 4f), new Vector2(8.5f, -4f), new Vector2(0f, -2.5f) },
            Block(-5f, 3f, 2, 2).Concat(Block(5f, 3f, 2, 2)).Concat(Block(-5f, -3f, 2, 2))
                .Concat(Block(5f, -3f, 2, 2)).ToArray());

        // Two 1x6 walls split the room into three lanes joined along the top and bottom.
        public static readonly LayoutSpec SplitLanes = new(
            "large-split-lanes",
            "Room Large Split Lanes",
            new[] { new Vector2(-8f, -4f), new Vector2(8f, 4f), Vector2.zero },
            Block(-4.5f, 0f, 1, 6).Concat(Block(4.5f, 0f, 1, 6)).ToArray());

        // Eight staggered single blocks.
        public static readonly LayoutSpec ScatteredRubble = new(
            "large-scattered-rubble",
            "Room Large Scattered Rubble",
            new[] { new Vector2(-5f, -4f), new Vector2(5f, 4f), Vector2.zero },
            new[]
            {
                new Vector2(-7.5f, 2.5f), new Vector2(-7.5f, -2.5f), new Vector2(7.5f, 2.5f),
                new Vector2(7.5f, -2.5f), new Vector2(-3.5f, 0.5f), new Vector2(3.5f, -0.5f),
                new Vector2(0.5f, 3.5f), new Vector2(-0.5f, -3.5f),
            });

        public static readonly LayoutSpec[] Layouts = { CoverBlocks, SplitLanes, ScatteredRubble };

        public static string ObstacleId(int index) => $"obstacle-{index + 1:00}";

        [MenuItem("Trickal Fan Game/Week 18/Setup Obstacle-2 Obstacle Layouts")]
        public static void Setup()
        {
            OpenGameScene();
            DestructibleObstacle obstaclePrefab = Week18Obstacle1Setup.EnsurePrefab();
            GameObject largePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week14Room6Setup.LargePrefabPath);
            RoomTemplateDefinition pillarTemplate =
                AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(Week14Room7Setup.TemplatePath);
            if (largePrefab == null || pillarTemplate == null)
                throw new InvalidOperationException("Obstacle-2 requires the Room-6 Large Prefab and Room-7 pillar template.");

            List<RoomTemplateDefinition> layoutTemplates = new();
            foreach (LayoutSpec layout in Layouts)
            {
                Week14Room3Setup.ConfigureLayout(Week14Room6Setup.LargeProfileId, layout.TemplateId,
                    Week14Room6Setup.LargeProfilePath, layout.TemplatePath, layout.PrefabPath, layout.PrefabName,
                    Week14Room6Setup.LargeSize, layout.SpawnPoints, largePrefab);
                PlaceObstacles(layout, obstaclePrefab.gameObject);
                RoomTemplateDefinition template = AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(layout.TemplatePath);
                if (template == null || !template.TryValidate(out string error) || !template.TryValidateLayout(out error))
                    throw new InvalidOperationException($"Obstacle-2 Layout '{layout.TemplateId}' is invalid after setup.");
                layoutTemplates.Add(template);
            }

            RegisterTemplates(layoutTemplates.Prepend(pillarTemplate));
            AssetDatabase.SaveAssets();
            Debug.Log("Obstacle-2 obstacle Layouts ready: large-cover-blocks, large-split-lanes and " +
                      "large-scattered-rubble share the Large profile with the empty and pillar rooms, own their " +
                      "SpawnPoints, and pass the overlap, required passage and ranged sightline checks.");
        }

        public static void OpenGameScene()
        {
            if (SceneManager.GetActiveScene().path == Week13FrontendSetup.GameScenePath) return;
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
        }

        public static string[] CreatedAssetPaths() =>
            Layouts.SelectMany(layout => new[] { layout.TemplatePath, layout.PrefabPath }).ToArray();

        private static void PlaceObstacles(LayoutSpec layout, GameObject obstaclePrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(layout.PrefabPath);
            try
            {
                RoomPrefab room = root.GetComponent<RoomPrefab>();
                if (room == null || room.Node == null || room.Node.ContentRoot == null)
                    throw new InvalidOperationException($"{layout.PrefabPath} is missing its room content root.");

                Transform content = room.Node.ContentRoot.transform;
                Transform container = content.Find(ObstacleContainerName);
                if (container == null)
                {
                    container = new GameObject(ObstacleContainerName).transform;
                    container.SetParent(content, false);
                }

                container.localPosition = Vector3.zero;
                container.localRotation = Quaternion.identity;
                container.localScale = Vector3.one;

                ResourceDropTable table = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week18Obstacle1Setup.DropTablePath);
                HashSet<GameObject> kept = new();
                for (int index = 0; index < layout.Cells.Length; index++)
                {
                    string id = ObstacleId(index);
                    string name = $"Obstacle {id}";
                    Transform existing = container.Find(name);
                    GameObject instance = existing != null &&
                                          PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == obstaclePrefab
                        ? existing.gameObject
                        : (GameObject)PrefabUtility.InstantiatePrefab(obstaclePrefab, container);
                    instance.name = name;
                    instance.transform.localPosition = layout.Cells[index];
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    DestructibleObstacle obstacle = instance.GetComponent<DestructibleObstacle>();
                    SpriteRenderer visual = instance.GetComponentInChildren<SpriteRenderer>(true);
                    obstacle.Configure(id, DestructibleObstacle.DefaultRequiredHits, table, visual);
                    kept.Add(instance);
                }

                foreach (Transform child in container.Cast<Transform>().ToArray())
                {
                    if (!kept.Contains(child.gameObject)) Object.DestroyImmediate(child.gameObject);
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, layout.PrefabPath) == null)
                    throw new InvalidOperationException($"Obstacle-2 could not save {layout.PrefabPath}.");
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
            if (generator == null)
                throw new InvalidOperationException("Obstacle-2 requires the Game Scene FloorGenerator.");

            RoomTemplateDefinition[] templates = generator.RoomTemplates
                .Concat(added)
                .Where(template => template != null)
                .GroupBy(template => template.TemplateId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderBy(template => template.TemplateId, StringComparer.Ordinal)
                .ToArray();
            Undo.RecordObject(generator, "Configure Obstacle-2 Layout catalog");
            generator.ConfigureTemplates(Math.Max(RoomContentVersion, generator.RoomContentVersion), templates);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Obstacle-2 setup.");
        }

        private static IEnumerable<Vector2> Block(float centerX, float centerY, int width, int height)
        {
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                yield return new Vector2(centerX - width * 0.5f + 0.5f + x, centerY - height * 0.5f + 0.5f + y);
            }
        }
    }
}
