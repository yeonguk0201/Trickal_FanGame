using System;
using System.Collections.Generic;
using TrickalFanGame.Data;
using UnityEngine;
using UnityEngine.InputSystem;
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

        // Sound-2: a cue that has to stop when its moment ends (a falling
        // whistle cut by the landing, the wind of a meteor cut by its impact)
        // cannot ride the shared one-shot source, so it gets a voice of its own.
        private sealed class Voice
        {
            public AudioSource Source;
            public int Handle;
            public float Volume;
            public float FadeStartedAt = -1f;
            public float FadeSeconds;
            // Sound pass 2026-11-21: a voice told to fade out on its own at this time (-1 = never).
            public float StopAt = -1f;
            public float StopFadeSeconds;
        }

        // Sound pass 2026-11-21: cues asked for with a delay, the later layers of one moment.
        private readonly List<(float At, CombatSfxCue Cue)> pendingCues = new();

        private const int MaximumVoices = 8;
        private readonly List<Voice> voices = new();
        private int nextVoiceHandle;

        private readonly HashSet<Button> boundButtons = new();
        private readonly HashSet<Button> cancelButtons = new();
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
        public int UiCancelPlayCount { get; private set; }
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
            TickVoices(Time.unscaledTime);
            TickPendingCues(Time.unscaledTime);
            // Esc backs out of whatever is open (the pause menu, the shop, a
            // frontend page), so it always sounds as a cancel.
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) PlayUiCancel();
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

        // Sound-2: backing out with a back, cancel, close or resume button, or Esc.
        public void PlayUiCancel()
        {
            UiCancelPlayCount++;
            PlayCombatCue(CombatSfxCue.UiCancel);
        }

        // Buttons are named by what they do, and the ones that back out of a
        // screen carry Back, Cancel, Close or Resume in their names.
        public static bool IsCancelButtonName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            return objectName.EndsWith("BackButton", StringComparison.Ordinal) ||
                   objectName.Equals("Back", StringComparison.OrdinalIgnoreCase) ||
                   objectName.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("Resume", StringComparison.OrdinalIgnoreCase) >= 0;
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

        // Plays a cue on a voice of its own and returns a handle to stop it
        // with, or 0 when nothing played. The retrigger guard applies as it
        // does to a one-shot. With every voice busy the oldest is taken over.
        // A looping voice (the dash run) keeps going until it is stopped.
        // `pitch` plays the clip faster (and higher) as Unity's AudioSource
        // pitch does; 1 is the clip as recorded.
        public int PlayCombatVoice(CombatSfxCue cue, bool loop = false, float pitch = 1f)
        {
            if (!TryGetCombatEntry(cue, out CombatSfxEntry entry) || entry.clip == null) return 0;
            float now = Time.unscaledTime;
            if (nextCueTime.TryGetValue(cue, out float readyAt) && now < readyAt) return 0;
            nextCueTime[cue] = now + Mathf.Max(0f, entry.minimumInterval);
            cuePlayCounts[cue] = GetCombatCuePlayCount(cue) + 1;

            Voice voice = voices.Find(candidate => candidate.Source != null && !candidate.Source.isPlaying);
            if (voice == null && voices.Count < MaximumVoices)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                voice = new Voice { Source = source };
                voices.Add(voice);
            }

            if (voice == null)
            {
                voice = voices[0];
                foreach (Voice candidate in voices)
                    if (candidate.Handle < voice.Handle) voice = candidate;
            }

            voice.Handle = ++nextVoiceHandle;
            voice.Volume = entry.volume > 0f ? Mathf.Clamp01(entry.volume) : 1f;
            voice.FadeStartedAt = -1f;
            voice.StopAt = -1f;
            voice.Source.Stop();
            voice.Source.loop = loop;
            voice.Source.pitch = pitch > 0f ? pitch : 1f;
            voice.Source.clip = entry.clip;
            voice.Source.volume = voice.Volume * SfxLevel;
            voice.Source.Play();
            return voice.Handle;
        }

        // Fades a voice out. A handle that has been taken over or has finished
        // is ignored, so a late stop never cuts another sound short.
        public void StopCombatVoice(int handle, float fadeSeconds = 0.08f)
        {
            if (handle <= 0) return;
            foreach (Voice voice in voices)
            {
                if (voice.Handle != handle || voice.Source == null || !voice.Source.isPlaying) continue;
                if (fadeSeconds <= 0f) voice.Source.Stop();
                else if (voice.FadeStartedAt < 0f)
                {
                    voice.FadeStartedAt = Time.unscaledTime;
                    voice.FadeSeconds = fadeSeconds;
                }
                return;
            }
        }

        // Sound pass 2026-11-21: lets a voice play for `delay` seconds and then fade out over
        // `fadeSeconds`, without the caller having to come back for it.
        public void StopCombatVoiceAfter(int handle, float delay, float fadeSeconds)
        {
            if (handle <= 0) return;
            foreach (Voice voice in voices)
            {
                if (voice.Handle != handle || voice.Source == null || !voice.Source.isPlaying) continue;
                voice.StopAt = Time.unscaledTime + Mathf.Max(0f, delay);
                voice.StopFadeSeconds = Mathf.Max(0f, fadeSeconds);
                return;
            }
        }

        // Sound pass 2026-11-21: plays a cue `delay` seconds from now (unscaled, like the
        // guards), so one moment can be layered from several clips.
        public void PlayCombatCueAfter(CombatSfxCue cue, float delay)
        {
            if (delay <= 0f)
            {
                PlayCombatCue(cue);
                return;
            }

            pendingCues.Add((Time.unscaledTime + delay, cue));
        }

        public int PendingCueCount => pendingCues.Count;

        private void TickPendingCues(float now)
        {
            for (int i = pendingCues.Count - 1; i >= 0; i--)
            {
                if (now < pendingCues[i].At) continue;
                CombatSfxCue cue = pendingCues[i].Cue;
                pendingCues.RemoveAt(i);
                PlayCombatCue(cue);
            }
        }

        private static float SfxLevel => LocalSettings.MasterVolume * LocalSettings.SfxVolume;

        private void TickVoices(float now)
        {
            foreach (Voice voice in voices)
            {
                if (voice.Source == null || !voice.Source.isPlaying) continue;
                if (voice.StopAt >= 0f && now >= voice.StopAt && voice.FadeStartedAt < 0f)
                {
                    voice.StopAt = -1f;
                    if (voice.StopFadeSeconds <= 0f)
                    {
                        voice.Source.Stop();
                        continue;
                    }

                    voice.FadeStartedAt = now;
                    voice.FadeSeconds = voice.StopFadeSeconds;
                }

                float fade = 1f;
                if (voice.FadeStartedAt >= 0f)
                {
                    fade = 1f - Mathf.Clamp01((now - voice.FadeStartedAt) / Mathf.Max(0.001f, voice.FadeSeconds));
                    if (fade <= 0f)
                    {
                        voice.Source.Stop();
                        continue;
                    }
                }

                voice.Source.volume = voice.Volume * SfxLevel * fade;
            }
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

        public bool IsCancelButtonBound(Button button)
        {
            return button != null && cancelButtons.Contains(button);
        }

        public void RefreshButtonBindings()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Button button in buttons)
            {
                if (button == null || !boundButtons.Add(button)) continue;
                if (IsCancelButtonName(button.gameObject.name))
                {
                    cancelButtons.Add(button);
                    button.onClick.AddListener(PlayUiCancel);
                }
                else button.onClick.AddListener(PlayUiClick);
            }
        }

        private void UnbindButtons()
        {
            foreach (Button button in boundButtons)
            {
                if (button == null) continue;
                button.onClick.RemoveListener(PlayUiClick);
                button.onClick.RemoveListener(PlayUiCancel);
            }
            boundButtons.Clear();
            cancelButtons.Clear();
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
