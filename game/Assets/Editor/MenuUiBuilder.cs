using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    // Shared Undo-aware builders for the Menu slices (docs/26 §3.5). Every call finds an existing child by name
    // before creating one, so rerunning a Menu setup changes values in place instead of adding duplicates.
    public static class MenuUiBuilder
    {
        // Menu-0 palette taken from the user's magic-library reference screens.
        public static readonly Color Ink = new(0.36f, 0.23f, 0.29f);          // body text on cream paper
        public static readonly Color InkSoft = new(0.52f, 0.38f, 0.44f);      // secondary text
        public static readonly Color RibbonText = new(1f, 0.97f, 0.93f);      // text on purple ribbons
        public static readonly Color ButtonText = new(0.17f, 0.29f, 0.07f);   // text on the lime primary button
        public static readonly Color Accent = new(0.56f, 0.36f, 0.67f);       // plum accent
        public static readonly Color Warning = new(0.80f, 0.27f, 0.27f);      // errors
        public static readonly Color Divider = new(0.91f, 0.78f, 0.66f, 0.8f);
        public static readonly Color HoverRing = new(1f, 0.83f, 0.36f, 1f);   // gold, 2px look
        public static readonly Color FocusRing = new(0.48f, 0.25f, 0.62f, 1f); // plum, 3px look

        public static RectTransform Node(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            var rect = existing as RectTransform;
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
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            return rect;
        }

        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            Undo.RecordObject(rect, "Layout " + rect.name);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect, float outset = 0f)
        {
            Undo.RecordObject(rect, "Stretch " + rect.name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = -Vector2.one * outset;
            rect.offsetMax = Vector2.one * outset;
        }

        public static T Ensure<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(owner);
            else Undo.RecordObject(component, "Configure " + typeof(T).Name);
            return component;
        }

        // A sliced pill keeps round caps when its multiplier matches the sprite height to the shown height.
        public static Image Skin(GameObject owner, string spriteName, Color color, bool raycast = false,
            float fitHeight = 0f, bool preserveAspect = false)
        {
            Image image = Ensure<Image>(owner);
            image.sprite = MenuArtSetup.Load(spriteName);
            image.color = color;
            image.raycastTarget = raycast;
            bool sliced = MenuArtSetup.Borders.ContainsKey(spriteName);
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.fillCenter = true;
            image.preserveAspect = !sliced && preserveAspect;
            image.pixelsPerUnitMultiplier = sliced && fitHeight > 0f ? image.sprite.rect.height / fitHeight : 1f;
            return image;
        }

        public static Image Picture(Transform parent, string name, string spriteName, Vector2 position, Vector2 size,
            Color? color = null)
        {
            RectTransform rect = Node(parent, name, position, size);
            return Skin(rect.gameObject, spriteName, color ?? Color.white, preserveAspect: true);
        }

        public static TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size,
            float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            TMP_Text text = Ensure<TextMeshProUGUI>(Node(parent, name, position, size).gameObject);
            StyleText(text, fontSize, color, alignment);
            if (value != null) text.text = value;
            return text;
        }

        public static void StyleText(TMP_Text text, float fontSize, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            Undo.RecordObject(text, "Style " + text.name);
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        // Outline drawn around a control. Old rectangle borders (Edge0..3) from the temporary UI are replaced.
        public static GameObject Ring(RectTransform owner, string name, string spriteName, Color color, float outset)
        {
            Transform previous = owner.Find(name);
            if (previous != null && previous.GetComponent<Image>() == null)
                Undo.DestroyObjectImmediate(previous.gameObject);
            else if (previous != null)
                // Older setups (Credits-0) add rectangle edges under an existing border; the ring replaces them.
                foreach (Transform edge in previous.Cast<Transform>().Where(child => child.name.StartsWith("Edge")).ToArray())
                    Undo.DestroyObjectImmediate(edge.gameObject);
            RectTransform rect = Node(owner, name, Vector2.zero, Vector2.zero);
            Stretch(rect, outset);
            float height = owner.rect.height + outset * 2f;
            Image image = Skin(rect.gameObject, spriteName, color,
                fitHeight: spriteName == MenuArtSetup.RingPill ? height : 0f);
            rect.SetAsLastSibling();
            rect.gameObject.SetActive(false);
            return rect.gameObject;
        }

        public static void StyleButton(FrontendStartButton button, bool primary, Vector2 size, float fontSize = 26f)
        {
            RectTransform rect = (RectTransform)button.transform;
            Undo.RecordObject(rect, "Size " + rect.name);
            rect.sizeDelta = size;
            Image image = Skin(button.gameObject, primary ? MenuArtSetup.ButtonPrimary : MenuArtSetup.ButtonSecondary,
                Color.white, raycast: true, fitHeight: size.y);
            Undo.RecordObject(button, "Style " + button.name);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = Colors();
            TMP_Text label = rect.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null)
            {
                StyleText(label, fontSize, primary ? ButtonText : Ink);
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(24, 4);
                label.rectTransform.offsetMax = new Vector2(-24, -4);
            }
            // User feedback: no outline on buttons; hover and keyboard focus make the button grow instead.
            RemoveChild(rect, "HoverBorder");
            RemoveChild(rect, "FocusBorder");
            button.ConfigureBorders(null, null);
            Pop(button);
        }

        // Sliders and dropdowns: no outline; the control (or the given part, e.g. the slider handle) grows instead.
        public static void Highlight(Selectable selectable, Transform grow = null, float scale = 1.07f, float press = 0.95f)
        {
            RemoveChild(selectable.transform, "HoverBorder");
            RemoveChild(selectable.transform, "FocusBorder");
            FrontendSelectableHighlight old = selectable.GetComponent<FrontendSelectableHighlight>();
            if (old != null) Undo.DestroyObjectImmediate(old);
            Undo.RecordObject(selectable, "Colors " + selectable.name);
            selectable.colors = Colors();
            MenuPopEffect pop = Ensure<MenuPopEffect>(selectable.gameObject);
            pop.Configure(grow, scale, press);
        }

        // Hover and focus show as rings, so the tint only marks pressed and disabled (45% opacity, docs/16 §4).
        public static ColorBlock Colors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            return colors;
        }

        // Image behind text: a sliced pill sized to the given height.
        public static Image Pill(Transform parent, string name, string spriteName, Vector2 position, Vector2 size,
            Color? color = null)
        {
            RectTransform rect = Node(parent, name, position, size);
            return Skin(rect.gameObject, spriteName, color ?? Color.white, fitHeight: size.y);
        }

        // Anchors a child to the left (x = 0), centre (0.5) or right (1) edge of a parent whose width a layout sets.
        public static void Anchor(RectTransform rect, float x, Vector2 position, Vector2 size)
        {
            Undo.RecordObject(rect, "Anchor " + rect.name);
            rect.anchorMin = rect.anchorMax = new Vector2(x, 0.5f);
            rect.pivot = new Vector2(x, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // Keeps back before front in draw order. Moving only when needed keeps reruns stable.
        public static void DrawBehind(Transform back, Transform front)
        {
            if (back.parent != front.parent) throw new InvalidOperationException($"{back.name} and {front.name} need one parent.");
            if (back.GetSiblingIndex() > front.GetSiblingIndex()) back.SetSiblingIndex(front.GetSiblingIndex());
        }

        public static T Require<T>(Transform parent, string path) where T : Component
        {
            T found = parent.Find(path)?.GetComponent<T>();
            if (found == null) throw new InvalidOperationException($"Missing {parent.name}/{path} ({typeof(T).Name}).");
            return found;
        }

        public static T FindSingle<T>(Scene scene) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"{scene.path} requires one {typeof(T).Name}; found {matches.Length}.");
            return matches[0];
        }

        public static void Deactivate(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null || !child.gameObject.activeSelf) return;
            Undo.RecordObject(child.gameObject, "Hide " + name);
            child.gameObject.SetActive(false);
        }

        // Soft glow behind a control, used for rounded-square controls instead of an outline box. Reuses an
        // existing border object (old ring or edge set) of the same name and puts it at the given draw position.
        public static readonly Color HoverGlow = new(1f, 0.86f, 0.45f, 0.9f);
        public static readonly Color FocusGlow = new(0.62f, 0.42f, 0.88f, 0.95f);
        public static readonly Color SelectedGlow = new(1f, 0.7f, 0.15f, 1f);

        public static GameObject GlowLayer(RectTransform owner, string name, Color color, float outset, int siblingIndex)
        {
            Transform previous = owner.Find(name);
            if (previous != null && previous.GetComponent<Image>() == null)
                Undo.DestroyObjectImmediate(previous.gameObject);
            else if (previous != null)
                foreach (Transform edge in previous.Cast<Transform>().Where(child => child.name.StartsWith("Edge")).ToArray())
                    Undo.DestroyObjectImmediate(edge.gameObject);
            RectTransform rect = Node(owner, name, Vector2.zero, Vector2.zero);
            Stretch(rect, outset);
            Skin(rect.gameObject, MenuArtSetup.Glow, color);
            rect.SetSiblingIndex(Mathf.Min(siblingIndex, owner.childCount - 1));
            rect.gameObject.SetActive(false);
            return rect.gameObject;
        }

        public static void Pop(Selectable selectable) => Ensure<MenuPopEffect>(selectable.gameObject);

        public static void RemoveChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
        }

        // Back buttons carry a left arrow in front of the label, like the reference screens.
        public static void BackArrow(FrontendStartButton button)
        {
            var rect = (RectTransform)button.transform;
            Image arrow = Picture(rect, "ArrowIcon", MenuArtSetup.ArrowLeft, Vector2.zero, new Vector2(20, 30), Accent);
            Anchor(arrow.rectTransform, 0f, new Vector2(30, 0), new Vector2(20, 30));
            TMP_Text label = rect.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null) label.rectTransform.offsetMin = new Vector2(58, 4);
            arrow.transform.SetSiblingIndex(label != null ? label.transform.GetSiblingIndex() : 0);
        }
    }
}
