using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Enemy0Setup
    {
        public const string ChaserPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        public const string RangedPrefabPath = "Assets/Prefabs/RangedEnemy.prefab";
        public const string ChargingPrefabPath = "Assets/Prefabs/ChargingEnemy.prefab";

        [MenuItem("Trickal Fan Game/Week 15/Setup Enemy-0 Shared Behavior")]
        public static void Setup()
        {
            AddContext(ChaserPrefabPath);
            AddContext(RangedPrefabPath);
            AddContext(ChargingPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Week 15 Enemy-0 setup complete: all normal enemy prefabs use the shared alert, target, and movement-suppression context.");
        }

        internal static void EnsureComponent<T>(string path) where T : Component
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null)
            {
                throw new UnityException($"Missing prefab at {path}.");
            }

            try
            {
                if (contents.GetComponent<T>() == null)
                {
                    contents.AddComponent<T>();
                }

                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                {
                    throw new UnityException($"Failed to save prefab at {path}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void AddContext(string path)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null)
            {
                throw new UnityException($"Missing enemy prefab at {path}.");
            }

            try
            {
                if (contents.GetComponent<EnemyBehaviorContext>() == null)
                {
                    contents.AddComponent<EnemyBehaviorContext>();
                }

                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                {
                    throw new UnityException($"Failed to save Enemy-0 prefab at {path}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
