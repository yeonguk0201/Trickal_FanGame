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
        [MenuItem("Trickal Fan Game/Verify PlayerStats and Item Effects")]
        public static void Verify()
        {
            ItemDefinition multiShot = LoadDefinition("item-06");
            ItemDefinition pierce = LoadDefinition("item-11");
            ItemDefinition healOnKill = LoadDefinition("item-08");
            ItemDefinition attackDamage = LoadDefinition("item-01");
            ItemDefinition maxHealth = LoadDefinition("item-02");
            ItemDefinition moveSpeed = LoadDefinition("item-03");

            GameObject firstPlayer = CreatePlayer("Week6 Verification Player A", out Health firstHealth,
                out PlayerStats firstStats, out PlayerMovement firstMovement,
                out PlayerProjectileAttack firstAttack, out PlayerInventory firstInventory);
            GameObject secondPlayer = CreatePlayer("Week6 Verification Player B", out _,
                out PlayerStats secondStats, out _,
                out PlayerProjectileAttack secondAttack, out PlayerInventory secondInventory);

            try
            {
                Assert(firstStats.MaxHealth == 10 && firstHealth.MaxHealth == firstStats.MaxHealth,
                    "PlayerStats should own the base max HP synchronized to Health.");
                Assert(firstStats.AttackDamage == 1 && firstAttack.CurrentDamage == firstStats.AttackDamage,
                    "PlayerProjectileAttack should read base damage from PlayerStats.");
                Assert(Mathf.Approximately(firstStats.MoveSpeed, 5f) &&
                       Mathf.Approximately(firstMovement.CurrentMoveSpeed, firstStats.MoveSpeed),
                    "PlayerMovement should read base movement speed from PlayerStats.");

                Assert(firstInventory.TryAcquire(attackDamage), "Attack Boost should be acquirable.");
                Assert(firstInventory.TryAcquire(attackDamage), "Attack Boost should stack without a cap.");
                Assert(firstStats.AttackDamage == 3 && firstAttack.CurrentDamage == 3,
                    "Stacked attack items should update PlayerStats and projectile damage.");

                firstHealth.TakeDamage(4);
                Assert(firstInventory.TryAcquire(maxHealth), "Max Health Boost should be acquirable.");
                Assert(firstStats.MaxHealth == 13 && firstHealth.MaxHealth == 13 && firstHealth.CurrentHealth == 9,
                    "Max HP should be owned by PlayerStats and heal only the three added HP.");

                Assert(firstInventory.TryAcquire(moveSpeed), "Move Speed Boost should be acquirable.");
                Assert(Mathf.Approximately(firstStats.MoveSpeed, 5.75f) &&
                       Mathf.Approximately(firstMovement.CurrentMoveSpeed, 5.75f),
                    "Movement items should update PlayerStats and the movement consumer.");

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
                Assert(secondStats.ProjectileCount == 2 && secondStats.PierceCount == 1 &&
                       secondAttack.ProjectileCount == 2 && secondAttack.PierceCount == 1,
                    "Reverse acquisition order should produce the same attack state.");

                Assert(firstInventory.TryAcquire(healOnKill), "Heal On Kill should be acquirable.");
                Assert(firstAttack.HealOnKill == 1, "Heal On Kill should add one healing point.");
                firstHealth.TakeDamage(3);
                Assert(firstHealth.Heal(firstAttack.HealOnKill) == 1, "Kill healing should restore one missing HP.");

                Debug.Log("PlayerStats verification passed: stat ownership, HP and attack behavior, item stacking, " +
                          "both synergy acquisition orders, stack caps, and kill healing values are valid.");
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
            out PlayerStats stats,
            out PlayerMovement movement,
            out PlayerProjectileAttack attack,
            out PlayerInventory inventory)
        {
            GameObject player = new(objectName);
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            health = player.AddComponent<Health>();
            stats = player.AddComponent<PlayerStats>();
            movement = player.AddComponent<PlayerMovement>();
            attack = player.AddComponent<PlayerProjectileAttack>();
            inventory = player.AddComponent<PlayerInventory>();

            InvokeAwake(health);
            InvokeAwake(stats);
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
