using System;
using System.IO;
using System.Linq;
using TMPro;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week20Special4Setup
    {
        public const string ShopDefinitionId = "shop-standard";
        public const string ShopDefinitionPath = Week8RandomRoomSetup.DefinitionFolder + "/shop-standard.asset";
        public const string CatalogFolder = "Assets/Items/Shop";
        public const string CatalogPath = CatalogFolder + "/shop-catalog.asset";
        public const string ShopRoomPrefabPath = "Assets/Prefabs/ShopRoom.prefab";
        private const string RewardDefinitionPath = Week8RandomRoomSetup.DefinitionFolder + "/reward-mixed.asset";
        private const string ItemPickupPrefabPath = "Assets/Prefabs/ItemPickup.prefab";
        private const string HeartPrefabPath = "Assets/Prefabs/HealthPickup.prefab";

        // The shop door changes its neighbor's exits, so template and Encounter selection get new content versions.
        public const int RoomContentVersion = 7;
        public const int EncounterContentVersion = 11;

        // Prices confirmed 2026-10-01. Elif income is tuned later (special obstacles), not here.
        public const int CommonPrice = 10;
        public const int UncommonPrice = 15;
        public const int RarePrice = 20;
        public const int EpicPrice = 25;
        public static readonly (string Id, string Name, string PrefabPath, int Price)[] Consumables =
        {
            ("heart", "하트", HeartPrefabPath, 3),
            ("key", "열쇠", Week17Resource1Setup.KeyPrefabPath, 5),
            ("bomb", "폭탄", Week17Resource1Setup.BombPrefabPath, 5),
        };

        public static readonly Vector2[] StallPositions =
        {
            new(-4.5f, 0.6f), new(-1.5f, 0.6f), new(1.5f, 0.6f), new(4.5f, 0.6f),
        };
        public static readonly Vector2 StallTriggerSize = new(2.4f, 2.4f);

        [MenuItem("Trickal Fan Game/Week 20/Setup Special-4 Shop")]
        public static void Setup()
        {
            RoomDefinition definition = EnsureShopDefinition();
            EnsureBasicTemplateAllowsShop();
            ShopCatalog catalog = EnsureCatalog();
            ShopRoom prefab = EnsureShopRoomPrefab();
            ConfigureScene(definition, catalog, prefab);
            Selection.activeObject = catalog;
            Debug.Log("Special-4 ready: floors roll a 60% key-locked shop beside the start or an intermediate room, " +
                      "selling two Items by rarity price and two consumables that drop as floor pickups.");
        }

        public static RoomDefinition EnsureShopDefinition()
        {
            RoomDefinition reward = AssetDatabase.LoadAssetAtPath<RoomDefinition>(RewardDefinitionPath);
            if (reward == null) throw new InvalidOperationException("Special-4 requires the reward-mixed definition.");
            RoomDefinition definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(ShopDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<RoomDefinition>();
                AssetDatabase.CreateAsset(definition, ShopDefinitionPath);
            }

            // The shop is a safe room; the encounter list only satisfies the Room Definition contract.
            definition.Configure(ShopDefinitionId, RoomType.Shop, reward.EncounterPrefabs.ToArray());
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        private static void EnsureBasicTemplateAllowsShop()
        {
            RoomTemplateDefinition template =
                AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(Week14Room1Setup.BasicTemplatePath);
            if (template == null) throw new InvalidOperationException("Special-4 requires the Basic room template.");
            if (template.SupportsRoomType(RoomType.Shop)) return;

            SerializedObject serialized = new(template);
            SerializedProperty types = serialized.FindProperty("allowedRoomTypes");
            types.InsertArrayElementAtIndex(types.arraySize);
            types.GetArrayElementAtIndex(types.arraySize - 1).intValue = (int)RoomType.Shop;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssetIfDirty(template);
        }

        public static ShopCatalog EnsureCatalog()
        {
            if (!AssetDatabase.IsValidFolder(CatalogFolder)) AssetDatabase.CreateFolder("Assets/Items", "Shop");
            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ShopCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            ShopConsumable[] consumables = Consumables.Select(spec =>
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
                if (prefab == null) throw new InvalidOperationException($"Missing shop pickup Prefab {spec.PrefabPath}.");
                return new ShopConsumable(spec.Id, spec.Name, prefab, spec.Price);
            }).ToArray();
            Undo.RecordObject(catalog, "Configure Special-4 shop catalog");
            catalog.Configure(CommonPrice, UncommonPrice, RarePrice, EpicPrice, consumables);
            if (!catalog.TryValidate(out string error)) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            return catalog;
        }

        public static ShopRoom EnsureShopRoomPrefab()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            GameObject itemPickup = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPickupPrefabPath);
            Sprite itemSprite = itemPickup != null ? itemPickup.GetComponent<SpriteRenderer>()?.sprite : null;
            int pickupLayer = LayerMask.NameToLayer("Pickup");
            if (font == null || itemSprite == null || pickupLayer < 0)
                throw new InvalidOperationException("Special-4 requires the HUD font, the Item pickup sprite, and the Pickup layer.");
            EnsureGlyphs(font);

            bool exists = File.Exists(ShopRoomPrefabPath);
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(ShopRoomPrefabPath) : new GameObject("Shop Room");
            try
            {
                ShopRoom shop = GetOrAdd<ShopRoom>(root);
                ShopStall[] stalls = new ShopStall[StallPositions.Length];
                for (int index = 0; index < stalls.Length; index++)
                    stalls[index] = EnsureStall(root.transform, index, font, itemSprite, pickupLayer);
                shop.ConfigureStalls(stalls);

                Directory.CreateDirectory("Assets/Prefabs");
                if (PrefabUtility.SaveAsPrefabAsset(root, ShopRoomPrefabPath) == null)
                    throw new InvalidOperationException($"Could not save {ShopRoomPrefabPath}.");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(ShopRoomPrefabPath).GetComponent<ShopRoom>();
        }

        private static ShopStall EnsureStall(Transform root, int index, TMP_FontAsset font, Sprite itemSprite,
            int pickupLayer)
        {
            GameObject stallObject = Child(root, $"Stall {index + 1}");
            stallObject.layer = pickupLayer;
            stallObject.transform.localPosition = StallPositions[index];
            foreach (Collider2D collider in stallObject.GetComponents<Collider2D>())
                if (collider is not BoxCollider2D) Object.DestroyImmediate(collider);
            BoxCollider2D trigger = GetOrAdd<BoxCollider2D>(stallObject);
            trigger.isTrigger = true;
            trigger.size = StallTriggerSize;
            trigger.offset = Vector2.zero;

            GameObject displayObject = Child(stallObject.transform, "Display");
            displayObject.transform.localPosition = Vector3.zero;
            displayObject.transform.localScale = Vector3.one * 0.9f;
            SpriteRenderer display = GetOrAdd<SpriteRenderer>(displayObject);
            display.sprite = itemSprite;
            display.color = Color.white;
            display.sortingOrder = 3;

            TextMeshPro label = Text(stallObject.transform, "Label", font, new Vector2(0f, 1.3f), 2.6f,
                new Color(1f, 0.93f, 0.62f, 1f));
            label.text = "상품\n0 엘리프";
            TextMeshPro prompt = Text(stallObject.transform, "Prompt", font, new Vector2(0f, -0.95f), 3f,
                new Color(0.9f, 0.98f, 1f, 1f));
            prompt.text = ShopStall.BuyPrompt;
            prompt.gameObject.SetActive(false);

            ShopStall stall = GetOrAdd<ShopStall>(stallObject);
            stall.ConfigureVisuals(display, label, prompt);
            return stall;
        }

        private static TextMeshPro Text(Transform parent, string name, TMP_FontAsset font, Vector2 position,
            float size, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject textObject = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
            textObject.transform.SetParent(parent, false);
            TextMeshPro text = textObject.GetComponent<TextMeshPro>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = color;
            text.sortingOrder = 4;
            RectTransform rect = (RectTransform)textObject.transform;
            rect.localPosition = position;
            rect.sizeDelta = new Vector2(3f, 1.4f);
            rect.localScale = Vector3.one;
            return text;
        }

        private static void EnsureGlyphs(TMP_FontAsset font)
        {
            string required = "상품엘리프부족구매[E]0123456789" +
                              string.Concat(Consumables.Select(spec => spec.Name));
            if (!font.HasCharacters(required) && !font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing Special-4 shop glyphs: " + missing);
            EditorUtility.SetDirty(font);
        }

        private static void ConfigureScene(RoomDefinition definition, ShopCatalog catalog, ShopRoom prefab)
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = generator != null ? generator.GetComponent<RoomGraphAssembler>() : null;
            if (generator == null || assembler == null)
                throw new InvalidOperationException("Special-4 requires the Game Scene generator and assembler.");

            RoomDefinition[] definitions = generator.RoomDefinitions
                .Where(candidate => candidate != null && candidate.RoomDefinitionId != ShopDefinitionId)
                .Append(definition)
                .OrderBy(candidate => candidate.RoomDefinitionId, StringComparer.Ordinal)
                .ToArray();
            Undo.RecordObject(generator, "Configure Special-4 shop generation");
            generator.Configure(generator.FloorCount, generator.MinimumRoomsPerFloor, generator.MaximumRoomsPerFloor,
                generator.MinimumBossDistance, generator.GenerationRetryLimit, definitions);
            generator.ConfigureTemplates(Math.Max(RoomContentVersion, generator.RoomContentVersion),
                generator.RoomTemplates.ToArray());
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                generator.EncounterDefinitions.ToArray());
            EditorUtility.SetDirty(generator);

            Undo.RecordObject(assembler, "Configure Special-4 shop room");
            assembler.ConfigureShop(prefab, catalog);
            EditorUtility.SetDirty(assembler);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Special-4 setup.");
        }

        private static GameObject Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            GameObject created = new(name);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
