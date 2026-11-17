using System;
using TMPro;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class GameFloorNameView : MonoBehaviour
    {
        [SerializeField] private RunProgress runProgress;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private CanvasGroup announcementCanvasGroup;
        [SerializeField] private TMP_Text announcementText;
        [SerializeField] private string regionName = "요정의 숲";
        [SerializeField] private string[] floorNameOverrides = Array.Empty<string>();
        [SerializeField, Min(0.1f)] private float displayDuration = 1.5f;
        // In-game presentation: the banner clears in like lifting fog over fadeSeconds, holds, then fogs out over
        // the last fadeSeconds of displayDuration (unscaled time).
        [SerializeField, Min(0f)] private float fadeSeconds = DefaultFadeSeconds;
        [SerializeField] private BannerMist mist;

        public const float DefaultFadeSeconds = 0.5f;
        public const float MaxFrameStep = 1f / 30f;

        private bool subscribed;
        private int lastAnnouncedFloor;
        private float remainingSeconds;

        public RunProgress Progress => runProgress;
        public TMP_Text HeaderText => headerText;
        public CanvasGroup AnnouncementCanvasGroup => announcementCanvasGroup;
        public TMP_Text AnnouncementText => announcementText;
        public string RegionName => regionName;
        public float DisplayDuration => displayDuration;
        public float FadeSeconds => fadeSeconds;
        public BannerMist Mist => mist;
        public float RemainingSeconds => remainingSeconds;
        public int LastAnnouncedFloor => lastAnnouncedFloor;
        public bool IsShowing { get; private set; }

        public void Configure(RunProgress configuredProgress, TMP_Text configuredHeaderText,
            CanvasGroup configuredAnnouncementCanvasGroup, TMP_Text configuredAnnouncementText,
            string configuredRegionName, string[] configuredFloorNameOverrides = null,
            float configuredDisplayDuration = 1.5f)
        {
            Unsubscribe();
            runProgress = configuredProgress;
            headerText = configuredHeaderText;
            announcementCanvasGroup = configuredAnnouncementCanvasGroup;
            announcementText = configuredAnnouncementText;
            regionName = string.IsNullOrWhiteSpace(configuredRegionName)
                ? string.Empty : configuredRegionName.Trim();
            floorNameOverrides = configuredFloorNameOverrides ?? Array.Empty<string>();
            displayDuration = Mathf.Max(0.1f, configuredDisplayDuration);
            lastAnnouncedFloor = 0;
            HideAnnouncement();
            RefreshHeader(runProgress != null && runProgress.CurrentFloor > 0 ? runProgress.CurrentFloor : 1);
            if (isActiveAndEnabled)
            {
                Subscribe();
                if (runProgress != null && runProgress.CurrentFloor > 0)
                    ShowFloor(runProgress.CurrentFloor);
            }
        }

        public void ConfigurePresentation(BannerMist configuredMist, float configuredDisplayDuration,
            float configuredFadeSeconds)
        {
            mist = configuredMist;
            displayDuration = Mathf.Max(0.1f, configuredDisplayDuration);
            fadeSeconds = Mathf.Max(0f, configuredFadeSeconds);
        }

        public string ResolveFloorName(int floorNumber)
        {
            int safeFloor = Mathf.Max(1, floorNumber);
            string suffix = safeFloor <= floorNameOverrides.Length &&
                            !string.IsNullOrWhiteSpace(floorNameOverrides[safeFloor - 1])
                ? floorNameOverrides[safeFloor - 1].Trim()
                : $"{safeFloor}층";
            return string.IsNullOrEmpty(regionName) ? suffix : $"{regionName} · {suffix}";
        }

        private void OnEnable()
        {
            Subscribe();
            int currentFloor = runProgress != null ? runProgress.CurrentFloor : 0;
            RefreshHeader(currentFloor > 0 ? currentFloor : 1);
            if (currentFloor > 0 && currentFloor != lastAnnouncedFloor) ShowFloor(currentFloor);
        }

        private void OnDisable()
        {
            Unsubscribe();
            HideAnnouncement();
        }

        private void Start()
        {
            int currentFloor = runProgress != null ? runProgress.CurrentFloor : 0;
            if (currentFloor > 0 && currentFloor != lastAnnouncedFloor) ShowFloor(currentFloor);
        }

        private void Update()
        {
            // The first frames after a cold scene load can each take a second or more (first asset and shader
            // loads); counting them in full used up the whole banner before it was ever drawn on the first Run.
            Advance(Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep));
        }

        private void HandleRoomChanged(int floorNumber, int _)
        {
            RefreshHeader(floorNumber);
            if (floorNumber != lastAnnouncedFloor) ShowFloor(floorNumber);
        }

        private void RefreshHeader(int floorNumber)
        {
            if (headerText != null) headerText.text = ResolveFloorName(floorNumber);
        }

        private void ShowFloor(int floorNumber)
        {
            lastAnnouncedFloor = Mathf.Max(1, floorNumber);
            string displayName = ResolveFloorName(lastAnnouncedFloor);
            RefreshHeader(lastAnnouncedFloor);
            if (announcementText != null) announcementText.text = displayName;
            remainingSeconds = displayDuration;
            IsShowing = true;
            ApplyFade();
        }

        // How far the banner is shown for the time left: rises over the first fadeSeconds and falls over the last,
        // 1 in between. Without mist this is the alpha itself.
        public static float FadeAlpha(float remaining, float duration, float fade)
        {
            if (remaining <= 0f) return 0f;
            float half = Mathf.Min(fade, duration * 0.5f);
            if (half <= 0f) return 1f;
            float elapsed = duration - remaining;
            return Mathf.Clamp01(Mathf.Min(elapsed / half, remaining / half));
        }

        private void ApplyFade()
        {
            float reveal = FadeAlpha(remainingSeconds, displayDuration, fadeSeconds);
            if (announcementCanvasGroup != null)
                announcementCanvasGroup.alpha = mist != null ? BannerMist.Alpha(reveal) : reveal;
            if (mist != null) mist.Apply(reveal);
        }

        private void Advance(float unscaledDeltaTime)
        {
            if (!IsShowing || unscaledDeltaTime <= 0f) return;
            remainingSeconds -= unscaledDeltaTime;
            if (remainingSeconds <= 0f) HideAnnouncement();
            else ApplyFade();
        }

        private void HideAnnouncement()
        {
            IsShowing = false;
            remainingSeconds = 0f;
            if (announcementCanvasGroup == null) return;
            announcementCanvasGroup.alpha = 0f;
            announcementCanvasGroup.interactable = false;
            announcementCanvasGroup.blocksRaycasts = false;
        }

        private void Subscribe()
        {
            if (subscribed || runProgress == null) return;
            runProgress.RoomChanged += HandleRoomChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (runProgress != null) runProgress.RoomChanged -= HandleRoomChanged;
            subscribed = false;
        }
    }
}
