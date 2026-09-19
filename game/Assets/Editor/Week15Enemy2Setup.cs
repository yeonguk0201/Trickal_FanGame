using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Enemy2Setup
    {
        public const string BulhyojasonPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        public const string SansamoPrefabPath = "Assets/Prefabs/SansamoEnemy.prefab";
        public const string LegacyBossPrefabPath = "Assets/Prefabs/TestBoss.prefab";

        [MenuItem("Trickal Fan Game/Week 15/Setup Enemy-2 Melee Chasers")]
        public static void Setup()
        {
            ConfigurePrefab(
                BulhyojasonPrefabPath,
                "BulhyojasonEnemy",
                maxHealth: 6f,
                moveSpeed: 2.25f,
                contactDamage: 2f,
                meleeDamage: 3f,
                color: Week15EnemyRoleColorSetup.BulhyojasonColor);

            CreateVariantIfMissing(BulhyojasonPrefabPath, SansamoPrefabPath, "SansamoEnemy");
            ConfigurePrefab(
                SansamoPrefabPath,
                "SansamoEnemy",
                maxHealth: 4f,
                moveSpeed: 3.5f,
                contactDamage: 0.75f,
                meleeDamage: 1.25f,
                color: Week15EnemyRoleColorSetup.SansamoColor);
            RemoveInheritedMeleeFromLegacyBoss();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Week 15 Enemy-2 setup complete: Bulhyojason is slow/strong and Sansamo is fast/weak; " +
                "both use the shared telegraph-active-recovery melee lifecycle.");
        }

        private static void RemoveInheritedMeleeFromLegacyBoss()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(LegacyBossPrefabPath);
            if (contents == null)
            {
                throw new UnityException($"Missing legacy boss prefab at {LegacyBossPrefabPath}.");
            }

            try
            {
                MeleeEnemyAttack melee = contents.GetComponent<MeleeEnemyAttack>();
                if (melee != null)
                {
                    Object.DestroyImmediate(melee, true);
                }

                EnemyAttackPresentation presentation = contents.GetComponent<EnemyAttackPresentation>();
                if (presentation != null)
                {
                    Object.DestroyImmediate(presentation, true);
                }

                if (PrefabUtility.SaveAsPrefabAsset(contents, LegacyBossPrefabPath) == null)
                {
                    throw new UnityException("Failed to isolate the legacy boss from Enemy-2 melee behavior.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void CreateVariantIfMissing(string sourcePath, string destinationPath, string objectName)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) != null)
            {
                return;
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                throw new UnityException($"Missing source chaser prefab at {sourcePath}.");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = objectName;
                if (PrefabUtility.SaveAsPrefabAsset(instance, destinationPath) == null)
                {
                    throw new UnityException($"Failed to create Enemy-2 prefab at {destinationPath}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void ConfigurePrefab(
            string path,
            string objectName,
            float maxHealth,
            float moveSpeed,
            float contactDamage,
            float meleeDamage,
            Color color)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null)
            {
                throw new UnityException($"Missing Enemy-2 prefab at {path}.");
            }

            try
            {
                contents.name = objectName;
                EnemyBehaviorContext behavior = contents.GetComponent<EnemyBehaviorContext>();
                if (behavior == null)
                {
                    behavior = contents.AddComponent<EnemyBehaviorContext>();
                }

                EnemyAttackPresentation presentation = contents.GetComponent<EnemyAttackPresentation>();
                if (presentation == null)
                {
                    presentation = contents.AddComponent<EnemyAttackPresentation>();
                }

                MeleeEnemyAttack melee = contents.GetComponent<MeleeEnemyAttack>();
                if (melee == null)
                {
                    melee = contents.AddComponent<MeleeEnemyAttack>();
                }

                Health health = contents.GetComponent<Health>();
                EnemyChase chase = contents.GetComponent<EnemyChase>();
                ContactDamage contact = contents.GetComponent<ContactDamage>();
                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                if (health == null || chase == null || contact == null || renderer == null)
                {
                    throw new UnityException($"Enemy-2 prefab contract is incomplete at {path}.");
                }

                SerializedObject serializedHealth = new(health);
                serializedHealth.FindProperty("maxHealth").floatValue = maxHealth;
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();
                chase.Configure(moveSpeed, 6f, 0.8f);
                contact.Configure(contactDamage, 1f);
                melee.Configure(1.15f, 0.4f, 0.12f, 0.65f, 0.2f, meleeDamage);
                renderer.color = color;

                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                {
                    throw new UnityException($"Failed to save Enemy-2 prefab at {path}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
