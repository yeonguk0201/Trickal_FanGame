using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud4BSetup
    {
        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-4B Explored Minimap")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-4B setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameMinimapView minimap = FindSingle<GameMinimapView>(scene, "GameMinimapView");
            MinimapRoomMarkerView marker = minimap.MarkerTemplate;
            if (marker == null) throw new InvalidOperationException("Run HUD-4A setup before HUD-4B setup.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-4B Explored Minimap");

            RectTransform badgeRect = Rect(marker.transform, "Cleared Badge", new Vector2(10, -10), new Vector2(14, 14));
            TMP_Text clearedBadge = Component<TextMeshProUGUI>(badgeRect.gameObject);
            clearedBadge.font = font;
            clearedBadge.text = "V";
            clearedBadge.fontSize = 12;
            clearedBadge.fontStyle = FontStyles.Bold;
            clearedBadge.alignment = TextAlignmentOptions.Center;
            clearedBadge.color = new Color(0.8f, 1f, 0.82f, 1f);
            clearedBadge.raycastTarget = false;
            clearedBadge.textWrappingMode = TextWrappingModes.NoWrap;
            clearedBadge.gameObject.SetActive(false);

            marker.ConfigureVisuals(marker.RoomFill, marker.CurrentOutline, marker.SymbolText, clearedBadge);
            foreach (Component component in minimap.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-4B setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-4B setup complete. Explored rooms persist, special door types are visible, and pre-entry clear badges stay hidden.");
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<T>(true)).ToArray();
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
    }
}
