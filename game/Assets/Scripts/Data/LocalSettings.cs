using System;
using UnityEngine;

namespace TrickalFanGame.Data
{
    public static class LocalSettings
    {
        private const string KeyMasterVolume = "Settings_MasterVolume";
        private const string KeyBgmVolume = "Settings_BgmVolume";
        private const string KeySfxVolume = "Settings_SfxVolume";
        private const string KeyFullscreen = "Settings_Fullscreen";
        private const string KeyResolutionWidth = "Settings_ResolutionWidth";
        private const string KeyResolutionHeight = "Settings_ResolutionHeight";
        private const string KeyDetailedStatsHud = "Settings_DetailedStatsHud";

        public const float DefaultVolume = 1f;
        public const int DefaultWidth = 1920;
        public const int DefaultHeight = 1080;
        public const bool DefaultFullscreen = true;
        public const bool DefaultDetailedStatsHud = false;

        public static readonly (int width, int height)[] SupportedResolutions =
        {
            (1280, 720),
            (1920, 1080),
            (2560, 1440)
        };

        private static float masterVolume = DefaultVolume;
        private static float bgmVolume = DefaultVolume;
        private static float sfxVolume = DefaultVolume;
        private static bool fullscreen = DefaultFullscreen;
        private static int resolutionWidth = DefaultWidth;
        private static int resolutionHeight = DefaultHeight;
        private static bool detailedStatsHud = DefaultDetailedStatsHud;

        public static event Action AudioVolumeChanged;
        public static event Action DetailedStatsHudChanged;

        public static float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(KeyMasterVolume, masterVolume);
                PlayerPrefs.Save();
                AudioVolumeChanged?.Invoke();
            }
        }

        public static float BgmVolume
        {
            get => bgmVolume;
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(KeyBgmVolume, bgmVolume);
                PlayerPrefs.Save();
                AudioVolumeChanged?.Invoke();
            }
        }

        public static float SfxVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(KeySfxVolume, sfxVolume);
                PlayerPrefs.Save();
                AudioVolumeChanged?.Invoke();
            }
        }

        public static bool Fullscreen
        {
            get => fullscreen;
            set
            {
                if (fullscreen == value) return;
                fullscreen = value;
                PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static int ResolutionWidth
        {
            get => resolutionWidth;
            private set => resolutionWidth = value;
        }

        public static int ResolutionHeight
        {
            get => resolutionHeight;
            private set => resolutionHeight = value;
        }

        public static bool DetailedStatsHud
        {
            get => detailedStatsHud;
            set
            {
                if (detailedStatsHud == value) return;
                detailedStatsHud = value;
                PlayerPrefs.SetInt(KeyDetailedStatsHud, detailedStatsHud ? 1 : 0);
                PlayerPrefs.Save();
                DetailedStatsHudChanged?.Invoke();
            }
        }

        public static void SetResolution(int width, int height)
        {
            if (!IsValidResolution(width, height))
                throw new ArgumentOutOfRangeException(nameof(width), $"Unsupported display resolution: {width}x{height}");
            if (resolutionWidth == width && resolutionHeight == height) return;
            resolutionWidth = width;
            resolutionHeight = height;
            PlayerPrefs.SetInt(KeyResolutionWidth, width);
            PlayerPrefs.SetInt(KeyResolutionHeight, height);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            masterVolume = PlayerPrefs.GetFloat(KeyMasterVolume, DefaultVolume);
            bgmVolume = PlayerPrefs.GetFloat(KeyBgmVolume, DefaultVolume);
            sfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, DefaultVolume);
            fullscreen = PlayerPrefs.GetInt(KeyFullscreen, DefaultFullscreen ? 1 : 0) == 1;
            resolutionWidth = PlayerPrefs.GetInt(KeyResolutionWidth, DefaultWidth);
            resolutionHeight = PlayerPrefs.GetInt(KeyResolutionHeight, DefaultHeight);
            detailedStatsHud = PlayerPrefs.GetInt(KeyDetailedStatsHud, DefaultDetailedStatsHud ? 1 : 0) == 1;

            if (!IsValidResolution(resolutionWidth, resolutionHeight))
            {
                resolutionWidth = DefaultWidth;
                resolutionHeight = DefaultHeight;
            }

            ApplyDisplay();
            AudioVolumeChanged?.Invoke();
            DetailedStatsHudChanged?.Invoke();
        }

        public static void ApplyDisplay()
        {
            FullScreenMode mode = fullscreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
            Screen.SetResolution(resolutionWidth, resolutionHeight, mode);
        }

        public static bool IsValidResolution(int width, int height)
        {
            foreach (var (w, h) in SupportedResolutions)
            {
                if (w == width && h == height) return true;
            }
            return false;
        }

        public static int GetResolutionIndex()
        {
            for (int i = 0; i < SupportedResolutions.Length; i++)
            {
                if (SupportedResolutions[i].width == resolutionWidth &&
                    SupportedResolutions[i].height == resolutionHeight)
                    return i;
            }
            return 1; // Default to 1920x1080
        }

        public static void ResetToDefaults()
        {
            MasterVolume = DefaultVolume;
            BgmVolume = DefaultVolume;
            SfxVolume = DefaultVolume;
            Fullscreen = DefaultFullscreen;
            SetResolution(DefaultWidth, DefaultHeight);
            DetailedStatsHud = DefaultDetailedStatsHud;
        }
    }
}
