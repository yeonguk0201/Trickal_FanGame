using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud2Setup
    {
        public const string SkillPanelName = "Skill HUD";

        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-2 Skill States")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-2 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameHudView view = FindSingle<GameHudView>(scene, "GameHudView");
            PlayerSkill lowerSkill = FindSingle<PlayerSkill>(scene, "lower-grade skill");
            PlayerUltimate highSkill = FindSingle<PlayerUltimate>(scene, "high-grade skill");
            if (lowerSkill.gameObject != highSkill.gameObject || lowerSkill != view.LowerGradeSkill)
                throw new InvalidOperationException("HUD-2 skill references must point to the HUD-1 player.");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            RectTransform frame = view.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("HUD-1 ReferenceFrame is missing.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-2 Skill States");

            Transform obsolete = frame.Find("Survival HUD/Lower Skill State");
            if (obsolete != null) Undo.DestroyObjectImmediate(obsolete.gameObject);

            RectTransform panel = Rect(frame, SkillPanelName, new Vector2(746, -428), new Vector2(300, 128));
            Image panelImage = Component<Image>(panel.gameObject);
            panelImage.color = new Color(0.035f, 0.055f, 0.09f, 0.88f);
            panelImage.raycastTarget = false;

            float iconX = Week27Ux1Setup.SkillIconSpacing * 0.5f;
            SkillVisual lower = ConfigureSkill(panel, "Lower Grade", new Vector2(-iconX, 0), "SPACE", font);
            SkillVisual high = ConfigureSkill(panel, "High Grade", new Vector2(iconX, 0), "Q", font);
            view.Configure(view.PlayerHealth, view.PlayerSP, lowerSkill, view.HpFill, view.HpValueText,
                view.HealthFeedback, view.SpSlotsRoot, view.SpSlotTemplate, view.SpValueText, lower.State,
                lower.Status);
            view.ConfigureSkills(highSkill, lower.CooldownFill, high.State, high.CooldownFill,
                high.CooldownText, high.Status);

            foreach (Component component in view.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-2 setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-2 setup complete. Lower/high-grade readiness and high-grade cooldown are connected to the player.");
        }

        // UX-1: both skills are the same rounded tile. The tile masks the artwork, the dark overlay and the key
        // label, so the high grade portrait and the lower grade emblem read as the same size. No status words.
        private static SkillVisual ConfigureSkill(Transform parent, string name, Vector2 position, string key,
            TMP_FontAsset font)
        {
            float size = Week27Ux1Setup.SkillIconSize;
            RectTransform root = Rect(parent, name, position, Vector2.one * size);
            Transform status = root.Find("Status");
            if (status != null) Undo.DestroyObjectImmediate(status.gameObject);

            RectTransform tileRect = Rect(root, "Icon", Vector2.zero, Vector2.one * size);
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

            Image cooldown = Component<Image>(Rect(tileRect, "Cooldown", Vector2.zero, Vector2.one * size).gameObject);
            // A Filled Image needs a sprite: without one Unity ignores the fill and draws the whole square, which
            // kept both icons fully dark and hid the sweep.
            cooldown.sprite = tile.sprite;
            cooldown.preserveAspect = false;
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            // Filled counter-clockwise, the overlay's edge sweeps clockwise as the remaining time shrinks.
            cooldown.fillClockwise = false;
            cooldown.color = GameHudView.SkillOverlayColor;
            cooldown.raycastTarget = false;
            cooldown.transform.SetSiblingIndex(1);

            Image keyBacking = Component<Image>(Rect(tileRect, "Key Backing", new Vector2(0, -size * 0.5f + 12),
                new Vector2(size, 24)).gameObject);
            keyBacking.color = new Color(0f, 0f, 0f, 0.55f);
            keyBacking.raycastTarget = false;
            TMP_Text keyText = Text(tileRect, "Key", key, new Vector2(0, -size * 0.5f + 12), new Vector2(size - 8, 22),
                16, font);
            keyText.fontStyle = FontStyles.Bold;
            TMP_Text cooldownText = Text(tileRect, "Cooldown Value", string.Empty, new Vector2(0, 6),
                new Vector2(size - 8, 40), 34, font);
            cooldownText.fontStyle = FontStyles.Bold;
            keyBacking.transform.SetSiblingIndex(2);
            keyText.transform.SetAsLastSibling();
            cooldownText.transform.SetAsLastSibling();
            return new SkillVisual(art, cooldown, cooldownText, null);
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

        private readonly struct SkillVisual
        {
            public SkillVisual(Image state, Image cooldownFill, TMP_Text cooldownText, TMP_Text status)
            {
                State = state;
                CooldownFill = cooldownFill;
                CooldownText = cooldownText;
                Status = status;
            }

            public Image State { get; }
            public Image CooldownFill { get; }
            public TMP_Text CooldownText { get; }
            public TMP_Text Status { get; }
        }
    }
}
