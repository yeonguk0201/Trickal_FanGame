using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Terrain-0: one kind of ordinary floor pit. The Pit physics layer collides only with walking bodies (player,
    // enemies, pickups), so pits block movement while projectiles pass over them. Two authored Layouts add pits under
    // new template IDs (Basic and Large); existing Layout IDs keep their meaning.
    public static class Week22Terrain0Setup
    {
        public const int RoomContentVersion = 9;
        public const string PitPrefabPath = "Assets/Prefabs/RoomPit.prefab";
        public const string PitContainerName = "Pits";
        public const string RimName = "Rim";
        public const string HoleName = "Hole";
        public const float RimWidth = 0.1f;
        // Above the floor artwork (-100), below obstacles, chests and actors (0).
        public const int PitSortingOrder = -10;
        public const string TagManagerPath = "ProjectSettings/TagManager.asset";
        public const string Physics2DSettingsPath = "ProjectSettings/Physics2DSettings.asset";

        public static readonly Color RimColor = new(0.34f, 0.25f, 0.2f);
        public static readonly Color HoleColor = new(0.05f, 0.04f, 0.06f);
        // Hitbox-0: the player meets terrain with its feet layer, not the body.
        public static readonly string[] PitCollisionLayers = { PlayerFeet.LayerName, "Enemy", "Pickup" };

        public readonly struct PitSpec
        {
            public PitSpec(Vector2 center, Vector2 size)
            {
                Center = center;
                Size = size;
            }

            public Vector2 Center { get; }
            public Vector2 Size { get; }
            public Rect Bounds => new(Center - Size * 0.5f, Size);
        }

        public sealed class LayoutSpec
        {
            public LayoutSpec(string templateId, string prefabName, string profileId, string profilePath,
                Vector2 roomSize, string sourcePrefabPath, Vector2[] spawnPoints, PitSpec[] pits,
                Vector2[] obstacleCells)
            {
                TemplateId = templateId;
                PrefabName = prefabName;
                ProfileId = profileId;
                ProfilePath = profilePath;
                RoomSize = roomSize;
                SourcePrefabPath = sourcePrefabPath;
                SpawnPoints = spawnPoints;
                Pits = pits;
                ObstacleCells = obstacleCells;
            }

            public string TemplateId { get; }
            public string PrefabName { get; }
            public string ProfileId { get; }
            public string ProfilePath { get; }
            public Vector2 RoomSize { get; }
            public string SourcePrefabPath { get; }
            // SpawnPoints 1~3; Encounter-4 appends the rest.
            public Vector2[] SpawnPoints { get; }
            // Index i becomes pit-(i+1).
            public PitSpec[] Pits { get; }
            public Vector2[] ObstacleCells { get; }
            public string TemplatePath => $"{Week14Room1Setup.TemplateFolder}/{TemplateId}.asset";
            public string PrefabPath => $"{Week8GridFloorSetup.PrefabFolder}/room-{TemplateId}.prefab";
        }

        // One 4x2 pit in the middle; every door walks around it.
        public static readonly LayoutSpec BasicCentralPit = new(
            "basic-central-pit", "Room Basic Central Pit",
            Week14Room1Setup.BasicProfileId, Week14Room1Setup.BasicProfilePath, RoomLayout.RoomSize,
            Week8GridFloorSetup.PrefabPath,
            new[] { new Vector2(-4.5f, 2.5f), new Vector2(4.5f, 2.5f), new Vector2(4.5f, -2.5f) },
            new[] { new PitSpec(Vector2.zero, new Vector2(4f, 2f)) },
            Array.Empty<Vector2>());

        // Four 3x2 pits form two broken lanes left and right of a rock pair column; the side doors run through the
        // gap between each lane's pits.
        public static readonly LayoutSpec LargePitLanes = new(
            "large-pit-lanes", "Room Large Pit Lanes",
            Week14Room6Setup.LargeProfileId, Week14Room6Setup.LargeProfilePath, Week14Room6Setup.LargeSize,
            Week14Room6Setup.LargePrefabPath,
            new[] { new Vector2(-8.5f, 4f), new Vector2(8.5f, -4f), Vector2.zero },
            new[]
            {
                new PitSpec(new Vector2(-5.5f, 2f), new Vector2(3f, 2f)),
                new PitSpec(new Vector2(-5.5f, -2f), new Vector2(3f, 2f)),
                new PitSpec(new Vector2(5.5f, 2f), new Vector2(3f, 2f)),
                new PitSpec(new Vector2(5.5f, -2f), new Vector2(3f, 2f)),
            },
            new[]
            {
                new Vector2(-0.5f, 2.5f), new Vector2(0.5f, 2.5f),
                new Vector2(-0.5f, -2.5f), new Vector2(0.5f, -2.5f),
            });

        public static readonly LayoutSpec[] Layouts = { BasicCentralPit, LargePitLanes };

        public static string PitId(int index) => $"pit-{index + 1:00}";

        [MenuItem("Trickal Fan Game/Week 22/Setup Terrain-0 Pit Layouts")]
        public static void Setup()
        {
            int pitLayer = EnsurePitLayer();
            ConfigurePitCollisions(pitLayer);
            Week18Obstacle2Setup.OpenGameScene();
            GameObject pitPrefab = EnsurePitPrefab(pitLayer);
            DestructibleObstacle obstaclePrefab = Week18Obstacle1Setup.EnsurePrefab();
            ObstacleVariantTable variantTable = AssetDatabase.LoadAssetAtPath<ObstacleVariantTable>(
                Week20Obstacle4Setup.FairyKingdomVariantTablePath);
            if (variantTable == null)
                throw new InvalidOperationException("Terrain-0 requires the Obstacle-4 Fairy Kingdom variant table.");

            List<RoomTemplateDefinition> added = new();
            foreach (LayoutSpec spec in Layouts)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(spec.SourcePrefabPath);
                if (source == null)
                    throw new InvalidOperationException($"Terrain-0 is missing source Prefab {spec.SourcePrefabPath}.");

                Week14Room3Setup.ConfigureLayout(spec.ProfileId, spec.TemplateId, spec.ProfilePath,
                    spec.TemplatePath, spec.PrefabPath, spec.PrefabName, spec.RoomSize, spec.SpawnPoints, source);
                if (spec.ObstacleCells.Length > 0)
                    Week20Obstacle4Setup.PlaceObstacles(spec.PrefabPath, spec.ObstacleCells, obstaclePrefab.gameObject,
                        variantTable);
                PlacePits(spec, pitPrefab);
                // ConfigureLayout rebuilds the walls with placeholder sprites; restore the connected artwork.
                FairyVillageArtworkSetup.ApplyToPrefab(spec.PrefabPath);

                RoomTemplateDefinition template = AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(spec.TemplatePath);
                Undo.RecordObject(template, "Configure Terrain-0 Layout modifier");
                template.ConfigureLayoutDifficultyModifier(Week19Difficulty1Setup.ObstacleLayoutModifier);
                EditorUtility.SetDirty(template);
                if (!template.TryValidate(out string error) || !template.TryValidateLayout(out error))
                    throw new InvalidOperationException($"Terrain-0 Layout '{spec.TemplateId}' is invalid. {error}");
                added.Add(template);
            }

            RegisterTemplates(added);
            Week19Encounter4Setup.Setup();
            AssetDatabase.SaveAssets();
            Debug.Log("Terrain-0 ready: pits sit on the Pit layer (blocks player, enemies and pickups; projectiles " +
                      "pass), and basic-central-pit and large-pit-lanes join the Normal room catalog on every floor.");
        }

        public static string[] CreatedAssetPaths() => Layouts
            .SelectMany(spec => new[] { spec.TemplatePath, spec.PrefabPath })
            .Append(PitPrefabPath)
            .ToArray();

        public static int EnsurePitLayer()
        {
            int existing = LayerMask.NameToLayer(RoomPit.LayerName);
            if (existing >= 0) return existing;

            SerializedObject tagManager = new(AssetDatabase.LoadAllAssetsAtPath(TagManagerPath)[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            // Layers 0~7 are reserved by Unity.
            for (int index = 8; index < layers.arraySize; index++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = RoomPit.LayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return index;
            }

            throw new InvalidOperationException("Terrain-0 found no free user layer for Pit.");
        }

        // The Pit row collides with walking bodies only; projectiles (PlayerProjectile, Default triggers), walls and
        // chests ignore it. Chests check pits through their push cast instead.
        public static void ConfigurePitCollisions(int pitLayer)
        {
            HashSet<int> colliding = new(PitCollisionLayers.Select(LayerMask.NameToLayer));
            if (colliding.Contains(-1))
                throw new InvalidOperationException("Terrain-0 needs the PlayerFeet, Enemy and Pickup layers.");

            SerializedObject settings = new(AssetDatabase.LoadAllAssetsAtPath(Physics2DSettingsPath)[0]);
            SerializedProperty matrix = settings.FindProperty("m_LayerCollisionMatrix");
            if (matrix == null || !matrix.isArray || matrix.arraySize < 32)
                throw new InvalidOperationException("Terrain-0 could not read the 2D layer collision matrix.");
            for (int layer = 0; layer < 32; layer++)
            {
                bool collide = colliding.Contains(layer);
                SetBit(matrix.GetArrayElementAtIndex(layer), pitLayer, collide);
                SetBit(matrix.GetArrayElementAtIndex(pitLayer), layer, collide);
            }

            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            for (int layer = 0; layer < 32; layer++)
            {
                if (Physics2D.GetIgnoreLayerCollision(pitLayer, layer) == colliding.Contains(layer))
                    throw new InvalidOperationException(
                        $"Terrain-0 could not set the Pit collision with layer {LayerMask.LayerToName(layer)}.");
            }
        }

        private static void SetBit(SerializedProperty row, int bit, bool value)
        {
            long mask = 1L << bit;
            long current = row.longValue & 0xFFFFFFFFL;
            row.longValue = value ? current | mask : current & ~mask;
        }

        public static GameObject EnsurePitPrefab(int pitLayer)
        {
            Sprite sprite = Week13FrontendUiAssets.LoadPlaceholderFillSprite();
            bool exists = File.Exists(PitPrefabPath);
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PitPrefabPath) : new GameObject("Room Pit");
            try
            {
                root.layer = pitLayer;
                root.transform.localScale = Vector3.one;
                BoxCollider2D collider = GetOrAdd<BoxCollider2D>(root);
                collider.isTrigger = false;
                collider.offset = Vector2.zero;
                collider.size = Vector2.one;
                ConfigureVisual(root.transform, RimName, sprite, RimColor, PitSortingOrder, pitLayer);
                ConfigureVisual(root.transform, HoleName, sprite, HoleColor, PitSortingOrder + 1, pitLayer);
                ApplySize(root, Vector2.one);
                GetOrAdd<RoomPit>(root).Configure(PitId(0));
                Directory.CreateDirectory("Assets/Prefabs");
                if (PrefabUtility.SaveAsPrefabAsset(root, PitPrefabPath) == null)
                    throw new InvalidOperationException($"Could not save {PitPrefabPath}.");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PitPrefabPath);
        }

        // The collider covers the whole pit; the hole sits inside a thin rim so neighbouring pits stay readable.
        public static void ApplySize(GameObject pit, Vector2 size)
        {
            pit.GetComponent<BoxCollider2D>().size = size;
            SetVisualSize(pit.transform.Find(RimName), size);
            SetVisualSize(pit.transform.Find(HoleName), size - Vector2.one * (RimWidth * 2f));
        }

        private static void SetVisualSize(Transform visual, Vector2 size)
        {
            Vector2 spriteSize = visual.GetComponent<SpriteRenderer>().sprite.bounds.size;
            visual.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
        }

        private static void ConfigureVisual(Transform root, string name, Sprite sprite, Color color, int sortingOrder,
            int layer)
        {
            Transform existing = root.Find(name);
            GameObject visual = existing != null ? existing.gameObject : new GameObject(name);
            visual.transform.SetParent(root, false);
            visual.transform.localPosition = Vector3.zero;
            visual.layer = layer;
            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(visual);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static void PlacePits(LayoutSpec spec, GameObject pitPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(spec.PrefabPath);
            try
            {
                RoomPrefab room = root.GetComponent<RoomPrefab>();
                Transform content = room?.Node?.ContentRoot != null ? room.Node.ContentRoot.transform : null;
                if (content == null) throw new InvalidOperationException($"{spec.PrefabPath} has no room content root.");
                Transform container = content.Find(PitContainerName);
                if (container == null)
                {
                    container = new GameObject(PitContainerName).transform;
                    container.SetParent(content, false);
                }

                container.localPosition = Vector3.zero;
                container.localRotation = Quaternion.identity;
                container.localScale = Vector3.one;
                HashSet<GameObject> kept = new();
                for (int index = 0; index < spec.Pits.Length; index++)
                {
                    string id = PitId(index);
                    string objectName = $"Pit {id}";
                    Transform existing = container.Find(objectName);
                    GameObject instance = existing != null &&
                                          PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == pitPrefab
                        ? existing.gameObject
                        : (GameObject)PrefabUtility.InstantiatePrefab(pitPrefab, container);
                    instance.name = objectName;
                    instance.transform.localPosition = spec.Pits[index].Center;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    ApplySize(instance, spec.Pits[index].Size);
                    instance.GetComponent<RoomPit>().Configure(id);
                    kept.Add(instance);
                }

                foreach (Transform child in container.Cast<Transform>().ToArray())
                    if (!kept.Contains(child.gameObject)) Object.DestroyImmediate(child.gameObject);
                if (PrefabUtility.SaveAsPrefabAsset(root, spec.PrefabPath) == null)
                    throw new InvalidOperationException($"Terrain-0 could not save {spec.PrefabPath}.");
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
            if (generator == null) throw new InvalidOperationException("Terrain-0 requires the Game Scene generator.");
            RoomTemplateDefinition[] templates = generator.RoomTemplates.Concat(added)
                .Where(template => template != null)
                .GroupBy(template => template.TemplateId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderBy(template => template.TemplateId, StringComparer.Ordinal)
                .ToArray();
            Undo.RecordObject(generator, "Configure Terrain-0 Layout catalog");
            generator.ConfigureTemplates(Math.Max(RoomContentVersion, generator.RoomContentVersion), templates);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Terrain-0 setup.");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component =>
            target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
    }
}
