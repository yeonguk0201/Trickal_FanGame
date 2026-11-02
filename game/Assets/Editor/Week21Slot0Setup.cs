using System;
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
    // Slot-0: wires the shared spell slot. Builds the floor pickup Prefab, adds the slot to the Game Scene player
    // and adds one HUD slot left of the skill panel. Re-running updates the same objects instead of adding more.
    public static class Week21Slot0Setup
    {
        public const string PickupPrefabPath = "Assets/Prefabs/SingleUseItemPickup.prefab";
        public const string HudPanelName = "Spell Slot HUD";
        public const float PickupWorldDiameter = 0.45f;
        // UX-1: a third skill-sized tile left of the lower and high grade skills.
        public static Vector2 HudPosition => Week27Ux1Setup.SpellTilePosition;
        public static Vector2 HudSize => Vector2.one * Week27Ux1Setup.SkillIconSize;

        [MenuItem("Trickal Fan Game/Week 21/Setup Slot-0 Spell Slot")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Slot-0 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            SingleUseItemPickup prefab = EnsurePickupPrefab();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerMovement player = FindSingle<PlayerMovement>(scene, "player");
            RunProgress progress = FindSingle<RunProgress>(scene, "RunProgress");
            GameHudView gameHud = FindSingle<GameHudView>(scene, "GameHudView");
            if (gameHud.PlayerHealth == null || gameHud.PlayerHealth.gameObject != player.gameObject)
                throw new InvalidOperationException("The Game HUD must belong to the Game Scene player.");
            if (player.GetComponent<PlayerInventory>() == null)
                throw new InvalidOperationException("The Game Scene player needs a PlayerInventory before Slot-0.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            EnsureGlyphs(font, GameSpellSlotHudView.AllFixedText);
            RectTransform frame = gameHud.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("Game HUD ReferenceFrame is missing.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Slot-0 Spell Slot");

            Component<PlayerSingleUseEffects>(player.gameObject);
            PlayerSpellSlot slot = Component<PlayerSpellSlot>(player.gameObject);
            slot.Configure(progress, prefab);
            EditorUtility.SetDirty(slot);

            BuildHud(frame, slot, font);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Slot-0 setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Slot-0 setup complete: the player holds one shared single-use item slot (Left Shift uses it, " +
                      "touching another swaps and drops the held one) and the HUD shows the slot.");
        }

        public static SingleUseItemPickup EnsurePickupPrefab()
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(Week17Resource1Setup.PlaceholderSpritePath);
            if (sprite == null) throw new InvalidOperationException("The built-in pickup placeholder sprite is missing.");

            bool exists = File.Exists(PickupPrefabPath);
            GameObject root = exists
                ? PrefabUtility.LoadPrefabContents(PickupPrefabPath)
                : new GameObject("Single Use Item Pickup");
            try
            {
                root.layer = LayerMask.NameToLayer("Default");
                root.transform.localScale = Vector3.one * (PickupWorldDiameter / sprite.bounds.size.x);
                SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(root);
                renderer.sprite = sprite;
                renderer.color = SingleUseItemPickup.SpellColor;
                renderer.sortingOrder = TrickalFanGame.Player.PlayerMovement.AboveBodySortingOrder;
                CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
                collider.isTrigger = true;
                collider.radius = sprite.bounds.extents.x;
                SingleUseItemPickup pickup = GetOrAdd<SingleUseItemPickup>(root);
                pickup.ConfigureDisplay(renderer);
                pickup.Configure(null, null, false);

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
            return AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath).GetComponent<SingleUseItemPickup>();
        }

        // The same rounded tile as the HUD-2 skills: artwork masked by the tile, "SHIFT" on a dark strip at the
        // bottom, and a see-through dark overlay while the held spell cannot be used. No name or kind words.
        internal static void BuildHud(RectTransform frame, PlayerSpellSlot slot, TMP_FontAsset font)
        {
            float size = Week27Ux1Setup.SkillIconSize;
            RectTransform panel = Rect(frame, HudPanelName, HudPosition, HudSize);
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null) Undo.DestroyObjectImmediate(panelImage);
            foreach (string obsolete in new[] { "Icon", "Kind", "Name", "Use Key" })
            {
                Transform child = panel.Find(obsolete);
                if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
            }

            RectTransform tileRect = Rect(panel, "Tile", Vector2.zero, Vector2.one * size);
            Image tile = Component<Image>(tileRect.gameObject);
            tile.sprite = Week13FrontendUiAssets.LoadOrCreateRoundedSquareSprite();
            tile.type = Image.Type.Simple;
            tile.preserveAspect = false;
            tile.color = Week27Ux1Setup.SkillTileColor;
            tile.raycastTarget = false;
            Mask mask = Component<Mask>(tileRect.gameObject);
            mask.showMaskGraphic = true;

            Image art = Component<Image>(Rect(tileRect, "Art", Vector2.zero, Vector2.one * size).gameObject);
            art.color = Color.white;
            art.preserveAspect = true;
            art.raycastTarget = false;
            art.transform.SetSiblingIndex(0);

            Image overlay = Component<Image>(Rect(tileRect, "Unusable", Vector2.zero, Vector2.one * size).gameObject);
            overlay.sprite = tile.sprite;
            overlay.preserveAspect = false;
            overlay.color = GameHudView.SkillOverlayColor;
            overlay.raycastTarget = false;
            overlay.transform.SetSiblingIndex(1);

            Image keyBacking = Component<Image>(Rect(tileRect, "Key Backing", new Vector2(0, -size * 0.5f + 12),
                new Vector2(size, 24)).gameObject);
            keyBacking.color = new Color(0f, 0f, 0f, 0.55f);
            keyBacking.raycastTarget = false;
            keyBacking.transform.SetSiblingIndex(2);
            TMP_Text key = Text(tileRect, "Key", GameSpellSlotHudView.KeyLabel, new Vector2(0, -size * 0.5f + 12),
                new Vector2(size - 8, 22), 16, font);
            key.fontStyle = FontStyles.Bold;
            key.color = Color.white;
            key.transform.SetAsLastSibling();

            GameSpellSlotHudView view = Component<GameSpellSlotHudView>(panel.gameObject);
            view.Configure(slot, art, key, overlay);
            foreach (Component component in panel.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
        }

        private static void EnsureGlyphs(TMP_FontAsset font, string characters)
        {
            if (!font.HasCharacters(characters) && !font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Slot-0 HUD glyphs: " + missing);

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
