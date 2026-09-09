using System.Collections.Generic;
using TMPro;
using TrickalFanGame.Item;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class GameArtifactHudView : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private RectTransform slotsRoot;
        [SerializeField] private ArtifactHudSlotView slotTemplate;
        [SerializeField] private TMP_Text overflowText;
        [SerializeField, Min(1)] private int maximumVisible = 10;

        private readonly List<ArtifactHudSlotView> visibleSlots = new();
        private bool subscribed;

        public PlayerInventory Inventory => inventory;
        public RectTransform SlotsRoot => slotsRoot;
        public ArtifactHudSlotView SlotTemplate => slotTemplate;
        public TMP_Text OverflowText => overflowText;
        public IReadOnlyList<ArtifactHudSlotView> VisibleSlots => visibleSlots;
        public int VisibleSlotCount => visibleSlots.Count;
        public int OverflowCount { get; private set; }
        public int MaximumVisible => maximumVisible;

        public void Configure(PlayerInventory configuredInventory, RectTransform configuredSlotsRoot,
            ArtifactHudSlotView configuredSlotTemplate, TMP_Text configuredOverflowText, int configuredMaximumVisible = 10)
        {
            Unsubscribe();
            inventory = configuredInventory;
            slotsRoot = configuredSlotsRoot;
            slotTemplate = configuredSlotTemplate;
            overflowText = configuredOverflowText;
            maximumVisible = Mathf.Max(1, configuredMaximumVisible);
            if (isActiveAndEnabled)
            {
                Subscribe();
                RefreshNow();
            }
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshNow();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void RefreshNow()
        {
            ClearVisibleSlots();
            if (inventory != null)
            {
                IReadOnlyList<ItemDefinition> definitions = inventory.AcquiredDefinitions;
                int visibleCount = Mathf.Min(maximumVisible, definitions.Count);
                for (int i = 0; i < visibleCount; i++)
                {
                    AddSlot(definitions[i], inventory.GetStackCount(definitions[i].ItemId));
                }
                OverflowCount = Mathf.Max(0, definitions.Count - maximumVisible);
            }
            else
            {
                OverflowCount = 0;
            }
            RefreshOverflow();
        }

        private void OnItemAcquired(ItemDefinition definition, int stackCount)
        {
            for (int i = 0; i < visibleSlots.Count; i++)
            {
                if (visibleSlots[i].Definition != null && visibleSlots[i].Definition.ItemId == definition.ItemId)
                {
                    visibleSlots[i].Bind(definition, stackCount);
                    return;
                }
            }

            if (visibleSlots.Count < maximumVisible)
            {
                AddSlot(definition, stackCount);
            }
            else
            {
                OverflowCount = Mathf.Max(0, inventory.AcquiredDefinitions.Count - maximumVisible);
                RefreshOverflow();
            }
        }

        private void AddSlot(ItemDefinition definition, int stackCount)
        {
            if (slotsRoot == null || slotTemplate == null || definition == null) return;
            ArtifactHudSlotView slot = Instantiate(slotTemplate, slotsRoot);
            slot.name = "Artifact " + definition.ItemId;
            slot.gameObject.SetActive(true);
            slot.Bind(definition, stackCount);
            visibleSlots.Add(slot);
        }

        private void ClearVisibleSlots()
        {
            for (int i = visibleSlots.Count - 1; i >= 0; i--)
            {
                ArtifactHudSlotView slot = visibleSlots[i];
                if (slot == null) continue;
                if (Application.isPlaying) Destroy(slot.gameObject);
                else DestroyImmediate(slot.gameObject);
            }
            visibleSlots.Clear();
        }

        private void RefreshOverflow()
        {
            if (overflowText == null) return;
            overflowText.text = OverflowCount > 0 ? $"+{OverflowCount}" : string.Empty;
            overflowText.gameObject.SetActive(OverflowCount > 0);
        }

        private void Subscribe()
        {
            if (subscribed || inventory == null) return;
            inventory.ItemAcquired += OnItemAcquired;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (inventory != null) inventory.ItemAcquired -= OnItemAcquired;
            subscribed = false;
        }
    }
}
