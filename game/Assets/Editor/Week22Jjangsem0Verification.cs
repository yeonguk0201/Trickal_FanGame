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
    // Jjangsem-0: 빅우드의 열매 is the first jjangsem spell. For 10 seconds every hit is half a heart weaker, and each
    // hit that still costs HP is healed 2 seconds later by that damage plus one heart. Shield-absorbed damage is not
    // healed, death is not prevented and drops the pending heals, hits after the window are not healed, and reuse
    // restarts the window. It shares the slot with spells and only diamond chests hold it.
    public static class Week22Jjangsem0Verification
    {
        public static void SetupAndVerifyBatch()
        {
            Week22Jjangsem0Setup.Setup();
            string itemGuid = AssetDatabase.AssetPathToGUID(Week22Jjangsem0Setup.BigwoodFruitPath);
            string tableGuid = AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath);
            Week22Jjangsem0Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(itemGuid) &&
                   itemGuid == AssetDatabase.AssetPathToGUID(Week22Jjangsem0Setup.BigwoodFruitPath) &&
                   tableGuid == AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath),
                "Jjangsem-0 setup changed the item or chest content table GUID.");
            Verify();
        }

        // Also re-runs the shared slot, the single-use spells that use the same executor, the chest pools that now
        // hold a jjangsem spell and the development panel.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week21Slot0Verification.Verify();
            Week21Spell0Verification.Verify();
            Week21Spell1Verification.Verify();
            Week21Spell2Verification.Verify();
            Week21Spell3Verification.Verify();
            Week22Chest2Verification.Verify();
            Week22Chest1Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Jjangsem-0 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Jjangsem-0 Bigwood Fruit")]
        public static void Verify()
        {
            ValidateContract();
            ValidateAcquisitionPath();
            // The runtime checks spawn pickups; keep them out of the Game Scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ValidateHealthReduction();
            ValidateRecovery();
            ValidateWindowAndStops();
            ValidateSharedSlot();
            Debug.Log("Jjangsem-0 verification passed: 빅우드의 열매 (Rare jjangsem spell, effect 36) makes every hit " +
                      "half a heart weaker for 10 seconds and heals each HP hit 2 seconds later by that damage plus " +
                      "one heart, ignores shield-absorbed damage, does not prevent death, stops healing new hits " +
                      "after the window, restarts on reuse, shares the slot with spells and drops only from diamond " +
                      "chests.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.ReduceAndRecoverDamageTaken == 36 && (int)ItemEffectType.Flight == 35,
                "Jjangsem-0 must add effect type 36 after the existing effects without renumbering them.");
            Assert(!new ItemEffectEntry(ItemEffectType.ReduceAndRecoverDamageTaken, configuredMagnitude: 1f,
                       configuredIntervalSeconds: 2f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.ReduceAndRecoverDamageTaken, configuredMagnitude: 1f,
                       configuredDurationSeconds: 10f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.ReduceAndRecoverDamageTaken, configuredIntervalSeconds: 2f,
                       configuredDurationSeconds: 10f).TryValidate(out _),
                "The effect must need a damage reduction, a recovery delay and a duration.");

            ItemDefinition fruit = LoadFruit();
            Assert(fruit.ItemId == "jjangsem-bigwood-fruit" && fruit.DisplayName == "빅우드의 열매" &&
                   fruit.Kind == ItemKind.JjangsemSpell && fruit.Rarity == ItemRarity.Rare && fruit.IsActive &&
                   fruit.MaxStacks == 1 && fruit.IsValid && fruit.IsSingleUse,
                "jjangsem-bigwood-fruit must be a valid active Rare jjangsem spell named 빅우드의 열매.");
            Assert(ItemDefinition.IsItemIdValidForKind(fruit.ItemId, ItemKind.JjangsemSpell) &&
                   !ItemDefinition.IsItemIdValidForKind(fruit.ItemId, ItemKind.SingleUseSpell) &&
                   !ItemDefinition.IsItemIdValidForKind(fruit.ItemId, ItemKind.Artifact),
                "The fruit ID must be a jjangsem spell ID only (Contract-0 §2.1).");
            Assert(fruit.Effects.Count == 1, "The fruit must carry exactly one effect.");
            ItemEffectEntry effect = fruit.Effects[0];
            Assert(effect.EffectType == ItemEffectType.ReduceAndRecoverDamageTaken &&
                   Mathf.Approximately(effect.Magnitude, 1f) && effect.IntegerAmount == 2 &&
                   Mathf.Approximately(effect.IntervalSeconds, 2f) && Mathf.Approximately(effect.DurationSeconds, 10f),
                "The fruit must reduce hits by half a heart for 10 seconds and heal the damage plus one heart after " +
                "2 seconds (D10).");

            string description = ArtifactEffectDescription.Build(fruit);
            Assert(description == "10초간 받는 피해 -반 칸, HP 피해를 받으면 2초 뒤 그 피해 +1칸 회복",
                $"Unexpected fruit description: {description}");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null && font.HasCharacters(fruit.DisplayName + description),
                "The Frontend font is missing a fruit glyph.");
        }

        // Contract-0 §3: a jjangsem spell comes only from diamond chests.
        private static void ValidateAcquisitionPath()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ItemDefinition fruit = LoadFruit();
            ChestContentTable table =
                AssetDatabase.LoadAssetAtPath<ChestContentTable>(Week22Chest1Setup.ChestContentTablePath);
            Assert(table != null && table.TryValidate(out string error), "The chest content table is missing or invalid.");
            Assert(table.JjangsemSpells.Contains(fruit) && !table.Spells.Contains(fruit) &&
                   !table.GoldenExclusiveArtifacts.Contains(fruit),
                "The fruit must be in the chest table's jjangsem spell list only.");
            Assert(table.FindRule(ChestKind.Diamond).JjangsemShare > 0f &&
                   Mathf.Approximately(table.FindRule(ChestKind.Normal).JjangsemShare, 0f) &&
                   Mathf.Approximately(table.FindRule(ChestKind.Golden).JjangsemShare, 0f),
                "Only diamond chests may hold a jjangsem spell.");

            bool diamondHoldsFruit = false;
            for (int index = 0; index < 4096 && !diamondHoldsFruit; index++)
                diamondHoldsFruit = table.RollContents(RoomClearRewardSpawner.DeriveChestSeed(index * 104729 + 3),
                    ChestKind.Diamond).Spell == fruit;
            Assert(diamondHoldsFruit, "A diamond chest must be able to hold the fruit.");

            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            Assert(assembler.ChestContentTable == table && !assembler.SelectionRewardPool.Contains(fruit) &&
                   assembler.SelectionRewardPool.All(item => item != null && !item.IsSingleUse),
                "The fruit must not join the selection reward pool or the shop stock that shares it.");
        }

        // The flat reduction comes off before the shield and never applies once it is cleared.
        private static void ValidateHealthReduction()
        {
            GameObject owner = new("Jjangsem-0 Health", typeof(Health));
            try
            {
                Health health = owner.GetComponent<Health>();
                InvokeLifecycle(health, "Awake");
                health.EnableHealthUnits();
                float max = health.MaxHealth;
                health.SetIncomingDamageReduction(1f);
                health.TakeDamage(2f);
                Assert(Mathf.Approximately(health.CurrentHealth, max - 1f),
                    "A one heart hit must cost half a heart under the reduction.");
                health.TakeDamage(3f);
                Assert(Mathf.Approximately(health.CurrentHealth, max - 3f),
                    "A one and a half heart hit must cost one heart under the reduction.");
                Assert(health.AddShield(1f), "Setup shield.");
                health.TakeDamage(2f);
                Assert(Mathf.Approximately(health.CurrentHealth, max - 3f) && Mathf.Approximately(health.CurrentShield, 0f),
                    "The reduction must apply before the shield absorbs the rest.");
                health.SetIncomingDamageReduction(0f);
                health.TakeDamage(2f);
                Assert(Mathf.Approximately(health.CurrentHealth, max - 5f), "A cleared reduction must not apply.");
                health.SetIncomingDamageReduction(1f);
                health.ResetHealth();
                Assert(Mathf.Approximately(health.IncomingDamageReduction, 0f),
                    "Resetting health must clear the reduction.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static void ValidateRecovery()
        {
            using TestPlayer test = new();
            ItemDefinition fruit = LoadFruit();
            Health health = test.Health;
            PlayerSingleUseEffects effects = test.Effects;
            float max = health.MaxHealth;

            health.TakeDamage(4f);
            Assert(Mathf.Approximately(health.CurrentHealth, max - 4f), "Setup: two hearts missing.");
            test.Give(fruit, "jjangsem0-fruit-1");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem &&
                   effects.IsDamageRecoveryActive && Mathf.Approximately(effects.DamageRecoveryRemainingSeconds, 10f) &&
                   Mathf.Approximately(health.IncomingDamageReduction, 1f) && effects.PendingRecoveryCount == 0 &&
                   Mathf.Approximately(health.CurrentHealth, max - 4f),
                "Using the fruit must consume it and start a 10 second window with half a heart of reduction.");

            // A one heart hit costs half a heart and comes back as one and a half hearts 2 seconds later.
            health.TakeDamage(2f);
            Assert(Mathf.Approximately(health.CurrentHealth, max - 5f) && effects.PendingRecoveryCount == 1,
                "A hit during the window must be reduced by half a heart and queue one heal.");
            Assert(Mathf.Approximately(effects.AdvanceDamageRecovery(1.99f), 0f) &&
                   Mathf.Approximately(health.CurrentHealth, max - 5f),
                "The heal must not arrive before its 2 second delay.");

            Time.timeScale = 0f;
            Assert(Mathf.Approximately(effects.AdvanceDamageRecovery(5f), 0f) && effects.PendingRecoveryCount == 1 &&
                   Mathf.Approximately(effects.DamageRecoveryRemainingSeconds, 8.01f),
                "Paused game time must not advance the window or the heal.");
            Time.timeScale = 1f;

            Assert(Mathf.Approximately(effects.AdvanceDamageRecovery(0.01f), 3f) &&
                   Mathf.Approximately(health.CurrentHealth, max - 2f) && effects.PendingRecoveryCount == 0,
                "After 2 seconds the hit must be healed by its HP damage plus one heart.");

            // Room and floor changes keep the window running.
            test.Progress.RecordRoomEntry(2, 3);
            Assert(effects.IsDamageRecoveryActive && Mathf.Approximately(health.IncomingDamageReduction, 1f),
                "Moving to another room or floor must keep the effect.");

            // The shield takes reduced damage first; only HP actually lost is healed.
            Assert(health.AddShield(1f), "Setup shield.");
            health.TakeDamage(2f);
            Assert(Mathf.Approximately(health.CurrentHealth, max - 2f) && Mathf.Approximately(health.CurrentShield, 0f) &&
                   effects.PendingRecoveryCount == 0,
                "A hit the shield absorbs whole must queue no heal.");
            Assert(health.AddShield(1f), "Setup shield.");
            health.TakeDamage(3f);
            Assert(Mathf.Approximately(health.CurrentHealth, max - 3f) && effects.PendingRecoveryCount == 1,
                "A hit that breaks the shield must queue a heal for the HP part only.");

            // Two hits heal separately, each after its own delay, and never above the maximum.
            Assert(Mathf.Approximately(effects.AdvanceDamageRecovery(1f), 0f), "One second in: no heal yet.");
            health.TakeDamage(2f);
            Assert(Mathf.Approximately(health.CurrentHealth, max - 4f) && effects.PendingRecoveryCount == 2,
                "A second hit must queue its own heal.");
            Assert(Mathf.Approximately(effects.AdvanceDamageRecovery(1f), 3f) &&
                   Mathf.Approximately(health.CurrentHealth, max - 1f) && effects.PendingRecoveryCount == 1,
                "The first heal (half a heart of HP damage plus one heart) must arrive alone.");
            Assert(Mathf.Approximately(effects.AdvanceDamageRecovery(1f), 1f) &&
                   Mathf.Approximately(health.CurrentHealth, max) && effects.PendingRecoveryCount == 0,
                "The second heal must stop at the maximum HP.");
        }

        private static void ValidateWindowAndStops()
        {
            ItemDefinition fruit = LoadFruit();

            // Hits after the window are not reduced or healed; a heal queued before it ends still arrives.
            using (TestPlayer test = new())
            {
                Health health = test.Health;
                PlayerSingleUseEffects effects = test.Effects;
                float max = health.MaxHealth;
                health.TakeDamage(4f);
                test.Give(fruit, "jjangsem0-window-1");
                Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "Start the window.");
                effects.AdvanceDamageRecovery(9f);
                health.TakeDamage(2f);
                Assert(effects.IsDamageRecoveryActive && effects.PendingRecoveryCount == 1 &&
                       Mathf.Approximately(health.CurrentHealth, max - 5f), "A hit at 9 seconds is still covered.");
                Assert(Mathf.Approximately(effects.AdvanceDamageRecovery(1f), 0f) && !effects.IsDamageRecoveryActive &&
                       Mathf.Approximately(health.IncomingDamageReduction, 0f) && effects.PendingRecoveryCount == 1,
                    "The window must end at 10 seconds, clear the reduction and keep the pending heal.");
                health.TakeDamage(2f);
                Assert(Mathf.Approximately(health.CurrentHealth, max - 7f) && effects.PendingRecoveryCount == 1,
                    "A hit after the window must take full damage and queue no heal.");
                Assert(Mathf.Approximately(effects.AdvanceDamageRecovery(1f), 3f) &&
                       Mathf.Approximately(health.CurrentHealth, max - 4f) && effects.PendingRecoveryCount == 0,
                    "The heal queued before the window ended must still arrive.");
                Assert(Mathf.Approximately(effects.AdvanceDamageRecovery(10f), 0f), "An ended effect must not heal.");

                // Reuse restarts the full window without stacking the reduction; pending heals stay.
                test.Give(fruit, "jjangsem0-window-2");
                Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "Start a second window.");
                effects.AdvanceDamageRecovery(6f);
                health.TakeDamage(2f);
                test.Give(fruit, "jjangsem0-window-3");
                Assert(test.Slot.TryUse() == SpellSlotUseResult.Used &&
                       Mathf.Approximately(effects.DamageRecoveryRemainingSeconds, 10f) &&
                       Mathf.Approximately(health.IncomingDamageReduction, 1f) && effects.PendingRecoveryCount == 1,
                    "Reuse must restart the 10 second window, keep one reduction and keep pending heals.");

                // The end of the Run drops the window and the pending heals.
                float before = health.CurrentHealth;
                test.Progress.StopProgression();
                Assert(Mathf.Approximately(effects.AdvanceDamageRecovery(5f), 0f) && !effects.IsDamageRecoveryActive &&
                       effects.PendingRecoveryCount == 0 && Mathf.Approximately(health.IncomingDamageReduction, 0f) &&
                       Mathf.Approximately(health.CurrentHealth, before),
                    "The end of the Run must stop the effect without healing.");
            }

            // Death is not prevented and drops everything.
            using (TestPlayer test = new())
            {
                Health health = test.Health;
                PlayerSingleUseEffects effects = test.Effects;
                test.Give(fruit, "jjangsem0-death-1");
                Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "Start the window.");
                health.TakeDamage(2f);
                Assert(effects.PendingRecoveryCount == 1, "Queue a heal before the lethal hit.");
                health.TakeDamage(health.MaxHealth * 10f);
                Assert(health.IsDead && effects.PendingRecoveryCount == 1,
                    "A lethal hit during the window must kill the player and queue no heal.");
                Assert(Mathf.Approximately(effects.AdvanceDamageRecovery(5f), 0f) && !effects.IsDamageRecoveryActive &&
                       effects.PendingRecoveryCount == 0 && health.IsDead && Mathf.Approximately(health.CurrentHealth, 0f),
                    "Death must end the effect and drop the pending heals.");
            }
        }

        // The fruit shares the single slot with spells: collecting one ejects the other, and each instance is recorded
        // and consumed once.
        private static void ValidateSharedSlot()
        {
            using TestPlayer test = new();
            ItemDefinition fruit = LoadFruit();
            ItemDefinition meditation = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week21Spell0Setup.MeditationTimePath);
            Assert(meditation != null, "Run Spell-0 setup first: 명상의 시간 is missing.");

            test.Give(meditation, "jjangsem0-slot-spell");
            test.Give(fruit, "jjangsem0-slot-fruit");
            Assert(test.Slot.HeldDefinition == fruit && test.Slot.HeldDefinition.Kind == ItemKind.JjangsemSpell,
                "Collecting the fruit over a spell must swap it into the shared slot.");
            Time.timeScale = 0f;
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Paused && test.Slot.HasItem &&
                   !test.Effects.IsDamageRecoveryActive, "The fruit must not be used while paused.");
            Time.timeScale = 1f;
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem &&
                   test.Effects.IsDamageRecoveryActive && test.Slot.TryUse() == SpellSlotUseResult.Empty,
                "Using the fruit must consume it once.");
            test.Give(meditation, "jjangsem0-slot-spell-2");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && test.Effects.IsRegenerating &&
                   test.Effects.IsDamageRecoveryActive, "A spell used afterwards must run beside the fruit effect.");
            Assert(test.Inventory.AcquiredItems.Count(item => item.ItemId == fruit.ItemId) == 1 &&
                   test.Inventory.AcquiredItems.Count(item => item.ItemId == meditation.ItemId) == 2,
                "Each single-use instance must be recorded once for the Run result.");
        }

        private static ItemDefinition LoadFruit()
        {
            ItemDefinition fruit = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week22Jjangsem0Setup.BigwoodFruitPath);
            Assert(fruit != null, $"Run Jjangsem-0 setup first: {Week22Jjangsem0Setup.BigwoodFruitPath} is missing.");
            return fruit;
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

        // A player with the real slot and executor and half-heart health units, torn down with the pickups it spawned.
        private sealed class TestPlayer : IDisposable
        {
            private readonly GameObject progressObject;
            private readonly GameObject floor;
            private readonly GameObject player;
            private readonly SingleUseItemPickup prefab;
            private readonly float previousTimeScale;

            public RunProgress Progress { get; }
            public Health Health { get; }
            public PlayerInventory Inventory { get; }
            public PlayerSpellSlot Slot { get; }
            public PlayerSingleUseEffects Effects { get; }

            public TestPlayer()
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;
                prefab = AssetDatabase.LoadAssetAtPath<SingleUseItemPickup>(Week21Slot0Setup.PickupPrefabPath);
                Assert(prefab != null, "Run Slot-0 setup first: the single-use pickup Prefab is missing.");
                progressObject = new GameObject("Jjangsem-0 Progress", typeof(RunProgress));
                floor = new GameObject("Jjangsem-0 Floor");
                player = new GameObject("Jjangsem-0 Player", typeof(Health), typeof(PlayerSP), typeof(PlayerStats),
                    typeof(PlayerInventory), typeof(PlayerSpellSlot));
                Progress = progressObject.GetComponent<RunProgress>();
                Health = player.GetComponent<Health>();
                Inventory = player.GetComponent<PlayerInventory>();
                Slot = player.GetComponent<PlayerSpellSlot>();
                Effects = player.GetComponent<PlayerSingleUseEffects>();
                InvokeLifecycle(Health, "Awake");
                Health.EnableHealthUnits();
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
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
