using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Melee-Lunge: Bulhyojason, Sansamo and the crayon axe minion start their swing from outside touching range and
    // step into it. The shield minion and the crumb minion keep the stationary swing.
    public static class Week22MeleeLungeSetup
    {
        public const float TriggerRange = 1.9f;
        public const float Distance = 1.5f;
        public const float AimLockLeadTime = 0.12f;
        public const float HitForwardOffset = 0.3f;
        public const float HitRadius = 0.75f;

        // Sansamo is a variant of the Bulhyojason prefab, so the base comes first.
        public static readonly string[] LungePrefabPaths =
        {
            Week15Enemy2Setup.BulhyojasonPrefabPath,
            Week15Enemy2Setup.SansamoPrefabPath,
            Week15Boss3Setup.AxePrefabPath,
        };

        public static readonly string[] StationaryPrefabPaths =
        {
            Week15Boss3Setup.ShieldPrefabPath,
            Week15Boss1Setup.MinionPrefabPath,
        };

        [MenuItem("Trickal Fan Game/Week 22/Setup Melee Lunge")]
        public static void Setup()
        {
            foreach (string path in LungePrefabPaths)
            {
                ConfigurePrefab(path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "Melee-Lunge setup complete: Bulhyojason, Sansamo and the crayon axe minion telegraph from " +
                $"{TriggerRange} units, lock their direction {AimLockLeadTime}s before the swing and lunge {Distance} units.");
        }

        private static void ConfigurePrefab(string path)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null)
            {
                throw new UnityException($"Missing Melee-Lunge prefab at {path}.");
            }

            try
            {
                MeleeEnemyAttack melee = contents.GetComponent<MeleeEnemyAttack>();
                if (melee == null)
                {
                    throw new UnityException($"Melee-Lunge prefab {path} has no MeleeEnemyAttack.");
                }

                if (melee.IsLunge &&
                    Mathf.Approximately(melee.LungeTriggerRange, TriggerRange) &&
                    Mathf.Approximately(melee.LungeDistance, Distance) &&
                    Mathf.Approximately(melee.AimLockLeadTime, AimLockLeadTime) &&
                    Mathf.Approximately(melee.LungeHitForwardOffset, HitForwardOffset) &&
                    Mathf.Approximately(melee.LungeHitRadius, HitRadius))
                {
                    return;
                }

                melee.ConfigureLunge(TriggerRange, Distance, AimLockLeadTime, HitForwardOffset, HitRadius);
                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                {
                    throw new UnityException($"Failed to save Melee-Lunge prefab at {path}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
