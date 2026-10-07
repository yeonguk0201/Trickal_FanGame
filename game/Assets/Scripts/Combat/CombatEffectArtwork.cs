using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Runtime artwork loading keeps the reviewed effects available in builds and newly spawned rooms.
    public static class CombatEffectArtwork
    {
        private static readonly Dictionary<string, Sprite[]> Sheets = new();
        private static readonly Dictionary<string, Material> Materials = new();

        public static Sprite[] Frames(string name, int columns = 1, int rows = 1, Vector2? pivot = null)
        {
            string key = name + columns + rows + pivot;
            if (Sheets.TryGetValue(key, out Sprite[] cached)) return cached;
            Texture2D texture = Resources.Load<Texture2D>("CombatEffects/" + name);
            if (texture == null) return System.Array.Empty<Sprite>();
            var frames = new Sprite[columns * rows];
            for (int i = 0; i < frames.Length; i++)
            {
                // Integer boundaries cover the original NPOT sheet without resizing or bleeding into neighbours.
                int x0 = Mathf.RoundToInt(i % columns * texture.width / (float)columns);
                int x1 = Mathf.RoundToInt((i % columns + 1) * texture.width / (float)columns);
                int y0 = Mathf.RoundToInt((rows - 1 - i / columns) * texture.height / (float)rows);
                int y1 = Mathf.RoundToInt((rows - i / columns) * texture.height / (float)rows);
                Vector2 anchor = pivot ?? Vector2.one * 0.5f;
                // The ground-eruption sheet has different ground baselines in its two rows.
                // Anchor the fissure itself so it stays on the floor throughout the animation.
                if (name == "sword-slam" && columns == 4 && rows == 2)
                    anchor.y = i < 4 ? 0.14f : 0.24f;
                if (name == "slam-ground" && columns == 4 && rows == 2)
                    anchor.y = i < 4 ? 0.16f : 0.25f;
                frames[i] = Sprite.Create(texture, new Rect(x0, y0, x1 - x0, y1 - y0),
                    anchor, 100f, 0, SpriteMeshType.FullRect);
                frames[i].name = name + "-" + i;
            }
            Sheets.Add(key, frames);
            return frames;
        }

        public static Material Material(string shaderName)
        {
            if (Materials.TryGetValue(shaderName, out Material cached)) return cached;
            Shader shader = Resources.Load<Shader>("CombatEffects/" + shaderName);
            if (shader == null) return null;
            var material = new Material(shader) { name = "Combat effect " + shaderName };
            Materials.Add(shaderName, material);
            return material;
        }

        public static SpriteRenderer Renderer(GameObject owner, Sprite sprite, Renderer source, int orderOffset)
        {
            var renderer = owner.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (source != null) renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = (source != null ? source.sortingOrder : 0) + orderOffset;
            return renderer;
        }

        public static void Size(SpriteRenderer renderer, float width, float height)
        {
            if (renderer.sprite == null) return;
            Vector3 parentScale = renderer.transform.parent != null
                ? renderer.transform.parent.lossyScale : Vector3.one;
            Vector3 size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(width / size.x / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                height / size.y / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
        }

        public static void Release(Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Object.Destroy(item);
            else Object.DestroyImmediate(item);
        }
    }
}
