using TMPro;
using UnityEngine;

namespace TrickalFanGame.Character
{
    // How an NPC standing in a room is drawn (2026-10-09): below the player, so the player is never hidden behind it.
    public static class NpcPresentation
    {
        // The player's sprite is 0 and its flight shadow -1; floors are -100 and pits -10.
        public const int BodySortingOrder = -2;
    }

    // A speech bubble above an NPC: a white box with a black outline and a tail, sized to its line. The owner shows
    // it while the player is near and hides it otherwise.
    [DisallowMultipleComponent]
    public sealed class NpcSpeechBubble : MonoBehaviour
    {
        public const float OutlineWidth = 0.05f;
        public const float TailSize = 0.2f;
        public static readonly Vector2 Padding = new(0.22f, 0.13f);
        public static readonly Color FillColor = Color.white;
        public static readonly Color OutlineColor = Color.black;

        [SerializeField] private SpriteRenderer outline;
        [SerializeField] private SpriteRenderer fill;
        [SerializeField] private SpriteRenderer tailOutline;
        [SerializeField] private SpriteRenderer tailFill;
        [SerializeField] private TextMeshPro text;

        public SpriteRenderer Outline => outline;
        public SpriteRenderer Fill => fill;
        public SpriteRenderer TailOutline => tailOutline;
        public SpriteRenderer TailFill => tailFill;
        public TextMeshPro Text => text;
        public bool IsShown => gameObject.activeSelf;
        public string Line => text != null ? text.text : string.Empty;

        public void Configure(SpriteRenderer configuredOutline, SpriteRenderer configuredFill,
            SpriteRenderer configuredTailOutline, SpriteRenderer configuredTailFill, TextMeshPro configuredText)
        {
            outline = configuredOutline;
            fill = configuredFill;
            tailOutline = configuredTailOutline;
            tailFill = configuredTailFill;
            text = configuredText;
        }

        // The bubble's own position is the tip of its tail; the box grows upward from it.
        public void Show(string line)
        {
            if (text == null || fill == null || outline == null) return;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (text.text != line || fill.size == Vector2.zero)
            {
                text.text = line;
                Layout(text.GetPreferredValues(line));
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Layout(Vector2 textSize)
        {
            Vector2 boxSize = textSize + Padding * 2f;
            float tailReach = TailSize * 0.5f * Mathf.Sqrt(2f);
            Vector3 boxCenter = new(0f, tailReach + boxSize.y * 0.5f, 0f);
            fill.size = boxSize;
            outline.size = boxSize + Vector2.one * (OutlineWidth * 2f);
            fill.transform.localPosition = boxCenter;
            outline.transform.localPosition = boxCenter;
            text.rectTransform.sizeDelta = textSize;
            text.rectTransform.localPosition = boxCenter;
            // The tail is a square turned on its corner, centred on the box's bottom edge. The white one covers the
            // box outline where they cross, so the outline runs around box and tail as one shape.
            Vector3 tailCenter = new(0f, tailReach, 0f);
            if (tailFill != null) tailFill.transform.localPosition = tailCenter;
            if (tailOutline != null) tailOutline.transform.localPosition = tailCenter;
        }
    }
}
