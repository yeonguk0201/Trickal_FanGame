using UnityEngine;

namespace TrickalFanGame.Room
{
    // Presentation only: DoorController retains lock state, type colors, and collision ownership.
    public sealed class FairyVillageDoorArtwork : MonoBehaviour
    {
        [SerializeField] private DoorController door;
        [SerializeField] private SpriteRenderer artwork;
        [SerializeField] private Sprite openSprite;
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite keyLockedSprite;
        [SerializeField] private SpriteRenderer foreground;
        [SerializeField] private Texture2D openMask;
        [SerializeField] private Texture2D closedMask;

        public void Configure(DoorController controller, SpriteRenderer renderer,
            Sprite open, Sprite closed, Sprite keyLocked)
        {
            door = controller;
            artwork = renderer;
            openSprite = open;
            closedSprite = closed;
            keyLockedSprite = keyLocked;
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        public void ConfigureForeground(SpriteRenderer renderer, Texture2D opened, Texture2D closed)
        {
            foreground = renderer; openMask = opened; closedMask = closed; Refresh();
        }

        public void Refresh()
        {
            if (door == null || artwork == null) return;
            artwork.sprite = !door.IsLocked ? openSprite :
                door.VisualKind == DoorVisualKind.KeyLockedTreasure ? keyLockedSprite : closedSprite;
            // This patch includes grass and wall joints; tinting would reveal its rectangular boundary.
            artwork.color = Color.white;
            if (foreground != null) {
                foreground.sprite = artwork.sprite;
                ConnectedRoomPatch patch = foreground.GetComponent<ConnectedRoomPatch>();
                patch.SetForegroundMask(door.IsLocked ? closedMask : openMask);
            }
        }
    }
}
