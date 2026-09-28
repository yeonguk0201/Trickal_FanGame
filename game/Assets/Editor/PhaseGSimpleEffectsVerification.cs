using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class PhaseGSimpleEffectsVerification
    {
        [MenuItem("Trickal Fan Game/Verify Phase G-3 Simple Effects")]
        public static void Verify()
        {
            ItemDefinition sword = LoadDefinition("item-01");
            ItemDefinition awl = LoadDefinition("item-12");
            ItemDefinition arrow = LoadDefinition("item-04");
            ItemDefinition pillow = LoadDefinition("item-08");
            GameObject player = CreatePlayer(
                out Health playerHealth,
                out PlayerStats stats,
                out PlayerCombatEvents combatEvents,
                out PlayerProjectileAttack attack,
                out PlayerInventory inventory);
            GameObject[] enemies = new GameObject[4];
            Health[] enemyHealths = new Health[enemies.Length];
            for (int index = 0; index < enemies.Length; index++)
            {
                enemies[index] = CreateEnemy($"Phase G-3 Enemy {index + 1}", out enemyHealths[index]);
            }
            DamageCalculator.SetCriticalRollProviderForTesting(() => 1f);

            try
            {
                InvokeLifecycle(attack, "OnEnable");
                VerifyStackedStats(inventory, stats, sword, awl, arrow);
                VerifyPillowHealing(
                    player,
                    playerHealth,
                    stats,
                    combatEvents,
                    inventory,
                    pillow,
                    enemyHealths);

                Debug.Log(
                    "Phase G-3 simple effects verification passed: sword attack +5%, awl critical chance +3%p, " +
                    "arrow attack speed +10%, stack caps, and Pillow half-heart healing every N kills are valid.");
            }
            finally
            {
                InvokeLifecycle(attack, "OnDisable");
                DamageCalculator.ResetCriticalRollProvider();
                foreach (GameObject enemy in enemies)
                {
                    UnityEngine.Object.DestroyImmediate(enemy);
                }
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void VerifyStackedStats(
            PlayerInventory inventory,
            PlayerStats stats,
            ItemDefinition sword,
            ItemDefinition awl,
            ItemDefinition arrow)
        {
            AcquireStacks(inventory, sword, 5);
            Assert(Approximately(stats.AttackDamage, 12.5f),
                "Five sword stacks must add 25% attack damage before multiplying the base stat.");
            Assert(!inventory.TryAcquire(sword) && inventory.GetStackCount(sword.ItemId) == 5,
                "Sword must reject acquisitions beyond its five-stack cap without changing stats.");

            AcquireStacks(inventory, awl, 5);
            Assert(Approximately(stats.CriticalChance, 0.20f),
                "Five awl stacks must add 15 percentage points to the base 5% critical chance.");
            Assert(!inventory.TryAcquire(awl) && inventory.GetStackCount(awl.ItemId) == 5,
                "Awl must reject acquisitions beyond its five-stack cap without changing stats.");

            AcquireStacks(inventory, arrow, 3);
            Assert(Approximately(stats.AttackSpeed, 1.30f),
                "Three arrow stacks must add 30% attack speed before multiplying the base stat.");
            Assert(!inventory.TryAcquire(arrow) && inventory.GetStackCount(arrow.ItemId) == 3,
                "Arrow must reject acquisitions beyond its three-stack cap without changing stats.");
        }

        private static void VerifyPillowHealing(
            GameObject player,
            Health playerHealth,
            PlayerStats stats,
            PlayerCombatEvents combatEvents,
            PlayerInventory inventory,
            ItemDefinition pillow,
            Health[] enemies)
        {
            AcquireStacks(inventory, pillow, 1);
            Assert(stats.PeriodicKillHealInterval == 2 &&
                   Approximately(stats.PeriodicKillHealAmount, 1f) &&
                   Approximately(stats.HealOnKillMaxHealthPercent, 0f),
                "One Pillow stack must heal one half-heart unit every two kills without max-HP scaling.");

            playerHealth.TakeDamage(9f);
            Assert(Approximately(playerHealth.CurrentHealth, 1f),
                "Pillow verification setup must leave the player at one half-heart unit.");
            enemies[0].TakeDamage(CreatePlayerKill(player));
            Assert(Approximately(playerHealth.CurrentHealth, 1f) && stats.PeriodicKillHealProgress == 1,
                "The first kill with one Pillow stack must only advance the kill counter.");
            Assert(!TryReportAgain(combatEvents, enemies[0], CreatePlayerKill(player)) &&
                   stats.PeriodicKillHealProgress == 1,
                "A duplicate report for the same defeated enemy must not advance the kill counter.");
            enemies[1].TakeDamage(CreatePlayerKill(player));
            Assert(Approximately(playerHealth.CurrentHealth, 2f) && stats.PeriodicKillHealProgress == 0,
                "The second kill with one Pillow stack must heal exactly one half-heart unit and reset the counter.");

            AcquireStacks(inventory, pillow, 1);
            Assert(stats.PeriodicKillHealInterval == 1 && Approximately(stats.PeriodicKillHealAmount, 1f),
                "The second Pillow stack must lower the kill requirement by one without raising the heal amount.");
            Assert(!inventory.TryAcquire(pillow) && inventory.GetStackCount(pillow.ItemId) == 2,
                "Pillow must reject acquisitions beyond its two-stack cap.");

            enemies[2].TakeDamage(CreatePlayerKill(player));
            enemies[3].TakeDamage(CreatePlayerKill(player));
            Assert(Approximately(playerHealth.CurrentHealth, 4f),
                "With two Pillow stacks every kill must heal one half-heart unit.");

            stats.AddPeriodicKillHeal(1f, 2);
            Assert(stats.PeriodicKillHealInterval == 1,
                "Extra periodic heal stacks must keep the kill requirement at its one-kill minimum.");
        }

        private static void AcquireStacks(PlayerInventory inventory, ItemDefinition definition, int count)
        {
            for (int index = 0; index < count; index++)
            {
                Assert(inventory.TryAcquire(definition),
                    $"{definition.DisplayName} stack {index + 1} should be acquirable.");
            }
        }

        private static GameObject CreatePlayer(
            out Health health,
            out PlayerStats stats,
            out PlayerCombatEvents combatEvents,
            out PlayerProjectileAttack attack,
            out PlayerInventory inventory)
        {
            GameObject player = new("Phase G-3 Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            health = player.AddComponent<Health>();
            stats = player.AddComponent<PlayerStats>();
            combatEvents = player.AddComponent<PlayerCombatEvents>();
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            attack = player.AddComponent<PlayerProjectileAttack>();
            inventory = player.AddComponent<PlayerInventory>();

            InvokeAwake(health);
            InvokeAwake(stats);
            InvokeAwake(movement);
            InvokeAwake(attack);
            InvokeAwake(inventory);
            return player;
        }

        private static GameObject CreateEnemy(string objectName, out Health health)
        {
            GameObject enemy = new(objectName);
            health = enemy.AddComponent<Health>();
            InvokeAwake(health);
            return enemy;
        }

        private static DamageContext CreatePlayerKill(GameObject player)
        {
            return new DamageContext(
                player,
                DamageSourceType.PlayerProjectile,
                10f,
                deliveryType: DamageDeliveryType.Direct,
                criticalChance: 0f);
        }

        private static bool TryReportAgain(
            PlayerCombatEvents combatEvents,
            Health target,
            DamageContext killingBlow)
        {
            MethodInfo report = typeof(PlayerCombatEvents).GetMethod(
                "TryReportEnemyKilled",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (report == null)
            {
                throw new MissingMethodException(typeof(PlayerCombatEvents).FullName, "TryReportEnemyKilled");
            }

            return (bool)report.Invoke(combatEvents, new object[] { target, killingBlow });
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

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }

            method.Invoke(component, null);
        }

        private static bool Approximately(float left, float right) => Mathf.Approximately(left, right);

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
