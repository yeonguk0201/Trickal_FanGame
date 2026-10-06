using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week16Test1FeedbackVerification
    {
        [MenuItem("Trickal Fan Game/Week 16/Verify Test-1 Feedback Fixes")]
        public static void SetupAndVerifyBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            PhaseGArtifactContractSetup.Setup();
            Week7ChargingEnemySetup.Setup();
            Week7HighGradeSkillSetup.Setup();
            AssetDatabase.SaveAssets();

            PhaseGArtifactContractVerification.Verify();
            PhaseGProjectileEffectsVerification.Verify();
            PhaseGDamageAuraVerification.Verify();
            Week7ChargingEnemyVerification.Verify();
            Week7HighGradeSkillVerification.Verify();
            Hp4LifeGemVerification.Verify();

            Debug.Log("Week 16 Test-1 feedback fixes passed: telescope distance scaling, charging-enemy " +
                      "contact damage, per-floor Life Gem recovery, cancellable high-grade skill with 80% " +
                      "cooldown, and the expanded visible Mask aura remain valid together.");
        }
    }
}
