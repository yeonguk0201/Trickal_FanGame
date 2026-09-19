using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week7EnemyBalanceSetup
    {
        private const string ChaserPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        private const string RangedPrefabPath = "Assets/Prefabs/RangedEnemy.prefab";
        private const string ChargingPrefabPath = "Assets/Prefabs/ChargingEnemy.prefab";

        [MenuItem("Trickal Fan Game/Setup Phase E-4 Enemy Balance")]
        public static void Setup()
        {
            if (!ValidateContracts())
            {
                return;
            }

            UpdateChaser();
            UpdateRanged();
            UpdateCharging();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Phase E-4 enemy balance ready: Chaser HP 6 / contact 2 / melee 3 / speed 2.25, " +
                "Ranged HP 3 / damage 2 / speed 1.5, Charging HP 7 / damage 3 / dash speed 8.");
        }

        private static bool ValidateContracts()
        {
            GameObject chaser = AssetDatabase.LoadAssetAtPath<GameObject>(ChaserPrefabPath);
            GameObject ranged = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefabPath);
            GameObject charging = AssetDatabase.LoadAssetAtPath<GameObject>(ChargingPrefabPath);
            bool valid = chaser != null && chaser.GetComponent<Health>() != null &&
                         chaser.GetComponent<EnemyChase>() != null &&
                         chaser.GetComponent<ContactDamage>() != null &&
                         ranged != null && ranged.GetComponent<Health>() != null &&
                         ranged.GetComponent<RangedEnemyController>() != null &&
                         charging != null && charging.GetComponent<Health>() != null &&
                         charging.GetComponent<ChargingEnemyController>() != null;
            if (!valid)
            {
                Debug.LogError(
                    "Phase E-4 requires completed E-1, E-2, and E-3 prefabs. Run both enemy Setup menus first.");
            }

            return valid;
        }

        private static void UpdateChaser()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(ChaserPrefabPath);
            try
            {
                SetMaxHealth(contents.GetComponent<Health>(), 6);
                contents.GetComponent<EnemyChase>().Configure(2.25f, 6f, 0.8f);
                contents.GetComponent<ContactDamage>().Configure(2, 1f);
                contents.GetComponent<MeleeEnemyAttack>()?.Configure(1.15f, 0.4f, 0.12f, 0.65f, 0.2f, 3f);
                Save(contents, ChaserPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void UpdateRanged()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(RangedPrefabPath);
            try
            {
                SetMaxHealth(contents.GetComponent<Health>(), 3);
                contents.GetComponent<RangedEnemyController>()
                    .Configure(1.5f, 8f, 3f, 6f, 1.5f, 5f, 2, 4f);
                Save(contents, RangedPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void UpdateCharging()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(ChargingPrefabPath);
            try
            {
                SetMaxHealth(contents.GetComponent<Health>(), 7);
                contents.GetComponent<ChargingEnemyController>()
                    .Configure(7f, 0.65f, 8f, 0.8f, 0.6f, 1.5f, 3);
                Save(contents, ChargingPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void SetMaxHealth(Health health, int maxHealth)
        {
            SerializedObject serializedHealth = new(health);
            serializedHealth.FindProperty("maxHealth").floatValue = maxHealth;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Save(GameObject contents, string path)
        {
            if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
            {
                throw new UnityException($"Failed to save balanced enemy prefab at {path}.");
            }
        }
    }
}
