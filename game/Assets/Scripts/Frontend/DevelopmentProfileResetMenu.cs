using TrickalFanGame.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Frontend
{
    public sealed class DevelopmentProfileResetMenu : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int FrontendBuildIndex = 0;
        private bool confirmationVisible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForDevelopment()
        {
            if (FindFirstObjectByType<DevelopmentProfileResetMenu>() != null)
            {
                return;
            }

            var owner = new GameObject(nameof(DevelopmentProfileResetMenu));
            DontDestroyOnLoad(owner);
            owner.AddComponent<DevelopmentProfileResetMenu>();
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().buildIndex != FrontendBuildIndex)
            {
                confirmationVisible = false;
                return;
            }

            float height = confirmationVisible ? 118f : 48f;
            GUILayout.BeginArea(new Rect(12f, 12f, 300f, height), GUI.skin.box);
            if (!confirmationVisible)
            {
                if (GUILayout.Button("DEV: RESET LOCAL PROFILE", GUILayout.Height(28f)))
                {
                    confirmationVisible = true;
                }
            }
            else
            {
                GUILayout.Label("Clear local user ID and nickname?");
                GUILayout.Label("The client profile ID will be kept.");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("RESET", GUILayout.Height(28f)))
                {
                    ResetProfileAndReloadFrontend();
                }
                if (GUILayout.Button("CANCEL", GUILayout.Height(28f)))
                {
                    confirmationVisible = false;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void ResetProfileAndReloadFrontend()
        {
            LocalProfile.ClearProfile();
            confirmationVisible = false;
            Debug.Log("[DevelopmentProfileResetMenu] Cleared local user ID and nickname; kept client profile ID.");
            SceneManager.LoadScene(FrontendBuildIndex);
        }
#endif
    }
}
