using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week19Door1Verification
    {
        private const float PlayerRadius = 0.5f;
        private const float MinimumWidthRatio = 0.6f;
        private const float MaximumWidthRatio = 0.7f;
        private const float ExpectedInvulnerability = 0.75f;
        private const float ExpectedReturnBlock = 0.4f;

        [MenuItem("Trickal Fan Game/Week 19/Setup and Verify Door-1 Doorway Passage")]
        public static void SetupAndVerifyBatch()
        {
            Dictionary<string, string> guids = new(StringComparer.Ordinal);
            foreach (string path in Week19Door1Setup.FindRoomPrefabPaths())
            {
                guids[path] = AssetDatabase.AssetPathToGUID(path);
            }

            Week19Door1Setup.Setup();
            Week19Door1Setup.Setup();
            Assert(guids.Count > 0, "Door-1 found no room Prefabs to configure.");
            foreach (KeyValuePair<string, string> entry in guids)
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Door-1 setup changed or lost the GUID of '{entry.Key}'.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 19/Verify Door-1 Doorway Passage")]
        public static void Verify()
        {
            int triggers = ValidateRoomPrefabs();
            ValidateCenterRule();
            ValidateTransitionProtection();
            Debug.Log($"Door-1 verification passed: {triggers} room Prefab transition triggers are " +
                      $"{RoomLayout.TransitionLength} wide ({RoomLayout.TransitionLength / RoomLayout.DoorOpeningLength:P1} " +
                      $"of the {RoomLayout.DoorOpeningLength} opening) and still reachable against the open door " +
                      "barrier, only a player center inside that width passes while edge contact does not, " +
                      $"passage grants {ExpectedInvulnerability}s invulnerability, and the entered doorway is " +
                      $"blocked for {ExpectedReturnBlock}s while other doorways stay usable.");
        }

        private static int ValidateRoomPrefabs()
        {
            float ratio = RoomLayout.TransitionLength / RoomLayout.DoorOpeningLength;
            Assert(ratio >= MinimumWidthRatio && ratio <= MaximumWidthRatio,
                $"Transition width ratio {ratio:0.###} must stay within 60-70% of the door opening.");

            int triggers = 0;
            IReadOnlyList<string> paths = Week19Door1Setup.FindRoomPrefabPaths();
            Assert(paths.Count > 0, "Door-1 found no room Prefabs to verify.");
            foreach (string path in paths)
            {
                RoomPrefab prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<RoomPrefab>();
                Assert(prefab.DoorSlots.Length == 4, $"'{path}' must keep four door slots.");
                foreach (RoomDoorSlot slot in prefab.DoorSlots)
                {
                    BoxCollider2D trigger = slot.Doorway != null ? slot.Doorway.GetComponent<BoxCollider2D>() : null;
                    Assert(trigger != null && trigger.isTrigger && trigger.offset == Vector2.zero &&
                           trigger.size == Week19Door1Setup.TransitionSize(slot.Direction) &&
                           slot.Doorway.transform.localPosition == Vector3.zero,
                        $"'{path}' {slot.Direction} transition trigger must be the Door-1 size at the slot origin.");
                    ValidateBarrierContact(path, slot, trigger);
                    triggers++;
                }
            }

            return triggers;
        }

        // The open door barrier stays solid, so the player can only reach the trigger by pressing its face.
        private static void ValidateBarrierContact(string path, RoomDoorSlot slot, BoxCollider2D trigger)
        {
            Assert(slot.Blocker != null, $"'{path}' {slot.Direction} door slot has no barrier.");
            Vector2 normal = RoomLayout.Direction(slot.Direction);
            bool side = RoomLayout.IsSideDoor(slot.Direction);
            Transform barrier = slot.Blocker.transform;
            float barrierDepth = Vector2.Dot(barrier.localPosition, normal);
            float barrierHalfThickness = (side ? barrier.localScale.x : barrier.localScale.y) * 0.5f;
            float barrierFace = barrierDepth - barrierHalfThickness;
            float triggerInnerEdge = -(side ? trigger.size.x : trigger.size.y) * 0.5f;
            float triggerOuterEdge = -triggerInnerEdge;
            Assert(barrierFace > triggerInnerEdge && barrierFace < triggerOuterEdge,
                $"'{path}' {slot.Direction} barrier face must sit inside the trigger so pressing the door overlaps it.");
            float entryDepth = -Vector2.Dot(slot.EntryPoint.localPosition, normal);
            Assert(entryDepth - PlayerRadius > -triggerInnerEdge,
                $"'{path}' {slot.Direction} entry point must not start the player inside the transition trigger.");
        }

        private static void ValidateCenterRule()
        {
            GameObject root = new("Door-1 Center Verification");
            try
            {
                RoomDoorway side = CreateDoorway(root.transform, RoomDoorDirection.Right, new Vector2(7.55f, 0f));
                RoomDoorway vertical = CreateDoorway(root.transform, RoomDoorDirection.Up, new Vector2(0f, 3.8f));
                float half = RoomLayout.TransitionLength * 0.5f;
                Assert(side.ContainsPassageCenter(new Vector2(7.1f, 0f)) &&
                       side.ContainsPassageCenter(new Vector2(7.1f, half - 0.01f)) &&
                       side.ContainsPassageCenter(new Vector2(7.1f, -half + 0.01f)),
                    "A player center inside the side door width must pass.");
                Assert(!side.ContainsPassageCenter(new Vector2(7.1f, half + 0.01f)) &&
                       !side.ContainsPassageCenter(new Vector2(7.1f, -(half + 0.01f))) &&
                       !side.ContainsPassageCenter(new Vector2(7.1f, RoomLayout.DoorOpeningLength * 0.5f)),
                    "A player center outside the side door width must not pass even while the body edge overlaps.");
                Assert(vertical.ContainsPassageCenter(new Vector2(half - 0.01f, 3.4f)) &&
                       !vertical.ContainsPassageCenter(new Vector2(half + 0.01f, 3.4f)),
                    "The center rule must use the horizontal axis for top and bottom doors.");
                // The body is mostly inside the frame at the widest passing offset.
                float opening = RoomLayout.DoorOpeningLength * 0.5f;
                float insideShare = (opening - (half - PlayerRadius)) / (PlayerRadius * 2f);
                Assert(insideShare >= 0.5f, "At the widest passing offset at least half the body must be in the frame.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateTransitionProtection()
        {
            GameObject root = new("Door-1 Graph Verification");
            GameObject playerObject = CreatePlayer(out PlayerMovement player, out Health health);
            GameObject cameraObject = new("Door-1 Verification Camera");
            try
            {
                RoomCameraController roomCamera = cameraObject.AddComponent<RoomCameraController>();
                RunProgress progress = root.AddComponent<RunProgress>();
                RoomGraphController graph = root.AddComponent<RoomGraphController>();
                Assert(Mathf.Approximately(graph.DoorwayInvulnerabilityDuration, ExpectedInvulnerability) &&
                       Mathf.Approximately(graph.ReturnDoorwayBlockDuration, ExpectedReturnBlock),
                    $"Room graph defaults must be {ExpectedInvulnerability}s invulnerability and " +
                    $"{ExpectedReturnBlock}s return block.");
                SetPrivateField(graph, "transitionCooldown", 0f);

                TestRoom first = CreateRoom(root.transform, "door1-a", 1, Vector2.zero);
                TestRoom second = CreateRoom(root.transform, "door1-b", 2, Vector2.right * 20f);
                TestRoom third = CreateRoom(root.transform, "door1-c", 3, Vector2.right * 40f);
                RoomDoorway firstToSecond = CreateGraphDoorway(first, second, graph, new Vector2(7.55f, 0f));
                RoomDoorway secondToFirst = CreateGraphDoorway(second, first, graph, new Vector2(12.45f, 0f));
                RoomDoorway secondToThird = CreateGraphDoorway(second, third, graph, new Vector2(27.55f, 0f));
                RoomDoorway thirdToSecond = CreateGraphDoorway(third, second, graph, new Vector2(32.45f, 0f));
                first.Node.SetDoorways(new[] { firstToSecond });
                second.Node.SetDoorways(new[] { secondToFirst, secondToThird });
                third.Node.SetDoorways(new[] { thirdToSecond });
                RoomNode[] nodes = { first.Node, second.Node, third.Node };
                graph.Configure(nodes, first.Node, player, roomCamera, progress);
                Assert(graph.TryValidateConfiguration(out string error), error);
                InvokeLifecycle(graph, "Start");
                Assert(graph.CurrentNode == first.Node && !health.IsInvulnerable,
                    "The graph must start in the first room without invulnerability.");

                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                SetPlayerPosition(player, body, new Vector2(7.1f, RoomLayout.TransitionLength * 0.5f + 0.2f));
                Assert(!firstToSecond.TryEnterFromContact(player) && graph.CurrentNode == first.Node,
                    "Contact with the center outside the door width must not transition.");
                SetPlayerPosition(player, body, new Vector2(7.1f, 0.3f));
                SetPrivateField(player, "movement", Vector2.up);
                Assert(!firstToSecond.TryEnterFromContact(player), "Sliding along a wall must not enter a doorway.");
                SetPrivateField(player, "movement", Vector2.zero);
                Assert(!firstToSecond.TryEnterFromContact(player), "Standing in a trigger must not enter a doorway.");
                SetPrivateField(player, "movement", Vector2.right);
                Assert(firstToSecond.TryEnterFromContact(player) && graph.CurrentNode == second.Node,
                    "Contact with the center inside the door width must transition.");

                DamageInvulnerability invulnerability = player.GetComponent<DamageInvulnerability>();
                Assert(health.IsInvulnerable && invulnerability != null &&
                       invulnerability.HitInvulnerableUntil >= Time.time + ExpectedInvulnerability - 0.0001f,
                    $"Passing a doorway must grant {ExpectedInvulnerability}s invulnerability.");
                Assert(graph.IsReturnDoorwayBlocked(first.Node) && !graph.IsReturnDoorwayBlocked(third.Node),
                    "Only the entered doorway must be blocked right after passing.");
                Assert(!graph.TryTransition(second.Node, first.Node, first.Entry, player) &&
                       graph.CurrentNode == second.Node,
                    "The entered doorway must not transition back during the return block.");
                Assert(graph.TryTransition(second.Node, third.Node, third.Entry, player) &&
                       graph.CurrentNode == third.Node,
                    "A different doorway must remain usable during the return block.");
                Assert(graph.IsReturnDoorwayBlocked(second.Node) && !graph.IsReturnDoorwayBlocked(first.Node),
                    "The return block must follow the most recently entered doorway.");

                // Editor time does not advance inside the batch call, so expire the block explicitly.
                SetPrivateField(graph, "returnBlockedUntil", Time.unscaledTime);
                Assert(!graph.IsReturnDoorwayBlocked(second.Node) &&
                       graph.TryTransition(third.Node, second.Node, second.Entry, player) &&
                       graph.CurrentNode == second.Node,
                    "The entered doorway must open again once the return block expires.");
                SetPrivateField(graph, "returnBlockedUntil", Time.unscaledTime);

                Assert(graph.TryTransition(second.Node, first.Node, first.Entry, player) &&
                       graph.IsReturnDoorwayBlocked(second.Node) &&
                       graph.TryInitializeStartingRoom(player, out error) &&
                       !graph.IsReturnDoorwayBlocked(second.Node),
                    "Reinitializing the floor must clear the return block.");

                health.SetInvulnerable(true);
                Assert(health.IsInvulnerable, "Explicit invulnerability must remain independent of passage.");
                health.SetInvulnerable(false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RoomDoorway CreateDoorway(Transform parent, RoomDoorDirection direction, Vector2 position)
        {
            GameObject doorwayObject = new($"{direction} Door-1 Transition");
            doorwayObject.transform.SetParent(parent);
            doorwayObject.transform.position = position;
            BoxCollider2D trigger = doorwayObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = Week19Door1Setup.TransitionSize(direction);
            return doorwayObject.AddComponent<RoomDoorway>();
        }

        private static RoomDoorway CreateGraphDoorway(
            TestRoom source, TestRoom destination, RoomGraphController graph, Vector2 position)
        {
            RoomDoorway doorway = CreateDoorway(source.Content.transform, RoomDoorDirection.Right, position);
            doorway.Configure(graph, source.Node, destination.Node, destination.Entry);
            return doorway;
        }

        private static TestRoom CreateRoom(Transform parent, string roomId, int roomNumber, Vector2 center)
        {
            GameObject nodeObject = new(roomId);
            nodeObject.transform.SetParent(parent);
            RoomNode node = nodeObject.AddComponent<RoomNode>();
            GameObject content = new("Content");
            content.transform.SetParent(nodeObject.transform);
            Transform cameraAnchor = new GameObject("Camera Anchor").transform;
            cameraAnchor.SetParent(nodeObject.transform);
            cameraAnchor.position = center;
            Transform entry = new GameObject("Entry").transform;
            entry.SetParent(nodeObject.transform);
            entry.position = center + Vector2.left;

            GameObject encounterObject = new("Encounter");
            encounterObject.transform.SetParent(content.transform);
            encounterObject.AddComponent<BoxCollider2D>();
            RoomController controller = encounterObject.AddComponent<RoomController>();
            controller.Configure(1, roomNumber, null, null, null, null);

            GameObject rewardObject = new("Reward State");
            rewardObject.transform.SetParent(content.transform);
            rewardObject.AddComponent<BoxCollider2D>();
            rewardObject.AddComponent<ItemDropSource>();
            rewardObject.AddComponent<RewardRoom>();

            node.Configure(roomId, 1, roomNumber, content, cameraAnchor, entry, Array.Empty<RoomDoorway>());
            return new TestRoom(node, content, entry);
        }

        private static GameObject CreatePlayer(out PlayerMovement movement, out Health health)
        {
            GameObject player = new("Door-1 Verification Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>().radius = PlayerRadius;
            health = player.AddComponent<Health>();
            player.AddComponent<PlayerStats>();
            movement = player.AddComponent<PlayerMovement>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(movement, "Awake");
            return player;
        }

        private static void SetPlayerPosition(PlayerMovement player, Rigidbody2D body, Vector2 position)
        {
            body.position = position;
            player.transform.position = position;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(field != null, $"{target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Door-1 verification failed: {message}");
            }
        }

        private readonly struct TestRoom
        {
            public TestRoom(RoomNode node, GameObject content, Transform entry)
            {
                Node = node;
                Content = content;
                Entry = entry;
            }

            public RoomNode Node { get; }
            public GameObject Content { get; }
            public Transform Entry { get; }
        }
    }
}
