using System;
using TMPro;
using TrickalFanGame.Character;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Builds the speech bubble of an NPC (white box, black outline, tail) under its object. Any NPC's Setup can call
    // Ensure; re-running updates the same child objects.
    public static class NpcSpeechBubbleSetup
    {
        public const string SpritePath = "Assets/Art/Npc/speech-bubble.png";
        public const string BubbleObjectName = "Speech Bubble";
        public const float FontSize = 2.6f;
        // Above the keeper's name label and prompt (4).
        public const int SortingOrder = 5;
        private const float PixelsPerUnit = 200f;
        private const int CornerPixels = 20;

        public static Sprite EnsureSprite()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException($"The speech bubble sprite {SpritePath} is missing.");
            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            Vector4 border = Vector4.one * CornerPixels;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit) || importer.spriteBorder != border ||
                settings.spriteMeshType != SpriteMeshType.FullRect || importer.mipmapEnabled ||
                !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.ReadTextureSettings(settings);
                // Sliced drawing needs the full rectangle.
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null) throw new InvalidOperationException($"Unity could not import {SpritePath} as a sprite.");
            return sprite;
        }

        // tailTip is where the tail points, in the parent's local space. The bubble is saved hidden.
        public static NpcSpeechBubble Ensure(Transform parent, TMP_FontAsset font, Vector2 tailTip, string previewLine)
        {
            Sprite sprite = EnsureSprite();
            GameObject bubbleObject = Child(parent, BubbleObjectName);
            bubbleObject.transform.localPosition = tailTip;
            bubbleObject.transform.localRotation = Quaternion.identity;
            bubbleObject.transform.localScale = Vector3.one;

            SpriteRenderer outline = Box(bubbleObject.transform, "Outline", sprite, NpcSpeechBubble.OutlineColor,
                SortingOrder);
            SpriteRenderer tailOutline = Tail(bubbleObject.transform, "Tail Outline", sprite,
                NpcSpeechBubble.OutlineColor, NpcSpeechBubble.TailSize + NpcSpeechBubble.OutlineWidth * 2f,
                SortingOrder);
            SpriteRenderer fill = Box(bubbleObject.transform, "Fill", sprite, NpcSpeechBubble.FillColor,
                SortingOrder + 1);
            SpriteRenderer tailFill = Tail(bubbleObject.transform, "Tail Fill", sprite, NpcSpeechBubble.FillColor,
                NpcSpeechBubble.TailSize, SortingOrder + 2);

            GameObject textObject = Child(bubbleObject.transform, "Text");
            if (textObject.GetComponent<RectTransform>() == null) textObject.AddComponent<RectTransform>();
            TextMeshPro text = GetOrAdd<TextMeshPro>(textObject);
            text.font = font;
            text.fontSize = FontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = NpcSpeechBubble.OutlineColor;
            text.sortingOrder = SortingOrder + 3;
            textObject.transform.localScale = Vector3.one;

            NpcSpeechBubble bubble = GetOrAdd<NpcSpeechBubble>(bubbleObject);
            bubble.Configure(outline, fill, tailOutline, tailFill, text);
            // Lay it out once so the saved Prefab already has a sensible shape, then hide it.
            fill.size = Vector2.zero;
            bubble.Show(previewLine);
            bubble.Hide();
            return bubble;
        }

        private static SpriteRenderer Box(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            GameObject boxObject = Child(parent, name);
            boxObject.transform.localRotation = Quaternion.identity;
            boxObject.transform.localScale = Vector3.one;
            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(boxObject);
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static SpriteRenderer Tail(Transform parent, string name, Sprite sprite, Color color, float side,
            int order)
        {
            GameObject tailObject = Child(parent, name);
            tailObject.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tailObject.transform.localScale = Vector3.one * (side / sprite.bounds.size.x);
            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(tailObject);
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static GameObject Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            GameObject created = new(name);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
