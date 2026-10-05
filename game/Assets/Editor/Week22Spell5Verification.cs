using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Spell-5: four small single-use spells. 갑옷축제 초대장 adds two hearts of shield to the current shield, 아멜리아의
    // 러브레터 drops two one-heart pickups at free points around the player, 랜덤코인 gains 2~10 gold (refused at a full
    // wallet) and 회심의 일격 adds +15%p critical chance and +50%p critical damage in the combat room where it is used.
    public static class Week22Spell5Verification
    {
        public static void SetupAndVerifyBatch()
        {
            Week22Spell5Setup.Setup();
            string[] paths = Week22Spell5Setup.ItemIds.Select(Week22Spell5Setup.PathFor)
                .Append(Week22Chest1Setup.ChestContentTablePath).Append(Week13FrontendSetup.GameScenePath).ToArray();
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week22Spell5Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID)),
                "Spell-5 setup changed an item, chest table or Game Scene GUID.");
            Verify();
        }

        // Also re-runs the shared slot, the other single-use items on the same executor, the chest pools and the
        // development panel.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week21Slot0Verification.Verify();
            Week21Spell0Verification.Verify();
            Week21Spell1Verification.Verify();
            Week21Spell2Verification.Verify();
            Week21Spell3Verification.Verify();
            Week22Jjangsem0Verification.Verify();
            Week22Chest2Verification.Verify();
            Week22Chest1Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Spell-5 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Spell-5 Small Spells")]
        public static void Verify()
        {
            ValidateContract();
            ValidateSceneAndPools();
            // The runtime checks spawn pickups; keep them out of the Game Scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ValidateArmorInvitation();
            ValidateLoveLetter();
            ValidateRandomCoin();
            ValidateDecisiveStrike();
            Debug.Log("Spell-5 verification passed: 갑옷축제 초대장 adds two hearts of shield on top of the current " +
                      "shield, 아멜리아의 러브레터 drops two one-heart pickups at free points around the player, " +
                      "랜덤코인 gains 2~10 gold and is refused at a full wallet, 회심의 일격 adds +15%p critical chance " +
                      "and +50%p critical damage in the combat room it is used in, and chests hold all four.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.GainShield == 37 && (int)ItemEffectType.SpawnHealthPickups == 38 &&
                   (int)ItemEffectType.GainRandomGold == 39 &&
                   (int)ItemEffectType.CurrentRoomCriticalBonus == 40 &&
                   (int)ItemEffectType.ReduceAndRecoverDamageTaken == 36,
                "Spell-5 must add effect types 37~40 after the existing effects without renumbering them.");
            Assert(!new ItemEffectEntry(ItemEffectType.GainShield).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.SpawnHealthPickups).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.GainRandomGold, configuredMagnitude: 10f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.GainRandomGold, configuredMagnitude: 1f,
                       configuredIntegerAmount: 2).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentRoomCriticalBonus).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentRoomCriticalBonus, configuredMagnitude: 0.5f,
                       configuredSecondaryMagnitude: 1.5f).TryValidate(out _),
                "Spell-5 effects must reject missing amounts and a gold range whose maximum is below its minimum.");

            AssertItem(Week22Spell5Setup.ArmorInvitationId, "갑옷축제 초대장", ItemRarity.Rare, "방어막 2칸 획득",
                effect => effect.EffectType == ItemEffectType.GainShield && Mathf.Approximately(effect.Magnitude, 4f));
            AssertItem(Week22Spell5Setup.LoveLetterId, "아멜리아의 러브레터", ItemRarity.Uncommon, "주변에 하트 2개 생성",
                effect => effect.EffectType == ItemEffectType.SpawnHealthPickups && effect.IntegerAmount == 2);
            AssertItem(Week22Spell5Setup.RandomCoinId, "랜덤코인", ItemRarity.Common, "골드 2~10 무작위 획득",
                effect => effect.EffectType == ItemEffectType.GainRandomGold && effect.IntegerAmount == 2 &&
                          Mathf.Approximately(effect.Magnitude, 10f));
            AssertItem(Week22Spell5Setup.DecisiveStrikeId, "회심의 일격", ItemRarity.Uncommon,
                "사용한 전투방에서 치명타 확률 +15%p·치명타 피해 +50%p (방을 떠나면 해제)",
                effect => effect.EffectType == ItemEffectType.CurrentRoomCriticalBonus &&
                          Mathf.Approximately(effect.Magnitude, 0.5f) &&
                          Mathf.Approximately(effect.SecondaryMagnitude, 0.15f));
        }

        private static void AssertItem(string itemId, string displayName, ItemRarity rarity, string description,
            Func<ItemEffectEntry, bool> isExpectedEffect)
        {
            ItemDefinition item = LoadItem(itemId);
            Assert(item.ItemId == itemId && item.DisplayName == displayName && item.Kind == ItemKind.SingleUseSpell &&
                   item.Rarity == rarity && item.IsActive && item.MaxStacks == 1 && item.IsValid && item.IsSingleUse &&
                   ItemDefinition.IsItemIdValidForKind(itemId, ItemKind.SingleUseSpell),
                $"{itemId} must be a valid active {rarity} single-use spell named {displayName}.");
            Assert(item.Effects.Count == 1 && isExpectedEffect(item.Effects[0]),
                $"{itemId} must carry exactly its decided effect values.");
            string built = ArtifactEffectDescription.Build(item);
            Assert(built == description, $"Unexpected {itemId} description: {built}");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null && font.HasCharacters(displayName + built), $"The Frontend font is missing a {itemId} glyph.");
        }

        private static void ValidateSceneAndPools()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            HealthPickup heart = AssetDatabase.LoadAssetAtPath<GameObject>(Week17Resource0Setup.PrefabPath)
                ?.GetComponent<HealthPickup>();
            PlayerSingleUseEffects effects = assembler.Graph.Player.GetComponent<PlayerSingleUseEffects>();
            Assert(heart != null && heart.HealUnits == HealthUnits.UnitsPerHeart && effects != null &&
                   effects.HeartPickupPrefab == heart,
                "The Game Scene player must drop the ordinary one-heart pickup for 아멜리아의 러브레터.");

            ChestContentTable table =
                AssetDatabase.LoadAssetAtPath<ChestContentTable>(Week22Chest1Setup.ChestContentTablePath);
            Assert(table != null && table.TryValidate(out string error), "The chest content table is missing or invalid.");
            ItemDefinition[] items = Week22Spell5Setup.ItemIds.Select(LoadItem).ToArray();
            Assert(items.All(item => table.Spells.Contains(item) && !table.JjangsemSpells.Contains(item)) &&
                   assembler.ChestContentTable == table,
                "Chests must draw all four spells from the single-use spell list.");
            Assert(assembler.SelectionRewardPool.All(item => item != null && !item.IsSingleUse),
                "Single-use spells must not join the selection reward pool (Contract-0 §3).");
        }

        private static void ValidateArmorInvitation()
        {
            using TestPlayer test = new();
            ItemDefinition item = LoadItem(Week22Spell5Setup.ArmorInvitationId);
            Health health = test.Health;

            test.Give(item, "spell5-armor-1");
            Time.timeScale = 0f;
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Paused && test.Slot.HasItem &&
                   Mathf.Approximately(health.CurrentShield, 0f), "갑옷축제 초대장 must not be used while paused.");
            Time.timeScale = 1f;
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem &&
                   Mathf.Approximately(health.CurrentShield, 4f) &&
                   Mathf.Approximately(health.CurrentHealth, health.MaxHealth),
                "갑옷축제 초대장 must add two hearts of shield at full HP and be consumed.");
            health.TakeDamage(2f);
            Assert(Mathf.Approximately(health.CurrentShield, 2f) &&
                   Mathf.Approximately(health.CurrentHealth, health.MaxHealth),
                "The shield must absorb a hit before HP.");
            test.Give(item, "spell5-armor-2");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && Mathf.Approximately(health.CurrentShield, 6f),
                "A second 갑옷축제 초대장 must add to the shield that is left.");
        }

        private static void ValidateLoveLetter()
        {
            ItemDefinition item = LoadItem(Week22Spell5Setup.LoveLetterId);
            HealthPickup heartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week17Resource0Setup.PrefabPath)
                .GetComponent<HealthPickup>();
            GameObject wall = new("Spell-5 Wall", typeof(BoxCollider2D));
            try
            {
                using TestPlayer test = new();
                test.Give(item, "spell5-letter-1");
                Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HeldDefinition == item &&
                       Hearts().Length == 0,
                    "아멜리아의 러브레터 must be refused and kept while no heart pickup is configured.");

                // The first drop point (right of the player) is inside a wall.
                test.Effects.ConfigureHeartPickup(heartPrefab);
                Vector2 origin = test.Player.transform.position;
                wall.layer = LayerMask.NameToLayer("Environment");
                wall.transform.position = origin + Vector2.right * PlayerSingleUseEffects.HeartDropDistance;
                Physics2D.SyncTransforms();

                Assert(Mathf.Approximately(test.Health.CurrentHealth, test.Health.MaxHealth) &&
                       test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem,
                    "아멜리아의 러브레터 must be usable at full HP and be consumed.");
                HealthPickup[] hearts = Hearts();
                Assert(hearts.Length == 2 && hearts.All(heart => heart.HealUnits == HealthUnits.UnitsPerHeart &&
                                                                 !heart.CanCollect(test.Health)),
                    "Two one-heart pickups must drop and stay on the floor at full HP.");
                Assert(hearts.All(heart => Mathf.Abs(Vector2.Distance(heart.transform.position, origin) -
                                                     PlayerSingleUseEffects.HeartDropDistance) < 0.001f) &&
                       Vector2.Distance(hearts[0].transform.position, hearts[1].transform.position) > 0.5f,
                    "The hearts must drop at separate points around the player.");
                Assert(hearts.All(heart => Vector2.Distance(heart.transform.position, wall.transform.position) > 0.5f),
                    "The hearts must avoid a blocked drop point.");

                test.Health.TakeDamage(2f);
                Assert(hearts[0].Collect(test.Health) && Mathf.Approximately(test.Health.CurrentHealth, test.Health.MaxHealth),
                    "A dropped heart must heal one heart once the player is missing HP.");
            }
            finally
            {
                foreach (HealthPickup heart in Hearts()) Object.DestroyImmediate(heart.gameObject);
                Object.DestroyImmediate(wall);
            }
        }

        private static void ValidateRandomCoin()
        {
            using TestPlayer test = new();
            ItemDefinition item = LoadItem(Week22Spell5Setup.RandomCoinId);
            RunProgress progress = test.Progress;
            try
            {
                HashSet<int> seen = new();
                for (int index = 0; index < 80; index++)
                {
                    int before = progress.GetResourceCount(RunResourceType.Gold);
                    if (before > 80) Assert(progress.TrySpendResource(RunResourceType.Gold, before), "Reset gold.");
                    before = progress.GetResourceCount(RunResourceType.Gold);
                    test.Give(item, $"spell5-coin-{index}");
                    Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem, "Use 랜덤코인.");
                    int gained = progress.GetResourceCount(RunResourceType.Gold) - before;
                    Assert(gained >= 2 && gained <= 10 && gained == test.Effects.LastGoldGained,
                        $"랜덤코인 gained {gained} gold outside 2~10.");
                    seen.Add(gained);
                }

                Assert(seen.Count >= 4, "랜덤코인 must give varying amounts.");

                // The roll is clamped to the range and clipped by the wallet limit; a full wallet refuses the coin.
                int gold = progress.GetResourceCount(RunResourceType.Gold);
                Assert(gold > 0 && progress.TrySpendResource(RunResourceType.Gold, gold), "Reset gold.");
                PlayerSingleUseEffects.SetGoldRollProviderForTesting((_, _) => 99);
                test.Give(item, "spell5-coin-max");
                Assert(test.Slot.TryUse() == SpellSlotUseResult.Used &&
                       progress.GetResourceCount(RunResourceType.Gold) == 10, "A roll above the range must give 10.");
                PlayerSingleUseEffects.SetGoldRollProviderForTesting((_, _) => 0);
                test.Give(item, "spell5-coin-min");
                Assert(test.Slot.TryUse() == SpellSlotUseResult.Used &&
                       progress.GetResourceCount(RunResourceType.Gold) == 12, "A roll below the range must give 2.");

                PlayerSingleUseEffects.SetGoldRollProviderForTesting((_, maximum) => maximum);
                progress.TryAddResource(RunResourceType.Gold, 1000);
                int limit = progress.GetResourceCount(RunResourceType.Gold);
                test.Give(item, "spell5-coin-full");
                Assert(!progress.CanAcceptResource(RunResourceType.Gold) &&
                       test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HeldDefinition == item &&
                       progress.GetResourceCount(RunResourceType.Gold) == limit,
                    "랜덤코인 must be refused and kept while the wallet is full.");
                Assert(progress.TrySpendResource(RunResourceType.Gold, 3) &&
                       test.Slot.TryUse() == SpellSlotUseResult.Used &&
                       progress.GetResourceCount(RunResourceType.Gold) == limit && test.Effects.LastGoldGained == 3,
                    "The wallet limit must clip the gold gained.");
            }
            finally
            {
                PlayerSingleUseEffects.ResetGoldRollProvider();
            }
        }

        private static void ValidateDecisiveStrike()
        {
            using TestPlayer test = new();
            ItemDefinition item = LoadItem(Week22Spell5Setup.DecisiveStrikeId);
            PlayerSingleUseEffects effects = test.Effects;
            float baseCritical = test.Stats.CriticalDamageMultiplier;
            float baseChance = test.Stats.CriticalChance;

            test.Progress.RecordRoomEntry(1, 1);
            test.Give(item, "spell5-strike-1");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HeldDefinition == item,
                "The start room must refuse 회심의 일격 and keep it in the slot.");
            test.Progress.GetRoomState(FloorGenerator.BuildRoomId(1, 2)).MarkPreCleared();
            test.Progress.RecordRoomEntry(1, 2);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HasItem,
                "A cleared combat room must refuse 회심의 일격.");

            test.Progress.RecordRoomEntry(1, 3);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem && effects.IsRoomBoostActive &&
                   Mathf.Approximately(effects.RoomCriticalDamagePercent, 0.5f) &&
                   Mathf.Approximately(effects.RoomCriticalChancePercent, 0.15f) &&
                   Mathf.Approximately(test.Stats.CriticalDamageMultiplier, baseCritical + 0.5f) &&
                   Mathf.Approximately(test.Stats.CriticalChance, baseChance + 0.15f),
                "An uncleared combat room must consume 회심의 일격 and add +15%p chance and +50%p critical damage.");
            Assert(Mathf.Approximately(test.Stats.BasicAttackRoomDamageMultiplier, 1f) &&
                   Mathf.Approximately(effects.RoomAttackDamagePercent, 0f) &&
                   Mathf.Approximately(effects.RoomAttackSpeedPercent, 0f),
                "회심의 일격 must change the critical stats only.");
            DamageContext context = test.Stats.CreateDirectDamageContext(test.Player, DamageSourceType.PlayerProjectile);
            Assert(Mathf.Approximately(context.CriticalDamageMultiplier, baseCritical + 0.5f) &&
                   Mathf.Approximately(context.CriticalChance, baseChance + 0.15f),
                "Basic attacks from the used room must carry both critical bonuses.");

            test.Give(item, "spell5-strike-2");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used &&
                   Mathf.Approximately(test.Stats.CriticalDamageMultiplier, baseCritical + 1f) &&
                   Mathf.Approximately(test.Stats.CriticalChance, baseChance + 0.3f),
                "A second use in the same room must add another +15%p chance and +50%p damage.");

            test.Progress.RecordRoomEntry(1, 1);
            Assert(!effects.IsRoomBoostActive && Mathf.Approximately(test.Stats.CriticalDamageMultiplier, baseCritical) &&
                   Mathf.Approximately(test.Stats.CriticalChance, baseChance),
                "Leaving the room must remove both critical bonuses.");
            test.Progress.RecordRoomEntry(1, 3);
            Assert(Mathf.Approximately(test.Stats.CriticalDamageMultiplier, baseCritical) &&
                   Mathf.Approximately(test.Stats.CriticalChance, baseChance),
                "Revisiting the used room must not bring the bonuses back.");

            test.Give(item, "spell5-strike-3");
            test.Progress.RecordRoomEntry(1, 5);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used &&
                   Mathf.Approximately(test.Stats.CriticalDamageMultiplier, baseCritical + 0.5f),
                "An uncleared boss room must accept 회심의 일격.");
            test.Health.TakeDamage(test.Health.MaxHealth * 10f);
            effects.RefreshRoomBoost();
            Assert(test.Health.IsDead && Mathf.Approximately(test.Stats.CriticalDamageMultiplier, baseCritical) &&
                   Mathf.Approximately(test.Stats.CriticalChance, baseChance),
                "Death must remove both critical bonuses.");
        }

        private static HealthPickup[] Hearts() =>
            Object.FindObjectsByType<HealthPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static ItemDefinition LoadItem(string itemId)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week22Spell5Setup.PathFor(itemId));
            Assert(item != null, $"Run Spell-5 setup first: {Week22Spell5Setup.PathFor(itemId)} is missing.");
            return item;
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        // A player with the real slot and executor, half-heart health units and a small generated graph: floor 1 has a
        // start room (1), combat rooms (2, 3), a treasure room (4) and a boss room (5).
        private sealed class TestPlayer : IDisposable
        {
            private readonly GameObject progressObject;
            private readonly GameObject floor;
            private readonly SingleUseItemPickup prefab;
            private readonly float previousTimeScale;

            public GameObject Player { get; }
            public RunProgress Progress { get; }
            public Health Health { get; }
            public PlayerStats Stats { get; }
            public PlayerSpellSlot Slot { get; }
            public PlayerSingleUseEffects Effects { get; }

            public TestPlayer()
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;
                prefab = AssetDatabase.LoadAssetAtPath<SingleUseItemPickup>(Week21Slot0Setup.PickupPrefabPath);
                Assert(prefab != null, "Run Slot-0 setup first: the single-use pickup Prefab is missing.");
                progressObject = new GameObject("Spell-5 Progress", typeof(RunProgress));
                floor = new GameObject("Spell-5 Floor");
                Progress = progressObject.GetComponent<RunProgress>();
                ConfigureTestGraph(Progress);
                Player = new GameObject("Spell-5 Player", typeof(Health), typeof(PlayerSP), typeof(PlayerStats),
                    typeof(PlayerInventory), typeof(PlayerSpellSlot));
                Player.transform.position = new Vector3(9000f, 9000f, 0f);
                Health = Player.GetComponent<Health>();
                Stats = Player.GetComponent<PlayerStats>();
                Slot = Player.GetComponent<PlayerSpellSlot>();
                Effects = Player.GetComponent<PlayerSingleUseEffects>();
                PlayerInventory inventory = Player.GetComponent<PlayerInventory>();
                typeof(PlayerInventory).GetField("runProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(inventory, Progress);
                InvokeLifecycle(Health, "Awake");
                Health.EnableHealthUnits();
                InvokeLifecycle(Player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(Stats, "Awake");
                InvokeLifecycle(inventory, "Awake");
                Slot.Configure(Progress, prefab);
                InvokeLifecycle(Slot, "Awake");
                InvokeLifecycle(Effects, "Awake");
            }

            public void Give(ItemDefinition item, string instanceId)
            {
                SingleUseItemPickup pickup = Object.Instantiate(prefab, floor.transform);
                pickup.Configure(item, instanceId, false);
                Assert(Slot.TryCollect(pickup) && Slot.HeldDefinition == item, $"The slot must take {item.ItemId}.");
            }

            public void Dispose()
            {
                Time.timeScale = previousTimeScale;
                Object.DestroyImmediate(Player);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(progressObject);
            }

            private static void ConfigureTestGraph(RunProgress progress)
            {
                GeneratedRoomNode[] nodes =
                {
                    Node(1, GeneratedRoomRole.Start),
                    Node(2, GeneratedRoomRole.Intermediate),
                    Node(3, GeneratedRoomRole.Intermediate),
                    Node(4, GeneratedRoomRole.Treasure),
                    Node(5, GeneratedRoomRole.Boss),
                };
                GeneratedFloor first = new(1, 0, 0, 0, nodes[0].RoomId, nodes[4].RoomId, nodes);
                FieldInfo graphField = typeof(RunProgress).GetField("<GeneratedGraph>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (graphField == null) throw new InvalidOperationException("RunProgress graph storage was not found.");
                graphField.SetValue(progress, new GeneratedFloorGraph(new[] { first }, 1));

                FieldInfo statesField = typeof(RunProgress).GetField("roomStates",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Dictionary<string, RoomRunState> states =
                    (Dictionary<string, RoomRunState>)statesField?.GetValue(progress);
                if (states == null) throw new InvalidOperationException("RunProgress room state storage was not found.");
                foreach (GeneratedRoomNode node in nodes) states.Add(node.RoomId, new RoomRunState(node.RoomId));
            }

            private static GeneratedRoomNode Node(int roomNumber, GeneratedRoomRole role) =>
                new(FloorGenerator.BuildRoomId(1, roomNumber), 1, roomNumber, role, null);
        }
    }
}
