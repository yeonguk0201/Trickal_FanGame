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

        private readonly List<FrontendCharacterCardView> cards = new();
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

        public event Action<string> OnCharacterConfirmed;
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

        private void OnEnable()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmSelection);
            if (backButton != null) backButton.onClick.AddListener(RequestBack);
        }

        private void OnDisable()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(ConfirmSelection);
            if (backButton != null) backButton.onClick.RemoveListener(RequestBack);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            BuildCards();
            selectedCharacter = null;
            confirmed = false;
            foreach (FrontendCharacterCardView card in cards) card.SetSelected(false);
            confirmButton.interactable = false;
            statusText.text = cards.Count > 0 ? SelectMessage : EmptyMessage;
            Focus(cards.Count > 0 ? cards[0].Button : backButton);
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
            statusText.text = $"{character.DisplayName}을(를) 선택했습니다.";
            confirmButton.interactable = true;
            Focus(confirmButton);
        }

        public void ConfirmSelection()
        {
            if (confirmed || selectedCharacter == null) return;
            confirmed = true;
            confirmButton.interactable = false;
            statusText.text = string.Format(ConfirmedMessageFormat, selectedCharacter.DisplayName);
            OnCharacterConfirmed?.Invoke(selectedCharacter.CharacterId);
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
