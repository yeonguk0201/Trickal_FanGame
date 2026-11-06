using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Pool-2: adds the pure-stat artifacts (Trickcal cards that only raise stats, priority 1 and 2) to the boss and
    // shop pools. They use existing effect types only and stack without limit (Pool-1). Re-running updates the same
    // assets, keeps their GUIDs and leaves the reward pool unchanged.
    public static class Pool2PureStatArtifactSetup
    {
        public const string TowelHeadbandId = "artifact-towel-headband";
        public const string OldTreeDaggerId = "artifact-old-tree-dagger";
        public const string PatchedStaffId = "artifact-patched-staff";
        public const string MistDaggerId = "artifact-mist-dagger";
        public const string ObsidianShurikenId = "artifact-obsidian-shuriken";
        public const string FoxDaggerId = "artifact-fox-dagger";
        public const string CombatManualId = "artifact-combat-manual";
        public const string JadeGrimoireId = "artifact-jade-grimoire";
        public const string SharpStaffId = "artifact-sharp-staff";
        public const string GoldenCrownId = "artifact-golden-crown";
        public const string BelitaStaffId = "artifact-belita-staff";
        public const string WeightedVestId = "artifact-weighted-vest";
        public const string FairyRobeId = "artifact-fairy-robe";
        public const string ScaleArmorId = "artifact-scale-armor";
        public const string JadeFountainPenId = "artifact-jade-fountain-pen";
        public const string ShiningTiaraId = "artifact-shining-tiara";
        public const string PriestCenserId = "artifact-priest-censer";
        public const string BlanchetBluebirdId = "artifact-blanchet-glass-bluebird";

        // Values per stack. Health is in half-heart units; critical chance and critical damage are points.
        public static readonly Week23Artifact2Setup.ArtifactSpec[] Artifacts =
        {
            new(TowelHeadbandId, "수건 머리띠", ItemRarity.Common, 0,
                "최대 HP +반 칸",
                new ItemEffectEntry(ItemEffectType.MaxHealthFlat, 1f)),
            new(OldTreeDaggerId, "고목나무 비수", ItemRarity.Common, 0,
                "치명타 확률 +2%p · 치명타 피해 +10%p",
                new ItemEffectEntry(ItemEffectType.CriticalChance, 0.02f),
                new ItemEffectEntry(ItemEffectType.CriticalDamage, 0.1f)),
            new(PatchedStaffId, "덧댄 지팡이", ItemRarity.Common, 0,
                "스킬 피해 +8%",
                new ItemEffectEntry(ItemEffectType.SkillDamagePercent, 0.08f)),
            new(MistDaggerId, "안개 비수", ItemRarity.Uncommon, 0,
                "치명타 피해 +25%p",
                new ItemEffectEntry(ItemEffectType.CriticalDamage, 0.25f)),
            new(ObsidianShurikenId, "흑요석 수리검", ItemRarity.Uncommon, 0,
                "치명타 피해 +10%p · 기본 공격 피해 +8%",
                new ItemEffectEntry(ItemEffectType.CriticalDamage, 0.1f),
                new ItemEffectEntry(ItemEffectType.BasicAttackDamagePercent, 0.08f)),
            new(FoxDaggerId, "여우 단검", ItemRarity.Uncommon, 0,
                "공격속도 +5% · 치명타 확률 +3%p · 공격력 +3%",
                new ItemEffectEntry(ItemEffectType.AttackSpeedPercent, 0.05f),
                new ItemEffectEntry(ItemEffectType.CriticalChance, 0.03f),
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.03f)),
            new(CombatManualId, "전투 지침서", ItemRarity.Rare, 0,
                "치명타 피해 +40%p",
                new ItemEffectEntry(ItemEffectType.CriticalDamage, 0.4f)),
            new(JadeGrimoireId, "옥빛 마법서", ItemRarity.Rare, 0,
                "공격력 +5% · 치명타 확률 +3%p · 스킬 피해 +10%",
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.05f),
                new ItemEffectEntry(ItemEffectType.CriticalChance, 0.03f),
                new ItemEffectEntry(ItemEffectType.SkillDamagePercent, 0.1f)),
            new(SharpStaffId, "날카로운 지팡이", ItemRarity.Rare, 0,
                "공격력 +5% · 치명타 피해 +15%p · 스킬 피해 +10%",
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.05f),
                new ItemEffectEntry(ItemEffectType.CriticalDamage, 0.15f),
                new ItemEffectEntry(ItemEffectType.SkillDamagePercent, 0.1f)),
            new(GoldenCrownId, "황금 왕관", ItemRarity.Rare, 0,
                "치명타 확률 +4%p · 치명타 피해 +15%p · 스킬 피해 +12%",
                new ItemEffectEntry(ItemEffectType.CriticalChance, 0.04f),
                new ItemEffectEntry(ItemEffectType.CriticalDamage, 0.15f),
                new ItemEffectEntry(ItemEffectType.SkillDamagePercent, 0.12f)),
            new(BelitaStaffId, "벨리타의 지팡이", ItemRarity.Epic, 0,
                "공격력 +20% · 치명타 피해 +15%p",
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.2f),
                new ItemEffectEntry(ItemEffectType.CriticalDamage, 0.15f)),

            // Priority 2: defensive Trickcal cards turned into health, and an ally buff turned into the player's own.
            new(WeightedVestId, "중량 조끼", ItemRarity.Common, 0,
                "최대 HP +1칸 · 이동속도 -5%",
                new ItemEffectEntry(ItemEffectType.MaxHealthFlat, 2f),
                new ItemEffectEntry(ItemEffectType.MoveSpeedPenaltyPercent, 0.05f)),
            new(FairyRobeId, "요정 로브", ItemRarity.Common, 0,
                "최대 HP +반 칸 · 이동속도 +3%",
                new ItemEffectEntry(ItemEffectType.MaxHealthFlat, 1f),
                new ItemEffectEntry(ItemEffectType.MoveSpeedPercent, 0.03f)),
            new(ScaleArmorId, "비늘 갑옷", ItemRarity.Rare, 0,
                "최대 HP +2칸",
                new ItemEffectEntry(ItemEffectType.MaxHealthFlat, 4f)),
            new(JadeFountainPenId, "제이드의 비싼 만년필", ItemRarity.Rare, 0,
                "공격력 +5% · 공격속도 +5% · 스킬 피해 +10%",
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.05f),
                new ItemEffectEntry(ItemEffectType.AttackSpeedPercent, 0.05f),
                new ItemEffectEntry(ItemEffectType.SkillDamagePercent, 0.1f)),

            // Priority 2 with small new effect types (87-89).
            new(ShiningTiaraId, "찬란한 티아라", ItemRarity.Uncommon, 0,
                "최대 HP +반 칸 · 방어막을 얻을 때 반 칸 더",
                new ItemEffectEntry(ItemEffectType.MaxHealthFlat, 1f),
                new ItemEffectEntry(ItemEffectType.ShieldGainBonus, 1f)),
            new(PriestCenserId, "제사장의 향로", ItemRarity.Rare, 0,
                "최대 HP +1칸 · 방어막을 얻을 때 반 칸 더",
                new ItemEffectEntry(ItemEffectType.MaxHealthFlat, 2f),
                new ItemEffectEntry(ItemEffectType.ShieldGainBonus, 1f)),
            new(BlanchetBluebirdId, "블랑셰의 유리 파랑새", ItemRarity.Epic, 0,
                "고학년 스킬 피해 +100% · 기본 공격 피해 -30%",
                new ItemEffectEntry(ItemEffectType.UltimateDamagePercent, 1f),
                new ItemEffectEntry(ItemEffectType.BasicAttackDamagePenaltyPercent, 0.3f)),
        };

        [MenuItem("Trickal Fan Game/Week 28/Setup Pool-2 Pure Stat Artifacts")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Pool-2 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Pool-2 Pure Stat Artifacts");

            ItemDefinition[] items = Artifacts.Select(ConfigureItem).ToArray();
            AddToSelectionPool(items);
            EnsureGlyphs(items);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Pool-2 setup complete: {items.Length} pure-stat artifacts are in the boss and shop pools.");
        }

        private static ItemDefinition ConfigureItem(Week23Artifact2Setup.ArtifactSpec spec)
        {
            string path = Week23Artifact2Setup.ItemPath(spec.ItemId);
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = spec.ItemId;
                AssetDatabase.CreateAsset(definition, path);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + spec.ItemId);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + spec.ItemId);
            }

            definition.ConfigureContract(spec.ItemId, spec.DisplayName, ItemKind.Artifact, spec.Rarity, true,
                spec.MaxStacks, spec.Effects);
            // Pool-0 classification is the single source of the boss and shop pools.
            definition.ConfigurePoolSources(Pool0ItemPoolSetup.ExpectedSources(definition));
            if (!definition.IsValid)
                throw new InvalidOperationException($"{spec.ItemId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void AddToSelectionPool(ItemDefinition[] items)
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            ItemDefinition[] pool = assembler.SelectionRewardPool.ToArray();
            if (items.All(pool.Contains)) return;

            // Same order as the Reward-3 setup that builds the whole pool.
            ItemDefinition[] extended = pool.Union(items)
                .OrderBy(definition => definition.ItemId, StringComparer.Ordinal).ToArray();
            Undo.RecordObject(assembler, "Add Pool-2 artifacts to the reward pool");
            assembler.ConfigureSelectionRewards(assembler.RewardSelectionSession, extended);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Pool-2 setup.");
        }

        // The acquisition toast, reward card, shop and pause list show the name and effect description.
        private static void EnsureGlyphs(ItemDefinition[] items)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Pool-2 requires the Frontend TMP font.");
            string characters = string.Concat(items.Select(item =>
                item.DisplayName + ArtifactEffectDescription.Build(item)));
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Pool-2 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
