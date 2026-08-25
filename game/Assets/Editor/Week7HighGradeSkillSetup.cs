using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week7HighGradeSkillSetup
    {
        private const string EnemyPrefabPath = "Assets/Prefabs/TestEnemy.prefab";

        [MenuItem("Trickal Fan Game/Setup Phase D High Grade Skill")]
        public static void Setup()
        {
            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                Debug.LogError("Phase D setup needs the existing Player from Phases A-C.");
                return;
            }

            if (!ConfigureEnemyPrefab())
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Phase D High Grade Skill");

            PlayerActionState actionState = GetOrAdd<PlayerActionState>(player.gameObject);
            PlayerUltimate ultimate = GetOrAdd<PlayerUltimate>(player.gameObject);
            ultimate.Configure(
                30f,
                10f,
                2f,
                0.4f,
                0.25f,
                1.5f,
                8f,
                0.25f,
                0.4f,
                0.15f,
                LayerMask.GetMask("Enemy"));

            EditorUtility.SetDirty(actionState);
            EditorUtility.SetDirty(ultimate);
            Undo.CollapseUndoOperations(undoGroup);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player.gameObject;
            Debug.Log(
                "Phase D high-grade skill ready: press Q to start an invulnerable steerable dash. " +
                "Enemy contact deals a 200% area impact, applies knockback, and starts cooldown on dash end.",
                player);
        }

        private static bool ConfigureEnemyPrefab()
        {
            GameObject enemyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (enemyAsset == null)
            {
                Debug.LogError($"Phase D setup could not find {EnemyPrefabPath}.");
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                if (contents.GetComponent<KnockbackReceiver>() == null)
                {
                    contents.AddComponent<KnockbackReceiver>();
                }

                PrefabUtility.SaveAsPrefabAsset(contents, EnemyPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }
    }
}
