using TrickalFanGame.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Run
{
    [DisallowMultipleComponent]
    public sealed class GameRunResultTransition : MonoBehaviour
    {
        [SerializeField] private string frontendScenePath = "Assets/Scenes/FrontendScene.unity";

        public string FrontendScenePath => frontendScenePath;

        public void Configure(string configuredFrontendScenePath)
        {
            frontendScenePath = configuredFrontendScenePath;
        }

        public bool TryTransition(CreateRunRequest request, CharacterProgressDto startingProgress,
            IGameApiClient apiClient)
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(frontendScenePath);
            if (buildIndex < 0 || !RunResultContext.TryPrepare(request, startingProgress, apiClient))
                return false;

            float previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
                return true;
            }
            catch
            {
                RunResultContext.Clear();
                Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
                throw;
            }
        }

        public bool CanLoadFrontend()
        {
            return SceneUtility.GetBuildIndexByScenePath(frontendScenePath) >= 0;
        }

        public bool TryReturnHomeWithoutResult()
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(frontendScenePath);
            if (buildIndex < 0) return false;

            RunLaunchContext.Clear();
            RunResultContext.Clear();
            FrontendEntryContext.Clear();
            FrontendEntryContext.TryRequestHome();
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            try
            {
                SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
                return true;
            }
            catch
            {
                FrontendEntryContext.Clear();
                Time.timeScale = previousTimeScale;
                throw;
            }
        }

        public bool CanReloadGame()
        {
            return SceneManager.GetActiveScene().buildIndex >= 0;
        }

        public bool TryRestartRun(string userId, string nickname, string characterId,
            IGameApiClient apiClient)
        {
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            if (buildIndex < 0 || !RunLaunchContext.TryPrepare(userId, nickname, characterId, apiClient))
                return false;

            RunResultContext.Clear();
            FrontendEntryContext.Clear();
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            try
            {
                SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
                return true;
            }
            catch
            {
                RunLaunchContext.Clear();
                Time.timeScale = previousTimeScale;
                throw;
            }
        }
    }
}
