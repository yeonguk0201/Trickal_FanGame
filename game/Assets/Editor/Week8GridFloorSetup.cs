using System;
using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week8GridFloorSetup
    {
        public const string PrefabFolder = "Assets/Rooms/Prefabs";
        public const string PrefabPath = PrefabFolder + "/room-grid-base.prefab";

        [MenuItem("Trickal Fan Game/Setup Phase F-5 Seeded Grid Floors")]
        public static void Setup()
        {
            Week8RandomRoomSetup.Setup();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)?.GetComponent<FloorGenerator>();
            RoomGraphController graph = FindSceneGraph();
            PlayerMovement player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            RunProgress progress = UnityEngine.Object.FindFirstObjectByType<RunProgress>();
            ItemPickup pickup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemPickup.prefab")?.GetComponent<ItemPickup>();
            ItemDefinition[] itemPool = LoadItemPool();
            if (generator == null || graph == null || graph.RoomCamera == null ||
                graph.RoomCamera.GetComponent<Camera>() == null || player == null || progress == null || pickup == null ||
                Array.Exists(itemPool, item => item == null))
            {
                Debug.LogError("Phase F-5 requires the Phase F definitions, fixed graph references, player, and item assets.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Phase F-5 Seeded Grid Floors");
            EnsureFolder();
            RoomPrefab prefab = CreateOrUpdatePrefab(player.GetComponent<SpriteRenderer>()?.sprite, pickup, itemPool);
            if (prefab == null) { Undo.RevertAllDownToGroup(undoGroup); return; }

            Undo.RecordObject(graph.RoomCamera, "Configure expanded room camera");
            Camera layoutCamera = graph.RoomCamera.GetComponent<Camera>();
            Undo.RecordObject(layoutCamera, "Configure expanded room camera size");
            graph.RoomCamera.Configure(graph.RoomCamera.GetComponent<CameraFollow>());
            EditorUtility.SetDirty(graph.RoomCamera);
            EditorUtility.SetDirty(layoutCamera);

            RoomDefinition[] definitions = new RoomDefinition[generator.RoomDefinitions.Count];
            for (int i = 0; i < definitions.Length; i++) definitions[i] = generator.RoomDefinitions[i];
            Undo.RecordObject(generator, "Configure F-5 generator settings");
            generator.Configure(3, 6, 8, 3, 32, definitions);

            RoomGraphAssembler assembler = generator.GetComponent<RoomGraphAssembler>();
            if (assembler == null) assembler = Undo.AddComponent<RoomGraphAssembler>(generator.gameObject);
            Undo.RecordObject(assembler, "Configure F-5 runtime assembler");
            assembler.Configure(generator, graph, progress, prefab);

            RemoveGeneratedPreview(generator.transform);
            RemoveLegacyRoomChildren(graph.transform);
            if (!assembler.TryApplyGeneratedGraphForVerification(Week8RandomRoomSetup.FixedVerificationSeed, out string error))
            {
                Debug.LogError($"Phase F-5 preview assembly failed. {error}", assembler);
                Undo.RevertAllDownToGroup(undoGroup);
                return;
            }

            EditorUtility.SetDirty(generator); EditorUtility.SetDirty(assembler); EditorUtility.SetDirty(graph);
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = generator.gameObject;
            Debug.Log("Phase F-5 setup ready: the shared 16x9 layout, 20x13 grid spacing, wall-aligned four-way doors, and current-floor runtime assembly are configured.", assembler);
        }

        private static RoomPrefab CreateOrUpdatePrefab(Sprite sprite, ItemPickup pickup, ItemDefinition[] itemPool)
        {
            GameObject root = new("Room Grid Base");
            try
            {
                RoomPrefab roomPrefab = root.AddComponent<RoomPrefab>();
                RoomNode node = root.AddComponent<RoomNode>();
                GameObject content = Child(root.transform, "Content");
                Transform cameraAnchor = Child(root.transform, "Camera Anchor").transform;
                Transform initialSpawnPoint = Child(content.transform, "Start Point").transform;

                GameObject encounterObject = Child(content.transform, "Encounter");
                BoxCollider2D encounterTrigger = encounterObject.AddComponent<BoxCollider2D>();
                encounterTrigger.isTrigger = true; encounterTrigger.size = RoomLayout.EncounterSize;
                RoomController controller = encounterObject.AddComponent<RoomController>();
                Transform[] spawns = new Transform[3];
                for (int i = 0; i < spawns.Length; i++)
                {
                    spawns[i] = Child(encounterObject.transform, $"Spawn {i + 1}").transform;
                    spawns[i].localPosition = RoomLayout.SpawnPosition(i, spawns.Length);
                }
                controller.Configure(1, 1, null, null, spawns, Array.Empty<DoorController>());

                BuildWalls(content.transform, sprite);
                RoomDoorSlot[] slots = new RoomDoorSlot[4];
                foreach (RoomDoorDirection direction in Enum.GetValues(typeof(RoomDoorDirection)))
                    slots[(int)direction] = CreateSlot(content.transform, direction, sprite);

                GameObject rewardObject = Child(content.transform, "Treasure Reward");
                BoxCollider2D rewardTrigger = rewardObject.AddComponent<BoxCollider2D>();
                rewardTrigger.isTrigger = true; rewardTrigger.size = RoomLayout.RewardTriggerSize;
                Transform dropPoint = Child(rewardObject.transform, "Drop Point").transform;
                ItemDropSource dropSource = rewardObject.AddComponent<ItemDropSource>();
                dropSource.Configure(pickup, itemPool, dropPoint, content.transform);
                RewardRoom reward = rewardObject.AddComponent<RewardRoom>();
                reward.Configure(1, 1, null, dropSource, controller);
                rewardObject.SetActive(false);

                node.Configure("floor-01-room-01", 1, 1, content, cameraAnchor,
                    slots[(int)RoomDoorDirection.Left].EntryPoint, Array.Empty<RoomDoorway>());
                node.ConfigureInitialSpawnPoint(initialSpawnPoint);
                roomPrefab.Configure(node, controller, reward, slots);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) { Debug.LogError($"Could not save {PrefabPath}."); return null; }
                return saved.GetComponent<RoomPrefab>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static RoomDoorSlot CreateSlot(Transform parent, RoomDoorDirection direction, Sprite sprite)
        {
            Vector2 normal = RoomLayout.Direction(direction);
            bool horizontal = RoomLayout.IsSideDoor(direction);
            GameObject slotObject = Child(parent, $"{direction} Door Slot");
            slotObject.transform.localPosition = normal * RoomLayout.TransitionCenter(direction);
            RoomDoorSlot slot = slotObject.AddComponent<RoomDoorSlot>();
            Transform entry = Child(slotObject.transform, "Entry Point").transform;
            entry.localPosition = -normal * RoomLayout.EntryInsetFromTransition;
            GameObject transition = Child(slotObject.transform, "Transition");
            BoxCollider2D trigger = transition.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = horizontal
                ? new Vector2(RoomLayout.TransitionThickness, RoomLayout.TransitionLength)
                : new Vector2(RoomLayout.TransitionLength, RoomLayout.TransitionThickness);
            RoomDoorway doorway = transition.AddComponent<RoomDoorway>();
            GameObject blockerObject = Child(slotObject.transform, "Blocking Door");
            blockerObject.layer = LayerMask.NameToLayer("Environment");
            blockerObject.transform.localPosition = normal * RoomLayout.TransitionInset;
            AddVisual(blockerObject, sprite,
                horizontal
                    ? new Vector2(RoomLayout.DoorThickness, RoomLayout.DoorLength)
                    : new Vector2(RoomLayout.DoorLength, RoomLayout.DoorThickness),
                new Color(0.2f, 0.75f, 0.3f));
            blockerObject.AddComponent<BoxCollider2D>();
            DoorController blocker = blockerObject.AddComponent<DoorController>();
            blocker.ConfigurePortalBarrier(true);
            blocker.ConfigureVisualKind(DoorVisualKind.Normal);
            GameObject seal = Child(slotObject.transform, "Boundary Seal");
            seal.layer = LayerMask.NameToLayer("Environment");
            seal.transform.localPosition = normal * RoomLayout.TransitionInset;
            AddVisual(seal, sprite,
                horizontal
                    ? new Vector2(RoomLayout.SealThickness, RoomLayout.SealLength)
                    : new Vector2(RoomLayout.SealLength, RoomLayout.SealThickness),
                new Color(0.3f, 0.34f, 0.43f));
            seal.AddComponent<BoxCollider2D>();
            transition.SetActive(false); blockerObject.SetActive(false); seal.SetActive(true);
            slot.Configure(direction, doorway, entry, blocker, seal); return slot;
        }

        private static void BuildWalls(Transform parent, Sprite sprite)
        {
            Color color = new(0.32f, 0.39f, 0.52f);
            float horizontalCenter = RoomLayout.HorizontalWallSegmentCenter;
            float verticalCenter = RoomLayout.VerticalWallSegmentCenter;
            Vector2 horizontalSize = new(RoomLayout.HorizontalWallSegmentLength, RoomLayout.WallThickness);
            Vector2 verticalSize = new(RoomLayout.WallThickness, RoomLayout.VerticalWallSegmentLength);
            CreateWall(parent, "Top Left Wall", new Vector2(-horizontalCenter, RoomLayout.VerticalWallCenter), horizontalSize, sprite, color);
            CreateWall(parent, "Top Right Wall", new Vector2(horizontalCenter, RoomLayout.VerticalWallCenter), horizontalSize, sprite, color);
            CreateWall(parent, "Bottom Left Wall", new Vector2(-horizontalCenter, -RoomLayout.VerticalWallCenter), horizontalSize, sprite, color);
            CreateWall(parent, "Bottom Right Wall", new Vector2(horizontalCenter, -RoomLayout.VerticalWallCenter), horizontalSize, sprite, color);
            CreateWall(parent, "Left Upper Wall", new Vector2(-RoomLayout.HorizontalWallCenter, verticalCenter), verticalSize, sprite, color);
            CreateWall(parent, "Left Lower Wall", new Vector2(-RoomLayout.HorizontalWallCenter, -verticalCenter), verticalSize, sprite, color);
            CreateWall(parent, "Right Upper Wall", new Vector2(RoomLayout.HorizontalWallCenter, verticalCenter), verticalSize, sprite, color);
            CreateWall(parent, "Right Lower Wall", new Vector2(RoomLayout.HorizontalWallCenter, -verticalCenter), verticalSize, sprite, color);
        }

        private static void CreateWall(Transform parent, string name, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            GameObject wall = Child(parent, name); wall.layer = LayerMask.NameToLayer("Environment");
            wall.transform.localPosition = position; AddVisual(wall, sprite, size, color); wall.AddComponent<BoxCollider2D>();
        }
        private static void AddVisual(GameObject target, Sprite sprite, Vector2 size, Color color)
        { SpriteRenderer renderer = target.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color;
          target.transform.localScale = new Vector3(size.x, size.y, 1f); }
        private static GameObject Child(Transform parent, string name)
        { GameObject child = new(name); child.transform.SetParent(parent, false); return child; }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Rooms")) AssetDatabase.CreateFolder("Assets", "Rooms");
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Rooms", "Prefabs");
        }
        private static ItemDefinition[] LoadItemPool() => new[]
        {
            AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Items/item-01.asset"),
            AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Items/item-02.asset"),
            AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Items/item-03.asset"),
        };
        private static RoomGraphController FindSceneGraph()
        { foreach (RoomGraphController graph in Resources.FindObjectsOfTypeAll<RoomGraphController>())
            if (graph.gameObject.scene == SceneManager.GetActiveScene() && graph.gameObject.name == "Week7 Fixed Room Graph") return graph;
          return null; }
        private static void RemoveGeneratedPreview(Transform generator)
        {
            for (int i = generator.childCount - 1; i >= 0; i--)
                if (generator.GetChild(i).name.StartsWith("Generated Floor ", StringComparison.Ordinal))
                    Undo.DestroyObjectImmediate(generator.GetChild(i).gameObject);
        }
        private static void RemoveLegacyRoomChildren(Transform graph)
        {
            for (int i = graph.childCount - 1; i >= 0; i--)
                if (graph.GetChild(i).name.StartsWith("Floor ", StringComparison.Ordinal))
                    Undo.DestroyObjectImmediate(graph.GetChild(i).gameObject);
        }
    }
}
