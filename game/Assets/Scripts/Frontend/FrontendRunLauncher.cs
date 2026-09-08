using TrickalFanGame.Data;
using TrickalFanGame.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class FrontendRunLauncher : MonoBehaviour
    {
        [SerializeField] private FrontendHomeView homeView;
        [SerializeField] private string gameScenePath = "Assets/Scenes/SampleScene.unity";

        private bool isLaunching;
        private bool sceneLoadingEnabled = true;

        public FrontendHomeView HomeView => homeView;
        public string GameScenePath => gameScenePath;
        public bool IsLaunching => isLaunching;

        public void Configure(FrontendHomeView configuredHomeView, string configuredGameScenePath)
        {
            homeView = configuredHomeView;
            gameScenePath = configuredGameScenePath;
        }

        private void OnEnable()
        {
            if (homeView != null) homeView.OnCharacterConfirmed += LaunchRun;
        }

        private void OnDisable()
        {
            if (homeView != null) homeView.OnCharacterConfirmed -= LaunchRun;
        }

        public void SetSceneLoadingEnabledForVerification(bool enabled)
        {
            sceneLoadingEnabled = enabled;
        }

        private void LaunchRun(string characterId)
        {
            if (!sceneLoadingEnabled || isLaunching) return;
            if (!LocalProfile.IsRegistered)
            {
                Debug.LogError("[FrontendRunLauncher] A registered local profile is required.", this);
                return;
            }

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(gameScenePath);
            if (buildIndex < 0)
            {
                Debug.LogError($"[FrontendRunLauncher] Game Scene is not enabled in Build Settings: {gameScenePath}", this);
                return;
            }

            if (!RunLaunchContext.TryPrepare(LocalProfile.UserId, LocalProfile.Nickname, characterId))
            {
                Debug.LogError("[FrontendRunLauncher] A Run launch is already pending or its identity is invalid.", this);
                return;
            }

            isLaunching = true;
            try
            {
                SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
            }
            catch
            {
                isLaunching = false;
                RunLaunchContext.Clear();
                throw;
            }
        }
    }
}
