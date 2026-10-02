using System;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Range-0: projectile travel distance = flight time × shot speed. Ranges used to be longer than the 16×9 room, so
    // every shot flew until it hit something. Each ranged enemy now gets a lifetime that gives it its own range; boss
    // barrages keep their 4-second lifetime and still fly to the walls.
    public static class Week21Range0Setup
    {
        public const string MagePrefabPath = Week15Boss3Setup.MagePrefabPath;
        public const string ArcherPrefabPath = Week15Boss3Setup.ArcherPrefabPath;
        public const string RangedPrefabPath = "Assets/Prefabs/RangedEnemy.prefab";

        // Player basic attack: 8 speed × 2/3 second ≈ 5.3 units (first set to 1 second / 8 units, then shortened by
        // 1.5×). It is the default of the PlayerProjectileAttack.baseProjectileLifetime field, which the Game Scene
        // does not override.
        public const float PlayerProjectileLifetime = 2f / 3f;
        public const float PlayerBaseRange = 8f * PlayerProjectileLifetime;
        // 5 speed × 1.8 s = 9 units (detection 8 + 1).
        public const float RangedProjectileLifetime = 1.8f;
        // 6.5 speed × 1.4 s ≈ 9 units; it shoots while moving, so its range stays short.
        public const float QuickRangedProjectileLifetime = 1.4f;
        // 11 speed × 1.3 s ≈ 14 units; the sniper covers most of the room width.
        public const float SniperProjectileLifetime = 1.3f;
        // 7 speed × 1.7 s ≈ 12 units.
        public const float ArcherProjectileLifetime = 1.7f;
        // 7 speed × 1.4 s ≈ 10 units.
        public const float MageProjectileLifetime = 1.4f;

        [MenuItem("Trickal Fan Game/Week 21/Setup Range-0 Projectile Lifetimes")]
        public static void Setup()
        {
            ConfigureLifetime<RangedEnemyController>(RangedPrefabPath, RangedProjectileLifetime);
            ConfigureLifetime<MobileRangedEnemyController>(Week19Spawn2Setup.QuickRangedPrefabPath,
                QuickRangedProjectileLifetime);
            ConfigureLifetime<LongRangeSniperController>(Week15Enemy4Setup.SniperPrefabPath, SniperProjectileLifetime);
            ConfigureLifetime<LongRangeSniperController>(ArcherPrefabPath, ArcherProjectileLifetime);
            ConfigureLifetime<LongRangeSniperController>(MagePrefabPath, MageProjectileLifetime);
            AssetDatabase.SaveAssets();
            Debug.Log("Range-0 ready: projectile lifetimes set so travel distance = flight time × shot speed " +
                      $"(player {PlayerProjectileLifetime}s, ranged {RangedProjectileLifetime}s, quick ranged " +
                      $"{QuickRangedProjectileLifetime}s, sniper {SniperProjectileLifetime}s, archer " +
                      $"{ArcherProjectileLifetime}s, mage {MageProjectileLifetime}s); boss barrages unchanged.");
        }

        private static void ConfigureLifetime<T>(string path, float lifetime) where T : MonoBehaviour
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                throw new InvalidOperationException($"Range-0 is missing Prefab '{path}'.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                T controller = root.GetComponent<T>();
                if (controller == null)
                {
                    throw new InvalidOperationException($"Range-0 requires {typeof(T).Name} on '{path}'.");
                }

                SerializedObject serializedController = new(controller);
                SerializedProperty property = serializedController.FindProperty("projectileLifetime");
                if (property == null)
                {
                    throw new InvalidOperationException($"{typeof(T).Name} has no projectileLifetime field.");
                }

                if (Mathf.Approximately(property.floatValue, lifetime))
                {
                    return;
                }

                property.floatValue = lifetime;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException($"Range-0 could not save Prefab '{path}'.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
