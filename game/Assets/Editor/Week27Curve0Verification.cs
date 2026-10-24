using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Curve-0: normal enemies keep their base health on every floor and only bosses keep the floor health
    // multiplier (x1.0/x1.5/x2.0). Enemy damage no longer rises every floor: floors 1-2 share the base row and
    // floor 3 steps up once, to the former floor 2 values.
    public static class Week27Curve0Verification
    {
        [MenuItem("Trickal Fan Game/Week 27/Verify Curve-0 Difficulty Curve")]
        public static void Verify()
        {
            RunSections(typeof(Week8FloorDifficultyVerification),
                "VerifyMultiplierValues", "VerifyChaserScaling", "VerifyRangedScaling",
                "VerifyChargingScaling", "VerifyBossScaling");
            ValidateNormalAndBossOnFloor3();
            Hp1HealthUnitsVerification.Verify();
            Debug.Log("Curve-0 verification passed: normal enemies keep base health on floors 1-3, bosses keep " +
                      "x1.0/x1.5/x2.0, and enemy damage is the base row on floors 1-2 and one step up on floor 3 " +
                      "(light 1/1/1, medium 1.5/1.5/2, heavy 2/2/3, critical 3/3/3.5 hearts).");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Curve-0 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();

            // HP-1's batch covers every verifier that reads the damage tiers. The scene checks open the Game Scene
            // and store state in it, so each starts from a freshly loaded scene. Every check runs even after a
            // failure so one report lists them all.
            (string Name, Action Run, bool OpensScene)[] checks =
            {
                ("HP-1 regression", Hp1HealthUnitsVerification.VerifyRegressionBatch, false),
                ("Week23Enemy6", Week23Enemy6Verification.Verify, true),
                ("Week25Enemy9", Week25Enemy9Verification.Verify, true),
                ("Week19Tune1", Week19Tune1Verification.Verify, true),
            };
            List<string> failures = new();
            foreach ((string name, Action run, bool opensScene) in checks)
            {
                try
                {
                    if (opensScene) EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
                    run();
                    Debug.Log($"[Curve-0 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[Curve-0 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Curve-0 regression failed ({failures.Count}/{checks.Length}):\n" +
                    string.Join("\n", failures));
            }

            Debug.Log("Curve-0 regression verification passed.");
        }

        private static void ValidateNormalAndBossOnFloor3()
        {
            GameObject normal = new("Curve-0 Normal Enemy");
            GameObject boss = new("Curve-0 Boss");
            try
            {
                Health normalHealth = CreateHealth(normal, 40f);
                Health bossHealth = CreateHealth(boss, 300f);
                boss.AddComponent<BossController>();

                FloorDifficultyScaler.ApplyScaling(normal, 3);
                FloorDifficultyScaler.ApplyScaling(boss, 3);
                Assert(Mathf.Approximately(normalHealth.MaxHealth, 40f) &&
                       Mathf.Approximately(normalHealth.CurrentHealth, 40f),
                    $"A floor 3 normal enemy must keep its 40 base health, got {normalHealth.MaxHealth}.");
                Assert(Mathf.Approximately(bossHealth.MaxHealth, 600f) &&
                       Mathf.Approximately(bossHealth.CurrentHealth, 600f),
                    $"A floor 3 boss must double its 300 base health, got {bossHealth.MaxHealth}.");
                Assert(EnemyFloorLevel.Resolve(normal) == 3 && EnemyFloorLevel.Resolve(boss) == 3,
                    "Both must still record floor 3 for tiered damage.");
            }
            finally
            {
                Object.DestroyImmediate(normal);
                Object.DestroyImmediate(boss);
            }
        }

        private static Health CreateHealth(GameObject owner, float maxHealth)
        {
            Health health = owner.AddComponent<Health>();
            SerializedObject serialized = new(health);
            serialized.FindProperty("maxHealth").floatValue = maxHealth;
            serialized.FindProperty("useHealthUnits").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            health.ResetHealth();
            return health;
        }

        private static void RunSections(Type verifier, params string[] methodNames)
        {
            foreach (string methodName in methodNames)
            {
                MethodInfo method = verifier.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
                Assert(method != null, $"{verifier.Name}.{methodName} was not found.");
                method.Invoke(null, null);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
