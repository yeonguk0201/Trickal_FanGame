using TMPro;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class FrontendCreditsEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text primaryText;
        [SerializeField] private TMP_Text licenseText;
        [SerializeField] private TMP_Text usageText;

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

        public void Bind(CreditsCatalog.Entry entry)
        {
            Entry = entry;
            if (primaryText != null) primaryText.text = entry != null ? entry.PrimaryLine : string.Empty;
            if (licenseText != null) licenseText.text = entry != null ? entry.LicenseLine : string.Empty;
            if (usageText != null) usageText.text = entry != null && !string.IsNullOrWhiteSpace(entry.usage)
                ? entry.usage.Trim() : string.Empty;
        }
    }
}
