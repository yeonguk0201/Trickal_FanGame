using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // Menu-5: clicking a volume row's icon mutes that channel and shows a slash over the icon. Clicking again brings
    // back the value from before muting, or the default volume when there is none. The slider stays the source of
    // truth, so FrontendSettingsView saves the value to LocalSettings as usual.
    [RequireComponent(typeof(Button))]
    public sealed class FrontendVolumeMuteButton : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private GameObject slash;
        private Button button;
        private float valueBeforeMute = -1f;

        public Slider Slider => slider;
        public GameObject Slash => slash;

        public void Configure(Slider volumeSlider, GameObject mutedSlash)
        {
            slider = volumeSlider;
            slash = mutedSlash;
        }

        private void Awake() => button = GetComponent<Button>();

        private void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            button.onClick.AddListener(Toggle);
            if (slider != null) slider.onValueChanged.AddListener(Refresh);
            Refresh(slider != null ? slider.value : 1f);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(Toggle);
            if (slider != null) slider.onValueChanged.RemoveListener(Refresh);
        }

        public void Toggle()
        {
            if (slider == null) return;
            if (slider.value > slider.minValue)
            {
                valueBeforeMute = slider.value;
                slider.value = slider.minValue;
            }
            else
            {
                slider.value = valueBeforeMute > slider.minValue ? valueBeforeMute : Data.LocalSettings.DefaultVolume;
            }
            Refresh(slider.value);
        }

        private void Refresh(float value)
        {
            if (slash != null) slash.SetActive(slider != null && value <= slider.minValue);
        }
    }
}
