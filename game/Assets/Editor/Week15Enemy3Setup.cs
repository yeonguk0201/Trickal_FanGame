using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Enemy3Setup
    {
        [MenuItem("Trickal Fan Game/Week 15/Setup Enemy-3 Pursuit Charger")]
        public static void Setup()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(Week15Enemy0Setup.ChargingPrefabPath);
            if (contents == null)
            {
                throw new UnityException($"Missing charging prefab at {Week15Enemy0Setup.ChargingPrefabPath}.");
            }

            try
            {
                ChargingEnemyController charging = contents.GetComponent<ChargingEnemyController>();
                if (charging == null)
                {
                    throw new UnityException("The Enemy-3 prefab requires ChargingEnemyController.");
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

                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = Week15EnemyRoleColorSetup.LowBloodSugarColor;
                }

                charging.ConfigurePursuitCharge(7f, 2.75f, 1.1f, 0.65f, 8f, 0.8f, 0.75f, EnemyDamageTier.Heavy);
                if (PrefabUtility.SaveAsPrefabAsset(contents, Week15Enemy0Setup.ChargingPrefabPath) == null)
                {
                    throw new UnityException("Failed to save the Enemy-3 charging prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Week 15 Enemy-3 setup complete: the charger pursues, telegraphs a locked direction, dashes, recovers, and resumes pursuit.");
        }
    }
}
