using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Damage sheets are separate from the 45-entry intact artwork catalog. Only presentation changes.
    public static class RockDamageArtwork
    {
        [Serializable]
        private struct Frame
        {
            public float x, y, width, height;
        }

        [Serializable]
        private sealed class Sheet
        {
            public int width;
            public int height;
            public Frame[] frames;
            [NonSerialized] public Texture2D texture;
        }

        public const string ResourceRoot = "RockDamage/";
        private static readonly Dictionary<string, Sheet> Sheets = new();
        private static readonly Dictionary<string, Sprite> Sprites = new();

        public static bool Supports(string id) =>
            id is "obstacle-rock" or "obstacle-gold-rock" or "trap-spiked-rock";

        // With the current four-hit rocks, surviving hits select frames 1, 2 and 3.
        public static int Stage(int hits, int requiredHits) => hits <= 0 ? 0 :
            Mathf.Clamp(Mathf.CeilToInt(hits * 3f / Mathf.Max(1, requiredHits - 1)), 1, 3);

        public static Sprite Fit(string id, int stage, float width)
        {
            if (!Supports(id) || stage < 1) return null;
            stage = Mathf.Clamp(stage, 1, 3);
            width = Mathf.Max(0.01f, width);
            string key = id + ":" + stage + ":" + width.ToString("R", CultureInfo.InvariantCulture);
            if (Sprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            if (!Sheets.TryGetValue(id, out Sheet sheet))
            {
                TextAsset data = Resources.Load<TextAsset>(ResourceRoot + id);
                Texture2D texture = Resources.Load<Texture2D>(ResourceRoot + id);
                if (data == null || texture == null) return null;
                sheet = JsonUtility.FromJson<Sheet>(data.text);
                if (sheet == null || sheet.width <= 0 || sheet.height <= 0 || sheet.frames?.Length != 3)
                    throw new InvalidOperationException("Invalid rock damage sheet: " + id);
                sheet.texture = texture;
                Sheets.Add(id, sheet);
            }

            Frame frame = sheet.frames[stage - 1];
            Rect rect = new Rect(frame.x * sheet.texture.width / sheet.width,
                frame.y * sheet.texture.height / sheet.height,
                frame.width * sheet.texture.width / sheet.width,
                frame.height * sheet.texture.height / sheet.height);
            Sprite intact = FairyKingdomArtwork.Fit(id, width);
            float ppu = rect.width / width;
            // Keep the original ground baseline even if a generated frame is a little taller.
            float pivotY = intact != null ? -intact.bounds.min.y * ppu / rect.height : 0.5f;
            Sprite sprite = Sprite.Create(sheet.texture, rect, new Vector2(0.5f, pivotY),
                ppu, 0, SpriteMeshType.FullRect);
            sprite.name = id + "-damage-" + stage;
            Sprites[key] = sprite;
            return sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearCache()
        {
            foreach (Sprite sprite in Sprites.Values)
                if (sprite != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                    else UnityEngine.Object.DestroyImmediate(sprite);
                }
            Sprites.Clear();
            Sheets.Clear();
        }
    }
}
