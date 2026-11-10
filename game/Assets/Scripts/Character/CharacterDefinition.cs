using UnityEngine;

namespace TrickalFanGame.Character
{
    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Trickal Fan Game/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        public const int MaxRating = 5;

        [SerializeField] private string characterId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite portrait;

        // Menu-3 (docs/26 §3.5): display-only data for the character select and skill upgrade screens.
        // Not part of the Backend contract; combat values stay in PlayerStats and the skill rules.
        [Header("Menu presentation")]
        [SerializeField] private string epithet;
        [SerializeField, TextArea] private string introduction;
        [SerializeField] private Sprite slotPortrait;
        [SerializeField, Range(0, MaxRating)] private int healthRating;
        [SerializeField, Range(0, MaxRating)] private int skillPointRating;
        [SerializeField, Range(0, MaxRating)] private int attackRating;
        [SerializeField] private string basicSkillName;
        [SerializeField, TextArea] private string basicSkillDescription;
        [SerializeField] private Sprite basicSkillIcon;
        [SerializeField] private string lowGradeSkillName;
        [SerializeField] private string highGradeSkillName;

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Portrait => portrait;
        public string Epithet => epithet;
        public string Introduction => string.IsNullOrWhiteSpace(introduction) ? description : introduction;
        public Sprite SlotPortrait => slotPortrait != null ? slotPortrait : portrait;
        public int HealthRating => healthRating;
        public int SkillPointRating => skillPointRating;
        public int AttackRating => attackRating;
        public string BasicSkillName => basicSkillName;
        public string BasicSkillDescription => basicSkillDescription;
        public Sprite BasicSkillIcon => basicSkillIcon;
        public string LowGradeSkillName => lowGradeSkillName;
        public string HighGradeSkillName => highGradeSkillName;
        public bool IsValid => !string.IsNullOrWhiteSpace(characterId) && !string.IsNullOrWhiteSpace(displayName);
    }
}
