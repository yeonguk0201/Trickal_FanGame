using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week16Artifact1Verification
    {
        [MenuItem("Trickal Fan Game/Week 16/Verify Artifact-1 Effects")]
        public static void Verify()
        {
            ValidateKettlebellStacks();
            ValidateClearWeatherHitCounting();
            ValidateFreshRunState();
            Debug.Log("Week 16 Artifact-1 verification passed: kettlebell stacks skill damage and movement " +
                      "penalties twice, clear-weather counts only applied basic melee/projectile hits, triggers " +
                      "one non-critical 150% lightning hit every ten hits without recursive counting, credits " +
                      "lightning kills to the player, and a fresh Run starts without artifact runtime state.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week16Content0Setup.Setup();
            Week16Content0Verification.Verify();
            Verify();
            Hp4LifeGemVerification.Verify();
            PhaseGSimpleEffectsVerification.Verify();
            PhaseGConditionalEffectsVerification.Verify();
            Week16Spell1Verification.Verify();
        }

        private static void ValidateKettlebellStacks()
        {
            GameObject player = CreatePlayer(out _, out PlayerStats stats, out _, out PlayerInventory inventory);
            ItemDefinition kettlebell = Load("artifact-30kg-kettlebell");
            try
            {
                Assert(inventory.TryAcquire(kettlebell) && inventory.TryAcquire(kettlebell) &&
                       !inventory.TryAcquire(kettlebell),
                    "Kettlebell must accept two stacks and reject a third.");
                Assert(Approximately(stats.SkillDamageMultiplier, 1.5f) &&
                       Approximately(stats.MoveSpeed, 4f) && Approximately(stats.AttackDamage, 10f) &&
                       Approximately(stats.AttackSpeed, 1f),
                    "Two kettlebell stacks must grant +50% lower/upper skill damage and -20% movement only.");
                Assert(inventory.AcquiredItems.Count == 2 &&
                       inventory.GetStackCount("artifact-30kg-kettlebell") == 2,
                    "Rejected stacks must not create acquisition records.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static void ValidateClearWeatherHitCounting()
        {
            GameObject player = CreatePlayer(out _, out PlayerStats stats,
                out PlayerCombatEvents combatEvents, out PlayerInventory inventory);
            GameObject durableTargetObject = CreateTarget("Artifact-1 Durable Target", 300f, out Health durableTarget);
            GameObject lightningKillObject = CreateTarget("Artifact-1 Lightning Kill Target", 20f,
                out Health lightningKillTarget);
            ItemDefinition clearWeather = Load("artifact-clear-weather-card");
            int lightningDamageEvents = 0;
            int playerKillEvents = 0;
            DamageSourceType killingSource = DamageSourceType.Unknown;
            durableTarget.DamageApplied += (context, applied, _) =>
            {
                if (context.SourceType == DamageSourceType.PlayerItemLightning && applied > 0f)
                    lightningDamageEvents++;
            };
            lightningKillTarget.DamageApplied += (context, applied, _) =>
            {
                if (context.SourceType == DamageSourceType.PlayerItemLightning && applied > 0f)
                    lightningDamageEvents++;
            };
            combatEvents.EnemyKilled += killed =>
            {
                playerKillEvents++;
                killingSource = killed.KillingBlow.SourceType;
            };

            try
            {
                Assert(inventory.TryAcquire(clearWeather) && !inventory.TryAcquire(clearWeather),
                    "Clear-weather must be acquired once through the common inventory path.");
                PlayerBasicAttackLightning lightning = player.GetComponent<PlayerBasicAttackLightning>();
                Assert(lightning != null && lightning.IsConfigured && lightning.HitProgress == 0,
                    "Clear-weather acquisition must configure its runtime once.");

                durableTarget.TakeDamage(Context(player, DamageSourceType.PlayerSkillExplosion));
                durableTarget.TakeDamage(Context(player, DamageSourceType.PlayerDamageAura));
                Assert(lightning.HitProgress == 0 && lightning.TriggerCount == 0,
                    "Skill and aura damage must not count as basic attack hits.");

                for (int hit = 0; hit < 9; hit++)
                {
                    DamageSourceType source = hit % 2 == 0
                        ? DamageSourceType.PlayerProjectile
                        : DamageSourceType.PlayerAttack;
                    durableTarget.TakeDamage(Context(player, source));
                }
                Assert(lightning.HitProgress == 9 && lightning.TriggerCount == 0 &&
                       lightningDamageEvents == 0,
                    "Nine applied melee/projectile hits must be preserved without an early trigger.");

                float beforeTenthHit = durableTarget.CurrentHealth;
                durableTarget.TakeDamage(Context(player, DamageSourceType.PlayerProjectile));
                Assert(lightning.HitProgress == 0 && lightning.TriggerCount == 1 &&
                       lightningDamageEvents == 1 &&
                       Approximately(beforeTenthHit - durableTarget.CurrentHealth,
                           10f + stats.AttackDamage * 1.5f),
                    "The tenth actual hit must deal its own damage plus exactly one 150% lightning hit.");

                for (int hit = 0; hit < 9; hit++)
                    durableTarget.TakeDamage(Context(player, DamageSourceType.PlayerProjectile));
                Assert(lightning.HitProgress == 9 && lightning.TriggerCount == 1,
                    "Lightning damage itself must not recursively advance the next hit cycle.");

                lightningKillTarget.TakeDamage(Context(player, DamageSourceType.PlayerAttack));
                Assert(lightningKillTarget.IsDead && lightning.HitProgress == 0 &&
                       lightning.TriggerCount == 2 && lightningDamageEvents == 2 &&
                       playerKillEvents == 1 && killingSource == DamageSourceType.PlayerItemLightning,
                    "A tenth hit must allow lightning to finish the same target and report one player kill.");
            }
            finally
            {
                Object.DestroyImmediate(lightningKillObject);
                Object.DestroyImmediate(durableTargetObject);
                Object.DestroyImmediate(player);
            }
        }

        private static void ValidateFreshRunState()
        {
            GameObject player = CreatePlayer(out _, out PlayerStats stats, out _, out PlayerInventory inventory);
            try
            {
                Assert(player.GetComponent<PlayerBasicAttackLightning>() == null &&
                       inventory.AcquiredItems.Count == 0 &&
                       Approximately(stats.SkillDamageMultiplier, 1f) &&
                       Approximately(stats.MoveSpeed, 5f),
                    "A fresh Run player must not retain kettlebell stacks or clear-weather hit progress.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static GameObject CreatePlayer(out Health health, out PlayerStats stats,
            out PlayerCombatEvents combatEvents, out PlayerInventory inventory)
        {
            GameObject player = new("Artifact-1 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerCombatEvents), typeof(PlayerInventory));
            health = player.GetComponent<Health>();
            stats = player.GetComponent<PlayerStats>();
            combatEvents = player.GetComponent<PlayerCombatEvents>();
            inventory = player.GetComponent<PlayerInventory>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
            InvokeLifecycle(stats, "Awake");
            InvokeLifecycle(inventory, "Awake");
            return player;
        }

        private static GameObject CreateTarget(string name, float maxHealth, out Health health)
        {
            GameObject target = new(name, typeof(Health));
            health = target.GetComponent<Health>();
            SetField(health, "maxHealth", maxHealth);
            InvokeLifecycle(health, "Awake");
            return target;
        }

        private static DamageContext Context(GameObject player, DamageSourceType sourceType) =>
            new(player, sourceType, 10f, criticalChance: 0f);

        private static ItemDefinition Load(string itemId)
        {
            ItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Items/{itemId}.asset");
            Assert(definition != null && definition.IsValid, $"Missing valid Item asset '{itemId}'.");
            return definition;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException($"Field {fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(component, null);
        }

        private static bool Approximately(float actual, float expected) =>
            Mathf.Abs(actual - expected) <= 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
