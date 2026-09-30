using System;

namespace TrickalFanGame.Network
{
    public interface IUserRegistrationClient
    {
        void PostUserWithErrorInfo(
            CreateUserRequest request,
            Action<CreateUserResponse> onSuccess,
            Action<RegistrationError> onError);
    }
}
