using System.Collections.Generic;
using TMPro;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class GamePauseArtifactView : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private RectTransform entriesRoot;
        [SerializeField] private ArtifactPauseListEntryView entryTemplate;
        [SerializeField] private TMP_Text emptyText;

        private readonly List<ArtifactPauseListEntryView> entries = new();
        private float previousTimeScale = 1f;

        public PlayerInventory Inventory => inventory;
        public CanvasGroup Overlay => overlay;
        public RectTransform EntriesRoot => entriesRoot;
        public ArtifactPauseListEntryView EntryTemplate => entryTemplate;
        public TMP_Text EmptyText => emptyText;
        public IReadOnlyList<ArtifactPauseListEntryView> Entries => entries;
        public bool IsPaused { get; private set; }
        public int EntryCount => entries.Count;

        public void Configure(PlayerInventory configuredInventory, CanvasGroup configuredOverlay,
            RectTransform configuredEntriesRoot, ArtifactPauseListEntryView configuredEntryTemplate,
            TMP_Text configuredEmptyText)
        {
            inventory = configuredInventory;
            overlay = configuredOverlay;
            entriesRoot = configuredEntriesRoot;
            entryTemplate = configuredEntryTemplate;
            emptyText = configuredEmptyText;
            ApplyVisibility(false);
        }

        private void Awake()
        {
            ApplyVisibility(false);
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (IsPaused) Resume();
            else TryPause();
        }

        private void OnDisable()
        {
            if (IsPaused) Resume();
        }

        public bool TryPause()
        {
            if (IsPaused || Time.timeScale <= 0f) return false;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            RebuildEntries();
            IsPaused = true;
            ApplyVisibility(true);
            return true;
        }

        public bool Resume()
        {
            if (!IsPaused) return false;
            IsPaused = false;
            ApplyVisibility(false);
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            return true;
        }

        public void RebuildEntries()
        {
            ClearEntries();
            if (inventory != null && entriesRoot != null && entryTemplate != null)
            {
                foreach (ItemDefinition definition in inventory.AcquiredDefinitions)
                {
                    ArtifactPauseListEntryView entry = Instantiate(entryTemplate, entriesRoot);
                    entry.name = "Artifact Detail " + definition.ItemId;
                    entry.gameObject.SetActive(true);
                    entry.Bind(definition, inventory.GetStackCount(definition.ItemId));
                    entries.Add(entry);
                }
            }

            if (emptyText != null) emptyText.gameObject.SetActive(entries.Count == 0);
        }

        private void ClearEntries()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                ArtifactPauseListEntryView entry = entries[i];
                if (entry == null) continue;
                entry.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(entry.gameObject);
                else DestroyImmediate(entry.gameObject);
            }
            entries.Clear();
        }

        private void ApplyVisibility(bool visible)
        {
            if (overlay == null) return;
            overlay.alpha = visible ? 1f : 0f;
            overlay.interactable = visible;
            overlay.blocksRaycasts = visible;
        }
    }
}
