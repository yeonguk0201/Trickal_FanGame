using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud4ASetup
    {
        public const string MinimapPanelName = "Minimap HUD";

        [MenuItem("Trickal Fan Game/Week 13/Setup HUD-4A Minimap Foundation")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before HUD-4A setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameHudView gameHud = FindSingle<GameHudView>(scene, "GameHudView");
            RunProgress progress = FindSingle<RunProgress>(scene, "RunProgress");
            if (FindAll<GamePauseArtifactView>(scene).Length != 1)
                throw new InvalidOperationException("Run HUD-3C setup before HUD-4A setup.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            RectTransform frame = gameHud.transform.Find("ReferenceFrame") as RectTransform;
            if (frame == null) throw new InvalidOperationException("Game HUD ReferenceFrame is missing.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup HUD-4A Minimap Foundation");

            RectTransform panel = Rect(frame, MinimapPanelName, new Vector2(786, 396), new Vector2(220, 180));
            Image panelImage = Component<Image>(panel.gameObject);
            panelImage.color = new Color(0.035f, 0.055f, 0.09f, 0.88f);
            panelImage.raycastTarget = false;
            TMP_Text title = Text(panel, "Header", "MAP", new Vector2(0, 70), new Vector2(196, 24), 16, font);
            title.alignment = TextAlignmentOptions.Left;
            title.fontStyle = FontStyles.Bold;

            RectTransform mapRoot = Rect(panel, "Map", new Vector2(0, -10), new Vector2(144, 144));
            mapRoot.SetAsFirstSibling();

            RectTransform connectionRect = Rect(mapRoot, "Connection Template", Vector2.zero, new Vector2(18, 4));
            Image connection = Component<Image>(connectionRect.gameObject);
            connection.color = new Color(0.48f, 0.59f, 0.7f, 1f);
            connection.raycastTarget = false;
            connectionRect.gameObject.SetActive(false);

            RectTransform markerRect = Rect(mapRoot, "Room Marker Template", Vector2.zero, new Vector2(30, 30));
            Image fill = Component<Image>(markerRect.gameObject);
            fill.color = new Color(0.16f, 0.22f, 0.3f, 1f);
            fill.raycastTarget = false;
            RectTransform outlineRect = Rect(markerRect, "Current Outline", Vector2.zero, new Vector2(38, 38));
            outlineRect.SetAsFirstSibling();
            Image outline = Component<Image>(outlineRect.gameObject);
            outline.color = new Color(0.92f, 0.97f, 1f, 1f);
            outline.raycastTarget = false;
            TMP_Text symbol = Text(markerRect, "Current Marker", "P", Vector2.zero, new Vector2(26, 26), 16, font);
            symbol.fontStyle = FontStyles.Bold;
            MinimapRoomMarkerView marker = Component<MinimapRoomMarkerView>(markerRect.gameObject);
            marker.ConfigureVisuals(fill, outline, symbol);
            markerRect.gameObject.SetActive(false);

            GameMinimapView minimap = Component<GameMinimapView>(panel.gameObject);
            minimap.Configure(progress, mapRoot, marker, connection, 48f);

            Transform pauseOverlay = frame.Find(Week13Hud3CSetup.OverlayName);
            if (pauseOverlay != null) pauseOverlay.SetAsLastSibling();
            foreach (Component component in panel.GetComponentsInChildren<Component>(true))
                EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during HUD-4A setup.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Week 13 HUD-4A setup complete. The current room and its generated directional connections are bound to the minimap.");
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = FindAll<T>(scene);
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

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
