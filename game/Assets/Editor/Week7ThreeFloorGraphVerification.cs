using System;
using System.Collections.Generic;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week7ThreeFloorGraphVerification
    {
        private const string RootName = "Week7 Fixed Room Graph";
        private const int FloorCount = 3;
        private const int RoomsPerFloor = 3;

        [MenuItem("Trickal Fan Game/Verify Phase E-5 Three Floor Graph")]
        public static void Verify()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            RoomGraphController graph = FindGraph(activeScene);
            Assert(graph.TryValidateConfiguration(out string error), error);
            Assert(graph.Nodes.Count == FloorCount * RoomsPerFloor,
                "Phase E-5 fixed graph must contain exactly nine room nodes.");

            Dictionary<(int Floor, int Room), RoomNode> rooms = new();
            foreach (RoomNode node in graph.Nodes)
            {
                (int Floor, int Room) key = (node.FloorNumber, node.RoomNumber);
                Assert(key.Floor >= 1 && key.Floor <= FloorCount &&
                       key.Room >= 1 && key.Room <= RoomsPerFloor,
                    $"Room {node.RoomId} is outside the 3 floors x 3 rooms fixed layout.");
                Assert(rooms.TryAdd(key, node),
                    $"Duplicate floor/room key ({key.Floor}, {key.Room}) found in the fixed graph.");
                Assert(node.RoomId == $"floor-{key.Floor:00}-room-{key.Room:00}",
                    $"Room ({key.Floor}, {key.Room}) has unstable ID '{node.RoomId}'.");
                Assert(node.transform.parent != null && node.transform.parent.name == $"Floor {key.Floor}",
                    $"Room {node.RoomId} must be grouped under Floor {key.Floor}.");

                RoomController controller = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                Assert(controller != null, $"Room {node.RoomId} is missing its RoomController.");
                SerializedObject serializedController = new(controller);
                Assert(serializedController.FindProperty("floorNumber").intValue == key.Floor &&
                       serializedController.FindProperty("roomNumber").intValue == key.Room,
                    $"RoomController coordinates drifted from RoomNode {node.RoomId}.");
            }

            for (int floor = 1; floor <= FloorCount; floor++)
            {
                Transform floorRoot = graph.transform.Find($"Floor {floor}");
                Assert(floorRoot != null && floorRoot.childCount == RoomsPerFloor,
                    $"Floor {floor} must contain exactly three room-node children.");

                for (int room = 1; room <= RoomsPerFloor; room++)
                {
                    RoomNode node = rooms[(floor, room)];
                    int expectedConnectionCount = room == 2 ? 2 : 1;
                    int internalConnectionCount = 0;
                    foreach (RoomDoorway doorway in node.Doorways)
                    {
                        if (doorway != null && doorway.Destination.FloorNumber == floor)
                        {
                            internalConnectionCount++;
                        }
                    }

                    Assert(internalConnectionCount == expectedConnectionCount,
                        $"Room {node.RoomId} has an unexpected number of internal connections.");
                    Assert(room == 1 || node.HasConnectionTo(rooms[(floor, room - 1)]),
                        $"Room {node.RoomId} is missing its previous-room connection.");
                    Assert(room == RoomsPerFloor || node.HasConnectionTo(rooms[(floor, room + 1)]),
                        $"Room {node.RoomId} is missing its next-room connection.");
                }
            }

            SerializedObject serializedGraph = new(graph);
            RoomNode startingNode = serializedGraph.FindProperty("startingNode").objectReferenceValue as RoomNode;
            Assert(startingNode == rooms[(1, 1)], "The fixed Run must start at floor 1, room 1.");

            Week7RoomGraphVerification.Verify();
            Debug.Log(
                "Phase E-5 three-floor graph verification passed: nine stable room keys, three floor groups, " +
                "three reciprocal internal room chains and fixed-graph regressions are valid.");
        }

        private static RoomGraphController FindGraph(Scene activeScene)
        {
            RoomGraphController match = null;
            foreach (RoomGraphController graph in Resources.FindObjectsOfTypeAll<RoomGraphController>())
            {
                if (graph == null || graph.gameObject.scene != activeScene || graph.name != RootName)
                {
                    continue;
                }

                Assert(match == null, $"More than one {RootName} exists. Re-run the E-5 Setup once.");
                match = graph;
            }

            Assert(match != null, $"Missing {RootName}. Run Setup Phase E-5 Three Floor Graph first.");
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
