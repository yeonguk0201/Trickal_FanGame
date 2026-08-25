using System.IO;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week7LowerGradeSkillSetup
    {
        private const string PickupPrefabPath = "Assets/Prefabs/SPPickup.prefab";
        private const string ProjectilePrefabPath = "Assets/Prefabs/HomingSkillProjectile.prefab";

        [MenuItem("Trickal Fan Game/Setup Phase C Lower Grade Skill")]
        public static void Setup()
        {
            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                Debug.LogError("Phase C setup needs the existing Player from Phase A and Phase B.");
                return;
            }

            Sprite sharedSprite = player.GetComponent<SpriteRenderer>()?.sprite;
            SPPickup pickupPrefab = CreatePickupPrefab(sharedSprite);
            HomingSkillProjectile projectilePrefab = CreateProjectilePrefab(sharedSprite);
            if (pickupPrefab == null || projectilePrefab == null)
            {
                Debug.LogError("Phase C setup could not create its pickup or projectile prefab.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Phase C Lower Grade Skill");

            PlayerSP playerSP = GetOrAdd<PlayerSP>(player.gameObject);
            PlayerSPDropper dropper = GetOrAdd<PlayerSPDropper>(player.gameObject);
            PlayerSkill skill = GetOrAdd<PlayerSkill>(player.gameObject);
            dropper.Configure(pickupPrefab, 0.25f);
            skill.Configure(projectilePrefab, LayerMask.GetMask("Enemy"), 0.08f, 12f);

            EditorUtility.SetDirty(playerSP);
            EditorUtility.SetDirty(dropper);
            EditorUtility.SetDirty(skill);
            Undo.CollapseUndoOperations(undoGroup);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player.gameObject;
            Debug.Log(
                "Phase C lower-grade skill ready: enemy kills have a 25% SP pickup chance; " +
                "collect SP and press Space to fire a 36-degree homing fan in slot order 1-3-2-4 at 0.08-second intervals.",
                player);
        }

        private static SPPickup CreatePickupPrefab(Sprite sprite)
        {
            Directory.CreateDirectory("Assets/Prefabs");
            GameObject instance = new("SP Pickup");
            instance.transform.localScale = Vector3.one * 0.35f;
            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.35f, 0.95f, 1f);
            CircleCollider2D collider = instance.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            instance.AddComponent<SPPickup>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PickupPrefabPath);
            Object.DestroyImmediate(instance);
            return prefab != null ? prefab.GetComponent<SPPickup>() : null;
        }

        private static HomingSkillProjectile CreateProjectilePrefab(Sprite sprite)
        {
            Directory.CreateDirectory("Assets/Prefabs");
            GameObject instance = new("Homing Skill Projectile");
            instance.transform.localScale = Vector3.one * 0.28f;
            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.25f, 0.8f, 1f);
            Rigidbody2D body = instance.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = instance.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            instance.AddComponent<HomingSkillProjectile>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, ProjectilePrefabPath);
            Object.DestroyImmediate(instance);
            return prefab != null ? prefab.GetComponent<HomingSkillProjectile>() : null;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }
    }
}
