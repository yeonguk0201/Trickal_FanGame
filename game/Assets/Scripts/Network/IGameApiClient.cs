using System;

namespace TrickalFanGame.Network
{
    public interface IGameApiClient
    {
        void GetUser(string nickname, Action<UserProfileResponse> onSuccess, Action<string> onError);
        void PostRun(CreateRunRequest request, Action<CreateRunResponse> onSuccess, Action<string> onError);
    }
}
