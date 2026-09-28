using System.IO;
using TrickalFanGame.Resource;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week17Resource0Setup
    {
        public const string PrefabPath = "Assets/Prefabs/HealthPickup.prefab";
        public const string SpritePath = "Assets/Art/UI/HudHeart.png";
        public const float PickupScale = 0.75f;
        public const float PickupMass = 0.25f;
        public const float PickupLinearDamping = 6f;

        [MenuItem("Trickal Fan Game/Week 17/Setup Resource-0 Health Pickup")]
        public static void Setup()
        {
            HealthPickup prefab = EnsurePrefab(out string error);
            if (prefab == null)
            {
                Debug.LogError($"Resource-0 setup failed: {error}");
                return;
            }

            Selection.activeObject = prefab.gameObject;
            Debug.Log(
                "Resource-0 health pickup ready: one pickup heals one heart (2 units), stays on the floor when " +
                "the full amount does not fit under max HP, and can be pushed by the player. Open the Item Test " +
                "Room to spawn pickups from its debug panel.",
                prefab);
        }

        public static HealthPickup EnsurePrefab(out string error)
        {
            error = null;
            int pickupLayer = LayerMask.NameToLayer(HealthPickup.LayerName);
            if (pickupLayer < 0)
            {
                error = $"Layer '{HealthPickup.LayerName}' is missing from the Tag Manager.";
                return null;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null)
            {
                error = $"Heart sprite is missing at {SpritePath}.";
                return null;
            }

            bool exists = File.Exists(PrefabPath);
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Health Pickup");
            try
            {
                Configure(root, sprite, pickupLayer);
                Directory.CreateDirectory("Assets/Prefabs");
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null)
                {
                    error = $"Could not save {PrefabPath}.";
                    return null;
                }
            }
            finally
            {
                if (exists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<HealthPickup>();
        }

        private static void Configure(GameObject root, Sprite sprite, int pickupLayer)
        {
            root.layer = pickupLayer;
            root.transform.localScale = Vector3.one * PickupScale;

            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(root);
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 1;

            Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.mass = PickupMass;
            body.linearDamping = PickupLinearDamping;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
            collider.isTrigger = false;
            collider.radius = sprite.bounds.extents.x * 0.8f;
            collider.offset = Vector2.zero;

            HealthPickup pickup = GetOrAdd<HealthPickup>(root);
            SerializedObject serializedPickup = new(pickup);
            serializedPickup.FindProperty("healUnits").intValue = HealthPickup.DefaultHealUnits;
            serializedPickup.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
