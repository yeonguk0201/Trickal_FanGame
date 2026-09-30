using System.Collections.Generic;
using TrickalFanGame.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Audio
{
    public sealed class GameAudioController : MonoBehaviour
    {
        public const string FrontendSceneName = "FrontendScene";
        public const string GameSceneName = "SampleScene";

        [SerializeField] private AudioClip homeBgm;
        [SerializeField] private AudioClip combatBgm;
        [SerializeField] private AudioClip uiClick;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        private readonly HashSet<Button> boundButtons = new();
        private float nextButtonScanTime;

        public static GameAudioController Instance { get; private set; }
        public AudioClip HomeBgm => homeBgm;
        public AudioClip CombatBgm => combatBgm;
        public AudioClip UiClick => uiClick;
        public AudioSource BgmSource => bgmSource;
        public AudioSource SfxSource => sfxSource;
        public AudioClip CurrentBgm => bgmSource != null ? bgmSource.clip : null;
        public int UiClickPlayCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        public void Configure(AudioClip configuredHomeBgm, AudioClip configuredCombatBgm,
            AudioClip configuredUiClick, AudioSource configuredBgmSource, AudioSource configuredSfxSource)
        {
            homeBgm = configuredHomeBgm;
            combatBgm = configuredCombatBgm;
            uiClick = configuredUiClick;
            bgmSource = configuredBgmSource;
            sfxSource = configuredSfxSource;
            ConfigureSources();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Instance.MergeClips(homeBgm, combatBgm, uiClick);
                Instance.ApplySceneAudio(SceneManager.GetActiveScene().name);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ConfigureSources();
            LocalSettings.AudioVolumeChanged += ApplyVolumes;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyVolumes();
            ApplySceneAudio(SceneManager.GetActiveScene().name);
            RefreshButtonBindings();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextButtonScanTime) return;
            nextButtonScanTime = Time.unscaledTime + 0.5f;
            RefreshButtonBindings();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            LocalSettings.AudioVolumeChanged -= ApplyVolumes;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindButtons();
            Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySceneAudio(scene.name);
            UnbindButtons();
            RefreshButtonBindings();
        }

        public void ApplySceneAudio(string sceneName)
        {
            AudioClip next = ResolveBgmForScene(sceneName);
            if (bgmSource == null || next == null || bgmSource.clip == next && bgmSource.isPlaying) return;
            bgmSource.Stop();
            bgmSource.clip = next;
            bgmSource.Play();
        }

        public AudioClip ResolveBgmForScene(string sceneName)
        {
            return sceneName == GameSceneName ? combatBgm : homeBgm;
        }

        public void ApplyVolumes()
        {
            if (bgmSource != null) bgmSource.volume = LocalSettings.MasterVolume * LocalSettings.BgmVolume;
            if (sfxSource != null) sfxSource.volume = LocalSettings.MasterVolume * LocalSettings.SfxVolume;
        }

        public void PlayUiClick()
        {
            UiClickPlayCount++;
            if (sfxSource != null && uiClick != null) sfxSource.PlayOneShot(uiClick);
        }

        public bool IsButtonBound(Button button)
        {
            return button != null && boundButtons.Contains(button);
        }

        public void RefreshButtonBindings()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Button button in buttons)
            {
                if (button == null || !boundButtons.Add(button)) continue;
                button.onClick.AddListener(PlayUiClick);
            }
        }

        private void UnbindButtons()
        {
            foreach (Button button in boundButtons)
                if (button != null) button.onClick.RemoveListener(PlayUiClick);
            boundButtons.Clear();
        }

        private void MergeClips(AudioClip configuredHomeBgm, AudioClip configuredCombatBgm, AudioClip configuredUiClick)
        {
            if (configuredHomeBgm != null) homeBgm = configuredHomeBgm;
            if (configuredCombatBgm != null) combatBgm = configuredCombatBgm;
            if (configuredUiClick != null) uiClick = configuredUiClick;
        }

        private void ConfigureSources()
        {
            if (bgmSource != null)
            {
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
                bgmSource.spatialBlend = 0f;
            }
            if (sfxSource != null)
            {
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                sfxSource.spatialBlend = 0f;
                sfxSource.ignoreListenerPause = true;
            }
        }
    }
}
