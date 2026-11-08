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

            float height = confirmationVisible ? 150f : 48f;
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
                GUILayout.Label("RESET: clear user ID and nickname, keep the device key");
                GUILayout.Label("(the server only accepts the old nickname again).");
                GUILayout.Label("NEW PLAYER: also make a new device key.");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("RESET", GUILayout.Height(28f)))
                {
                    ResetProfileAndReloadFrontend(false);
                }
                if (GUILayout.Button("NEW PLAYER", GUILayout.Height(28f)))
                {
                    ResetProfileAndReloadFrontend(true);
                }
                if (GUILayout.Button("CANCEL", GUILayout.Height(28f)))
                {
                    confirmationVisible = false;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void ResetProfileAndReloadFrontend(bool newDeviceKey)
        {
            if (newDeviceKey) LocalProfile.ResetDeviceKey();
            else LocalProfile.ClearProfile();
            confirmationVisible = false;
            Debug.Log(newDeviceKey
                ? "[DevelopmentProfileResetMenu] Cleared the local profile and made a new device key (new player)."
                : "[DevelopmentProfileResetMenu] Cleared local user ID and nickname; kept client profile ID.");
            SceneManager.LoadScene(FrontendBuildIndex);
        }
#endif
    }
}
