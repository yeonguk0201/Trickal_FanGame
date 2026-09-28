using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Hp2HeartHudVerification
    {
        // The heart row lives in the HUD-1 Survival HUD, so HUD-1 Setup/Verification own its structure.
        [MenuItem("Trickal Fan Game/HP/Verify HP-2 Heart HUD")]
        public static void Verify()
        {
            Week13Hud1Verification.Verify();
        }

        // HUD-1 Setup restores its own skill slot, which HUD-2 Setup then replaces; run both in order.
        [MenuItem("Trickal Fan Game/HP/Setup HP-2 Heart HUD")]
        public static void Setup()
        {
            Week13Hud1Setup.Setup();
            Week13Hud2Setup.Setup();
        }

        public static void SetupAndVerifyBatch()
        {
            Setup();
            Setup();
            Week13Hud1Verification.Verify();
            Week13Hud2Verification.Verify();
        }

        public static void VerifyBatch()
        {
            (string Name, Action Run)[] checks =
            {
                ("HUD-1 setup twice + heart HUD", Week13Hud1Verification.SetupAndVerifyBatch),
                ("HUD-2~5 setup chain", Week13Hud5Verification.SetupAndVerifyBatch),
                ("HUD-7D chain", Week13Hud7DVerification.SetupAndVerifyBatch),
                ("Setting-1C display", Week13Setting1CVerification.SetupAndVerifyBatch),
                ("Reward-2 cards", Week16Reward2Verification.SetupAndVerifyBatch),
                ("Reward-3 rooms", Week16Reward3Verification.SetupAndVerifyBatch),
                ("HP-4 life gem", Hp4LifeGemVerification.Verify),
                ("HP-1 regression", Hp1HealthUnitsVerification.VerifyRegressionBatch),
            };

            List<string> failures = new();
            foreach ((string name, Action run) in checks)
            {
                try
                {
                    run();
                    Debug.Log($"[HP-2 regression] PASS {name}");
                }
                catch (Exception exception)
                {
                    Exception root = exception.GetBaseException();
                    failures.Add($"{name}: {root.GetType().Name}: {root.Message}");
                    Debug.LogError($"[HP-2 regression] FAIL {name}: {root}");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"HP-2 regression failed ({failures.Count}/{checks.Length}):\n" + string.Join("\n", failures));
            }

            Debug.Log($"[HP-2 regression] All {checks.Length} checks passed.");
        }
    }
}
