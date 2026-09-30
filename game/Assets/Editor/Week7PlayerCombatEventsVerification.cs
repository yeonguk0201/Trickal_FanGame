using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week7PlayerCombatEventsVerification
    {
        [MenuItem("Trickal Fan Game/Verify Player Combat Events")]
        public static void Verify()
        {
            GameObject player = CreatePlayer(
                out Health playerHealth,
                out PlayerStats stats,
                out PlayerCombatEvents combatEvents,
                out PlayerProjectileAttack attack);
            GameObject hazard = new("Combat Events Verification Hazard");
            GameObject firstEnemy = CreateEnemy("Combat Events Enemy 1", out Health firstHealth);
            GameObject outsideKillEnemy = CreateEnemy("Combat Events Outside Kill Enemy", out Health outsideHealth);
            GameObject invalidPlayerSourceEnemy = CreateEnemy(
                "Combat Events Invalid Player Source Enemy",
                out Health invalidPlayerSourceHealth);
            GameObject secondEnemy = CreateEnemy("Combat Events Enemy 2", out Health secondHealth);
            GameObject thirdEnemy = CreateEnemy("Combat Events Enemy 3", out Health thirdHealth);
            GameObject unsubscribedEnemy = CreateEnemy("Combat Events Unsubscribed Enemy", out Health unsubscribedHealth);

            try
            {
                stats.AddHealOnKill(1);
                playerHealth.TakeDamage(5);

                int observedKills = 0;
                PlayerEnemyKilledEvent lastKill = default;
                Action<PlayerEnemyKilledEvent> observer = killEvent =>
                {
                    observedKills++;
                    lastKill = killEvent;
                };
                combatEvents.EnemyKilled += observer;

                // Repeated enabling must not register the HealOnKill consumer twice.
                InvokeLifecycle(attack, "OnEnable");
                InvokeLifecycle(attack, "OnEnable");

                DamageContext playerKill = CreatePlayerKill(player);
                firstHealth.TakeDamage(playerKill);
                Assert(observedKills == 1 && lastKill.Target == firstHealth,
                    "A player projectile kill should publish the defeated Health exactly once.");
                Assert(IsSameAttack(lastKill.KillingBlow, playerKill),
                    "The kill event should preserve the killing DamageContext.");
                Assert(playerHealth.CurrentHealth == 6,
                    "HealOnKill should consume the shared event once even after repeated subscription attempts.");

                firstHealth.TakeDamage(playerKill);
                Assert(!TryReportAgain(combatEvents, firstHealth, playerKill),
                    "The combat event hub should reject an explicitly repeated target report.");
                Assert(observedKills == 1 && playerHealth.CurrentHealth == 6,
                    "Duplicate damage and reports must not publish or heal again.");

                outsideHealth.TakeDamage(new DamageContext(
                    hazard,
                    DamageSourceType.EnemyContact,
                    outsideHealth.CurrentHealth));
                invalidPlayerSourceHealth.TakeDamage(new DamageContext(
                    player,
                    DamageSourceType.EnemyContact,
                    invalidPlayerSourceHealth.CurrentHealth));
                Assert(outsideHealth.IsDead && invalidPlayerSourceHealth.IsDead && observedKills == 1,
                    "Non-player causes and non-player damage types must not publish player kill events.");
                Assert(playerHealth.CurrentHealth == 6,
                    "Deaths outside the player combat path must not trigger HealOnKill.");

                secondHealth.TakeDamage(CreatePlayerKill(player));
                thirdHealth.TakeDamage(CreatePlayerKill(player));
                Assert(observedKills == 3 && playerHealth.CurrentHealth == 8,
                    "Consecutive player kills should each publish once and heal once.");

                combatEvents.EnemyKilled -= observer;
                InvokeLifecycle(attack, "OnDisable");
                InvokeLifecycle(attack, "OnDisable");

                int remainingSubscriberCalls = 0;
                Action<PlayerEnemyKilledEvent> remainingSubscriber = _ => remainingSubscriberCalls++;
                combatEvents.EnemyKilled += remainingSubscriber;
                unsubscribedHealth.TakeDamage(CreatePlayerKill(player));
                combatEvents.EnemyKilled -= remainingSubscriber;

                Assert(remainingSubscriberCalls == 1,
                    "The event hub should continue publishing after an unrelated consumer unsubscribes.");
                Assert(observedKills == 3 && playerHealth.CurrentHealth == 8,
                    "Removed observers and HealOnKill must not receive later kills.");

                Debug.Log("PlayerCombatEvents verification passed: player-only kill routing, duplicate suppression, " +
                          "consecutive kills, HealOnKill consumption, and subscription cleanup are valid.");
            }
            finally
            {
                InvokeLifecycle(attack, "OnDisable");
                UnityEngine.Object.DestroyImmediate(unsubscribedEnemy);
                UnityEngine.Object.DestroyImmediate(thirdEnemy);
                UnityEngine.Object.DestroyImmediate(secondEnemy);
                UnityEngine.Object.DestroyImmediate(invalidPlayerSourceEnemy);
                UnityEngine.Object.DestroyImmediate(outsideKillEnemy);
                UnityEngine.Object.DestroyImmediate(firstEnemy);
                UnityEngine.Object.DestroyImmediate(hazard);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static GameObject CreatePlayer(
            out Health health,
            out PlayerStats stats,
            out PlayerCombatEvents combatEvents,
            out PlayerProjectileAttack attack)
        {
            GameObject player = new("Combat Events Verification Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            health = player.AddComponent<Health>();
            stats = player.AddComponent<PlayerStats>();
            combatEvents = player.AddComponent<PlayerCombatEvents>();
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            attack = player.AddComponent<PlayerProjectileAttack>();

            InvokeAwake(health);
            InvokeAwake(stats);
            InvokeAwake(movement);
            InvokeAwake(attack);
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
            return new DamageContext(player, DamageSourceType.PlayerProjectile, 10);
        }

        private static bool IsSameAttack(DamageContext actual, DamageContext expected)
        {
            return actual.Source == expected.Source &&
                   actual.SourceType == expected.SourceType &&
                   actual.BaseDamage == expected.BaseDamage &&
                   Mathf.Approximately(actual.Multiplier, expected.Multiplier);
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

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
