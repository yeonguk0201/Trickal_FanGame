using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week16Spell1Verification
    {
        [MenuItem("Trickal Fan Game/Week 16/Verify Spell-1 Effects")]
        public static void Verify()
        {
            ValidateSpellEffectsAndRoomBoundaries();
            ValidateFreshRunState();
            Debug.Log("Week 16 Spell-1 verification passed: all three spells use the common acquisition path, " +
                      "afterimage stacks permanently within one Run, catch-that-one ignores safe and cleared " +
                      "rooms before applying to exactly one uncleared combat room, final-sprint applies only " +
                      "inside an uncleared boss room, and a fresh Run starts without spell state.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week16Content0Setup.Setup();
            Week16Content0Verification.Verify();
            Verify();
            PhaseGSimpleEffectsVerification.Verify();
            PhaseGConditionalEffectsVerification.Verify();
            Hp4LifeGemVerification.Verify();
        }

        private static void ValidateSpellEffectsAndRoomBoundaries()
        {
            GameObject progressObject = new("Spell-1 Progress", typeof(RunProgress));
            RunProgress progress = progressObject.GetComponent<RunProgress>();
            ConfigureTestGraph(progress);
            GameObject player = CreatePlayer(progress, out PlayerStats stats, out PlayerInventory inventory);
            ItemDefinition catchThatOne = LoadSpell("spell-catch-that-one");
            ItemDefinition finalSprint = LoadSpell("spell-final-sprint");
            ItemDefinition afterimage = LoadSpell("spell-afterimage");

            try
            {
                progress.RecordRoomEntry(1, 1);
                Assert(inventory.TryAcquire(catchThatOne) && inventory.TryAcquire(finalSprint),
                    "Conditional spells must be acquirable through PlayerInventory.");
                Assert(inventory.TryAcquire(afterimage) && inventory.TryAcquire(afterimage) &&
                       inventory.TryAcquire(afterimage) && !inventory.TryAcquire(afterimage),
                    "Afterimage must stack exactly three times through the common acquisition path.");

                PlayerSpellEffects effects = player.GetComponent<PlayerSpellEffects>();
                Assert(effects != null &&
                       Approximately(stats.MoveSpeed, 6.5f) && Approximately(stats.AttackSpeed, 1.15f) &&
                       Approximately(stats.AttackDamage, 10f) &&
                       Approximately(effects.PendingNextCombatRoomAttackDamagePercent, 0.1f),
                    "Afterimage must grant +10% move speed and +5% attack speed per stack without " +
                    "consuming the next-combat-room spell in the start room.");

                progress.RecordRoomEntry(1, 4);
                Assert(Approximately(effects.PendingNextCombatRoomAttackDamagePercent, 0.1f) &&
                       string.IsNullOrEmpty(effects.ActiveNextCombatRoomId) &&
                       !effects.IsBossRoomEffectActive,
                    "Treasure rooms must not consume catch-that-one or activate final-sprint.");

                RoomRunState clearedCombat = progress.GetRoomState(FloorGenerator.BuildRoomId(1, 2));
                clearedCombat.MarkPreCleared();
                progress.RecordRoomEntry(1, 2);
                Assert(Approximately(effects.PendingNextCombatRoomAttackDamagePercent, 0.1f) &&
                       Approximately(stats.AttackDamage, 10f),
                    "Revisiting a cleared combat room must not consume catch-that-one.");

                progress.RecordRoomEntry(1, 3);
                Assert(Approximately(effects.PendingNextCombatRoomAttackDamagePercent, 0f) &&
                       effects.ActiveNextCombatRoomId == FloorGenerator.BuildRoomId(1, 3) &&
                       Approximately(stats.AttackDamage, 10f) &&
                       Approximately(stats.CreateDirectDamageContext(
                           player, DamageSourceType.PlayerAttack).Multiplier, 1.1f) &&
                       Approximately(stats.CreateDirectDamageContext(
                           player, DamageSourceType.PlayerProjectile).Multiplier, 1.1f) &&
                       Approximately(stats.CreateDirectDamageContext(
                           player, DamageSourceType.PlayerSkillExplosion).Multiplier, 1f),
                    "The next uncleared combat room must consume catch-that-one and grant +10% attack damage.");
                progress.GetRoomState(FloorGenerator.BuildRoomId(1, 3)).MarkCleared();
                Assert(string.IsNullOrEmpty(effects.ActiveNextCombatRoomId) &&
                       Approximately(stats.CreateDirectDamageContext(
                           player, DamageSourceType.PlayerAttack).Multiplier, 1f),
                    "Clearing the consumed room must remove catch-that-one without refunding it.");

                progress.RecordRoomEntry(1, 5);
                Assert(effects.IsBossRoomEffectActive &&
                       Approximately(stats.AttackSpeed, 1.45f) && Approximately(stats.MoveSpeed, 6.75f) &&
                       Approximately(stats.AttackDamage, 10f),
                    "An uncleared boss room must add final-sprint's +30% attack speed and +5% move speed " +
                    "without reactivating the consumed spell.");
                progress.GetRoomState(FloorGenerator.BuildRoomId(1, 5)).MarkCleared();
                Assert(!effects.IsBossRoomEffectActive &&
                       Approximately(stats.AttackSpeed, 1.15f) && Approximately(stats.MoveSpeed, 6.5f),
                    "Boss-room bonuses must end as soon as the boss room becomes cleared.");
                progress.RecordRoomEntry(1, 1);
                progress.RecordRoomEntry(1, 5);
                Assert(!effects.IsBossRoomEffectActive &&
                       Approximately(stats.AttackSpeed, 1.15f) && Approximately(stats.MoveSpeed, 6.5f),
                    "A cleared boss-room revisit must not reactivate final-sprint.");

                Assert(inventory.AcquiredItems.Count == 5 &&
                       inventory.GetStackCount("spell-catch-that-one") == 1 &&
                       inventory.GetStackCount("spell-final-sprint") == 1 &&
                       inventory.GetStackCount("spell-afterimage") == 3,
                    "Spell stacks and acquisition records must match the accepted common-path grants.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(progressObject);
            }
        }

        private static void ValidateFreshRunState()
        {
            GameObject progressObject = new("Spell-1 Fresh Progress", typeof(RunProgress));
            RunProgress progress = progressObject.GetComponent<RunProgress>();
            ConfigureTestGraph(progress);
            GameObject player = CreatePlayer(progress, out PlayerStats stats, out PlayerInventory inventory);
            try
            {
                progress.RecordRoomEntry(1, 5);
                Assert(player.GetComponent<PlayerSpellEffects>() == null &&
                       inventory.AcquiredItems.Count == 0 &&
                       Approximately(stats.AttackDamage, 10f) && Approximately(stats.AttackSpeed, 1f) &&
                       Approximately(stats.MoveSpeed, 5f),
                    "A fresh Run player must not retain pending, conditional, permanent, or stack spell state.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(progressObject);
            }
        }

        private static GameObject CreatePlayer(RunProgress progress, out PlayerStats stats,
            out PlayerInventory inventory)
        {
            GameObject player = new("Spell-1 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory));
            stats = player.GetComponent<PlayerStats>();
            inventory = player.GetComponent<PlayerInventory>();
            SetField(inventory, "runProgress", progress);
            InvokeLifecycle(player.GetComponent<Health>(), "Awake");
            InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
            InvokeLifecycle(stats, "Awake");
            InvokeLifecycle(inventory, "Awake");
            return player;
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
            GeneratedFloor floor = new(1, 0, 0, 0, nodes[0].RoomId, nodes[4].RoomId, nodes);
            GeneratedFloorGraph graph = new(new[] { floor }, 1);
            SetAutoProperty(progress, "GeneratedGraph", graph);

            FieldInfo statesField = typeof(RunProgress).GetField("roomStates",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Dictionary<string, RoomRunState> states =
                (Dictionary<string, RoomRunState>)statesField?.GetValue(progress);
            if (states == null) throw new InvalidOperationException("RunProgress room state storage was not found.");
            foreach (GeneratedRoomNode node in nodes) states.Add(node.RoomId, new RoomRunState(node.RoomId));
        }

        private static GeneratedRoomNode Node(int roomNumber, GeneratedRoomRole role) =>
            new(FloorGenerator.BuildRoomId(1, roomNumber), 1, roomNumber, role, null);

        private static ItemDefinition LoadSpell(string itemId)
        {
            ItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Items/{itemId}.asset");
            if (definition == null) throw new InvalidOperationException($"Missing spell asset '{itemId}'.");
            return definition;
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            FieldInfo field = target.GetType().GetField($"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException($"Backing field for {propertyName} was not found.");
            field.SetValue(target, value);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException($"Field {fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static bool Approximately(float actual, float expected) =>
            Mathf.Abs(actual - expected) <= 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
