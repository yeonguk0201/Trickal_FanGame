using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Power-1: creates the six 교주의 권능 assets (contract 23 §9.2, plan 26 §4.4), the floor pickup Prefab, the
    // player's 권능 slot and executor in the Game Scene, and one HUD panel above the spell slot. Re-running updates
    // the same assets and objects, keeping their GUIDs, instead of adding more.
    public static class Week27Power1Setup
    {
        public const string PickupPrefabPath = "Assets/Prefabs/ActivePowerPickup.prefab";
        public const string HudPanelName = "Power Slot HUD";
        public const float PickupWorldDiameter = 0.5f;
        public const int MaximumGaugeCells = 5;
        // Above the Slot-0 spell slot (480, -430, 220 x 112). Final placement belongs to UX-1.
        public static readonly Vector2 HudPosition = new(480, -300);
        public static readonly Vector2 HudSize = new(220, 132);
        // Contract 23 §9.1: every 권능 is Rare until Balance-0.
        public const ItemRarity PowerRarity = ItemRarity.Rare;

        public const string PoppinStarId = "active-poppin-star";
        public const string SecurityBlanketId = "active-security-blanket";
        public const string DivinePunishmentId = "active-divine-punishment";
        public const string SolarPlexusStrikeId = "active-solar-plexus-strike";
        public const string FreeloaderShieldId = "active-freeloader-shield";
        public const string SuddenAccelerationId = "active-sudden-acceleration";

        public sealed class PowerSpec
        {
            public PowerSpec(string id, string name, int chargeRooms, params ItemEffectEntry[] effects)
            {
                Id = id;
                Name = name;
                ChargeRooms = chargeRooms;
                Effects = effects;
            }

            public string Id { get; }
            public string Name { get; }
            public int ChargeRooms { get; }
            public ItemEffectEntry[] Effects { get; }
            public string AssetPath => "Assets/Items/" + Id + ".asset";
        }

        // Plan 26 §4.4 "교주의 권능 6종". HP values are half-heart units; damage ratios are of attack damage.
        public static IReadOnlyList<PowerSpec> Specs => new[]
        {
            new PowerSpec(PoppinStarId, "팝핀 스타", 2,
                new ItemEffectEntry(ItemEffectType.PowerRoomBlast, configuredMagnitude: 1f,
                    configuredSecondaryMagnitude: 0.08f, configuredIntegerAmount: 5, configuredScaleMultiplier: 2f,
                    configuredDurationSeconds: 6f)),
            new PowerSpec(SecurityBlanketId, "애착 담요", 3,
                new ItemEffectEntry(ItemEffectType.PowerProtectiveBlanket, configuredMagnitude: 1f,
                    configuredIntegerAmount: 1, configuredIntervalSeconds: 3f, configuredDurationSeconds: 6f)),
            new PowerSpec(DivinePunishmentId, "찐짜천벌", 3,
                // Shock slow and duration follow 앗땃따건 (24번 §4.8).
                new ItemEffectEntry(ItemEffectType.PowerSmite, configuredMagnitude: 5f,
                    configuredSecondaryMagnitude: 0.1f, configuredIntegerAmount: 4, configuredDurationSeconds: 2.5f)),
            new PowerSpec(SolarPlexusStrikeId, "명치 꽂기", 4,
                new ItemEffectEntry(ItemEffectType.PowerStunRoomEnemies, configuredDurationSeconds: 3f),
                new ItemEffectEntry(ItemEffectType.PowerRoomEnemyVulnerability, configuredMagnitude: 0.5f,
                    configuredDurationSeconds: 6f)),
            new PowerSpec(FreeloaderShieldId, "날먹보호막", 4,
                new ItemEffectEntry(ItemEffectType.PowerGainShield, configuredMagnitude: 4f)),
            new PowerSpec(SuddenAccelerationId, "급발진", 5,
                new ItemEffectEntry(ItemEffectType.PowerOverdrive, configuredMagnitude: 0.6f,
                    configuredSecondaryMagnitude: 0.2f, configuredScaleMultiplier: 0.4f, configuredDurationSeconds: 15f),
                new ItemEffectEntry(ItemEffectType.PowerRestoreSP, configuredIntegerAmount: 1)),
        };

        [MenuItem("Trickal Fan Game/Week 27/Setup Power-1 Active Powers")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Power-1 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Power-1 Active Powers");

            ItemDefinition[] powers = Specs.Select(ConfigureItem).ToArray();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Power-1 requires the Frontend TMP font.");
            EnsureGlyphs(font, GamePowerHudView.AllFixedText +
                               string.Concat(powers.Select(power => power.DisplayName + ArtifactEffectDescription.Build(power))));
            AssetDatabase.SaveAssets();
            ActivePowerPickup prefab = EnsurePickupPrefab();
            ConfigureGameScene(prefab, font);

            Undo.CollapseUndoOperations(group);
            Debug.Log($"Power-1 setup complete: {powers.Length} 교주의 권능 assets, the pickup Prefab, the player 권능 " +
                      "slot (R uses a full gauge, a combat or boss room clear adds one charge) and its HUD.");
        }

        private static ItemDefinition ConfigureItem(PowerSpec spec)
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(spec.AssetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = spec.Id;
                AssetDatabase.CreateAsset(definition, spec.AssetPath);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + spec.Id);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + spec.Id);
            }

            definition.ConfigureContract(spec.Id, spec.Name, ItemKind.Active, PowerRarity, true, 1, spec.Effects);
            definition.ConfigureActiveCharge(spec.ChargeRooms);
            if (!definition.IsValid)
                throw new InvalidOperationException($"{spec.Id} is not a valid 권능 contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        public static ActivePowerPickup EnsurePickupPrefab()
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(Week17Resource1Setup.PlaceholderSpritePath);
            if (sprite == null) throw new InvalidOperationException("The built-in pickup placeholder sprite is missing.");

            bool exists = File.Exists(PickupPrefabPath);
            GameObject root = exists
                ? PrefabUtility.LoadPrefabContents(PickupPrefabPath)
                : new GameObject("Active Power Pickup");
            try
            {
                root.layer = LayerMask.NameToLayer("Default");
                root.transform.localScale = Vector3.one * (PickupWorldDiameter / sprite.bounds.size.x);
                SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(root);
                renderer.sprite = sprite;
                renderer.color = ActivePowerPickup.PowerColor;
                renderer.sortingOrder = PlayerMovement.AboveBodySortingOrder;
                CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
                collider.isTrigger = true;
                collider.radius = sprite.bounds.extents.x;
                ActivePowerPickup pickup = GetOrAdd<ActivePowerPickup>(root);
                pickup.ConfigureDisplay(renderer);
                pickup.Configure(null, null, ActivePowerPickup.NoStoredCharge, false);

                Directory.CreateDirectory("Assets/Prefabs");
                if (PrefabUtility.SaveAsPrefabAsset(root, PickupPrefabPath) == null)
                    throw new InvalidOperationException($"Could not save {PickupPrefabPath}.");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath).GetComponent<ActivePowerPickup>();
        }

        private static void ConfigureGameScene(ActivePowerPickup prefab, TMP_FontAsset font)
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerMovement player = FindSingle<PlayerMovement>(scene, "player");
            RunProgress progress = FindSingle<RunProgress>(scene, "RunProgress");
            RoomGraphController graph = FindSingle<RoomGraphController>(scene, "RoomGraphController");
            GameHudView gameHud = FindSingle<GameHudView>(scene, "GameHudView");
            if (graph.Player != player)
                throw new InvalidOperationException("The room graph must belong to the Game Scene player.");
            if (player.GetComponent<PlayerInventory>() == null || player.GetComponent<PlayerSP>() == null)
                throw new InvalidOperationException("The Game Scene player needs PlayerInventory and PlayerSP first.");
            RectTransform frame = gameHud.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("Game HUD ReferenceFrame is missing.");

            PlayerPowerEffects effects = Component<PlayerPowerEffects>(player.gameObject);
            effects.Configure(graph);
            EditorUtility.SetDirty(effects);
            PlayerPowerSlot slot = Component<PlayerPowerSlot>(player.gameObject);
            slot.Configure(progress, prefab);
            EditorUtility.SetDirty(slot);
            BuildHud(frame, slot, font);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Power-1 setup.");
        }

        private static void BuildHud(RectTransform frame, PlayerPowerSlot slot, TMP_FontAsset font)
        {
            RectTransform panel = Rect(frame, HudPanelName, HudPosition, HudSize);
            Image panelImage = Component<Image>(panel.gameObject);
            panelImage.color = new Color(0.035f, 0.055f, 0.09f, 0.88f);
            panelImage.raycastTarget = false;

            Image icon = Component<Image>(Rect(panel, "Icon", new Vector2(-74, 20), new Vector2(56, 56)).gameObject);
            icon.raycastTarget = false;
            TMP_Text kind = Text(panel, "Kind", GamePowerHudView.KindLabel, new Vector2(30, 44), new Vector2(140, 24),
                16, font);
            kind.color = new Color(1f, 0.86f, 0.5f, 1f);
            TMP_Text itemName = Text(panel, "Name", GamePowerHudView.EmptyText, new Vector2(30, 18),
                new Vector2(140, 30), 22, font);
            itemName.fontStyle = FontStyles.Bold;
            itemName.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform gauge = Rect(panel, "Gauge", new Vector2(-14, -20), new Vector2(150, 18));
            Image[] cells = new Image[MaximumGaugeCells];
            const float cellWidth = 26f;
            const float spacing = 4f;
            float firstX = -(MaximumGaugeCells - 1) * (cellWidth + spacing) * 0.5f;
            for (int index = 0; index < MaximumGaugeCells; index++)
            {
                cells[index] = Component<Image>(Rect(gauge, $"Cell {index + 1}",
                    new Vector2(firstX + index * (cellWidth + spacing), 0), new Vector2(cellWidth, 14)).gameObject);
                cells[index].raycastTarget = false;
                cells[index].color = GamePowerHudView.EmptyCellColor;
            }

            TMP_Text gaugeText = Text(panel, "Gauge Count", "0/0", new Vector2(84, -20), new Vector2(48, 24), 16, font);
            TMP_Text key = Text(panel, "Use Key", GamePowerHudView.UsableText, new Vector2(0, -48),
                new Vector2(200, 26), 18, font);

            GamePowerHudView view = Component<GamePowerHudView>(panel.gameObject);
            view.Configure(slot, icon, itemName, gaugeText, key, cells);
            foreach (Component component in panel.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
        }

        private static void EnsureGlyphs(TMP_FontAsset font, string characters)
        {
            if (font.HasCharacters(characters)) return;
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Power-1 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }

        private static T Component<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(owner);
            else Undo.RecordObject(component, "Configure " + typeof(T).Name);
            return component;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            RectTransform rect = existing != null ? existing as RectTransform : null;
            if (existing != null && rect == null) throw new InvalidOperationException(name + " requires RectTransform.");
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                Undo.RegisterCreatedObjectUndo(rect.gameObject, "Create " + name);
                rect.SetParent(parent, false);
            }
            Undo.RecordObject(rect, "Layout " + name);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size,
            float fontSize, TMP_FontAsset font)
        {
            TMP_Text text = Component<TextMeshProUGUI>(Rect(parent, name, position, size).gameObject);
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.92f, 0.96f, 1f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
