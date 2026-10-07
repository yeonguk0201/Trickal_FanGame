using TrickalFanGame.Item;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Artifact-3: creates 아이시아의 지갑 (gold when acquired) as a golden chest exclusive artifact. It is dropped only
    // by a golden chest's special reward and never joins the selection reward pool or the shop stock.
    // Re-running updates the same asset and table and keeps their GUIDs.
    public static class Week23Artifact3Setup
    {
        public const string Piece = "Artifact-3";
        public const string WalletId = "artifact-aisia-wallet";
        public const string WalletName = "아이시아의 지갑";
        public const float WalletGold = 100f;
        public const string WalletDescription = "획득 시 골드 +100 (보유 한도 99까지)";

        [MenuItem("Trickal Fan Game/Week 23/Setup Artifact-3 Aisia Wallet")]
        public static void Setup()
        {
            ArtifactSetupUtility.RequireEditMode(Piece);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Artifact-3 Aisia Wallet");

            ItemDefinition wallet = ArtifactSetupUtility.ConfigureArtifact(WalletId, WalletName, ItemRarity.Rare, 1,
                new ItemEffectEntry(ItemEffectType.GainGoldOnAcquire, WalletGold));
            GoldenChestExclusivePool.ConfigureTable(Piece);
            ArtifactSetupUtility.EnsureGlyphs(Piece, wallet);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Artifact-3 setup complete: {WalletName} ({WalletId}) is a golden chest exclusive artifact.");
        }
    }
}
