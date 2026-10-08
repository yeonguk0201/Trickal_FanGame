using TrickalFanGame.Item;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Passive-4: creates 샤샤의 항아리 (the basic attack becomes a water stream, Passive-0 §4.5) and adds it to the
    // selection reward pool. Re-running updates the same asset, keeps its GUID and leaves the pool unchanged.
    public static class Week23Passive4Setup
    {
        public const string Piece = "Passive-4";
        public const string JarId = "artifact-shasha-jar";
        public const string JarName = "샤샤의 항아리";
        public const float StreamWidth = 0.2f;
        public const float KnockbackShare = 0.3f;
        public const string JarDescription = "기본 공격이 벽까지 뻗는 물줄기로 변경 (줄기 위의 모든 적과 장애물을 타격)";

        [MenuItem("Trickal Fan Game/Week 23/Setup Passive-4 Shasha Jar")]
        public static void Setup()
        {
            ArtifactSetupUtility.RequireEditMode(Piece);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Passive-4 Shasha Jar");

            ItemDefinition jar = ArtifactSetupUtility.ConfigureArtifact(JarId, JarName, ItemRarity.Epic, 1,
                new ItemEffectEntry(ItemEffectType.WaterStreamAttack, StreamWidth,
                    configuredSecondaryMagnitude: KnockbackShare));
            ArtifactSetupUtility.AddToSelectionPool(Piece, jar);
            ArtifactSetupUtility.EnsureGlyphs(Piece, jar);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Passive-4 setup complete: {JarName} ({JarId}) is in the selection reward pool.");
        }
    }
}
