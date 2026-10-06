using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class PhaseGDamageAuraVerification
    {
        private const int VerificationEnemyLayer = 30;

        [MenuItem("Trickal Fan Game/Verify Phase G-6 Damage Aura")]
        public static void Verify()
        {
            ItemDefinition mask = LoadDefinition("item-14");
            ItemDefinition pillow = LoadDefinition("item-08");
            GameObject runObject = new("Phase G-6 Run Session");
            RunSession runSession = runObject.AddComponent<RunSession>();
            GameObject player = CreatePlayer(
                out Health playerHealth,
                out PlayerCombatEvents combatEvents,
                out PlayerProjectileAttack attack,
                out PlayerInventory inventory);
            GameObject activeTargetObject = CreateTarget(
                "Phase G-6 Active Multi-Collider Target",
                100f,
                new Vector2(0.5f, 0f),
                true,
                out Health activeTarget);
            AddChildCollider(activeTargetObject, "Second Collider");
            GameObject inactiveRoom = new("Phase G-6 Inactive Room");
            GameObject inactiveTargetObject = CreateTarget(
                "Phase G-6 Inactive Target",
                100f,
                new Vector2(0.5f, 0.5f),
                true,
                out Health inactiveTarget);
            inactiveTargetObject.transform.SetParent(inactiveRoom.transform);
            inactiveRoom.SetActive(false);
            GameObject deadTargetObject = CreateTarget(
                "Phase G-6 Dead Target",
                1f,
                new Vector2(-0.5f, 0f),
                true,
                out Health deadTarget);
            deadTarget.TakeDamage(10f);
            GameObject killTargetObject = CreateTarget(
                "Phase G-6 Aura Kill Target",
                0.1f,
                new Vector2(0f, 0.75f),
                true,
                out Health killTarget);
            GameObject farTargetObject = CreateTarget(
                "Phase G-6 Far Target",
                100f,
                new Vector2(4f, 0f),
                true,
                out Health farTarget);

            try
            {
                VerifyAura(
                    mask,
                    pillow,
                    runSession,
                    playerHealth,
                    combatEvents,
                    inventory,
                    activeTargetObject,
                    activeTarget,
                    inactiveTarget,
                    deadTarget,
                    killTarget,
                    farTarget);

                Debug.Log(
                    "Phase G-6 damage aura verification passed: dedicated Mask-only periodic damage, three-stack " +
                    "attack-damage scaling, one-second schedule, one hit per enemy per tick, radius filtering, inactive-room " +
                    "and dead-enemy exclusion, player kill source with Pillow healing, and ended-Run shutdown are valid.");
            }
            finally
            {
                InvokeLifecycle(attack, "OnDisable");
                UnityEngine.Object.DestroyImmediate(farTargetObject);
                UnityEngine.Object.DestroyImmediate(killTargetObject);
                UnityEngine.Object.DestroyImmediate(deadTargetObject);
                UnityEngine.Object.DestroyImmediate(inactiveRoom);
                UnityEngine.Object.DestroyImmediate(activeTargetObject);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(runObject);
            }
        }

        private static void VerifyAura(
            ItemDefinition mask,
            ItemDefinition pillow,
            RunSession runSession,
            Health playerHealth,
            PlayerCombatEvents combatEvents,
            PlayerInventory inventory,
            GameObject activeTargetObject,
            Health activeTarget,
            Health inactiveTarget,
            Health deadTarget,
            Health killTarget,
            Health farTarget)
        {
            Assert(inventory.TryAcquire(pillow) && inventory.TryAcquire(pillow),
                "Two Pillow stacks (heal every kill) are required to verify that aura kills use the shared player-kill route.");
            Assert(inventory.TryAcquire(mask) && inventory.TryAcquire(mask) && inventory.TryAcquire(mask),
                "All three Mask stacks should be acquirable.");
            Assert(!inventory.TryAcquire(mask),
                "Mask must reject acquisitions beyond its three-stack cap.");

            PlayerDamageAura aura = playerHealth.GetComponent<PlayerDamageAura>();
            Assert(aura != null && aura.StackCount == 3 && Approximately(aura.AttackDamagePercent, 0.60f) &&
                   Approximately(aura.MaxHealthDamagePercent, 0f) &&
                   Approximately(aura.Radius, 2.5f) && Approximately(aura.IntervalSeconds, 1f) &&
                   aura.ShowDebugRadius,
                "Three Mask stacks must configure one dedicated aura at 60% attack damage, 2.5m, visible debug radius, and one-second ticks.");
            PlayerStats ownerStats = playerHealth.GetComponent<PlayerStats>();
            Assert(ownerStats != null && Approximately(ownerStats.AttackDamage, 10f),
                "Aura verification assumes the base attack damage of 10.");
            aura.BindRunSession(runSession);
            SetPrivateField(aura, "targetLayers", (LayerMask)(1 << VerificationEnemyLayer));

            int activeDamageApplications = 0;
            activeTarget.DamageApplied += (_, _, _) => activeDamageApplications++;
            int playerKillEvents = 0;
            DamageContext killingBlow = default;
            combatEvents.EnemyKilled += killEvent =>
            {
                playerKillEvents++;
                killingBlow = killEvent.KillingBlow;
            };

            playerHealth.TakeDamage(1f);
            Assert(playerHealth.UsesHealthUnits && Approximately(playerHealth.CurrentHealth, 8f),
                "Pillow verification setup must leave the player missing the one-heart minimum (2 units).");
            Physics2D.SyncTransforms();
            float firstTick = aura.NextTickTime;
            Assert(!aura.Tick(firstTick - 0.001f),
                "The aura must not tick before its one-second schedule boundary.");
            Assert(aura.Tick(firstTick),
                "The aura must tick exactly at its scheduled boundary.");

            Assert(Approximately(activeTarget.CurrentHealth, 94f) && activeDamageApplications == 1,
                "A target with multiple colliders must take one 6 damage hit (60% of attack damage 10) per tick at three stacks.");
            Assert(Approximately(inactiveTarget.CurrentHealth, 100f),
                "An enemy under an inactive room root must not receive aura damage.");
            Assert(deadTarget.IsDead && Approximately(deadTarget.CurrentHealth, 0f),
                "An enemy already dead before the tick must remain ignored.");
            Assert(killTarget.IsDead && playerKillEvents == 1 &&
                   killingBlow.Source == playerHealth.gameObject &&
                   killingBlow.SourceType == DamageSourceType.PlayerDamageAura &&
                   killingBlow.DeliveryType == DamageDeliveryType.Periodic,
                "An aura kill must preserve the player source and dedicated periodic damage context exactly once.");
            Assert(Approximately(playerHealth.CurrentHealth, 9f),
                "The shared kill event must let two Pillow stacks heal one half-heart unit on the aura kill.");
            Assert(Approximately(farTarget.CurrentHealth, 100f),
                "An enemy outside the 2.5m radius must not receive aura damage.");

            Assert(!aura.Tick(firstTick) && Approximately(activeTarget.CurrentHealth, 94f) &&
                   activeDamageApplications == 1 && playerKillEvents == 1,
                "Repeating Tick at the same time must not duplicate damage or kill events.");

            activeTargetObject.SetActive(false);
            Physics2D.SyncTransforms();
            Assert(aura.Tick(firstTick + 1f) && Approximately(activeTarget.CurrentHealth, 94f),
                "Deactivating the target room between ticks must immediately stop damage to its enemies.");

            activeTargetObject.SetActive(true);
            Physics2D.SyncTransforms();
            SetPrivateField(runSession, "hasEnded", true);
            Assert(!aura.Tick(firstTick + 2f) && Approximately(activeTarget.CurrentHealth, 94f),
                "An ended Run must stop future aura ticks even when enemies become active again.");
        }

        private static GameObject CreatePlayer(
            out Health health,
            out PlayerCombatEvents combatEvents,
            out PlayerProjectileAttack attack,
            out PlayerInventory inventory)
        {
            GameObject player = new("Phase G-6 Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            health = player.AddComponent<Health>();
            PlayerStats stats = player.AddComponent<PlayerStats>();
            combatEvents = player.AddComponent<PlayerCombatEvents>();
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerSP>();
            attack = player.AddComponent<PlayerProjectileAttack>();
            inventory = player.AddComponent<PlayerInventory>();

            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(stats, "Awake");
            InvokeLifecycle(movement, "Awake");
            InvokeLifecycle(attack, "Awake");
            InvokeLifecycle(inventory, "Awake");
            InvokeLifecycle(attack, "OnEnable");
            return player;
        }

        private static GameObject CreateTarget(
            string objectName,
            float maximumHealth,
            Vector2 position,
            bool addCollider,
            out Health health)
        {
            GameObject target = new(objectName);
            target.layer = VerificationEnemyLayer;
            target.transform.position = position;
            if (addCollider)
            {
                target.AddComponent<CircleCollider2D>();
            }
            health = target.AddComponent<Health>();
            SerializedObject serializedHealth = new(health);
            serializedHealth.FindProperty("maxHealth").floatValue = maximumHealth;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            InvokeLifecycle(health, "Awake");
            return target;
        }

        private static void AddChildCollider(GameObject parent, string objectName)
        {
            GameObject child = new(objectName);
            child.layer = VerificationEnemyLayer;
            child.transform.SetParent(parent.transform, false);
            child.AddComponent<CircleCollider2D>();
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

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(component.GetType().FullName, methodName);
            }
            method.Invoke(component, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }
            field.SetValue(target, value);
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
