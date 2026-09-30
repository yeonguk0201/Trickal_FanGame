using System;
using System.Collections.Generic;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class PhaseGConditionalEffectsVerification
    {
        [MenuItem("Trickal Fan Game/Verify Phase G-4 Conditional Effects")]
        public static void Verify()
        {
            ItemDefinition armor = LoadDefinition("item-02");
            ItemDefinition hat = LoadDefinition("item-03");
            ItemDefinition staff = LoadDefinition("item-13");
            GameObject player = CreatePlayer(
                out Health health,
                out PlayerStats stats,
                out PlayerSP playerSP,
                out PlayerSkill skill,
                out PlayerInventory inventory);
            GameObject projectileTemplateObject = CreateProjectile(out HomingSkillProjectile projectileTemplate);
            List<HomingSkillProjectile> launched = new();

            try
            {
                VerifyBalloonArmor(inventory, health, armor);
                VerifyGoblinHat(inventory, health, stats, hat);
                VerifyErpinStaff(inventory, stats, playerSP, skill, staff, projectileTemplate, launched);

                Debug.Log(
                    "Phase G-4 conditional effects verification passed: Balloon Armor max HP and non-stacking " +
                    "50% shield, Goblin Hat 30%-HP movement activation/release, and Erpin Staff maximum SP plus " +
                    "pre-spend six-projectile salvo are valid.");
            }
            finally
            {
                skill.ProjectileLaunched -= launched.Add;
                RemoveSpawnedProjectiles(projectileTemplate);
                UnityEngine.Object.DestroyImmediate(projectileTemplateObject);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void VerifyBalloonArmor(
            PlayerInventory inventory,
            Health health,
            ItemDefinition armor)
        {
            Assert(inventory.TryAcquire(armor), "The first Balloon Armor stack should be acquirable.");
            Assert(Approximately(health.MaxHealth, 12f) && Approximately(health.CurrentHealth, 12f),
                "Balloon Armor must add 2 maximum HP and heal the newly added amount first.");
            Assert(Approximately(health.CurrentShield, 6f),
                "The first Balloon Armor stack must set shield to 50% of the new 12 maximum HP.");

            health.SetShield(8f);
            Assert(inventory.TryAcquire(armor), "The second Balloon Armor stack should be acquirable.");
            Assert(Approximately(health.MaxHealth, 14f) && Approximately(health.CurrentHealth, 14f),
                "Two Balloon Armor stacks must provide 4 total maximum HP.");
            Assert(Approximately(health.CurrentShield, 8f),
                "Acquiring Balloon Armor must keep a shield already greater than 50% of the new maximum HP.");
            Assert(!inventory.TryAcquire(armor) && Approximately(health.MaxHealth, 14f) &&
                   Approximately(health.CurrentShield, 8f),
                "Balloon Armor must reject a third stack without changing HP or shield.");
        }

        private static void VerifyGoblinHat(
            PlayerInventory inventory,
            Health health,
            PlayerStats stats,
            ItemDefinition hat)
        {
            Assert(inventory.TryAcquire(hat) && inventory.TryAcquire(hat),
                "Two Goblin Hat stacks should be acquirable.");
            Assert(!stats.IsBelowMoveSpeedHealthThreshold && Approximately(stats.MoveSpeed, 5f),
                "Goblin Hat must remain inactive above 30% HP.");

            health.SetShield(0f);
            health.TakeDamage(9.8f);
            Assert(Approximately(health.CurrentHealth, 4.2f) && stats.IsBelowMoveSpeedHealthThreshold &&
                   Approximately(stats.MoveSpeed, 7.5f),
                "At exactly 30% HP, two Goblin Hat stacks must add 50% movement speed.");

            health.Heal(0.1f);
            Assert(!stats.IsBelowMoveSpeedHealthThreshold && Approximately(stats.MoveSpeed, 5f),
                "Goblin Hat movement speed must be removed immediately after HP rises above 30%.");
            Assert(!inventory.TryAcquire(hat),
                "Goblin Hat must reject acquisitions beyond its two-stack cap.");
        }

        private static void VerifyErpinStaff(
            PlayerInventory inventory,
            PlayerStats stats,
            PlayerSP playerSP,
            PlayerSkill skill,
            ItemDefinition staff,
            HomingSkillProjectile projectileTemplate,
            List<HomingSkillProjectile> launched)
        {
            int changedCount = 0;
            playerSP.Changed += (_, _) => changedCount++;
            Assert(inventory.TryAcquire(staff), "Erpin Staff should be acquirable once.");
            Assert(playerSP.MaxSP == 4 && playerSP.CurrentSP == 0 && changedCount == 1,
                "Erpin Staff must add 1 maximum SP, preserve current SP, and publish the UI change.");
            Assert(stats.GetLowerGradeSkillProjectileBonus(0) == 0 &&
                   stats.GetLowerGradeSkillProjectileBonus(1) == 2,
                "Erpin Staff must grant two lower-grade projectiles only at or above its SP threshold.");
            Assert(!inventory.TryAcquire(staff) && playerSP.MaxSP == 4,
                "Erpin Staff must reject a second stack without increasing maximum SP again.");

            Assert(playerSP.TryAdd(), "The staff salvo verification needs exactly one SP.");
            skill.Configure(projectileTemplate, 0, 0.08f, 12f);
            skill.ProjectileLaunched += launched.Add;
            const float castTime = 100f;
            Assert(skill.TryCast(Vector2.down, castTime) && playerSP.CurrentSP == 0,
                "The staff bonus must be captured immediately before the cast spends its one SP.");
            skill.Tick(castTime + 0.08f * 5f);
            Assert(launched.Count == 6 && !skill.IsFiring,
                "A qualifying Erpin Staff cast must finish with exactly six projectiles.");
        }

        private static GameObject CreatePlayer(
            out Health health,
            out PlayerStats stats,
            out PlayerSP playerSP,
            out PlayerSkill skill,
            out PlayerInventory inventory)
        {
            GameObject player = new("Phase G-4 Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            health = player.AddComponent<Health>();
            stats = player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerCombatEvents>();
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            playerSP = player.AddComponent<PlayerSP>();
            skill = player.AddComponent<PlayerSkill>();
            inventory = player.AddComponent<PlayerInventory>();

            InvokeAwake(health);
            InvokeAwake(stats);
            InvokeAwake(movement);
            InvokeAwake(playerSP);
            InvokeAwake(skill);
            InvokeAwake(inventory);
            return player;
        }

        private static GameObject CreateProjectile(out HomingSkillProjectile projectile)
        {
            GameObject projectileObject = new("Phase G-4 Projectile Template");
            projectileObject.transform.position = Vector3.one * 1000f;
            projectileObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            projectileObject.AddComponent<CircleCollider2D>().isTrigger = true;
            projectile = projectileObject.AddComponent<HomingSkillProjectile>();
            return projectileObject;
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
