using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud3ASetup
    {
        public const string ArtifactPanelName = "Artifact HUD";

        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-3A Artifact List")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-3A setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameHudView gameHud = FindSingle<GameHudView>(scene, "GameHudView");
            PlayerInventory inventory = FindSingle<PlayerInventory>(scene, "player inventory");
            if (inventory.gameObject != gameHud.PlayerHealth.gameObject)
                throw new InvalidOperationException("HUD-3A inventory must belong to the HUD player.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            RectTransform frame = gameHud.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("Game HUD ReferenceFrame is missing.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-3A Artifact List");

            RectTransform panel = Rect(frame, ArtifactPanelName, new Vector2(-752, -430), new Vector2(288, 112));
            Image panelImage = Component<Image>(panel.gameObject);
            panelImage.color = new Color(0.035f, 0.055f, 0.09f, 0.88f);
            panelImage.raycastTarget = false;

            RectTransform slots = Rect(panel, "Slots", new Vector2(-8, 0), new Vector2(272, 104));
            GridLayoutGroup grid = Component<GridLayoutGroup>(slots.gameObject);
            grid.cellSize = new Vector2(48, 48);
            grid.spacing = new Vector2(8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.padding = new RectOffset();

            RectTransform templateRect = Rect(slots, "Artifact Slot Template", Vector2.zero, new Vector2(48, 48));
            Image icon = Component<Image>(templateRect.gameObject);
            icon.color = new Color(0.36f, 0.4f, 0.46f, 1f);
            icon.raycastTarget = false;
            TMP_Text iconText = Text(templateRect, "Stable ID", "00", Vector2.zero, new Vector2(44, 32), 20, font);
            iconText.fontStyle = FontStyles.Bold;
            RectTransform badgeRect = Rect(templateRect, "Stack Badge", new Vector2(14, -14), new Vector2(20, 20));
            Image badge = Component<Image>(badgeRect.gameObject);
            badge.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);
            badge.raycastTarget = false;
            TMP_Text stackText = Text(badgeRect, "Count", "1", Vector2.zero, new Vector2(20, 20), 16, font);
            stackText.fontStyle = FontStyles.Bold;
            ArtifactHudSlotView slotTemplate = Component<ArtifactHudSlotView>(templateRect.gameObject);
            slotTemplate.ConfigureVisuals(icon, iconText, badge, stackText);
            templateRect.gameObject.SetActive(false);

            TMP_Text overflow = Text(panel, "Overflow", string.Empty, new Vector2(120, -40),
                new Vector2(48, 24), 20, font);
            overflow.fontStyle = FontStyles.Bold;
            overflow.color = new Color(1f, 0.83f, 0.3f, 1f);
            overflow.gameObject.SetActive(false);

            GameArtifactHudView artifactHud = Component<GameArtifactHudView>(panel.gameObject);
            artifactHud.Configure(inventory, slots, slotTemplate, overflow, 10);

            foreach (Component component in panel.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-3A setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-3A setup complete. Acquisition-order artifact slots and stack badges are connected to the player inventory.");
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
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
