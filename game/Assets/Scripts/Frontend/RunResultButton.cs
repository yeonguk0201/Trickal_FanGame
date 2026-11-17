using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public static class RunResultButton
    {
        public static Button Create(Transform parent, TMP_FontAsset font, string label, bool primary)
        {
            Image face = RunResultStageView.Plate(parent, label, Vector2.zero, new Vector2(360, 64), Color.white);
            face.raycastTarget = true;
            Button button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            RunResultStageView.Text(face.transform, font, "Label", label, 24, 0, 0, 344,
                new Color(0.025f, 0.06f, 0.08f));
            Apply(button, primary);
            return button;
        }

        public static void Apply(Button button, bool primary)
        {
            if (button is FrontendStartButton bordered)
                bordered.ConfigureBorders(null, null);
            if (button.targetGraphic is not Image image) return;

            image.sprite = RunResultStageView.Rounded;
            image.type = Image.Type.Sliced;
            image.color = primary ? new Color(1f, 0.79f, 0.34f) : new Color(1f, 0.91f, 0.63f);
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.83f);
            colors.selectedColor = new Color(1f, 0.88f, 0.69f);
            colors.pressedColor = new Color(0.85f, 0.74f, 0.55f);
            button.colors = colors;
        }
    }
}
