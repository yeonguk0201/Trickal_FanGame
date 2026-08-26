using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week7RangedEnemySetup
    {
        private const string ChaserPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        private const string RangedPrefabPath = "Assets/Prefabs/RangedEnemy.prefab";

        [MenuItem("Trickal Fan Game/Setup Phase E-2 Ranged Enemy")]
        public static void Setup()
        {
            GameObject rangedPrefab = CreateOrUpdatePrefab();
            if (rangedPrefab == null)
            {
                return;
            }

            bool assignedToTestRoom = AssignToFixedGraphTestRoom(rangedPrefab);
            AssetDatabase.SaveAssets();
            if (assignedToTestRoom)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }

            Selection.activeObject = rangedPrefab;
            Debug.Log(
                assignedToTestRoom
                    ? "Phase E-2 ranged enemy ready. Fixed graph room 3 now uses RangedEnemy for Play Mode testing."
                    : "Phase E-2 ranged enemy prefab ready. Run the fixed room graph setup to assign it to room 3 automatically.",
                rangedPrefab);
        }

        private static GameObject CreateOrUpdatePrefab()
        {
            string sourcePath = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefabPath) != null
                ? RangedPrefabPath
                : ChaserPrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
            {
                Debug.LogError($"Phase E-2 setup could not find {ChaserPrefabPath}.");
                return null;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                contents.name = "RangedEnemy";
                contents.layer = LayerMask.NameToLayer("Enemy");

                EnemyChase chase = contents.GetComponent<EnemyChase>();
                if (chase != null)
                {
                    Object.DestroyImmediate(chase);
                }

                ContactDamage contactDamage = contents.GetComponent<ContactDamage>();
                if (contactDamage != null)
                {
                    Object.DestroyImmediate(contactDamage);
                }

                RangedEnemyController ranged = contents.GetComponent<RangedEnemyController>();
                if (ranged == null)
                {
                    ranged = contents.AddComponent<RangedEnemyController>();
                }

                SetMaxHealth(contents.GetComponent<Health>(), 3);
                ranged.Configure(1.5f, 8f, 3f, 6f, 1.5f, 5f, 2, 4f);
                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = new Color(0.35f, 0.75f, 1f);
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(contents, RangedPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"Failed to save {RangedPrefabPath}.");
                }

                return prefab;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void SetMaxHealth(Health health, int maxHealth)
        {
            SerializedObject serializedHealth = new(health);
            serializedHealth.FindProperty("maxHealth").intValue = maxHealth;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool AssignToFixedGraphTestRoom(GameObject rangedPrefab)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (RoomNode node in Resources.FindObjectsOfTypeAll<RoomNode>())
            {
                if (node == null || node.gameObject.scene != activeScene ||
                    node.FloorNumber != 1 || node.RoomNumber != 3 || node.ContentRoot == null)
                {
                    continue;
                }

                RoomController controller = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                if (controller == null)
                {
                    continue;
                }

                Undo.RecordObject(controller, "Assign Phase E-2 ranged enemy test room");
                SerializedObject serializedController = new(controller);
                serializedController.FindProperty("enemyPrefab").objectReferenceValue = rangedPrefab;
                serializedController.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
                return true;
            }

            return false;
        }
    }
}
