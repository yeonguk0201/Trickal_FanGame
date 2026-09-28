using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week7LowerGradeSkillVerification
    {
        private const int VerificationEnemyLayer = 30;

        [MenuItem("Trickal Fan Game/Verify Phase C Lower Grade Skill")]
        public static void Verify()
        {
            GameObject player = CreatePlayer(
                out Health playerHealth,
                out PlayerStats stats,
                out PlayerCombatEvents combatEvents,
                out PlayerSP playerSP,
                out PlayerSPDropper dropper,
                out PlayerSkill skill);
            GameObject pickupTemplateObject = CreatePickup("Phase C Pickup Template", out SPPickup pickupTemplate);
            GameObject projectileTemplateObject = CreateProjectile(
                "Phase C Projectile Template",
                out HomingSkillProjectile projectileTemplate);
            GameObject dropParent = new("Phase C Drop Parent");
            GameObject enemy = CreateEnemy("Phase C Explosion Enemy", new Vector2(3f, 0f), 300, out Health enemyHealth);
            GameObject secondEnemy = CreateEnemy("Phase C Second Enemy", new Vector2(0f, 5f), 300,
                out Health secondEnemyHealth);
            GameObject thirdEnemy = CreateEnemy("Phase C Third Enemy", new Vector2(-7f, 0f), 300,
                out Health thirdEnemyHealth);
            GameObject wall = new("Phase C Solid Wall");
            Collider2D wallCollider = wall.AddComponent<BoxCollider2D>();
            List<HomingSkillProjectile> launched = new();
            DamageCalculator.SetCriticalRollProviderForTesting(() => 1f);

            try
            {
                VerifySPBoundaries(playerSP);
                VerifyFullSPPickupDisappears(playerSP);

                dropper.Configure(pickupTemplate, 0.25f);
                Assert(dropper.ShouldDrop(0f) && dropper.ShouldDrop(0.249f) && !dropper.ShouldDrop(0.25f),
                    "The default SP drop boundary must be exactly 25%.");
                dropper.Configure(pickupTemplate, 1f);
                int pickupCountBeforeKill = UnityEngine.Object.FindObjectsByType<SPPickup>(FindObjectsSortMode.None).Length;
                GameObject dropEnemy = CreateEnemy("Phase C Drop Enemy", Vector2.zero, 1, out Health dropEnemyHealth);
                dropEnemy.transform.SetParent(dropParent.transform);
                dropEnemyHealth.TakeDamage(new DamageContext(
                    player,
                    DamageSourceType.PlayerProjectile,
                    1));
                int pickupCountAfterKill = UnityEngine.Object.FindObjectsByType<SPPickup>(FindObjectsSortMode.None).Length;
                Assert(pickupCountAfterKill == pickupCountBeforeKill + 1,
                    "One eligible player kill at 100% chance must spawn one floor pickup.");
                UnityEngine.Object.DestroyImmediate(dropEnemy);
                RemoveSpawnedPickups(pickupTemplate);

                stats.AddAttackDamage(40);
                const float firstCastTime = 100f;
                const float shotInterval = 0.08f;
                const float fanSpacingAngle = 12f;
                skill.Configure(projectileTemplate, 1 << VerificationEnemyLayer, shotInterval, fanSpacingAngle);
                skill.ProjectileLaunched += launched.Add;
                Assert(playerSP.CurrentSP == playerSP.MaxSP,
                    "The skill verification must retain the capped SP from the pickup boundary check.");
                int spBeforeCast = playerSP.CurrentSP;
                Assert(skill.TryCast(Vector2.left, firstCastTime),
                    "Space-equivalent casting must succeed when SP and a prefab are available.");
                Assert(playerSP.CurrentSP == spBeforeCast - 1,
                    "One lower-grade skill cast must consume exactly one SP.");
                Assert(launched.Count == 1 && skill.IsFiring,
                    "One cast must launch its first projectile immediately and keep the salvo active.");
                int spDuringSalvo = playerSP.CurrentSP;
                Assert(!skill.TryCast(Vector2.right, firstCastTime + 0.01f) &&
                       playerSP.CurrentSP == spDuringSalvo,
                    "Repeated Space during the active salvo must not overlap salvos or spend another SP.");
                skill.Tick(firstCastTime + shotInterval - 0.001f);
                Assert(launched.Count == 1,
                    "The second projectile must not launch before the configured interval.");
                skill.Tick(firstCastTime + shotInterval);
                Assert(launched.Count == 2, "The second projectile must launch at one interval.");
                skill.Tick(firstCastTime + shotInterval * 2f);
                Assert(launched.Count == 3, "The third projectile must launch at two intervals.");
                skill.Tick(firstCastTime + shotInterval * 3f);
                Assert(launched.Count == 4 && !skill.IsFiring,
                    "One cast must finish with exactly four projectiles over three short intervals.");

                Health[] expectedTargets = { enemyHealth, secondEnemyHealth, thirdEnemyHealth, enemyHealth };
                int[] expectedSlotOrder = { 0, 2, 1, 3 };
                for (int index = 0; index < launched.Count; index++)
                {
                    HomingSkillProjectile projectile = launched[index];
                    Assert(projectile.Target == expectedTargets[index],
                        "Three enemies must receive four shots in distance-ordered round-robin order. " +
                        $"Shot {index + 1} targeted {projectile.Target?.name ?? "none"}.");
                    Assert(projectile.HasAssignedTarget,
                        "A projectile launched with a target must preserve its target-assigned collision rule.");
                    float expectedAngle = (expectedSlotOrder[index] - 1.5f) * fanSpacingAngle;
                    Vector2 expectedDirection = Rotate(Vector2.left, expectedAngle);
                    Assert(Vector2.Dot(projectile.Direction, expectedDirection) > 0.9999f,
                        "Targeted projectiles must leave through fan slots in 1-3-2-4 order before homing.");
                    Assert(projectile.DamageContext.Source == player &&
                           projectile.DamageContext.SourceType == DamageSourceType.PlayerSkillExplosion &&
                           projectile.DamageContext.BaseDamage == stats.AttackDamage &&
                           Mathf.Approximately(projectile.DamageContext.Multiplier, 1f),
                        "Each explosion must capture current attack damage at a 100% multiplier.");
                    InvokePrivate(projectile, "OnTriggerEnter2D", wallCollider);
                    Assert(projectile != null && !projectile.DidExplode,
                        "A targeted projectile must pass through solid walls without being consumed.");
                    projectile.transform.position = enemy.transform.position;
                }

                Physics2D.SyncTransforms();
                foreach (HomingSkillProjectile projectile in launched.ToArray())
                {
                    projectile.ExplodeNow();
                }
                Assert(enemyHealth.CurrentHealth == 100,
                    "Four overlapping explosions must stack to current attack damage x400% on one enemy.");

                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(secondEnemy);
                UnityEngine.Object.DestroyImmediate(thirdEnemy);
                launched.Clear();
                Assert(playerSP.TryAdd(), "The no-target verification needs one additional SP.");
                const float secondCastTime = 200f;
                Assert(skill.TryCast(Vector2.left, secondCastTime),
                    "Casting must still succeed when no target exists.");
                skill.Tick(secondCastTime + shotInterval * 3f);
                Assert(launched.Count == 4, "The no-target cast must still launch four projectiles.");
                for (int index = 0; index < launched.Count; index++)
                {
                    HomingSkillProjectile projectile = launched[index];
                    float expectedAngle = (expectedSlotOrder[index] - 1.5f) * fanSpacingAngle;
                    Assert(projectile.Target == null &&
                           !projectile.HasAssignedTarget &&
                           Vector2.Dot(projectile.Direction, Rotate(Vector2.left, expectedAngle)) > 0.9999f,
                        "Without a target, projectiles must keep the same 36-degree fan and 1-3-2-4 slot order.");
                    InvokePrivate(projectile, "OnTriggerEnter2D", wallCollider);
                    Assert(projectile == null,
                        "An untargeted projectile must be consumed by a solid wall without exploding.");
                }

                launched.Clear();
                while (playerSP.TrySpend())
                {
                }
                Assert(!skill.TryCast() && launched.Count == 0,
                    "A cast without SP must not launch projectiles or consume anything.");

                Debug.Log(
                    "Phase C verification passed: SP cap/spend, full-cap pickup consumption, kill-based floor drop, " +
                    "four interval-fired 36-degree fan shots in 1-3-2-4 order, direction-first homing, " +
                    "multi-target round-robin, current-attack 100% " +
                    "stacked explosions, targeted wall passage, untargeted wall expiry, and zero-SP rejection are valid.");
            }
            finally
            {
                DamageCalculator.ResetCriticalRollProvider();
                skill.ProjectileLaunched -= launched.Add;
                RemoveSpawnedPickups(pickupTemplate);
                RemoveSpawnedProjectiles(projectileTemplate);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
                if (secondEnemy != null) UnityEngine.Object.DestroyImmediate(secondEnemy);
                if (thirdEnemy != null) UnityEngine.Object.DestroyImmediate(thirdEnemy);
                UnityEngine.Object.DestroyImmediate(wall);
                UnityEngine.Object.DestroyImmediate(dropParent);
                UnityEngine.Object.DestroyImmediate(projectileTemplateObject);
                if (pickupTemplateObject != null) UnityEngine.Object.DestroyImmediate(pickupTemplateObject);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void VerifySPBoundaries(PlayerSP playerSP)
        {
            int changedCount = 0;
            playerSP.Changed += (_, _) => changedCount++;
            Assert(playerSP.CurrentSP == 0 && playerSP.MaxSP == 3,
                "PlayerSP must start at 0 with a default maximum of 3.");
            Assert(playerSP.TryAdd(3) && playerSP.CurrentSP == 3,
                "PlayerSP must accept gains up to its maximum.");
            Assert(!playerSP.TryAdd() && playerSP.CurrentSP == 3,
                "PlayerSP must reject overflow without changing its value.");
            Assert(playerSP.TrySpend() && playerSP.CurrentSP == 2,
                "PlayerSP must spend one point per successful request.");
            Assert(!playerSP.TrySpend(3) && playerSP.CurrentSP == 2,
                "PlayerSP must reject an unaffordable cost without partial spending.");
            Assert(changedCount == 2,
                "PlayerSP must publish changes only when its value actually changes.");
        }

        private static void VerifyFullSPPickupDisappears(PlayerSP playerSP)
        {
            playerSP.TryAdd();
            GameObject pickupObject = CreatePickup("Phase C Full-SP Pickup", out SPPickup pickup);
            Assert(pickup.Collect(playerSP), "A valid player must collect an SP pickup even at the SP cap.");
            Assert(playerSP.CurrentSP == playerSP.MaxSP,
                "A pickup at maximum SP must not increase SP.");
            Assert(pickupObject == null,
                "A pickup collected at maximum SP must still disappear.");
        }

        private static GameObject CreatePlayer(
            out Health health,
            out PlayerStats stats,
            out PlayerCombatEvents combatEvents,
            out PlayerSP playerSP,
            out PlayerSPDropper dropper,
            out PlayerSkill skill)
        {
            GameObject player = new("Phase C Verification Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            health = player.AddComponent<Health>();
            stats = player.AddComponent<PlayerStats>();
            combatEvents = player.AddComponent<PlayerCombatEvents>();
            player.AddComponent<PlayerMovement>();
            playerSP = player.AddComponent<PlayerSP>();
            dropper = player.AddComponent<PlayerSPDropper>();
            skill = player.AddComponent<PlayerSkill>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(stats, "Awake");
            InvokeLifecycle(playerSP, "Awake");
            InvokeLifecycle(dropper, "Awake");
            InvokeLifecycle(dropper, "OnEnable");
            InvokeLifecycle(skill, "Awake");
            return player;
        }

        private static GameObject CreateEnemy(string name, Vector2 position, float maxHealth, out Health health)
        {
            GameObject enemy = new(name);
            enemy.layer = VerificationEnemyLayer;
            enemy.transform.position = position;
            enemy.AddComponent<CircleCollider2D>();
            health = enemy.AddComponent<Health>();
            SerializedObject serializedHealth = new(health);
            serializedHealth.FindProperty("maxHealth").floatValue = maxHealth;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            InvokeLifecycle(health, "Awake");
            return enemy;
        }

        private static GameObject CreatePickup(string name, out SPPickup pickup)
        {
            GameObject pickupObject = new(name);
            CircleCollider2D collider = pickupObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            pickup = pickupObject.AddComponent<SPPickup>();
            return pickupObject;
        }

        private static GameObject CreateProjectile(string name, out HomingSkillProjectile projectile)
        {
            GameObject projectileObject = new(name);
            projectileObject.transform.position = Vector3.one * 1000f;
            Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            projectile = projectileObject.AddComponent<HomingSkillProjectile>();
            return projectileObject;
        }

        private static void RemoveSpawnedPickups(SPPickup template)
        {
            foreach (SPPickup pickup in UnityEngine.Object.FindObjectsByType<SPPickup>(FindObjectsSortMode.None))
            {
                if (pickup != template)
                {
                    UnityEngine.Object.DestroyImmediate(pickup.gameObject);
                }
            }
        }

        private static void RemoveSpawnedProjectiles(HomingSkillProjectile template)
        {
            foreach (HomingSkillProjectile projectile in
                     UnityEngine.Object.FindObjectsByType<HomingSkillProjectile>(FindObjectsSortMode.None))
            {
                if (projectile != template)
                {
                    UnityEngine.Object.DestroyImmediate(projectile.gameObject);
                }
            }
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }

            method.Invoke(component, null);
        }

        private static void InvokePrivate(MonoBehaviour component, string methodName, params object[] arguments)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }

            method.Invoke(component, arguments);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine).normalized;
        }
    }
}
