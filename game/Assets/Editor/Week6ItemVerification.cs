using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week6ItemVerification
    {
        [MenuItem("Trickal Fan Game/Verify Week 6 Items and Synergy")]
        public static void Verify()
        {
            ItemDefinition multiShot = LoadDefinition("item-06");
            ItemDefinition pierce = LoadDefinition("item-11");
            ItemDefinition healOnKill = LoadDefinition("item-08");

            GameObject firstPlayer = CreatePlayer("Week6 Verification Player A", out Health firstHealth,
                out PlayerProjectileAttack firstAttack, out PlayerInventory firstInventory);
            GameObject secondPlayer = CreatePlayer("Week6 Verification Player B", out _,
                out PlayerProjectileAttack secondAttack, out PlayerInventory secondInventory);

            try
            {
                int firstActivations = 0;
                firstInventory.SynergyActivated += _ => firstActivations++;
                Assert(firstInventory.TryAcquire(multiShot), "Multi Shot should be acquirable.");
                Assert(firstInventory.TryAcquire(pierce), "Pierce should be acquirable after Multi Shot.");
                Assert(firstInventory.IsMultiShotPierceSynergyActive, "Multi Shot -> Pierce should activate synergy.");
                Assert(firstActivations == 1, "Synergy should activate exactly once.");
                Assert(firstAttack.ProjectileCount == 2 && firstAttack.PierceCount == 1,
                    "Every shot should create two projectiles with one pierce after synergy activation.");

                Assert(firstInventory.TryAcquire(multiShot), "The second Multi Shot stack should be accepted.");
                Assert(!firstInventory.TryAcquire(multiShot), "Multi Shot should respect its two-stack cap.");
                Assert(firstAttack.ProjectileCount == 3 && firstActivations == 1,
                    "Duplicate acquisition should add power without reactivating synergy.");

                int secondActivations = 0;
                secondInventory.SynergyActivated += _ => secondActivations++;
                Assert(secondInventory.TryAcquire(pierce), "Pierce should be acquirable first.");
                Assert(!secondInventory.IsMultiShotPierceSynergyActive, "One source item must not activate synergy.");
                Assert(secondInventory.TryAcquire(multiShot), "Multi Shot should be acquirable after Pierce.");
                Assert(secondInventory.IsMultiShotPierceSynergyActive && secondActivations == 1,
                    "Pierce -> Multi Shot should activate synergy exactly once.");
                Assert(secondAttack.ProjectileCount == 2 && secondAttack.PierceCount == 1,
                    "Reverse acquisition order should produce the same attack state.");

                Assert(firstInventory.TryAcquire(healOnKill), "Heal On Kill should be acquirable.");
                Assert(firstAttack.HealOnKill == 1, "Heal On Kill should add one healing point.");
                firstHealth.TakeDamage(3);
                Assert(firstHealth.Heal(firstAttack.HealOnKill) == 1, "Kill healing should restore one missing HP.");

                Debug.Log("Week 6 verification passed: both acquisition orders, single activation, stack caps, " +
                          "multi-shot pierce state, and healing values are valid.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstPlayer);
                UnityEngine.Object.DestroyImmediate(secondPlayer);
            }
        }

        private static GameObject CreatePlayer(
            string objectName,
            out Health health,
            out PlayerProjectileAttack attack,
            out PlayerInventory inventory)
        {
            GameObject player = new(objectName);
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            health = player.AddComponent<Health>();
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            attack = player.AddComponent<PlayerProjectileAttack>();
            inventory = player.AddComponent<PlayerInventory>();

            InvokeAwake(health);
            InvokeAwake(movement);
            InvokeAwake(attack);
            InvokeAwake(inventory);
            return player;
        }

        private static ItemDefinition LoadDefinition(string itemId)
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Items/{itemId}.asset");
            if (definition == null)
            {
                throw new InvalidOperationException($"Missing item definition: {itemId}");
            }

            return definition;
        }

        private static void InvokeAwake(MonoBehaviour component)
        {
            MethodInfo awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(component, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
