using System;
using TMPro;
using TrickalFanGame.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public sealed class FrontendSettingsView : MonoBehaviour
    {
        [Header("Volume Controls")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private TMP_Text masterVolumeLabel;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private TMP_Text bgmVolumeLabel;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TMP_Text sfxVolumeLabel;

        [Header("Display Controls")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("Navigation")]
        [SerializeField] private Button backButton;

        public Slider MasterVolumeSlider => masterVolumeSlider;
        public TMP_Text MasterVolumeLabel => masterVolumeLabel;
        public Slider BgmVolumeSlider => bgmVolumeSlider;
        public TMP_Text BgmVolumeLabel => bgmVolumeLabel;
        public Slider SfxVolumeSlider => sfxVolumeSlider;
        public TMP_Text SfxVolumeLabel => sfxVolumeLabel;
        public TMP_Dropdown ResolutionDropdown => resolutionDropdown;
        public Toggle FullscreenToggle => fullscreenToggle;
        public Button BackButton => backButton;

        public event Action OnBackRequested;

        public void Configure(
            Slider masterSlider, TMP_Text masterLabel,
            Slider bgmSlider, TMP_Text bgmLabel,
            Slider sfxSlider, TMP_Text sfxLabel,
            TMP_Dropdown resolution,
            Toggle fullscreen,
            Button back)
        {
            masterVolumeSlider = masterSlider;
            masterVolumeLabel = masterLabel;
            bgmVolumeSlider = bgmSlider;
            bgmVolumeLabel = bgmLabel;
            sfxVolumeSlider = sfxSlider;
            sfxVolumeLabel = sfxLabel;
            resolutionDropdown = resolution;
            fullscreenToggle = fullscreen;
            backButton = back;
        }

        private void OnEnable()
        {
            LoadCurrentSettings();
            BindEvents();
            FocusMasterSlider();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void LoadCurrentSettings()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = LocalSettings.MasterVolume;
                UpdateVolumeLabel(masterVolumeLabel, LocalSettings.MasterVolume);
            }
            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.value = LocalSettings.BgmVolume;
                UpdateVolumeLabel(bgmVolumeLabel, LocalSettings.BgmVolume);
            }
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = LocalSettings.SfxVolume;
                UpdateVolumeLabel(sfxVolumeLabel, LocalSettings.SfxVolume);
            }
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                var options = new System.Collections.Generic.List<string>();
                foreach (var (width, height) in LocalSettings.SupportedResolutions)
                    options.Add($"{width} x {height}");
                resolutionDropdown.AddOptions(options);
                resolutionDropdown.value = LocalSettings.GetResolutionIndex();
            }
            if (fullscreenToggle != null)
                fullscreenToggle.isOn = LocalSettings.Fullscreen;
        }

        private void BindEvents()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (bgmVolumeSlider != null) bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (backButton != null) backButton.onClick.AddListener(RequestBack);
        }

        private void UnbindEvents()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            if (bgmVolumeSlider != null) bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
            if (backButton != null) backButton.onClick.RemoveListener(RequestBack);
        }

        private void OnMasterVolumeChanged(float value)
        {
            LocalSettings.MasterVolume = value;
            UpdateVolumeLabel(masterVolumeLabel, value);
        }

        private void OnBgmVolumeChanged(float value)
        {
            LocalSettings.BgmVolume = value;
            UpdateVolumeLabel(bgmVolumeLabel, value);
        }

        private void OnSfxVolumeChanged(float value)
        {
            LocalSettings.SfxVolume = value;
            UpdateVolumeLabel(sfxVolumeLabel, value);
        }

        private void OnResolutionChanged(int index)
        {
            if (index < 0 || index >= LocalSettings.SupportedResolutions.Length) return;
            var (width, height) = LocalSettings.SupportedResolutions[index];
            LocalSettings.SetResolution(width, height);
            LocalSettings.ApplyDisplay();
        }

        private void OnFullscreenChanged(bool isFullscreen)
        {
            LocalSettings.Fullscreen = isFullscreen;
            LocalSettings.ApplyDisplay();
        }

        private void RequestBack()
        {
            OnBackRequested?.Invoke();
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void FocusMasterSlider()
        {
            if (masterVolumeSlider != null && masterVolumeSlider.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(masterVolumeSlider.gameObject);
        }

        private static void UpdateVolumeLabel(TMP_Text label, float value)
        {
            if (label != null) label.text = $"{Mathf.RoundToInt(value * 100)}%";
        }
    }
}
