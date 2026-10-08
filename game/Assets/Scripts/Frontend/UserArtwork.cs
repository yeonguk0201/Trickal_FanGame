using System.Collections.Generic;
using TMPro;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // Presentation only: stable item IDs select supplied artwork without changing gameplay contracts.
    public static class UserArtwork
    {
        private static readonly Dictionary<string, Sprite> sprites = new();
        private static readonly Dictionary<bool, Material> materials = new();

        public static Sprite Load(string key)
        {
            if (!sprites.TryGetValue(key, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>("UserArtwork/" + key);
                sprites[key] = sprite;
            }
            return sprite;
        }

        public static Sprite HudIcon(int index)
        {
            if (index == 3) return Load("erpin-high-grade");
            string key = "hud-" + index;
            if (sprites.TryGetValue(key, out Sprite cached)) return cached;
            Sprite sheet = Load("hud-icons");
            if (sheet == null) return null;
            float w = sheet.texture.width / 2f, h = sheet.texture.height / 2f;
            Sprite icon = Sprite.Create(sheet.texture,
                new Rect(index % 2 * w, index < 2 ? h : 0f, w, h),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            icon.name = key;
            sprites[key] = icon;
            return icon;
        }

        public static Material ItemMaterial(bool spell)
        {
            if (materials.TryGetValue(spell, out Material material)) return material;
            Shader shader = Resources.Load<Shader>("UserArtwork/ItemArtwork");
            if (shader == null) return null;
            material = new Material(shader) { name = spell ? "Spell artwork" : "Artifact artwork" };
            material.SetFloat("_Card", spell ? 1f : 0f);
            materials[spell] = material;
            return material;
        }

        public static bool Apply(Image image, ItemDefinition definition, TMP_Text placeholder = null)
        {
            Sprite sprite = definition != null ? Load(definition.ItemId) : null;
            if (image != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
                image.material = sprite != null ? ItemMaterial(definition.Kind != ItemKind.Artifact) : null;
                if (sprite != null) image.color = Color.white;
            }
            if (placeholder != null) placeholder.gameObject.SetActive(sprite == null);
            return sprite != null;
        }

        public static void Apply(SpriteRenderer renderer, ItemDefinition definition)
        {
            Sprite sprite = definition != null ? Load(definition.ItemId) : null;
            if (renderer == null || sprite == null) return;
            bool spell = definition.Kind != ItemKind.Artifact;
            renderer.sprite = sprite;
            renderer.color = Color.white;
            PickupArtworkFit fit = FitPickup(renderer, sprite, spell ? SpellPickupWorldSize : PickupWorldSize);
            // Each pickup draws with its own material that holds its own texture. With one shared material the
            // pickups on the floor all showed the artwork of the one that appeared last, until the room was
            // shown again.
            Material shared = ItemMaterial(spell);
            if (shared != null)
            {
                Material own = new(shared) { name = shared.name + " (" + definition.ItemId + ")" };
                own.mainTexture = sprite.texture;
                renderer.sharedMaterial = own;
                fit.OwnMaterial(own);
            }
        }

        // A dropped item's longer side in world units, whatever the artwork's import size and the pickup's own
        // scale are. The pickup's collider is not changed.
        // 0.75 and 0.5 are the sizes confirmed on screen (2026-10-07): the artifact drop and the SP capsule.
        public const float PickupWorldSize = 0.75f;
        // A spell card fills its whole square, so it is drawn a little smaller than an artifact.
        public const float SpellPickupWorldSize = 0.65f;
        // The same for the heart and SP capsules, which are smaller than an item.
        public const float ResourcePickupWorldSize = 0.5f;

        private static PickupArtworkFit FitPickup(SpriteRenderer renderer, Sprite sprite, float worldSize)
        {
            PickupArtworkFit fit = renderer.GetComponent<PickupArtworkFit>();
            if (fit == null) fit = renderer.gameObject.AddComponent<PickupArtworkFit>();
            Vector2 spriteSize = sprite.bounds.size;
            float longerSide = Mathf.Max(spriteSize.x, spriteSize.y);
            if (longerSide <= 0f) return fit;
            // Sliced without borders stretches the whole sprite to the size; it also draws the full rectangle, so
            // the white outline has room outside a tightly cut silhouette.
            // Switching the draw mode makes Unity rescale the transform to keep the old look, which would also
            // resize the pickup's collider. The scale is put back, so only the artwork changes size.
            Vector3 localScale = renderer.transform.localScale;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.transform.localScale = localScale;
            renderer.size = spriteSize / longerSide * worldSize;
            fit.Configure(renderer, worldSize);
            return fit;
        }

        public static void Apply(Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        public static void ApplyPickup(SpriteRenderer renderer, string key)
        {
            Sprite sprite = Load(key);
            if (renderer == null || sprite == null) return;
            renderer.sprite = sprite;
            renderer.color = Color.white;
        }

        // The heart and SP capsules on the floor: the supplied artwork at a readable size. The shopkeeper uses
        // ApplyPickup and keeps its own size.
        public static void ApplyResourcePickup(SpriteRenderer renderer, string key)
        {
            Sprite sprite = Load(key);
            if (renderer == null || sprite == null) return;
            ApplyPickup(renderer, key);
            FitPickup(renderer, sprite, ResourcePickupWorldSize);
        }
    }
}
