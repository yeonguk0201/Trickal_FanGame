using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Jjangsem-2: creates the four jjangsem spells of plan 26 §4.2 (파스텔빛, 수상한 물약, 이성의 끈, 깜짝상자) and
    // rebuilds the chest content table so diamond chests can hold them (Chest-2 gathers every active, valid jjangsem
    // spell). Re-running updates the same assets and table, keeping their GUIDs.
    public static class Week26Jjangsem2Setup
    {
        public const string PastelLightId = "jjangsem-pastel-light";
        public const string PastelLightName = "파스텔빛";
        public const string SuspiciousPotionId = "jjangsem-suspicious-potion";
        public const string SuspiciousPotionName = "수상한 물약";
        public const string ThreadOfReasonId = "jjangsem-thread-of-reason";
        public const string ThreadOfReasonName = "이성의 끈";
        public const string SurpriseBoxId = "jjangsem-surprise-box";
        public const string SurpriseBoxName = "깜짝상자";

        // Not fixed by the plan; Rare like the other jjangsem spells until Balance-0.
        public const ItemRarity Rarity = ItemRarity.Rare;

        // 파스텔빛 (2026-10-05/06, tuned 2026-10-15/16): for 18 seconds, 100% attack damage to every enemy every
        // 2.5 seconds, half a heart (1 HP unit) healed every 3 seconds and half an SP slot every 6 seconds.
        public const float PastelDurationSeconds = 18f;
        public const float PastelDamageShare = 1f;
        public const float PastelDamageIntervalSeconds = 2.5f;
        public const int PastelHealUnits = 1;
        public const float PastelHealIntervalSeconds = 3f;
        public const int PastelSPHalves = 1;
        public const float PastelSPIntervalSeconds = 6f;
        // 수상한 물약: enemy hits -50% for 10 seconds, and (2026-10-15) three poison stacks on every enemy in the room,
        // each dealing 15% attack damage every second for 10 seconds (the 비비의 콧물 tick rate).
        public const float PotionReduction = 0.5f;
        public const float PotionDurationSeconds = 10f;
        public const int PotionPoisonStacks = 3;
        public const float PotionPoisonTickDamageRatio = 0.15f;
        public const float PotionPoisonIntervalSeconds = 1f;
        public const float PotionPoisonDurationSeconds = 10f;
        // 이성의 끈: player damage x2 in the room (권능 excluded since 2026-10-15).
        public const float ReasonMultiplier = 2f;
        // 깜짝상자: two random enemies, 2 second stun, maximum health -50%; a boss loses 20% of its maximum health
        // instead (2026-10-16).
        public const int SurpriseTargets = 2;
        public const float SurpriseStunSeconds = 2f;
        public const float SurpriseHealthCut = 0.5f;
        public const float SurpriseBossMaxHealthShare = 0.2f;

        public static readonly string[] ItemIds =
        {
            PastelLightId, SuspiciousPotionId, ThreadOfReasonId, SurpriseBoxId,
        };

        public static string PathOf(string itemId) => "Assets/Items/" + itemId + ".asset";

        [MenuItem("Trickal Fan Game/Week 26/Setup Jjangsem-2 Four Jjangsem Spells")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Jjangsem-2 setup.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Jjangsem-2 Four Jjangsem Spells");

            ItemDefinition[] items =
            {
                ConfigureItem(PastelLightId, PastelLightName,
                    new ItemEffectEntry(ItemEffectType.CurrentRoomPulseDamage,
                        configuredMagnitude: PastelDamageShare,
                        configuredIntervalSeconds: PastelDamageIntervalSeconds,
                        configuredDurationSeconds: PastelDurationSeconds),
                    new ItemEffectEntry(ItemEffectType.CurrentRoomPulseSP,
                        configuredIntegerAmount: PastelSPHalves,
                        configuredIntervalSeconds: PastelSPIntervalSeconds,
                        configuredDurationSeconds: PastelDurationSeconds),
                    new ItemEffectEntry(ItemEffectType.CurrentRoomPulseHeal,
                        configuredIntegerAmount: PastelHealUnits,
                        configuredIntervalSeconds: PastelHealIntervalSeconds,
                        configuredDurationSeconds: PastelDurationSeconds)),
                ConfigureItem(SuspiciousPotionId, SuspiciousPotionName,
                    new ItemEffectEntry(ItemEffectType.CurrentRoomEnemyDamageReduction,
                        configuredMagnitude: PotionReduction,
                        configuredDurationSeconds: PotionDurationSeconds),
                    new ItemEffectEntry(ItemEffectType.CurrentRoomEnemyPoison,
                        configuredMagnitude: PotionPoisonTickDamageRatio,
                        configuredIntegerAmount: PotionPoisonStacks,
                        configuredIntervalSeconds: PotionPoisonIntervalSeconds,
                        configuredDurationSeconds: PotionPoisonDurationSeconds)),
                ConfigureItem(ThreadOfReasonId, ThreadOfReasonName,
                    new ItemEffectEntry(ItemEffectType.CurrentRoomDamageMultiplier,
                        configuredMagnitude: ReasonMultiplier)),
                ConfigureItem(SurpriseBoxId, SurpriseBoxName,
                    new ItemEffectEntry(ItemEffectType.StunAndCutRandomEnemies,
                        configuredMagnitude: SurpriseHealthCut,
                        configuredSecondaryMagnitude: SurpriseBossMaxHealthShare,
                        configuredIntegerAmount: SurpriseTargets,
                        configuredDurationSeconds: SurpriseStunSeconds)),
            };
            EnsureGlyphs(items);
            AssetDatabase.SaveAssets();
            ChestContentTable table = Week22Chest1Setup.EnsureTable();

            Undo.CollapseUndoOperations(group);
            Debug.Log("Jjangsem-2 setup complete: " +
                      string.Join(", ", items.Select(item => $"{item.DisplayName} ({item.ItemId})")) +
                      $" are jjangsem spells; a diamond chest can hold {table.JjangsemSpells.Count} jjangsem spell(s).");
        }

        private static ItemDefinition ConfigureItem(string itemId, string displayName, params ItemEffectEntry[] effects)
        {
            string path = PathOf(itemId);
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

            definition.ConfigureContract(itemId, displayName, ItemKind.JjangsemSpell, Rarity, true, 1, effects);
            if (!definition.IsValid)
                throw new InvalidOperationException($"{itemId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // The slot HUD and pickup description show the name and effect description.
        private static void EnsureGlyphs(ItemDefinition[] items)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Jjangsem-2 requires the Frontend TMP font.");
            string characters = string.Concat(items.Select(item => item.DisplayName + ArtifactEffectDescription.Build(item)));
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Jjangsem-2 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
