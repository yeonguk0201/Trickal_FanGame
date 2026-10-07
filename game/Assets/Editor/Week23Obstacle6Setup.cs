using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Obstacle-6: the rest of the §6.1 obstacles. The exploding box, 셰이디의 랜덤박스 and 마요의 수집품 상자 are kinds
    // of the special obstacle table that Obstacle-5 setup builds; this adds the high obstacle (a tree, which not even
    // a flying player passes) with one authored Basic Layout under a new template ID.
    // Obstacle-7: a tree is a fixed-kind DestructibleObstacle that breaks from 4 hits or a bomb and drops nothing.
    // Re-running updates the same assets and keeps their GUIDs.
    public static class Week23Obstacle6Setup
    {
        public const string TemplateId = "basic-tree-grove";
        public const string PrefabName = "Room Basic Tree Grove";
        public const string TreeContainerName = "Trees";
        public const string TreeVisualName = "Visual";
        public const string TemplatePath = Week14Room1Setup.TemplateFolder + "/" + TemplateId + ".asset";
        public const string PrefabPath = Week8GridFloorSetup.PrefabFolder + "/room-" + TemplateId + ".prefab";

        public const int TreeHits = DestructibleObstacle.DefaultRequiredHits;

        public static readonly Color TreeColor = new(0.13f, 0.4f, 0.2f);
        public static readonly Color TreeCrackedColor = new(0.2f, 0.16f, 0.1f);
        public static readonly Color TreeBurningColor = new(0.95f, 0.45f, 0.1f);

        // Four single trees around the middle; both door axes and the room center stay open.
        public static readonly Vector2[] TreeCells =
        {
            new(-2.5f, 1.5f), new(2.5f, 1.5f), new(-2.5f, -1.5f), new(2.5f, -1.5f),
        };

        // SpawnPoints 1~3; Encounter-4 appends the rest.
        public static readonly Vector2[] SpawnPoints =
        {
            new(-4.5f, 2.5f), new(4.5f, -2.5f), Vector2.zero,
        };

        public static string TreeId(int index) => $"tree-{index + 1:00}";

        [MenuItem("Trickal Fan Game/Week 23/Setup Obstacle-6 Remaining Obstacles")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Obstacle-6 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Week23Obstacle5Setup.Setup();
            Week18Obstacle2Setup.OpenGameScene();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Week8GridFloorSetup.PrefabPath);
            if (source == null)
                throw new InvalidOperationException($"Obstacle-6 is missing source Prefab {Week8GridFloorSetup.PrefabPath}.");

            Week14Room3Setup.ConfigureLayout(Week14Room1Setup.BasicProfileId, TemplateId,
                Week14Room1Setup.BasicProfilePath, TemplatePath, PrefabPath, PrefabName, RoomLayout.RoomSize,
                SpawnPoints, source);
            PlaceTrees();
            // ConfigureLayout rebuilds the walls with placeholder sprites; restore the connected artwork.
            FairyVillageArtworkSetup.ApplyToPrefab(PrefabPath);

            RoomTemplateDefinition template = AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(TemplatePath);
            Undo.RecordObject(template, "Configure Obstacle-6 Layout modifier");
            template.ConfigureLayoutDifficultyModifier(Week19Difficulty1Setup.ObstacleLayoutModifier);
            EditorUtility.SetDirty(template);
            if (!template.TryValidate(out string error) || !template.TryValidateLayout(out error))
                throw new InvalidOperationException($"Obstacle-6 Layout '{TemplateId}' is invalid. {error}");

            RegisterTemplate(template);
            Week19Encounter4Setup.Setup();
            AssetDatabase.SaveAssets();
            Debug.Log("Obstacle-6 ready: the exploding box, Shady's random box and Mayo's collection box join the " +
                      "special obstacle table, and basic-tree-grove adds four breakable trees that block flight to " +
                      "the Normal room catalog on every floor.");
        }

        public static string[] CreatedAssetPaths() => new[] { TemplatePath, PrefabPath };

        private static void PlaceTrees()
        {
            Sprite sprite = Week13FrontendUiAssets.LoadPlaceholderFillSprite();
            int environment = LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                RoomPrefab room = root.GetComponent<RoomPrefab>();
                Transform content = room?.Node?.ContentRoot != null ? room.Node.ContentRoot.transform : null;
                if (content == null) throw new InvalidOperationException($"{PrefabPath} has no room content root.");
                Transform container = content.Find(TreeContainerName);
                if (container == null)
                {
                    container = new GameObject(TreeContainerName).transform;
                    container.SetParent(content, false);
                }

                container.localPosition = Vector3.zero;
                container.localRotation = Quaternion.identity;
                container.localScale = Vector3.one;
                HashSet<GameObject> kept = new();
                for (int index = 0; index < TreeCells.Length; index++)
                {
                    string id = TreeId(index);
                    string objectName = $"Tree {id}";
                    Transform existing = container.Find(objectName);
                    GameObject tree = existing != null ? existing.gameObject : new GameObject(objectName);
                    tree.transform.SetParent(container, false);
                    tree.transform.localPosition = TreeCells[index];
                    tree.transform.localRotation = Quaternion.identity;
                    tree.transform.localScale = Vector3.one;
                    tree.layer = environment;
                    BoxCollider2D collider = GetOrAdd<BoxCollider2D>(tree);
                    collider.isTrigger = false;
                    collider.offset = Vector2.zero;
                    collider.size = Vector2.one;

                    Transform visualTransform = tree.transform.Find(TreeVisualName);
                    GameObject visual = visualTransform != null ? visualTransform.gameObject : new GameObject(TreeVisualName);
                    visual.transform.SetParent(tree.transform, false);
                    visual.transform.localPosition = Vector3.zero;
                    visual.layer = environment;
                    SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(visual);
                    renderer.sprite = sprite;
                    renderer.color = TreeColor;
                    renderer.sortingOrder = 1;
                    Vector2 spriteSize = sprite.bounds.size;
                    visual.transform.localScale = new Vector3(1f / spriteSize.x, 1f / spriteSize.y, 1f);

                    // Before Obstacle-7 a tree was a fixed RoomStaticObstacle.
                    if (tree.TryGetComponent(out RoomStaticObstacle fixedObstacle))
                        Object.DestroyImmediate(fixedObstacle);
                    DestructibleObstacle obstacle = GetOrAdd<DestructibleObstacle>(tree);
                    obstacle.Configure(id, TreeHits, null, renderer);
                    obstacle.ConfigureFixedKind(DestructibleObstacle.TreeVariantId, TreeColor, TreeCrackedColor, true,
                        DestructibleObstacle.TreeBurnHits, TreeBurningColor);
                    if (!obstacle.TryValidate(out string error)) throw new InvalidOperationException(error);
                    kept.Add(tree);
                }

                foreach (Transform child in container.Cast<Transform>().ToArray())
                    if (!kept.Contains(child.gameObject)) Object.DestroyImmediate(child.gameObject);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException($"Obstacle-6 could not save {PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RegisterTemplate(RoomTemplateDefinition added)
        {
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null) throw new InvalidOperationException("Obstacle-6 requires the Game Scene generator.");
            RoomTemplateDefinition[] templates = generator.RoomTemplates.Append(added)
                .Where(template => template != null)
                .GroupBy(template => template.TemplateId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderBy(template => template.TemplateId, StringComparer.Ordinal)
                .ToArray();
            Undo.RecordObject(generator, "Configure Obstacle-6 Layout catalog");
            generator.ConfigureTemplates(
                Math.Max(Week23Obstacle5Setup.RoomContentVersion, generator.RoomContentVersion), templates);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Obstacle-6 setup.");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component =>
            target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
    }
}
