using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week7FloorTransitionVerification
    {
        private const string GraphRootName = "Week7 Fixed Room Graph";

        [MenuItem("Trickal Fan Game/Verify Phase E-7 Floor Transitions")]
        public static void Verify()
        {
            Week7FloorEncounterVerification.Verify();
            ValidateSceneTransitions();
            ValidateRuntimeTransition();
            Debug.Log(
                "Phase E-7 floor transition verification passed: two gated one-way floor links, no final-floor " +
                "exit, graph validation, player placement, room visibility, RunProgress, and repeat-entry rejection are valid.");
        }

        private static void ValidateSceneTransitions()
        {
            RoomGraphController graph = FindGraph(SceneManager.GetActiveScene());
            Assert(graph != null, "Missing the E-5 fixed room graph.");
            Assert(graph.TryValidateConfiguration(out string graphError), graphError);
            Dictionary<(int Floor, int Room), RoomNode> rooms = IndexRooms(graph);
            Assert(rooms.Count == 9, "Phase E-7 requires exactly nine indexed rooms.");

            int floorLinkCount = 0;
            foreach (RoomNode node in graph.Nodes)
            {
                foreach (RoomDoorway doorway in node.Doorways)
                {
                    if (doorway.Destination.FloorNumber == node.FloorNumber)
                    {
                        continue;
                    }

                    floorLinkCount++;
                    Assert(node.RoomNumber == 3 && node.FloorNumber <= 2,
                        $"Unexpected floor transition from {node.RoomId}.");
                    RoomNode expectedDestination = rooms[(node.FloorNumber + 1, 1)];
                    RoomController bossRoom = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                    Assert(doorway.Destination == expectedDestination &&
                           doorway.DestinationEntryPoint == expectedDestination.DefaultEntryPoint,
                        $"{node.RoomId} must lead to room 1 of the next floor.");
                    Assert(doorway.AllowsOneWay && doorway.RequiredClearedRoom == bossRoom,
                        $"{node.RoomId} floor transition must be one-way and gated by its boss-room clear state.");
                    Assert(!expectedDestination.HasConnectionTo(node),
                        $"{node.RoomId} floor transition must not create an unintended return path.");
                }
            }

            Assert(floorLinkCount == 2,
                "Phase E-7 must contain exactly the floor 1->2 and floor 2->3 transitions.");
        }

        private static void ValidateRuntimeTransition()
        {
            GameObject root = new("Phase E-7 Runtime Verification");
            GameObject playerObject = CreatePlayer(out PlayerMovement player);
            playerObject.transform.SetParent(root.transform);
            RunProgress progress = root.AddComponent<RunProgress>();
            RoomGraphController graph = root.AddComponent<RoomGraphController>();
            SetPrivateField(graph, "transitionCooldown", 0f);
            TestRoom source = CreateRoom(root.transform, "floor-01-room-03", 1, 3, Vector2.zero);
            TestRoom destination = CreateRoom(root.transform, "floor-02-room-01", 2, 1, Vector2.down * 12f);
            GameObject doorwayObject = new("Runtime Floor Transition");
            doorwayObject.transform.SetParent(source.Content.transform);
            doorwayObject.AddComponent<BoxCollider2D>();
            RoomDoorway doorway = doorwayObject.AddComponent<RoomDoorway>();
            doorway.Configure(graph, source.Node, destination.Node, destination.Entry, source.Controller, true);
            source.Node.SetDoorways(new[] { doorway });
            graph.Configure(
                new[] { source.Node, destination.Node },
                source.Node,
                player,
                null,
                progress);

            try
            {
                Assert(graph.TryValidateConfiguration(out string error), error);
                InvokeLifecycle(graph, "Start");
                Assert(!doorway.IsOpen && !doorway.TryEnter(player) && graph.CurrentNode == source.Node,
                    "The next-floor transition must reject entry before its boss room is cleared.");

                SetAutoProperty(source.Controller, "State", RoomState.Cleared);
                Assert(doorway.IsOpen && doorway.TryEnter(player),
                    "Clearing the boss room must allow entry into the next floor.");
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                Assert(graph.CurrentNode == destination.Node && destination.Node.IsVisible && !source.Node.IsVisible,
                    "Floor transition must leave only the destination room visible.");
                Assert((body.position - (Vector2)destination.Entry.position).sqrMagnitude < 0.0001f &&
                       progress.CurrentFloor == 2 && progress.CurrentRoom == 1,
                    "Floor transition must move the same player and record floor 2 room 1.");
                Assert(!doorway.TryEnter(player) && graph.CurrentNode == destination.Node,
                    "Repeating a source-floor transition after leaving it must be rejected.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static TestRoom CreateRoom(
            Transform parent,
            string roomId,
            int floor,
            int room,
            Vector2 center)
        {
            GameObject nodeObject = new(roomId);
            nodeObject.transform.SetParent(parent);
            RoomNode node = nodeObject.AddComponent<RoomNode>();
            GameObject content = new("Content");
            content.transform.SetParent(nodeObject.transform);
            Transform anchor = new GameObject("Camera Anchor").transform;
            anchor.SetParent(nodeObject.transform);
            anchor.position = center;
            Transform entry = new GameObject("Entry").transform;
            entry.SetParent(nodeObject.transform);
            entry.position = center + Vector2.left;
            GameObject encounter = new("Encounter");
            encounter.transform.SetParent(content.transform);
            encounter.AddComponent<BoxCollider2D>();
            RoomController controller = encounter.AddComponent<RoomController>();
            controller.Configure(floor, room, null, null, Array.Empty<Transform>(), Array.Empty<DoorController>());
            node.Configure(roomId, floor, room, content, anchor, entry, Array.Empty<RoomDoorway>());
            return new TestRoom(node, content, entry, controller);
        }

        private static GameObject CreatePlayer(out PlayerMovement movement)
        {
            GameObject player = new("Phase E-7 Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            Health health = player.AddComponent<Health>();
            player.AddComponent<PlayerStats>();
            movement = player.AddComponent<PlayerMovement>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(movement, "Awake");
            return player;
        }

        private static Dictionary<(int Floor, int Room), RoomNode> IndexRooms(RoomGraphController graph)
        {
            Dictionary<(int Floor, int Room), RoomNode> rooms = new();
            foreach (RoomNode node in graph.Nodes)
            {
                rooms.Add((node.FloorNumber, node.RoomNumber), node);
            }

            return rooms;
        }

        private static RoomGraphController FindGraph(Scene scene)
        {
            RoomGraphController match = null;
            foreach (RoomGraphController graph in Resources.FindObjectsOfTypeAll<RoomGraphController>())
            {
                if (graph == null || graph.gameObject.scene != scene || graph.name != GraphRootName)
                {
                    continue;
                }

                Assert(match == null, $"Found multiple {GraphRootName} objects.");
                match = graph;
            }

            return match;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }

            method.Invoke(component, null);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetPrivateField(target, $"<{propertyName}>k__BackingField", value);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private readonly struct TestRoom
        {
            public TestRoom(RoomNode node, GameObject content, Transform entry, RoomController controller)
            {
                Node = node;
                Content = content;
                Entry = entry;
                Controller = controller;
            }

            public RoomNode Node { get; }
            public GameObject Content { get; }
            public Transform Entry { get; }
            public RoomController Controller { get; }
        }
    }
}
