using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Hp4LifeGemVerification
    {
        private const string LifeGemPath = "Assets/Items/artifact-life-gem.asset";
        private const string BalloonArmorPath = "Assets/Items/item-02.asset";

        [MenuItem("Trickal Fan Game/HP/Verify HP-4 Life Gem")]
        public static void Verify()
        {
            ItemDefinition gem = Load(LifeGemPath);
            ValidateTriggerAndPulses(gem);
            ValidateAcquireBelowThreshold(gem);
            ValidateDeathAndLethalHit(gem);
            ValidateScalesWithMaxHealth(gem, Load(BalloonArmorPath));
            Debug.Log("HP-4 life gem verification passed: the gem triggers once per floor at or below 30% HP, " +
                      "heals 45% of max HP floored to half hearts one unit at a time over 3 seconds, starts " +
                      "immediately when acquired below the threshold, never triggers from a lethal hit, stops on " +
                      "death, and rejects a second stack.");
        }

        public static void VerifyBatch()
        {
            Verify();
            Week16Content0Verification.Verify();
            Hp1HealthUnitsVerification.VerifyRegressionBatch();
        }

        private static void ValidateTriggerAndPulses(ItemDefinition gem)
        {
            GameObject player = CreatePlayer(out Health health, out PlayerInventory inventory);
            try
            {
                Assert(inventory.TryAcquire(gem), "The life gem must be acquirable.");
                Assert(!inventory.TryAcquire(gem), "The life gem must reject a second stack.");
                PlayerLowHealthRecovery recovery = player.GetComponent<PlayerLowHealthRecovery>();
                Assert(recovery != null && recovery.IsConfigured && !recovery.IsConsumed &&
                       Mathf.Approximately(recovery.HealthThreshold, 0.3f) &&
                       Mathf.Approximately(recovery.HealRatio, 0.45f) &&
                       Mathf.Approximately(recovery.DurationSeconds, 3f),
                    "Acquiring the gem at full HP must configure 30% / 45% / 3s without triggering.");
                Assert(!recovery.Configure(0.3f, 0.45f, 3f), "The gem runtime must not be configured twice.");

                health.TakeDamage(6f);
                Assert(Mathf.Approximately(health.CurrentHealth, 4f) && !recovery.IsConsumed,
                    "2 hearts is above the 1.5-heart threshold and must not trigger the gem.");

                health.TakeDamage(1f);
                Assert(Mathf.Approximately(health.CurrentHealth, 2f) && recovery.IsConsumed && recovery.IsActive &&
                       recovery.TotalPulses == 4 && Mathf.Approximately(recovery.PulseAmount, 1f),
                    "Dropping to 1 heart must trigger 45% of 10 units floored to 4 half-heart pulses.");

                float start = recovery.StartedAt;
                Assert(recovery.Tick(start + 0.74f) == 0 && Mathf.Approximately(health.CurrentHealth, 2f),
                    "No pulse may arrive before the first quarter of the duration.");
                Assert(recovery.Tick(start + 0.75f) == 1 && Mathf.Approximately(health.CurrentHealth, 3f),
                    "The first half-heart pulse must arrive at 0.75 seconds.");
                Assert(recovery.Tick(start + 3f) == 3 && Mathf.Approximately(health.CurrentHealth, 6f) &&
                       !recovery.IsActive,
                    "All four pulses (2 hearts) must be delivered by the end of 3 seconds.");
                Assert(recovery.Tick(start + 10f) == 0 && Mathf.Approximately(health.CurrentHealth, 6f),
                    "A finished recovery must not heal again.");

                health.TakeDamage(4f);
                Assert(Mathf.Approximately(health.CurrentHealth, 2f) && !recovery.IsActive &&
                       recovery.DeliveredPulses == 4,
                    "The gem must trigger only once on the same floor.");

                RunProgress progress = player.GetComponent<RunProgress>();
                progress.RecordRoomEntry(2, 1);
                Assert(recovery.IsConsumed && recovery.IsActive && recovery.TotalPulses == 4,
                    "Entering a new floor below the threshold must refresh and immediately trigger the gem once.");
                recovery.Tick(recovery.StartedAt + 3f);
                Assert(!recovery.IsActive && Mathf.Approximately(health.CurrentHealth, 6f),
                    "The refreshed floor charge must deliver exactly one full recovery.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static void ValidateAcquireBelowThreshold(ItemDefinition gem)
        {
            GameObject player = CreatePlayer(out Health health, out PlayerInventory inventory);
            try
            {
                health.TakeDamage(7f);
                Assert(Mathf.Approximately(health.CurrentHealth, 3f), "Setup must leave the player at 1.5 hearts.");
                Assert(inventory.TryAcquire(gem), "The life gem must be acquirable below the threshold.");
                PlayerLowHealthRecovery recovery = player.GetComponent<PlayerLowHealthRecovery>();
                Assert(recovery.IsConsumed && recovery.IsActive && recovery.TotalPulses == 4,
                    "Acquiring the gem at exactly 30% HP must start the recovery immediately.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static void ValidateDeathAndLethalHit(ItemDefinition gem)
        {
            GameObject lethal = CreatePlayer(out Health lethalHealth, out PlayerInventory lethalInventory);
            GameObject dying = CreatePlayer(out Health dyingHealth, out PlayerInventory dyingInventory);
            try
            {
                Assert(lethalInventory.TryAcquire(gem), "Lethal-hit setup could not acquire the gem.");
                lethalHealth.TakeDamage(10f);
                Assert(lethalHealth.IsDead && !lethal.GetComponent<PlayerLowHealthRecovery>().IsConsumed,
                    "A lethal hit must not trigger or consume the gem.");

                Assert(dyingInventory.TryAcquire(gem), "Death setup could not acquire the gem.");
                dyingHealth.TakeDamage(8f);
                PlayerLowHealthRecovery recovery = dying.GetComponent<PlayerLowHealthRecovery>();
                Assert(recovery.IsActive, "Death setup must start the recovery.");
                dyingHealth.TakeDamage(2f);
                Assert(dyingHealth.IsDead && recovery.Tick(recovery.StartedAt + 3f) == 0 &&
                       Mathf.Approximately(dyingHealth.CurrentHealth, 0f) && !recovery.IsActive,
                    "Dying during the recovery must cancel the remaining pulses.");
            }
            finally
            {
                Object.DestroyImmediate(lethal);
                Object.DestroyImmediate(dying);
            }
        }

        private static void ValidateScalesWithMaxHealth(ItemDefinition gem, ItemDefinition armor)
        {
            GameObject player = CreatePlayer(out Health health, out PlayerInventory inventory);
            try
            {
                Assert(inventory.TryAcquire(armor) && inventory.TryAcquire(gem),
                    "Max-health setup could not acquire balloon armor and the gem.");
                health.SetShield(0f);
                health.TakeDamage(8f);
                PlayerLowHealthRecovery recovery = player.GetComponent<PlayerLowHealthRecovery>();
                Assert(Mathf.Approximately(health.CurrentHealth, 4f) && !recovery.IsConsumed,
                    "At 12 units, 4 units is above the 3.6-unit threshold.");
                health.TakeDamage(1f);
                Assert(Mathf.Approximately(health.CurrentHealth, 2f) && recovery.TotalPulses == 5,
                    "At 12 units the gem must heal 45% floored to 5 half-heart pulses.");
                recovery.Tick(recovery.StartedAt + 3f);
                Assert(Mathf.Approximately(health.CurrentHealth, 7f),
                    "The full 12-unit recovery must restore 2.5 hearts.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static GameObject CreatePlayer(out Health health, out PlayerInventory inventory)
        {
            GameObject player = new("HP-4 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(RunProgress), typeof(PlayerInventory));
            health = player.GetComponent<Health>();
            inventory = player.GetComponent<PlayerInventory>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
            InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
            player.GetComponent<RunProgress>().RecordRoomEntry(1, 1);
            InvokeLifecycle(inventory, "Awake");
            Assert(health.UsesHealthUnits && Mathf.Approximately(health.CurrentHealth, 10f),
                "HP-4 verification requires a 10-unit player.");
            return player;
        }

        private static ItemDefinition Load(string path)
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            Assert(definition != null && definition.IsValid, $"Missing valid item definition at {path}.");
            return definition;
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
