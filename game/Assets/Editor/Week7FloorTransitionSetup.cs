using System;
using System.Collections.Generic;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week7FloorTransitionSetup
    {
        private const string GraphRootName = "Week7 Fixed Room Graph";
        private const string TransitionName = "Next Floor Transition";

        [MenuItem("Trickal Fan Game/Setup Phase E-7 Floor Transitions")]
        public static void Setup()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            RoomGraphController graph = FindGraph(activeScene);
            if (graph == null)
            {
                Debug.LogError("Phase E-7 requires the completed E-5 fixed room graph.");
                return;
            }

            if (!graph.TryValidateConfiguration(out string graphError))
            {
                Debug.LogError($"Phase E-7 found an invalid room graph. {graphError}");
                return;
            }

            Dictionary<(int Floor, int Room), RoomNode> rooms = IndexRooms(graph);
            if (!TryValidatePrerequisites(rooms, out string prerequisiteError))
            {
                Debug.LogError(prerequisiteError, graph);
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Phase E-7 Floor Transitions");

            for (int floor = 1; floor <= 2; floor++)
            {
                RoomNode source = rooms[(floor, 3)];
                RoomNode destination = rooms[(floor + 1, 1)];
                RoomController requiredRoom = source.ContentRoot.GetComponentInChildren<RoomController>(true);
                List<RoomDoorway> preservedDoorways = new();
                foreach (RoomDoorway doorway in source.Doorways)
                {
                    if (doorway != null && doorway.Destination != null &&
                        doorway.Destination.FloorNumber == source.FloorNumber)
                    {
                        preservedDoorways.Add(doorway);
                    }
                }

                foreach (RoomDoorway doorway in source.ContentRoot.GetComponentsInChildren<RoomDoorway>(true))
                {
                    if (doorway != null &&
                        (doorway.name == TransitionName || doorway.Destination == null ||
                         doorway.Destination.FloorNumber != source.FloorNumber))
                    {
                        Undo.DestroyObjectImmediate(doorway.gameObject);
                    }
                }

                RoomDoorway floorTransition = CreateFloorTransition(
                    source,
                    destination,
                    requiredRoom,
                    graph);
                preservedDoorways.Add(floorTransition);
                Undo.RecordObject(source, "Assign next-floor transition");
                source.SetDoorways(preservedDoorways.ToArray());
                EditorUtility.SetDirty(source);
            }

            if (!graph.TryValidateConfiguration(out graphError))
            {
                Debug.LogError($"Phase E-7 produced an invalid room graph. {graphError}", graph);
                Undo.RevertAllDownToGroup(undoGroup);
                return;
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Selection.activeGameObject = graph.gameObject;
            Debug.Log(
                "Phase E-7 floor transitions ready: clearing the floor 1 or floor 2 boss room opens a one-way " +
                "transition to room 1 of the next floor. Floor 3 remains connected only to final Run clear.",
                graph);
        }

        private static RoomDoorway CreateFloorTransition(
            RoomNode source,
            RoomNode destination,
            RoomController requiredRoom,
            RoomGraphController graph)
        {
            GameObject doorwayObject = new(TransitionName);
            Undo.RegisterCreatedObjectUndo(doorwayObject, $"Create floor {source.FloorNumber} exit");
            doorwayObject.transform.SetParent(source.ContentRoot.transform);
            doorwayObject.transform.position = source.CameraAnchor.position + Vector3.right * 5.15f;
            BoxCollider2D trigger = Undo.AddComponent<BoxCollider2D>(doorwayObject);
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.8f, 2.5f);
            RoomDoorway doorway = Undo.AddComponent<RoomDoorway>(doorwayObject);
            doorway.Configure(
                graph,
                source,
                destination,
                destination.DefaultEntryPoint,
                requiredRoom,
                true);
            return doorway;
        }

        private static bool TryValidatePrerequisites(
            IReadOnlyDictionary<(int Floor, int Room), RoomNode> rooms,
            out string error)
        {
            if (rooms.Count != 9)
            {
                error = "Phase E-7 requires exactly three floors with three rooms each.";
                return false;
            }

            for (int floor = 1; floor <= 3; floor++)
            {
                if (!rooms.TryGetValue((floor, 1), out RoomNode firstRoom) ||
                    !rooms.TryGetValue((floor, 3), out RoomNode bossRoom) ||
                    firstRoom.DefaultEntryPoint == null)
                {
                    error = $"Phase E-7 is missing floor {floor} room 1 or room 3.";
                    return false;
                }

                RoomController controller = bossRoom.ContentRoot.GetComponentInChildren<RoomController>(true);
                if (controller == null || controller.PreplacedEnemies.Count != 1 ||
                    controller.PreplacedEnemies[0] == null ||
                    controller.PreplacedEnemies[0].GetComponent<BossController>() == null)
                {
                    error = $"Phase E-7 requires the E-6 boss encounter in {bossRoom.RoomId}.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static Dictionary<(int Floor, int Room), RoomNode> IndexRooms(RoomGraphController graph)
        {
            Dictionary<(int Floor, int Room), RoomNode> rooms = new();
            foreach (RoomNode node in graph.Nodes)
            {
                if (node != null)
                {
                    rooms[(node.FloorNumber, node.RoomNumber)] = node;
                }
            }

            return rooms;
        }

        private static RoomGraphController FindGraph(Scene activeScene)
        {
            RoomGraphController match = null;
            foreach (RoomGraphController graph in Resources.FindObjectsOfTypeAll<RoomGraphController>())
            {
                if (graph == null || graph.gameObject.scene != activeScene || graph.name != GraphRootName)
                {
                    continue;
                }

                if (match != null)
                {
                    Debug.LogError($"Phase E-7 found multiple {GraphRootName} objects.");
                    return null;
                }

                match = graph;
            }

            return match;
        }
    }
}
