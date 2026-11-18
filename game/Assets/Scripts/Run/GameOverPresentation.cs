using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Run
{
    internal sealed class GameOverPresentation
    {
        private const float LetterDelay = 0.12f;
        private const float HopDuration = 0.54f;
        private const float HopHeight = 24f;
        private const float WavePeriod = 2.2f;
        private const string PortraitPath = "Characters/Erpin_Death/Erpin_Death_3";
        private const string TitleMaterialPath = "UiEffects/Game Over Title";

        private readonly CanvasGroup backdrop;
        private readonly TMP_Text title;
        private readonly Texture2D backgroundTexture;
        private readonly Texture2D shadowTexture;
        private readonly Texture2D buttonTexture;
        private readonly Sprite buttonSprite;
        private float waveTime;

        public GameOverPresentation(CanvasGroup titleGroup, Button confirm)
        {
            RectTransform titleRect = (RectTransform)titleGroup.transform;
            titleRect.anchoredPosition = new Vector2(0f, -160.5f);
            titleRect.sizeDelta = new Vector2(940f, 143f);
            title = titleGroup.GetComponentInChildren<TMP_Text>(true);
            title.rectTransform.sizeDelta = titleRect.sizeDelta;
            title.fontSize = 122f;
            title.fontStyle = FontStyles.Normal;
            title.characterSpacing = 3.5f;
            title.color = new Color32(255, 210, 223, 255);
            Material titleMaterial = Resources.Load<Material>(TitleMaterialPath);
            if (titleMaterial != null) title.fontSharedMaterial = titleMaterial;
            title.UpdateMeshPadding();
            title.OnPreRenderText += ApplyLetterOffsets;
            title.ForceMeshUpdate();

            RectTransform backdropRect = NewGraphic<RawImage>("Game Over Backdrop", titleRect.parent,
                Vector2.zero, Vector2.zero, out RawImage background);
            backdropRect.SetSiblingIndex(titleRect.GetSiblingIndex());
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdrop = background.gameObject.AddComponent<CanvasGroup>();
            backdrop.interactable = false;
            backdrop.blocksRaycasts = false;
            backgroundTexture = CreateTexture("Game Over backdrop", 192, 108, BackgroundPixel);
            background.texture = backgroundTexture;

            NewGraphic<RawImage>("Portrait Shadow", backdropRect, new Vector2(0f, -67.5f),
                new Vector2(466f, 73f), out RawImage shadow);
            shadowTexture = CreateTexture("Game Over shadow", 128, 32, (x, y) =>
            {
                float distance = new Vector2(2f * x - 1f, 2f * y - 1f).magnitude;
                return new Color32(17, 12, 25, (byte)Mathf.RoundToInt(168f * (1f - Mathf.SmoothStep(0f, 1f, distance))));
            });
            shadow.texture = shadowTexture;

            NewGraphic<Image>("Erpin Defeat Portrait", backdropRect, new Vector2(0f, 158.5f),
                new Vector2(442f, 471f), out Image portrait);
            portrait.sprite = Resources.Load<Sprite>(PortraitPath);
            portrait.enabled = portrait.sprite != null;
            portrait.preserveAspect = true;

            RectTransform buttonRect = (RectTransform)confirm.transform;
            buttonRect.anchoredPosition = new Vector2(0f, -333f);
            buttonRect.sizeDelta = new Vector2(304f, 76f);
            buttonTexture = CreateTexture("Game Over button", 304, 76, ButtonPixel);
            buttonSprite = Sprite.Create(buttonTexture, new Rect(0f, 0f, 304f, 76f), Vector2.one * 0.5f);
            Image buttonImage = confirm.GetComponent<Image>();
            buttonImage.sprite = buttonSprite;
            buttonImage.type = Image.Type.Simple;
            buttonImage.color = Color.white;
            confirm.targetGraphic = buttonImage;
            if (confirm.TryGetComponent(out Outline outline)) outline.enabled = false;
            ColorBlock colors = confirm.colors;
            colors.normalColor = Color.white;
            // The EventSystem selects this button as soon as it appears. Keep focus visible
            // without washing out the artwork or requiring the mouse to move.
            colors.selectedColor = new Color32(255, 239, 249, 255);
            colors.highlightedColor = new Color32(255, 226, 243, 255);
            colors.pressedColor = new Color32(205, 172, 192, 255);
            confirm.colors = colors;
            TMP_Text label = confirm.GetComponentInChildren<TMP_Text>(true);
            label.rectTransform.sizeDelta = buttonRect.sizeDelta;
            label.fontSize = 32f;
            label.fontStyle = FontStyles.Normal;
            label.color = new Color32(255, 240, 233, 255);
        }

        public void SetOpacity(float opacity) => backdrop.alpha = opacity;

        public void Advance(float seconds)
        {
            waveTime = Mathf.Repeat(waveTime + seconds, WavePeriod);
            ApplyLetterOffsets(title.textInfo);
            title.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        private void ApplyLetterOffsets(TMP_TextInfo textInfo)
        {
            int letter = 0;
            for (int index = 0; index < textInfo.characterCount; index++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[index];
                if (!character.isVisible) continue;

                float phase = Mathf.Clamp01((waveTime - letter++ * LetterDelay) / HopDuration);
                float sine = Mathf.Sin(phase * Mathf.PI);
                Vector3 offset = Vector3.up * (HopHeight * sine * sine);
                Vector3[] vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
                int vertex = character.vertexIndex;
                // TMP keeps these corners at their layout positions even after we update the mesh.
                vertices[vertex] = character.vertex_BL.position + offset;
                vertices[vertex + 1] = character.vertex_TL.position + offset;
                vertices[vertex + 2] = character.vertex_TR.position + offset;
                vertices[vertex + 3] = character.vertex_BR.position + offset;
            }
        }

        private static RectTransform NewGraphic<T>(string name, Transform parent, Vector2 position, Vector2 size,
            out T graphic) where T : Graphic
        {
            var owner = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)owner.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            graphic = owner.AddComponent<T>();
            graphic.raycastTarget = false;
            return rect;
        }

        private static Texture2D CreateTexture(string name, int width, int height, Func<float, float, Color32> sample)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = sample((x + 0.5f) / width, (y + 0.5f) / height);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Color32 BackgroundPixel(float x, float y)
        {
            float vignette = new Vector2((x - 0.5f) * 1.65f, (y - 0.62f) * 1.5f).magnitude;
            Color32 dark = Color32.Lerp(new Color32(52, 35, 59, 255), new Color32(20, 15, 26, 255),
                Mathf.SmoothStep(0f, 1f, vignette));
            float radius = new Vector2((x - 0.5f) * 5f, (y - 0.63f) * 2.8f).magnitude;
            float light = 0.29f * (1f - Mathf.SmoothStep(0f, 1f, radius));
            return Color32.Lerp(dark, new Color32(181, 129, 165, 255), light);
        }

        private static Color32 ButtonPixel(float x, float y)
        {
            float horizontal = Mathf.Max(0f, Mathf.Abs(x - 0.5f) * 304f - 114f);
            float distance = new Vector2(horizontal, (y - 0.5f) * 76f).magnitude;
            Color32 fill = Color32.Lerp(new Color32(64, 43, 69, 255), new Color32(81, 55, 80, 255), y);
            Color32 color = Color32.Lerp(fill, new Color32(193, 147, 170, 255), distance - 33f);
            color.a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(38f - distance));
            return color;
        }

        public void Dispose()
        {
            if (title != null) title.OnPreRenderText -= ApplyLetterOffsets;
            Release(backgroundTexture);
            Release(shadowTexture);
            Release(buttonSprite);
            Release(buttonTexture);
        }

        private static void Release(Object resource)
        {
            if (Application.isPlaying) Object.Destroy(resource);
            else Object.DestroyImmediate(resource);
        }
    }
}
