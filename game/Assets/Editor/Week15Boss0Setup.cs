using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Boss0Setup
    {
        public const string BossPrefabPath = "Assets/Prefabs/TestBoss.prefab";
        public const float BossVisualScale = 1.8f;
        public const float BossColliderRadius = 0.36f;

        [MenuItem("Trickal Fan Game/Week 15/Setup Boss-0 Common Runtime")]
        public static void Setup()
        {
            Week15Enemy5Setup.Setup();
            GameObject contents = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            if (contents == null) throw new UnityException($"Missing Boss-0 prefab at {BossPrefabPath}.");

            try
            {
                BossController boss = contents.GetComponent<BossController>();
                CircleCollider2D collider = contents.GetComponent<CircleCollider2D>();
                if (boss == null || collider == null)
                    throw new UnityException("Boss-0 requires BossController and CircleCollider2D.");

                EnemyAttackPresentation presentation = contents.GetComponent<EnemyAttackPresentation>();
                if (presentation == null) presentation = contents.AddComponent<EnemyAttackPresentation>();

                contents.transform.localScale = new Vector3(BossVisualScale, BossVisualScale, 1f);
                collider.radius = BossColliderRadius;
                boss.ConfigureHud("임시 보스", 3);
                boss.ConfigureEncounterSeed(1);
                boss.ConfigurePatterns(new[]
                {
                    new BossPatternDefinition("aimed-shot", BossPatternExecution.AimedProjectile,
                        0.65f, 0.15f, 0.55f, 1.1f),
                    new BossPatternDefinition("delayed-shot", BossPatternExecution.AimedProjectile,
                        0.9f, 0.15f, 0.65f, 1.4f),
                    new BossPatternDefinition("quick-shot", BossPatternExecution.AimedProjectile,
                        0.4f, 0.1f, 0.75f, 1.2f),
                });

                if (PrefabUtility.SaveAsPrefabAsset(contents, BossPrefabPath) == null)
                    throw new UnityException("Failed to save the Boss-0 prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Week 15 Boss-0 setup complete: the temporary large boss now uses deterministic " +
                      "telegraph-active-recovery-cooldown patterns, three HUD phases, and owned-object cleanup.");
        }
    }
}
