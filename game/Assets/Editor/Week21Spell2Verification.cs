using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Spell-2: 그건 내 잔상 becomes a single-use escape. During combat in a combat or boss room it moves the player to
    // the current floor's start room; the escaped room drops its remaining enemies without kills, returns to uncleared
    // and restarts its Encounter from the first wave (a boss at full HP) on the next entry, while destroyed obstacles,
    // floor pickups and the unpaid clear reward stay as they were. Anywhere else it is refused unconsumed.
    public static class Week21Spell2Verification
    {
        private const int MaximumSeedSearch = 4096;

        public static void SetupAndVerifyBatch()
        {
            Week21Spell2Setup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(Week21Spell2Setup.AfterimagePath);
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week21Spell2Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) &&
                   guid == AssetDatabase.AssetPathToGUID(Week21Spell2Setup.AfterimagePath) &&
                   sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Spell-2 setup changed an asset or Game Scene GUID.");
            Verify();
        }

        // Also re-runs the room features the escape touches: Encounter waves, the shared teleport path and Spell-1.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week19Encounter4Verification.Verify();
            Week20Special3Verification.Verify();
            Week21Spell1Verification.Verify();
            Debug.Log("Spell-2 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 21/Verify Spell-2 Afterimage")]
        public static void Verify()
        {
            ValidateContract();
            RoomGraphAssembler assembler = ValidateSceneWiring();
            ValidateCombatRoomEscape(assembler);
            ValidateBossRoomEscape(assembler);
            Debug.Log("Spell-2 verification passed: 그건 내 잔상 is a single-use spell that, during combat in a combat " +
                      "or boss room, moves the player to the floor's start room once; the escaped room drops its " +
                      "enemies without kills and restarts from the first wave (a boss at full HP) on re-entry while " +
                      "obstacles, pickups and the clear reward keep their state; the start room, rooms not in combat, " +
                      "cleared rooms and a blocked move refuse it unconsumed; the legacy spell keeps its contract and " +
                      "left the selection reward pool.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.EscapeToFloorStartRoom == 31 &&
                   (int)ItemEffectType.CurrentRoomBasicAttackDamagePercent == 30 &&
                   (int)ItemEffectType.MoveSpeedPercent == 19 && (int)ItemEffectType.AttackSpeedPercent == 9,
                "Spell-2 must add effect type 31 without renumbering existing effects.");

            ItemDefinition item = LoadItem(Week21Spell2Setup.AfterimagePath);
            Assert(item.ItemId == Week21Spell2Setup.AfterimageId && item.DisplayName == "그건 내 잔상" &&
                   item.Kind == ItemKind.SingleUseSpell && item.Rarity == ItemRarity.Uncommon && item.IsActive &&
                   item.MaxStacks == 1 && item.IsValid && item.IsSingleUse,
                "single-spell-afterimage must be a valid active Uncommon single-use spell named 그건 내 잔상.");
            Assert(item.Effects.Count == 1 && item.Effects[0].EffectType == ItemEffectType.EscapeToFloorStartRoom,
                "그건 내 잔상 must carry only the escape effect.");
            Assert(ArtifactEffectDescription.Build(item) ==
                   "전투 중 현재 층 시작방으로 탈출 (탈출한 방은 다시 들어가면 처음부터 시작)",
                "그건 내 잔상 must describe its escape effect.");

            ItemDefinition legacy = LoadItem(Week21Spell2Setup.LegacyAfterimagePath);
            Assert(legacy.ItemId == Week21Spell2Setup.LegacyAfterimageId && legacy.DisplayName == "그건 내 잔상" &&
                   legacy.Kind == ItemKind.Spell && legacy.Rarity == ItemRarity.Uncommon && legacy.IsActive &&
                   legacy.IsValid && !legacy.IsSingleUse && legacy.MaxStacks == 3 && legacy.Effects.Count == 2 &&
                   legacy.Effects[0].EffectType == ItemEffectType.MoveSpeedPercent &&
                   Approximately(legacy.Effects[0].Magnitude, 0.1f) &&
                   legacy.Effects[1].EffectType == ItemEffectType.AttackSpeedPercent &&
                   Approximately(legacy.Effects[1].Magnitude, 0.05f),
                "The legacy spell-afterimage contract must stay unchanged and active (Contract-0 §2.2).");
            Assert(LegacySpellRetirement.IsRetired(legacy) && !LegacySpellRetirement.IsRetired(item),
                "Only the legacy spell-afterimage may be marked retired.");
        }

        private static RoomGraphAssembler ValidateSceneWiring()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            IReadOnlyList<ItemDefinition> pool = assembler.SelectionRewardPool;
            Assert(pool.Count >= ArtifactRewardSelector.MaximumCandidateCount &&
                   pool.All(item => item != null && item.IsActive && item.IsValid && !item.IsSingleUse),
                "The selection reward pool must keep enough active valid non-single-use items.");
            Assert(pool.All(item => item.ItemId != Week21Spell2Setup.LegacyAfterimageId &&
                                    !LegacySpellRetirement.IsRetired(item)),
                "The legacy spell-afterimage must leave the selection reward pool and shop stock (Contract-0 §2.2).");

            RoomGraphController graph = assembler.Graph;
            PlayerSingleUseEffects effects = graph?.Player != null
                ? graph.Player.GetComponent<PlayerSingleUseEffects>()
                : null;
            Assert(effects != null && effects.RoomGraph == graph,
                "The Game Scene player's single-use executor must reference the room graph it escapes through.");
            return assembler;
        }

        private static void ValidateCombatRoomEscape(RoomGraphAssembler assembler)
        {
            int seed = FindSeed(assembler.Generator, node =>
                node.Role == GeneratedRoomRole.Intermediate && node.Encounter != null &&
                node.Encounter.Waves.Count >= 2);
            using TestRun run = new(assembler, seed);
            ItemDefinition item = LoadItem(Week21Spell2Setup.AfterimagePath);
            RoomNode start = run.Graph.StartingNode;
            GeneratedRoomNode generated = run.Floor.Nodes.First(node =>
                node.Role == GeneratedRoomRole.Intermediate && node.Encounter?.Waves.Count >= 2);
            RoomPrefab room = run.FindRoom(generated.RoomId);
            RoomController controller = room.Controller;
            RoomRunState state = run.Progress.GetRoomState(generated.RoomId);

            Assert(run.Graph.CurrentNode == start, "The verification Run must begin in the start room.");
            run.Give(item, "spell2-start");
            Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HeldDefinition == item &&
                   run.Graph.CurrentNode == start,
                "The start room must refuse 그건 내 잔상 and keep it in the slot.");

            run.MoveTo(room);
            Assert(controller.State == RoomState.Waiting && run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet &&
                   run.Slot.HasItem && run.Graph.CurrentNode == room.Node,
                "A combat room whose fight has not started must refuse 그건 내 잔상.");

            // The obstacle, the floor pickup and the unpaid clear reward are terrain and reward state of the room.
            string obstacleId = room.GetComponentsInChildren<DestructibleObstacle>(true).FirstOrDefault()?.ObstacleId ??
                                "spell2-obstacle";
            Assert(state.TryMarkObstacleDestroyed(obstacleId), "The verification obstacle must start intact.");
            SingleUseItemPickup floorPickup = Object.Instantiate(run.PickupPrefab, room.Node.ContentRoot.transform);
            floorPickup.Configure(item, "spell2-floor", false);

            List<GameObject> firstSpawn = run.BeginCombat(controller);
            Assert(controller.State == RoomState.Combat && controller.CurrentWaveNumber == 1 &&
                   controller.AliveEnemyCount > 0 && EncounterLocked(room, true),
                "Entering the combat room must start the first wave behind locked doors.");
            string[] firstWave = Names(firstSpawn);

            run.ResetTransitionCooldown();
            Time.timeScale = 0f;
            Assert(run.Slot.TryUse() == SpellSlotUseResult.Paused && run.Slot.HasItem,
                "그건 내 잔상 must not be used while paused.");
            Time.timeScale = 1f;

            // Wave 1 cleared, wave 2 running: the escape must still restart from wave 1.
            run.KillAll(controller);
            Assert(state.CompletedWaveCount == 1 && controller.CurrentWaveNumber == 2 &&
                   controller.AliveEnemyCount > 0 && controller.State == RoomState.Combat,
                "The second wave must be running before the escape.");
            List<GameObject> liveEnemies = run.Spawned.Where(enemy => enemy != null &&
                !enemy.GetComponent<Health>().IsDead).ToList();
            int killsBefore = run.Progress.KillCount;

            run.BlockTransitions();
            Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HasItem &&
                   run.Graph.CurrentNode == room.Node && controller.State == RoomState.Combat,
                "A blocked room move must refuse 그건 내 잔상 without consuming it or resetting the room.");
            run.ResetTransitionCooldown();

            Assert(run.Slot.TryUse() == SpellSlotUseResult.Used && !run.Slot.HasItem,
                "During combat 그건 내 잔상 must be consumed.");
            Assert(run.Graph.CurrentNode == start && run.Progress.CurrentRoom == start.RoomNumber &&
                   ((Vector2)run.Graph.Player.transform.position - start.InitialSpawnPosition).sqrMagnitude < 0.0001f &&
                   !room.Node.IsVisible && start.IsVisible,
                "그건 내 잔상 must move the player to the current floor's start room.");
            Assert(liveEnemies.Count > 0 && liveEnemies.All(enemy => enemy == null) &&
                   controller.AliveEnemyCount == 0 &&
                   run.Progress.KillCount == killsBefore,
                "The escape must remove the remaining enemies without counting kills.");
            Assert(controller.State == RoomState.Waiting && !controller.HasStarted &&
                   controller.CurrentWaveNumber == 0 && !state.IsCleared && state.CompletedWaveCount == 0 &&
                   !state.HasGrantedClearReward && state.HasVisited && EncounterLocked(room, false),
                "The escaped room must return to uncleared with its doors open and its waves reset.");
            Assert(state.IsObstacleDestroyed(obstacleId) && floorPickup != null && !floorPickup.IsCollected,
                "The escape must keep destroyed obstacles and floor pickups.");

            run.Give(item, "spell2-again");
            Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HasItem &&
                   run.Graph.CurrentNode == start,
                "Using another 그건 내 잔상 in the start room must not move or consume it.");

            run.Spawned.Clear();
            run.MoveTo(room);
            List<GameObject> secondSpawn = run.BeginCombat(controller);
            Assert(controller.State == RoomState.Combat && controller.CurrentWaveNumber == 1 &&
                   Names(secondSpawn).SequenceEqual(firstWave) && EncounterLocked(room, true),
                "Re-entering the escaped room must restart the same Encounter from its first wave.");
            Assert(state.IsObstacleDestroyed(obstacleId) && floorPickup != null,
                "Re-entering must keep destroyed obstacles and floor pickups.");

            for (int wave = 0; wave < generated.Encounter.Waves.Count && controller.State == RoomState.Combat; wave++)
                run.KillAll(controller);
            Assert(controller.State == RoomState.Cleared && state.IsCleared && state.HasGrantedClearReward &&
                   state.CompletedWaveCount == generated.Encounter.Waves.Count,
                "Clearing the restarted Encounter must clear the room and pay its reward once.");
            run.ResetTransitionCooldown();
            Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HasItem &&
                   run.Graph.CurrentNode == room.Node,
                "A cleared room must refuse 그건 내 잔상.");

            Assert(run.Inventory.AcquiredItems.Count(record => record.ItemId == item.ItemId) == 2,
                "Each 그건 내 잔상 instance must be recorded once.");
        }

        private static void ValidateBossRoomEscape(RoomGraphAssembler assembler)
        {
            int seed = FindSeed(assembler.Generator, node => node.Role == GeneratedRoomRole.Boss);
            using TestRun run = new(assembler, seed);
            ItemDefinition item = LoadItem(Week21Spell2Setup.AfterimagePath);
            GeneratedRoomNode generated = run.Floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Boss);
            RoomPrefab room = run.FindRoom(generated.RoomId);
            RoomController controller = room.Controller;
            RoomRunState state = run.Progress.GetRoomState(generated.RoomId);

            run.MoveTo(room);
            BossController firstBoss = run.BeginCombat(controller).Select(enemy => enemy.GetComponent<BossController>())
                .Single(boss => boss != null);
            Health firstHealth = firstBoss.GetComponent<Health>();
            Assert(Approximately(firstHealth.CurrentHealth, firstHealth.MaxHealth) && firstHealth.MaxHealth > 1f,
                "The boss must start at full HP.");
            firstHealth.TakeDamage(firstHealth.MaxHealth * 0.5f);
            Assert(!firstHealth.IsDead && firstHealth.CurrentHealth < firstHealth.MaxHealth,
                "The boss must be damaged before the escape.");

            run.Give(item, "spell2-boss");
            run.ResetTransitionCooldown();
            Assert(run.Slot.TryUse() == SpellSlotUseResult.Used && run.Graph.CurrentNode == run.Graph.StartingNode,
                "그건 내 잔상 must escape from a boss fight.");
            Assert(firstBoss == null && controller.State == RoomState.Waiting && !state.IsCleared &&
                   !run.Progress.HasClearedFinalBoss,
                "The escape must remove the boss and leave the boss room uncleared.");

            run.MoveTo(room);
            BossController secondBoss = run.BeginCombat(controller).Select(enemy => enemy.GetComponent<BossController>())
                .Single(boss => boss != null);
            Health secondHealth = secondBoss.GetComponent<Health>();
            Assert(secondBoss.name == run.FirstBossName && !secondHealth.IsDead &&
                   Approximately(secondHealth.CurrentHealth, secondHealth.MaxHealth) &&
                   Approximately(secondHealth.MaxHealth, run.FirstBossMaxHealth),
                "Re-entering the boss room must restart the same boss at full HP.");
        }

        private static int FindSeed(FloorGenerator generator, Func<GeneratedRoomNode, bool> wanted)
        {
            Assert(generator != null, "The Game Scene assembler needs its FloorGenerator.");
            for (int seed = 1; seed <= MaximumSeedSearch; seed++)
            {
                if (!generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                if (graph.FindFloor(1)?.Nodes.Any(wanted) == true) return seed;
            }

            throw new InvalidOperationException("No seed produced the Spell-2 verification room on floor 1.");
        }

        private static string[] Names(IEnumerable<GameObject> enemies) =>
            enemies.Select(enemy => enemy.name).ToArray();

        // Only the Encounter lock follows the fight; a key lock on a neighboring treasure door stays for its own reason.
        private static bool EncounterLocked(RoomPrefab room, bool locked)
        {
            FieldInfo field = typeof(DoorController).GetField("encounterLocked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(field != null, "DoorController Encounter lock storage was not found.");
            return room.DoorSlots.All(slot => (bool)field.GetValue(slot.Blocker) == locked);
        }

        private static ItemDefinition LoadItem(string path)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            Assert(item != null, $"Run Spell-2 setup first: {path} is missing.");
            return item;
        }

        private static bool Approximately(float actual, float expected) => Mathf.Abs(actual - expected) <= 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        // The Game Scene Run on a fixed seed, with the real player, slot, executor and room graph. Edit mode runs no
        // lifecycle callbacks, so the player components and each spawned enemy are woken here as Play Mode would.
        private sealed class TestRun : IDisposable
        {
            private readonly RoomGraphAssembler assembler;
            private readonly float previousTimeScale;
            private readonly List<RoomController> watchedRooms = new();
            private int givenCount;

            public RunProgress Progress => assembler.Progress;
            public RoomGraphController Graph => assembler.Graph;
            public GeneratedFloor Floor { get; }
            public PlayerSpellSlot Slot { get; }
            public PlayerInventory Inventory { get; }
            public SingleUseItemPickup PickupPrefab { get; }
            public List<GameObject> Spawned { get; } = new();
            public string FirstBossName { get; private set; }
            public float FirstBossMaxHealth { get; private set; }

            public TestRun(RoomGraphAssembler configuredAssembler, int seed)
            {
                assembler = configuredAssembler;
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;
                Progress.ResetProgress();
                Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
                Floor = assembler.GeneratedGraph.FindFloor(1);

                GameObject player = Graph.Player.gameObject;
                Slot = player.GetComponent<PlayerSpellSlot>();
                Inventory = player.GetComponent<PlayerInventory>();
                PickupPrefab = Slot.PickupPrefab;
                Assert(Slot != null && Inventory != null && PickupPrefab != null &&
                       player.GetComponent<PlayerSingleUseEffects>() != null,
                    "Run Slot-0 setup first: the Game Scene player needs the spell slot.");
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(Inventory, "Awake");
                InvokeLifecycle(Slot, "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSingleUseEffects>(), "Awake");

                // Every wave of every room spawns through EnemySpawned, including waves started by a kill.
                foreach (RoomNode node in Graph.Nodes)
                {
                    RoomController controller = node.GetComponent<RoomPrefab>()?.Controller;
                    if (controller == null) continue;
                    controller.EnemySpawned += Wake;
                    watchedRooms.Add(controller);
                }
            }

            public void Give(ItemDefinition item, string instanceId)
            {
                SingleUseItemPickup pickup = Object.Instantiate(PickupPrefab, Graph.CurrentNode.ContentRoot.transform);
                pickup.Configure(item, $"{instanceId}-{++givenCount}", false);
                Assert(Slot.TryCollect(pickup) && Slot.HeldDefinition == item, $"The slot must take {item.ItemId}.");
            }

            public RoomPrefab FindRoom(string roomId)
            {
                foreach (RoomNode node in Graph.Nodes)
                    if (node.RoomId == roomId) return node.GetComponent<RoomPrefab>();
                throw new InvalidOperationException($"Runtime room {roomId} is missing.");
            }

            public void MoveTo(RoomPrefab room)
            {
                ResetTransitionCooldown();
                Assert(Graph.TryTeleport(Graph.CurrentNode, room.Node, room.Node.DefaultEntryPoint.position,
                        Graph.Player) && Graph.CurrentNode == room.Node,
                    $"The verification player must reach {room.Node.RoomId}.");
                Physics2D.SyncTransforms();
            }

            // The room trigger does not fire in edit mode; entering starts the fight the same way.
            public List<GameObject> BeginCombat(RoomController controller)
            {
                int before = Spawned.Count;
                controller.BeginCombat(Graph.Player.GetComponent<Health>());
                return Spawned.Skip(before).ToList();
            }

            private void Wake(GameObject enemy)
            {
                InvokeLifecycle(enemy.GetComponent<Health>(), "Awake");
                Spawned.Add(enemy);
                if (enemy.GetComponent<BossController>() == null || FirstBossName != null) return;
                FirstBossName = enemy.name;
                FirstBossMaxHealth = enemy.GetComponent<Health>().MaxHealth;
            }

            public void KillAll(RoomController controller)
            {
                Assert(controller.KillAliveEnemiesForDevelopment() > 0, "A running wave must have enemies to kill.");
            }

            public void ResetTransitionCooldown() => SetCooldown(0f);

            public void BlockTransitions() => SetCooldown(float.MaxValue);

            public void Dispose()
            {
                foreach (RoomController controller in watchedRooms)
                    if (controller != null) controller.EnemySpawned -= Wake;
                Time.timeScale = previousTimeScale;
                ResetTransitionCooldown();
                Progress.ResetProgress();
            }

            private void SetCooldown(float until)
            {
                typeof(RoomGraphController).GetField("nextTransitionTime", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(Graph, until);
            }

            private static void InvokeLifecycle(object target, string methodName)
            {
                if (target == null) return;
                MethodInfo method = target.GetType().GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                method?.Invoke(target, null);
            }
        }
    }
}
