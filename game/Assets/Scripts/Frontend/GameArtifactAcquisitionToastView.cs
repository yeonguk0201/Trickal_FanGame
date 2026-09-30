using System.Collections.Generic;
using TMPro;
using TrickalFanGame.Item;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class GameArtifactAcquisitionToastView : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text messageText;
        [SerializeField, Min(0.1f)] private float displayDuration = 1.5f;

        private readonly Queue<ItemDefinition> pending = new();
        private bool subscribed;
        private float remainingSeconds;

        public PlayerInventory Inventory => inventory;
        public CanvasGroup ToastCanvasGroup => canvasGroup;
        public TMP_Text MessageText => messageText;
        public string CurrentName { get; private set; } = string.Empty;
        public string CurrentDescription { get; private set; } = string.Empty;
        public float DisplayDuration => displayDuration;
        public float RemainingSeconds => remainingSeconds;
        public int PendingCount => pending.Count;
        public bool IsShowing { get; private set; }

        public void Configure(PlayerInventory configuredInventory, CanvasGroup configuredCanvasGroup,
            TMP_Text configuredMessageText, float configuredDisplayDuration = 1.5f)
        {
            Unsubscribe();
            inventory = configuredInventory;
            canvasGroup = configuredCanvasGroup;
            messageText = configuredMessageText;
            displayDuration = Mathf.Max(0.1f, configuredDisplayDuration);
            ClearNotifications();
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
            Hide();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearNotifications();
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        private void OnItemAcquired(ItemDefinition definition, int stackCount)
        {
            if (definition == null) return;
            pending.Enqueue(definition);
            if (!IsShowing) ShowNext();
        }

        private void Advance(float unscaledDeltaTime)
        {
            if (!IsShowing || unscaledDeltaTime <= 0f) return;

            remainingSeconds -= unscaledDeltaTime;
            while (IsShowing && remainingSeconds <= 0f)
            {
                float overflowSeconds = -remainingSeconds;
                if (pending.Count == 0)
                {
                    Hide();
                    return;
                }

                ShowNext();
                remainingSeconds -= overflowSeconds;
            }
        }

        private void ShowNext()
        {
            if (pending.Count == 0)
            {
                Hide();
                return;
            }

            ItemDefinition definition = pending.Dequeue();
            CurrentName = definition.DisplayName;
            CurrentDescription = ArtifactEffectDescription.Build(definition);
            if (messageText != null)
            {
                messageText.text = $"<b>{CurrentName}</b>\n<size=20><color=#C2D6EB>{CurrentDescription}</color></size>";
            }
            remainingSeconds = displayDuration;
            IsShowing = true;
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        private void ClearNotifications()
        {
            pending.Clear();
            Hide();
        }

        private void Hide()
        {
            IsShowing = false;
            remainingSeconds = 0f;
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
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
