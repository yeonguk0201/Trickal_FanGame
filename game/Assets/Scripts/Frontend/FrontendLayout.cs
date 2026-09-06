using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Keep the reference frame centered even when a window is not 16:9.
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class FrontendLayout : MonoBehaviour
    {
        public static readonly Vector2 ReferenceSize = new(1920, 1080);
        public static readonly Vector2 SafeMargin = new(64, 54);

        private void OnEnable() => ApplyLayout();
        private void LateUpdate() => ApplyLayout();

        public void ApplyLayout()
        {
            var frame = (RectTransform)transform;
            if (frame.parent is not RectTransform parent) return;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = ReferenceSize;
            float scale = Mathf.Min(parent.rect.width / ReferenceSize.x, parent.rect.height / ReferenceSize.y);
            frame.localScale = Vector3.one * Mathf.Max(0.001f, scale);
        }
    }
}
