using System.Collections.Generic;
using System;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
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
            IGameApiClient apiClient, IReadOnlyDictionary<string, ItemRarity> itemRarities = null,
            string deathDescription = null, Action onLoadFailed = null)
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(frontendScenePath);
            if (!RunSceneLoader.CanLoad(buildIndex) ||
                !RunResultContext.TryPrepare(request, startingProgress, apiClient, itemRarities, deathDescription))
                return false;

            if (RunSceneLoader.TryLoad(buildIndex, 0f, () =>
            {
                RunResultContext.Clear();
                onLoadFailed?.Invoke();
            })) return true;
            RunResultContext.Clear();
            return false;
        }

        public bool CanLoadFrontend()
        {
            return RunSceneLoader.CanLoad(SceneUtility.GetBuildIndexByScenePath(frontendScenePath));
        }

        public bool TryReturnHomeWithoutResult()
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(frontendScenePath);
            if (!RunSceneLoader.CanLoad(buildIndex)) return false;

            RunLaunchContext.Clear();
            RunResultContext.Clear();
            FrontendEntryContext.Clear();
            FrontendEntryContext.TryRequestHome();
            if (RunSceneLoader.TryLoad(buildIndex, onFailure: FrontendEntryContext.Clear)) return true;
            FrontendEntryContext.Clear();
            return false;
        }

        public bool CanReloadGame()
        {
            return RunSceneLoader.CanLoad(SceneManager.GetActiveScene().buildIndex);
        }

        public bool TryRestartRun(string userId, string nickname, string characterId,
            IGameApiClient apiClient)
        {
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            if (!RunSceneLoader.CanLoad(buildIndex) || !RunLaunchContext.TryPrepare(userId, nickname, characterId, apiClient))
                return false;

            RunResultContext.Clear();
            FrontendEntryContext.Clear();
            if (RunSceneLoader.TryLoad(buildIndex, onFailure: RunLaunchContext.Clear)) return true;
            RunLaunchContext.Clear();
            return false;
        }
    }
}
