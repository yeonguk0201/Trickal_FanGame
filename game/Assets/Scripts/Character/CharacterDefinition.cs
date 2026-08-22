using UnityEngine;

namespace TrickalFanGame.Character
{
    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Trickal Fan Game/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string characterId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public string Description => description;
        public bool IsValid => !string.IsNullOrWhiteSpace(characterId) && !string.IsNullOrWhiteSpace(displayName);
    }
}
