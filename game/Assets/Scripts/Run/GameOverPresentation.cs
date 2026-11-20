using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Run
{
    // The title card after the iris closes. GAME OVER (UX-0) shows 에르핀's defeat pose on a plum backdrop; VICTORY
    // (last boss cleared) shows her jumping for joy on a warm gold one with the same layout and letter hop.
    internal sealed class GameOverPresentation
    {
        public const string GameOverTitle = "GAME OVER";
        public const string VictoryTitle = "VICTORY";

        private const float LetterDelay = 0.12f;
        private const float HopDuration = 0.54f;
        private const float HopHeight = 24f;
        private const float WavePeriod = 2.2f;
        private const string PortraitPath = "Characters/Erpin_Death/Erpin_Death_3";
        // The arms-up frame of her victory jump.
        public const string VictoryPortraitPath = "Characters/Erpin_Victory/Erpin_Victory_3";
        private const string TitleMaterialPath = "UiEffects/Game Over Title";

        private readonly CanvasGroup backdrop;
        private readonly TMP_Text title;
        private readonly Texture2D backgroundTexture;
        private readonly Texture2D shadowTexture;
        private readonly Texture2D buttonTexture;
        private readonly Sprite buttonSprite;
        private readonly Material victoryTitleMaterial;
        private readonly bool victory;
        private float waveTime;

        public bool IsVictory => victory;

        public GameOverPresentation(CanvasGroup titleGroup, Button confirm, bool isVictory = false)
        {
            victory = isVictory;
            RectTransform titleRect = (RectTransform)titleGroup.transform;
            titleRect.anchoredPosition = new Vector2(0f, -160.5f);
            titleRect.sizeDelta = new Vector2(940f, 143f);
            title = titleGroup.GetComponentInChildren<TMP_Text>(true);
            title.rectTransform.sizeDelta = titleRect.sizeDelta;
            title.fontSize = 122f;
            title.fontStyle = FontStyles.Normal;
            title.characterSpacing = 3.5f;
            title.text = victory ? VictoryTitle : GameOverTitle;
            title.color = victory ? new Color32(255, 228, 150, 255) : new Color32(255, 210, 223, 255);
            Material titleMaterial = Resources.Load<Material>(TitleMaterialPath);
            if (titleMaterial != null && victory)
            {
                // Same outline and underlay as GAME OVER, recoloured to a warm brown so the gold letters stay legible.
                victoryTitleMaterial = new Material(titleMaterial) { name = "Victory Title" };
                victoryTitleMaterial.SetColor("_OutlineColor", new Color(0.33f, 0.17f, 0.06f, 1f));
                victoryTitleMaterial.SetColor("_UnderlayColor", new Color(0.86f, 0.5f, 0.18f, 1f));
                titleMaterial = victoryTitleMaterial;
            }
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
            backgroundTexture = CreateTexture(victory ? "Victory backdrop" : "Game Over backdrop", 192, 108,
                victory ? VictoryBackgroundPixel : BackgroundPixel);
            background.texture = backgroundTexture;

            NewGraphic<RawImage>("Portrait Shadow", backdropRect, new Vector2(0f, -67.5f),
                new Vector2(466f, 73f), out RawImage shadow);
            shadowTexture = CreateTexture("Game Over shadow", 128, 32, (x, y) =>
            {
                float distance = new Vector2(2f * x - 1f, 2f * y - 1f).magnitude;
                return new Color32(17, 12, 25, (byte)Mathf.RoundToInt(168f * (1f - Mathf.SmoothStep(0f, 1f, distance))));
            });
            shadow.texture = shadowTexture;

            NewGraphic<Image>(victory ? "Erpin Victory Portrait" : "Erpin Defeat Portrait", backdropRect,
                new Vector2(0f, 158.5f), new Vector2(442f, 471f), out Image portrait);
            portrait.sprite = Resources.Load<Sprite>(victory ? VictoryPortraitPath : PortraitPath);
            portrait.enabled = portrait.sprite != null;
            portrait.preserveAspect = true;

            RectTransform buttonRect = (RectTransform)confirm.transform;
            buttonRect.anchoredPosition = new Vector2(0f, -333f);
            buttonRect.sizeDelta = new Vector2(304f, 76f);
            buttonTexture = CreateTexture(victory ? "Victory button" : "Game Over button", 304, 76,
                victory ? VictoryButtonPixel : ButtonPixel);
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
            colors.selectedColor = victory ? new Color32(255, 248, 232, 255) : new Color32(255, 239, 249, 255);
            colors.highlightedColor = victory ? new Color32(255, 240, 214, 255) : new Color32(255, 226, 243, 255);
            colors.pressedColor = victory ? new Color32(210, 186, 150, 255) : new Color32(205, 172, 192, 255);
            confirm.colors = colors;
            TMP_Text label = confirm.GetComponentInChildren<TMP_Text>(true);
            label.rectTransform.sizeDelta = buttonRect.sizeDelta;
            label.fontSize = 32f;
            label.fontStyle = FontStyles.Normal;
            label.color = victory ? new Color32(255, 246, 226, 255) : new Color32(255, 240, 233, 255);
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

        // Warm brown with a gold glow behind the portrait and a few soft rays fanning out from it.
        private static Color32 VictoryBackgroundPixel(float x, float y)
        {
            float vignette = new Vector2((x - 0.5f) * 1.65f, (y - 0.62f) * 1.5f).magnitude;
            Color32 dark = Color32.Lerp(new Color32(74, 46, 30, 255), new Color32(28, 17, 12, 255),
                Mathf.SmoothStep(0f, 1f, vignette));
            Vector2 fromPortrait = new((x - 0.5f) * 1.78f, y - 0.63f);
            float angle = Mathf.Atan2(fromPortrait.y, fromPortrait.x);
            float rays = 0.07f * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(angle * 12f), 6f) *
                         (1f - Mathf.SmoothStep(0.1f, 0.9f, fromPortrait.magnitude));
            float radius = new Vector2((x - 0.5f) * 5f, (y - 0.63f) * 2.8f).magnitude;
            float light = 0.36f * (1f - Mathf.SmoothStep(0f, 1f, radius)) + rays;
            return Color32.Lerp(dark, new Color32(255, 196, 108, 255), light);
        }

        private static Color32 VictoryButtonPixel(float x, float y)
        {
            float horizontal = Mathf.Max(0f, Mathf.Abs(x - 0.5f) * 304f - 114f);
            float distance = new Vector2(horizontal, (y - 0.5f) * 76f).magnitude;
            Color32 fill = Color32.Lerp(new Color32(88, 54, 28, 255), new Color32(116, 74, 36, 255), y);
            Color32 color = Color32.Lerp(fill, new Color32(236, 190, 110, 255), distance - 33f);
            color.a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(38f - distance));
            return color;
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
            // The backdrop (and the portrait in it) belongs to this presentation; a VICTORY after a GAME OVER test
            // run builds its own.
            if (backdrop != null) Release(backdrop.gameObject);
            Release(backgroundTexture);
            Release(shadowTexture);
            Release(buttonSprite);
            Release(buttonTexture);
            if (victoryTitleMaterial != null) Release(victoryTitleMaterial);
        }

        private static void Release(Object resource)
        {
            if (Application.isPlaying) Object.Destroy(resource);
            else Object.DestroyImmediate(resource);
        }
    }
}
