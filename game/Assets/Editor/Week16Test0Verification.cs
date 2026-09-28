using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week16Test0Verification
    {
        [MenuItem("Trickal Fan Game/Week 16/Verify Test-0 Full Regression")]
        public static void SetupAndVerifyBatch()
        {
            Week16Reward3Verification.SetupAndVerifyBatch();
            Week16Spell1Verification.Verify();
            Week16Artifact1Verification.Verify();
            PhaseGRewardRunVerification.Verify();
            Hp1HealthUnitsVerification.Verify();
            Hp2HeartHudVerification.Verify();
            Hp3ItemEffectsVerification.Verify();
            Hp4LifeGemVerification.Verify();
            Hp5EnemyScaleVerification.Verify();

            Debug.Log("Week 16 Test-0 full regression passed: deterministic unified rewards, cancellation and " +
                      "revisit persistence, duplicate-input protection, stack limits, room/boss integration, " +
                      "spell and artifact effects, acquisition-order Run records, and HP-1 through HP-5 core " +
                      "contracts remain valid together.");
        }
    }
}
