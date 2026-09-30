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

            SkillVisual lower = ConfigureSkill(panel, "Lower Grade", new Vector2(-52, 14), "SPACE", font);
            SkillVisual high = ConfigureSkill(panel, "High Grade", new Vector2(52, 14), "Q", font);
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

        private static SkillVisual ConfigureSkill(Transform parent, string name, Vector2 position, string key,
            TMP_FontAsset font)
        {
            RectTransform root = Rect(parent, name, position, new Vector2(88, 108));
            Image state = Component<Image>(Rect(root, "Icon", new Vector2(0, 12), new Vector2(72, 72)).gameObject);
            state.color = new Color(0.25f, 0.3f, 0.36f, 0.7f);
            state.raycastTarget = false;

            Image cooldown = Component<Image>(Rect(state.transform, "Cooldown", Vector2.zero, new Vector2(72, 72)).gameObject);
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            cooldown.fillClockwise = false;
            cooldown.color = new Color(0.02f, 0.03f, 0.06f, 0.78f);
            cooldown.raycastTarget = false;

            TMP_Text keyText = Text(state.transform, "Key", key, new Vector2(0, 22), new Vector2(68, 24), 16, font);
            keyText.fontStyle = FontStyles.Bold;
            TMP_Text cooldownText = Text(state.transform, "Cooldown Value", string.Empty, Vector2.zero,
                new Vector2(68, 34), 24, font);
            cooldownText.fontStyle = FontStyles.Bold;
            TMP_Text status = Text(root, "Status", "사용 불가", new Vector2(0, -42), new Vector2(96, 24), 16, font);
            return new SkillVisual(state, cooldown, cooldownText, status);
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
