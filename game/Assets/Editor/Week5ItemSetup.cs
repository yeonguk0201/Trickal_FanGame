using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week5ItemSetup
    {
        private const string RootName = "Week5 Item Test Pickups";
        private const string RewardRootName = "Week5 Reward Drop Test";
        private const string ItemFolder = "Assets/Items";
        private const string PickupPrefabPath = "Assets/Prefabs/ItemPickup.prefab";

        [MenuItem("Trickal Fan Game/Setup Week 5 Item Slice")]
        public static void Setup()
        {
            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                Debug.LogError("Run the Week 3 setup first; it provides the Player.");
                return;
            }

            if (GameObject.Find(RootName) != null)
            {
                Debug.LogWarning("Week 5 item setup already exists.");
                return;
            }

            EnsureItemFolder();
            ItemDefinition attack = LoadOrCreateDefinition(
                "item-01", "Attack Boost", ItemEffectType.AttackDamage, 1f, 0);
            ItemDefinition health = LoadOrCreateDefinition(
                "item-02", "Max Health Boost", ItemEffectType.MaxHealth, 3f, 0);
            ItemDefinition speed = LoadOrCreateDefinition(
                "item-03", "Move Speed Boost", ItemEffectType.MoveSpeed, 0.75f, 5);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Week 5 Item Slice");

            if (player.GetComponent<PlayerInventory>() == null)
            {
                Undo.AddComponent<PlayerInventory>(player.gameObject);
            }

            GameObject root = CreateObject(RootName, null);
            Sprite pickupSprite = player.GetComponent<SpriteRenderer>()?.sprite;
            CreatePickup(root.transform, attack, new Vector2(-3f, 2f), pickupSprite, new Color(1f, 0.35f, 0.3f));
            CreatePickup(root.transform, health, new Vector2(-1f, 2f), pickupSprite, new Color(0.3f, 1f, 0.45f));
            CreatePickup(root.transform, speed, new Vector2(1f, 2f), pickupSprite, new Color(0.3f, 0.65f, 1f));
            CreatePickup(root.transform, attack, new Vector2(3f, 2f), pickupSprite, new Color(1f, 0.35f, 0.3f));

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("Week 5 item slice ready. Pick up all four objects to verify three effects and duplicate stacking.", root);
        }

        [MenuItem("Trickal Fan Game/Setup Week 5 Reward Drops")]
        public static void SetupRewardDrops()
        {
            if (GameObject.Find(RewardRootName) != null)
            {
                Debug.LogWarning("Week 5 reward drop setup already exists.");
                return;
            }

            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            RunProgress progress = Object.FindFirstObjectByType<RunProgress>();
            BossController boss = Object.FindFirstObjectByType<BossController>();
            RunSession session = Object.FindFirstObjectByType<RunSession>();
            RoomController prerequisiteRoom = GameObject.Find("Room 2")?.GetComponent<RoomController>();
            RoomController bossRoom = GameObject.Find("Boss Room")?.GetComponent<RoomController>();
            if (player == null || progress == null || boss == null || session == null ||
                prerequisiteRoom == null || bossRoom == null)
            {
                Debug.LogError(
                    "Run the Week 4 setup first; it provides Player, RunProgress, Room 2, Boss Room, Boss, and RunSession.");
                return;
            }

            EnsureItemFolder();
            ItemDefinition[] itemPool =
            {
                AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/item-01.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/item-02.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/item-03.asset")
            };
            if (System.Array.Exists(itemPool, definition => definition == null))
            {
                Debug.LogError("Run the Week 5 item slice setup first; it creates the three item definitions.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Week 5 Reward Drops");

            ItemPickup pickupPrefab = LoadOrCreatePickupPrefab(player.GetComponent<SpriteRenderer>()?.sprite);
            GameObject root = CreateObject(RewardRootName, null);
            CreateRewardRoom(
                root.transform,
                progress,
                prerequisiteRoom,
                pickupPrefab,
                itemPool,
                player.GetComponent<SpriteRenderer>()?.sprite);
            ConfigureBossDrop(boss, pickupPrefab, itemPool);

            SerializedObject serializedBossRoom = new(bossRoom);
            serializedBossRoom.FindProperty("roomNumber").intValue = 4;
            serializedBossRoom.ApplyModifiedProperties();

            SerializedObject serializedSession = new(session);
            serializedSession.FindProperty("boss").objectReferenceValue = null;
            serializedSession.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log(
                "Week 5 reward drops ready. The reward-room marker and the floor-1 boss each drop one random item. " +
                "The boss no longer clears the Run; final-boss completion returns when the three-floor flow is added.",
                root);
        }

        private static void EnsureItemFolder()
        {
            if (!AssetDatabase.IsValidFolder(ItemFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Items");
            }
        }

        private static ItemDefinition LoadOrCreateDefinition(
            string itemId,
            string displayName,
            ItemEffectType effectType,
            float effectValue,
            int maxStacks)
        {
            string path = $"{ItemFolder}/{itemId}.asset";
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            SerializedObject serialized = new(definition);
            serialized.FindProperty("itemId").stringValue = itemId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("effectType").enumValueIndex = (int)effectType;
            serialized.FindProperty("effectValue").floatValue = effectValue;
            serialized.FindProperty("stackMode").enumValueIndex = (int)ItemStackMode.Additive;
            serialized.FindProperty("maxStacks").intValue = maxStacks;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        private static void CreatePickup(
            Transform parent,
            ItemDefinition definition,
            Vector2 position,
            Sprite sprite,
            Color color)
        {
            GameObject pickupObject = CreateObject($"Pickup - {definition.DisplayName}", parent);
            pickupObject.transform.position = position;
            pickupObject.transform.localScale = Vector3.one * 0.45f;

            SpriteRenderer renderer = Undo.AddComponent<SpriteRenderer>(pickupObject);
            renderer.sprite = sprite;
            renderer.color = color;

            CircleCollider2D collider = Undo.AddComponent<CircleCollider2D>(pickupObject);
            collider.isTrigger = true;
            ItemPickup pickup = Undo.AddComponent<ItemPickup>(pickupObject);
            pickup.Configure(definition);
        }

        private static ItemPickup LoadOrCreatePickupPrefab(Sprite sprite)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<ItemPickup>();
            }

            GameObject instance = new("Item Pickup");
            instance.transform.localScale = Vector3.one * 0.45f;
            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 0.82f, 0.2f);
            CircleCollider2D collider = instance.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            instance.AddComponent<ItemPickup>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PickupPrefabPath);
            Object.DestroyImmediate(instance);
            return prefab.GetComponent<ItemPickup>();
        }

        private static void CreateRewardRoom(
            Transform parent,
            RunProgress progress,
            RoomController prerequisiteRoom,
            ItemPickup pickupPrefab,
            ItemDefinition[] itemPool,
            Sprite sprite)
        {
            GameObject roomObject = CreateObject("Floor 1 Reward Room", parent);
            roomObject.transform.position = new Vector2(15.5f, 0f);
            BoxCollider2D trigger = Undo.AddComponent<BoxCollider2D>(roomObject);
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.5f, 6f);

            GameObject markerObject = CreateObject("Reward Room Marker", roomObject.transform);
            SpriteRenderer marker = Undo.AddComponent<SpriteRenderer>(markerObject);
            marker.sprite = sprite;
            marker.color = new Color(1f, 0.82f, 0.2f, 0.45f);
            markerObject.transform.localScale = new Vector3(0.35f, 2.5f, 1f);

            GameObject dropPointObject = CreateObject("Reward Drop Point", roomObject.transform);
            dropPointObject.transform.position = new Vector2(17f, 0f);

            ItemDropSource source = Undo.AddComponent<ItemDropSource>(roomObject);
            source.Configure(pickupPrefab, itemPool, dropPointObject.transform);
            RewardRoom rewardRoom = Undo.AddComponent<RewardRoom>(roomObject);
            rewardRoom.Configure(1, 3, progress, source, prerequisiteRoom);
        }

        private static void ConfigureBossDrop(
            BossController boss,
            ItemPickup pickupPrefab,
            ItemDefinition[] itemPool)
        {
            ItemDropSource source = boss.GetComponent<ItemDropSource>();
            if (source == null)
            {
                source = Undo.AddComponent<ItemDropSource>(boss.gameObject);
            }
            source.Configure(pickupPrefab, itemPool, boss.transform);

            BossItemDrop drop = boss.GetComponent<BossItemDrop>();
            if (drop == null)
            {
                drop = Undo.AddComponent<BossItemDrop>(boss.gameObject);
            }
            drop.Configure(false, source);
        }

        private static GameObject CreateObject(string objectName, Transform parent)
        {
            GameObject created = new(objectName);
            Undo.RegisterCreatedObjectUndo(created, $"Create {objectName}");
            if (parent != null)
            {
                created.transform.SetParent(parent);
            }

            return created;
        }
    }
}
