using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class RunLoadingScreenView : MonoBehaviour
    {
        public const int SortingOrder = 32760;
        private Canvas canvas;
        private RectTransform frame;
        private RectTransform fill;
        private TMP_Text percent;
        private RunLoadingScreenStyle style;
        private RunLoadingLayout layout;
        public bool IsVisible => canvas != null && canvas.enabled;
        public float Progress { get; private set; }
        public RunLoadingLayout Layout => layout;
        public RectTransform Frame => frame;

        public void Build(RunLoadingScreenStyle configuredStyle)
        {
            if (canvas != null) return;
            style = configuredStyle;
            layout = style.ReadLayout();
            GameObject canvasObject = new("Loading Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = FrontendLayout.ReferenceSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasObject.AddComponent<GraphicRaycaster>();
            Image background = canvasObject.AddComponent<Image>();
            background.color = Hex("#a9db96");
            background.raycastTarget = true;
            RectTransform pattern = Rect(canvasObject.transform, "Check, Trees and Stars", Vector2.zero, Vector2.zero);
            pattern.anchorMin = Vector2.zero;
            pattern.anchorMax = Vector2.one;
            Shape(pattern, RunLoadingScreenGraphic.Shape.Pattern, Color.white);
            frame = Rect(canvasObject.transform, "Reference Frame", Vector2.zero, FrontendLayout.ReferenceSize);
            frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.one * 0.5f;
            frame.gameObject.AddComponent<FrontendLayout>().ApplyLayout();
            foreach (RunLoadingLayer layer in layout.layers.OrderBy(l => l.z)) BuildLayer(layer);
            SetProgress(0f);
            SetVisible(false);
        }

        private void BuildLayer(RunLoadingLayer layer)
        {
            RectTransform root = Rect(frame, layer.id, new Vector2(layer.x, -layer.y), new Vector2(layer.w, layer.h));
            // CSS rotates about the layer center; exported x/y is the unrotated top-left.
            root.pivot = Vector2.one * 0.5f;
            root.anchoredPosition += new Vector2(layer.w, -layer.h) * 0.5f;
            root.localRotation = Quaternion.Euler(0f, 0f, -layer.rotation);
            root.gameObject.SetActive(layer.visible);
            switch (layer.id)
            {
                case "portrait": BuildPortrait(root, layer); break;
                case "bubble":
                    Box(root, "Shadow", 0, 8, layer.w, layer.h, new Color(0.32f, 0.42f, 0.26f, 0.27f), 60);
                    RectTransform tail = Box(root, "Tail", layer.w - 100, layer.h - 10, 36, 30, Color.white, 0);
                    tail.GetComponent<RunLoadingScreenGraphic>().Kind = RunLoadingScreenGraphic.Shape.Tail;
                    tail.GetComponent<RunLoadingScreenGraphic>().SetVerticesDirty();
                    Box(root, "Bubble", 0, 0, layer.w, layer.h, Color.white, 60);
                    Label(root, layer, false);
                    break;
                case "strap": Box(root, "Ribbon", 0, 0, layer.w, layer.h, Hex("#7ab439"), 15); break;
                case "epithet": Label(root, layer, true); break;
                case "name":
                    Box(root, "Green Border", 0, 0, layer.w, layer.h, Hex("#589d36"), 32);
                    RectTransform inner = Box(root, "White Panel", 16, 16, layer.w - 32, layer.h - 32, Color.white, 16);
                    RectTransform stitch = Rect(inner, "Dashed Border", Vector2.zero, inner.sizeDelta);
                    Shape(stitch, RunLoadingScreenGraphic.Shape.DashedBorder, Hex("#d7eea4"));
                    Label(root, layer, false);
                    break;
                case "bar":
                    Box(root, "Dark Border", 0, 0, layer.w, layer.h, Hex("#42593c"), 9);
                    RectTransform track = Box(root, "Track", 4, 4, layer.w - 8, layer.h - 8, Hex("#e0ecc4"), 5);
                    track.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                    fill = Box(track, "Fill", 0, 0, layer.w - 8, layer.h - 8, Hex("#bada52"), 5);
                    break;
                case "percent": percent = Label(root, layer, true); break;
                default: throw new InvalidOperationException($"Unsupported loading layer: {layer.id}.");
            }
        }

        private void BuildPortrait(RectTransform parent, RunLoadingLayer layer)
        {
            Shape(parent, RunLoadingScreenGraphic.Shape.Circle, Hex("#f1ffba"));
            RectTransform clip = Rect(parent, "Portrait Clip", new Vector2(11, -11), new Vector2(layer.w - 22, layer.h - 22));
            Shape(clip, RunLoadingScreenGraphic.Shape.Circle, Hex("#e3efb0"));
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            // Match HTML flex sizing (width shrinks to the content width; height remains 116%).
            Vector2 size = new(clip.sizeDelta.x, clip.sizeDelta.y * 1.16f);
            RectTransform art = Rect(clip, "Character", Vector2.zero, size);
            art.anchorMin = art.anchorMax = art.pivot = Vector2.one * 0.5f;
            art.anchoredPosition = new Vector2(layer.portraitX, -size.y * 0.06f - layer.portraitY);
            art.localScale = Vector3.one * (layer.portraitZoom / 100f);
            Image image = art.gameObject.AddComponent<Image>();
            image.sprite = style.portrait;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private TMP_Text Label(RectTransform parent, RunLoadingLayer layer, bool outlined)
        {
            RectTransform rect = Rect(parent, "Text", Vector2.zero, new Vector2(layer.w, layer.h));
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = style.font;
            text.text = layer.text ?? "";
            text.fontSize = layer.font;
            text.color = Hex(layer.color);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            text.raycastTarget = false;
            if (outlined)
            {
                text.outlineColor = Color.white;
                text.outlineWidth = 0.18f;
            }
            return text;
        }

        public void SetVisible(bool visible)
        {
            if (canvas == null) return;
            // Also remove hidden graphics from raycasting; a disabled Canvas alone can leave a raycaster active.
            canvas.enabled = visible;
            canvas.gameObject.SetActive(visible);
        }

        public void SetProgress(float value)
        {
            Progress = Mathf.Clamp01(value);
            if (fill != null)
            {
                RectTransform track = (RectTransform)fill.parent;
                fill.sizeDelta = new Vector2(track.sizeDelta.x * Progress, fill.sizeDelta.y);
                fill.gameObject.SetActive(Progress > 0f);
            }
            if (percent != null) percent.text = $"{Mathf.RoundToInt(Progress * 100f)}%";
        }

        private static Color Hex(string value) => ColorUtility.TryParseHtmlString(value, out Color c) ? c : Color.white;
        private static RectTransform Box(Transform parent, string name, float x, float y, float w, float h,
            Color color, float radius)
        {
            RectTransform rect = Rect(parent, name, new Vector2(x, -y), new Vector2(w, h));
            RunLoadingScreenGraphic graphic = Shape(rect, RunLoadingScreenGraphic.Shape.RoundedBox, color);
            graphic.Radius = radius;
            return rect;
        }

        private static RunLoadingScreenGraphic Shape(RectTransform rect, RunLoadingScreenGraphic.Shape kind, Color color)
        {
            RunLoadingScreenGraphic graphic = rect.gameObject.AddComponent<RunLoadingScreenGraphic>();
            graphic.Kind = kind;
            graphic.color = color;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
