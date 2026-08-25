using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week7DamageContextVerification
    {
        [MenuItem("Trickal Fan Game/Verify Damage Context and Projectile")]
        public static void Verify()
        {
            GameObject player = CreatePlayer(out Health ownerHealth, out PlayerStats stats,
                out PlayerCombatEvents combatEvents, out PlayerProjectileAttack attack);
            GameObject enemy = CreateTarget("Damage Verification Enemy", out Health enemyHealth,
                out Collider2D enemyCollider);
            GameObject boss = CreateBoss(out Health bossHealth, out BossController bossController,
                out Collider2D bossCollider);
            GameObject piercingProjectile = CreateProjectile("Damage Verification Piercing Projectile",
                out Projectile projectile);
            GameObject enemyKillProjectile = CreateProjectile("Damage Verification Enemy Kill Projectile",
                out Projectile enemyKiller);
            GameObject bossKillProjectile = CreateProjectile("Damage Verification Boss Kill Projectile",
                out Projectile bossKiller);

            try
            {
                stats.AddAttackDamage(2);
                stats.AddProjectiles(1);
                stats.AddPierce(2);

                DamageContext context = attack.CreateDamageContext();
                Assert(context.Source == player, "The player GameObject should be the projectile attack source.");
                Assert(context.SourceType == DamageSourceType.PlayerProjectile,
                    "The damage source type should identify the player projectile attack.");
                Assert(context.BaseDamage == stats.AttackDamage && DamageCalculator.Calculate(context) == 3,
                    "DamageContext should carry the current PlayerStats attack damage without drift.");
                Assert(attack.ProjectileCount == 2,
                    "Multi Shot should still expose two projectiles through PlayerStats.");

                DamageContext enemyReceivedContext = default;
                DamageContext bossReceivedContext = default;
                int enemyReceivedDamage = 0;
                int bossReceivedDamage = 0;
                enemyHealth.DamageApplied += (received, damage, _) =>
                {
                    enemyReceivedContext = received;
                    enemyReceivedDamage = damage;
                };
                bossHealth.DamageApplied += (received, damage, _) =>
                {
                    bossReceivedContext = received;
                    bossReceivedDamage = damage;
                };

                projectile.Launch(Vector2.zero, ownerHealth, context, attack.PierceCount);
                InvokeHit(projectile, enemyCollider);
                Assert(enemyHealth.CurrentHealth == 7 && enemyReceivedDamage == 3,
                    "The projectile should apply the calculated damage to a normal enemy.");
                Assert(IsSameAttack(enemyReceivedContext, context),
                    "The normal enemy should receive the original source and damage values.");
                Assert(GetRemainingPierces(projectile) == 1,
                    "The first target should consume exactly one configured pierce.");

                InvokeHit(projectile, enemyCollider);
                Assert(enemyHealth.CurrentHealth == 7 && GetRemainingPierces(projectile) == 1,
                    "The same projectile must not damage or consume pierce on the same target twice.");

                InvokeHit(projectile, bossCollider);
                Assert(bossHealth.CurrentHealth == 7 && bossReceivedDamage == 3,
                    "The same projectile damage path should apply to a boss Health component.");
                Assert(IsSameAttack(bossReceivedContext, context),
                    "The boss should receive the original player projectile source.");
                Assert(GetRemainingPierces(projectile) == 0,
                    "The second distinct target should consume the second configured pierce.");

                int enemyDeaths = 0;
                int playerKillEvents = 0;
                Health lastKilledTarget = null;
                combatEvents.EnemyKilled += killEvent =>
                {
                    playerKillEvents++;
                    lastKilledTarget = killEvent.Target;
                };
                enemyHealth.Died += () => enemyDeaths++;
                DamageContext finishingContext = new(
                    player,
                    DamageSourceType.PlayerProjectile,
                    enemyHealth.CurrentHealth);
                enemyKiller.Launch(Vector2.zero, ownerHealth, finishingContext, 1);
                InvokeHit(enemyKiller, enemyCollider);
                Assert(enemyHealth.IsDead && enemyDeaths == 1 && playerKillEvents == 1 &&
                       lastKilledTarget == enemyHealth,
                    "Enemy death and the common player kill event should fire exactly once.");

                int bossDeaths = 0;
                bossController.Died += () => bossDeaths++;
                DamageContext bossFinishingContext = new(
                    player,
                    DamageSourceType.PlayerProjectile,
                    bossHealth.CurrentHealth);
                bossKiller.Launch(Vector2.zero, ownerHealth, bossFinishingContext, 1);
                InvokeHit(bossKiller, bossCollider);
                Assert(bossHealth.IsDead && bossDeaths == 1 && playerKillEvents == 2 &&
                       lastKilledTarget == bossHealth,
                    "Boss death and the common player kill event should fire exactly once.");

                Debug.Log("DamageContext verification passed: PlayerStats damage, source identity, common damage " +
                          "calculation, duplicate-hit protection, piercing, enemy death, and boss damage are valid.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(piercingProjectile);
                UnityEngine.Object.DestroyImmediate(enemyKillProjectile);
                UnityEngine.Object.DestroyImmediate(bossKillProjectile);
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(boss);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static GameObject CreatePlayer(
            out Health health,
            out PlayerStats stats,
            out PlayerCombatEvents combatEvents,
            out PlayerProjectileAttack attack)
        {
            GameObject player = new("Damage Verification Player");
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

        private static GameObject CreateTarget(
            string objectName,
            out Health health,
            out Collider2D collider)
        {
            GameObject target = new(objectName);
            collider = target.AddComponent<CircleCollider2D>();
            health = target.AddComponent<Health>();
            InvokeAwake(health);
            return target;
        }

        private static GameObject CreateBoss(
            out Health health,
            out BossController controller,
            out Collider2D collider)
        {
            GameObject boss = CreateTarget("Damage Verification Boss", out health, out collider);
            controller = boss.AddComponent<BossController>();
            InvokeAwake(controller);
            return boss;
        }

        private static GameObject CreateProjectile(string objectName, out Projectile projectile)
        {
            GameObject projectileObject = new(objectName);
            projectileObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            projectileObject.AddComponent<CircleCollider2D>();
            projectile = projectileObject.AddComponent<Projectile>();
            InvokeAwake(projectile);
            return projectileObject;
        }

        private static bool IsSameAttack(DamageContext actual, DamageContext expected)
        {
            return actual.Source == expected.Source &&
                   actual.SourceType == expected.SourceType &&
                   actual.BaseDamage == expected.BaseDamage &&
                   Mathf.Approximately(actual.Multiplier, expected.Multiplier);
        }

        private static int GetRemainingPierces(Projectile projectile)
        {
            FieldInfo field = typeof(Projectile).GetField(
                "remainingPierces",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(typeof(Projectile).FullName, "remainingPierces");
            }

            return (int)field.GetValue(projectile);
        }

        private static void InvokeHit(Projectile projectile, Collider2D target)
        {
            MethodInfo hit = typeof(Projectile).GetMethod(
                "Hit",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (hit == null)
            {
                throw new MissingMethodException(typeof(Projectile).FullName, "Hit");
            }

            hit.Invoke(projectile, new object[] { target });
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
