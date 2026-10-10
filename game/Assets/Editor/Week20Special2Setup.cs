using System;
using System.IO;
using System.Linq;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week20Special2Setup
    {
        public const string PlacedBombPrefabPath = "Assets/Prefabs/PlacedBomb.prefab";
        public const float WorldDiameter = 0.5f;
        public static readonly Color ArmedColor = new(0.18f, 0.18f, 0.22f);

        [MenuItem("Trickal Fan Game/Week 20/Setup Special-2 Player Bomb")]
        public static void Setup()
        {
            PlacedBomb prefab = EnsurePlacedBombPrefab();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerMovement player = FindSingle<PlayerMovement>(scene, "player");
            RunProgress progress = FindSingle<RunProgress>(scene, "RunProgress");
            PlayerBombController controller = player.GetComponent<PlayerBombController>();
            if (controller == null) controller = Undo.AddComponent<PlayerBombController>(player.gameObject);
            controller.Configure(progress, prefab);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PlacedBombPrefabPath);
            Debug.Log("Special-2 ready: F places one bomb, consumes it immediately, explodes after 0.75s in a " +
                      "2.5 radius, deals 30 enemy damage and 1-heart self damage, and destroys obstacles.");
        }

        public static PlacedBomb EnsurePlacedBombPrefab()
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(Week17Resource1Setup.PlaceholderSpritePath);
            if (sprite == null) throw new InvalidOperationException("The built-in bomb placeholder sprite is missing.");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer < 0) throw new InvalidOperationException("The Enemy layer is missing.");

            bool exists = File.Exists(PlacedBombPrefabPath);
            GameObject root = exists
                ? PrefabUtility.LoadPrefabContents(PlacedBombPrefabPath)
                : new GameObject("Placed Bomb");
            try
            {
                root.layer = LayerMask.NameToLayer("Default");
                root.transform.localScale = Vector3.one * (WorldDiameter / sprite.bounds.size.x);
                SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(root);
                renderer.sprite = sprite;
                renderer.color = ArmedColor;
                renderer.sortingOrder = TrickalFanGame.Player.PlayerMovement.AboveBodySortingOrder;
                PlacedBomb bomb = GetOrAdd<PlacedBomb>(root);
                bomb.ConfigureValues(PlacedBomb.DefaultFuseDuration, PlacedBomb.DefaultExplosionRadius,
                    PlacedBomb.DefaultEnemyDamage, PlacedBomb.DefaultSelfDamage, 1 << enemyLayer, renderer);

                foreach (Collider2D collider in root.GetComponents<Collider2D>()) Object.DestroyImmediate(collider);
                foreach (Rigidbody2D body in root.GetComponents<Rigidbody2D>()) Object.DestroyImmediate(body);
                foreach (RunResourcePickup pickup in root.GetComponents<RunResourcePickup>())
                    Object.DestroyImmediate(pickup);

                Directory.CreateDirectory("Assets/Prefabs");
                if (PrefabUtility.SaveAsPrefabAsset(root, PlacedBombPrefabPath) == null)
                    throw new InvalidOperationException($"Could not save {PlacedBombPrefabPath}.");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PlacedBombPrefabPath).GetComponent<PlacedBomb>();
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(candidate => candidate.gameObject.scene == scene).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Special-2 expected one {label}, found {matches.Length}.");
            return matches[0];
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
