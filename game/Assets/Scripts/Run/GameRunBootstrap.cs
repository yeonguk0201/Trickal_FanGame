using TrickalFanGame.Character;
using TrickalFanGame.Meta;
using UnityEngine;

namespace TrickalFanGame.Run
{
    [DefaultExecutionOrder(-400), DisallowMultipleComponent]
    public sealed class GameRunBootstrap : MonoBehaviour
    {
        [SerializeField] private RunSession runSession;
        [SerializeField] private PlayerProgressClient playerProgressClient;
        [SerializeField] private CharacterSelectionUI legacyCharacterSelection;

        public RunSession Session => runSession;
        public PlayerProgressClient ProgressClient => playerProgressClient;
        public CharacterSelectionUI LegacyCharacterSelection => legacyCharacterSelection;
        public bool HasAppliedLaunch { get; private set; }
        public string AppliedUserId { get; private set; }
        public string AppliedCharacterId { get; private set; }

        public void Configure(RunSession configuredSession, PlayerProgressClient configuredProgressClient,
            CharacterSelectionUI configuredLegacySelection)
        {
            runSession = configuredSession;
            playerProgressClient = configuredProgressClient;
            legacyCharacterSelection = configuredLegacySelection;
        }

        private void Awake()
        {
            if (!RunLaunchContext.TryPeek(out RunLaunchRequest request)) return;
            if (runSession == null)
                runSession = FindFirstObjectByType<RunSession>(FindObjectsInactive.Include);
            if (playerProgressClient == null)
                playerProgressClient = FindFirstObjectByType<PlayerProgressClient>(FindObjectsInactive.Include);
            if (runSession == null || playerProgressClient == null)
            {
                Debug.LogError("[GameRunBootstrap] RunSession and PlayerProgressClient are required.", this);
                return;
            }
            if (!runSession.ConfigureLaunchIdentity(request.UserId, request.Nickname, request.CharacterId))
            {
                Debug.LogError("[GameRunBootstrap] The pending Run launch identity is invalid.", this);
                return;
            }
            if (!RunLaunchContext.TryConsume(out request)) return;

            playerProgressClient.Configure(request.Nickname, null, null);
            if (request.ApiClient != null)
            {
                playerProgressClient.SetApiClient(request.ApiClient);
                runSession.SetApiClient(request.ApiClient);
            }
            if (legacyCharacterSelection != null)
                legacyCharacterSelection.gameObject.SetActive(false);

            HasAppliedLaunch = true;
            AppliedUserId = request.UserId;
            AppliedCharacterId = request.CharacterId;
        }
    }
}
