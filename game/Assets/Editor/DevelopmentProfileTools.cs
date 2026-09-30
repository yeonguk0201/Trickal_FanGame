using TrickalFanGame.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class DevelopmentProfileTools
    {
        [MenuItem("Trickal Fan Game/Development/Clear Local Profile (Editor)")]
        public static void ClearLocalProfile()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Local Profile",
                "Delete the Editor PlayerPrefs user ID and nickname?\n\n" +
                "The client profile ID and other local settings will be kept.",
                "Clear Profile",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            LocalProfile.ClearProfile();
            Debug.Log("[DevelopmentProfileTools] Cleared Editor local user ID and nickname; kept client profile ID.");

            if (EditorApplication.isPlaying && SceneManager.sceneCount > 0)
            {
                SceneManager.LoadScene(0);
            }
        }
    }
}
