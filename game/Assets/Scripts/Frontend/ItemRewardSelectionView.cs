using System;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class ItemRewardSelectionView : MonoBehaviour
    {
        public const float CompletionDisplaySeconds = 0.45f;

        [SerializeField] private ItemRewardSelectionSession session;
        [SerializeField] private Health playerHealth;
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private RectTransform focusScope;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private EventSystem uiEventSystem;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text inputHintText;
        [SerializeField] private ItemRewardCardView[] cards = Array.Empty<ItemRewardCardView>();
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private bool subscribed;
        private bool visible;
        private float hideAtUnscaledTime = -1f;
        private GameObject previousSelectedObject;
        private ItemRewardCandidate focusedCandidate;

        public ItemRewardSelectionSession Session => session;
        public CanvasGroup Overlay => overlay;
        public RectTransform FocusScope => focusScope;
        public GraphicRaycaster GraphicRaycaster => graphicRaycaster;
        public EventSystem UiEventSystem => uiEventSystem;
        public TMP_Text TitleText => titleText;
        public TMP_Text InputHintText => inputHintText;
        public ItemRewardCardView[] Cards => cards;
        public bool IsVisible => visible;
        public Button ConfirmButton => confirmButton;
        public Button CancelButton => cancelButton;

        public void Configure(ItemRewardSelectionSession configuredSession, Health configuredHealth,
            CanvasGroup configuredOverlay, RectTransform configuredFocusScope,
            GraphicRaycaster configuredRaycaster, EventSystem configuredEventSystem,
            TMP_Text configuredTitle, TMP_Text configuredInputHint,
            ItemRewardCardView[] configuredCards, Button configuredConfirmButton = null,
            Button configuredCancelButton = null)
        {
            Unsubscribe();
            session = configuredSession;
            playerHealth = configuredHealth;
            overlay = configuredOverlay;
            focusScope = configuredFocusScope;
            graphicRaycaster = configuredRaycaster;
            uiEventSystem = configuredEventSystem;
            titleText = configuredTitle;
            inputHintText = configuredInputHint;
            cards = configuredCards ?? Array.Empty<ItemRewardCardView>();
            confirmButton = configuredConfirmButton;
            cancelButton = configuredCancelButton;
            Subscribe();
            HideNow();
        }

        private void Awake()
        {
            Subscribe();
            ApplyVisibility(false);
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Update()
        {
            if (visible && hideAtUnscaledTime >= 0f && Time.unscaledTime >= hideAtUnscaledTime)
                HideNow();
        }

        private void LateUpdate()
        {
            if (visible && hideAtUnscaledTime < 0f) EnsureFocusNow();
        }

        public void Show(ItemRewardSelectionState selectionState)
        {
            if (selectionState == null || selectionState.IsCompleted) return;
            EventSystem activeEventSystem = ActiveEventSystem;
            previousSelectedObject = activeEventSystem != null
                ? activeEventSystem.currentSelectedGameObject
                : null;
            hideAtUnscaledTime = -1f;
            focusedCandidate = null;
            int visibleCount = Mathf.Min(cards.Length, selectionState.Candidates.Count);
            for (int index = 0; index < cards.Length; index++)
            {
                ItemRewardCardView card = cards[index];
                if (card == null) continue;
                bool active = index < visibleCount;
                card.gameObject.SetActive(active);
                if (active) card.Bind(selectionState.Candidates[index], playerHealth);
            }

            ConfigureNavigation(visibleCount);
            if (confirmButton != null) confirmButton.interactable = visibleCount > 0;
            ApplyVisibility(true);
            FocusFirstCard();
        }

        public void EnsureFocusNow()
        {
            EventSystem activeEventSystem = ActiveEventSystem;
            if (!visible || activeEventSystem == null) return;
            GameObject selected = activeEventSystem.currentSelectedGameObject;
            if (selected == null || focusScope == null || !selected.transform.IsChildOf(focusScope) ||
                selected.GetComponent<Button>()?.interactable == false)
            {
                FocusFirstCard();
            }
        }

        public void HideNow()
        {
            hideAtUnscaledTime = -1f;
            ApplyVisibility(false);
            RestorePreviousFocus();
        }

        private void OnSessionOpened(ItemRewardSelectionState selectionState) => Show(selectionState);

        private void OnSessionCompleted(ItemRewardSelectionState selectionState,
            ItemRewardCandidate selectedCandidate)
        {
            for (int index = 0; index < cards.Length; index++)
            {
                ItemRewardCardView card = cards[index];
                if (card == null || !card.gameObject.activeSelf) continue;
                card.ShowOutcome(ReferenceEquals(card.Candidate, selectedCandidate));
            }

            hideAtUnscaledTime = Time.unscaledTime + CompletionDisplaySeconds;
            if (confirmButton != null) confirmButton.interactable = false;
        }

        private void OnCardClicked(ItemRewardCardView card)
        {
            if (!visible || card?.Candidate == null) return;
            focusedCandidate = card.Candidate;
            ActiveEventSystem?.SetSelectedGameObject(confirmButton != null ? confirmButton.gameObject : card.gameObject);
        }

        private void OnCardFocused(ItemRewardCardView card)
        {
            if (visible && card?.Candidate != null) focusedCandidate = card.Candidate;
        }

        private void ConfirmSelection()
        {
            if (visible && session != null && focusedCandidate != null)
                session.TrySelect(focusedCandidate.StableId);
        }

        private void CancelSelection()
        {
            if (visible && hideAtUnscaledTime < 0f && session?.TryCancel() == true) HideNow();
        }

        private void ConfigureNavigation(int visibleCount)
        {
            if (visibleCount <= 0) return;
            for (int index = 0; index < visibleCount; index++)
            {
                Button current = cards[index].Button;
                if (current == null) continue;
                Navigation navigation = new() { mode = Navigation.Mode.Explicit };
                navigation.selectOnLeft = cards[(index + visibleCount - 1) % visibleCount].Button;
                navigation.selectOnRight = cards[(index + 1) % visibleCount].Button;
                navigation.selectOnUp = current;
                navigation.selectOnDown = confirmButton != null ? confirmButton : current;
                current.navigation = navigation;
            }
            if (confirmButton != null && cancelButton != null)
            {
                Navigation confirmNavigation = new() { mode = Navigation.Mode.Explicit };
                confirmNavigation.selectOnUp = cards[0].Button;
                confirmNavigation.selectOnRight = cancelButton;
                confirmNavigation.selectOnLeft = cancelButton;
                confirmButton.navigation = confirmNavigation;
                Navigation cancelNavigation = new() { mode = Navigation.Mode.Explicit };
                cancelNavigation.selectOnUp = cards[0].Button;
                cancelNavigation.selectOnRight = confirmButton;
                cancelNavigation.selectOnLeft = confirmButton;
                cancelButton.navigation = cancelNavigation;
            }
        }

        private void FocusFirstCard()
        {
            EventSystem activeEventSystem = ActiveEventSystem;
            if (activeEventSystem == null) return;
            for (int index = 0; index < cards.Length; index++)
            {
                Button button = cards[index]?.Button;
                if (button != null && button.gameObject.activeInHierarchy && button.interactable)
                {
                    activeEventSystem.SetSelectedGameObject(button.gameObject);
                    return;
                }
            }
        }

        private void ApplyVisibility(bool show)
        {
            visible = show;
            if (overlay != null)
            {
                overlay.alpha = show ? 1f : 0f;
                overlay.interactable = show;
                overlay.blocksRaycasts = show;
            }
            if (graphicRaycaster != null) graphicRaycaster.enabled = show;
        }

        private void RestorePreviousFocus()
        {
            EventSystem activeEventSystem = ActiveEventSystem;
            if (visible || activeEventSystem == null) return;
            GameObject target = previousSelectedObject != null && previousSelectedObject.activeInHierarchy
                ? previousSelectedObject
                : null;
            activeEventSystem.SetSelectedGameObject(target);
            previousSelectedObject = null;
        }

        private EventSystem ActiveEventSystem => uiEventSystem != null ? uiEventSystem : EventSystem.current;

        private void Subscribe()
        {
            if (subscribed || session == null) return;
            session.Opened += OnSessionOpened;
            session.Completed += OnSessionCompleted;
            for (int index = 0; index < cards.Length; index++)
                if (cards[index] != null)
                {
                    cards[index].Clicked += OnCardClicked;
                    cards[index].Focused += OnCardFocused;
                }
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmSelection);
            if (cancelButton != null) cancelButton.onClick.AddListener(CancelSelection);
            session.Cancelled += OnSessionCancelled;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (session != null)
            {
                session.Opened -= OnSessionOpened;
                session.Completed -= OnSessionCompleted;
                session.Cancelled -= OnSessionCancelled;
            }
            for (int index = 0; index < cards.Length; index++)
                if (cards[index] != null)
                {
                    cards[index].Clicked -= OnCardClicked;
                    cards[index].Focused -= OnCardFocused;
                }
            if (confirmButton != null) confirmButton.onClick.RemoveListener(ConfirmSelection);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(CancelSelection);
            subscribed = false;
        }

        private void OnSessionCancelled(ItemRewardSelectionState selectionState) => HideNow();
    }
}
