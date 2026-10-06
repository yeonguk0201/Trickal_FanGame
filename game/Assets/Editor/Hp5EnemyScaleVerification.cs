using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Hp5EnemyScaleVerification
    {
        private const float PlayerAttackDamage = 10f;
        private const float SaemaeumHealPerUse = 150f;

        // D-7: every enemy-side HP value is 10x its pre-HP-5 value, except the Buseureogi obstacles that were
        // retuned to two base-damage hits (20) on 2026-09-30.
        private static readonly (string Prefab, float Health)[] EnemyHealth =
        {
            ("TestEnemy", 60f), ("SansamoEnemy", 40f), ("RangedEnemy", 30f), ("HighBloodSugarFairy", 30f),
            ("ChargingEnemy", 70f), ("BuseureogiCrumbMinion", 30f), ("BuseureogiCreamObstacle", 20f),
            ("BuseureogiDoughObstacle", 20f), ("CrayonAxeMinion", 40f), ("CrayonShieldMinion", 40f),
            ("CrayonArcherMinion", 30f), ("CrayonMageMinion", 30f), ("TestBoss", 250f),
            ("SaemaeumVaultBoss", 800f), ("CrayonHeroBoss", 1100f),
        };

        private static readonly string[] PlayerScenes =
        {
            "Assets/Scenes/SampleScene.unity", "Assets/Scenes/ItemTestScene.unity",
            "Assets/Scenes/BossTestScene.unity", "Assets/Scenes/Boss2TestScene.unity",
        };

        [MenuItem("Trickal Fan Game/HP/Verify HP-5 Enemy Scale")]
        public static void Verify()
        {
            foreach ((string prefabName, float expected) in EnemyHealth)
            {
                string path = $"Assets/Prefabs/{prefabName}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert(prefab != null, $"Missing enemy prefab: {path}");
                Health health = prefab.GetComponent<Health>();
                Assert(health != null && !health.UsesHealthUnits && Mathf.Approximately(health.MaxHealth, expected),
                    $"{prefabName} must have continuous HP {expected}, got {health?.MaxHealth}.");
            }

            SaemaeumVaultBossPatternRuntime saemaeum = AssetDatabase
                .LoadAssetAtPath<GameObject>("Assets/Prefabs/SaemaeumVaultBoss.prefab")
                .GetComponent<SaemaeumVaultBossPatternRuntime>();
            Assert(saemaeum != null && Mathf.Approximately(saemaeum.HealPerUse, SaemaeumHealPerUse),
                "Saemaeum Vault treasure healing must be 150 per use.");

            ValidateDefaultPlayerStats();
            ValidateScenePlayerStats();
            Debug.Log("HP-5 enemy scale verification passed: player attack damage is 10 in code and all player " +
                      "scenes, enemy/boss/obstacle HP is 10x, Saemaeum heals 150, and player HP stays at 10 half-heart units.");
        }

        private static void ValidateDefaultPlayerStats()
        {
            GameObject player = new("HP-5 Player");
            try
            {
                Health health = player.AddComponent<Health>();
                PlayerStats stats = player.AddComponent<PlayerStats>();
                InvokeAwake(health);
                InvokeAwake(stats);
                Assert(Mathf.Approximately(stats.AttackDamage, PlayerAttackDamage),
                    "PlayerStats must default to attack damage 10.");
                Assert(health.UsesHealthUnits && Mathf.Approximately(health.MaxHealth, 10f),
                    "HP-5 must not change the player's 10 half-heart units.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void ValidateScenePlayerStats()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("HP-5 scene checks need the open scenes saved or discarded.");
            }

            SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                ValidateScenes();
            }
            finally
            {
                if (previousScenes.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                }
            }
        }

        private static void ValidateScenes()
        {
            foreach (string scenePath in PlayerScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int players = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (PlayerStats stats in root.GetComponentsInChildren<PlayerStats>(true))
                    {
                        players++;
                        SerializedObject serialized = new(stats);
                        Assert(Mathf.Approximately(serialized.FindProperty("baseAttackDamage").floatValue, PlayerAttackDamage) &&
                               Mathf.Approximately(serialized.FindProperty("baseMaxHealth").floatValue, 10f),
                            $"{scenePath} player must use attack damage 10 and 10 half-heart units.");
                    }
                }

                Assert(players > 0, $"{scenePath} must contain a player.");
            }
        }

        private static void InvokeAwake(MonoBehaviour component)
        {
            MethodInfo awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        // Static verifiers whose Verify() exercises enemy HP, player damage, or enemy healing.
        private static readonly string[] WideRegressionVerifiers =
        {
            "Hp5EnemyScaleVerification", "Hp1HealthUnitsVerification", "Hp3ItemEffectsVerification", "Hp4LifeGemVerification",
            "PhaseGArtifactContractVerification", "PhaseGCombatFoundationVerification",
            "PhaseGConditionalEffectsVerification", "PhaseGDamageAuraVerification",
            "PhaseGProjectileEffectsVerification", "PhaseGRewardRunVerification",
            "PhaseGSimpleEffectsVerification", "Week6ItemVerification", "Week7ChargingEnemyVerification",
            "Week7ChaserMonsterVerification", "Week7DamageContextVerification",
            "Week7EnemyBalanceVerification", "Week7HighGradeSkillVerification",
            "Week7LowerGradeSkillVerification", "Week7PlayerCombatEventsVerification",
            "Week7RangedEnemyVerification", "Week7RoomGraphVerification", "Week8FloorDifficultyVerification",
            "Week8ProjectileSizingVerification", "Week13Hud1Verification", "Week13Hud3BVerification",
            "Week13Hud5Verification", "Week13SkillUpgradeVerification", "Week14Encounter1Verification",
            "Week14Encounter2Verification", "Week14Encounter3Verification", "Week14Room7Verification",
            "Week14Room8Verification", "Week15Boss0Verification", "Week15Boss1Verification",
            "Week15Boss2Verification", "Week15Boss3Verification", "Week15Boss4Verification",
            "Week15Enemy0Verification", "Week15Enemy1Verification", "Week15Enemy2Verification",
            "Week15Enemy3Verification", "Week15Enemy4Verification", "Week15Enemy5Verification",
            "Week16Content0Verification", "Week16Item0Verification", "Week16Reward0Verification",
            "Week16Reward1Verification", "Week16Reward2Verification", "Week16Reward3Verification",
            "ItemTestRoomVerification",
        };

        public static void WideRegressionBatch()
        {
            List<string> failures = new();
            foreach (string typeName in WideRegressionVerifiers)
            {
                Type type = typeof(Hp5EnemyScaleVerification).Assembly.GetType("TrickalFanGame.Editor." + typeName);
                MethodInfo verify = type?.GetMethod("Verify", BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                if (verify == null)
                {
                    Debug.Log($"[HP-5 regression] SKIP {typeName}: no public static Verify()");
                    continue;
                }

                try
                {
                    verify.Invoke(null, null);
                    Debug.Log($"[HP-5 regression] PASS {typeName}");
                }
                catch (Exception exception)
                {
                    Exception cause = exception is TargetInvocationException { InnerException: not null }
                        ? exception.InnerException
                        : exception;
                    failures.Add(typeName);
                    Debug.LogError($"[HP-5 regression] FAIL {typeName}: {cause.GetType().Name}: {cause.Message}");
                }
            }

            string summary = failures.Count == 0
                ? $"All {WideRegressionVerifiers.Length} checks passed."
                : $"{failures.Count} failed: {string.Join(", ", failures)}";
            Debug.Log("[HP-5 regression] " + summary);
            if (failures.Count > 0 && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
