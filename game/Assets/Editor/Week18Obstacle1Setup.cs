using System;
using System.IO;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week18Obstacle1Setup
    {
        public const string PrefabPath = "Assets/Prefabs/DestructibleObstacle.prefab";
        public const string DropTablePath = Week17Resource3Setup.DropTableFolder + "/obstacle-basic-drop-table.asset";
        public const string DefaultObstacleId = "obstacle-01";
        public const float DropChance = 0.03f;
        public const string VisualName = "Visual";

        public static readonly (string dropId, int weight)[] DropWeights =
        {
            ("heart", 25),
            ("sp", 25),
            ("key", 10),
            ("bomb", 8),
            ("elif", 30),
            ("pit", 2),
        };

        [MenuItem("Trickal Fan Game/Week 18/Setup Obstacle-1 Destructible Obstacle")]
        public static void Setup()
        {
            DestructibleObstacle prefab = EnsurePrefab();
            Selection.activeObject = prefab.gameObject;
            Debug.Log("Obstacle-1 destructible obstacle ready: a 1x1 Environment block that breaks after 5 player " +
                      "attack or skill hits and rolls a 3% drop (heart 25 / SP 25 / key 10 / bomb 8 / elif 30 / " +
                      "pit 2, pit drops nothing until Special-3). Obstacle-2 places it in room layouts.", prefab);
        }

        public static ResourceDropTable EnsureDropTable()
        {
            return Week17Resource3Setup.EnsureTable(DropTablePath, DropChance, DropWeights,
                "Configure Obstacle-1 drop table");
        }

        public static DestructibleObstacle EnsurePrefab()
        {
            int environmentLayer = LayerMask.NameToLayer("Environment");
            if (environmentLayer < 0) throw new InvalidOperationException("The Environment layer is missing.");
            Sprite sprite = Week13FrontendUiAssets.LoadPlaceholderFillSprite();
            EnsureDropTable();

            bool exists = File.Exists(PrefabPath);
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Destructible Obstacle");
            try
            {
                // Opening prefab contents can unload assets held only from C#; load the table right before use.
                ResourceDropTable table = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(DropTablePath);
                Configure(root, sprite, table, environmentLayer);
                Directory.CreateDirectory("Assets/Prefabs");
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException($"Could not save {PrefabPath}.");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<DestructibleObstacle>();
        }

        private static void Configure(GameObject root, Sprite sprite, ResourceDropTable table, int environmentLayer)
        {
            root.layer = environmentLayer;
            root.transform.localScale = Vector3.one;

            BoxCollider2D collider = GetOrAdd<BoxCollider2D>(root);
            collider.isTrigger = false;
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;

            Transform visualTransform = root.transform.Find(VisualName);
            GameObject visual = visualTransform != null ? visualTransform.gameObject : new GameObject(VisualName);
            visual.transform.SetParent(root.transform, false);
            visual.layer = environmentLayer;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(1f / sprite.bounds.size.x, 1f / sprite.bounds.size.y, 1f);
            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(visual);
            renderer.sprite = sprite;
            renderer.color = new Color(0.62f, 0.45f, 0.3f);
            renderer.sortingOrder = 0;

            DestructibleObstacle obstacle = GetOrAdd<DestructibleObstacle>(root);
            obstacle.Configure(DefaultObstacleId, DestructibleObstacle.DefaultRequiredHits, table, renderer);
            EditorUtility.SetDirty(obstacle);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
