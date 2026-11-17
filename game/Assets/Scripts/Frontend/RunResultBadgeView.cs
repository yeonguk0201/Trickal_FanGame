using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class RunResultBadgeView : MonoBehaviour
    {
        private const int SparkCount = 18;
        private const float WordY = -248f;
        private static readonly float[] VictoryAdvance = { 71, 42, 76, 72, 82, 76, 73 };
        private static readonly float[] DefeatAdvance = { 82, 69, 67, 69, 80, 74 };
        private static readonly Dictionary<string, Sprite[]> LetterSprites = new();
        private static Sprite maskSprite;

        private readonly Image[] letters = new Image[7];
        private readonly Image[] sparks = new Image[SparkCount];
        private readonly Vector2[] letterPositions = new Vector2[7];
        private Image portrait;
        private RunResultPortraitClip portraitClip;
        private RectTransform portraitMask;
        private Vector2 portraitOffset;
        private float portraitScale = 1f;
        private float crestScale = 1f;
        private bool customPortraitLayout;
        private Image crest;
        private Image aura;
        private TMP_Text dialogue;
        private int letterCount;
        private bool victory;
        private float elapsed;

        public static RunResultBadgeView CreateVictory(Transform parent, Sprite character) =>
            Create(parent, character, true);

        public static RunResultBadgeView CreateDefeat(Transform parent, Sprite character) =>
            Create(parent, character, false);

        public void SetPortrait(Sprite character)
        {
            portrait.sprite = character;
            portrait.enabled = character != null;
        }

        public void SetCrestLayout(Vector2 position, float scale)
        {
            crestScale = Mathf.Max(0.01f, scale);
            crest.rectTransform.anchoredPosition = position;
            crest.rectTransform.localScale = new Vector3(crestScale, crestScale, 1f);
            portraitMask.anchoredPosition = position + new Vector2(-5f, 3f) * crestScale;
            portraitMask.localScale = crest.rectTransform.localScale;
            ApplyPortraitLayout();
        }

        public void SetPortraitLayout(Vector2 offset, float scale)
        {
            customPortraitLayout = true;
            portraitOffset = offset;
            portraitScale = Mathf.Max(0.01f, scale);
            ApplyPortraitLayout();
        }

        private void ApplyPortraitLayout()
        {
            float scale = portraitScale / crestScale;
            portrait.rectTransform.anchoredPosition = portraitOffset / crestScale;
            portrait.rectTransform.localScale = new Vector3(scale, scale, 1f);
            portraitClip.Refresh();
        }

        public void SetDialogue(TMP_FontAsset font, string text)
        {
            if (dialogue == null)
            {
                Image bubble = RunResultStageView.Plate(transform, "Dialogue Bubble",
                    new Vector2(240, 330), new Vector2(420, 100), Color.white,
                    UserArtwork.Load("run-result-dialogue-bubble"));
                if (bubble.sprite == null)
                {
                    bubble.sprite = RunResultStageView.Rounded;
                    bubble.type = Image.Type.Sliced;
                    bubble.color = new Color(1f, 0.92f, 0.74f);
                }

                dialogue = RunResultStageView.Text(bubble.transform, font, "Dialogue", "", 26,
                    8, 0, 362, new Color(0.20f, 0.14f, 0.11f));
                dialogue.enableAutoSizing = true;
                dialogue.fontSizeMin = 18;
                dialogue.fontSizeMax = 26;
            }

            dialogue.text = text;
            dialogue.transform.parent.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
        }

        public void SetOutcome(bool cleared)
        {
            victory = cleared;
            elapsed = 0f;
            crest.sprite = UserArtwork.Load(cleared ? "run-result-crest" : "run-result-defeat-crest");
            portrait.rectTransform.sizeDelta = Vector2.one * (cleared ? 510f : 455f);
            if (!customPortraitLayout)
                portraitOffset = new Vector2(0f, cleared ? 10f : -3f);
            ApplyPortraitLayout();

            string word = cleared ? "VICTORY" : "DEFEAT";
            float[] advances = cleared ? VictoryAdvance : DefeatAdvance;
            Sprite[] glyphs = GetLetters(word);
            letterCount = glyphs.Length;
            float width = 0f;
            foreach (float advance in advances) width += advance;
            float x = -width / 2f;

            for (int i = 0; i < letters.Length; i++)
            {
                bool shown = i < letterCount;
                letters[i].enabled = shown;
                if (!shown) continue;
                letters[i].name = $"Letter {i + 1} {word[i]}";
                letters[i].sprite = glyphs[i];
                letterPositions[i] = new Vector2(x + advances[i] / 2f, 0);
                x += advances[i];
            }
            Animate();
        }

        private void OnEnable() => elapsed = 0f;

        private void Update()
        {
            if (aura == null) return;
            elapsed += Time.unscaledDeltaTime;
            Animate();
        }

        private void Animate()
        {
            Color glow = victory ? new Color(0.27f, 1f, 0.13f) : new Color(1f, 0.19f, 0.10f);
            glow.a = 0.72f + 0.14f * Mathf.Sin(elapsed * 2.8f);
            aura.color = glow;

            for (int i = 0; i < letterCount; i++)
            {
                float t = Mathf.Clamp01((elapsed - 0.05f * i) / 0.34f);
                float settle = 1f - t;
                float bob = Mathf.Sin(elapsed * 3.5f - i * 0.52f) * 2.6f * t;
                RectTransform rect = letters[i].rectTransform;
                rect.anchoredPosition = letterPositions[i] + new Vector2(0f, bob - 29f * settle * settle);
                rect.localScale = Vector3.one * (0.72f + 0.28f * t + 0.22f * Mathf.Sin(t * Mathf.PI));
                letters[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(t * 3f));
            }

            for (int i = 0; i < sparks.Length; i++)
            {
                float t = Mathf.Repeat(i * 0.618f + elapsed / (1.65f + (i % 5) * 0.25f), 1f);
                float x = (Mathf.Repeat(i * 0.38197f + 0.14f, 1f) - 0.5f) * 565f;
                float drift = Mathf.Sin(i * 2.4f + t * 4.4f) * 30f;
                Image spark = sparks[i];
                spark.rectTransform.anchoredPosition = new Vector2(x + drift, -40f + t * 162f);
                spark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 29f + t * 50f);
                Color color = victory ? new Color(0.88f, 1f, 0.53f) : new Color(1f, 0.81f, 0.58f);
                color.a = Mathf.Sin(t * Mathf.PI) * (0.50f + (i % 3) * 0.16f);
                spark.color = color;
            }
        }

        private static RunResultBadgeView Create(Transform parent, Sprite character, bool cleared)
        {
            RectTransform root = RunResultStageView.Rect(parent, "RunResultBadge", Vector2.zero, new Vector2(730, 730));
            var badge = root.gameObject.AddComponent<RunResultBadgeView>();
            badge.crest = RunResultStageView.Plate(root, "Crest", new Vector2(0, 46), new Vector2(730, 730), Color.white);

            const int diameter = 439;
            Image disc = RunResultStageView.Plate(root, "PortraitMask", new Vector2(-5, 49),
                new Vector2(diameter, diameter), Color.white, CircleMask());
            badge.portraitMask = disc.rectTransform;
            badge.portrait = RunResultStageView.Icon(disc.transform, "Portrait", null, 0, 10, 510);
            badge.portraitClip = badge.portrait.gameObject.AddComponent<RunResultPortraitClip>();
            if (badge.portraitClip.Configure(disc.rectTransform))
                disc.enabled = false;
            else
                disc.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            RectTransform atmosphere = RunResultStageView.Rect(root, "Outcome Effects", new Vector2(0, WordY), Vector2.zero);
            badge.aura = RunResultStageView.Plate(atmosphere, "Outcome Aura", new Vector2(0, 24f),
                new Vector2(690, 300), Color.white, UserArtwork.Load("run-result-aura"));
            Sprite sparkSprite = UserArtwork.Load("run-result-spark");
            for (int i = 0; i < badge.sparks.Length; i++)
                badge.sparks[i] = RunResultStageView.Icon(atmosphere, $"Spark {i + 1}", sparkSprite, 0, 0, 12 + i % 4 * 5);
            RectTransform wordmark = RunResultStageView.Rect(root, "Outcome Wordmark", new Vector2(0, WordY), Vector2.zero);
            for (int i = 0; i < badge.letters.Length; i++)
                badge.letters[i] = RunResultStageView.Plate(wordmark, "Letter", Vector2.zero,
                    new Vector2(124, 139.5f), Color.white);

            badge.SetPortrait(character);
            badge.SetOutcome(cleared);
            return badge;
        }

        private static Sprite[] GetLetters(string word)
        {
            if (LetterSprites.TryGetValue(word, out Sprite[] sprites) && sprites.Length > 0 && sprites[0] != null)
                return sprites;

            Sprite atlas = UserArtwork.Load("run-result-" + word.ToLowerInvariant() + "-letters");
            if (atlas == null) return System.Array.Empty<Sprite>();

            sprites = new Sprite[word.Length];
            float cellWidth = atlas.rect.width / word.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                Rect cell = new(atlas.rect.x + cellWidth * i, atlas.rect.y, cellWidth, atlas.rect.height);
                sprites[i] = Sprite.Create(atlas.texture, cell, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                sprites[i].name = word + "_" + i + "_" + word[i];
            }
            LetterSprites[word] = sprites;
            return sprites;
        }

        private static Sprite CircleMask()
        {
            if (maskSprite != null) return maskSprite;

            const int size = 128;
            const float radius = size / 2f;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { name = "Run Result Portrait Mask" };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new(x + 0.5f - radius, y + 0.5f - radius);
                pixels[y * size + x] = new Color32(255, 255, 255, offset.sqrMagnitude < radius * radius ? (byte)255 : (byte)0);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            maskSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return maskSprite;
        }
    }
}
