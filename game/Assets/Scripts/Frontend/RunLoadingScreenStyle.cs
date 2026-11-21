using System;
using TMPro;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // References the existing portrait and ONE Mobile POP TMP asset without duplicating them.
    public sealed class RunLoadingScreenStyle : ScriptableObject
    {
        public const string ResourcePath = "RunLoadingScreen/Style";
        public TMP_FontAsset font;
        public Sprite portrait;
        public TextAsset layout;

        public RunLoadingLayout ReadLayout()
        {
            if (font == null || portrait == null || layout == null)
                throw new InvalidOperationException("Run loading screen requires its font, portrait and layout.");
            RunLoadingLayout result = JsonUtility.FromJson<RunLoadingLayout>(layout.text);
            if (result == null || result.version != 1 || result.canvas == null ||
                result.canvas.width != 1920 || result.canvas.height != 1080 ||
                result.layers == null || result.layers.Length != 7)
                throw new InvalidOperationException("Invalid Run loading screen layout.");
            foreach (string id in new[] { "portrait", "bubble", "strap", "epithet", "name", "bar", "percent" })
            {
                int count = 0;
                foreach (RunLoadingLayer layer in result.layers)
                    if (layer != null && layer.id == id) count++;
                if (count != 1) throw new InvalidOperationException($"Loading layout requires one {id} layer.");
            }
            foreach (RunLoadingLayer layer in result.layers)
            {
                if (!Finite(layer.x) || !Finite(layer.y) || !Finite(layer.w) || !Finite(layer.h) ||
                    layer.w <= 0f || layer.h <= 0f || !Finite(layer.rotation) || !Finite(layer.font) ||
                    !Finite(layer.portraitZoom) || !Finite(layer.portraitX) || !Finite(layer.portraitY) ||
                    (layer.id == "portrait" && (layer.portraitZoom < 25f || layer.portraitZoom > 400f)))
                    throw new InvalidOperationException($"Invalid loading layout geometry: {layer.id}.");
            }
            return result;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class RunLoadingLayout
    {
        public int version;
        public RunLoadingCanvas canvas;
        public RunLoadingLayer[] layers;
    }

    [Serializable]
    public sealed class RunLoadingCanvas
    {
        public int width;
        public int height;
    }

    [Serializable]
    public sealed class RunLoadingLayer
    {
        public string id;
        public float x, y, w, h, rotation;
        public int z;
        public bool visible;
        public string text, color;
        public float font;
        public float portraitZoom = 100f;
        public float portraitX, portraitY;
    }
}
