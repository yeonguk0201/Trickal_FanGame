using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week7ChargingEnemySetup
    {
        private const string ChaserPrefabPath = "Assets/Prefabs/TestEnemy.prefab";
        private const string ChargingPrefabPath = "Assets/Prefabs/ChargingEnemy.prefab";

        [MenuItem("Trickal Fan Game/Setup Phase E-3 Charging Enemy")]
        public static void Setup()
        {
            GameObject chargingPrefab = CreateOrUpdatePrefab();
            if (chargingPrefab == null)
            {
                return;
            }

            bool assignedToTestRoom = AssignToFixedGraphTestRoom(chargingPrefab);
            AssetDatabase.SaveAssets();
            if (assignedToTestRoom)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }

            Selection.activeObject = chargingPrefab;
            Debug.Log(
                assignedToTestRoom
                    ? "Phase E-3 charging enemy ready. Fixed graph room 2 now uses ChargingEnemy for Play Mode testing."
                    : "Phase E-3 charging enemy prefab ready. Run the fixed room graph setup to assign it to room 2 automatically.",
                chargingPrefab);
        }

        private static GameObject CreateOrUpdatePrefab()
        {
            string sourcePath = AssetDatabase.LoadAssetAtPath<GameObject>(ChargingPrefabPath) != null
                ? ChargingPrefabPath
                : ChaserPrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
            {
                Debug.LogError($"Phase E-3 setup could not find {ChaserPrefabPath}.");
                return null;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                contents.name = "ChargingEnemy";
                contents.layer = LayerMask.NameToLayer("Enemy");

                EnemyChase chase = contents.GetComponent<EnemyChase>();
                if (chase != null)
                {
                    Object.DestroyImmediate(chase);
                }

                RangedEnemyController ranged = contents.GetComponent<RangedEnemyController>();
                if (ranged != null)
                {
                    Object.DestroyImmediate(ranged);
                }

                ContactDamage contactDamage = contents.GetComponent<ContactDamage>();
                if (contactDamage != null)
                {
                    Object.DestroyImmediate(contactDamage);
                }

                ChargingEnemyController charging = contents.GetComponent<ChargingEnemyController>();
                if (charging == null)
                {
                    charging = contents.AddComponent<ChargingEnemyController>();
                }

                SetMaxHealth(contents.GetComponent<Health>(), 7);
                charging.Configure(7f, 0.65f, 8f, 0.8f, 0.6f, 1.5f, 3);
                Rigidbody2D body = contents.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    body.interpolation = RigidbodyInterpolation2D.Interpolate;
                }

                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = new Color(1f, 0.5f, 0.15f);
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(contents, ChargingPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"Failed to save {ChargingPrefabPath}.");
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
            serializedHealth.FindProperty("maxHealth").floatValue = maxHealth;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool AssignToFixedGraphTestRoom(GameObject chargingPrefab)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (RoomNode node in Resources.FindObjectsOfTypeAll<RoomNode>())
            {
                if (node == null || node.gameObject.scene != activeScene ||
                    node.GetComponentInParent<RoomGraphController>() == null ||
                    node.FloorNumber != 1 || node.RoomNumber != 2 || node.ContentRoot == null)
                {
                    continue;
                }

                RoomController controller = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                if (controller == null)
                {
                    continue;
                }

                Undo.RecordObject(controller, "Assign Phase E-3 charging enemy test room");
                SerializedObject serializedController = new(controller);
                serializedController.FindProperty("enemyPrefab").objectReferenceValue = chargingPrefab;
                serializedController.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
                return true;
            }

            return false;
        }
    }
}
