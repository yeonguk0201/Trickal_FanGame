using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud1Setup
    {
        public const string HudRootName = "Game HUD Canvas";
        public const string HeartRowName = "HP Hearts";
        public const string HeartTemplateName = "Heart Template";
        // UX-1 enlarged the hearts and SP and owns their layout values.
        public static Vector2 HeartRowPosition => Week27Ux1Setup.HeartRowPosition;
        public static Vector2 HeartRowSize => Week27Ux1Setup.HeartRowSize;

        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-1 HP and SP")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-1 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerSP playerSP = FindSingle<PlayerSP>(scene, "player SP");
            Health playerHealth = playerSP.GetComponent<Health>();
            PlayerSkill playerSkill = playerSP.GetComponent<PlayerSkill>();
            if (playerHealth == null || playerSkill == null)
                throw new InvalidOperationException("The Game Scene player requires Health and PlayerSkill for HUD-1.");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null)
                throw new InvalidOperationException("Frontend TMP font is missing. Run Setup Frontend Flow first.");
            Sprite heartSprite = Week13FrontendUiAssets.LoadOrCreateHeartSprite();

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-1 HP and SP");

            GameObject canvasObject = Root(scene, HudRootName, typeof(RectTransform));
            Canvas canvas = Component<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = Component<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Component<GraphicRaycaster>(canvasObject).enabled = false;

            RectTransform frame = Rect(canvasObject.transform, "ReferenceFrame", Vector2.zero, new Vector2(1920, 1080));
            Component<FrontendLayout>(frame.gameObject).ApplyLayout();
            Image frameImage = Component<Image>(frame.gameObject);
            frameImage.color = Color.clear;
            frameImage.raycastTarget = false;

            RectTransform panel = Rect(frame, "Survival HUD", Week27Ux1Setup.SurvivalPosition,
                Week27Ux1Setup.SurvivalSize);
            Image panelImage = Component<Image>(panel.gameObject);
            panelImage.color = new Color(0.035f, 0.055f, 0.09f, 0.88f);
            panelImage.raycastTarget = false;

            // HP-2 replaced the HP gauge with a half-heart icon row (docs/idea-implementation/01, D-4).
            RemoveChild(panel, "HP Label");
            RemoveChild(panel, "HP Bar");
            // Changed hearts pop individually; a row-wide colour flash read as a red box behind the hearts.
            RemoveChild(panel, "HP Change Feedback");
            RectTransform heartRow = Rect(panel, HeartRowName, HeartRowPosition, HeartRowSize);
            HorizontalLayoutGroup heartLayout = Component<HorizontalLayoutGroup>(heartRow.gameObject);
            heartLayout.spacing = 4;
            heartLayout.childAlignment = TextAnchor.MiddleLeft;
            heartLayout.childControlWidth = false;
            heartLayout.childControlHeight = false;
            heartLayout.childForceExpandWidth = false;
            heartLayout.childForceExpandHeight = false;
            RectTransform heartTemplate = Rect(heartRow, HeartTemplateName, Vector2.zero,
                Vector2.one * Week27Ux1Setup.HeartMaximumSize);
            Image heartBackground = Component<Image>(Rect(heartTemplate, "Background", Vector2.zero,
                Vector2.zero).gameObject);
            Stretch(heartBackground.rectTransform);
            heartBackground.sprite = heartSprite;
            heartBackground.preserveAspect = true;
            heartBackground.color = GameHudView.HeartEmptyColor;
            heartBackground.raycastTarget = false;
            Image heartFill = Component<Image>(Rect(heartTemplate, "Fill", Vector2.zero, Vector2.zero).gameObject);
            Stretch(heartFill.rectTransform);
            heartFill.sprite = heartSprite;
            heartFill.preserveAspect = true;
            heartFill.type = Image.Type.Filled;
            heartFill.fillMethod = Image.FillMethod.Horizontal;
            heartFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            heartFill.fillAmount = 1f;
            heartFill.color = GameHudView.HeartHealthColor;
            heartFill.raycastTarget = false;
            heartTemplate.gameObject.SetActive(false);

            Text(panel, "SP Label", "SP", Week27Ux1Setup.SpLabelPosition, new Vector2(44, 48), 24, font);
            RectTransform slots = Rect(panel, "SP Slots", Week27Ux1Setup.SpSlotsPosition, Week27Ux1Setup.SpSlotsSize);
            HorizontalLayoutGroup layout = Component<HorizontalLayoutGroup>(slots.gameObject);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            Image slotTemplate = Component<Image>(Rect(slots, "SP Slot Template", Vector2.zero,
                Vector2.one * Week27Ux1Setup.SpSlotSize).gameObject);
            slotTemplate.color = new Color(0.18f, 0.28f, 0.38f, 0.55f);
            slotTemplate.raycastTarget = false;
            slotTemplate.gameObject.SetActive(false);
            TMP_Text spValue = Text(panel, "SP Value", "SP 0 / 0", Week27Ux1Setup.SpValuePosition,
                new Vector2(140, 36), 20, font);
            spValue.alignment = TextAlignmentOptions.Right;

            // Once HUD-2 owns the skill icons (and UX-1 removed the status words), keep its icon instead of
            // bringing back the old lower-grade state box.
            GameHudView view = Component<GameHudView>(canvasObject);
            Image skillState;
            TMP_Text skillText;
            if (frame.Find(Week13Hud2Setup.SkillPanelName) != null && view.LowerGradeSkillState != null)
            {
                skillState = view.LowerGradeSkillState;
                skillText = null;
            }
            else
            {
                RectTransform skillStateRect = Rect(panel, "Lower Skill State", new Vector2(194, -22), new Vector2(76, 48));
                skillState = Component<Image>(skillStateRect.gameObject);
                skillState.raycastTarget = false;
                skillText = Text(skillStateRect, "State", "SPACE", Vector2.zero, new Vector2(160, 32), 16, font);
                skillText.alignment = TextAlignmentOptions.Center;
            }

            view.Configure(playerHealth, playerSP, playerSkill, null, null, null, slots, slotTemplate,
                spValue, skillState, skillText);
            view.ConfigureHearts(heartRow, heartTemplate, Week27Ux1Setup.HeartMaximumSize);

            foreach (Component component in canvasObject.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-1 setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-1 setup complete. HP, repeated SP slots and lower-grade skill readiness are connected to the player.");
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }

        private static GameObject Root(Scene scene, string name, params Type[] types)
        {
            GameObject[] matches = scene.GetRootGameObjects().Where(root => root.name == name).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("Duplicate root: " + name);
            if (matches.Length == 1) return matches[0];
            GameObject created = new(name, types);
            SceneManager.MoveGameObjectToScene(created, scene);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            return created;
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

        private static void RemoveChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
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
