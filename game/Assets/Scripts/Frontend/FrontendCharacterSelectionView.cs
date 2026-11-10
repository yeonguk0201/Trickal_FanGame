using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TrickalFanGame.Character;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public enum FrontendCharacterSelectionPurpose { StartRun, UpgradeSkills }

    public sealed class FrontendCharacterSelectionView : MonoBehaviour
    {
        public const string EmptyMessage = "선택할 수 있는 캐릭터가 없습니다.";
        public const string SelectMessage = "캐릭터를 선택해 주세요.";
        public const string ConfirmedMessageFormat = "{0} 선택 완료";

        [SerializeField] private CharacterDefinition[] characters = Array.Empty<CharacterDefinition>();
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private FrontendCharacterCardView cardTemplate;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        // Menu-3: info panel, previous/next arrows and "준비중" slots that fill the roster grid.
        [SerializeField] private FrontendCharacterDetailsView detailsView;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private GameObject comingSoonTemplate;
        [SerializeField, Min(0)] private int slotCount;
        [SerializeField] private GameObject statusBackground; // hidden while the status line is empty

        private readonly List<FrontendCharacterCardView> cards = new();
        private readonly List<GameObject> comingSoonSlots = new();
        private CharacterDefinition selectedCharacter;
        private bool confirmed;

        public IReadOnlyList<FrontendCharacterCardView> Cards => cards;
        public CharacterDefinition SelectedCharacter => selectedCharacter;
        public string SelectedCharacterId => selectedCharacter != null ? selectedCharacter.CharacterId : null;
        public TMP_Text StatusText => statusText;
        public Button ConfirmButton => confirmButton;
        public Button BackButton => backButton;
        public RectTransform CardContainer => cardContainer;
        public FrontendCharacterCardView CardTemplate => cardTemplate;
        public FrontendCharacterSelectionPurpose Purpose { get; private set; }
        public FrontendCharacterDetailsView DetailsView => detailsView;
        public Button PreviousButton => previousButton;
        public Button NextButton => nextButton;
        public GameObject ComingSoonTemplate => comingSoonTemplate;
        public int SlotCount => slotCount;
        public IReadOnlyList<GameObject> ComingSoonSlots => comingSoonSlots;

        public event Action<string> OnCharacterConfirmed;
        public event Action<CharacterDefinition> OnUpgradeCharacterConfirmed;
        public event Action OnBackRequested;

        public void Configure(CharacterDefinition[] definitions, RectTransform container,
            FrontendCharacterCardView template, TMP_Text status, Button confirm, Button back)
        {
            characters = definitions ?? Array.Empty<CharacterDefinition>();
            cardContainer = container;
            cardTemplate = template;
            statusText = status;
            confirmButton = confirm;
            backButton = back;
        }

        public void ConfigurePresentation(FrontendCharacterDetailsView details, Button previous, Button next,
            GameObject comingSoon, int slots, GameObject statusPill = null)
        {
            statusBackground = statusPill;
            detailsView = details;
            previousButton = previous;
            nextButton = next;
            comingSoonTemplate = comingSoon;
            slotCount = slots;
        }

        private void OnEnable()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmSelection);
            if (backButton != null) backButton.onClick.AddListener(RequestBack);
            if (previousButton != null) previousButton.onClick.AddListener(SelectPrevious);
            if (nextButton != null) nextButton.onClick.AddListener(SelectNext);
        }

        private void OnDisable()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(ConfirmSelection);
            if (backButton != null) backButton.onClick.RemoveListener(RequestBack);
            if (previousButton != null) previousButton.onClick.RemoveListener(SelectPrevious);
            if (nextButton != null) nextButton.onClick.RemoveListener(SelectNext);
        }

        public void SelectPrevious() => Step(-1);
        public void SelectNext() => Step(1);

        private void Step(int direction)
        {
            if (confirmed || cards.Count == 0) return;
            int index = cards.FindIndex(card => card.Character == selectedCharacter);
            int next = index < 0 ? 0 : (index + direction + cards.Count) % cards.Count;
            Select(cards[next].Character);
        }

        public void Show(FrontendCharacterSelectionPurpose purpose = FrontendCharacterSelectionPurpose.StartRun)
        {
            Purpose = purpose;
            gameObject.SetActive(true);
            BuildCards();
            selectedCharacter = null;
            confirmed = false;
            foreach (FrontendCharacterCardView card in cards) card.SetSelected(false);
            if (detailsView != null) detailsView.Bind(cards.Count > 0 ? cards[0].Character : null);
            confirmButton.interactable = false;
            SetStatus(cards.Count > 0
                ? purpose == FrontendCharacterSelectionPurpose.UpgradeSkills
                    ? "강화할 캐릭터를 선택해 주세요."
                    : SelectMessage
                : EmptyMessage);
            TMP_Text confirmLabel = confirmButton != null ? confirmButton.GetComponentInChildren<TMP_Text>() : null;
            if (confirmLabel != null) confirmLabel.text = purpose == FrontendCharacterSelectionPurpose.UpgradeSkills
                ? "강화 화면으로" : "게임 시작";
            // Menu-3 (user decision 2026-11-10): the first character starts selected, so Enter starts right away.
            // Selecting still only marks the card; a Run starts only on confirm.
            if (cards.Count > 0) Select(cards[0].Character);
            else Focus(backButton);
        }

        public void Hide()
        {
            selectedCharacter = null;
            confirmed = false;
            gameObject.SetActive(false);
        }

        public void Select(CharacterDefinition character)
        {
            if (confirmed || character == null || cards.All(card => card.Character != character)) return;
            selectedCharacter = character;
            foreach (FrontendCharacterCardView card in cards) card.SetSelected(card.Character == character);
            if (detailsView != null) detailsView.Bind(character);
            SetStatus(string.Empty); // the slot ring, 선택됨 badge and info panel already show the choice
            confirmButton.interactable = true;
            Focus(confirmButton);
        }

        public void ConfirmSelection()
        {
            if (confirmed || selectedCharacter == null) return;
            confirmed = true;
            confirmButton.interactable = false;
            SetStatus(string.Format(ConfirmedMessageFormat, selectedCharacter.DisplayName));
            if (Purpose == FrontendCharacterSelectionPurpose.UpgradeSkills)
                OnUpgradeCharacterConfirmed?.Invoke(selectedCharacter);
            else
                OnCharacterConfirmed?.Invoke(selectedCharacter.CharacterId);
        }

        private void SetStatus(string message)
        {
            statusText.text = message;
            if (statusBackground != null) statusBackground.SetActive(!string.IsNullOrEmpty(message));
        }

        public void RequestBack()
        {
            OnBackRequested?.Invoke();
        }

        private void BuildCards()
        {
            if (cards.Count > 0) return;
            if (cardContainer == null || cardTemplate == null) return;

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterDefinition character in characters)
            {
                if (character == null || !character.IsValid || !seenIds.Add(character.CharacterId)) continue;
                FrontendCharacterCardView card = Instantiate(cardTemplate, cardContainer);
                card.Bind(character, () => Select(character));
                cards.Add(card);
            }
            cardTemplate.gameObject.SetActive(false);
            if (comingSoonTemplate != null)
            {
                for (int i = cards.Count; i < slotCount; i++)
                {
                    GameObject slot = Instantiate(comingSoonTemplate, cardContainer);
                    slot.name = "ComingSoonSlot";
                    slot.SetActive(true);
                    comingSoonSlots.Add(slot);
                }
                comingSoonTemplate.SetActive(false);
            }
            ConfigureNavigation();
        }

        private void ConfigureNavigation()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].Button.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = cards[(i + cards.Count - 1) % cards.Count].Button,
                    selectOnRight = cards[(i + 1) % cards.Count].Button,
                    selectOnDown = confirmButton
                };
            }
            confirmButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = cards.Count > 0 ? cards[0].Button : backButton,
                selectOnDown = backButton
            };
            backButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = confirmButton,
                selectOnDown = cards.Count > 0 ? cards[0].Button : confirmButton
            };
        }

        private static void Focus(Selectable selectable)
        {
            if (selectable != null && selectable.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
