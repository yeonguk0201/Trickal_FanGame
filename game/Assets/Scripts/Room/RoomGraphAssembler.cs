using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [DefaultExecutionOrder(-200)]
    public sealed class RoomGraphAssembler : MonoBehaviour
    {
        [SerializeField] private FloorGenerator generator;
        [SerializeField] private RoomGraphController graph;
        [SerializeField] private RunProgress runProgress;
        [SerializeField] private RoomPrefab roomPrefab;

        private GeneratedFloorGraph generatedGraph;
        private GameObject currentFloorRoot;

        private bool hasAppliedRuntimeGraph;
        private int appliedRunSeed;

        public FloorGenerator Generator => generator;
        public RoomGraphController Graph => graph;
        public RunProgress Progress => runProgress;
        public bool HasAppliedRuntimeGraph => hasAppliedRuntimeGraph;
        public int AppliedRunSeed => appliedRunSeed;
        public RoomPrefab ConfiguredRoomPrefab => roomPrefab;
        public GeneratedFloorGraph GeneratedGraph => generatedGraph;

        public void Configure(
            FloorGenerator configuredGenerator,
            RoomGraphController configuredGraph,
            RunProgress configuredProgress)
        {
            generator = configuredGenerator;
            graph = configuredGraph;
            runProgress = configuredProgress;
        }

        public void Configure(FloorGenerator configuredGenerator, RoomGraphController configuredGraph,
            RunProgress configuredProgress, RoomPrefab configuredRoomPrefab)
        {
            Configure(configuredGenerator, configuredGraph, configuredProgress);
            roomPrefab = configuredRoomPrefab;
        }

        private void Awake()
        {
            if (runProgress == null && graph != null)
            {
                runProgress = graph.Progress;
            }

            if (roomPrefab != null)
            {
                for (int index = transform.childCount - 1; index >= 0; index--)
                {
                    GameObject child = transform.GetChild(index).gameObject;
                    if (child.name.StartsWith("Generated Floor ", StringComparison.Ordinal))
                        DestroyFloor(child);
                }
            }

            if (!TryApplyGeneratedGraph(out string error))
            {
                Debug.LogError($"{name}: Failed to assemble generated room graph. {error}", this);
                enabled = false;
            }
        }

        public bool TryApplyGeneratedGraph(out string error)
        {
            if (generator == null || graph == null || runProgress == null)
            {
                error = "FloorGenerator, RoomGraphController, and RunProgress references are required.";
                return false;
            }

            if (!runProgress.HasRunSeed)
            {
                error = "RunProgress must initialize the Run seed before graph assembly.";
                return false;
            }

            if (hasAppliedRuntimeGraph && appliedRunSeed != runProgress.RunSeed)
            {
                error = $"The assembled Run seed {appliedRunSeed} cannot change to {runProgress.RunSeed}.";
                return false;
            }

            if (!generator.TryInitializeRunSeed(runProgress.RunSeed, out error))
            {
                return false;
            }

            if (!TryApplyGeneratedGraphForSeed(runProgress.RunSeed, out error))
            {
                return false;
            }

            appliedRunSeed = runProgress.RunSeed;
            hasAppliedRuntimeGraph = true;
            return true;
        }

        public bool TryApplyGeneratedGraphForVerification(int fixedSeed, out string error)
        {
            return TryApplyGeneratedGraphForSeed(fixedSeed, out error);
        }

        private bool TryApplyGeneratedGraphForSeed(int seed, out string error)
        {
            if (generator == null || graph == null)
            {
                error = "FloorGenerator and RoomGraphController references are required.";
                return false;
            }

            if (!generator.TryGenerateForSeed(seed, out GeneratedFloorGraph generatedGraph, out error))
            {
                return false;
            }

            this.generatedGraph = generatedGraph;
            if (roomPrefab != null)
            {
                if (!roomPrefab.TryValidate(out error)) return false;
                if (runProgress != null && runProgress.GeneratedGraph == null &&
                    !runProgress.TrySetGeneratedGraph(generatedGraph, out error)) return false;
                return TryBuildFloor(1, null, out error);
            }

            if (!graph.TryValidateConfiguration(out error))
            {
                error = $"RoomGraphController is invalid before generated binding. {error}";
                return false;
            }

            if (!TryCreateBindings(generatedGraph, out List<RoomBinding> bindings, out error))
            {
                return false;
            }

            foreach (RoomBinding binding in bindings)
            {
                ApplyDefinition(binding);
                ApplyDoorways(binding);
            }

            return graph.TryValidateConfiguration(out error);
        }

        public bool TryLoadFloor(int floorNumber, PlayerMovement transitioningPlayer, out string error)
        {
            if (generatedGraph == null)
            {
                error = "No generated graph is available for floor loading.";
                return false;
            }

            if (!TryBuildFloor(floorNumber, transitioningPlayer, out error)) return false;
            return true;
        }

        private bool TryBuildFloor(int floorNumber, PlayerMovement transitioningPlayer, out string error)
        {
            GeneratedFloor floor = generatedGraph?.FindFloor(floorNumber);
            if (floor == null || roomPrefab == null)
            {
                error = $"Generated floor {floorNumber} or its verified Room Prefab is missing.";
                return false;
            }

            GameObject nextRoot = new($"Generated Floor {floorNumber:00}");
            nextRoot.transform.SetParent(transform, false);
            Dictionary<string, RoomPrefab> instances = new(StringComparer.Ordinal);
            List<RoomNode> nodes = new(floor.Nodes.Count);
            foreach (GeneratedRoomNode generatedNode in floor.Nodes)
            {
                RoomPrefab instance = Instantiate(roomPrefab, nextRoot.transform);
                instance.name = generatedNode.RoomId;
                instance.transform.localPosition = new Vector3(
                    generatedNode.GridPosition.X * 16f, generatedNode.GridPosition.Y * 12f, 0f);
                if (!instance.TryValidate(out error)) { DestroyFloor(nextRoot); return false; }

                RoomNode node = instance.Node;
                node.Configure(generatedNode.RoomId, generatedNode.FloorNumber, generatedNode.RoomNumber,
                    node.ContentRoot, node.CameraAnchor, node.DefaultEntryPoint, Array.Empty<RoomDoorway>());
                node.ApplyGeneratedDefinition(generatedNode.Definition);
                RoomRunState state = runProgress?.GetRoomState(generatedNode.RoomId);
                DoorController[] blockers = new DoorController[instance.DoorSlots.Length];
                for (int i = 0; i < blockers.Length; i++) blockers[i] = instance.DoorSlots[i].Blocker;
                Transform[] spawnPoints = CopySpawnPoints(instance.Controller.SpawnPoints,
                    generatedNode.Role == GeneratedRoomRole.Boss ? 1 : int.MaxValue);
                instance.Controller.Configure(generatedNode.FloorNumber, generatedNode.RoomNumber, runProgress,
                    null, spawnPoints, blockers);
                bool safeRoom = generatedNode.Role == GeneratedRoomRole.Start || generatedNode.Role == GeneratedRoomRole.Treasure;
                instance.Controller.BindRunState(state, safeRoom);
                ApplyEncounter(instance.Controller, generatedNode.Definition);
                if (generatedNode.Role == GeneratedRoomRole.Boss)
                    ConfigureBossDrop(instance, generatedNode.FloorNumber >= generatedGraph.Floors.Count);
                if (instance.RewardRoom != null)
                {
                    instance.RewardRoom.gameObject.SetActive(generatedNode.Role == GeneratedRoomRole.Treasure);
                    instance.RewardRoom.Configure(generatedNode.FloorNumber, generatedNode.RoomNumber,
                        runProgress, instance.RewardRoom.GetComponent<ItemDropSource>(), instance.Controller);
                    instance.RewardRoom.BindRunState(state);
                }
                instances.Add(generatedNode.RoomId, instance); nodes.Add(node);
            }

            foreach (GeneratedRoomNode generatedNode in floor.Nodes)
            {
                RoomPrefab instance = instances[generatedNode.RoomId];
                List<RoomDoorway> doorways = new();
                foreach (RoomDoorDirection direction in Enum.GetValues(typeof(RoomDoorDirection)))
                {
                    RoomDoorSlot slot = instance.FindSlot(direction);
                    if (slot == null) { error = $"{generatedNode.RoomId} is missing its {direction} slot."; DestroyFloor(nextRoot); return false; }
                    RoomNode destination = null; Transform entry = null;
                    if (generatedNode.TryGetConnection(direction, out GeneratedRoomConnection connection))
                    {
                        RoomPrefab destinationPrefab = instances[connection.DestinationRoomId];
                        destination = destinationPrefab.Node;
                        entry = destinationPrefab.FindSlot(GeneratedFloorGraph.Opposite(direction)).EntryPoint;
                        doorways.Add(slot.Doorway);
                    }
                    slot.Bind(graph, instance.Node, destination, entry, instance.Controller);
                }
                instance.Node.SetDoorways(doorways.ToArray());
                instance.Node.SetVisible(false);
            }

            RoomNode startingNode = instances[floor.StartingRoomId].Node;
            graph.Configure(nodes.ToArray(), startingNode, graph.Player, graph.RoomCamera, runProgress);
            if (!graph.TryInitializeStartingRoom(transitioningPlayer, out error))
            {
                DestroyFloor(nextRoot);
                return false;
            }

            ConfigureFloorCompletion(floor, instances[floor.BossRoomId], nextRoot);
            GameObject previousRoot = currentFloorRoot;
            currentFloorRoot = nextRoot;
            if (previousRoot != null) DestroyFloor(previousRoot);
            return true;
        }

        private void ConfigureFloorCompletion(GeneratedFloor floor, RoomPrefab bossRoom, GameObject floorRoot)
        {
            if (floor.FloorNumber >= generatedGraph.Floors.Count)
            {
                bossRoom.Controller.StateChanged += state =>
                {
                    if (state == RoomState.Cleared) runProgress?.RecordFinalBossCleared();
                };
                return;
            }

            GameObject portalObject = new($"Floor {floor.FloorNumber} Exit");
            portalObject.transform.SetParent(bossRoom.Node.ContentRoot.transform, false);
            portalObject.transform.localPosition = Vector3.up * 2.6f;
            BoxCollider2D collider = portalObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.2f, 1.2f);
            FloorAdvancePortal portal = portalObject.AddComponent<FloorAdvancePortal>();
            portal.Configure(this, bossRoom.Controller, floor.FloorNumber + 1);
        }

        private static Transform[] CopySpawnPoints(IReadOnlyList<Transform> source, int maximumCount)
        {
            Transform[] result = new Transform[Math.Min(source.Count, maximumCount)];
            for (int i = 0; i < result.Length; i++) result[i] = source[i];
            return result;
        }

        private static void ConfigureBossDrop(RoomPrefab instance, bool isFinalBoss)
        {
            ItemDropSource template = instance.RewardRoom != null
                ? instance.RewardRoom.GetComponent<ItemDropSource>() : null;
            instance.Controller.EnemySpawned += enemy =>
            {
                BossController boss = enemy != null ? enemy.GetComponent<BossController>() : null;
                if (boss == null || template == null) return;
                ItemDropSource source = enemy.GetComponent<ItemDropSource>();
                if (source == null) source = enemy.AddComponent<ItemDropSource>();
                ItemDefinition[] items = new ItemDefinition[template.ItemPool.Count];
                for (int i = 0; i < items.Length; i++) items[i] = template.ItemPool[i];
                source.Configure(template.PickupPrefab, items, enemy.transform, instance.Node.ContentRoot.transform);
                BossItemDrop drop = enemy.GetComponent<BossItemDrop>();
                if (drop == null) drop = enemy.AddComponent<BossItemDrop>();
                drop.Configure(isFinalBoss, source);
            };
        }

        private static void ApplyEncounter(RoomController controller, RoomDefinition definition)
        {
            GameObject[] prefabs = new GameObject[controller.SpawnPoints.Count];
            for (int i = 0; i < prefabs.Length; i++)
                prefabs[i] = definition.EncounterPrefabs[i % definition.EncounterPrefabs.Count];
            controller.ConfigurePreplacedEnemies(Array.Empty<Health>());
            controller.ConfigureEnemyPrefabs(prefabs);
        }

        private static void DestroyFloor(GameObject floorRoot)
        {
            if (floorRoot == null) return;
            floorRoot.SetActive(false);
            if (Application.isPlaying) Destroy(floorRoot); else DestroyImmediate(floorRoot);
        }

        private bool TryCreateBindings(
            GeneratedFloorGraph generatedGraph,
            out List<RoomBinding> bindings,
            out string error)
        {
            bindings = new List<RoomBinding>(generatedGraph.Nodes.Count);
            if (graph.Nodes.Count != generatedGraph.Nodes.Count)
            {
                error = $"Generated graph has {generatedGraph.Nodes.Count} rooms but the scene graph has {graph.Nodes.Count}.";
                return false;
            }

            Dictionary<string, RoomNode> sceneNodes = new(StringComparer.Ordinal);
            foreach (RoomNode sceneNode in graph.Nodes)
            {
                if (sceneNode == null || !sceneNodes.TryAdd(sceneNode.RoomId, sceneNode))
                {
                    error = $"Scene room ID '{sceneNode?.RoomId}' is empty or duplicated.";
                    return false;
                }
            }

            foreach (GeneratedRoomNode generatedNode in generatedGraph.Nodes)
            {
                if (!sceneNodes.TryGetValue(generatedNode.RoomId, out RoomNode sceneNode) ||
                    sceneNode.FloorNumber != generatedNode.FloorNumber ||
                    sceneNode.RoomNumber != generatedNode.RoomNumber)
                {
                    error = $"Generated room {generatedNode.RoomId} has no matching scene room instance.";
                    return false;
                }

                RoomController controller = sceneNode.ContentRoot != null
                    ? sceneNode.ContentRoot.GetComponentInChildren<RoomController>(true)
                    : null;
                if (controller == null)
                {
                    error = $"Scene room {sceneNode.RoomId} is missing RoomController.";
                    return false;
                }

                RewardRoom reward = sceneNode.ContentRoot.GetComponentInChildren<RewardRoom>(true);
                if (generatedNode.Definition.RoomType == RoomType.Reward && reward == null)
                {
                    error = $"Generated reward room {sceneNode.RoomId} has no verified RewardRoom instance.";
                    return false;
                }

                if (generatedNode.Definition.RoomType == RoomType.Boss)
                {
                    if (controller.PreplacedEnemies.Count != 1 ||
                        controller.PreplacedEnemies[0] == null ||
                        controller.PreplacedEnemies[0].GetComponent<BossController>() == null)
                    {
                        error = $"Generated boss room {sceneNode.RoomId} has no verified preplaced boss.";
                        return false;
                    }
                }
                else if (controller.SpawnPoints.Count == 0)
                {
                    error = $"Generated encounter room {sceneNode.RoomId} has no spawn points.";
                    return false;
                }

                if (!TryResolveDoorways(
                        sceneNode,
                        generatedNode,
                        sceneNodes,
                        out RoomDoorway[] orderedDoorways,
                        out error))
                {
                    return false;
                }

                bindings.Add(new RoomBinding(
                    generatedNode,
                    sceneNode,
                    controller,
                    reward,
                    orderedDoorways));
            }

            error = null;
            return true;
        }

        private static bool TryResolveDoorways(
            RoomNode sceneNode,
            GeneratedRoomNode generatedNode,
            IReadOnlyDictionary<string, RoomNode> sceneNodes,
            out RoomDoorway[] orderedDoorways,
            out string error)
        {
            orderedDoorways = null;
            Dictionary<string, RoomDoorway> existing = new(StringComparer.Ordinal);
            foreach (RoomDoorway doorway in sceneNode.Doorways)
            {
                if (doorway == null || doorway.Destination == null ||
                    !existing.TryAdd(doorway.Destination.RoomId, doorway))
                {
                    error = $"Scene room {sceneNode.RoomId} has a missing or duplicate doorway destination.";
                    return false;
                }
            }

            if (existing.Count != generatedNode.ConnectedRoomIds.Count)
            {
                error = $"Scene room {sceneNode.RoomId} has {existing.Count} doorways but generated room needs " +
                        $"{generatedNode.ConnectedRoomIds.Count}.";
                return false;
            }

            orderedDoorways = new RoomDoorway[generatedNode.ConnectedRoomIds.Count];
            for (int index = 0; index < generatedNode.ConnectedRoomIds.Count; index++)
            {
                string destinationId = generatedNode.ConnectedRoomIds[index];
                if (!sceneNodes.ContainsKey(destinationId) ||
                    !existing.TryGetValue(destinationId, out RoomDoorway doorway))
                {
                    error = $"Scene room {sceneNode.RoomId} is missing generated connection to {destinationId}.";
                    return false;
                }

                orderedDoorways[index] = doorway;
            }

            error = null;
            return true;
        }

        private static void ApplyDefinition(RoomBinding binding)
        {
            RoomDefinition definition = binding.GeneratedNode.Definition;
            binding.SceneNode.ApplyGeneratedDefinition(definition);

            if (binding.Reward != null)
            {
                binding.Reward.gameObject.SetActive(definition.RoomType == RoomType.Reward);
            }

            if (definition.RoomType == RoomType.Boss)
            {
                return;
            }

            GameObject[] encounterPrefabs = new GameObject[binding.Controller.SpawnPoints.Count];
            for (int index = 0; index < encounterPrefabs.Length; index++)
            {
                encounterPrefabs[index] =
                    definition.EncounterPrefabs[index % definition.EncounterPrefabs.Count];
            }

            binding.Controller.ConfigurePreplacedEnemies(Array.Empty<Health>());
            binding.Controller.ConfigureEnemyPrefabs(encounterPrefabs);
        }

        private void ApplyDoorways(RoomBinding binding)
        {
            foreach (RoomDoorway doorway in binding.OrderedDoorways)
            {
                doorway.Configure(
                    graph,
                    binding.SceneNode,
                    doorway.Destination,
                    doorway.DestinationEntryPoint,
                    doorway.RequiredClearedRoom,
                    doorway.AllowsOneWay);
            }

            binding.SceneNode.SetDoorways(binding.OrderedDoorways);
        }

        private sealed class RoomBinding
        {
            public RoomBinding(
                GeneratedRoomNode generatedNode,
                RoomNode sceneNode,
                RoomController controller,
                RewardRoom reward,
                RoomDoorway[] orderedDoorways)
            {
                GeneratedNode = generatedNode;
                SceneNode = sceneNode;
                Controller = controller;
                Reward = reward;
                OrderedDoorways = orderedDoorways;
            }

            public GeneratedRoomNode GeneratedNode { get; }
            public RoomNode SceneNode { get; }
            public RoomController Controller { get; }
            public RewardRoom Reward { get; }
            public RoomDoorway[] OrderedDoorways { get; }
        }
    }
}
