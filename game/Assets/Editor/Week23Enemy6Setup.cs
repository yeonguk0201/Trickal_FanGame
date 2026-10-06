using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Enemy-6: 쥬비, a small flying enemy (user decision, 2026-10-06). One player hit kills it, its touch takes half a
    // heart on every floor, it flies over pits and low obstacles straight at the player, and it is a little faster
    // than the player. For now it only comes out of 셰이디의 랜덤박스; it is in no Encounter. Re-running updates the
    // same Prefab and keeps its GUID.
    public static class Week23Enemy6Setup
    {
        public const string PrefabPath = "Assets/Prefabs/JyubiEnemy.prefab";
        public const string PrefabName = "JyubiEnemy";
        // Dies to any player hit, also after the floor health multiplier (x2 on floor 3).
        public const float MaxHealth = 1f;
        // The player's base speed is 5.
        public const float MoveSpeed = 5.5f;
        public const float DetectionRange = 20f;
        public const float ContactCooldown = 1f;
        public const float Scale = 0.5f;
        public const int SortingOrder = 2;

        // Placeholder look until 쥬비 has artwork: the chaser sprite, small and tinted.
        public static readonly Color PlaceholderColor = new(0.75f, 0.6f, 1f);

        [MenuItem("Trickal Fan Game/Week 23/Setup Enemy-6 Jyubi")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Enemy-6 setup.");
            EnsurePrefab();
            // The random box releases 쥬비, so its kind is rebuilt with the Prefab.
            Week23Obstacle5Setup.Setup();
            Debug.Log($"Enemy-6 setup complete: {PrefabPath} is a flying enemy with {MaxHealth} health, speed " +
                      $"{MoveSpeed} and half-heart contact damage, released by Shady's random box.");
        }

        public static GameObject EnsurePrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.ChaserPrefabPath);
            if (source == null)
                throw new InvalidOperationException($"Enemy-6 needs the chaser Prefab at {Week15Enemy0Setup.ChaserPrefabPath}.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                    instance.name = PrefabName;
                    if (PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath) == null)
                        throw new InvalidOperationException($"Could not create {PrefabPath}.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                contents.name = PrefabName;
                contents.transform.localScale = Vector3.one * Scale;
                // 쥬비 only rams the player: no melee swing and no ground chaser animation.
                Remove<EnemyAttackArtwork>(contents);
                Remove<EnemyMovementAnimator>(contents);
                Remove<MeleeEnemyAttack>(contents);
                Remove<EnemyAttackPresentation>(contents);

                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = PlaceholderColor;
                    renderer.sortingOrder = SortingOrder;
                }

                Health health = contents.GetComponent<Health>();
                SerializedObject serializedHealth = new(health);
                serializedHealth.FindProperty("maxHealth").floatValue = MaxHealth;
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();

                EnemyChase chase = contents.GetComponent<EnemyChase>();
                chase.Configure(MoveSpeed, DetectionRange, 0f);
                chase.ConfigureFlight(true);
                ContactDamage contact = contents.GetComponent<ContactDamage>();
                contact.Configure(EnemyDamageTier.Light, ContactCooldown);
                contact.ConfigureHalfHeart(true);
                if (contents.GetComponent<EnemyFlight>() == null) contents.AddComponent<EnemyFlight>();

                if (PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath) == null)
                    throw new InvalidOperationException($"Could not save {PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static void Remove<T>(GameObject target) where T : Component
        {
            foreach (T component in target.GetComponents<T>()) Object.DestroyImmediate(component, true);
        }
    }
}
