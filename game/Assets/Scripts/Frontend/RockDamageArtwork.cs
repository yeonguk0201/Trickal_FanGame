using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Damage sheets for rocks and resource boxes, separate from the 45-entry intact artwork catalog.
    // Only presentation changes.
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
            // Optional: where the intact drawing sits in each frame. Box frames add debris around the box, so the
            // box itself, not the whole frame, keeps the intact width and position.
            public Frame[] bodies;
            [NonSerialized] public Texture2D texture;
        }

        public const string ResourceRoot = "RockDamage/";
        private static readonly Dictionary<string, Sheet> Sheets = new();
        private static readonly Dictionary<string, Sprite> Sprites = new();

        public static bool Supports(string id) => FrameCount(id) > 0;

        public static int FrameCount(string id) => id switch
        {
            "obstacle-rock" or "obstacle-gold-rock" or "trap-spiked-rock" => 3,
            "obstacle-marie-bomb-box" or "obstacle-mayo-key-bundle" or "obstacle-erpin-snack-box" or
                "obstacle-eshur-bread-box" or "obstacle-ricotta-food-box" or "obstacle-shady-random-box" or
                "obstacle-mayo-collection-box" => 3,
            // Two hits set it off, so its single surviving hit has one frame.
            "obstacle-explosive-box" => 1,
            _ => 0,
        };

        // With the current four-hit rocks, surviving hits select frames 1, 2 and 3.
        public static int Stage(int hits, int requiredHits) => Stage(hits, requiredHits, 3);

        public static int Stage(int hits, int requiredHits, int frameCount) => hits <= 0 || frameCount <= 0 ? 0 :
            Mathf.Clamp(Mathf.CeilToInt(hits * (float)frameCount / Mathf.Max(1, requiredHits - 1)), 1, frameCount);

        public static Sprite Fit(string id, int stage, float width)
        {
            int count = FrameCount(id);
            if (count <= 0 || stage < 1) return null;
            stage = Mathf.Clamp(stage, 1, count);
            width = Mathf.Max(0.01f, width);
            string key = id + ":" + stage + ":" + width.ToString("R", CultureInfo.InvariantCulture);
            if (Sprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            if (!Sheets.TryGetValue(id, out Sheet sheet))
            {
                TextAsset data = Resources.Load<TextAsset>(ResourceRoot + id);
                Texture2D texture = Resources.Load<Texture2D>(ResourceRoot + id);
                if (data == null || texture == null) return null;
                sheet = JsonUtility.FromJson<Sheet>(data.text);
                if (sheet == null || sheet.width <= 0 || sheet.height <= 0 || sheet.frames?.Length != count ||
                    (sheet.bodies != null && sheet.bodies.Length != 0 && sheet.bodies.Length != count))
                    throw new InvalidOperationException("Invalid damage sheet: " + id);
                sheet.texture = texture;
                Sheets.Add(id, sheet);
            }

            Rect rect = ToTexture(sheet, sheet.frames[stage - 1]);
            Sprite sprite;
            if (sheet.bodies != null && sheet.bodies.Length == count)
            {
                // The intact sprite is centred on its visible rect, so centre the matched body the same way.
                Rect body = ToTexture(sheet, sheet.bodies[stage - 1]);
                Vector2 pivot = new((body.center.x - rect.x) / rect.width, (body.center.y - rect.y) / rect.height);
                sprite = Sprite.Create(sheet.texture, rect, pivot, body.width / width, 0, SpriteMeshType.FullRect);
            }
            else
            {
                Sprite intact = FairyKingdomArtwork.Fit(id, width);
                float ppu = rect.width / width;
                // Keep the original ground baseline even if a generated frame is a little taller.
                float pivotY = intact != null ? -intact.bounds.min.y * ppu / rect.height : 0.5f;
                sprite = Sprite.Create(sheet.texture, rect, new Vector2(0.5f, pivotY),
                    ppu, 0, SpriteMeshType.FullRect);
            }
            sprite.name = id + "-damage-" + stage;
            Sprites[key] = sprite;
            return sprite;
        }

        private static Rect ToTexture(Sheet sheet, Frame frame) => new(frame.x * sheet.texture.width / sheet.width,
            frame.y * sheet.texture.height / sheet.height,
            frame.width * sheet.texture.width / sheet.width,
            frame.height * sheet.texture.height / sheet.height);

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
