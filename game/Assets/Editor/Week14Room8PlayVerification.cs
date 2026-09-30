using System;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // An isolated Play Mode fixture: real door triggers and interpolated physics, no Run/API traffic.
    [InitializeOnLoad]
    public static class Week14Room8PlayVerification
    {
        private const string Pending = "Week14Room8PlayVerification.Pending";
        private static double deadline;
        private static int caseIndex;
        private static Week14Room8FrameProbe probe;
        private static readonly string[] Sources = { "wide", "large" };
        private static readonly string[] Destinations = { "basic", "small", "tall" };

        static Week14Room8PlayVerification() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 120;
            if (EditorApplication.timeSinceStartup > deadline) { Finish(1, "Room-8 Play Mode timed out."); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 5) return;
            try
            {
                if (probe == null) CreateCase();
                else if (probe.Failure != null) Finish(1, probe.Failure);
                else if (probe.Done)
                {
                    UnityEngine.Object.DestroyImmediate(probe.gameObject);
                    probe = null;
                    if (++caseIndex == 24) Finish(0,
                        "Room-8 Play Mode passed: 24 horizontal door crossings, actual triggers, interpolated " +
                        "player render position, camera framing and single-room visibility across first frames.");
                }
            }
            catch (Exception exception) { Finish(1, exception.ToString()); }
        }

        private static void CreateCase()
        {
            int pair = caseIndex / 4;
            bool reverse = caseIndex % 4 >= 2;
            int sign = caseIndex % 2 == 0 ? 1 : -1;
            string from = Sources[pair / 3];
            string to = Destinations[pair % 3];
            if (reverse) (from, to) = (to, from);
            GameObject root = new($"Room-8 {from}->{to} sign={sign}");
            RoomGraphController graph = root.AddComponent<RoomGraphController>();
            RoomPrefab source = CreateRoom(from, root.transform, Vector3.zero, "source");
            RoomPrefab destination = CreateRoom(to, root.transform, new Vector3(sign * 20, 0, 0), "destination");
            GameObject playerObject = new("Player");
            playerObject.transform.SetParent(root.transform);
            playerObject.tag = "Player";
            PlayerMovement player = playerObject.AddComponent<PlayerMovement>();
            player.enabled = false;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            playerObject.AddComponent<BoxCollider2D>().size = Vector2.one;
            GameObject cameraObject = new("Camera");
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 16f / 9f;
            RoomCameraController roomCamera = cameraObject.AddComponent<RoomCameraController>();
            RoomDoorDirection direction = sign > 0 ? RoomDoorDirection.Right : RoomDoorDirection.Left;
            RoomDoorSlot exit = source.FindSlot(direction);
            RoomDoorSlot entry = destination.FindSlot(GeneratedFloorGraph.Opposite(direction));
            exit.Bind(graph, source.Node, destination.Node, entry.EntryPoint, null);
            entry.Bind(graph, destination.Node, source.Node, exit.EntryPoint, null);
            source.Node.SetDoorways(new[] { exit.Doorway });
            destination.Node.SetDoorways(new[] { entry.Doorway });
            source.Node.DefaultEntryPoint.position = exit.EntryPoint.position;
            graph.Configure(new[] { source.Node, destination.Node }, source.Node, player, roomCamera, null);
            probe = root.AddComponent<Week14Room8FrameProbe>();
            probe.Graph = graph;
            probe.Body = body;
            probe.Source = source.Node;
            probe.Destination = destination.Node;
            probe.Entry = entry.EntryPoint;
            probe.Direction = sign;
            Debug.Log($"Room-8 Play case {caseIndex}: {root.name}");
        }

        private static RoomPrefab CreateRoom(string id, Transform parent, Vector3 position, string roomId)
        {
            RoomTemplateDefinition template = AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(
                $"Assets/Rooms/Templates/{id}-standard.asset");
            RoomPrefab instance = UnityEngine.Object.Instantiate(template.RoomPrefabAsset, parent).GetComponent<RoomPrefab>();
            instance.transform.localPosition = position;
            instance.Controller.enabled = false;
            instance.Controller.GetComponent<Collider2D>().enabled = false;
            if (instance.RewardRoom != null) instance.RewardRoom.gameObject.SetActive(false);
            RoomNode node = instance.Node;
            node.Configure(roomId, 1, roomId == "source" ? 1 : 2, node.ContentRoot, node.CameraAnchor,
                node.DefaultEntryPoint, Array.Empty<RoomDoorway>());
            node.ApplyRoomProfile(template.Profile);
            return instance;
        }

        private static void Finish(int code, string message)
        {
            SessionState.SetBool(Pending, false);
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(code);
        }
    }

    [DefaultExecutionOrder(30000)]
    public sealed class Week14Room8FrameProbe : MonoBehaviour
    {
        public RoomGraphController Graph;
        public Rigidbody2D Body;
        public RoomNode Source;
        public RoomNode Destination;
        public Transform Entry;
        public int Direction;
        public string Failure { get; private set; }
        public bool Done { get; private set; }
        private int frames;
        private int enteredFrames;

        private void FixedUpdate()
        {
            if (frames > 5 && Graph.CurrentNode == Source) Body.linearVelocity = new Vector2(Direction * 5f, 0);
        }

        private void LateUpdate()
        {
            frames++;
            if (Graph.CurrentNode != Destination) return;
            float error = Vector2.Distance(Body.transform.position, Body.position);
            if (error > 0.1f)
                Failure = $"{name}: first destination frame {enteredFrames}: rendered player={Body.transform.position}, " +
                          $"physics={Body.position}, entry={Entry.position}, mismatch={error}";
            if (Source.ContentRoot.activeInHierarchy || !Destination.ContentRoot.activeInHierarchy)
                Failure = $"{name}: previous room remains active.";
            if (Vector2.Distance(Body.position, Entry.position) > 0.1f ||
                Body.interpolation != RigidbodyInterpolation2D.Interpolate)
                Failure = $"{name}: safe entry position or smooth movement interpolation was lost.";
            Vector2 cameraLocal = Destination.CameraAnchor.InverseTransformPoint(Graph.RoomCamera.transform.position);
            Rect bounds = Graph.RoomCamera.ActiveCenterBounds;
            if (cameraLocal.x < bounds.xMin - 0.001f || cameraLocal.x > bounds.xMax + 0.001f ||
                cameraLocal.y < bounds.yMin - 0.001f || cameraLocal.y > bounds.yMax + 0.001f)
                Failure = $"{name}: destination camera is outside its valid framing bounds.";
            if (++enteredFrames >= 5) Done = true;
        }
    }
}
