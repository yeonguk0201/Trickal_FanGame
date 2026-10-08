using System;
using System.Collections.Generic;
using System.Linq;
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
                    "2-6m straight-line damage, homing impact distance, Diamond Cutter four-way split on every hit " +
                    "(50% of the base shot scale, 50% damage, 3m range, no re-split, the hit enemy spared unless a " +
                    "pierce is inherited, a piercing parent splitting on each enemy), and wall cleanup are valid.");
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
            // Passive-5: no pierce of its own; every hit splits toward the screen's up, down, left and right.
            Assert(stats.PierceCount == 0 && stats.ProjectileSplitSettings.IsEnabled &&
                   stats.ProjectileSplitSettings.SplitsOnHit && stats.ProjectileSplitSettings.ProjectileCount == 4,
                "Diamond Cutter must grant no pierce and a four-way split on every hit.");

            Projectile parent = CreateBasicProjectile("Phase G-5 Parent Projectile");
            // A grown shot (칸나의 대포): the split shots still use half of the base shot size.
            parent.transform.localScale = Vector3.one * 0.8f;
            parent.Launch(
                Vector2.right * 8f,
                playerHealth,
                new DamageContext(playerHealth.gameObject, DamageSourceType.PlayerProjectile, 10f),
                stats.PierceCount,
                stats.ProjectileSplitSettings);
            parent.transform.position = firstTarget.transform.position + Vector3.left * 0.4f;
            InvokePrivate(parent, "OnTriggerEnter2D", firstTargetCollider);
            Assert(parent == null && Approximately(firstTarget.CurrentHealth, 10f),
                "Without a pierce the parent projectile must deal normal damage and disappear on its hit.");

            List<Projectile> splits = new(UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None));
            Assert(splits.Count == 4, $"Diamond Cutter must create exactly four split projectiles, not {splits.Count}.");
            foreach (Vector2 direction in ProjectileSplitSettings.OnHitDirections)
            {
                Assert(splits.Count(split => Vector2.Dot(split.Velocity.normalized, direction) > 0.999f) == 1,
                    $"One split projectile must fly toward {direction}.");
            }

            foreach (Projectile split in splits)
            {
                Assert(split.IsSplitProjectile &&
                       Approximately(split.transform.localScale.x, ProjectileSizing.PlayerBasicScale * 0.5f) &&
                       Approximately(split.Velocity.magnitude, 8f),
                    "Every split projectile must be marked as split, use 50% of the base shot scale and keep the speed.");
                Assert(Approximately(split.DamageContext.Multiplier, 0.5f) &&
                       split.DamageContext.Source == playerHealth.gameObject,
                    "Every split projectile must preserve player source and deal 50% of the shot's damage.");
                Assert(Approximately(split.MaximumTravelDistance, 3f) &&
                       ((Vector2)split.transform.position - (Vector2)firstTarget.transform.position).sqrMagnitude <
                       0.0001f,
                    "Split projectiles must start at the enemy that was hit and keep a 3m travel cap.");
            }

            Projectile damageSplit = splits[0];
            InvokePrivate(damageSplit, "OnTriggerEnter2D", firstTargetCollider);
            Assert(damageSplit != null && Approximately(firstTarget.CurrentHealth, 10f),
                "Without a pierce, split projectiles must not hit the enemy that caused the split.");
            damageSplit.transform.position = splitTarget.transform.position;
            InvokePrivate(damageSplit, "OnTriggerEnter2D", splitTargetCollider);
            Assert(damageSplit == null && Approximately(splitTarget.CurrentHealth, 5f),
                "A split projectile must deal 50% damage once, then disappear without piercing or re-splitting.");
            Assert(UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 3,
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
            RemoveProjectiles();

            // With a pierce from another item the parent flies on and splits on every hit, and the split shots
            // inherit the pierce, so they also hit the enemy they start inside.
            stats.AddPierce(1);
            GameObject piercedObject = CreateTarget("Phase G-5 Pierced Target", 100f, new Vector2(1f, 20f),
                out Health pierced);
            GameObject secondObject = CreateTarget("Phase G-5 Second Pierced Target", 100f, new Vector2(3f, 20f),
                out Health second);
            try
            {
                Projectile piercing = CreateBasicProjectile("Phase G-5 Piercing Parent Projectile");
                piercing.Launch(
                    Vector2.right * 8f,
                    playerHealth,
                    new DamageContext(playerHealth.gameObject, DamageSourceType.PlayerProjectile, 10f),
                    stats.PierceCount,
                    stats.ProjectileSplitSettings);
                piercing.transform.position = pierced.transform.position;
                InvokePrivate(piercing, "OnTriggerEnter2D", piercedObject.GetComponent<Collider2D>());
                Projectile[] piercingSplits = UnityEngine.Object
                    .FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                    .Where(shot => shot.IsSplitProjectile).ToArray();
                Assert(piercing != null && !piercing.IsSpent && Approximately(pierced.CurrentHealth, 90f) &&
                       piercingSplits.Length == 4,
                    "With a pierce the parent projectile must split and fly on.");
                InvokePrivate(piercingSplits[0], "OnTriggerEnter2D", piercedObject.GetComponent<Collider2D>());
                Assert(Approximately(pierced.CurrentHealth, 85f) && piercingSplits[0] != null,
                    "A split projectile that inherited a pierce must hit the enemy it started inside and fly on.");

                piercing.transform.position = second.transform.position;
                InvokePrivate(piercing, "OnTriggerEnter2D", secondObject.GetComponent<Collider2D>());
                Assert(piercing == null && Approximately(second.CurrentHealth, 90f) &&
                       UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                           .Count(shot => shot.IsSplitProjectile) == 8,
                    "A piercing parent must split again on its next enemy and vanish once its pierce is used.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(piercedObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
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
