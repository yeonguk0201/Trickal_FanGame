using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Explicit references keep all selected artwork available in a player build, including future trap art.
    public sealed class FairyKingdomArtworkCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public Sprite sprite;
            public Rect visibleRect;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        private readonly Dictionary<string, Sprite> fitted = new();
        public IReadOnlyList<Entry> Entries => entries;
        public void Configure(Entry[] value) { entries = value; fitted.Clear(); }

        public Sprite Fit(string id, float width)
        {
            width = Mathf.Max(0.01f, width);
            string key = id + ":" + width.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (fitted.TryGetValue(key, out Sprite cached)) return cached;
            foreach (Entry entry in entries)
            {
                if (entry.id != id || entry.sprite == null) continue;
                Rect rect = entry.visibleRect;
                if (rect.width <= 0 || rect.height <= 0) rect = entry.sprite.rect;
                Vector2 pivot = Vector2.one * 0.5f;
                if (id.StartsWith("chest-", StringComparison.Ordinal) && id.EndsWith("-open", StringComparison.Ordinal))
                {
                    string closedId = id.Substring(0, id.Length - 5) + "-closed";
                    Sprite closed = Fit(closedId, width);
                    if (closed != null) pivot.y = closed.bounds.size.y * 0.5f / (rect.height * width / rect.width);
                }
                Sprite sprite = Sprite.Create(entry.sprite.texture, rect, pivot,
                    rect.width / width, 0, SpriteMeshType.FullRect);
                sprite.name = id;
                fitted.Add(key, sprite);
                return sprite;
            }
            return null;
        }

        private void OnDisable()
        {
            foreach (Sprite sprite in fitted.Values)
                if (sprite != null) { if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite); }
            fitted.Clear();
        }
    }
}
