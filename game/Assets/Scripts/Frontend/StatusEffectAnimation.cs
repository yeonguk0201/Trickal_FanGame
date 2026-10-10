using System;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Reviewed sheet rectangles and layout are shared by players, enemies and bosses.
    public static class StatusEffectAnimation
    {
        [Serializable] public sealed class Frame { public float x, y, width, height, durationMs; }
        [Serializable] public sealed class Piece { public float x, y, width, height, anchorX, anchorY; }
        [Serializable] public sealed class Effect
        {
            public string id;
            public float loopMs, motionCycleMs;
            public Frame[] frames;
            public Piece[] pieces;
            [NonSerialized] public Sprite[,] sprites;
        }
        [Serializable] private sealed class Definition { public Effect[] effects; }
        public struct Motion { public float x, y, scale, alpha; }
        private static Definition definition;
        public static Effect[] Effects
        {
            get
            {
                if (definition != null) return definition.effects;
                TextAsset json = Resources.Load<TextAsset>("StatusEffects/animation");
                if (json == null) return Array.Empty<Effect>();
                definition = JsonUtility.FromJson<Definition>(json.text);
                foreach (Effect effect in definition.effects)
                {
                    Texture2D texture = Resources.Load<Texture2D>("StatusEffects/" + effect.id + "-loop-v2");
                    if (texture == null) throw new InvalidOperationException("Missing status sheet: " + effect.id);
                    effect.sprites = new Sprite[effect.frames.Length, effect.pieces.Length];
                    for (int f = 0; f < effect.frames.Length; f++)
                    for (int p = 0; p < effect.pieces.Length; p++)
                    {
                        Frame frame = effect.frames[f]; Piece piece = effect.pieces[p];
                        int left = Mathf.RoundToInt(frame.x + piece.x * frame.width);
                        int right = Mathf.RoundToInt(frame.x + (piece.x + piece.width) * frame.width);
                        int top = Mathf.RoundToInt(frame.y + piece.y * frame.height);
                        int bottom = Mathf.RoundToInt(frame.y + (piece.y + piece.height) * frame.height);
                        var sprite = Sprite.Create(texture, new Rect(left, texture.height - bottom,
                            right - left, bottom - top), Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect);
                        sprite.name = "effect-" + effect.id;
                        effect.sprites[f, p] = sprite;
                    }
                }
                return definition.effects;
            }
        }
        public static int FrameAt(Effect effect, float seconds, int piece)
        {
            float time = Mathf.Repeat(seconds * 1000f, effect.loopMs);
            int f = 0;
            while (f < effect.frames.Length - 1 && time >= effect.frames[f].durationMs)
                time -= effect.frames[f++].durationMs;
            return (f + piece * 2) % effect.frames.Length;
        }
        public static Motion Sample(Effect effect, int piece, float seconds)
        {
            float ms = seconds * 1000f;
            if (effect.id == "poison")
            {
                float p = Mathf.Repeat(ms / effect.motionCycleMs + piece / 3f, 1f);
                return new Motion { x = Mathf.Sin(p * Mathf.PI * 2f + piece) * .014f, y = p * .18f,
                    scale = .55f + .5f * Mathf.Sin(p * Mathf.PI),
                    alpha = Mathf.Clamp01(Mathf.Min(p / .1f, (1f - p) / .15f)) };
            }
            if (effect.id == "shock")
            {
                float t = Mathf.Repeat(ms - piece * 55f, effect.motionCycleMs);
                bool on = t < 220f && (t < 45f || (t >= 75f && t < 120f) || t >= 150f);
                int j = Mathf.FloorToInt(t / 24f) + piece;
                return new Motion { x = Mathf.Sin(j * 7.1f) * .012f, y = Mathf.Cos(j * 5.3f) * .012f,
                    scale = 1f + j % 3 * .06f, alpha = on ? 1f : 0f };
            }
            return new Motion { x = 0f, y = Mathf.Sin(ms / 95f + piece * 2f) * .006f, scale = 1f, alpha = 1f };
        }
    }
}
