using System;
using System.IO;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Chest-0: builds the shared chest Prefab. The kind and room-local ID are set per instance when a chest is placed,
    // so one Prefab serves all three kinds. Re-running updates the same Prefab and keeps its GUID.
    public static class Week22Chest0Setup
    {
        public const string ChestPrefabPath = "Assets/Prefabs/TreasureChest.prefab";

        [MenuItem("Trickal Fan Game/Week 22/Setup Chest-0 Chest Prefab")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Chest-0 setup.");
            if (LayerMask.NameToLayer(TreasureChest.LayerName) < 0)
                throw new InvalidOperationException($"The {TreasureChest.LayerName} layer is missing.");

            TreasureChest chest = EnsureChestPrefab();
            Debug.Log($"Chest-0 setup complete: {AssetDatabase.GetAssetPath(chest)} is a pushable solid chest on the " +
                      $"{TreasureChest.LayerName} layer.");
        }

        public static TreasureChest EnsureChestPrefab()
        {
            Sprite sprite =
                AssetDatabase.GetBuiltinExtraResource<Sprite>(Week13FrontendUiAssets.PlaceholderFillSpritePath);
            if (sprite == null) throw new InvalidOperationException("The built-in chest sprite is missing.");

            bool exists = File.Exists(ChestPrefabPath);
            GameObject root = exists
                ? PrefabUtility.LoadPrefabContents(ChestPrefabPath)
                : new GameObject("Treasure Chest");
            try
            {
                root.layer = LayerMask.NameToLayer(TreasureChest.LayerName);
                root.transform.localScale = Vector3.one * (ChestPlacement.ChestWorldSize / sprite.bounds.size.x);
                SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(root);
                renderer.sprite = sprite;
                renderer.sortingOrder = 1;
                BoxCollider2D collider = GetOrAdd<BoxCollider2D>(root);
                collider.isTrigger = false;
                TreasureChest.ConfigureBody(GetOrAdd<Rigidbody2D>(root));
                collider.offset = Vector2.zero;
                collider.size = sprite.bounds.size;
                TreasureChest chest = GetOrAdd<TreasureChest>(root);
                chest.Configure(TreasureChest.DefaultChestId, ChestKind.Normal);
                chest.ConfigureDisplay(renderer);

                Directory.CreateDirectory("Assets/Prefabs");
                if (PrefabUtility.SaveAsPrefabAsset(root, ChestPrefabPath) == null)
                    throw new InvalidOperationException($"Could not save {ChestPrefabPath}.");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath).GetComponent<TreasureChest>();
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }
    }
}
