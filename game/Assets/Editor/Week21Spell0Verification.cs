using System;
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
    // Spell-0: 아로마 테라피 fills SP to the maximum plus one overcharge slot (refused, unconsumed, when already
    // overcharged), and 명상의 시간 adds half an SP slot every second for 12 seconds. Verifies the item contract,
    // the SP half/overcharge rules, the timer boundaries (first tick, cap, pause, room change, refresh, death,
    // Run end) and the HUD display.
    public static class Week21Spell0Verification
    {
        public static void SetupAndVerifyBatch()
        {
            Week21Spell0Setup.Setup();
            string[] paths = { Week21Spell0Setup.AromaTherapyPath, Week21Spell0Setup.MeditationTimePath };
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week21Spell0Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID)),
                "Spell-0 setup changed an item asset GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 21/Verify Spell-0 SP Spells")]
        public static void Verify()
        {
            ValidateContract();
            ValidateSPRules();
            ValidateAromaTherapy();
            ValidateMeditationTime();
            ValidateHud();
            Debug.Log("Spell-0 verification passed: 아로마 테라피 fills SP to max + 1 and is refused unconsumed while " +
                      "overcharged; 명상의 시간 adds half an SP slot every second for 12 ticks, capped at the maximum, " +
                      "paused with the game, kept across rooms, refreshed on reuse, stopped by death or Run end; " +
                      "the HUD shows half and overcharge slots.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.RestoreAllSPWithOvercharge == 28 &&
                   (int)ItemEffectType.RegenerateSPHalvesOverTime == 29,
                "Spell-0 effect types are serialized by number and must be 28 and 29.");

            ItemDefinition aroma = LoadItem(Week21Spell0Setup.AromaTherapyPath);
            ItemDefinition meditation = LoadItem(Week21Spell0Setup.MeditationTimePath);
            AssertItem(aroma, Week21Spell0Setup.AromaTherapyId, "아로마 테라피");
            AssertItem(meditation, Week21Spell0Setup.MeditationTimeId, "명상의 시간");
            Assert(aroma.Effects.Count == 1 &&
                   aroma.Effects[0].EffectType == ItemEffectType.RestoreAllSPWithOvercharge &&
                   aroma.Effects[0].IntegerAmount == 1,
                "아로마 테라피 must fill SP with one overcharge slot.");
            Assert(meditation.Effects.Count == 1 &&
                   meditation.Effects[0].EffectType == ItemEffectType.RegenerateSPHalvesOverTime &&
                   meditation.Effects[0].IntegerAmount == 1 &&
                   Mathf.Approximately(meditation.Effects[0].IntervalSeconds, 1f) &&
                   Mathf.Approximately(meditation.Effects[0].DurationSeconds, 12f),
                "명상의 시간 must add half an SP slot every second for 12 seconds.");
            Assert(!new ItemEffectEntry(ItemEffectType.RegenerateSPHalvesOverTime, configuredIntegerAmount: 1,
                    configuredIntervalSeconds: 2f, configuredDurationSeconds: 1f).TryValidate(out _),
                "A regeneration shorter than one tick must be invalid.");

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            Assert(assembler.SelectionRewardPool.All(item => item != null && !item.IsSingleUse),
                "Single-use spells must not join the selection reward pool (Contract-0 §3).");
        }

        private static void ValidateSPRules()
        {
            GameObject owner = new("Spell-0 SP", typeof(PlayerSP));
            try
            {
                PlayerSP sp = owner.GetComponent<PlayerSP>();
                InvokeLifecycle(sp, "Awake");
                Assert(sp.CurrentSP == 0 && sp.MaxSP == 3 && !sp.HasHalfSP, "Verification SP must start at 0/3.");
                Assert(sp.TryAddHalf() && sp.CurrentSP == 0 && sp.HasHalfSP, "The first half must not fill a slot.");
                Assert(sp.TryAddHalf() && sp.CurrentSP == 1 && !sp.HasHalfSP, "Two halves must fill one slot.");
                Assert(sp.TryAddHalf() && sp.TrySpend() && sp.CurrentSP == 0 && sp.HasHalfSP,
                    "Spending whole SP must keep the half slot.");
                Assert(sp.TryAdd(3) && sp.CurrentSP == 3 && !sp.HasHalfSP,
                    "Reaching the maximum must clear the half slot.");
                Assert(!sp.TryAddHalf() && !sp.TryAdd() && sp.CurrentSP == 3,
                    "Ordinary gains must not exceed the maximum.");
                Assert(sp.TryFillWithOvercharge(1) && sp.CurrentSP == 4 && sp.IsOvercharged,
                    "Overcharge must fill to the maximum plus one.");
                Assert(!sp.TryFillWithOvercharge(1) && sp.CurrentSP == 4,
                    "Overcharge must not stack once already overcharged.");
                Assert(sp.TrySpend() && sp.CurrentSP == 3 && !sp.IsOvercharged,
                    "Spending must remove the overcharge slot first.");
                Assert(sp.TrySpend() && sp.CurrentSP == 2 && sp.TryFillWithOvercharge(1) && sp.CurrentSP == 4,
                    "Overcharge after spending must refill to the maximum plus one.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static void ValidateAromaTherapy()
        {
            using TestPlayer test = new();
            ItemDefinition aroma = LoadItem(Week21Spell0Setup.AromaTherapyPath);
            PlayerSP sp = test.SP;

            Assert(sp.TryAdd() && sp.TryAddHalf() && sp.CurrentSP == 1 && sp.HasHalfSP, "Setup SP 1.5/3.");
            test.Give(aroma, "spell0-aroma-1");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem &&
                   sp.CurrentSP == 4 && !sp.HasHalfSP && sp.IsOvercharged,
                "아로마 테라피 must fill SP to 4/3, clear the half slot and be consumed.");

            test.Give(aroma, "spell0-aroma-2");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HeldDefinition == aroma &&
                   sp.CurrentSP == 4,
                "아로마 테라피 must be refused and kept while already overcharged.");
            Assert(sp.TrySpend() && sp.CurrentSP == 3, "Spend back to the maximum.");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem && sp.CurrentSP == 4,
                "아로마 테라피 must be usable at exactly the maximum, overcharging to 4/3.");

            test.Give(aroma, "spell0-aroma-3");
            Time.timeScale = 0f;
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Paused && test.Slot.HasItem,
                "아로마 테라피 must not be used while paused.");
            Time.timeScale = 1f;
            Assert(test.Inventory.AcquiredItems.Count == 3 &&
                   test.Inventory.AcquiredItems.All(item => item.ItemId == aroma.ItemId),
                "Each 아로마 테라피 instance must be recorded once.");
        }

        private static void ValidateMeditationTime()
        {
            using TestPlayer test = new();
            ItemDefinition meditation = LoadItem(Week21Spell0Setup.MeditationTimePath);
            PlayerSP sp = test.SP;
            PlayerSingleUseEffects effects = test.Effects;

            // Usable at full SP: the timer refills SP spent while it runs.
            Assert(sp.TryAdd(3), "Setup full SP.");
            test.Give(meditation, "spell0-meditation-1");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && effects.IsRegenerating &&
                   effects.RegenerationTotalTicks == 12 && Mathf.Approximately(effects.RegenerationRemainingSeconds, 12f),
                "명상의 시간 must be usable at full SP and start 12 ticks.");
            Assert(effects.AdvanceRegeneration(1f) == 1 && sp.CurrentSP == 3 && !sp.HasHalfSP,
                "A tick at full SP must be wasted without exceeding the maximum.");
            Assert(sp.TrySpend(3) && sp.CurrentSP == 0, "Spend all SP.");

            Assert(effects.AdvanceRegeneration(0.99f) == 0 && !sp.HasHalfSP,
                "No tick may arrive before the next whole second.");
            Assert(effects.AdvanceRegeneration(0.01f) == 1 && sp.CurrentSP == 0 && sp.HasHalfSP,
                "The second tick must add half a slot.");

            Time.timeScale = 0f;
            Assert(effects.AdvanceRegeneration(5f) == 0 && sp.HasHalfSP && sp.CurrentSP == 0,
                "Paused game time must not advance the timer.");
            Time.timeScale = 1f;

            // Room and floor changes keep it running.
            test.Progress.RecordRoomEntry(2, 3);
            Assert(effects.IsRegenerating && effects.AdvanceRegeneration(1f) == 1 && sp.CurrentSP == 1 &&
                   !sp.HasHalfSP,
                "Moving to another room or floor must keep the timer running.");

            // Reuse while active restarts the full duration without stacking.
            test.Give(meditation, "spell0-meditation-2");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && effects.RegenerationDeliveredTicks == 0 &&
                   Mathf.Approximately(effects.RegenerationRemainingSeconds, 12f),
                "Reuse must restart the 12 second duration.");
            Assert(effects.AdvanceRegeneration(3f) == 3 && sp.CurrentSP == 2 && sp.HasHalfSP,
                "Three ticks after 1/3 must reach 2.5/3.");
            Assert(effects.AdvanceRegeneration(3f) == 3 && sp.CurrentSP == 3 && !sp.HasHalfSP,
                "Regeneration must stop at the maximum and clear the half slot.");
            Assert(effects.AdvanceRegeneration(100f) == 6 && !effects.IsRegenerating &&
                   effects.RegenerationDeliveredTicks == 12 && sp.CurrentSP == 3,
                "Exactly 12 ticks are delivered, never more.");
            Assert(effects.AdvanceRegeneration(1f) == 0, "An ended timer must not tick.");

            // Run end and death stop it.
            Assert(sp.TrySpend(3), "Spend all SP.");
            test.Give(meditation, "spell0-meditation-3");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "Start a third regeneration.");
            test.Progress.StopProgression();
            Assert(effects.AdvanceRegeneration(5f) == 0 && !effects.IsRegenerating && sp.CurrentSP == 0 &&
                   !sp.HasHalfSP,
                "The end of the Run must stop the timer.");
            test.Progress.ResetProgress();
            test.Give(meditation, "spell0-meditation-4");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "Start a fourth regeneration.");
            test.Health.TakeDamage(test.Health.MaxHealth * 10f);
            Assert(test.Health.IsDead && effects.AdvanceRegeneration(5f) == 0 && !effects.IsRegenerating &&
                   sp.CurrentSP == 0,
                "Death must stop the timer.");
        }

        private static void ValidateHud()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameHudView source = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameHudView>(true)).Single();
            GameObject player = new("Spell-0 HUD Player", typeof(Health), typeof(PlayerSP));
            GameObject hudClone = Object.Instantiate(source.gameObject);
            try
            {
                Health health = player.GetComponent<Health>();
                PlayerSP sp = player.GetComponent<PlayerSP>();
                InvokeLifecycle(health, "Awake");
                InvokeLifecycle(sp, "Awake");
                GameHudView view = hudClone.GetComponent<GameHudView>();
                view.Configure(health, sp, null, view.HpFill, view.HpValueText, view.HealthFeedback,
                    view.SpSlotsRoot, view.SpSlotTemplate, view.SpValueText, view.LowerGradeSkillState,
                    view.LowerGradeSkillText);
                view.RefreshNow();

                Assert(sp.TryAdd() && sp.TryAddHalf() && view.SlotCount == 3 && view.ActiveSlotCount == 1 &&
                       view.IsShowingHalfSlot && view.OverchargeSlotCount == 0 && view.SpValueText.text == "SP 1.5 / 3",
                    "The HUD must show a half slot as SP 1.5 / 3.");
                Assert(sp.TryFillWithOvercharge(1) && view.SlotCount == 4 && view.ActiveSlotCount == 4 &&
                       !view.IsShowingHalfSlot && view.OverchargeSlotCount == 1 && view.SpValueText.text == "SP 4 / 3",
                    "The HUD must add an overcharge slot and show SP 4 / 3.");
                Assert(sp.TrySpend() && view.SlotCount == 3 && view.ActiveSlotCount == 3 &&
                       view.OverchargeSlotCount == 0 && view.SpValueText.text == "SP 3 / 3",
                    "Spending the overcharge must remove its slot.");
            }
            finally
            {
                Object.DestroyImmediate(hudClone);
                Object.DestroyImmediate(player);
            }
        }

        private static ItemDefinition LoadItem(string path)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            Assert(item != null, $"Run Spell-0 setup first: {path} is missing.");
            return item;
        }

        private static void AssertItem(ItemDefinition item, string itemId, string displayName)
        {
            Assert(item.ItemId == itemId && item.DisplayName == displayName && item.Kind == ItemKind.SingleUseSpell &&
                   item.Rarity == ItemRarity.Uncommon && item.IsActive && item.MaxStacks == 1 && item.IsValid,
                $"{itemId} must be a valid active Uncommon single-use spell named {displayName}.");
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

        // A player with the real slot and executor, plus a Run progress, torn down with the pickups it spawned.
        private sealed class TestPlayer : IDisposable
        {
            private readonly GameObject progressObject;
            private readonly GameObject floor;
            private readonly GameObject player;
            private readonly SingleUseItemPickup prefab;
            private readonly float previousTimeScale;

            public RunProgress Progress { get; }
            public Health Health { get; }
            public PlayerSP SP { get; }
            public PlayerInventory Inventory { get; }
            public PlayerSpellSlot Slot { get; }
            public PlayerSingleUseEffects Effects { get; }

            public TestPlayer()
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;
                prefab = AssetDatabase.LoadAssetAtPath<SingleUseItemPickup>(Week21Slot0Setup.PickupPrefabPath);
                Assert(prefab != null, "Run Slot-0 setup first: the single-use pickup Prefab is missing.");
                progressObject = new GameObject("Spell-0 Progress", typeof(RunProgress));
                floor = new GameObject("Spell-0 Floor");
                player = new GameObject("Spell-0 Player", typeof(Health), typeof(PlayerSP), typeof(PlayerStats),
                    typeof(PlayerInventory), typeof(PlayerSpellSlot));
                Progress = progressObject.GetComponent<RunProgress>();
                Health = player.GetComponent<Health>();
                SP = player.GetComponent<PlayerSP>();
                Inventory = player.GetComponent<PlayerInventory>();
                Slot = player.GetComponent<PlayerSpellSlot>();
                Effects = player.GetComponent<PlayerSingleUseEffects>();
                InvokeLifecycle(Health, "Awake");
                InvokeLifecycle(SP, "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
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
        }
    }
}
