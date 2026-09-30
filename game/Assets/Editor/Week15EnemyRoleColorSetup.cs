using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15EnemyRoleColorSetup
    {
        public static readonly Color BulhyojasonColor = new(0.72f, 0.28f, 0.08f, 1f);
        public static readonly Color SansamoColor = new(0.25f, 0.85f, 0.2f, 1f);
        public static readonly Color LowBloodSugarColor = new(0.2f, 0.55f, 1f, 1f);
        public static readonly Color HighBloodSugarColor = new(1f, 0.25f, 0.7f, 1f);
        public static readonly Color LegacyBossColor = new(0.95f, 0.55f, 0.2f, 1f);

        [MenuItem("Trickal Fan Game/Week 15/Setup Temporary Enemy Role Colors")]
        public static void Setup()
        {
            ApplyColor(Week15Enemy2Setup.BulhyojasonPrefabPath, BulhyojasonColor);
            ApplyColor(Week15Enemy2Setup.SansamoPrefabPath, SansamoColor);
            ApplyColor(Week15Enemy0Setup.ChargingPrefabPath, LowBloodSugarColor);
            ApplyColor(Week15Enemy4Setup.SniperPrefabPath, HighBloodSugarColor);
            ApplyColor(Week15Enemy2Setup.LegacyBossPrefabPath, LegacyBossColor);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Week 15 temporary enemy colors applied: Bulhyojason=brown, Sansamo=green, " +
                "LowBloodSugar=blue, HighBloodSugar=pink.");
        }

        private static void ApplyColor(string path, Color color)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null)
            {
                throw new UnityException($"Missing enemy prefab at {path}.");
            }

            try
            {
                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    throw new UnityException($"Enemy prefab at {path} requires a SpriteRenderer.");
                }

                renderer.color = color;
                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                {
                    throw new UnityException($"Failed to save enemy role color at {path}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
