using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Spell-5: creates four small single-use spells (갑옷축제 초대장, 아멜리아의 러브레터, 랜덤코인, 회심의 일격), gives the
    // Game Scene player the heart pickup the love letter drops, and rebuilds the chest content table so chests can
    // hold them. Re-running updates the same assets, keeping their GUIDs, and leaves a configured scene unchanged.
    public static class Week22Spell5Setup
    {
        public const string ArmorInvitationId = "single-spell-armor-festival-invitation";
        public const string LoveLetterId = "single-spell-amelia-love-letter";
        public const string RandomCoinId = "single-spell-random-coin";
        public const string DecisiveStrikeId = "single-spell-decisive-strike";

        // D6 (2026-10-05). Shield values are half-heart units: 2 hearts = 4 units.
        public const float ArmorShieldUnits = 4f;
        public const int LoveLetterHearts = 2;
        public const int RandomCoinMinimum = 2;
        public const int RandomCoinMaximum = 10;
        // Balance decision (2026-10-05): one room only, so it is larger than a permanent artifact.
        public const float DecisiveStrikeCriticalDamage = 0.5f;
        public const float DecisiveStrikeCriticalChance = 0.15f;

        public static readonly string[] ItemIds = { ArmorInvitationId, LoveLetterId, RandomCoinId, DecisiveStrikeId };

        public static string PathFor(string itemId) => "Assets/Items/" + itemId + ".asset";

        [MenuItem("Trickal Fan Game/Week 22/Setup Spell-5 Small Spells")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Spell-5 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Spell-5 Small Spells");

            ItemDefinition[] items =
            {
                Configure(ArmorInvitationId, "갑옷축제 초대장", ItemRarity.Rare,
                    new ItemEffectEntry(ItemEffectType.GainShield, configuredMagnitude: ArmorShieldUnits)),
                Configure(LoveLetterId, "아멜리아의 러브레터", ItemRarity.Uncommon,
                    new ItemEffectEntry(ItemEffectType.SpawnHealthPickups, configuredIntegerAmount: LoveLetterHearts)),
                Configure(RandomCoinId, "랜덤코인", ItemRarity.Common,
                    new ItemEffectEntry(ItemEffectType.GainRandomGold, configuredMagnitude: RandomCoinMaximum,
                        configuredIntegerAmount: RandomCoinMinimum)),
                Configure(DecisiveStrikeId, "회심의 일격", ItemRarity.Uncommon,
                    new ItemEffectEntry(ItemEffectType.CurrentRoomCriticalBonus,
                        configuredMagnitude: DecisiveStrikeCriticalDamage,
                        configuredSecondaryMagnitude: DecisiveStrikeCriticalChance)),
            };
            EnsureGlyphs(items);
            AssetDatabase.SaveAssets();
            ConfigurePlayerHeartPickup();
            ChestContentTable table = Week22Chest1Setup.EnsureTable();

            Undo.CollapseUndoOperations(group);
            Debug.Log("Spell-5 setup complete: 갑옷축제 초대장, 아멜리아의 러브레터, 랜덤코인 and 회심의 일격 configured; " +
                      $"chests draw from {table.Spells.Count} single-use spells.");
        }

        private static ItemDefinition Configure(string itemId, string displayName, ItemRarity rarity,
            ItemEffectEntry effect)
        {
            string path = PathFor(itemId);
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

            definition.ConfigureContract(itemId, displayName, ItemKind.SingleUseSpell, rarity, true, 1, effect);
            if (!definition.IsValid) throw new InvalidOperationException($"{itemId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // 아멜리아의 러브레터 drops the ordinary one-heart pickup.
        private static void ConfigurePlayerHeartPickup()
        {
            HealthPickup heart = AssetDatabase.LoadAssetAtPath<GameObject>(Week17Resource0Setup.PrefabPath)
                ?.GetComponent<HealthPickup>();
            if (heart == null)
                throw new InvalidOperationException($"Run Resource-0 setup first: {Week17Resource0Setup.PrefabPath}.");

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphController[] graphs = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphController>(true)).ToArray();
            if (graphs.Length != 1)
                throw new InvalidOperationException($"Game Scene requires one RoomGraphController; found {graphs.Length}.");
            PlayerMovement player = graphs[0].Player;
            PlayerSingleUseEffects effects = player != null ? player.GetComponent<PlayerSingleUseEffects>() : null;
            if (effects == null)
                throw new InvalidOperationException("Run Slot-0 setup first: the room graph player has no " +
                                                    "PlayerSingleUseEffects.");
            if (effects.HeartPickupPrefab == heart) return;

            Undo.RecordObject(effects, "Configure Spell-5 heart pickup");
            effects.ConfigureHeartPickup(heart);
            EditorUtility.SetDirty(effects);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Spell-5 setup.");
        }

        // The slot HUD and pickup description show the names and effect descriptions.
        private static void EnsureGlyphs(ItemDefinition[] items)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Spell-5 requires the Frontend TMP font.");
            string characters = string.Concat(items.Select(item => item.DisplayName + ArtifactEffectDescription.Build(item)));
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Spell-5 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
