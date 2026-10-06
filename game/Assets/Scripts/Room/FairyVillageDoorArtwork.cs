using System;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Presentation only: DoorController retains lock state, type colors, and collision ownership.
    public sealed class FairyVillageDoorArtwork : MonoBehaviour
    {
        [Serializable]
        public sealed class Variant
        {
            public DoorVisualKind kind;
            public Sprite open, closed, locked;
            public Texture2D openMask, closedMask;
        }

        [SerializeField] private Variant[] variants = Array.Empty<Variant>();
        [SerializeField] private DoorController door;
        [SerializeField] private SpriteRenderer artwork;
        [SerializeField] private Sprite openSprite;
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite keyLockedSprite;
        [SerializeField] private SpriteRenderer foreground;
        [SerializeField] private Texture2D openMask;
        [SerializeField] private Texture2D closedMask;
        [SerializeField] private Vector2 artworkSize;
        [SerializeField] private Vector2 foregroundSize;

        public void Configure(DoorController controller, SpriteRenderer renderer,
            Sprite open, Sprite closed, Sprite keyLocked)
        {
            door = controller;
            artwork = renderer;
            artworkSize = Vector2.Scale(renderer.sprite.bounds.size, renderer.transform.localScale);
            openSprite = open;
            closedSprite = closed;
            keyLockedSprite = keyLocked;
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        public void ConfigureVariants(Variant[] configuredVariants)
        {
            variants = configuredVariants ?? Array.Empty<Variant>();
            Refresh();
        }

        public void ConfigureForeground(SpriteRenderer renderer, Texture2D opened, Texture2D closed)
        {
            foreground = renderer;
            foregroundSize = Vector2.Scale(renderer.sprite.bounds.size, renderer.transform.localScale);
            openMask = opened; closedMask = closed; Refresh();
        }

        public void Refresh()
        {
            if (door == null || artwork == null) return;
            artwork.sprite = !door.IsLocked ? openSprite :
                door.VisualKind == DoorVisualKind.KeyLockedTreasure ? keyLockedSprite : closedSprite;
            Sprite baseSprite = artwork.sprite;
            Texture2D mask = door.IsLocked ? closedMask : openMask;
            foreach (Variant variant in variants)
            {
                if (variant.kind != door.VisualKind) continue;
                artwork.sprite = !door.IsLocked ? variant.open :
                    door.VisualKind == DoorVisualKind.KeyLockedTreasure ? variant.locked : variant.closed;
                mask = door.IsLocked ? variant.closedMask : variant.openMask;
                break;
            }
            // This patch includes grass and wall joints; tinting would reveal its rectangular boundary.
            artwork.color = Color.white;
            FitSprite(artwork, artworkSize);
            artwork.GetComponent<ConnectedRoomPatch>()?.SetDoorBaseTexture(
                artwork.sprite == baseSprite ? null : baseSprite.texture);
            artwork.GetComponent<ConnectedRoomPatch>()?.RefreshSpriteRegistration();
            if (foreground != null) {
                foreground.sprite = artwork.sprite;
                FitSprite(foreground, foregroundSize);
                ConnectedRoomPatch patch = foreground.GetComponent<ConnectedRoomPatch>();
                patch.SetForegroundMask(mask);
                patch.RefreshSpriteRegistration();
            }
        }

        private static void FitSprite(SpriteRenderer renderer, Vector2 size)
        {
            if (renderer.sprite == null || size.x <= 0 || size.y <= 0) return;
            Vector2 bounds = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, renderer.transform.localScale.z);
        }
    }
}
