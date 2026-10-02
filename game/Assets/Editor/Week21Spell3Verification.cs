using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    // Spell-3: 막판 스퍼트 becomes a single-use spell. Used in an uncleared boss room it adds +30% attack speed and +5%
    // move speed in that room only; any other room or a cleared boss room refuses it unconsumed (D0), leaving the room
    // ends it and a revisit never brings it back. The legacy spell-final-sprint keeps its contract for past records but
    // leaves the selection reward pool, which now holds no legacy spell at all.
    public static class Week21Spell3Verification
    {
        private const float BaseMoveSpeed = 5f;

        public static void SetupAndVerifyBatch()
        {
            Week21Spell3Setup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(Week21Spell3Setup.FinalSprintPath);
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week21Spell3Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) &&
                   guid == AssetDatabase.AssetPathToGUID(Week21Spell3Setup.FinalSprintPath) &&
                   sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Spell-3 setup changed an asset or Game Scene GUID.");
            Verify();
        }

        // Also re-runs the checks that shared the room bonus or the reward pool with this change.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week21Spell1Verification.Verify();
            Week21Spell2Verification.Verify();
            Week16Spell1Verification.Verify();
            Week16Reward3Verification.Verify();
            Debug.Log("Spell-3 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 21/Verify Spell-3 Final Sprint")]
        public static void Verify()
        {
            ValidateContract();
            ValidateRewardPool();
            ValidateRoomRules();
            ValidateLegacyCoexistence();
            ValidateStopConditions();
            Debug.Log("Spell-3 verification passed: 막판 스퍼트 is a single-use spell that adds +30% attack speed and " +
                      "+5% move speed (attack damage unchanged) only in the uncleared boss room where it is used, is " +
                      "refused unconsumed in any other room or a cleared boss room, ends on leaving, death or Run end " +
                      "and never returns on a revisit; the legacy spell keeps its contract and every legacy spell " +
                      "left the selection reward pool.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.CurrentBossRoomSpeedPercent == 32 &&
                   (int)ItemEffectType.EscapeToFloorStartRoom == 31 &&
                   (int)ItemEffectType.BossRoomAttackSpeedPercent == 22 &&
                   (int)ItemEffectType.BossRoomMoveSpeedPercent == 23,
                "Spell-3 must add effect type 32 without renumbering the legacy effects 22 and 23.");

            ItemDefinition item = LoadItem(Week21Spell3Setup.FinalSprintPath);
            Assert(item.ItemId == Week21Spell3Setup.FinalSprintId && item.DisplayName == "막판 스퍼트" &&
                   item.Kind == ItemKind.SingleUseSpell && item.Rarity == ItemRarity.Rare && item.IsActive &&
                   item.MaxStacks == 1 && item.IsValid && item.IsSingleUse,
                "single-spell-final-sprint must be a valid active Rare single-use spell named 막판 스퍼트.");
            Assert(item.Effects.Count == 1 &&
                   item.Effects[0].EffectType == ItemEffectType.CurrentBossRoomSpeedPercent &&
                   Approximately(item.Effects[0].Magnitude, 0.3f) &&
                   Approximately(item.Effects[0].SecondaryMagnitude, 0.05f),
                "막판 스퍼트 must keep the +30% attack speed and +5% move speed values.");
            Assert(ArtifactEffectDescription.Build(item) ==
                   "사용한 보스방에서 공격속도 +30%·이동속도 +5% (방을 떠나면 해제)",
                "막판 스퍼트 must describe its boss-room effect.");
            Assert(!new ItemEffectEntry(ItemEffectType.CurrentBossRoomSpeedPercent,
                       configuredSecondaryMagnitude: 0.05f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentBossRoomSpeedPercent, 0.3f,
                       configuredSecondaryMagnitude: -0.05f).TryValidate(out _),
                "A boss-room speed effect needs a positive attack speed and a non-negative move speed.");

            ItemDefinition legacy = LoadItem(Week21Spell3Setup.LegacyFinalSprintPath);
            Assert(legacy.ItemId == Week21Spell3Setup.LegacyFinalSprintId && legacy.DisplayName == "막판 스퍼트" &&
                   legacy.Kind == ItemKind.Spell && legacy.Rarity == ItemRarity.Rare && legacy.IsActive &&
                   legacy.IsValid && !legacy.IsSingleUse && legacy.Effects.Count == 2 &&
                   legacy.Effects[0].EffectType == ItemEffectType.BossRoomAttackSpeedPercent &&
                   Approximately(legacy.Effects[0].Magnitude, 0.3f) &&
                   legacy.Effects[1].EffectType == ItemEffectType.BossRoomMoveSpeedPercent &&
                   Approximately(legacy.Effects[1].Magnitude, 0.05f),
                "The legacy spell-final-sprint contract must stay unchanged and active (Contract-0 §2.2).");
        }

        private static void ValidateRewardPool()
        {
            Assert(new[] { "spell-catch-that-one", "spell-afterimage", Week21Spell3Setup.LegacyFinalSprintId }
                       .All(id => LegacySpellRetirement.RetiredItemIds.Contains(id)) &&
                   LegacySpellRetirement.IsRetired(LoadItem(Week21Spell3Setup.LegacyFinalSprintPath)) &&
                   !LegacySpellRetirement.IsRetired(LoadItem(Week21Spell3Setup.FinalSprintPath)),
                "All three legacy spells, and only they, must be retired.");

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            IReadOnlyList<ItemDefinition> pool = assembler.SelectionRewardPool;
            Assert(pool.Count >= ArtifactRewardSelector.MaximumCandidateCount &&
                   pool.All(item => item != null && item.IsActive && item.IsValid),
                "The selection reward pool must keep enough active valid items.");
            Assert(pool.All(item => item.Kind == ItemKind.Artifact),
                "The selection reward pool and shop stock must hold Artifacts only once every legacy spell is " +
                "retired (Contract-0 §3).");
        }

        private static void ValidateRoomRules()
        {
            using TestPlayer test = new();
            ItemDefinition item = LoadItem(Week21Spell3Setup.FinalSprintPath);
            ItemDefinition catchThatOne = LoadItem(Week21Spell1Setup.CatchThatOnePath);
            PlayerSingleUseEffects effects = test.Effects;

            test.Give(item, "spell3-1");
            foreach (int room in new[] { 1, 3, 4 })
            {
                test.Progress.RecordRoomEntry(1, room);
                Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HeldDefinition == item &&
                       !effects.IsRoomBoostActive,
                    $"Room {room} (start, combat or treasure) must refuse 막판 스퍼트 and keep it in the slot.");
            }

            string bossRoomId = FloorGenerator.BuildRoomId(1, 5);
            test.Progress.RecordRoomEntry(1, 5);
            Time.timeScale = 0f;
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Paused && test.Slot.HasItem && !effects.IsRoomBoostActive,
                "막판 스퍼트 must not be used while paused.");
            Time.timeScale = 1f;

            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem &&
                   effects.IsRoomBoostActive && effects.RoomBoostRoomId == bossRoomId &&
                   Approximately(effects.RoomAttackSpeedPercent, 0.3f) &&
                   Approximately(effects.RoomMoveSpeedPercent, 0.05f),
                "An uncleared boss room must consume 막판 스퍼트 and start the room bonus.");
            AssertStats(test, 1.3f, 1.05f, 1f, "The used boss room must grant +30% attack and +5% move speed only.");

            test.Give(item, "spell3-2");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used &&
                   Approximately(effects.RoomAttackSpeedPercent, 0.6f),
                "A second use in the same boss room must add another bonus.");
            AssertStats(test, 1.6f, 1.1f, 1f, "Two uses in one boss room must add up.");

            test.Give(catchThatOne, "spell3-catch");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used,
                "저놈 잡아라 must be usable in the same boss room.");
            AssertStats(test, 1.6f, 1.1f, 1.1f, "저놈 잡아라 must add its damage without removing the speed bonus.");

            test.Progress.GetRoomState(bossRoomId).MarkCleared();
            effects.RefreshRoomBoost();
            Assert(effects.IsRoomBoostActive, "Defeating the boss must keep the bonus until the player leaves.");
            test.Give(item, "spell3-cleared");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HasItem,
                "A cleared boss room must refuse 막판 스퍼트.");

            test.Progress.RecordRoomEntry(1, 1);
            Assert(!effects.IsRoomBoostActive && effects.RoomBoostRoomId == null,
                "Leaving the boss room must end the bonus.");
            AssertStats(test, 1f, 1f, 1f, "Leaving the boss room must remove every room bonus.");
            test.Progress.RecordRoomEntry(1, 5);
            Assert(!effects.IsRoomBoostActive, "Revisiting the boss room must not bring the bonus back.");
            AssertStats(test, 1f, 1f, 1f, "A revisit must keep the base speeds.");

            // A boss room on the next floor accepts it, and the floor change of the first use ends that bonus.
            test.Progress.RecordRoomEntry(2, 5);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && effects.IsRoomBoostActive,
                "The next floor's uncleared boss room must accept 막판 스퍼트.");
            test.Progress.RecordRoomEntry(2, 1);
            Assert(!effects.IsRoomBoostActive, "Leaving the next floor's boss room must end the bonus.");

            Assert(test.Inventory.AcquiredItems.Count(record => record.ItemId == item.ItemId) == 3,
                "Each 막판 스퍼트 instance must be recorded once.");
        }

        // The legacy spell drives its own boss-room source; neither may overwrite the other.
        private static void ValidateLegacyCoexistence()
        {
            using TestPlayer test = new();
            ItemDefinition item = LoadItem(Week21Spell3Setup.FinalSprintPath);
            ItemDefinition legacy = LoadItem(Week21Spell3Setup.LegacyFinalSprintPath);

            test.Progress.RecordRoomEntry(1, 1);
            Assert(test.Inventory.TryAcquire(legacy), "The legacy spell must still be acquirable for old loadouts.");
            test.Progress.RecordRoomEntry(1, 5);
            AssertStats(test, 1.3f, 1.05f, 1f, "The legacy spell must still apply in the uncleared boss room.");
            test.Give(item, "spell3-legacy");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "The single-use spell must be usable alongside it.");
            AssertStats(test, 1.6f, 1.1f, 1f, "Legacy and single-use boss-room bonuses must add up.");
            test.Progress.RecordRoomEntry(1, 1);
            AssertStats(test, 1f, 1f, 1f, "Leaving the boss room must end both bonuses.");
        }

        private static void ValidateStopConditions()
        {
            ItemDefinition item = LoadItem(Week21Spell3Setup.FinalSprintPath);
            using (TestPlayer test = new())
            {
                test.Progress.RecordRoomEntry(1, 5);
                test.Give(item, "spell3-stop");
                Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "Start a boss-room bonus before the Run ends.");
                test.Progress.StopProgression();
                test.Effects.RefreshRoomBoost();
                Assert(!test.Effects.IsRoomBoostActive, "The end of the Run must end the bonus.");
                AssertStats(test, 1f, 1f, 1f, "The end of the Run must remove the speed bonus.");
            }

            using (TestPlayer test = new())
            {
                test.Progress.RecordRoomEntry(1, 5);
                test.Give(item, "spell3-death");
                Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "Start a boss-room bonus before death.");
                test.Health.TakeDamage(test.Health.MaxHealth * 10f);
                test.Effects.RefreshRoomBoost();
                Assert(test.Health.IsDead && !test.Effects.IsRoomBoostActive, "Death must end the bonus.");
            }
        }

        // Attack speed and move speed are multipliers of the base values; damage is the basic attack multiplier.
        private static void AssertStats(TestPlayer test, float attackSpeed, float moveSpeed, float basicDamage,
            string message)
        {
            PlayerStats stats = test.Stats;
            Assert(Approximately(stats.AttackSpeed, attackSpeed) &&
                   Approximately(stats.MoveSpeed, BaseMoveSpeed * moveSpeed) &&
                   Approximately(stats.CreateDirectDamageContext(stats.gameObject, DamageSourceType.PlayerAttack)
                       .Multiplier, basicDamage),
                $"{message} (attack speed {stats.AttackSpeed}, move speed {stats.MoveSpeed})");
        }

        private static ItemDefinition LoadItem(string path)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            Assert(item != null, $"Run the Spell-1 and Spell-3 setups first: {path} is missing.");
            return item;
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException($"Field {fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static bool Approximately(float actual, float expected) => Mathf.Abs(actual - expected) <= 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        // A player with the real slot and executor on a two-floor test graph: start, two combat rooms, treasure and
        // boss on floor 1, and start and boss on floor 2.
        private sealed class TestPlayer : IDisposable
        {
            private readonly GameObject progressObject;
            private readonly GameObject floor;
            private readonly GameObject player;
            private readonly SingleUseItemPickup prefab;
            private readonly float previousTimeScale;

            public RunProgress Progress { get; }
            public Health Health { get; }
            public PlayerStats Stats { get; }
            public PlayerInventory Inventory { get; }
            public PlayerSpellSlot Slot { get; }
            public PlayerSingleUseEffects Effects { get; }

            public TestPlayer()
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;
                prefab = AssetDatabase.LoadAssetAtPath<SingleUseItemPickup>(Week21Slot0Setup.PickupPrefabPath);
                Assert(prefab != null, "Run Slot-0 setup first: the single-use pickup Prefab is missing.");
                progressObject = new GameObject("Spell-3 Progress", typeof(RunProgress));
                floor = new GameObject("Spell-3 Floor");
                Progress = progressObject.GetComponent<RunProgress>();
                ConfigureTestGraph(Progress);
                player = new GameObject("Spell-3 Player", typeof(Health), typeof(PlayerSP), typeof(PlayerStats),
                    typeof(PlayerInventory), typeof(PlayerSpellSlot));
                Health = player.GetComponent<Health>();
                Stats = player.GetComponent<PlayerStats>();
                Inventory = player.GetComponent<PlayerInventory>();
                Slot = player.GetComponent<PlayerSpellSlot>();
                Effects = player.GetComponent<PlayerSingleUseEffects>();
                SetField(Inventory, "runProgress", Progress);
                InvokeLifecycle(Health, "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(Stats, "Awake");
                InvokeLifecycle(Inventory, "Awake");
                Slot.Configure(Progress, prefab);
                InvokeLifecycle(Slot, "Awake");
                InvokeLifecycle(Effects, "Awake");
                Assert(Approximately(Stats.AttackSpeed, 1f) && Approximately(Stats.MoveSpeed, BaseMoveSpeed),
                    "The Spell-3 test player must start at the default speeds.");
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
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(progressObject);
            }

            private static void ConfigureTestGraph(RunProgress progress)
            {
                GeneratedRoomNode[] firstNodes =
                {
                    Node(1, 1, GeneratedRoomRole.Start),
                    Node(1, 2, GeneratedRoomRole.Intermediate),
                    Node(1, 3, GeneratedRoomRole.Intermediate),
                    Node(1, 4, GeneratedRoomRole.Treasure),
                    Node(1, 5, GeneratedRoomRole.Boss),
                };
                GeneratedRoomNode[] secondNodes =
                {
                    Node(2, 1, GeneratedRoomRole.Start),
                    Node(2, 5, GeneratedRoomRole.Boss),
                };
                GeneratedFloor first = new(1, 0, 0, 0, firstNodes[0].RoomId, firstNodes[4].RoomId, firstNodes);
                GeneratedFloor second = new(2, 0, 0, 0, secondNodes[0].RoomId, secondNodes[1].RoomId, secondNodes);
                FieldInfo graphField = typeof(RunProgress).GetField("<GeneratedGraph>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (graphField == null) throw new InvalidOperationException("RunProgress graph storage was not found.");
                graphField.SetValue(progress, new GeneratedFloorGraph(new[] { first, second }, 1));

                FieldInfo statesField = typeof(RunProgress).GetField("roomStates",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Dictionary<string, RoomRunState> states =
                    (Dictionary<string, RoomRunState>)statesField?.GetValue(progress);
                if (states == null) throw new InvalidOperationException("RunProgress room state storage was not found.");
                foreach (GeneratedRoomNode node in firstNodes.Concat(secondNodes))
                    states.Add(node.RoomId, new RoomRunState(node.RoomId));
            }

            private static GeneratedRoomNode Node(int floorNumber, int roomNumber, GeneratedRoomRole role) =>
                new(FloorGenerator.BuildRoomId(floorNumber, roomNumber), floorNumber, roomNumber, role, null);
        }
    }
}
