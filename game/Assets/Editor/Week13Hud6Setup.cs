using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud6Setup
    {
        public const string RootName = "World Combat Numbers";
        public const string TemplateName = "Combat Number Template";
        public const string HealthBarTemplateName = "Enemy Health Bar Template";

        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-6 World Combat Numbers")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-6 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            Sprite fillSprite = Week13FrontendUiAssets.LoadPlaceholderFillSprite();
            EnsureGlyphs(font);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-6 World Combat Numbers");

            GameObject root = Root(scene, RootName);
            WorldCombatNumberPool pool = Component<WorldCombatNumberPool>(root);
            Transform existingTemplate = root.transform.Find(TemplateName);
            GameObject templateObject = existingTemplate != null ? existingTemplate.gameObject : new GameObject(TemplateName);
            if (existingTemplate == null)
            {
                Undo.RegisterCreatedObjectUndo(templateObject, "Create combat number template");
                templateObject.transform.SetParent(root.transform, false);
            }

            TMP_Text template = Component<TextMeshPro>(templateObject);
            template.font = font;
            template.text = "0";
            template.fontSize = 4.5f;
            template.fontStyle = FontStyles.Bold;
            template.alignment = TextAlignmentOptions.Center;
            template.textWrappingMode = TextWrappingModes.NoWrap;
            template.overflowMode = TextOverflowModes.Overflow;
            template.color = Color.white;
            template.GetComponent<Renderer>().sortingOrder = 60;
            templateObject.SetActive(false);

            Transform existingHealthBar = root.transform.Find(HealthBarTemplateName);
            GameObject healthBarObject = existingHealthBar != null
                ? existingHealthBar.gameObject
                : new GameObject(HealthBarTemplateName, typeof(RectTransform));
            if (existingHealthBar == null)
            {
                Undo.RegisterCreatedObjectUndo(healthBarObject, "Create enemy health bar template");
                healthBarObject.transform.SetParent(root.transform, false);
            }
            RectTransform healthBarRect = healthBarObject.GetComponent<RectTransform>();
            Undo.RecordObject(healthBarRect, "Layout enemy health bar template");
            healthBarRect.sizeDelta = new Vector2(120f, 14f);
            healthBarRect.localPosition = Vector3.zero;
            healthBarRect.localRotation = Quaternion.identity;
            healthBarRect.localScale = Vector3.one * 0.01f;
            Canvas healthBarCanvas = Component<Canvas>(healthBarObject);
            healthBarCanvas.renderMode = RenderMode.WorldSpace;
            healthBarCanvas.sortingOrder = 50;
            CanvasGroup healthBarGroup = Component<CanvasGroup>(healthBarObject);
            healthBarGroup.alpha = 0f;
            healthBarGroup.interactable = false;
            healthBarGroup.blocksRaycasts = false;
            Image background = Component<Image>(healthBarObject);
            background.color = new Color(0.06f, 0.07f, 0.09f, 0.92f);
            background.raycastTarget = false;
            RectTransform fillRect = Rect(healthBarRect, "Fill", Vector2.zero, new Vector2(112f, 8f));
            Image fill = Component<Image>(fillRect.gameObject);
            fill.sprite = fillSprite;
            fill.color = new Color(0.82f, 0.16f, 0.18f, 1f);
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            EnemyWorldHealthBarView healthBarView = Component<EnemyWorldHealthBarView>(healthBarObject);
            healthBarView.Configure(healthBarGroup, fill);
            healthBarObject.SetActive(false);

            pool.Configure(template, healthBarView, 0.8f, 0.65f, 0.18f);

            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-6 setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-6 setup complete. Pooled world numbers observe enemy damage and regular enemies receive damage-triggered world health bars.");
        }

        private static void EnsureGlyphs(TMP_FontAsset font)
        {
            const string required = "+0123456789";
            if (!font.HasCharacters(required) && !font.TryAddCharacters(required, out string missing))
                throw new InvalidOperationException("Missing HUD-6 glyphs: " + missing);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }

        private static GameObject Root(Scene scene, string name)
        {
            GameObject[] matches = scene.GetRootGameObjects().Where(root => root.name == name).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("Duplicate root: " + name);
            if (matches.Length == 1) return matches[0];
            GameObject created = new(name);
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
    }
}
