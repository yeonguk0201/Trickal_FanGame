using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class FrontendCreditsEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text primaryText;
        [SerializeField] private TMP_Text licenseText;
        [SerializeField] private TMP_Text usageText;

        // Menu-5: music note for BGM rows, starburst for sound effects.
        [SerializeField] private Image kindIcon;
        [SerializeField] private Sprite soundIcon;
        [SerializeField] private Sprite musicIcon;

        public CreditsCatalog.Entry Entry { get; private set; }
        public TMP_Text PrimaryText => primaryText;
        public TMP_Text LicenseText => licenseText;
        public TMP_Text UsageText => usageText;

        public void ConfigureVisuals(TMP_Text primary, TMP_Text license, TMP_Text usage)
        {
            primaryText = primary;
            licenseText = license;
            usageText = usage;
        }

        public void ConfigureKindIcon(Image icon, Sprite sound, Sprite music)
        {
            kindIcon = icon;
            soundIcon = sound;
            musicIcon = music;
        }

        public void Bind(CreditsCatalog.Entry entry)
        {
            if (kindIcon != null)
            {
                bool music = entry != null && entry.usage != null &&
                             entry.usage.IndexOf("BGM", System.StringComparison.OrdinalIgnoreCase) >= 0;
                kindIcon.sprite = music ? musicIcon : soundIcon;
            }
            Entry = entry;
            if (primaryText != null) primaryText.text = entry != null ? entry.PrimaryLine : string.Empty;
            if (licenseText != null) licenseText.text = entry != null ? entry.LicenseLine : string.Empty;
            if (usageText != null) usageText.text = entry != null && !string.IsNullOrWhiteSpace(entry.usage)
                ? entry.usage.Trim() : string.Empty;
        }
    }
}
