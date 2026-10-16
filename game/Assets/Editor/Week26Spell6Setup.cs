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
    // Spell-6: creates the five single-use spells of plan 26 §4.3 (네티의 지도, 에슈르의 마도서, 티그의 도전장, 골디의 초대장,
    // 엘리스의 타로카드), rebuilds the chest content table so chests can hold them, and adds them to the obstacle variants
    // that already drop a rare spell. Re-running updates the same assets and tables, keeping their GUIDs.
    public static class Week26Spell6Setup
    {
        public const string NettiMapId = "single-spell-netti-map";
        public const string NettiMapName = "네티의 지도";
        public const string EshurGrimoireId = "single-spell-eshur-grimoire";
        public const string EshurGrimoireName = "에슈르의 마도서";
        public const string TigChallengeId = "single-spell-tig-challenge";
        public const string TigChallengeName = "티그의 도전장";
        public const string GoldiInvitationId = "single-spell-goldi-invitation";
        public const string GoldiInvitationName = "골디의 초대장";
        public const string EllisTarotId = "single-spell-ellis-tarot-card";
        public const string EllisTarotName = "엘리스의 타로카드";

        // Not fixed by the plan; implementation defaults until Balance-0. A one-room or small effect is Uncommon, a
        // move that skips the floor or a Run-long change is Rare.
        public const ItemRarity NettiMapRarity = ItemRarity.Uncommon;
        public const ItemRarity EshurGrimoireRarity = ItemRarity.Uncommon;
        public const ItemRarity TigChallengeRarity = ItemRarity.Rare;
        public const ItemRarity GoldiInvitationRarity = ItemRarity.Rare;
        public const ItemRarity EllisTarotRarity = ItemRarity.Rare;

        // 네티의 지도 (2026-10-09): +5 gold, usable with a full wallet.
        public const int NettiMapGold = 5;
        // 에슈르의 마도서 (2026-10-09): homing coefficient 0.5 (180° per second) within 4 in front of the shot.
        public const float EshurHomingCoefficient = 0.5f;
        public const float EshurDetectionRange = 4f;

        public static readonly string[] ItemIds =
        {
            NettiMapId, EshurGrimoireId, TigChallengeId, GoldiInvitationId, EllisTarotId,
        };

        public static string PathOf(string itemId) => "Assets/Items/" + itemId + ".asset";

        [MenuItem("Trickal Fan Game/Week 26/Setup Spell-6 Five Spells")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Spell-6 setup.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Spell-6 Five Spells");

            ItemDefinition[] items =
            {
                ConfigureItem(NettiMapId, NettiMapName, NettiMapRarity,
                    new ItemEffectEntry(ItemEffectType.RevealCurrentFloorMap),
                    new ItemEffectEntry(ItemEffectType.GainGold, configuredIntegerAmount: NettiMapGold)),
                ConfigureItem(EshurGrimoireId, EshurGrimoireName, EshurGrimoireRarity,
                    new ItemEffectEntry(ItemEffectType.CurrentRoomBasicAttackHoming,
                        configuredMagnitude: EshurHomingCoefficient, configuredRadius: EshurDetectionRange)),
                ConfigureItem(TigChallengeId, TigChallengeName, TigChallengeRarity,
                    new ItemEffectEntry(ItemEffectType.TeleportToBossRoom)),
                ConfigureItem(GoldiInvitationId, GoldiInvitationName, GoldiInvitationRarity,
                    new ItemEffectEntry(ItemEffectType.TeleportToGoldiShop)),
                ConfigureItem(EllisTarotId, EllisTarotName, EllisTarotRarity,
                    new ItemEffectEntry(ItemEffectType.RandomStatShift)),
            };
            EnsureGlyphs(items);
            AssetDatabase.SaveAssets();
            ChestContentTable table = Week22Chest1Setup.EnsureTable();
            int variants = RefreshObstacleRareSpells();
            AssetDatabase.SaveAssets();

            Undo.CollapseUndoOperations(group);
            Debug.Log("Spell-6 setup complete: " +
                      string.Join(", ", items.Select(item => $"{item.DisplayName} ({item.ItemId})")) +
                      $" are single-use spells; chests draw from {table.Spells.Count} spells and {variants} obstacle " +
                      "variant(s) drop them as rare spells.");
        }

        private static ItemDefinition ConfigureItem(string itemId, string displayName, ItemRarity rarity,
            params ItemEffectEntry[] effects)
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

            definition.ConfigureContract(itemId, displayName, ItemKind.SingleUseSpell, rarity, true, 1, effects);
            if (!definition.IsValid)
                throw new InvalidOperationException($"{itemId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // Obstacle-5 gave its rare-spell variants the spells of that time; they now hold every implemented spell.
        // Variants without a rare spell roll stay untouched.
        public static int RefreshObstacleRareSpells()
        {
            ItemDefinition[] spells = Week22Chest1Setup.FindSingleUseItems(ItemKind.SingleUseSpell);
            int refreshed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ObstacleVariantDefinition"))
            {
                ObstacleVariantDefinition variant =
                    AssetDatabase.LoadAssetAtPath<ObstacleVariantDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (variant == null || variant.RareItemChance <= 0f) continue;
                refreshed++;
                if (variant.RareSpells.SequenceEqual(spells)) continue;
                Undo.RecordObject(variant, "Refresh rare spells of " + variant.name);
                variant.ConfigureRareItems(variant.RareItemChance, variant.RareArtifactShare, spells,
                    variant.SpellPickupPrefab, variant.ArtifactPickupPrefab);
                if (!variant.TryValidate(out string error))
                    throw new InvalidOperationException($"Spell-6 left {variant.name} invalid. {error}");
                EditorUtility.SetDirty(variant);
            }

            return refreshed;
        }

        // The slot HUD and pickup description show the name and effect description.
        private static void EnsureGlyphs(ItemDefinition[] items)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Spell-6 requires the Frontend TMP font.");
            string characters = string.Concat(items.Select(item => item.DisplayName + ArtifactEffectDescription.Build(item)));
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Spell-6 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
