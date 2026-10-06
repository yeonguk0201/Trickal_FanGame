using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week17Resource0Verification
    {
        [MenuItem("Trickal Fan Game/Week 17/Setup and Verify Resource-0 Health Pickup")]
        public static void SetupAndVerifyBatch()
        {
            HealthPickup first = Week17Resource0Setup.EnsurePrefab(out string error);
            Assert(first != null, $"Resource-0 setup failed: {error}");
            string guid = AssetDatabase.AssetPathToGUID(Week17Resource0Setup.PrefabPath);
            HealthPickup second = Week17Resource0Setup.EnsurePrefab(out error);
            Assert(second != null, $"Resource-0 setup rerun failed: {error}");
            Assert(AssetDatabase.AssetPathToGUID(Week17Resource0Setup.PrefabPath) == guid,
                "Rerunning Resource-0 setup must keep the health pickup prefab GUID.");
            Assert(second.GetComponents<HealthPickup>().Length == 1 &&
                   second.GetComponents<Rigidbody2D>().Length == 1 &&
                   second.GetComponents<CircleCollider2D>().Length == 1,
                "Rerunning Resource-0 setup must not duplicate prefab components.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 17/Verify Resource-0 Health Pickup")]
        public static void Verify()
        {
            ValidateLayerMatrix();
            ValidatePrefab();
            ValidateCollectRules();
            ValidateColliderFiltering();
            Debug.Log("Resource-0 health pickup verification passed: one pickup heals exactly one heart, is " +
                      "collected only when the whole heart fits under max HP, stays on the floor at full or " +
                      "half-missing HP, never grants twice, ignores non-player colliders, and uses a solid " +
                      "Pickup-layer body that only collides with the player, walls and other pickups.");
        }

        private static void ValidateLayerMatrix()
        {
            int pickupLayer = LayerMask.NameToLayer(HealthPickup.LayerName);
            Assert(pickupLayer >= 0, $"Layer '{HealthPickup.LayerName}' must exist.");
            int[] collidingLayers =
            {
                LayerMask.NameToLayer("Player"),
                LayerMask.NameToLayer("Environment"),
                pickupLayer,
            };
            Assert(Array.TrueForAll(collidingLayers, layer => layer >= 0),
                "Player and Environment layers must exist.");

            for (int layer = 0; layer < 32; layer++)
            {
                bool shouldCollide = Array.IndexOf(collidingLayers, layer) >= 0;
                bool collides = !Physics2D.GetIgnoreLayerCollision(pickupLayer, layer);
                Assert(collides == shouldCollide,
                    $"Pickup layer collision with layer {layer} ('{LayerMask.LayerToName(layer)}') must be " +
                    (shouldCollide ? "enabled." : "disabled so enemies and projectiles pass through hearts."));
            }
        }

        private static void ValidatePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week17Resource0Setup.PrefabPath);
            Assert(prefab != null, $"Health pickup prefab must exist at {Week17Resource0Setup.PrefabPath}.");
            HealthPickup pickup = prefab.GetComponent<HealthPickup>();
            Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
            CircleCollider2D collider = prefab.GetComponent<CircleCollider2D>();
            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
            Assert(pickup != null && body != null && collider != null && renderer != null,
                "Health pickup prefab must have HealthPickup, Rigidbody2D, CircleCollider2D and SpriteRenderer.");
            Assert(prefab.layer == LayerMask.NameToLayer(HealthPickup.LayerName),
                "Health pickup prefab must use the Pickup layer.");
            Assert(pickup.HealUnits == HealthUnits.UnitsPerHeart,
                "A health pickup must heal exactly one heart (two units).");
            Assert(!collider.isTrigger && collider.radius > 0f,
                "A health pickup must have a solid collider so the player can push it.");
            Assert(body.bodyType == RigidbodyType2D.Dynamic && Mathf.Approximately(body.gravityScale, 0f) &&
                   body.freezeRotation && body.linearDamping > 0f && body.mass < 1f,
                "A health pickup must be a light, damped, non-rotating dynamic body without gravity.");
            Assert(renderer.sprite != null, "A health pickup must show a heart sprite.");
        }

        private static void ValidateCollectRules()
        {
            GameObject player = CreatePlayer(out Health health);
            GameObject pickupObject = null;
            try
            {
                Assert(health.UsesHealthUnits && Mathf.Approximately(health.MaxHealth, 10f),
                    "The verification player must start at 10 half-heart units.");

                pickupObject = CreatePickup(out HealthPickup pickup);
                health.ResetHealth();
                Assert(!pickup.CanCollect(health) && !pickup.Collect(health) && pickupObject != null &&
                       Mathf.Approximately(health.CurrentHealth, 10f),
                    "At full HP the pickup must stay on the floor without healing.");

                SetMissingUnits(health, 1);
                Assert(!pickup.Collect(health) && pickupObject != null &&
                       Mathf.Approximately(health.CurrentHealth, 9f),
                    "With only half a heart missing the pickup must stay on the floor without healing.");

                SetMissingUnits(health, 2);
                Assert(pickup.Collect(health) && pickupObject == null &&
                       Mathf.Approximately(health.CurrentHealth, 10f),
                    "With one heart missing the pickup must heal to max HP and disappear.");
                Assert(!pickup.Collect(health) && Mathf.Approximately(health.CurrentHealth, 10f),
                    "A collected pickup must never heal a second time.");

                pickupObject = CreatePickup(out pickup);
                SetMissingUnits(health, 3);
                Assert(pickup.Collect(health) && pickupObject == null &&
                       Mathf.Approximately(health.CurrentHealth, 9f),
                    "With one and a half hearts missing the pickup must heal exactly one heart.");

                pickupObject = CreatePickup(out pickup);
                health.ResetHealth();
                health.TakeDamage(20f);
                Assert(health.IsDead && !pickup.Collect(health) && pickupObject != null,
                    "A dead player must not collect a health pickup.");
                Assert(!pickup.Collect(null) && pickupObject != null,
                    "A missing Health must not collect a health pickup.");
            }
            finally
            {
                if (pickupObject != null) Object.DestroyImmediate(pickupObject);
                Object.DestroyImmediate(player);
            }
        }

        private static void ValidateColliderFiltering()
        {
            GameObject player = CreatePlayer(out Health health);
            GameObject enemy = new("Resource-0 Verification Enemy", typeof(CircleCollider2D), typeof(Health));
            InvokeLifecycle(enemy.GetComponent<Health>(), "Awake");
            enemy.GetComponent<Health>().TakeDamage(1f);
            GameObject pickupObject = CreatePickup(out HealthPickup pickup);
            try
            {
                SetMissingUnits(health, 2);
                Assert(!pickup.TryCollectFrom(null) && !pickup.TryCollectFrom(enemy.GetComponent<Collider2D>()) &&
                       pickupObject != null,
                    "Only the player's collider may collect a health pickup, even when another Health is hurt.");
                Assert(pickup.TryCollectFrom(player.GetComponent<Collider2D>()) && pickupObject == null &&
                       Mathf.Approximately(health.CurrentHealth, health.MaxHealth),
                    "Touching the player's collider must collect a pickup whose whole heart fits.");
            }
            finally
            {
                if (pickupObject != null) Object.DestroyImmediate(pickupObject);
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(player);
            }
        }

        private static void SetMissingUnits(Health health, int missingUnits)
        {
            health.ResetHealth();
            if (missingUnits == 1)
            {
                // Enemy damage has a one-heart minimum, so a half-heart gap is made by damage then healing.
                health.TakeDamage(3f);
                health.Heal(2f);
            }
            else if (missingUnits > 1)
            {
                health.TakeDamage(missingUnits);
            }

            Assert(Mathf.Approximately(health.MaxHealth - health.CurrentHealth, missingUnits),
                $"Verification setup must leave exactly {missingUnits} missing units.");
        }

        private static GameObject CreatePlayer(out Health health)
        {
            GameObject player = new("Resource-0 Verification Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            health = player.AddComponent<Health>();
            PlayerStats stats = player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerMovement>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(stats, "Awake");
            return player;
        }

        private static GameObject CreatePickup(out HealthPickup pickup)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week17Resource0Setup.PrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = Vector3.one * 1000f;
            pickup = instance.GetComponent<HealthPickup>();
            InvokeLifecycle(pickup, "Awake");
            return instance;
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
