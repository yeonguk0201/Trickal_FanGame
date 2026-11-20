using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Presentation only. A tree sheet is four frames packed 2x2 in equal cells, read left to right, top row first.
    // The manifest gives the shared stump pivot (pixels from the cell's left/bottom) and the standing tree's drawn
    // height; a leaf sheet has no pivot and is laid out by its caller.
    public static class TreeSheetArtwork
    {
        public const int FrameCount = 4;

        [Serializable] public sealed class Manifest
        {
            public int width, height, cellWidth, cellHeight, frames, pivotX, pivotY, intactHeight, top, bottom;
        }

        private sealed class Sheet { public Manifest manifest; public Texture2D texture; }
        private static readonly Dictionary<string, Sheet> Sheets = new();
        private static readonly Dictionary<string, Sprite> Sprites = new();

        public static Manifest Load(string id) => Find(id)?.manifest;

        // pivot: in cell pixels from the left/bottom edge.
        public static Sprite Frame(string id, int index, float pixelsPerUnit, Vector2 pivot)
        {
            Sheet sheet = Find(id);
            if (sheet == null) return null;
            index = Mathf.Clamp(index, 0, FrameCount - 1);
            string key = string.Join(":", id, index.ToString(CultureInfo.InvariantCulture),
                pixelsPerUnit.ToString("R", CultureInfo.InvariantCulture),
                pivot.x.ToString("R", CultureInfo.InvariantCulture), pivot.y.ToString("R", CultureInfo.InvariantCulture));
            if (Sprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Manifest m = sheet.manifest;
            var rect = new Rect(index % 2 * m.cellWidth, (1 - index / 2) * m.cellHeight, m.cellWidth, m.cellHeight);
            Sprite sprite = Sprite.Create(sheet.texture, rect,
                new Vector2(pivot.x / m.cellWidth, pivot.y / m.cellHeight), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = id + "-" + index;
            Sprites[key] = sprite;
            return sprite;
        }

        private static Sheet Find(string id)
        {
            if (Sheets.TryGetValue(id, out Sheet sheet)) return sheet;
            TextAsset text = Resources.Load<TextAsset>(EnvironmentHazardArtwork.ResourceRoot + id);
            Texture2D texture = Resources.Load<Texture2D>(EnvironmentHazardArtwork.ResourceRoot + id);
            if (text == null || texture == null) return null;
            Manifest m = JsonUtility.FromJson<Manifest>(text.text);
            if (m == null || m.frames != FrameCount || m.width != texture.width || m.height != texture.height ||
                m.cellWidth * 2 != m.width || m.cellHeight * 2 != m.height || m.pivotX < 0 ||
                m.pivotX >= m.cellWidth || m.pivotY < 0 || m.pivotY >= m.cellHeight || m.intactHeight < 0)
                throw new InvalidOperationException("Invalid tree sheet: " + id);
            sheet = new Sheet { manifest = m, texture = texture };
            Sheets.Add(id, sheet);
            return sheet;
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
