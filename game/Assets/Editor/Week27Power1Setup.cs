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
        // UX-1: the icon sits in the corner and the vertical gauge stands right beside it at the icon's height.
        // The gauge cells split that height evenly whatever the room count (GamePowerHudView lays them out).
        public const float IconSize = 108f;
        public static readonly Vector2 IconPosition = new(-11, 14);
        public static readonly Vector2 GaugeColumnPosition = new(57, 14);
        public static readonly Vector2 GaugeColumnSize = new(20, IconSize);
        public static readonly Vector2 KeyPosition = new(-11, -54);
        // UX-1: top left, beside the hearts, with a vertical gauge.
        public static Vector2 HudPosition => Week27Ux1Setup.PowerPosition;
        public static Vector2 HudSize => Week27Ux1Setup.PowerSize;
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
            RetiredSetup.Stop(nameof(Week27Power1Setup), RetiredSetup.Items);
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

        internal static void BuildHud(RectTransform frame, PlayerPowerSlot slot, TMP_FontAsset font)
        {
            RectTransform panel = Rect(frame, HudPanelName, HudPosition, HudSize);
            // No dark panel behind the 권능 (earlier builds drew one).
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null) Undo.DestroyObjectImmediate(panelImage);

            // The icon alone says which 권능 is held: the "교주의 권능" title, the name and the "0/4" count are gone.
            RemoveChild(panel, "Kind");
            RemoveChild(panel, "Name");
            RemoveChild(panel, "Gauge Count");
            Image icon = Component<Image>(Rect(panel, "Icon", IconPosition, Vector2.one * IconSize).gameObject);
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            // One pill-shaped bar: a thin outline, and inside it the bar that masks the cells to its round ends.
            Sprite pill = Week13FrontendUiAssets.LoadOrCreatePillSprite();
            RectTransform gauge = Rect(panel, "Gauge", GaugeColumnPosition, GaugeColumnSize);
            ConfigurePill(Component<Image>(gauge.gameObject), pill, GaugeColumnSize.x);
            Vector2 innerSize = GaugeColumnSize - Vector2.one * (GamePowerHudView.GaugeOutline * 2f);
            RectTransform bar = Rect(gauge, "Bar", Vector2.zero, innerSize);
            ConfigurePill(Component<Image>(bar.gameObject), pill, innerSize.x);
            Component<Mask>(bar.gameObject).showMaskGraphic = true;

            Image[] cells = new Image[MaximumGaugeCells];
            for (int index = 0; index < MaximumGaugeCells; index++)
            {
                string cellName = $"Cell {index + 1}";
                // Earlier builds kept the cells straight under the gauge.
                Transform loose = gauge.Find(cellName);
                if (loose != null) Undo.SetTransformParent(loose, bar, "Move " + cellName);
                cells[index] = Component<Image>(Rect(bar, cellName, Vector2.zero, Vector2.zero).gameObject);
                cells[index].raycastTarget = false;
                cells[index].color = GamePowerHudView.EmptyCellColor;
            }
            GamePowerHudView.LayoutGauge(cells, MaximumGaugeCells);

            TMP_Text key = Text(panel, "Use Key", GamePowerHudView.UsableText, KeyPosition,
                new Vector2(Week27Ux1Setup.PowerSize.x, 24), 16, font);

            GamePowerHudView view = Component<GamePowerHudView>(panel.gameObject);
            view.Configure(slot, icon, null, null, key, cells);
            foreach (Component component in panel.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
        }

        // A 9-sliced pill whose round ends are exactly half the bar's width.
        private static void ConfigurePill(Image image, Sprite pill, float width)
        {
            image.sprite = pill;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = Week13FrontendUiAssets.PillBorder / (width * 0.5f);
            image.color = GamePowerHudView.GaugeFrameColor;
            image.raycastTarget = false;
        }

        private static void RemoveChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
        }

        internal static void EnsureGlyphs(TMP_FontAsset font, string characters)
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
