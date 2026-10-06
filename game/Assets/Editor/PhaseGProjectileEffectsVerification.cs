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
    public static class PhaseGProjectileEffectsVerification
    {
        private const int VerificationEnemyLayer = 30;

        [MenuItem("Trickal Fan Game/Verify Phase G-5 Projectile Effects")]
        public static void Verify()
        {
            ItemDefinition telescope = LoadDefinition("item-15");
            ItemDefinition cutter = LoadDefinition("item-11");
            GameObject player = CreatePlayer(
                out Health playerHealth,
                out PlayerStats stats,
                out PlayerInventory inventory);
            GameObject homingObject = CreateHomingProjectile(out HomingSkillProjectile homing);
            GameObject distanceTargetObject = CreateTarget(
                "Phase G-5 Distance Target",
                100f,
                new Vector2(6f, 0f),
                out Health distanceTarget);
            GameObject firstTargetObject = CreateTarget(
                "Phase G-5 First Pierce Target",
                20f,
                new Vector2(1f, 0f),
                out Health firstTarget);
            GameObject splitTargetObject = CreateTarget(
                "Phase G-5 Split Target",
                10f,
                new Vector2(3f, 0f),
                out Health splitTarget);
            GameObject wall = new("Phase G-5 Wall");
            BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
            DamageCalculator.SetCriticalRollProviderForTesting(() => 1f);

            try
            {
                VerifyTelescope(inventory, stats, telescope, playerHealth, homing, distanceTarget);
                VerifyDiamondCutter(
                    inventory,
                    stats,
                    cutter,
                    playerHealth,
                    firstTarget,
                    firstTargetObject.GetComponent<Collider2D>(),
                    splitTarget,
                    splitTargetObject.GetComponent<Collider2D>(),
                    wallCollider);

                Debug.Log(
                    "Phase G-5 projectile effects verification passed: Telescope additive attack and clamped " +
                    "2-6m straight-line damage, homing impact distance, Diamond Cutter first-pierce three-way split, " +
                    "60% scale, 30% damage, 3m range, no repeat hit/re-pierce/re-split, and wall cleanup are valid.");
            }
            finally
            {
                DamageCalculator.ResetCriticalRollProvider();
                RemoveProjectiles();
                UnityEngine.Object.DestroyImmediate(wall);
                UnityEngine.Object.DestroyImmediate(splitTargetObject);
                UnityEngine.Object.DestroyImmediate(firstTargetObject);
                UnityEngine.Object.DestroyImmediate(distanceTargetObject);
                if (homingObject != null) UnityEngine.Object.DestroyImmediate(homingObject);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void VerifyTelescope(
            PlayerInventory inventory,
            PlayerStats stats,
            ItemDefinition telescope,
            Health playerHealth,
            HomingSkillProjectile homing,
            Health distanceTarget)
        {
            Assert(inventory.TryAcquire(telescope) && !inventory.TryAcquire(telescope),
                "Telescope must be acquirable once and enforce its epic one-stack cap.");
            Assert(Approximately(stats.AttackDamage, 11.5f),
                "Telescope must always add 15% attack damage.");

            DamageContext context = stats.CreateDirectDamageContext(
                playerHealth.gameObject,
                DamageSourceType.PlayerProjectile);
            Assert(Approximately(DamageCalculator.Calculate(context.WithImpactDistance(2f), 1f), 11.5f),
                "Telescope must add no distance damage at 2m.");
            Assert(Approximately(DamageCalculator.Calculate(context.WithImpactDistance(4f), 1f), 13.8f),
                "Telescope must linearly add 20% damage at the midpoint between 2m and 6m.");
            Assert(Approximately(DamageCalculator.Calculate(context.WithImpactDistance(6f), 1f), 16.1f) &&
                   Approximately(DamageCalculator.Calculate(context.WithImpactDistance(20f), 1f), 16.1f),
                "Telescope distance damage must reach and remain capped at 40% from 6m onward.");

            homing.transform.position = Vector2.zero;
            homing.Launch(
                Vector2.right,
                playerHealth,
                distanceTarget,
                stats.CreateDirectDamageContext(
                    playerHealth.gameObject,
                    DamageSourceType.PlayerSkillExplosion),
                1 << VerificationEnemyLayer);
            homing.transform.position = distanceTarget.transform.position;
            Physics2D.SyncTransforms();
            homing.ExplodeNow();
            Assert(Approximately(distanceTarget.CurrentHealth, 83.9f),
                "A homing projectile must use launch-to-impact displacement, not accumulated curved path length.");
        }

        private static void VerifyDiamondCutter(
            PlayerInventory inventory,
            PlayerStats stats,
            ItemDefinition cutter,
            Health playerHealth,
            Health firstTarget,
            Collider2D firstTargetCollider,
            Health splitTarget,
            Collider2D splitTargetCollider,
            Collider2D wallCollider)
        {
            Assert(inventory.TryAcquire(cutter) && !inventory.TryAcquire(cutter),
                "Diamond Cutter must be acquirable once and enforce its epic one-stack cap.");
            Assert(stats.PierceCount == 1 && stats.ProjectileSplitSettings.IsEnabled,
                "Diamond Cutter must grant one pierce and a valid runtime split configuration.");

            Projectile parent = CreateBasicProjectile("Phase G-5 Parent Projectile");
            parent.transform.localScale = Vector3.one * 0.5f;
            parent.Launch(
                Vector2.right * 8f,
                playerHealth,
                new DamageContext(playerHealth.gameObject, DamageSourceType.PlayerProjectile, 10f),
                stats.PierceCount,
                stats.ProjectileSplitSettings);
            parent.transform.position = firstTarget.transform.position;
            InvokePrivate(parent, "OnTriggerEnter2D", firstTargetCollider);
            Assert(parent == null && Approximately(firstTarget.CurrentHealth, 10f),
                "The parent projectile must deal normal damage, then disappear immediately after its first pierce.");

            List<Projectile> splits = new(UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None));
            splits.Sort((left, right) => SignedAngle(left.Velocity).CompareTo(SignedAngle(right.Velocity)));
            Assert(splits.Count == 3, "Diamond Cutter must create exactly three split projectiles.");
            float[] expectedAngles = { -15f, 0f, 15f };
            foreach ((Projectile split, int index) in WithIndex(splits))
            {
                Assert(split.IsSplitProjectile && Approximately(split.transform.localScale.x, 0.3f),
                    "Every split projectile must be marked as split and use 60% of the parent scale.");
                Assert(Approximately(split.DamageContext.Multiplier, 0.3f) &&
                       split.DamageContext.Source == playerHealth.gameObject,
                    "Every split projectile must preserve player source and deal 30% of parent base damage.");
                Assert(Approximately(split.MaximumTravelDistance, 3f) &&
                       Approximately(SignedAngle(split.Velocity), expectedAngles[index]),
                    "Split projectiles must use -15/0/+15 degree directions and a 3m travel cap.");
            }

            Projectile damageSplit = splits[0];
            InvokePrivate(damageSplit, "OnTriggerEnter2D", firstTargetCollider);
            Assert(damageSplit != null && Approximately(firstTarget.CurrentHealth, 10f),
                "Split projectiles must not hit the enemy that caused the split again.");
            damageSplit.transform.position = splitTarget.transform.position;
            InvokePrivate(damageSplit, "OnTriggerEnter2D", splitTargetCollider);
            Assert(damageSplit == null && Approximately(splitTarget.CurrentHealth, 7f),
                "A split projectile must deal 30% damage once, then disappear without piercing or re-splitting.");
            Assert(UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 2,
                "A split hit must not create another generation of projectiles.");

            Projectile wallSplit = splits[1];
            InvokePrivate(wallSplit, "OnTriggerEnter2D", wallCollider);
            Assert(wallSplit == null,
                "A split projectile must be destroyed by a solid wall.");

            Projectile rangeSplit = splits[2];
            rangeSplit.transform.position = rangeSplit.LaunchPosition + rangeSplit.Velocity.normalized * 3.01f;
            InvokePrivate(rangeSplit, "FixedUpdate");
            Assert(rangeSplit == null,
                "A split projectile must be destroyed once it reaches its 3m travel limit.");
        }

        private static IEnumerable<(Projectile Projectile, int Index)> WithIndex(List<Projectile> projectiles)
        {
            for (int index = 0; index < projectiles.Count; index++)
            {
                yield return (projectiles[index], index);
            }
        }

        private static GameObject CreatePlayer(
            out Health health,
            out PlayerStats stats,
            out PlayerInventory inventory)
        {
            GameObject player = new("Phase G-5 Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            health = player.AddComponent<Health>();
            stats = player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerSP>();
            player.AddComponent<PlayerCombatEvents>();
            inventory = player.AddComponent<PlayerInventory>();
            InvokeAwake(health);
            InvokeAwake(stats);
            InvokeAwake(inventory);
            return player;
        }

        private static GameObject CreateHomingProjectile(out HomingSkillProjectile projectile)
        {
            GameObject projectileObject = new("Phase G-5 Homing Projectile");
            projectileObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            projectileObject.AddComponent<CircleCollider2D>().isTrigger = true;
            projectile = projectileObject.AddComponent<HomingSkillProjectile>();
            InvokeAwake(projectile);
            return projectileObject;
        }

        private static Projectile CreateBasicProjectile(string objectName)
        {
            GameObject projectileObject = new(objectName);
            projectileObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            projectileObject.AddComponent<CircleCollider2D>().isTrigger = true;
            Projectile projectile = projectileObject.AddComponent<Projectile>();
            InvokeAwake(projectile);
            return projectile;
        }

        private static GameObject CreateTarget(
            string objectName,
            float maximumHealth,
            Vector2 position,
            out Health health)
        {
            GameObject target = new(objectName);
            target.layer = VerificationEnemyLayer;
            target.transform.position = position;
            target.AddComponent<CircleCollider2D>();
            health = target.AddComponent<Health>();
            SerializedObject serializedHealth = new(health);
            serializedHealth.FindProperty("maxHealth").floatValue = maximumHealth;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            InvokeAwake(health);
            return target;
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

        private static void RemoveProjectiles()
        {
            foreach (Projectile projectile in
                     UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(projectile.gameObject);
            }

            foreach (HomingSkillProjectile projectile in
                     UnityEngine.Object.FindObjectsByType<HomingSkillProjectile>(FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(projectile.gameObject);
            }
        }

        private static void InvokeAwake(MonoBehaviour component)
        {
            MethodInfo awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(component, null);
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

        private static float SignedAngle(Vector2 direction) => Vector2.SignedAngle(Vector2.right, direction);
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
