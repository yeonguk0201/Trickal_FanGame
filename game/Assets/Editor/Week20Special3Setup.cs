using System;
using System.IO;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week20Special3Setup
    {
        public const string SecretPitPrefabPath = "Assets/Prefabs/SecretPit.prefab";
        // Hidden passages now count as neighbor exits for template and Encounter selection.
        public const int RoomContentVersion = 6;
        public const int EncounterContentVersion = 10;
        public const float PitWorldDiameter = 0.9f;
        public static readonly Color PitColor = new(0.08f, 0.06f, 0.1f, 0.95f);

        [MenuItem("Trickal Fan Game/Week 20/Setup Special-3 Secret Rooms")]
        public static void Setup()
        {
            EnsureSecretPitPrefab();
            Week18Obstacle1Setup.EnsureDropTable();
            Week17Resource3Setup.EnsureTable(Week20Obstacle4Setup.MarieDropTablePath,
                Week20Obstacle4Setup.MarieDropChance, Week20Obstacle4Setup.MarieDropWeights,
                "Configure Special-3 Marie bomb box pit drop");
            ConfigureGenerator();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(SecretPitPrefabPath);
            Debug.Log("Special-3 ready: floors roll a 50% secret room beside the most rooms (not next to start or " +
                      "boss), bombs open its hidden walls, and obstacle pit drops lead into it.");
        }

        public static SecretPit EnsureSecretPitPrefab()
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(Week17Resource1Setup.PlaceholderSpritePath);
            if (sprite == null) throw new InvalidOperationException("The built-in pit placeholder sprite is missing.");
            int pickupLayer = LayerMask.NameToLayer("Pickup");
            if (pickupLayer < 0) throw new InvalidOperationException("The Pickup layer is missing.");

            bool exists = File.Exists(SecretPitPrefabPath);
            GameObject root = exists
                ? PrefabUtility.LoadPrefabContents(SecretPitPrefabPath)
                : new GameObject("Secret Pit");
            try
            {
                // Pickup only meets Player, Environment and Pickup, so enemies and projectiles pass over the pit.
                root.layer = pickupLayer;
                root.transform.localScale = Vector3.one * (PitWorldDiameter / sprite.bounds.size.x);
                SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(root);
                renderer.sprite = sprite;
                renderer.color = PitColor;
                renderer.sortingOrder = 0;
                foreach (Collider2D collider in root.GetComponents<Collider2D>())
                    if (collider is not CircleCollider2D) Object.DestroyImmediate(collider);
                CircleCollider2D trigger = GetOrAdd<CircleCollider2D>(root);
                trigger.isTrigger = true;
                trigger.radius = sprite.bounds.extents.x;
                foreach (Rigidbody2D body in root.GetComponents<Rigidbody2D>()) Object.DestroyImmediate(body);
                GetOrAdd<SecretPit>(root);

                Directory.CreateDirectory("Assets/Prefabs");
                if (PrefabUtility.SaveAsPrefabAsset(root, SecretPitPrefabPath) == null)
                    throw new InvalidOperationException($"Could not save {SecretPitPrefabPath}.");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(SecretPitPrefabPath).GetComponent<SecretPit>();
        }

        private static void ConfigureGenerator()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null) throw new InvalidOperationException("Special-3 requires the Game Scene generator.");
            Undo.RecordObject(generator, "Configure Special-3 content versions");
            generator.ConfigureTemplates(Math.Max(RoomContentVersion, generator.RoomContentVersion),
                generator.RoomTemplates.ToArray());
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                generator.EncounterDefinitions.ToArray());
            EditorUtility.SetDirty(generator);
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Special-3 setup.");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
