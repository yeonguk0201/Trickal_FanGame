using TMPro;
using TrickalFanGame.Character;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // Menu-3: the character shown in the middle of the character select screen and its info panel.
    // Display only; selection and confirmation stay in FrontendCharacterSelectionView.
    public sealed class FrontendCharacterDetailsView : MonoBehaviour
    {
        public static readonly Color FilledSegment = new(0.56f, 0.36f, 0.67f, 1f);
        public static readonly Color EmptySegment = new(1f, 1f, 1f, 1f);

        [SerializeField] private Image previewImage;
        [SerializeField] private Image faceImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text epithetText;
        [SerializeField] private TMP_Text introductionText;
        [SerializeField] private Image[] healthSegments;
        [SerializeField] private Image[] skillPointSegments;
        [SerializeField] private Image[] attackSegments;
        [SerializeField] private Image basicSkillIcon;
        [SerializeField] private TMP_Text basicSkillNameText;
        [SerializeField] private TMP_Text basicSkillDescriptionText;

        public CharacterDefinition Character { get; private set; }
        public TMP_Text NameText => nameText;
        public Image PreviewImage => previewImage;

        public void Configure(Image preview, Image face, TMP_Text characterName, TMP_Text epithet, TMP_Text introduction,
            Image[] health, Image[] skillPoints, Image[] attack, Image skillIcon, TMP_Text skillName,
            TMP_Text skillDescription)
        {
            previewImage = preview;
            faceImage = face;
            nameText = characterName;
            epithetText = epithet;
            introductionText = introduction;
            healthSegments = health;
            skillPointSegments = skillPoints;
            attackSegments = attack;
            basicSkillIcon = skillIcon;
            basicSkillNameText = skillName;
            basicSkillDescriptionText = skillDescription;
        }

        public void Bind(CharacterDefinition character)
        {
            Character = character;
            if (character == null) return;
            SetSprite(previewImage, character.Portrait);
            SetSprite(faceImage, character.SlotPortrait);
            SetText(nameText, character.DisplayName);
            SetText(epithetText, character.Epithet);
            SetText(introductionText, character.Introduction);
            SetRating(healthSegments, character.HealthRating);
            SetRating(skillPointSegments, character.SkillPointRating);
            SetRating(attackSegments, character.AttackRating);
            SetSprite(basicSkillIcon, character.BasicSkillIcon);
            SetText(basicSkillNameText, character.BasicSkillName);
            SetText(basicSkillDescriptionText, character.BasicSkillDescription);
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value ?? string.Empty;
        }

        private static void SetRating(Image[] segments, int rating)
        {
            if (segments == null) return;
            for (int i = 0; i < segments.Length; i++)
                if (segments[i] != null) segments[i].color = i < rating ? FilledSegment : EmptySegment;
        }
    }
}
