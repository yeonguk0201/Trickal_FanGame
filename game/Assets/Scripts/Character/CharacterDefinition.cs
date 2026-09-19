using UnityEngine;

namespace TrickalFanGame.Character
{
    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Trickal Fan Game/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string characterId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite portrait;

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Portrait => portrait;
        public bool IsValid => !string.IsNullOrWhiteSpace(characterId) && !string.IsNullOrWhiteSpace(displayName);
    }
}
