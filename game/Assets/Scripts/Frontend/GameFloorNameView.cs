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

        private bool subscribed;
        private int lastAnnouncedFloor;
        private float remainingSeconds;

        public RunProgress Progress => runProgress;
        public TMP_Text HeaderText => headerText;
        public CanvasGroup AnnouncementCanvasGroup => announcementCanvasGroup;
        public TMP_Text AnnouncementText => announcementText;
        public string RegionName => regionName;
        public float DisplayDuration => displayDuration;
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
            Advance(Time.unscaledDeltaTime);
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
            if (announcementCanvasGroup != null) announcementCanvasGroup.alpha = 1f;
        }

        private void Advance(float unscaledDeltaTime)
        {
            if (!IsShowing || unscaledDeltaTime <= 0f) return;
            remainingSeconds -= unscaledDeltaTime;
            if (remainingSeconds <= 0f) HideAnnouncement();
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
