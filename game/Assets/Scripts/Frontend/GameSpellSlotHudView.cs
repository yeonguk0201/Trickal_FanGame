using TMPro;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // The shared spell slot on the game HUD, drawn as the same rounded tile as the lower and high grade skills: the
    // held spell's artwork (an empty tile when nothing is held), "SHIFT" at the bottom, and a dark overlay while the
    // held spell cannot be used. Final placement and resolution checks belong to UX-1.
    [DisallowMultipleComponent]
    public sealed class GameSpellSlotHudView : MonoBehaviour
    {
        public const string KeyLabel = "SHIFT";
        public const string AllFixedText = KeyLabel;

        [SerializeField] private PlayerSpellSlot slot;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private Image unusableOverlay;

        private bool subscribed;
        private bool hasRendered;
        private ItemDefinition renderedDefinition;
        private bool renderedUsable;

        public PlayerSpellSlot Slot => slot;
        public Image Icon => icon;
        public TMP_Text KeyText => keyText;
        public Image UnusableOverlay => unusableOverlay;
        public bool IsShowingUsable => renderedUsable;
        public bool IsShowingItem => icon != null && icon.enabled;

        public void Configure(PlayerSpellSlot configuredSlot, Image configuredIcon, TMP_Text configuredKeyText,
            Image configuredUnusableOverlay)
        {
            Unsubscribe();
            slot = configuredSlot;
            icon = configuredIcon;
            keyText = configuredKeyText;
            unusableOverlay = configuredUnusableOverlay;
            hasRendered = false;
            if (isActiveAndEnabled) Subscribe();
            RefreshNow();
        }

        private void OnEnable()
        {
            Subscribe();
            hasRendered = false;
            RefreshNow();
        }

        private void OnDisable() => Unsubscribe();

        // Usability follows pause, death and room conditions that raise no event, so it is polled.
        private void Update() => RefreshNow();

        public void RefreshNow()
        {
            ItemDefinition held = slot != null ? slot.HeldDefinition : null;
            bool usable = slot != null && slot.CanUse;
            if (hasRendered && held == renderedDefinition && usable == renderedUsable) return;
            hasRendered = true;
            renderedDefinition = held;
            renderedUsable = usable;

            if (icon != null)
            {
                icon.enabled = held != null;
                if (held != null)
                {
                    // Without supplied artwork the spell colour stands in for it.
                    icon.color = held.Kind == ItemKind.JjangsemSpell
                        ? SingleUseItemPickup.JjangsemSpellColor
                        : SingleUseItemPickup.SpellColor;
                    UserArtwork.Apply(icon, held);
                }
            }
            if (unusableOverlay != null) unusableOverlay.gameObject.SetActive(held != null && !usable);
            if (keyText != null) keyText.text = KeyLabel;
        }

        private void OnSlotChanged() => RefreshNow();

        private void Subscribe()
        {
            if (subscribed || slot == null) return;
            slot.Changed += OnSlotChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (slot != null) slot.Changed -= OnSlotChanged;
            subscribed = false;
        }
    }
}
