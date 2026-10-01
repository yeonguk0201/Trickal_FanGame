using System;
using System.IO;
using TrickalFanGame.Resource;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week17Resource1Setup
    {
        public const string ElifPrefabPath = "Assets/Prefabs/ElifPickup.prefab";
        public const string KeyPrefabPath = "Assets/Prefabs/KeyPickup.prefab";
        public const string BombPrefabPath = "Assets/Prefabs/BombPickup.prefab";
        // Unity built-in circle; each resource is told apart by tint and size until final art replaces it.
        public const string PlaceholderSpritePath = "UI/Skin/Knob.psd";
        public const int PickupAmount = 1;
        public const float PickupMass = 0.25f;
        public const float PickupLinearDamping = 6f;

        public sealed class PickupSpec
        {
            public PickupSpec(RunResourceType type, string prefabPath, string objectName, Color tint,
                float worldDiameter)
            {
                Type = type;
                PrefabPath = prefabPath;
                ObjectName = objectName;
                Tint = tint;
                WorldDiameter = worldDiameter;
            }

            public RunResourceType Type { get; }
            public string PrefabPath { get; }
            public string ObjectName { get; }
            public Color Tint { get; }
            public float WorldDiameter { get; }
        }

        public static readonly PickupSpec[] Specs =
        {
            new(RunResourceType.Gold, ElifPrefabPath, "Elif Pickup", new Color(1f, 0.82f, 0.2f), 0.35f),
            new(RunResourceType.Key, KeyPrefabPath, "Key Pickup", new Color(0.75f, 0.85f, 0.95f), 0.4f),
            new(RunResourceType.Bomb, BombPrefabPath, "Bomb Pickup", new Color(0.18f, 0.18f, 0.22f), 0.5f),
        };

        [MenuItem("Trickal Fan Game/Week 17/Setup Resource-1 Run Resource Pickups")]
        public static void Setup()
        {
            if (!EnsurePrefabs(out string error))
            {
                Debug.LogError($"Resource-1 setup failed: {error}");
                return;
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(ElifPrefabPath);
            Debug.Log(
                "Resource-1 pickups ready: gold, key and bomb pickups each grant 1, are held up to 99 per Run, " +
                "and stay on the floor as pushable bodies once that resource is full. Open the Item Test Room " +
                "to spawn them from its debug panel.");
        }

        public static bool EnsurePrefabs(out string error)
        {
            foreach (PickupSpec spec in Specs)
            {
                if (EnsurePrefab(spec, out error) == null)
                {
                    return false;
                }
            }

            error = null;
            return true;
        }

        public static RunResourcePickup LoadPrefab(RunResourceType type)
        {
            PickupSpec spec = Array.Find(Specs, candidate => candidate.Type == type);
            GameObject prefab = spec != null ? AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath) : null;
            return prefab != null ? prefab.GetComponent<RunResourcePickup>() : null;
        }

        private static RunResourcePickup EnsurePrefab(PickupSpec spec, out string error)
        {
            error = null;
            int pickupLayer = LayerMask.NameToLayer(HealthPickup.LayerName);
            if (pickupLayer < 0)
            {
                error = $"Layer '{HealthPickup.LayerName}' is missing from the Tag Manager.";
                return null;
            }

            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(PlaceholderSpritePath);
            if (sprite == null)
            {
                error = $"Unity built-in placeholder sprite is missing: {PlaceholderSpritePath}.";
                return null;
            }

            bool exists = File.Exists(spec.PrefabPath);
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(spec.PrefabPath) : new GameObject(spec.ObjectName);
            try
            {
                Configure(root, spec, sprite, pickupLayer);
                Directory.CreateDirectory("Assets/Prefabs");
                if (PrefabUtility.SaveAsPrefabAsset(root, spec.PrefabPath) == null)
                {
                    error = $"Could not save {spec.PrefabPath}.";
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
            return AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath).GetComponent<RunResourcePickup>();
        }

        private static void Configure(GameObject root, PickupSpec spec, Sprite sprite, int pickupLayer)
        {
            root.layer = pickupLayer;
            root.transform.localScale = Vector3.one * (spec.WorldDiameter / sprite.bounds.size.x);

            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(root);
            renderer.sprite = sprite;
            renderer.color = spec.Tint;
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
            collider.radius = sprite.bounds.extents.x;
            collider.offset = Vector2.zero;

            RunResourcePickup pickup = GetOrAdd<RunResourcePickup>(root);
            SerializedObject serializedPickup = new(pickup);
            serializedPickup.FindProperty("resourceType").intValue = (int)spec.Type;
            serializedPickup.FindProperty("amount").intValue = PickupAmount;
            serializedPickup.FindProperty("runProgress").objectReferenceValue = null;
            serializedPickup.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
