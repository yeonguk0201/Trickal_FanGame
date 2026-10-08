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
    // Artifact-2: creates the 14 confirmed artifacts (docs/12 "확정 상세 효과") and adds them to the selection reward
    // pool. Re-running updates the same assets, keeps their GUIDs and leaves the pool unchanged.
    public static class Week23Artifact2Setup
    {
        public readonly struct ArtifactSpec
        {
            public ArtifactSpec(string itemId, string displayName, ItemRarity rarity, int maxStacks,
                string description, params ItemEffectEntry[] effects)
            {
                ItemId = itemId;
                DisplayName = displayName;
                Rarity = rarity;
                MaxStacks = maxStacks;
                Description = description;
                Effects = effects;
            }

            public string ItemId { get; }
            public string DisplayName { get; }
            public ItemRarity Rarity { get; }
            public int MaxStacks { get; }
            // The text ArtifactEffectDescription must build for the asset.
            public string Description { get; }
            public ItemEffectEntry[] Effects { get; }
        }

        public const string FlagId = "artifact-ner-eldr-flag";
        public const string BeltId = "artifact-emergency-protection-belt";
        public const string HairpinId = "artifact-pork-cutlet-hairpin";
        public const string ScytheId = "artifact-rim-scythe";
        public const string MuffinId = "artifact-explosive-muffin";
        public const string BowId = "artifact-blazing-bow";
        public const string GunId = "artifact-attatta-gun";
        public const string SwordId = "artifact-atta-sword";
        public const string BranchId = "artifact-burning-branch";
        public const string EPadId = "artifact-amelia-epad-classic";
        public const string RingId = "artifact-greed-ring";
        public const string WindArrowId = "artifact-sylla-wind-arrow";
        public const string DaggerId = "artifact-levi-dagger";
        public const string GloveId = "artifact-shushushushuk-glove";

        // Passive-0 §4.8 status effect values.
        public const float BurnTickDamageRatio = 0.2f;
        public const float BurnDurationSeconds = 3f;
        public const float BurnIntervalSeconds = 0.5f;
        public const float ShockSlowPerStack = 0.1f;
        public const float ShockDurationSeconds = 2.5f;
        public const int ShockMaxStacks = 4;

        public static readonly ArtifactSpec[] Artifacts =
        {
            new(FlagId, "네르의 엘드르 깃발", ItemRarity.Epic, 2,
                "최대 HP +1칸 · 저학년 스킬 3회 시전마다 HP 반 칸 회복 (스택마다 필요 횟수 -1)",
                new ItemEffectEntry(ItemEffectType.MaxHealthFlat, 2f),
                new ItemEffectEntry(ItemEffectType.HealOnLowerGradeSkillEveryN, 1f, configuredIntegerAmount: 3)),
            new(BeltId, "긴급 보호 벨트", ItemRarity.Rare, 1,
                "전투방에 들어갈 때마다 방어막 반 칸 획득 (HP와 방어막 합 15칸까지)",
                new ItemEffectEntry(ItemEffectType.ShieldOnCombatRoomEntry, 1f)),
            new(HairpinId, "돈까스 모양 머리핀", ItemRarity.Uncommon, 3,
                "최대 HP +1칸",
                new ItemEffectEntry(ItemEffectType.MaxHealthFlat, 2f)),
            new(ScytheId, "림의 낫", ItemRarity.Epic, 1,
                "공격력 +8% · 피해를 준 적의 HP가 20% 이하면 즉시 처치 (보스 제외)",
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.08f),
                new ItemEffectEntry(ItemEffectType.ExecuteBelowHealth, 0.2f)),
            new(MuffinId, "폭발 머핀", ItemRarity.Rare, 1,
                "치명타 확률 +5%p · 기본 공격 8회 적중마다 반경 1.5m에 공격력의 100% 폭발 피해",
                new ItemEffectEntry(ItemEffectType.CriticalChance, 0.05f),
                new ItemEffectEntry(ItemEffectType.BasicAttackHitExplosion, 1f, configuredIntegerAmount: 8,
                    configuredRadius: 1.5f)),
            new(BowId, "활활 불타활", ItemRarity.Uncommon, 2,
                "공격력 +3% · 기본 공격 적중 시 20% 확률로 화상 (3초간 0.5초마다 공격력의 20% 피해)",
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.03f),
                Burn(0.2f)),
            new(GunId, "앗땃따건", ItemRarity.Uncommon, 2,
                "공격력 +3% · 기본 공격 적중 시 20% 확률로 감전 (2.5초간 이동속도 -10%, 최대 4중첩)",
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.03f),
                new ItemEffectEntry(ItemEffectType.BasicAttackShock, 0.2f,
                    configuredSecondaryMagnitude: ShockSlowPerStack, configuredIntegerAmount: ShockMaxStacks,
                    configuredDurationSeconds: ShockDurationSeconds)),
            new(SwordId, "앗따검", ItemRarity.Uncommon, 2,
                "상태이상 피해 +10% · 공격력 +5%",
                new ItemEffectEntry(ItemEffectType.StatusTickDamagePercent, 0.1f),
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.05f)),
            new(BranchId, "불타는 가지", ItemRarity.Uncommon, 1,
                "공격력 +3% · 화상에 걸린 적에게 주는 직접 피해 +25% · " +
                "기본 공격 적중 시 10% 확률로 화상 (3초간 0.5초마다 공격력의 20% 피해)",
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.03f),
                new ItemEffectEntry(ItemEffectType.DirectDamagePercentVsBurning, 0.25f),
                Burn(0.1f)),
            new(EPadId, "아멜리아의 E-Pad 클래식", ItemRarity.Epic, 1,
                "스킬 피해 +8% · 감전된 적에게 스킬 피해 +60% · " +
                "감전된 적에게 치명타 확률 +33.61%p·치명타 피해 +33.61%p",
                new ItemEffectEntry(ItemEffectType.SkillDamagePercent, 0.08f),
                new ItemEffectEntry(ItemEffectType.SkillDamagePercentVsShocked, 0.6f),
                new ItemEffectEntry(ItemEffectType.CriticalBonusVsShocked, 0.3361f,
                    configuredSecondaryMagnitude: 0.3361f)),
            new(RingId, "탐욕의 반지", ItemRarity.Uncommon, 2,
                "치명타 피해 +30%p · 상태이상 피해 +30%",
                new ItemEffectEntry(ItemEffectType.CriticalDamage, 0.3f),
                new ItemEffectEntry(ItemEffectType.StatusTickDamagePercent, 0.3f)),
            new(WindArrowId, "실라의 바람살", ItemRarity.Epic, 2,
                "공격속도 +15% · 이동속도 +5% · 탄속 +15% (사거리 증가)",
                new ItemEffectEntry(ItemEffectType.AttackSpeedPercent, 0.15f),
                new ItemEffectEntry(ItemEffectType.MoveSpeedPercent, 0.05f),
                new ItemEffectEntry(ItemEffectType.ProjectileSpeedPercent, 0.15f)),
            new(DaggerId, "레비의 단도", ItemRarity.Epic, 1,
                "공격력 +8% · 이동속도 +3% · Run당 1회 사망 피해 무효 후 5초간 무적",
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.08f),
                new ItemEffectEntry(ItemEffectType.MoveSpeedPercent, 0.03f),
                new ItemEffectEntry(ItemEffectType.NegateLethalDamageOnce, configuredDurationSeconds: 5f)),
            new(GloveId, "슈슈슈슉 글러브", ItemRarity.Epic, 1,
                "공격력 +8% · 적 처치 시 5초간 기본 공격 피해 +5%·공격속도 +8% " +
                "(최대 3중첩, 끝난 뒤 10초간 재발동 불가) · 중첩당 넉백 +15%",
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.08f),
                new ItemEffectEntry(ItemEffectType.KillFrenzy, 0.05f, configuredSecondaryMagnitude: 0.08f,
                    configuredIntegerAmount: 3, configuredIntervalSeconds: 10f, configuredDurationSeconds: 5f),
                new ItemEffectEntry(ItemEffectType.KillFrenzyKnockbackPercent, 0.15f)),
        };

        public static string ItemPath(string itemId) => $"Assets/Items/{itemId}.asset";

        [MenuItem("Trickal Fan Game/Week 23/Setup Artifact-2 Confirmed Artifacts")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Artifact-2 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Artifact-2 Confirmed Artifacts");

            ItemDefinition[] items = Artifacts.Select(ConfigureItem).ToArray();
            AddToSelectionPool(items);
            EnsureGlyphs(items);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Artifact-2 setup complete: {items.Length} artifacts are in the selection reward pool.");
        }

        private static ItemEffectEntry Burn(float chance) =>
            new(ItemEffectType.BasicAttackBurn, chance, configuredSecondaryMagnitude: BurnTickDamageRatio,
                configuredIntervalSeconds: BurnIntervalSeconds, configuredDurationSeconds: BurnDurationSeconds);

        private static ItemDefinition ConfigureItem(ArtifactSpec spec)
        {
            string path = ItemPath(spec.ItemId);
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
            Undo.RecordObject(assembler, "Add Artifact-2 artifacts to the reward pool");
            assembler.ConfigureSelectionRewards(assembler.RewardSelectionSession, extended);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Artifact-2 setup.");
        }

        // The acquisition toast, reward card and pause list show the name and effect description.
        private static void EnsureGlyphs(ItemDefinition[] items)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Artifact-2 requires the Frontend TMP font.");
            Week13Hud3BSetup.EnsureDescriptionGlyphs(font);
            string characters = string.Concat(items.Select(item =>
                item.DisplayName + ArtifactEffectDescription.Build(item)));
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Artifact-2 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
