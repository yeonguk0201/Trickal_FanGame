using System;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Pool2PureStatArtifactVerification
    {
        private const ItemPoolSource PureStatSources = ItemPoolSource.Boss | ItemPoolSource.Shop;
        private const int StacksChecked = 2;

        [MenuItem("Trickal Fan Game/Week 28/Setup and Verify Pool-2 Pure Stat Artifacts")]
        public static void SetupAndVerifyBatch()
        {
            Pool2PureStatArtifactSetup.Setup();
            string[] guids = Pool2PureStatArtifactSetup.Artifacts
                .Select(spec => AssetDatabase.AssetPathToGUID(Week23Artifact2Setup.ItemPath(spec.ItemId))).ToArray();
            Pool2PureStatArtifactSetup.Setup();
            Assert(guids.All(guid => !string.IsNullOrEmpty(guid)) &&
                   guids.SequenceEqual(Pool2PureStatArtifactSetup.Artifacts
                       .Select(spec => AssetDatabase.AssetPathToGUID(Week23Artifact2Setup.ItemPath(spec.ItemId)))),
                "Running Pool-2 setup twice must keep the same artifact assets and GUIDs.");
            Verify();
        }

        // Pool-2 grows the boss and shop pools and the pure-stat group Pool-1 lifts the stack limit for, and adds
        // supplied artwork, so those checks run after it.
        [MenuItem("Trickal Fan Game/Week 28/Verify Pool-2 Pure Stat Artifacts With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            SetupAndVerifyBatch();
            Pool0ItemPoolVerification.Verify();
            Pool1StackLimitVerification.Verify();
            UserArtworkVerification.Verify();
            Week23Artifact2Verification.Verify();
            Week22Shop0Verification.Verify();
            // Effects 87-89 touch shared health, shield, high-grade skill and basic attack code.
            Pool1StackLimitVerification.VerifyWithRegressionsBatch();
            Hp1HealthUnitsVerification.Verify();
            PhaseGCombatFoundationVerification.Verify();
            PhaseGConditionalEffectsVerification.Verify();
            ShieldBubbleVerification.Verify();
            // The high-grade skill check reads the player from the open Game Scene.
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            Week7HighGradeSkillVerification.Verify();
            Week22Spell5Verification.Verify();
            Week27Power1Verification.Verify();
            Debug.Log("Pool-2 pure stat artifact verification with regressions passed.");
        }

        [MenuItem("Trickal Fan Game/Week 28/Verify Pool-2 Pure Stat Artifacts")]
        public static void Verify()
        {
            VerifyAssets();
            VerifyRewardPool();
            VerifyStats();
            VerifyOverallCaps();
            VerifyNewEffects();
            Debug.Log($"Pool-2 pure stat artifact verification passed: {Pool2PureStatArtifactSetup.Artifacts.Length} " +
                      "artifacts with artwork in the boss and shop pools, stacking without limit.");
        }

        private static void VerifyAssets()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null, "Pool-2 verification requires the Frontend TMP font.");
            Assert(Pool2PureStatArtifactSetup.Artifacts.Select(spec => spec.ItemId).Distinct(StringComparer.Ordinal)
                    .Count() == 18, "Pool-2 must add eighteen distinct artifacts (priority 1: 11, priority 2: 7).");

            foreach (Week23Artifact2Setup.ArtifactSpec spec in Pool2PureStatArtifactSetup.Artifacts)
            {
                ItemDefinition item = Load(spec.ItemId);
                Assert(item.IsValid && item.IsActive && item.Kind == ItemKind.Artifact && !item.IsSingleUse &&
                       item.DisplayName == spec.DisplayName && item.Rarity == spec.Rarity && item.MaxStacks == 0,
                    $"{spec.ItemId} must be an active {spec.Rarity} artifact named {spec.DisplayName} with no stack limit.");
                Assert(item.Effects.Count == spec.Effects.Length &&
                       item.Effects.Zip(spec.Effects, (actual, expected) =>
                           actual.EffectType == expected.EffectType && Near(actual.Magnitude, expected.Magnitude)).All(ok => ok),
                    $"{spec.ItemId} effects must match the Pool-2 table.");
                Assert(item.Effects.All(effect => IsPureStat(effect.EffectType)),
                    $"{spec.ItemId} must use only existing lasting stat effects.");
                string description = ArtifactEffectDescription.Build(item);
                Assert(description == spec.Description,
                    $"{spec.ItemId} description must be \"{spec.Description}\", got \"{description}\".");
                Assert(font.HasCharacters(spec.DisplayName + description),
                    $"The Frontend font is missing glyphs for {spec.ItemId}.");
                Assert(item.PoolSources == PureStatSources && Pool1StackLimitSetup.IsUnlimited(spec.ItemId),
                    $"{spec.ItemId} must be in the boss and shop pools and in the Pool-1 unlimited list.");
                Assert(UserArtwork.Load(spec.ItemId) != null,
                    $"{spec.ItemId} must have its supplied artwork in Resources/UserArtwork.");
            }

            string[] pureStat = Pool0ItemPoolSetup.Sources.Where(pair => pair.Value == PureStatSources)
                .Select(pair => pair.Key).ToArray();
            ItemDefinition[] group = pureStat.Select(Load).ToArray();
            Assert(group.Length == 24 &&
                   group.Count(item => item.Rarity == ItemRarity.Common) == 7 &&
                   group.Count(item => item.Rarity == ItemRarity.Uncommon) == 6 &&
                   group.Count(item => item.Rarity == ItemRarity.Rare) == 8 &&
                   group.Count(item => item.Rarity == ItemRarity.Epic) == 3,
                "The pure-stat group must grow from 6 to 24 artifacts (Common 7, Uncommon 6, Rare 8, Epic 3).");
        }

        private static void VerifyRewardPool()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            foreach (Week23Artifact2Setup.ArtifactSpec spec in Pool2PureStatArtifactSetup.Artifacts)
                Assert(assembler.SelectionRewardPool.Count(item => item != null && item.ItemId == spec.ItemId) == 1,
                    $"The selection reward pool must offer {spec.ItemId} once.");
            Assert(assembler.SelectionRewardPool.Select(item => item.ItemId)
                    .SequenceEqual(assembler.SelectionRewardPool.Select(item => item.ItemId)
                        .OrderBy(id => id, StringComparer.Ordinal)),
                "The selection reward pool must stay sorted by item ID.");
        }

        // Each stack adds its full value: two stacks give twice the bonus.
        private static void VerifyStats()
        {
            foreach (Week23Artifact2Setup.ArtifactSpec spec in Pool2PureStatArtifactSetup.Artifacts)
            {
                ItemDefinition item = Load(spec.ItemId);
                WithPlayer((stats, inventory, health) =>
                {
                    Snapshot before = Snapshot.Of(stats);
                    for (int stack = 0; stack < StacksChecked; stack++)
                        Assert(inventory.TryAcquire(item) && ArtifactRewardSelector.IsEligible(item, inventory),
                            $"{spec.ItemId} must stack and stay offered.");
                    Snapshot after = Snapshot.Of(stats);

                    Snapshot expected = before;
                    foreach (ItemEffectEntry effect in spec.Effects)
                        expected = expected.With(effect.EffectType, effect.Magnitude * StacksChecked, before);
                    Assert(after.Matches(expected),
                        $"{spec.ItemId} x{StacksChecked} must give {expected}, got {after} (from {before}).");
                    Assert(Near(health.CurrentHealth, stats.MaxHealth),
                        $"{spec.ItemId} must fill the health it adds.");
                });
            }
        }

        // Health caps at 15 hearts (5 base): 수건 머리띠 leaves at 20, 비늘 갑옷 at 5, and 중량 조끼 at 10 because its move speed
        // penalty is not a reason to take it. Any uncapped stat (critical damage, skill, attack) keeps the rest offered.
        private static void VerifyOverallCaps()
        {
            ItemDefinition towel = Load(Pool2PureStatArtifactSetup.TowelHeadbandId);
            ItemDefinition fox = Load(Pool2PureStatArtifactSetup.FoxDaggerId);
            ItemDefinition oldTree = Load(Pool2PureStatArtifactSetup.OldTreeDaggerId);
            ItemDefinition vest = Load(Pool2PureStatArtifactSetup.WeightedVestId);
            ItemDefinition armor = Load(Pool2PureStatArtifactSetup.ScaleArmorId);
            Assert(CountUntilCapped(vest) == 10 && CountUntilCapped(armor) == 5,
                "중량 조끼 must stay offered for 10 stacks and 비늘 갑옷 for 5 before 15 hearts.");
            WithPlayer((stats, inventory, _) =>
            {
                int towels = 0;
                while (ArtifactRewardSelector.IsEligible(towel, inventory))
                {
                    Assert(towels < 100 && inventory.TryAcquire(towel), "수건 머리띠 never reached the health cap.");
                    towels++;
                }

                Assert(towels == 20 && Near(stats.MaxHealth, HealthUnits.MaximumHealthAndShieldUnits),
                    $"수건 머리띠 must stay offered until 15 hearts (20 stacks), took {towels}.");
                Assert(!ArtifactRewardSelector.IsEligible(vest, inventory),
                    "중량 조끼 must leave the rewards at 15 hearts even though its move speed penalty has no cap.");

                stats.AddCriticalChance(1f);
                Assert(Near(stats.CriticalChance, 1f) && ArtifactRewardSelector.IsEligible(fox, inventory) &&
                       ArtifactRewardSelector.IsEligible(oldTree, inventory),
                    "Artifacts with an uncapped stat must stay offered after critical chance reaches 100%.");
            });
        }

        // 87 shield gain bonus, 88 high-grade skill damage, 89 basic attack penalty with its floor.
        private static void VerifyNewEffects()
        {
            Assert((int)ItemEffectType.ShieldGainBonus == 87 && (int)ItemEffectType.UltimateDamagePercent == 88 &&
                   (int)ItemEffectType.BasicAttackDamagePenaltyPercent == 89,
                "Pool-2 effect types must be appended as 87-89.");
            foreach (ItemEffectType type in new[] { ItemEffectType.ShieldGainBonus, ItemEffectType.UltimateDamagePercent,
                         ItemEffectType.BasicAttackDamagePenaltyPercent })
                Assert(!new ItemEffectEntry(type, 0f).TryValidate(out _) && new ItemEffectEntry(type, 1f).TryValidate(out _),
                    $"{type} must require a positive magnitude.");

            ItemDefinition tiara = Load(Pool2PureStatArtifactSetup.ShiningTiaraId);
            ItemDefinition censer = Load(Pool2PureStatArtifactSetup.PriestCenserId);
            ItemDefinition bluebird = Load(Pool2PureStatArtifactSetup.BlanchetBluebirdId);
            ItemDefinition balloon = Load("item-02");

            WithPlayer((_, inventory, health) =>
            {
                Assert(inventory.TryAcquire(tiara) && inventory.TryAcquire(censer), "Tiara and censer must be acquirable.");
                // Two bonuses: a gain of half a heart becomes a heart and a half.
                Assert(Near(health.GainShield(1f), 3f) && Near(health.CurrentShield, 3f),
                    "Each shield gain must add half a heart per tiara and censer.");
                Assert(health.AddShield(2f) && Near(health.CurrentShield, 7f),
                    "Every AddShield path (spells) must add the bonus too.");
                float room = HealthUnits.MaximumHealthAndShieldUnits - health.CurrentHealth - health.CurrentShield;
                Assert(Near(health.GainShield(100f), room),
                    "The shield gain bonus must stay inside the 15-heart health + shield limit.");
                health.SetShield(0f);
                Assert(inventory.TryAcquire(balloon) &&
                       Near(health.CurrentShield, health.GetMaxHealthRatioAmount(0.5f) + 2f),
                    "The balloon armor acquire shield must get the shield gain bonus.");
            });

            WithPlayer((stats, inventory, _) =>
            {
                Assert(ArtifactRewardSelector.IsEligible(tiara, inventory),
                    "찬란한 티아라 must be offered while health is below 15 hearts.");
                stats.AddMaxHealth(HealthUnits.MaximumHealthAndShieldUnits, true);
                Assert(!ArtifactRewardSelector.IsEligible(tiara, inventory) &&
                       !ArtifactRewardSelector.IsEligible(censer, inventory),
                    "At 15 hearts no shield fits, so the tiara and the censer must leave the rewards.");
            });

            WithPlayer((stats, inventory, _) =>
            {
                float skill = stats.SkillDamageMultiplier;
                float basic = stats.BasicAttackRoomDamageMultiplier;
                Assert(inventory.TryAcquire(bluebird) && Near(stats.UltimateDamageMultiplier, skill + 1f) &&
                       Near(stats.SkillDamageMultiplier, skill) && Near(stats.BasicAttackRoomDamageMultiplier, basic - 0.3f),
                    "블랑셰의 유리 파랑새 must add 100% to the high-grade skill only and take 30% off the basic attack.");
                Assert(inventory.TryAcquire(bluebird) && inventory.TryAcquire(bluebird) &&
                       Near(stats.BasicAttackDamagePenalty, PlayerStats.MaximumBasicAttackDamagePenalty) &&
                       Near(PlayerStats.MaximumBasicAttackDamagePenalty, 0.8f) &&
                       Near(stats.BasicAttackRoomDamageMultiplier, basic - 0.8f) &&
                       Near(stats.UltimateDamageMultiplier, skill + 3f),
                    "Three bluebirds must stop the basic attack penalty at -80% while the high-grade bonus keeps adding.");
                Assert(ArtifactRewardSelector.IsEligible(bluebird, inventory),
                    "The bluebird has no high-grade cap, so it must stay offered.");
                stats.AddBasicAttackDamagePercent(0.5f);
                Assert(Near(stats.BasicAttackRoomDamageMultiplier, basic + 0.5f - 0.8f),
                    "Basic attack bonuses must still add on top of the penalty.");
            });

            // Enemies share Health but never get the bonus.
            GameObject enemy = new("Pool-2 Enemy", typeof(Health));
            try
            {
                Assert(Near(enemy.GetComponent<Health>().ShieldGainBonus, 0f), "An enemy must have no shield gain bonus.");
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        private static int CountUntilCapped(ItemDefinition item)
        {
            int count = 0;
            WithPlayer((_, inventory, _) =>
            {
                while (ArtifactRewardSelector.IsEligible(item, inventory))
                {
                    Assert(count < 100 && inventory.TryAcquire(item), $"{item.ItemId} never reached the health cap.");
                    count++;
                }
            });
            return count;
        }

        private static bool IsPureStat(ItemEffectType type) => type is ItemEffectType.MaxHealthFlat or
            ItemEffectType.CriticalChance or ItemEffectType.CriticalDamage or ItemEffectType.SkillDamagePercent or
            ItemEffectType.BasicAttackDamagePercent or ItemEffectType.AttackSpeedPercent or
            ItemEffectType.AttackDamagePercent or ItemEffectType.MoveSpeedPercent or
            ItemEffectType.MoveSpeedPenaltyPercent or ItemEffectType.ShieldGainBonus or
            ItemEffectType.UltimateDamagePercent or ItemEffectType.BasicAttackDamagePenaltyPercent;

        private readonly struct Snapshot
        {
            private Snapshot(float[] values) => Values = values;

            // attack, skill, crit chance, crit damage, attack speed, basic attack, max health, move speed, ultimate,
            // shield gain bonus
            private float[] Values { get; }

            public static Snapshot Of(PlayerStats stats) => new(new[]
            {
                stats.AttackDamage, stats.SkillDamageMultiplier, stats.CriticalChance, stats.CriticalDamageMultiplier,
                stats.AttackSpeed, stats.BasicAttackRoomDamageMultiplier, stats.MaxHealth, stats.MoveSpeed,
                stats.UltimateDamageMultiplier, stats.GetComponent<Health>().ShieldGainBonus,
            });

            // Percent stats multiply the base value; point stats and health add directly.
            public Snapshot With(ItemEffectType type, float amount, Snapshot baseline)
            {
                float[] values = (float[])Values.Clone();
                switch (type)
                {
                    case ItemEffectType.AttackDamagePercent: values[0] += baseline.Values[0] * amount; break;
                    // The high-grade skill multiplier holds the skill damage bonus too.
                    case ItemEffectType.SkillDamagePercent: values[1] += amount; values[8] += amount; break;
                    case ItemEffectType.CriticalChance: values[2] += amount; break;
                    case ItemEffectType.CriticalDamage: values[3] += amount; break;
                    case ItemEffectType.AttackSpeedPercent: values[4] += baseline.Values[4] * amount; break;
                    case ItemEffectType.BasicAttackDamagePercent: values[5] += amount; break;
                    case ItemEffectType.MaxHealthFlat: values[6] += amount; break;
                    case ItemEffectType.MoveSpeedPercent: values[7] += baseline.Values[7] * amount; break;
                    case ItemEffectType.MoveSpeedPenaltyPercent: values[7] -= baseline.Values[7] * amount; break;
                    case ItemEffectType.UltimateDamagePercent: values[8] += amount; break;
                    case ItemEffectType.BasicAttackDamagePenaltyPercent: values[5] -= amount; break;
                    case ItemEffectType.ShieldGainBonus: values[9] += amount; break;
                    default: throw new InvalidOperationException($"Pool-2 does not expect effect {type}.");
                }

                return new Snapshot(values);
            }

            public bool Matches(Snapshot other) => Values.Zip(other.Values, Near).All(ok => ok);

            public override string ToString() => string.Join(", ", new[]
                { "attack", "skill x", "crit", "crit x", "speed", "basic x", "hp", "move", "ultimate x", "shield+" }
                .Zip(Values, (name, value) => $"{name} {value:0.###}"));
        }

        private static void WithPlayer(Action<PlayerStats, PlayerInventory, Health> check)
        {
            GameObject player = new("Pool-2 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory));
            try
            {
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerInventory>(), "Awake");
                check(player.GetComponent<PlayerStats>(), player.GetComponent<PlayerInventory>(),
                    player.GetComponent<Health>());
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static ItemDefinition Load(string itemId)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week23Artifact2Setup.ItemPath(itemId));
            Assert(item != null, $"Missing artifact asset {itemId}. Run Pool-2 setup.");
            return item;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(component, null);
        }

        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) <= 0.001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
