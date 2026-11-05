using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class RangedContactDamageVerification
    {
        private const string SelfDestructPrefabPath = "Assets/Prefabs/SelfDestructFairy.prefab";

        [MenuItem("Trickal Fan Game/Week 28/Setup and Verify Contact-0 Ranged Contact Damage")]
        public static void SetupAndVerifyBatch()
        {
            Dictionary<string, string> before = RangedContactDamageSetup.PrefabPaths
                .ToDictionary(path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            RangedContactDamageSetup.Setup();
            RangedContactDamageSetup.Setup();
            foreach (KeyValuePair<string, string> entry in before)
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Contact-0 setup changed or lost the GUID for {entry.Key}.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 28/Verify Contact-0 Ranged Contact Damage")]
        public static void Verify()
        {
            foreach (string path in RangedContactDamageSetup.PrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert(prefab != null, $"Missing {path}.");
                ContactDamage[] contacts = prefab.GetComponents<ContactDamage>();
                Assert(contacts.Length == 1, $"{path} must have exactly one ContactDamage.");
                Assert(contacts[0].HalfHeartDamage && contacts[0].DamageTier == EnemyDamageTier.Light &&
                       Mathf.Approximately(contacts[0].Cooldown, RangedContactDamageSetup.Cooldown),
                    $"{path} must deal half a heart on contact with a 1 s cooldown.");
                Assert(prefab.GetComponent<Rigidbody2D>() != null &&
                       prefab.GetComponents<Collider2D>().Any(collider => collider.enabled && !collider.isTrigger),
                    $"{path} needs a body that collides with the player for contact damage to land.");
                Assert(prefab.GetComponent<EnemyContactRecoil>() == null,
                    $"{path} must not bounce off on touch like a rammer.");
                VerifyHalfHeartHit(prefab);
            }

            // Every enemy hurts on touch, except the self-destruct enemy, which hurts only by exploding.
            GameObject selfDestruct = AssetDatabase.LoadAssetAtPath<GameObject>(SelfDestructPrefabPath);
            Assert(selfDestruct != null && selfDestruct.GetComponent<ContactDamage>() == null,
                "The self-destruct enemy must keep no contact damage.");
            string[] missing = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path != SelfDestructPrefabPath)
                .Select(path => (path, prefab: AssetDatabase.LoadAssetAtPath<GameObject>(path)))
                // Boss obstacles (부스러기 크림·반죽) also carry TestEnemy but never move or get knocked back.
                .Where(entry => entry.prefab != null && entry.prefab.GetComponent<TestEnemy>() != null &&
                                (entry.prefab.GetComponent<EnemyBehaviorContext>() != null ||
                                 entry.prefab.GetComponent<KnockbackReceiver>() != null) &&
                                entry.prefab.GetComponent<ContactDamage>() == null)
                .Select(entry => entry.path).ToArray();
            Assert(missing.Length == 0, "Every enemy except the self-destruct one needs contact damage: " +
                                        string.Join(", ", missing));

            Debug.Log("Contact-0 ranged contact damage verification passed.");
        }

        private static void VerifyHalfHeartHit(GameObject prefab)
        {
            GameObject root = new("Contact-0 Damage Verification");
            root.transform.position = new Vector3(-9000f, 9000f);
            try
            {
                foreach (int floor in new[] { 1, 3 })
                {
                    // A fresh player per floor: the first hit's invulnerability would block the next one.
                    GameObject player = new($"Contact-0 Player {floor}", typeof(PlayerMovement),
                        typeof(CircleCollider2D));
                    player.transform.SetParent(root.transform, false);
                    player.layer = LayerMask.NameToLayer("Player");
                    player.GetComponent<Rigidbody2D>().gravityScale = 0f;
                    Health health = player.GetComponent<Health>();
                    typeof(Health).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(health, null);
                    health.EnableHealthUnits();
                    GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    EnemyFloorLevel.Apply(enemy, floor);
                    ContactDamage contact = enemy.GetComponent<ContactDamage>();
                    float before = health.CurrentHealth;
                    Assert(contact.TryApplyDamage(health, 0f) &&
                           Mathf.Approximately(before - health.CurrentHealth, HealthUnits.HalfHeartDamageUnits),
                        $"{prefab.name} contact damage must take exactly half a heart on floor {floor}.");
                    Assert(!contact.TryApplyDamage(health, 0.5f),
                        $"{prefab.name} contact damage must wait for its cooldown.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
