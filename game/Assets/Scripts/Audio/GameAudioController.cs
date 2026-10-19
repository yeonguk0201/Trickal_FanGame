using System;
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

        // Sound-0: one combat cue. Volume and the retrigger guard are data, so
        // the mix can be tuned without touching code. A volume of 0 — which is
        // what an unfilled entry holds — plays at full level instead of
        // silently muting the cue; silence a cue by leaving its clip empty.
        [Serializable]
        public struct CombatSfxEntry
        {
            public CombatSfxCue cue;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
            [Min(0f)] public float minimumInterval;
        }

        [SerializeField] private AudioClip homeBgm;
        [SerializeField] private AudioClip combatBgm;
        [SerializeField] private AudioClip uiClick;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private CombatSfxEntry[] combatSfx = Array.Empty<CombatSfxEntry>();

        private readonly HashSet<Button> boundButtons = new();
        private readonly Dictionary<CombatSfxCue, float> nextCueTime = new();
        private readonly Dictionary<CombatSfxCue, int> cuePlayCounts = new();
        private float nextButtonScanTime;

        public static GameAudioController Instance { get; private set; }
        public AudioClip HomeBgm => homeBgm;
        public AudioClip CombatBgm => combatBgm;
        public AudioClip UiClick => uiClick;
        public AudioSource BgmSource => bgmSource;
        public AudioSource SfxSource => sfxSource;
        public AudioClip CurrentBgm => bgmSource != null ? bgmSource.clip : null;
        public int UiClickPlayCount { get; private set; }
        public IReadOnlyList<CombatSfxEntry> CombatSfx => combatSfx;

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
                Instance.MergeClips(homeBgm, combatBgm, uiClick, combatSfx);
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

        public void ConfigureCombatSfx(CombatSfxEntry[] entries)
        {
            combatSfx = entries ?? Array.Empty<CombatSfxEntry>();
            nextCueTime.Clear();
        }

        public AudioClip GetCombatClip(CombatSfxCue cue)
        {
            return TryGetCombatEntry(cue, out CombatSfxEntry entry) ? entry.clip : null;
        }

        public int GetCombatCuePlayCount(CombatSfxCue cue)
        {
            return cuePlayCounts.TryGetValue(cue, out int count) ? count : 0;
        }

        // Sound-0: a hit sound can be asked for several times in one frame (an
        // explosion catching four enemies), and identical one-shots stacked on
        // the same frame read as one loud click rather than four hits. The
        // per-cue interval drops the repeats; it runs on unscaled time so hit
        // stop does not hold the guard open.
        public bool PlayCombatCue(CombatSfxCue cue)
        {
            if (!TryGetCombatEntry(cue, out CombatSfxEntry entry) || entry.clip == null) return false;
            float now = Time.unscaledTime;
            if (nextCueTime.TryGetValue(cue, out float readyAt) && now < readyAt) return false;
            nextCueTime[cue] = now + Mathf.Max(0f, entry.minimumInterval);
            cuePlayCounts[cue] = GetCombatCuePlayCount(cue) + 1;
            if (sfxSource != null)
                sfxSource.PlayOneShot(entry.clip, entry.volume > 0f ? Mathf.Clamp01(entry.volume) : 1f);
            return true;
        }

        private bool TryGetCombatEntry(CombatSfxCue cue, out CombatSfxEntry entry)
        {
            for (int i = 0; i < combatSfx.Length; i++)
            {
                if (combatSfx[i].cue != cue) continue;
                entry = combatSfx[i];
                return true;
            }

            entry = default;
            return false;
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

        private void MergeClips(AudioClip configuredHomeBgm, AudioClip configuredCombatBgm,
            AudioClip configuredUiClick, CombatSfxEntry[] configuredCombatSfx)
        {
            if (configuredHomeBgm != null) homeBgm = configuredHomeBgm;
            if (configuredCombatBgm != null) combatBgm = configuredCombatBgm;
            if (configuredUiClick != null) uiClick = configuredUiClick;
            // The surviving controller keeps whichever table has cues, so the
            // scene that carries the combat sounds can load in either order.
            if (configuredCombatSfx is { Length: > 0 } && combatSfx.Length == 0)
            {
                combatSfx = configuredCombatSfx;
                nextCueTime.Clear();
            }
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
