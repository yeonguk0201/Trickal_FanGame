using System;
using System.Collections.Generic;
using System.Text;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week8RandomRoomBindingVerification
    {
        private const string GraphRootName = "Week7 Fixed Room Graph";

        [MenuItem("Trickal Fan Game/Verify Phase F-2 Generated Room Graph Binding")]
        public static void Verify()
        {
            Week8RandomRoomVerification.Verify();
            RoomGraphAssembler assembler = FindSceneObject<RoomGraphAssembler>(
                SceneManager.GetActiveScene(),
                Week8RandomRoomSetup.GeneratorObjectName);
            RoomGraphController graph = FindSceneObject<RoomGraphController>(
                SceneManager.GetActiveScene(),
                GraphRootName);
            Assert(assembler != null && graph != null &&
                   assembler.Generator != null && assembler.Graph == graph,
                "Phase F-2 assembler is missing its generator or RoomGraphController binding.");
            Assert(assembler.TryApplyGeneratedGraph(out string error), error);
            Assert(graph.TryValidateConfiguration(out error), error);
            Assert(assembler.Generator.TryGenerate(out GeneratedFloorGraph generated, out error), error);

            string before = BuildAppliedSignature(graph);
            ValidateAppliedRooms(generated, graph);
            Assert(assembler.TryApplyGeneratedGraph(out error), error);
            string after = BuildAppliedSignature(graph);
            Assert(before == after,
                "Applying the same generated graph twice must preserve room, encounter, reward, and doorway bindings.");
            Assert(assembler.gameObject.GetComponents<RoomGraphAssembler>().Length == 1,
                "Phase F-2 Setup must not create duplicate RoomGraphAssembler components.");
            ValidateCountMismatchFails(assembler.Generator);

            Debug.Log(
                "Phase F-2 generated room graph binding verification passed: generated definitions, repeated " +
                "encounter patterns, reward activation, exact doorway order, graph validation, idempotent reapply, " +
                "and scene-count mismatch failure are valid.");
        }

        private static void ValidateAppliedRooms(
            GeneratedFloorGraph generated,
            RoomGraphController graph)
        {
            Dictionary<string, RoomNode> sceneNodes = new(StringComparer.Ordinal);
            foreach (RoomNode node in graph.Nodes)
            {
                Assert(node != null && sceneNodes.TryAdd(node.RoomId, node),
                    $"Scene room ID '{node?.RoomId}' is missing or duplicated.");
            }

            Assert(sceneNodes.Count == generated.Nodes.Count,
                "Generated and applied scene graph room counts must match.");
            foreach (GeneratedRoomNode generatedNode in generated.Nodes)
            {
                Assert(sceneNodes.TryGetValue(generatedNode.RoomId, out RoomNode sceneNode),
                    $"Generated room {generatedNode.RoomId} has no scene instance.");
                Assert(sceneNode.Definition == generatedNode.Definition,
                    $"Scene room {sceneNode.RoomId} has the wrong generated definition.");
                RoomController controller = sceneNode.ContentRoot.GetComponentInChildren<RoomController>(true);
                RewardRoom reward = sceneNode.ContentRoot.GetComponentInChildren<RewardRoom>(true);
                Assert(controller != null, $"Scene room {sceneNode.RoomId} is missing RoomController.");

                if (generatedNode.Definition.RoomType == RoomType.Boss)
                {
                    Assert(controller.PreplacedEnemies.Count == 1 &&
                           controller.PreplacedEnemies[0] != null &&
                           controller.PreplacedEnemies[0].GetComponent<BossController>() != null,
                        $"Generated boss room {sceneNode.RoomId} lost its verified boss instance.");
                }
                else
                {
                    Assert(controller.PreplacedEnemies.Count == 0 &&
                           controller.EnemyPrefabs.Count == controller.SpawnPoints.Count,
                        $"Generated encounter room {sceneNode.RoomId} has an invalid spawn binding.");
                    for (int index = 0; index < controller.EnemyPrefabs.Count; index++)
                    {
                        GameObject expected = generatedNode.Definition.EncounterPrefabs[
                            index % generatedNode.Definition.EncounterPrefabs.Count];
                        Assert(controller.EnemyPrefabs[index] == expected,
                            $"Generated encounter room {sceneNode.RoomId} has the wrong prefab at spawn {index + 1}.");
                    }
                }

                if (reward != null)
                {
                    Assert(reward.gameObject.activeSelf ==
                           (generatedNode.Definition.RoomType == RoomType.Reward),
                        $"Generated reward activation drifted in {sceneNode.RoomId}.");
                }

                Assert(sceneNode.Doorways.Length == generatedNode.ConnectedRoomIds.Count,
                    $"Scene room {sceneNode.RoomId} doorway count differs from the generated graph.");
                for (int index = 0; index < sceneNode.Doorways.Length; index++)
                {
                    RoomDoorway doorway = sceneNode.Doorways[index];
                    Assert(doorway != null && doorway.Graph == graph && doorway.Source == sceneNode &&
                           doorway.Destination != null &&
                           doorway.Destination.RoomId == generatedNode.ConnectedRoomIds[index],
                        $"Scene room {sceneNode.RoomId} doorway {index + 1} is not bound to the generated connection.");
                }
            }

            SerializedObject serializedGraph = new(graph);
            RoomNode startingNode = serializedGraph.FindProperty("startingNode").objectReferenceValue as RoomNode;
            Assert(startingNode != null && startingNode.RoomId == generated.StartingRoomId,
                "RoomGraphController starting node differs from the generated graph.");
        }

        private static void ValidateCountMismatchFails(FloorGenerator generator)
        {
            GameObject holder = new("Phase F-2 Invalid Binding Verification");
            try
            {
                RoomGraphController incompleteGraph = holder.AddComponent<RoomGraphController>();
                GameObject nodeObject = new("Only Scene Room");
                nodeObject.transform.SetParent(holder.transform);
                RoomNode onlyNode = nodeObject.AddComponent<RoomNode>();
                GameObject content = new("Content");
                content.transform.SetParent(nodeObject.transform);
                Transform anchor = new GameObject("Camera Anchor").transform;
                anchor.SetParent(nodeObject.transform);
                Transform entry = new GameObject("Entry").transform;
                entry.SetParent(nodeObject.transform);
                onlyNode.Configure(
                    "floor-01-room-01",
                    1,
                    1,
                    content,
                    anchor,
                    entry,
                    Array.Empty<RoomDoorway>());
                incompleteGraph.Configure(new[] { onlyNode }, onlyNode, null, null, null);
                RoomGraphAssembler invalidAssembler = holder.AddComponent<RoomGraphAssembler>();
                invalidAssembler.Configure(generator, incompleteGraph);
                Assert(!invalidAssembler.TryApplyGeneratedGraph(out string error) &&
                       error.Contains("scene graph has 1", StringComparison.Ordinal),
                    "A generated/scene room-count mismatch must fail explicitly before applying changes.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static string BuildAppliedSignature(RoomGraphController graph)
        {
            StringBuilder signature = new();
            foreach (RoomNode node in graph.Nodes)
            {
                signature.Append(node.RoomId).Append(':')
                    .Append(node.Definition != null ? node.Definition.RoomDefinitionId : "missing")
                    .Append(':');
                RoomController controller = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                foreach (GameObject enemyPrefab in controller.EnemyPrefabs)
                {
                    signature.Append(enemyPrefab != null ? enemyPrefab.name : "missing").Append(',');
                }

                RewardRoom reward = node.ContentRoot.GetComponentInChildren<RewardRoom>(true);
                signature.Append(':').Append(reward != null && reward.gameObject.activeSelf).Append(':');
                foreach (RoomDoorway doorway in node.Doorways)
                {
                    signature.Append(doorway.GetInstanceID()).Append('>')
                        .Append(doorway.Destination.RoomId).Append(',');
                }

                signature.Append('|');
            }

            return signature.ToString();
        }

        private static T FindSceneObject<T>(Scene scene, string requiredName) where T : Component
        {
            T match = null;
            foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
            {
                if (candidate == null || candidate.gameObject.scene != scene ||
                    candidate.gameObject.name != requiredName)
                {
                    continue;
                }

                Assert(match == null, $"Found multiple scene objects named {requiredName} with {typeof(T).Name}.");
                match = candidate;
            }

            return match;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
