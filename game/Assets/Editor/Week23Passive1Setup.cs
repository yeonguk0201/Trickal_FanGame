using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Passive-1: creates 칸나의 대포 (bigger basic attack shots) and 비비의 콧물 (poison on basic attack hits), adds both
    // to the selection reward pool, and gives every enemy Prefab its own basic attack knockback weight (Passive-0
    // §4.6). Re-running updates the same assets, keeps their GUIDs and leaves configured Prefabs and the pool unchanged.
    public static class Week23Passive1Setup
    {
        public const string CannonId = "artifact-kanna-cannon";
        public const string CannonName = "칸나의 대포";
        public const float CannonSizeBonus = 0.5f;
        public const int CannonMaxStacks = 2;

        public const string SnotId = "artifact-bibi-snot";
        public const string SnotName = "비비의 콧물";
        public const float PoisonChance = 0.15f;
        public const float PoisonTickDamageRatio = 0.15f;
        public const float PoisonDurationSeconds = 4f;
        public const float PoisonIntervalSeconds = 1f;
        public const int PoisonMaxStacks = 3;
        public const int SnotMaxStacks = 2;

        public const float BossKnockbackWeight = 4f;

        // The knockback weight of every enemy Prefab. A heavier enemy is pushed less (push = formula ÷ weight).
        public static readonly (string Path, float Weight)[] KnockbackWeights =
        {
            ("Assets/Prefabs/JyubiEnemy.prefab", 0.5f),
            ("Assets/Prefabs/BuseureogiCrumbMinion.prefab", 0.8f),
            ("Assets/Prefabs/QuickRangedFairy.prefab", 0.8f),
            ("Assets/Prefabs/CrayonMageMinion.prefab", 0.9f),
            ("Assets/Prefabs/SansamoEnemy.prefab", 0.9f),
            ("Assets/Prefabs/RangedEnemy.prefab", 1f),
            ("Assets/Prefabs/HighBloodSugarFairy.prefab", 1f),
            ("Assets/Prefabs/CrayonArcherMinion.prefab", 1f),
            ("Assets/Prefabs/TestEnemy.prefab", 1f),
            ("Assets/Prefabs/CrayonAxeMinion.prefab", 1.3f),
            ("Assets/Prefabs/ChargingEnemy.prefab", 1.5f),
            ("Assets/Prefabs/CrayonShieldMinion.prefab", 2f),
            ("Assets/Prefabs/TestBoss.prefab", BossKnockbackWeight),
            ("Assets/Prefabs/SaemaeumVaultBoss.prefab", BossKnockbackWeight),
            ("Assets/Prefabs/CrayonHeroBoss.prefab", BossKnockbackWeight),
        };

        public static string ItemPath(string itemId) => $"Assets/Items/{itemId}.asset";

        [MenuItem("Trickal Fan Game/Week 23/Setup Passive-1 Cannon and Snot")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Passive-1 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Passive-1 Cannon and Snot");

            ItemDefinition cannon = ConfigureItem(CannonId, CannonName, CannonMaxStacks,
                new ItemEffectEntry(ItemEffectType.ProjectileSizePercent, CannonSizeBonus));
            ItemDefinition snot = ConfigureItem(SnotId, SnotName, SnotMaxStacks,
                new ItemEffectEntry(ItemEffectType.BasicAttackPoison, PoisonChance,
                    configuredSecondaryMagnitude: PoisonTickDamageRatio,
                    configuredIntegerAmount: PoisonMaxStacks,
                    configuredIntervalSeconds: PoisonIntervalSeconds,
                    configuredDurationSeconds: PoisonDurationSeconds));
            ConfigureKnockbackWeights();
            AddToSelectionPool(cannon, snot);
            EnsureGlyphs(cannon, snot);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Passive-1 setup complete: {CannonName} ({CannonId}) and {SnotName} ({SnotId}) are in the " +
                      $"selection reward pool and {KnockbackWeights.Length} enemy Prefabs have a knockback weight.");
        }

        private static ItemDefinition ConfigureItem(string itemId, string displayName, int maxStacks,
            ItemEffectEntry effect)
        {
            string path = ItemPath(itemId);
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = itemId;
                AssetDatabase.CreateAsset(definition, path);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + itemId);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + itemId);
            }

            definition.ConfigureContract(itemId, displayName, ItemKind.Artifact, ItemRarity.Rare, true, maxStacks,
                effect);
            if (!definition.IsValid)
                throw new InvalidOperationException($"{itemId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ConfigureKnockbackWeights()
        {
            foreach ((string path, float weight) in KnockbackWeights)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                KnockbackReceiver configured = prefab != null ? prefab.GetComponent<KnockbackReceiver>() : null;
                if (configured == null)
                    throw new InvalidOperationException($"Passive-1 needs an enemy Prefab with a KnockbackReceiver: {path}.");
                if (Mathf.Approximately(configured.KnockbackWeight, weight)) continue;

                // The scope also saves a Prefab Variant as an override on its base.
                using PrefabUtility.EditPrefabContentsScope scope = new(path);
                scope.prefabContentsRoot.GetComponent<KnockbackReceiver>().ConfigureWeight(weight);
            }
        }

        private static void AddToSelectionPool(params ItemDefinition[] items)
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            ItemDefinition[] pool = assembler.SelectionRewardPool.ToArray();
            if (items.All(pool.Contains)) return;

            // Same order as the Reward-3 setup that builds the whole pool.
            ItemDefinition[] extended = pool.Union(items)
                .OrderBy(definition => definition.ItemId, StringComparer.Ordinal).ToArray();
            Undo.RecordObject(assembler, "Add Passive-1 artifacts to the reward pool");
            assembler.ConfigureSelectionRewards(assembler.RewardSelectionSession, extended);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Passive-1 setup.");
        }

        // The acquisition toast, reward card and pause list show the name and effect description.
        private static void EnsureGlyphs(params ItemDefinition[] items)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Passive-1 requires the Frontend TMP font.");
            Week13Hud3BSetup.EnsureDescriptionGlyphs(font);
            string characters = string.Concat(items.Select(item =>
                item.DisplayName + ArtifactEffectDescription.Build(item)));
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Passive-1 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
