using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Hitbox-2: splits a normal enemy's terrain collision (feet) from its body, like the player's Hitbox-0. Adds the
    // EnemyFeet layer that collides with Environment and Pit only, and gives every normal enemy Prefab a feet child
    // while its body circle excludes those two layers. The Enemy layer keeps its terrain collisions for bosses.
    public static class Week27Hitbox2Setup
    {
        public const string PrefabSearchFolder = "Assets";

        [MenuItem("Trickal Fan Game/Week 27/Setup Hitbox-2 Enemy Feet Collider")]
        public static void Setup()
        {
            int feetLayer = EnsureFeetLayer();
            ConfigureCollisions(feetLayer);
            int changed = ConfigureEnemyPrefabs(out int withFeet);
            AssetDatabase.SaveAssets();
            Debug.Log($"Hitbox-2 ready: the {EnemyFeet.LayerName} layer collides with " +
                      $"{string.Join(" and ", EnemyFeet.CollisionLayers)} only, and {withFeet} normal enemy Prefabs " +
                      $"have feet of radius {EnemyFeet.Radius} ({changed} changed).");
        }

        public static int EnsureFeetLayer()
        {
            int existing = EnemyFeet.Layer;
            if (existing >= 0) return existing;

            SerializedObject tagManager = new(AssetDatabase.LoadAllAssetsAtPath(Week23Hitbox0Setup.TagManagerPath)[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            // Layers 0~7 are reserved by Unity.
            for (int index = 8; index < layers.arraySize; index++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = EnemyFeet.LayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return index;
            }

            throw new InvalidOperationException($"Hitbox-2 found no free user layer for {EnemyFeet.LayerName}.");
        }

        // The feet row collides with terrain only. Other rows are left alone, so the Enemy layer (bosses) still meets
        // terrain and normal enemy bodies drop it per collider instead.
        public static void ConfigureCollisions(int feetLayer)
        {
            HashSet<int> terrain = new(EnemyFeet.CollisionLayers.Select(LayerMask.NameToLayer));
            if (terrain.Contains(-1))
                throw new InvalidOperationException(
                    $"Hitbox-2 needs the {string.Join(" and ", EnemyFeet.CollisionLayers)} layers.");

            SerializedObject settings =
                new(AssetDatabase.LoadAllAssetsAtPath(Week23Hitbox0Setup.Physics2DSettingsPath)[0]);
            SerializedProperty matrix = settings.FindProperty("m_LayerCollisionMatrix");
            if (matrix == null || !matrix.isArray || matrix.arraySize < 32)
                throw new InvalidOperationException("Hitbox-2 could not read the 2D layer collision matrix.");
            for (int layer = 0; layer < 32; layer++)
            {
                bool collide = terrain.Contains(layer);
                SetBit(matrix.GetArrayElementAtIndex(layer), feetLayer, collide);
                SetBit(matrix.GetArrayElementAtIndex(feetLayer), layer, collide);
            }

            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            for (int layer = 0; layer < 32; layer++)
            {
                if (Physics2D.GetIgnoreLayerCollision(feetLayer, layer) == terrain.Contains(layer))
                    throw new InvalidOperationException(
                        $"Hitbox-2 could not set the feet collision with layer {LayerMask.LayerToName(layer)}.");
            }
        }

        private static void SetBit(SerializedProperty row, int bit, bool value)
        {
            long mask = 1L << bit;
            long current = row.longValue & 0xFFFFFFFFL;
            row.longValue = value ? current | mask : current & ~mask;
        }

        // Every Prefab whose root is an enemy (TestEnemy), bases before their variants so a variant inherits the feet
        // its base gains.
        public static string[] EnemyPrefabPaths()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { PrefabSearchFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    return prefab != null && prefab.GetComponent<TestEnemy>() != null;
                })
                .OrderBy(VariantDepth).ThenBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static int VariantDepth(string path)
        {
            int depth = 0;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            while (prefab != null && PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant)
            {
                depth++;
                prefab = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            }

            return depth;
        }

        private static int ConfigureEnemyPrefabs(out int withFeet)
        {
            int changed = 0;
            withFeet = 0;
            foreach (string path in EnemyPrefabPaths())
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool needs = EnemyFeet.Needs(contents);
                    if (needs) withFeet++;
                    if (!(needs ? EnemyFeet.Ensure(contents, out _) : StripFeet(contents))) continue;
                    PrefabUtility.SaveAsPrefabAsset(contents, path, out bool saved);
                    if (!saved) throw new InvalidOperationException($"Hitbox-2 could not save '{path}'.");
                    changed++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return changed;
        }

        // A boss variant of a normal enemy inherits its base's feet: they are switched off there, and the body takes
        // terrain back. Returns true when anything changed.
        private static bool StripFeet(GameObject enemy)
        {
            bool changed = false;
            foreach (EnemyFeet feet in enemy.GetComponentsInChildren<EnemyFeet>(true))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(feet.gameObject))
                {
                    if (!feet.gameObject.activeSelf) continue;
                    feet.gameObject.SetActive(false);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(feet.gameObject);
                }

                changed = true;
            }

            int terrain = EnemyFeet.TerrainMask;
            if (EnemyFeet.TryGetBody(enemy, out CircleCollider2D body) && (body.excludeLayers.value & terrain) != 0)
            {
                body.excludeLayers = body.excludeLayers.value & ~terrain;
                changed = true;
            }

            return changed;
        }
    }
}
