using System;
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
    public static class Week7RoomGraphVerification
    {
        [MenuItem("Trickal Fan Game/Verify Fixed Room Graph")]
        public static void Verify()
        {
            GameObject root = new("Room Graph Verification");
            GameObject playerObject = CreatePlayer(out PlayerMovement player, out Health playerHealth);
            GameObject cameraObject = new("Room Graph Verification Camera");
            RoomCameraController roomCamera = cameraObject.AddComponent<RoomCameraController>();
            RunProgress progress = root.AddComponent<RunProgress>();
            RoomGraphController graph = root.AddComponent<RoomGraphController>();
            SetPrivateField(graph, "transitionCooldown", 0f);

            TestRoom first = CreateRoom(root.transform, graph, "room-a", 1, Vector2.zero);
            TestRoom second = CreateRoom(root.transform, graph, "room-b", 2, Vector2.right * 20f);
            TestRoom third = CreateRoom(root.transform, graph, "room-c", 3, Vector2.right * 40f);
            RoomDoorway firstToSecond = CreateDoorway(first, second, graph);
            RoomDoorway secondToFirst = CreateDoorway(second, first, graph);
            RoomDoorway secondToThird = CreateDoorway(second, third, graph);
            RoomDoorway thirdToSecond = CreateDoorway(third, second, graph);
            first.Node.SetDoorways(new[] { firstToSecond });
            second.Node.SetDoorways(new[] { secondToFirst, secondToThird });
            third.Node.SetDoorways(new[] { thirdToSecond });
            RoomNode[] nodes = { first.Node, second.Node, third.Node };
            graph.Configure(nodes, first.Node, player, roomCamera, progress);

            try
            {
                Assert(graph.TryValidateConfiguration(out string error), error);
                InvokeLifecycle(graph, "Start");
                Assert(graph.CurrentNode == first.Node && CountVisible(nodes) == 1 && first.Node.IsVisible,
                    "Graph start must leave exactly the starting room visible.");
                Assert(first.Node.HasBeenVisited && progress.CurrentRoom == 1,
                    "Starting a graph must record the first room as visited progress.");

                first.Controller.BeginCombat(playerHealth);
                Assert(first.Controller.State == RoomState.Cleared,
                    "The empty verification encounter should reach Cleared before leaving.");
                SetAutoProperty(first.RewardRoom, "HasRewarded", true);

                DoorController portalBarrier = CreatePortalBarrier(root.transform, out Collider2D portalCollider);
                portalBarrier.SetLocked(true);
                Assert(portalBarrier.IsLocked && portalBarrier.IsPortalBarrierActive,
                    "A locked portal door must remain a solid barrier.");
                portalBarrier.SetLocked(false);
                Assert(!portalBarrier.IsLocked && portalBarrier.IsPortalBarrierActive,
                    "An open portal door must stay physically solid while its transition trigger handles travel.");
                DoorController legacyDoor = CreateLegacyDoor(root.transform, out Collider2D legacyCollider);
                legacyDoor.SetLocked(true);
                legacyDoor.SetLocked(false);
                Assert(!legacyDoor.IsPortalBarrier && !legacyCollider.enabled,
                    "Legacy non-portal doors must retain their existing passable-when-open behavior.");

                Projectile boundaryBasic = CreateBasicProjectile(playerHealth);
                InvokePrivate(boundaryBasic, "OnTriggerEnter2D", portalCollider);
                Assert(boundaryBasic == null, "A basic projectile must be consumed by the portal barrier.");
                HomingSkillProjectile untargetedHoming = CreateHomingProjectile(playerHealth);
                InvokePrivate(untargetedHoming, "OnTriggerEnter2D", portalCollider);
                Assert(untargetedHoming == null,
                    "An untargeted lower-grade projectile must be consumed by the portal barrier.");
                GameObject targetObject = CreateEnemyTarget(root.transform, out Health assignedTarget);
                HomingSkillProjectile targetedHoming = CreateHomingProjectile(playerHealth, assignedTarget);
                InvokePrivate(targetedHoming, "OnTriggerEnter2D", portalCollider);
                Assert(targetedHoming != null && targetedHoming.HasAssignedTarget && !targetedHoming.DidExplode,
                    "A targeted lower-grade projectile must pass through the portal barrier without exploding.");
                targetedHoming.StopAtBoundary();
                UnityEngine.Object.DestroyImmediate(targetObject);
                BossProjectile boundaryBoss = CreateBossProjectile(playerHealth);
                InvokePrivate(boundaryBoss, "OnTriggerEnter2D", portalCollider);
                Assert(boundaryBoss == null, "An enemy projectile must be consumed by the portal barrier.");

                Projectile leakedBasic = CreateBasicProjectile(playerHealth);
                HomingSkillProjectile leakedHoming = CreateHomingProjectile(playerHealth);
                BossProjectile leakedBoss = CreateBossProjectile(playerHealth);

                Assert(graph.TryTransition(first.Node, second.Node, second.Entry, player),
                    "A registered doorway must transition to its destination.");
                Assert(leakedBasic == null && leakedHoming == null && leakedBoss == null,
                    "Room transition must clear every transient projectile before revealing the destination room.");
                Assert(graph.CurrentNode == second.Node && CountVisible(nodes) == 1 && second.Node.IsVisible,
                    "A transition must activate only the destination room.");
                Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
                Assert(playerBody != null &&
                       (playerBody.position - (Vector2)second.Entry.position).sqrMagnitude < 0.0001f,
                    "A transition must place the player at the destination entry point.");
                Assert((Vector2)cameraObject.transform.position == (Vector2)second.CameraAnchor.position,
                    "A transition must snap the camera to the destination room anchor.");

                Assert(graph.TryTransition(second.Node, first.Node, first.Entry, player),
                    "The fixed graph must support its declared return path.");
                Assert(first.Controller.State == RoomState.Cleared && first.RewardRoom.HasRewarded,
                    "Disabling and revisiting a room must preserve clear and reward state.");
                Assert(first.Node.HasBeenVisited && second.Node.HasBeenVisited && CountVisible(nodes) == 1,
                    "Visited flags and the single-visible-room invariant must survive backtracking.");

                first.Node.Configure("room-b", 1, 1, first.Content, first.CameraAnchor, first.Entry,
                    first.Node.Doorways);
                Assert(!graph.TryValidateConfiguration(out error) && error.Contains("duplicated"),
                    "Duplicate stable room IDs must fail graph validation.");
                first.Node.Configure("room-a", 1, 1, first.Content, first.CameraAnchor, first.Entry,
                    first.Node.Doorways);

                third.Node.SetDoorways(Array.Empty<RoomDoorway>());
                Assert(!graph.TryValidateConfiguration(out error) && error.Contains("return path"),
                    "A one-way fixed graph connection must fail validation.");
                third.Node.SetDoorways(new[] { thirdToSecond });
                Assert(graph.TryValidateConfiguration(out error), error);

                Debug.Log(
                    "Fixed room graph verification passed: stable IDs, reciprocal graph links, single-room visibility, " +
                    "portal barriers, target-aware lower-grade projectile passage, projectile transition cleanup, " +
                    "player/camera transition, backtracking, " +
                    "clear state, and reward state are valid.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static TestRoom CreateRoom(
            Transform parent,
            RoomGraphController graph,
            string roomId,
            int roomNumber,
            Vector2 center)
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
            RewardRoom rewardRoom = rewardObject.AddComponent<RewardRoom>();

            node.Configure(roomId, 1, roomNumber, content, cameraAnchor, entry, Array.Empty<RoomDoorway>());
            return new TestRoom(node, content, cameraAnchor, entry, controller, rewardRoom);
        }

        private static RoomDoorway CreateDoorway(TestRoom source, TestRoom destination, RoomGraphController graph)
        {
            GameObject doorwayObject = new($"{source.Node.RoomId} to {destination.Node.RoomId}");
            doorwayObject.transform.SetParent(source.Content.transform);
            doorwayObject.AddComponent<BoxCollider2D>();
            RoomDoorway doorway = doorwayObject.AddComponent<RoomDoorway>();
            doorway.Configure(graph, source.Node, destination.Node, destination.Entry);
            return doorway;
        }

        private static GameObject CreatePlayer(out PlayerMovement movement, out Health health)
        {
            GameObject player = new("Room Graph Verification Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            health = player.AddComponent<Health>();
            player.AddComponent<PlayerStats>();
            movement = player.AddComponent<PlayerMovement>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(movement, "Awake");
            return player;
        }

        private static DoorController CreatePortalBarrier(Transform parent, out Collider2D barrierCollider)
        {
            GameObject barrier = new("Room Graph Verification Portal Barrier");
            barrier.transform.SetParent(parent);
            barrierCollider = barrier.AddComponent<BoxCollider2D>();
            DoorController door = barrier.AddComponent<DoorController>();
            door.ConfigurePortalBarrier(true);
            return door;
        }

        private static DoorController CreateLegacyDoor(Transform parent, out Collider2D doorCollider)
        {
            GameObject doorObject = new("Room Graph Verification Legacy Door");
            doorObject.transform.SetParent(parent);
            doorCollider = doorObject.AddComponent<BoxCollider2D>();
            return doorObject.AddComponent<DoorController>();
        }

        private static Projectile CreateBasicProjectile(Health owner)
        {
            GameObject projectileObject = new("Room Graph Verification Basic Projectile");
            projectileObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            Projectile projectile = projectileObject.AddComponent<Projectile>();
            InvokeLifecycle(projectile, "Awake");
            projectile.Launch(Vector2.right, owner,
                new DamageContext(owner.gameObject, DamageSourceType.PlayerProjectile, 1));
            return projectile;
        }

        private static HomingSkillProjectile CreateHomingProjectile(Health owner, Health target = null)
        {
            GameObject projectileObject = new("Room Graph Verification Homing Projectile");
            projectileObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            HomingSkillProjectile projectile = projectileObject.AddComponent<HomingSkillProjectile>();
            InvokeLifecycle(projectile, "Awake");
            projectile.Launch(Vector2.right, owner, target,
                new DamageContext(owner.gameObject, DamageSourceType.PlayerSkillExplosion, 1),
                LayerMask.GetMask("Enemy"));
            return projectile;
        }

        private static GameObject CreateEnemyTarget(Transform parent, out Health health)
        {
            GameObject targetObject = new("Room Graph Verification Homing Target");
            targetObject.transform.SetParent(parent);
            targetObject.layer = LayerMask.NameToLayer("Enemy");
            targetObject.AddComponent<CircleCollider2D>();
            health = targetObject.AddComponent<Health>();
            InvokeLifecycle(health, "Awake");
            return targetObject;
        }

        private static BossProjectile CreateBossProjectile(Health owner)
        {
            return BossProjectile.Create(Vector2.zero, Vector2.right, owner.gameObject, 1, null);
        }

        private static int CountVisible(RoomNode[] nodes)
        {
            int count = 0;
            foreach (RoomNode node in nodes)
            {
                if (node.IsVisible)
                {
                    count++;
                }
            }

            return count;
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

        private static void InvokePrivate(MonoBehaviour component, string methodName, params object[] arguments)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }

            method.Invoke(component, arguments);
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
            public TestRoom(
                RoomNode node,
                GameObject content,
                Transform cameraAnchor,
                Transform entry,
                RoomController controller,
                RewardRoom rewardRoom)
            {
                Node = node;
                Content = content;
                CameraAnchor = cameraAnchor;
                Entry = entry;
                Controller = controller;
                RewardRoom = rewardRoom;
            }

            public RoomNode Node { get; }
            public GameObject Content { get; }
            public Transform CameraAnchor { get; }
            public Transform Entry { get; }
            public RoomController Controller { get; }
            public RewardRoom RewardRoom { get; }
        }
    }
}
