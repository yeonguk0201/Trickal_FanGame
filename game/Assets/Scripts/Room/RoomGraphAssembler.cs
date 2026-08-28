using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RoomGraphAssembler : MonoBehaviour
    {
        [SerializeField] private FloorGenerator generator;
        [SerializeField] private RoomGraphController graph;

        public FloorGenerator Generator => generator;
        public RoomGraphController Graph => graph;

        public void Configure(FloorGenerator configuredGenerator, RoomGraphController configuredGraph)
        {
            generator = configuredGenerator;
            graph = configuredGraph;
        }

        private void Awake()
        {
            if (!TryApplyGeneratedGraph(out string error))
            {
                Debug.LogError($"{name}: Failed to assemble generated room graph. {error}", this);
                enabled = false;
            }
        }

        public bool TryApplyGeneratedGraph(out string error)
        {
            if (generator == null || graph == null)
            {
                error = "FloorGenerator and RoomGraphController references are required.";
                return false;
            }

            if (!graph.TryValidateConfiguration(out error))
            {
                error = $"RoomGraphController is invalid before generated binding. {error}";
                return false;
            }

            if (!generator.TryGenerate(out GeneratedFloorGraph generatedGraph, out error))
            {
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
