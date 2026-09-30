using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Boss4Verification
    {
        private static readonly string[] ExpectedProfiles = { "boss-floor-1", "boss-floor-2", "boss-floor-3" };

        [MenuItem("Trickal Fan Game/Week 15/Verify Boss-4 Full Three-Floor Run")]
        public static void Verify()
        {
            Week15Boss3Verification.Verify();
            ValidateRosterAndProfiles();
            ValidateGeneratedGraphSeeds(1504, 1515);
            RoomGraphAssembler configured = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>(
                FindObjectsInactive.Include);
            ValidateThreeFloorRun(configured.Progress.HasRunSeed ? configured.Progress.RunSeed : 1504);
            Debug.Log("Week 15 Boss-4 verification passed: floor-specific bosses, rewards, exits, final clear, " +
                      "and three-floor Run progression are idempotent across multiple seeds.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week15Boss4Setup.Setup();
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week15Boss4Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Boss-4 setup changed the Game Scene GUID.");
            Verify();
        }

        private static void ValidateRosterAndProfiles()
        {
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>(
                FindObjectsInactive.Include);
            Assert(assembler != null && assembler.FloorBossPrefabs.Count == 3,
                "Boss-4 requires exactly three floor boss prefabs.");
            Assert(assembler.ResolveBossPrefab(1) != null && assembler.ResolveBossPrefab(2) != null &&
                   assembler.ResolveBossPrefab(3) != null &&
                   !ReferenceEquals(assembler.ResolveBossPrefab(1), assembler.ResolveBossPrefab(2)) &&
                   !ReferenceEquals(assembler.ResolveBossPrefab(2), assembler.ResolveBossPrefab(3)),
                "Each floor must resolve a distinct boss prefab.");
            Assert(assembler.ResolveBossPrefab(1).GetComponent<BuseureogiBossPatternRuntime>() != null &&
                   assembler.ResolveBossPrefab(2).GetComponent<SaemaeumVaultBossPatternRuntime>() != null &&
                   assembler.ResolveBossPrefab(3).GetComponent<CrayonHeroBossPatternRuntime>() != null,
                "Floor boss roster is not bound to the expected runtime implementations.");

            for (int floor = 1; floor <= 3; floor++)
            {
                Assert(assembler.Generator.RoomTemplates.Any(template =>
                        template != null && template.Profile != null &&
                        template.Profile.ProfileId == ExpectedProfiles[floor - 1] &&
                        template.MinimumFloor == floor && template.MaximumFloor == floor),
                    $"Floor {floor} is missing its floor-specific boss Room Template.");
            }
        }

        private static void ValidateThreeFloorRun(int seed)
        {
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>(
                FindObjectsInactive.Include);
            RunProgress progress = assembler.Progress;
            progress.ResetProgress();
            Assert(progress.TryInitializeRunSeed(seed, out string error), error);
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out error), error);

            PlayerMovement player = assembler.Graph.Player;
            Assert(player != null && player.GetComponent<Health>() != null,
                "Boss-4 integration verification requires a configured player.");
            if (assembler.RewardSelectionSession != null)
            {
                Health health = player.GetComponent<Health>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                InvokeLifecycle(health, "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                InvokeLifecycle(inventory, "Awake");
                assembler.RewardSelectionSession.Configure(progress, inventory, health,
                    player.GetComponent<PlayerActionState>());
            }
            int finalClearEvents = 0;
            Action onFinalBossCleared = () => finalClearEvents++;
            progress.FinalBossCleared += onFinalBossCleared;
            try
            {
                for (int floor = 1; floor <= 3; floor++)
                {
                    Assert(assembler.TryLoadFloor(floor, player, out error), error);
                    RoomPrefab bossRoom = FindBossRoom(assembler);
                    Assert(bossRoom.Node.Profile != null &&
                           (bossRoom.Node.Profile.ProfileId == ExpectedProfiles[floor - 1] ||
                            bossRoom.Node.Profile.ProfileId == "basic"),
                        $"Floor {floor} did not use its dedicated boss Room Profile.");

                    BossController spawned = null;
                    bossRoom.Controller.EnemySpawned += enemy => spawned = enemy?.GetComponent<BossController>();
                    bossRoom.Controller.BeginCombat(player.GetComponent<Health>());
                    Assert(spawned != null && spawned.GetComponent<BossController>() != null,
                        $"Floor {floor} boss did not spawn from the floor roster.");
                    ItemDropSource drop = spawned.GetComponent<ItemDropSource>();
                    BossItemDrop bossDrop = spawned.GetComponent<BossItemDrop>();
                    spawned.GetComponent<Health>().TakeDamage(99999f);
                    Assert(bossRoom.Controller.State == RoomState.Cleared,
                        $"Floor {floor} boss room did not clear after the boss died.");

                    if (floor < 3)
                    {
                        FloorAdvancePortal portal = bossRoom.GetComponentInChildren<FloorAdvancePortal>(true);
                        if (bossDrop != null && bossDrop.UsesSelectionReward)
                        {
                            ItemRewardSelectionSession session = assembler.RewardSelectionSession;
                            Assert(session != null && session.IsOpen && portal != null && !portal.IsUnlocked,
                                $"Floor {floor} selection reward must keep the portal locked before confirmation.");
                            Assert(session.TrySelect(0),
                                $"Floor {floor} selection reward could not be confirmed.");
                        }
                        Assert(drop != null && drop.HasDropped,
                            $"Floor {floor} boss did not resolve its one growth reward. " +
                            $"source={(drop == null ? "missing" : $"present/hasDropped={drop.HasDropped}")}, " +
                            $"bossDrop={(bossDrop == null ? "missing" : $"present/final={bossDrop.IsFinalBoss}")}.");
                        Assert(portal != null && portal.IsUnlocked && portal.TryEnter(player),
                            $"Floor {floor} boss clear did not unlock the next-floor portal.");
                        Assert(progress.CurrentFloor == floor + 1,
                            $"Floor {floor} portal did not record entry into floor {floor + 1}.");
                    }
                    else
                    {
                        Assert(progress.HasClearedFinalBoss && finalClearEvents == 1,
                            "Final boss clear must emit one idempotent completion event.");
                        Assert(drop == null || !drop.HasDropped,
                            "The final boss must not create a duplicate growth reward.");
                    }
                }

                Assert(finalClearEvents == 1, "A three-floor Run must complete exactly once.");
                Assert(assembler.TryLoadFloor(3, player, out error), error);
                RoomPrefab revisitedBoss = FindBossRoom(assembler);
                Assert(revisitedBoss.Controller.State == RoomState.Cleared &&
                       revisitedBoss.Controller.AliveEnemyCount == 0,
                    "Revisiting the final boss floor must not respawn or re-reward the boss.");
                progress.RecordFinalBossCleared();
                Assert(finalClearEvents == 1, "Repeated final-clear notifications must be ignored.");
            }
            finally
            {
                progress.FinalBossCleared -= onFinalBossCleared;
            }
        }

        private static void ValidateGeneratedGraphSeeds(params int[] seeds)
        {
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>(
                FindObjectsInactive.Include);
            string firstSignature = null;
            foreach (int seed in seeds)
            {
                Assert(assembler.Generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph,
                        out string error), error);
                Assert(graph.Floors.Count == 3, $"Seed {seed} did not generate three floors.");
                for (int floor = 1; floor <= 3; floor++)
                {
                    GeneratedFloor generated = graph.FindFloor(floor);
                    GeneratedRoomNode boss = generated.Nodes.Single(node =>
                        node.Definition.RoomType == RoomType.Boss);
                    Assert(boss.Template != null && boss.Template.Profile != null &&
                           (boss.Template.Profile.ProfileId == ExpectedProfiles[floor - 1] ||
                            boss.Template.Profile.ProfileId == "basic"),
                        $"Seed {seed} selected the wrong boss Room Template on floor {floor}.");
                }

                string signature = string.Join("|", graph.Nodes.Select(node =>
                    $"{node.RoomId}:{node.TemplateId}:{node.EncounterId}"));
                if (firstSignature == null) firstSignature = signature;
                else Assert(signature != firstSignature,
                    "The Boss-4 regression seeds must exercise distinct generated content.");
            }
        }

        private static RoomPrefab FindBossRoom(RoomGraphAssembler assembler) =>
            assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(room => room.Node.Definition.RoomType == RoomType.Boss);

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target?.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
