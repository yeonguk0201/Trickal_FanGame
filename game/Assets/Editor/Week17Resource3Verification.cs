using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week17Resource3Verification
    {
        private const int DistributionSeedCount = 30000;
        private const float ChanceTolerance = 0.015f;
        private const float ShareTolerance = 0.03f;

        [MenuItem("Trickal Fan Game/Week 17/Setup and Verify Resource-3 Room Clear Drops")]
        public static void SetupAndVerifyBatch()
        {
            Week17Resource3Setup.Setup();
            string tableGuid = AssetDatabase.AssetPathToGUID(Week17Resource3Setup.RoomClearDropTablePath);
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week17Resource3Setup.Setup();
            Assert(tableGuid == AssetDatabase.AssetPathToGUID(Week17Resource3Setup.RoomClearDropTablePath) &&
                   sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Rerunning Resource-3 setup must keep the drop table and Game Scene GUIDs.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 17/Verify Resource-3 Room Clear Drops")]
        public static void Verify()
        {
            ResourceDropTable table =
                AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week17Resource3Setup.RoomClearDropTablePath);
            Assert(table != null, "Run Resource-3 setup before verification.");
            ValidateTableContract(table);
            ValidateSeededDistribution(table);
            ValidateSpawnerOnceAndRevisit(table);
            ValidateResourceDropBinding(table);
            ValidateSpDropperRemoved();

            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            // Opening scenes unloads assets held only from C#; reload before comparing references.
            table = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week17Resource3Setup.RoomClearDropTablePath);
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(table != null && assembler != null && assembler.EncounterClearDropTable == table,
                "The Game Scene RoomGraphAssembler must use the room clear drop table.");
            ValidateAssemblerIntegration(assembler, table);
            Week7LowerGradeSkillVerification.Verify();
            Debug.Log("Resource-3 room clear drop verification passed: each combat room rolls once from its seed " +
                      "with a 33% chance for heart 30 / SP 30 / elif 20 / key 12 / bomb 8, replays per seed, " +
                      "never re-rolls on revisit or floor reload, binds resource drops to the Run, and enemy " +
                      "kills no longer drop SP.");
        }

        private static void ValidateTableContract(ResourceDropTable table)
        {
            Assert(table.TryValidate(out string error), error);
            Assert(Mathf.Approximately(table.DropChance, Week17Resource3Setup.RoomClearDropChance),
                "Room clear drops must roll with a 33% chance.");
            Assert(table.Entries.Select(entry => (entry.DropId, entry.Weight))
                    .SequenceEqual(Week17Resource3Setup.RoomClearWeights),
                "Room clear drop candidates must be heart 30 / sp 30 / elif 20 / key 12 / bomb 8 in that order.");

            Dictionary<string, ResourceDropEntry> byId = table.Entries.ToDictionary(entry => entry.DropId);
            Assert(byId["heart"].Prefab.GetComponent<HealthPickup>() != null,
                "The heart drop must spawn the health pickup.");
            Assert(byId["sp"].Prefab.GetComponent<SPPickup>() != null, "The sp drop must spawn the SP pickup.");
            Assert(ResourceTypeOf(byId["elif"]) == RunResourceType.Gold &&
                   ResourceTypeOf(byId["key"]) == RunResourceType.Key &&
                   ResourceTypeOf(byId["bomb"]) == RunResourceType.Bomb,
                "Gold, key and bomb drops must spawn their Resource-1 pickups.");
        }

        private static void ValidateSeededDistribution(ResourceDropTable table)
        {
            Dictionary<string, int> counts = table.Entries.ToDictionary(entry => entry.DropId, _ => 0);
            int drops = 0;
            for (int index = 0; index < DistributionSeedCount; index++)
            {
                int seed = RoomClearRewardSpawner.DeriveDropSeed(index * 7919 + 17);
                bool dropped = table.TryRoll(seed, out ResourceDropEntry entry);
                bool replayed = table.TryRoll(seed, out ResourceDropEntry replay);
                Assert(dropped == replayed && entry == replay, $"Drop seed {seed} did not replay the same result.");
                if (!dropped) continue;
                drops++;
                counts[entry.DropId]++;
            }

            float dropRate = drops / (float)DistributionSeedCount;
            Assert(Mathf.Abs(dropRate - table.DropChance) <= ChanceTolerance,
                $"Room clear drop rate {dropRate:P2} must stay near {table.DropChance:P0}.");
            int totalWeight = table.Entries.Sum(entry => entry.Weight);
            foreach (ResourceDropEntry entry in table.Entries)
            {
                float share = counts[entry.DropId] / (float)drops;
                float expected = entry.Weight / (float)totalWeight;
                Assert(counts[entry.DropId] > 0 && Mathf.Abs(share - expected) <= ShareTolerance,
                    $"Drop '{entry.DropId}' share {share:P1} must stay near {expected:P0}.");
            }

            ResourceDropTable never = ScriptableObject.CreateInstance<ResourceDropTable>();
            try
            {
                never.Configure(0f, table.Entries.ToArray());
                Assert(Enumerable.Range(0, 1000).All(seed => !never.TryRoll(seed, out _)),
                    "A 0% table must never drop.");
                never.Configure(1f, Array.Empty<ResourceDropEntry>());
                Assert(!never.TryValidate(out _) && !never.TryRoll(1, out _),
                    "A table without candidates must fail validation and never drop.");
            }
            finally
            {
                Object.DestroyImmediate(never);
            }
        }

        private static void ValidateSpawnerOnceAndRevisit(ResourceDropTable table)
        {
            int dropSeed = FindSeed(table, entry => entry != null);
            int emptySeed = FindSeed(table, entry => entry == null);
            table.TryRoll(dropSeed, out ResourceDropEntry expected);

            GameObject root = new("Resource-3 Spawner Verification");
            try
            {
                RoomRunState state = new("resource-3-room");
                RoomClearRewardSpawner first = CreateSpawner(root.transform, table, state, dropSeed, null);
                Assert(first.TrySpawn() && first.HasRolled && first.LastSpawnedReward != null &&
                       first.LastSpawnedReward.name.Contains(expected.DropId),
                    "The first clear must spawn the seed's drop exactly once.");
                int children = root.transform.childCount;
                Assert(!first.TrySpawn() && root.transform.childCount == children,
                    "A second clear call must not spawn again.");

                RoomClearRewardSpawner revisit = CreateSpawner(root.transform, table, state, dropSeed, null);
                Assert(!revisit.TrySpawn() && revisit.LastSpawnedReward == null &&
                       root.transform.childCount == children + 1,
                    "A reconstructed room must not re-roll or drop again on revisit.");

                RoomClearRewardSpawner replay = CreateSpawner(root.transform, table, new RoomRunState("replay"),
                    dropSeed, null);
                Assert(replay.TrySpawn() && replay.LastSpawnedReward.name.Contains(expected.DropId),
                    "The same drop seed must spawn the same candidate in another Run.");

                RoomRunState emptyState = new("resource-3-empty-room");
                RoomClearRewardSpawner empty = CreateSpawner(root.transform, table, emptyState, emptySeed, null);
                Assert(!empty.TrySpawn() && empty.HasRolled && empty.LastSpawnedReward == null &&
                       emptyState.HasGrantedClearReward,
                    "A roll without a drop must still use up the room's roll.");
                RoomClearRewardSpawner emptyRevisit = CreateSpawner(root.transform, table, emptyState, emptySeed, null);
                Assert(!emptyRevisit.TrySpawn() && emptyRevisit.LastSpawnedReward == null,
                    "A room whose roll dropped nothing must not roll again on revisit.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateResourceDropBinding(ResourceDropTable table)
        {
            int keySeed = FindSeed(table, entry => entry != null && entry.DropId == "key");
            GameObject root = new("Resource-3 Binding Verification");
            GameObject progressHolder = new("Resource-3 Binding Progress");
            GameObject player = new("Resource-3 Binding Player");
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                player.AddComponent<Rigidbody2D>().gravityScale = 0f;
                player.AddComponent<CircleCollider2D>();
                Health health = player.AddComponent<Health>();
                PlayerStats stats = player.AddComponent<PlayerStats>();
                player.AddComponent<PlayerMovement>();
                InvokeLifecycle(health, "Awake");
                InvokeLifecycle(stats, "Awake");

                RoomClearRewardSpawner spawner = CreateSpawner(root.transform, table, new RoomRunState("key-room"),
                    keySeed, progress);
                Assert(spawner.TrySpawn(), "The key seed must drop a key.");
                RunResourcePickup key = spawner.LastSpawnedReward.GetComponent<RunResourcePickup>();
                InvokeLifecycle(key, "Awake");
                Assert(key != null && key.TryCollectFrom(player.GetComponent<Collider2D>()) &&
                       progress.GetResourceCount(RunResourceType.Key) == 1,
                    "A dropped key must be granted to the Run that the spawner is bound to.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(progressHolder);
                Object.DestroyImmediate(root);
            }
        }

        // Uses the real Game Scene assembler: every Encounter room gets the table and its content-seed drop seed, and a
        // cleared room keeps its single roll across a floor unload/reload.
        private static void ValidateAssemblerIntegration(RoomGraphAssembler assembler, ResourceDropTable table)
        {
            // Chest-1 replaced the assembler's clear drop with chests and Week22Chest1Verification owns that path.
            // The table, seeded roll and spawner checks above still cover the legacy single-pickup drop mode.
            if (assembler.ChestContentTable != null)
            {
                Week22Chest1Verification.ValidateEncounterRoomsUseChests(assembler);
                return;
            }

            Health playerHealth = assembler.Graph != null && assembler.Graph.Player != null
                ? assembler.Graph.Player.GetComponent<Health>()
                : null;
            Assert(playerHealth != null && assembler.Progress != null, "The Game Scene needs a player and RunProgress.");

            int runSeed = -1;
            string targetRoomId = null;
            for (int seed = 1; seed <= 256 && targetRoomId == null; seed++)
            {
                if (!assembler.Generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                targetRoomId = graph.Nodes.FirstOrDefault(node => node.Encounter != null &&
                    table.TryRoll(RoomClearRewardSpawner.DeriveDropSeed(node.ContentSeed), out _))?.RoomId;
                runSeed = seed;
            }
            Assert(targetRoomId != null, "No generated Encounter room within 256 seeds rolled a clear drop.");

            assembler.Progress.ResetProgress();
            Assert(assembler.Progress.TryInitializeRunSeed(runSeed, out string error), error);
            Assert(assembler.TryApplyGeneratedGraphForVerification(runSeed, out error), error);
            int encounterRooms = 0;
            foreach (GeneratedFloor floor in assembler.GeneratedGraph.Floors)
            {
                Assert(assembler.TryLoadFloor(floor.FloorNumber, assembler.Graph.Player, out error), error);
                foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
                {
                    GeneratedRoomNode node = floor.Nodes.Single(candidate => candidate.RoomId == room.Node.RoomId);
                    if (node.Encounter == null) continue;
                    encounterRooms++;
                    RoomClearRewardSpawner spawner = room.Controller.GetComponent<RoomClearRewardSpawner>();
                    Assert(spawner != null && spawner.DropTable == table &&
                           spawner.DropSeed == RoomClearRewardSpawner.DeriveDropSeed(node.ContentSeed),
                        $"Encounter room {node.RoomId} must roll the room clear table from its content seed.");
                }
            }
            Assert(encounterRooms > 0, "The verification Run must contain Encounter rooms.");

            GeneratedRoomNode target = assembler.GeneratedGraph.Nodes.Single(node => node.RoomId == targetRoomId);
            table.TryRoll(RoomClearRewardSpawner.DeriveDropSeed(target.ContentSeed), out ResourceDropEntry expected);
            Assert(assembler.TryLoadFloor(target.FloorNumber, assembler.Graph.Player, out error), error);
            RoomController controller = FindRoom(assembler, targetRoomId).Controller;
            RoomRunState state = assembler.Progress.GetRoomState(targetRoomId);
            List<GameObject> spawned = new();
            controller.EnemySpawned += spawned.Add;
            controller.BeginCombat(playerHealth);
            for (int guard = 0; controller.State != RoomState.Cleared && guard < 16; guard++)
            {
                foreach (GameObject enemy in spawned.ToArray())
                {
                    Health health = enemy != null ? enemy.GetComponent<Health>() : null;
                    if (health != null && !health.IsDead) health.TakeDamage(health.MaxHealth + 1f);
                }
            }

            RoomClearRewardSpawner clearDrop = controller.GetComponent<RoomClearRewardSpawner>();
            Assert(controller.State == RoomState.Cleared && state.HasGrantedClearReward &&
                   clearDrop.LastSpawnedReward != null &&
                   clearDrop.LastSpawnedReward.name.Contains(expected.DropId),
                $"Clearing {targetRoomId} must drop its seeded '{expected.DropId}' exactly once.");
            RunResourcePickup resourceDrop = clearDrop.LastSpawnedReward.GetComponent<RunResourcePickup>();
            Assert(resourceDrop == null || resourceDrop.CanCollect(assembler.Progress),
                "A dropped Run resource must be collectible into the current Run.");

            int otherFloor = target.FloorNumber == 1 ? 2 : 1;
            Assert(assembler.TryLoadFloor(otherFloor, assembler.Graph.Player, out error), error);
            Assert(assembler.TryLoadFloor(target.FloorNumber, assembler.Graph.Player, out error), error);
            RoomController revisited = FindRoom(assembler, targetRoomId).Controller;
            int revisitSpawns = 0;
            revisited.EnemySpawned += _ => revisitSpawns++;
            revisited.BeginCombat(playerHealth);
            RoomClearRewardSpawner revisitDrop = revisited.GetComponent<RoomClearRewardSpawner>();
            Assert(revisited.State == RoomState.Cleared && revisitSpawns == 0 && revisitDrop.HasRolled &&
                   !revisitDrop.TrySpawn() && revisitDrop.LastSpawnedReward == null,
                "A floor reload must not re-roll or re-drop a cleared room's drop.");
            assembler.Progress.ResetProgress();
        }

        private static RoomPrefab FindRoom(RoomGraphAssembler assembler, string roomId)
        {
            return assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(room => room.Node.RoomId == roomId);
        }

        private static void ValidateSpDropperRemoved()
        {
            Assert(typeof(PlayerSP).Assembly.GetType("TrickalFanGame.Player.PlayerSPDropper") == null,
                "The enemy-kill SP dropper must be removed.");
            foreach (string scenePath in Week17Resource3Setup.PlayerScenePaths)
            {
                string text = File.ReadAllText(scenePath, System.Text.Encoding.UTF8);
                Assert(!text.Contains("PlayerSPDropper") && !text.Contains("encounterClearRewardPrefab"),
                    $"{scenePath} must not keep the removed SP dropper or SP clear reward.");
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (PlayerMovement movement in Object.FindObjectsByType<PlayerMovement>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    Assert(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(movement.gameObject) == 0,
                        $"{scenePath} player '{movement.name}' must not keep a missing script.");
                }
            }
        }

        private static RoomClearRewardSpawner CreateSpawner(Transform parent, ResourceDropTable table,
            RoomRunState state, int seed, RunProgress progress)
        {
            GameObject room = new($"Room {state.RoomId}");
            room.transform.SetParent(parent);
            room.transform.position = Vector3.one * 1000f;
            RoomClearRewardSpawner spawner = room.AddComponent<RoomClearRewardSpawner>();
            spawner.Configure(table, room.transform, parent, state, seed, progress);
            return spawner;
        }

        private static int FindSeed(ResourceDropTable table, Func<ResourceDropEntry, bool> predicate)
        {
            for (int seed = 1; seed < 100000; seed++)
            {
                table.TryRoll(seed, out ResourceDropEntry entry);
                if (predicate(entry)) return seed;
            }

            throw new InvalidOperationException("No drop seed matched the verification case.");
        }

        private static RunResourceType? ResourceTypeOf(ResourceDropEntry entry)
        {
            RunResourcePickup pickup = entry.Prefab != null ? entry.Prefab.GetComponent<RunResourcePickup>() : null;
            return pickup != null ? pickup.ResourceType : null;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
