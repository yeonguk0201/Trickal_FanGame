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
    // Spell-1: 저놈 잡아라 becomes a single-use spell. Used in an uncleared combat room it adds +10% basic attack
    // damage (not skills) in that room only; safe or cleared rooms refuse it unconsumed, leaving the room ends it and a
    // revisit never brings it back. The legacy spell-catch-that-one keeps its contract for past records but leaves
    // the selection reward pool (and the shop stock that shares it).
    public static class Week21Spell1Verification
    {
        public static void SetupAndVerifyBatch()
        {
            Week21Spell1Setup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(Week21Spell1Setup.CatchThatOnePath);
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week21Spell1Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) &&
                   guid == AssetDatabase.AssetPathToGUID(Week21Spell1Setup.CatchThatOnePath) &&
                   sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Spell-1 setup changed an asset or Game Scene GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 21/Verify Spell-1 Catch That One")]
        public static void Verify()
        {
            ValidateContract();
            ValidateRewardPool();
            ValidateRoomRules();
            ValidateLegacyCoexistence();
            ValidateStopConditions();
            Debug.Log("Spell-1 verification passed: 저놈 잡아라 is a single-use spell that adds +10% basic attack " +
                      "damage (skills unchanged) only in the uncleared combat room where it is used, is refused " +
                      "unconsumed in safe or cleared rooms, ends on leaving, death or Run end and never returns " +
                      "on a revisit; the legacy spell keeps its contract and left the selection reward pool.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.CurrentRoomBasicAttackDamagePercent == 30 &&
                   (int)ItemEffectType.NextCombatRoomAttackDamagePercent == 21,
                "Spell-1 must add effect type 30 without renumbering the legacy effect 21.");

            ItemDefinition item = LoadItem(Week21Spell1Setup.CatchThatOnePath);
            Assert(item.ItemId == Week21Spell1Setup.CatchThatOneId && item.DisplayName == "저놈 잡아라" &&
                   item.Kind == ItemKind.SingleUseSpell && item.Rarity == ItemRarity.Uncommon && item.IsActive &&
                   item.MaxStacks == 1 && item.IsValid && item.IsSingleUse,
                "single-spell-catch-that-one must be a valid active Uncommon single-use spell named 저놈 잡아라.");
            Assert(item.Effects.Count == 1 &&
                   item.Effects[0].EffectType == ItemEffectType.CurrentRoomBasicAttackDamagePercent &&
                   Approximately(item.Effects[0].Magnitude, 0.1f),
                "저놈 잡아라 must keep the +10% basic attack damage value.");
            Assert(ArtifactEffectDescription.Build(item) == "사용한 전투방에서 기본 공격 피해 +10% (방을 떠나면 해제)",
                "저놈 잡아라 must describe its current-room effect.");
            Assert(!new ItemEffectEntry(ItemEffectType.CurrentRoomBasicAttackDamagePercent).TryValidate(out _),
                "A current-room attack effect without a positive magnitude must be invalid.");

            ItemDefinition legacy = LoadItem(Week21Spell1Setup.LegacyCatchThatOnePath);
            Assert(legacy.ItemId == Week21Spell1Setup.LegacyCatchThatOneId && legacy.DisplayName == "저놈 잡아라" &&
                   legacy.Kind == ItemKind.Spell && legacy.Rarity == ItemRarity.Uncommon && legacy.IsActive &&
                   legacy.IsValid && !legacy.IsSingleUse && legacy.Effects.Count == 1 &&
                   legacy.Effects[0].EffectType == ItemEffectType.NextCombatRoomAttackDamagePercent &&
                   Approximately(legacy.Effects[0].Magnitude, 0.1f),
                "The legacy spell-catch-that-one contract must stay unchanged and active (Contract-0 §2.2).");
        }

        private static void ValidateRewardPool()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            IReadOnlyList<ItemDefinition> pool = assembler.SelectionRewardPool;
            Assert(pool.Count >= ArtifactRewardSelector.MaximumCandidateCount &&
                   pool.All(item => item != null && item.IsActive && item.IsValid),
                "The selection reward pool must keep enough active valid items.");
            Assert(pool.All(item => item.ItemId != Week21Spell1Setup.LegacyCatchThatOneId &&
                                    !LegacySpellRetirement.IsRetired(item)),
                "The legacy spell-catch-that-one must leave the selection reward pool and shop stock " +
                "(Contract-0 §2.2).");
            Assert(pool.All(item => !item.IsSingleUse),
                "Single-use spells must not join the selection reward pool (Contract-0 §3).");
            Assert(LegacySpellRetirement.IsRetired(LoadItem(Week21Spell1Setup.LegacyCatchThatOnePath)) &&
                   !LegacySpellRetirement.IsRetired(LoadItem(Week21Spell1Setup.CatchThatOnePath)),
                "Only the legacy spell may be marked retired.");
        }

        private static void ValidateRoomRules()
        {
            using TestPlayer test = new();
            ItemDefinition item = LoadItem(Week21Spell1Setup.CatchThatOnePath);
            PlayerSingleUseEffects effects = test.Effects;

            test.Progress.RecordRoomEntry(1, 1);
            test.Give(item, "spell1-1");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HeldDefinition == item &&
                   !effects.IsRoomAttackBoostActive,
                "The start room must refuse 저놈 잡아라 and keep it in the slot.");
            test.Progress.RecordRoomEntry(1, 4);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HasItem,
                "A treasure room must refuse 저놈 잡아라.");
            test.Progress.GetRoomState(FloorGenerator.BuildRoomId(1, 2)).MarkPreCleared();
            test.Progress.RecordRoomEntry(1, 2);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HasItem,
                "A cleared combat room must refuse 저놈 잡아라.");

            test.Progress.RecordRoomEntry(1, 3);
            Time.timeScale = 0f;
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Paused && test.Slot.HasItem &&
                   !effects.IsRoomAttackBoostActive,
                "저놈 잡아라 must not be used while paused.");
            Time.timeScale = 1f;

            string combatRoomId = FloorGenerator.BuildRoomId(1, 3);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem &&
                   effects.IsRoomAttackBoostActive && effects.RoomAttackBoostRoomId == combatRoomId &&
                   Approximately(effects.RoomAttackDamagePercent, 0.1f),
                "An uncleared combat room must consume 저놈 잡아라 and start the room bonus.");
            AssertMultipliers(test, 1.1f, "The used room must grant +10% basic attack damage only.");

            test.Give(item, "spell1-2");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used &&
                   Approximately(effects.RoomAttackDamagePercent, 0.2f),
                "A second use in the same room must add another +10%.");
            AssertMultipliers(test, 1.2f, "Two uses in one room must grant +20% basic attack damage.");

            test.Progress.GetRoomState(combatRoomId).MarkCleared();
            effects.RefreshRoomAttackBoost();
            Assert(effects.IsRoomAttackBoostActive, "Clearing the room must keep the bonus until the player leaves.");

            test.Progress.RecordRoomEntry(1, 1);
            Assert(!effects.IsRoomAttackBoostActive && effects.RoomAttackBoostRoomId == null,
                "Leaving the room must end the bonus.");
            AssertMultipliers(test, 1f, "Leaving the room must remove the basic attack bonus.");
            test.Progress.RecordRoomEntry(1, 3);
            Assert(!effects.IsRoomAttackBoostActive, "Revisiting the used room must not bring the bonus back.");
            AssertMultipliers(test, 1f, "A revisit must keep the base basic attack damage.");

            // The boss room is a combat room too, and moving to the next floor leaves it.
            test.Give(item, "spell1-3");
            test.Progress.RecordRoomEntry(1, 5);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && effects.IsRoomAttackBoostActive,
                "An uncleared boss room must accept 저놈 잡아라.");
            test.Progress.RecordRoomEntry(2, 5);
            Assert(!effects.IsRoomAttackBoostActive, "Moving to another floor must end the bonus.");

            Assert(test.Inventory.AcquiredItems.Count == 3 &&
                   test.Inventory.AcquiredItems.All(record => record.ItemId == item.ItemId),
                "Each 저놈 잡아라 instance must be recorded once.");
        }

        // The legacy spell drives its own room source; neither may overwrite the other.
        private static void ValidateLegacyCoexistence()
        {
            using TestPlayer test = new();
            ItemDefinition item = LoadItem(Week21Spell1Setup.CatchThatOnePath);
            ItemDefinition legacy = LoadItem(Week21Spell1Setup.LegacyCatchThatOnePath);

            test.Progress.RecordRoomEntry(1, 1);
            Assert(test.Inventory.TryAcquire(legacy), "The legacy spell must still be acquirable for old loadouts.");
            test.Progress.RecordRoomEntry(1, 3);
            AssertMultipliers(test, 1.1f, "The legacy spell must still apply in the next uncleared combat room.");
            test.Give(item, "spell1-legacy");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "The single-use spell must be usable alongside it.");
            AssertMultipliers(test, 1.2f, "Legacy and single-use room bonuses must add up.");
            test.Progress.RecordRoomEntry(1, 1);
            AssertMultipliers(test, 1f, "Leaving the room must end both room bonuses.");
        }

        private static void ValidateStopConditions()
        {
            ItemDefinition item = LoadItem(Week21Spell1Setup.CatchThatOnePath);
            using (TestPlayer test = new())
            {
                test.Progress.RecordRoomEntry(1, 3);
                test.Give(item, "spell1-stop");
                Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "Start a room bonus before the Run ends.");
                test.Progress.StopProgression();
                test.Effects.RefreshRoomAttackBoost();
                Assert(!test.Effects.IsRoomAttackBoostActive, "The end of the Run must end the bonus.");
                AssertMultipliers(test, 1f, "The end of the Run must remove the basic attack bonus.");
            }

            using (TestPlayer test = new())
            {
                test.Progress.RecordRoomEntry(1, 3);
                test.Give(item, "spell1-death");
                Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "Start a room bonus before death.");
                test.Health.TakeDamage(test.Health.MaxHealth * 10f);
                test.Effects.RefreshRoomAttackBoost();
                Assert(test.Health.IsDead && !test.Effects.IsRoomAttackBoostActive, "Death must end the bonus.");
            }
        }

        private static void AssertMultipliers(TestPlayer test, float basic, string message)
        {
            PlayerStats stats = test.Stats;
            GameObject source = stats.gameObject;
            Assert(Approximately(stats.CreateDirectDamageContext(source, DamageSourceType.PlayerAttack).Multiplier,
                       basic) &&
                   Approximately(stats.CreateDirectDamageContext(source, DamageSourceType.PlayerProjectile)
                       .Multiplier, basic) &&
                   Approximately(stats.CreateDirectDamageContext(source, DamageSourceType.PlayerSkillExplosion)
                       .Multiplier, 1f) &&
                   Approximately(stats.AttackDamage, 10f),
                message);
        }

        private static ItemDefinition LoadItem(string path)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            Assert(item != null, $"Run Spell-1 setup first: {path} is missing.");
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
        // boss on floor 1, and one combat room on floor 2.
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
                progressObject = new GameObject("Spell-1 Progress", typeof(RunProgress));
                floor = new GameObject("Spell-1 Floor");
                Progress = progressObject.GetComponent<RunProgress>();
                ConfigureTestGraph(Progress);
                player = new GameObject("Spell-1 Player", typeof(Health), typeof(PlayerSP), typeof(PlayerStats),
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
                    Node(2, 5, GeneratedRoomRole.Intermediate),
                };
                GeneratedFloor first = new(1, 0, 0, 0, firstNodes[0].RoomId, firstNodes[4].RoomId, firstNodes);
                GeneratedFloor second = new(2, 0, 0, 0, secondNodes[0].RoomId, null, secondNodes);
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
