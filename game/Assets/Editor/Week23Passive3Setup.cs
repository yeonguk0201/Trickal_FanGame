using TrickalFanGame.Item;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Passive-3: creates 칸타의 팽이 (basic attack shots bounce between enemies, Passive-0 §4.4) and adds it to the
    // selection reward pool. Re-running updates the same asset, keeps its GUID and leaves the pool unchanged.
    // Balance (user decision, 2026-11-13): no stack limit, the first stack costs 15% basic attack damage, every
    // bounce keeps 50% (to another enemy) or 35% (back to the same enemy) of the damage, compounding, and a hit after
    // a bounce pushes with 25% knockback (ProjectileBounceSettings.BouncedKnockbackScale).
    public static class Week23Passive3Setup
    {
        public const string Piece = "Passive-3";
        public const string TopId = "artifact-kanta-top";
        public const string TopName = "칸타의 팽이";
        public const int BounceCount = 2;
        public const float SearchRadius = 3.5f;
        public const float OtherEnemyDamageRatio = 0.5f;
        public const float RepeatDamageRatio = 0.35f;
        public const float SameTargetDelaySeconds = 0.15f;
        public const float BasicAttackDamagePenalty = 0.15f;
        // 0 = no stack limit (Pool-1 keeps it in Pool1StackLimitSetup.UnlimitedSpecialItemIds).
        public const int TopMaxStacks = 0;
        public const string TopDescription =
            "기본 공격 피해 -15% (첫 획득 시), 기본 공격이 적을 맞히면 가까운 다른 적에게 2회 튕김 (스택마다 +1회, " +
            "튕길 때마다 피해 -50%, 같은 적을 다시 맞히면 -65%, 튕긴 탄 넉백 25%)";

        [MenuItem("Trickal Fan Game/Week 23/Setup Passive-3 Kanta Top")]
        public static void Setup()
        {
            ArtifactSetupUtility.RequireEditMode(Piece);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Passive-3 Kanta Top");

            ItemDefinition top = ArtifactSetupUtility.ConfigureArtifact(TopId, TopName, ItemRarity.Rare, TopMaxStacks,
                new ItemEffectEntry(ItemEffectType.BounceBetweenEnemies,
                    configuredMagnitude: BasicAttackDamagePenalty,
                    configuredSecondaryMagnitude: RepeatDamageRatio, configuredIntegerAmount: BounceCount,
                    configuredRadius: SearchRadius, configuredIntervalSeconds: SameTargetDelaySeconds,
                    configuredScaleMultiplier: OtherEnemyDamageRatio));
            ArtifactSetupUtility.AddToSelectionPool(Piece, top);
            ArtifactSetupUtility.EnsureGlyphs(Piece, top);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Passive-3 setup complete: {TopName} ({TopId}) is in the selection reward pool.");
        }
    }
}
