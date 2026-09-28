using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Enemy4Setup
    {
        public const string SniperPrefabPath = "Assets/Prefabs/HighBloodSugarFairy.prefab";

        [MenuItem("Trickal Fan Game/Week 15/Setup Enemy-4 Long-Range Sniper")]
        public static void Setup()
        {
            CreatePrefabIfMissing();
            ConfigurePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Week 15 Enemy-4 setup complete: the original ranged prefab is preserved and the high-blood-sugar " +
                "fairy uses relocate-aim-fire-recovery sniper behavior for large rooms.");
        }

        private static void CreatePrefabIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SniperPrefabPath) != null)
            {
                return;
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.RangedPrefabPath);
            if (source == null)
            {
                throw new UnityException($"Missing source ranged prefab at {Week15Enemy0Setup.RangedPrefabPath}.");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = "HighBloodSugarFairy";
                if (PrefabUtility.SaveAsPrefabAsset(instance, SniperPrefabPath) == null)
                {
                    throw new UnityException($"Failed to create Enemy-4 prefab at {SniperPrefabPath}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void ConfigurePrefab()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(SniperPrefabPath);
            if (contents == null)
            {
                throw new UnityException($"Missing Enemy-4 prefab at {SniperPrefabPath}.");
            }

            try
            {
                contents.name = "HighBloodSugarFairy";
                RangedEnemyController ranged = contents.GetComponent<RangedEnemyController>();
                if (ranged != null)
                {
                    Object.DestroyImmediate(ranged, true);
                }

                LongRangeSniperController sniper = contents.GetComponent<LongRangeSniperController>();
                if (sniper == null)
                {
                    sniper = contents.AddComponent<LongRangeSniperController>();
                }
                if (contents.GetComponent<EnemyBehaviorContext>() == null)
                {
                    contents.AddComponent<EnemyBehaviorContext>();
                }
                if (contents.GetComponent<EnemyAttackPresentation>() == null)
                {
                    contents.AddComponent<EnemyAttackPresentation>();
                }
                if (contents.GetComponent<LineRenderer>() == null)
                {
                    contents.AddComponent<LineRenderer>();
                }

                sniper.Configure(2.5f, 14f, 8f, 1f, 1f, 0.3f, 0.7f, 0.08f, 0.65f, 7f, EnemyDamageTier.Heavy, 5f);
                Health health = contents.GetComponent<Health>();
                if (health == null)
                {
                    throw new UnityException("Enemy-4 prefab requires Health.");
                }
                SerializedObject serializedHealth = new(health);
                serializedHealth.FindProperty("maxHealth").floatValue = 30f;
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();

                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = Week15EnemyRoleColorSetup.HighBloodSugarColor;
                }

                if (PrefabUtility.SaveAsPrefabAsset(contents, SniperPrefabPath) == null)
                {
                    throw new UnityException("Failed to save the Enemy-4 sniper prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
