using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Presentation only. Integer source rectangles handle the non-divisible generated sheets without
    // resampling their pixels. A shared PPU and ground baseline prevent per-frame size/baseline pumping.
    public static class EnvironmentHazardArtwork
    {
        public const string Tree = "tree-burning-sheet";
        public const string Fire = "fire-trap-sheet";
        public const string FireMedium = "fire-hit-1-medium-sheet";
        public const string FireLow = "fire-hit-2-low-sheet";
        public const string FireExtinguish = "fire-hit-3-extinguish-sheet";
        public const float ExtinguishSeconds = 0.8f;
        public const string Gas = "poison-gas-sheet";
        public const string Spikes = "spikes-retracted";
        public const string Mushroom = "mushroom-hit-preburst";
        public const string ResourceRoot = "EnvironmentHazards/";
        [Serializable] private struct Frame { public int x, y, width, height; }
        [Serializable] private sealed class Sheet
        {
            public int width, height, frameWidth;
            public Frame[] frames;
            [NonSerialized] public Texture2D texture;
        }
        private static readonly Dictionary<string, Sheet> Sheets = new();
        private static readonly Dictionary<string, Sprite> Sprites = new();

        public static int FrameAt(string id, float elapsed) =>
            id == Spikes || id == Mushroom ? 0 :
            id == FireExtinguish ? Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, elapsed) * 10f), 0, 7) :
            Mathf.FloorToInt(Mathf.Repeat(Mathf.Max(0f, elapsed) * (id == Gas ? 6f : 10f), 8f));

        public static string FireStage(int hits) => hits <= 0 ? Fire : hits == 1 ? FireMedium : FireLow;

        public static Sprite Fit(string id, float elapsed, float width)
        {
            if (!Sheets.TryGetValue(id, out Sheet sheet))
            {
                TextAsset manifest = Resources.Load<TextAsset>(ResourceRoot + id);
                Texture2D texture = Resources.Load<Texture2D>(ResourceRoot + id);
                if (manifest == null || texture == null) return null;
                sheet = JsonUtility.FromJson<Sheet>(manifest.text);
                int count = id == Spikes || id == Mushroom ? 1 : 8;
                if (sheet == null || sheet.frames?.Length != count || sheet.frameWidth <= 0 ||
                    sheet.width != texture.width || sheet.height != texture.height)
                    throw new InvalidOperationException("Invalid environment hazard sheet: " + id);
                foreach (Frame frame in sheet.frames)
                    if (frame.x < 0 || frame.y < 0 || frame.width <= 0 || frame.height <= 0 ||
                        frame.x + frame.width > sheet.width || frame.y + frame.height > sheet.height)
                        throw new InvalidOperationException("Invalid environment hazard frame: " + id);
                sheet.texture = texture;
                Sheets.Add(id, sheet);
            }
            int index = FrameAt(id, elapsed);
            width = Mathf.Max(0.01f, width);
            string key = id + ":" + index + ":" + width.ToString("R", CultureInfo.InvariantCulture);
            if (Sprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Frame selected = sheet.frames[index];
            float ppu = sheet.frameWidth / width;
            string reference = id switch
            {
                Tree => "obstacle-tree", Fire or FireMedium or FireLow or FireExtinguish => "trap-fire",
                Spikes => "trap-periodic-spikes",
                Mushroom => "trap-poison-mushroom", _ => null,
            };
            Sprite intact = reference != null ? FairyKingdomArtwork.Fit(reference, width) : null;
            float pivotY = intact != null ? -intact.bounds.min.y * ppu / selected.height : 0.5f;
            Sprite sprite = Sprite.Create(sheet.texture,
                new Rect(selected.x, selected.y, selected.width, selected.height),
                new Vector2(0.5f, pivotY), ppu, 0, SpriteMeshType.FullRect);
            sprite.name = id + "-" + index;
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
