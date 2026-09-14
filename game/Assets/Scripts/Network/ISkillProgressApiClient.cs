using System;

namespace TrickalFanGame.Network
{
    public interface ISkillProgressApiClient
    {
        void UpgradeSkill(
            string nickname,
            string characterId,
            SkillType skillType,
            int targetLevel,
            Action<CharacterProgressDto> onSuccess,
            Action<SkillUpgradeError> onError);
    }
}
