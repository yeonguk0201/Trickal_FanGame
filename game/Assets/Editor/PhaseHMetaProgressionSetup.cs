using TrickalFanGame.Meta;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class PhaseHMetaProgressionSetup
    {
        [MenuItem("Trickal Fan Game/Setup Phase H-5 Meta Progression")]
        public static void Setup()
        {
            RunSession session = Object.FindFirstObjectByType<RunSession>();
            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            if (session == null || player == null)
            {
                Debug.LogError("Phase H-5 setup requires the existing RunSession and Player.");
                return;
            }

            PlayerSkill lowerGradeSkill = player.GetComponent<PlayerSkill>();
            PlayerUltimate highGradeSkill = player.GetComponent<PlayerUltimate>();
            if (lowerGradeSkill == null || highGradeSkill == null)
            {
                Debug.LogError("Phase H-5 setup requires the completed lower-grade and high-grade skill setup.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Phase H-5 Meta Progression");

            PlayerProgressClient progressClient = session.GetComponent<PlayerProgressClient>();
            if (progressClient == null)
            {
                progressClient = Undo.AddComponent<PlayerProgressClient>(session.gameObject);
            }

            progressClient.Configure(
                "test-player",
                lowerGradeSkill,
                highGradeSkill);
            session.ConfigureMetaProgression(progressClient);

            EditorUtility.SetDirty(progressClient);
            EditorUtility.SetDirty(session);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = session.gameObject;
            Debug.Log(
                "Phase H-5 meta progression ready: character selection loads Backend progression before the Run, " +
                "offline fallback is visible, and Run results can retry with the same clientRunId.",
                session);
        }
    }
}
