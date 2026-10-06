using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week17Resource1Verification
    {
        [MenuItem("Trickal Fan Game/Week 17/Setup and Verify Resource-1 Run Resource Pickups")]
        public static void SetupAndVerifyBatch()
        {
            Assert(Week17Resource1Setup.EnsurePrefabs(out string error), $"Resource-1 setup failed: {error}");
            Dictionary<string, string> guids = new();
            foreach (Week17Resource1Setup.PickupSpec spec in Week17Resource1Setup.Specs)
            {
                guids[spec.PrefabPath] = AssetDatabase.AssetPathToGUID(spec.PrefabPath);
            }

            Assert(Week17Resource1Setup.EnsurePrefabs(out error), $"Resource-1 setup rerun failed: {error}");
            foreach (Week17Resource1Setup.PickupSpec spec in Week17Resource1Setup.Specs)
            {
                Assert(AssetDatabase.AssetPathToGUID(spec.PrefabPath) == guids[spec.PrefabPath],
                    $"Rerunning Resource-1 setup must keep the {spec.Type} pickup prefab GUID.");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
                Assert(prefab.GetComponents<RunResourcePickup>().Length == 1 &&
                       prefab.GetComponents<Rigidbody2D>().Length == 1 &&
                       prefab.GetComponents<CircleCollider2D>().Length == 1 &&
                       prefab.GetComponents<SpriteRenderer>().Length == 1,
                    $"Rerunning Resource-1 setup must not duplicate {spec.Type} pickup components.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 17/Verify Resource-1 Run Resource Pickups")]
        public static void Verify()
        {
            ValidatePrefabs();
            ValidateWallet();
            ValidateRunProgressLifecycle();
            ValidatePickupCollection();
            ValidateColliderFiltering();
            Debug.Log("Resource-1 run resource verification passed: gold, key and bomb pickups each grant 1, " +
                      "counts cap at 99 with the overflow dropped, a full resource leaves its pickup on the " +
                      "floor, a pickup never grants twice, only the player collects, nothing is granted after " +
                      "the Run ends, and every count starts at 0 and resets with the Run.");
        }

        private static void ValidatePrefabs()
        {
            int pickupLayer = LayerMask.NameToLayer(HealthPickup.LayerName);
            Assert(pickupLayer >= 0, $"Layer '{HealthPickup.LayerName}' must exist.");
            HashSet<RunResourceType> covered = new();
            foreach (Week17Resource1Setup.PickupSpec spec in Week17Resource1Setup.Specs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
                Assert(prefab != null, $"{spec.Type} pickup prefab must exist at {spec.PrefabPath}.");
                RunResourcePickup pickup = prefab.GetComponent<RunResourcePickup>();
                Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
                CircleCollider2D collider = prefab.GetComponent<CircleCollider2D>();
                SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
                Assert(pickup != null && body != null && collider != null && renderer != null,
                    $"{spec.Type} pickup prefab must have RunResourcePickup, Rigidbody2D, CircleCollider2D " +
                    "and SpriteRenderer.");
                Assert(pickup.ResourceType == spec.Type && pickup.Amount == 1,
                    $"{spec.PrefabPath} must grant exactly one {spec.Type}.");
                Assert(covered.Add(pickup.ResourceType), $"Only one prefab may grant {spec.Type}.");
                Assert(prefab.layer == pickupLayer, $"{spec.Type} pickup prefab must use the Pickup layer.");
                Assert(!collider.isTrigger && collider.radius > 0f,
                    $"{spec.Type} pickup must have a solid collider so a capped pickup can be pushed.");
                Assert(body.bodyType == RigidbodyType2D.Dynamic && Mathf.Approximately(body.gravityScale, 0f) &&
                       body.freezeRotation && body.linearDamping > 0f && body.mass < 1f,
                    $"{spec.Type} pickup must be a light, damped, non-rotating dynamic body without gravity.");
                Assert(renderer.sprite != null, $"{spec.Type} pickup must show a sprite.");
            }

            foreach (RunResourceType type in Enum.GetValues(typeof(RunResourceType)))
            {
                Assert(covered.Contains(type), $"Resource {type} must have a pickup prefab.");
            }
        }

        private static void ValidateWallet()
        {
            Assert((int)RunResourceType.Gold == 0 && (int)RunResourceType.Key == 1 &&
                   (int)RunResourceType.Bomb == 2,
                "RunResourceType values are serialized in prefabs and must stay stable.");

            RunResourceWallet wallet = new();
            List<(RunResourceType type, int count)> changes = new();
            wallet.Changed += (type, count) => changes.Add((type, count));

            foreach (RunResourceType type in Enum.GetValues(typeof(RunResourceType)))
            {
                Assert(wallet.GetCount(type) == 0 && wallet.CanAccept(type), $"{type} must start at 0.");
                Assert(wallet.Add(type, 0) == 0 && wallet.Add(type, -3) == 0 && wallet.GetCount(type) == 0,
                    $"A non-positive {type} grant must be ignored.");
                Assert(wallet.Add(type, 1) == 1 && wallet.GetCount(type) == 1, $"Adding one {type} must hold 1.");
                Assert(wallet.Add(type, 95) == 95 && wallet.GetCount(type) == 96, $"{type} must accumulate.");
                Assert(wallet.Add(type, 5) == 3 && wallet.GetCount(type) == RunResourceWallet.MaxCount,
                    $"{type} must fill to 99 and drop the overflow.");
                Assert(!wallet.CanAccept(type) && wallet.Add(type, 1) == 0 &&
                       wallet.GetCount(type) == RunResourceWallet.MaxCount,
                    $"A full {type} must refuse further grants.");
            }

            Assert(changes.Count == 9, $"Only effective grants may raise Changed, got {changes.Count} events.");
            RunResourceType invalid = (RunResourceType)99;
            Assert(!wallet.CanAccept(invalid) && wallet.Add(invalid, 1) == 0 && wallet.GetCount(invalid) == 0,
                "An undefined resource type must never be granted.");

            changes.Clear();
            wallet.Clear();
            foreach (RunResourceType type in Enum.GetValues(typeof(RunResourceType)))
            {
                Assert(wallet.GetCount(type) == 0, $"Clear must return {type} to 0.");
            }
            Assert(changes.Count == 3 && changes.TrueForAll(change => change.count == 0),
                "Clear must report each emptied resource once.");
            wallet.Clear();
            Assert(changes.Count == 3, "Clearing an empty wallet must not raise Changed.");
        }

        private static void ValidateRunProgressLifecycle()
        {
            GameObject holder = new("Resource-1 Verification Progress");
            GameObject nextHolder = new("Resource-1 Verification Next Run");
            try
            {
                RunProgress progress = holder.AddComponent<RunProgress>();
                int reportedKeys = -1;
                progress.ResourceChanged += (type, count) =>
                {
                    if (type == RunResourceType.Key) reportedKeys = count;
                };

                Assert(progress.TryAddResource(RunResourceType.Key, 2) == 2 &&
                       progress.GetResourceCount(RunResourceType.Key) == 2 && reportedKeys == 2,
                    "RunProgress must hold resources and report changes for the HUD.");

                progress.StopProgression();
                Assert(!progress.CanAcceptResource(RunResourceType.Key) &&
                       progress.TryAddResource(RunResourceType.Key, 1) == 0 &&
                       progress.GetResourceCount(RunResourceType.Key) == 2,
                    "No resource may be granted after the Run has ended.");

                progress.ResetProgress();
                Assert(progress.GetResourceCount(RunResourceType.Key) == 0 && reportedKeys == 0 &&
                       progress.CanAcceptResource(RunResourceType.Key),
                    "Resetting the Run must return every resource to 0.");

                RunProgress nextRun = nextHolder.AddComponent<RunProgress>();
                foreach (RunResourceType type in Enum.GetValues(typeof(RunResourceType)))
                {
                    Assert(nextRun.GetResourceCount(type) == 0, $"A new Run must start with 0 {type}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(nextHolder);
                Object.DestroyImmediate(holder);
            }
        }

        private static void ValidatePickupCollection()
        {
            GameObject holder = new("Resource-1 Verification Progress");
            RunProgress progress = holder.AddComponent<RunProgress>();
            GameObject pickupObject = null;
            try
            {
                foreach (Week17Resource1Setup.PickupSpec spec in Week17Resource1Setup.Specs)
                {
                    pickupObject = CreatePickup(spec.Type, out RunResourcePickup pickup);
                    Assert(!pickup.Collect(null) && pickupObject != null,
                        $"A {spec.Type} pickup must not be collected without a Run.");
                    Assert(pickup.Collect(progress) && pickupObject == null &&
                           progress.GetResourceCount(spec.Type) == 1,
                        $"A {spec.Type} pickup must grant exactly one and disappear.");
                    Assert(!pickup.Collect(progress) && progress.GetResourceCount(spec.Type) == 1,
                        $"A collected {spec.Type} pickup must never grant a second time.");

                    progress.TryAddResource(spec.Type, RunResourceWallet.MaxCount);
                    pickupObject = CreatePickup(spec.Type, out pickup);
                    Assert(!pickup.CanCollect(progress) && !pickup.Collect(progress) && pickupObject != null &&
                           !pickup.IsCollected &&
                           progress.GetResourceCount(spec.Type) == RunResourceWallet.MaxCount,
                        $"At 99 {spec.Type} the pickup must stay on the floor.");
                    Object.DestroyImmediate(pickupObject);
                }

                progress.ResetProgress();
                progress.TryAddResource(RunResourceType.Gold, 97);
                pickupObject = CreatePickup(RunResourceType.Gold, out RunResourcePickup bigGold);
                SerializedObject serialized = new(bigGold);
                serialized.FindProperty("amount").intValue = 5;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert(bigGold.Collect(progress) && pickupObject == null &&
                       progress.GetResourceCount(RunResourceType.Gold) == RunResourceWallet.MaxCount,
                    "A pickup that only partly fits must fill to 99, drop the rest and disappear.");

                progress.ResetProgress();
                progress.StopProgression();
                pickupObject = CreatePickup(RunResourceType.Bomb, out RunResourcePickup lateBomb);
                Assert(!lateBomb.Collect(progress) && pickupObject != null &&
                       progress.GetResourceCount(RunResourceType.Bomb) == 0,
                    "A pickup touched after the Run has ended must not be collected.");
            }
            finally
            {
                if (pickupObject != null) Object.DestroyImmediate(pickupObject);
                Object.DestroyImmediate(holder);
            }
        }

        private static void ValidateColliderFiltering()
        {
            GameObject holder = new("Resource-1 Verification Progress");
            RunProgress progress = holder.AddComponent<RunProgress>();
            GameObject player = CreatePlayer();
            GameObject enemy = new("Resource-1 Verification Enemy", typeof(CircleCollider2D));
            GameObject pickupObject = CreatePickup(RunResourceType.Key, out RunResourcePickup pickup);
            try
            {
                pickup.BindRunProgress(progress);
                Assert(!pickup.TryCollectFrom(null) && !pickup.TryCollectFrom(enemy.GetComponent<Collider2D>()) &&
                       pickupObject != null && progress.GetResourceCount(RunResourceType.Key) == 0,
                    "Only the player's collider may collect a resource pickup.");
                Assert(pickup.TryCollectFrom(player.GetComponent<Collider2D>()) && pickupObject == null &&
                       progress.GetResourceCount(RunResourceType.Key) == 1,
                    "Touching the player's collider must grant the pickup to the bound Run.");
            }
            finally
            {
                if (pickupObject != null) Object.DestroyImmediate(pickupObject);
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(holder);
            }
        }

        private static GameObject CreatePlayer()
        {
            GameObject player = new("Resource-1 Verification Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            Health health = player.AddComponent<Health>();
            PlayerStats stats = player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerMovement>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(stats, "Awake");
            return player;
        }

        private static GameObject CreatePickup(RunResourceType type, out RunResourcePickup pickup)
        {
            RunResourcePickup prefab = Week17Resource1Setup.LoadPrefab(type);
            Assert(prefab != null, $"{type} pickup prefab must exist.");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
            instance.transform.position = Vector3.one * 1000f;
            pickup = instance.GetComponent<RunResourcePickup>();
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
