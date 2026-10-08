using TMPro;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // The shared spell slot on the game HUD: held item name, its kind (spell / jjangsem spell), the use key and
    // whether it can be used right now. Final placement and resolution checks belong to UX-1.
    [DisallowMultipleComponent]
    public sealed class GameSpellSlotHudView : MonoBehaviour
    {
        public const string EmptyText = "비어 있음";
        public const string UsableText = "[" + PlayerSpellSlot.UseKeyLabel + "] 사용";
        public const string UnusableText = "[" + PlayerSpellSlot.UseKeyLabel + "] 사용 불가";
        public const string AllFixedText = EmptyText + UsableText + UnusableText + "스펠짱셈스펠";

        private static readonly Color EmptyIconColor = new(0.2f, 0.22f, 0.27f, 0.9f);
        private static readonly Color UsableKeyColor = new(0.3f, 0.88f, 0.72f, 1f);
        private static readonly Color UnusableKeyColor = new(0.55f, 0.6f, 0.66f, 1f);

        [SerializeField] private PlayerSpellSlot slot;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text kindText;
        [SerializeField] private TMP_Text keyText;

        private bool subscribed;
        private bool hasRendered;
        private ItemDefinition renderedDefinition;
        private bool renderedUsable;

        public PlayerSpellSlot Slot => slot;
        public Image Icon => icon;
        public TMP_Text NameText => nameText;
        public TMP_Text KindText => kindText;
        public TMP_Text KeyText => keyText;
        public bool IsShowingUsable => renderedUsable;

        public void Configure(PlayerSpellSlot configuredSlot, Image configuredIcon, TMP_Text configuredNameText,
            TMP_Text configuredKindText, TMP_Text configuredKeyText)
        {
            Unsubscribe();
            slot = configuredSlot;
            icon = configuredIcon;
            nameText = configuredNameText;
            kindText = configuredKindText;
            keyText = configuredKeyText;
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
                icon.color = held == null
                    ? EmptyIconColor
                    : held.Kind == ItemKind.JjangsemSpell
                        ? SingleUseItemPickup.JjangsemSpellColor
                        : SingleUseItemPickup.SpellColor;
            }
            UserArtwork.Apply(icon, held);
            if (nameText != null) nameText.text = held != null ? held.DisplayName : EmptyText;
            if (kindText != null) kindText.text = held != null ? ItemKindText.GetDisplayName(held.Kind) : string.Empty;
            if (keyText != null)
            {
                keyText.gameObject.SetActive(held != null);
                keyText.text = usable ? UsableText : UnusableText;
                keyText.color = usable ? UsableKeyColor : UnusableKeyColor;
            }
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
