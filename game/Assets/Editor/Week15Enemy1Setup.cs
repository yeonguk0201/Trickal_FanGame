using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Enemy1Setup
    {
        [MenuItem("Trickal Fan Game/Week 15/Setup Enemy-1 Damage and Readability")]
        public static void Setup()
        {
            Week15Enemy0Setup.EnsureComponent<EnemyAttackPresentation>(
                Week15Enemy0Setup.ChargingPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Week 15 Enemy-1 setup complete: the charging enemy uses shared silhouette phases; " +
                "players receive shared hit invulnerability through PlayerMovement at runtime.");
        }
    }
}
