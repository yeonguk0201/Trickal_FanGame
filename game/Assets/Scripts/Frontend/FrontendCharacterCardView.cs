using TMPro;
using TrickalFanGame.Character;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public sealed class FrontendCharacterCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text selectionText;
        [SerializeField] private GameObject selectionBorder;
        [SerializeField] private Image portraitImage;

        public Button Button => button;
        public CharacterDefinition Character { get; private set; }

        public void ConfigureTemplate(Button cardButton, TMP_Text characterName, TMP_Text description,
            TMP_Text selection, GameObject border, Image portrait)
        {
            button = cardButton;
            nameText = characterName;
            descriptionText = description;
            selectionText = selection;
            selectionBorder = border;
            portraitImage = portrait;
        }

        public void Bind(CharacterDefinition character, UnityEngine.Events.UnityAction onSelected)
        {
            Character = character;
            nameText.text = character.DisplayName;
            descriptionText.text = character.Description;
            if (portraitImage != null)
            {
                portraitImage.sprite = character.Portrait;
                portraitImage.color = character.Portrait != null ? Color.white : new Color(0.3f, 0.72f, 0.68f);
                portraitImage.preserveAspect = true;
            }
            selectionText.text = "";
            selectionBorder.SetActive(false);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onSelected);
            gameObject.name = $"CharacterCard-{character.CharacterId}";
            gameObject.SetActive(true);
        }

        public void SetSelected(bool selected)
        {
            selectionText.text = selected ? "선택됨" : "";
            selectionBorder.SetActive(selected);
        }
    }
}
