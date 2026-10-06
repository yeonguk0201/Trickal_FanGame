using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week23Passive2Verification
    {
        private static readonly Vector2 Origin = new(400f, 480f);

        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Passive-2 Giant Potion")]
        public static void SetupAndVerifyBatch()
        {
            Week23Passive2Setup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(Week23Passive2Setup.PotionPath);
            Week23Passive2Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) &&
                   guid == AssetDatabase.AssetPathToGUID(Week23Passive2Setup.PotionPath),
                "Passive-2 setup changed or lost the item GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Passive-2 Giant Potion")]
        public static void Verify()
        {
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
            ValidateContract();
            ValidateAssetAndPool();
            ValidateStacks();
            Debug.Log("Passive-2 verification passed: effect types 45 and 46 are appended, 거대화 물약 is an Epic " +
                      "two-stack " +
                      "artifact in the selection reward pool, and each stack adds +30% body size (the hurtbox " +
                      "follows it, feet unchanged), +20% basic attack damage only, +3 hearts and -20% movement speed.");
        }

        // Also re-runs the size, artifact contract and reward pool checks the potion builds on.
        [MenuItem("Trickal Fan Game/Week 23/Verify Passive-2 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            foreach (Action check in new Action[]
                     {
                         Week23Hitbox1Verification.Verify, PhaseGArtifactContractVerification.Verify,
                         Week16Artifact1Verification.Verify,
                     })
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                check();
            }

            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
            Week23Passive1Verification.Verify();
            Week16Reward3Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Passive-2 regression verification passed.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.PlayerSizePercent == 45 &&
                   (int)ItemEffectType.BasicAttackDamagePercent == 46 &&
                   (int)ItemEffectType.BasicAttackPoison == 44,
                "Passive-2 must append effect types 45 and 46 without renumbering earlier effects.");
        }

        private static void ValidateAssetAndPool()
        {
            ItemDefinition potion = LoadPotion();
            Assert(potion.DisplayName == Week23Passive2Setup.PotionName && potion.Kind == ItemKind.Artifact &&
                   potion.Rarity == ItemRarity.Epic && potion.IsActive && potion.MaxStacks == 2 &&
                   potion.Effects.Count == 4 &&
                   potion.Effects[0].EffectType == ItemEffectType.PlayerSizePercent &&
                   Mathf.Approximately(potion.Effects[0].Magnitude, 0.3f) &&
                   potion.Effects[1].EffectType == ItemEffectType.BasicAttackDamagePercent &&
                   Mathf.Approximately(potion.Effects[1].Magnitude, 0.2f) &&
                   potion.Effects[2].EffectType == ItemEffectType.MaxHealthFlat &&
                   Mathf.Approximately(potion.Effects[2].Magnitude, 6f) &&
                   potion.Effects[3].EffectType == ItemEffectType.MoveSpeedPenaltyPercent &&
                   Mathf.Approximately(potion.Effects[3].Magnitude, 0.2f),
                "거대화 물약 must be an Epic artifact with two stacks of +30% size, +20% basic attack damage, " +
                "+3 hearts and -20% movement.");
            string description = ArtifactEffectDescription.Build(potion);
            Assert(description == "몸 크기 +30% · 기본 공격 피해 +20% · 최대 HP +3칸 · 이동속도 -20%",
                $"거대화 물약 must describe its four effects, but reads '{description}'.");

            string[] sizeUsers = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(definition => definition != null && definition.Effects.Any(effect =>
                    effect.EffectType is ItemEffectType.PlayerSizePercent
                        or ItemEffectType.BasicAttackDamagePercent))
                .Select(definition => definition.ItemId).ToArray();
            Assert(sizeUsers.SequenceEqual(new[] { Week23Passive2Setup.PotionId }),
                "Only 거대화 물약 may use the body size and basic attack damage effects.");

            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>(FindObjectsInactive.Include);
            Assert(assembler != null && assembler.SelectionRewardPool.Count(item => item == potion) == 1,
                "The selection reward pool must offer 거대화 물약 once.");
            Assert(assembler.SelectionRewardPool.All(item => item != null && item.Kind == ItemKind.Artifact &&
                                                             !GoldenChestExclusivePool.IsExclusive(item)),
                "The selection reward pool must hold only non-exclusive artifacts.");
        }

        private static void ValidateStacks()
        {
            ItemDefinition potion = LoadPotion();
            GameObject player = new("Passive-2 Player", typeof(PlayerMovement), typeof(CircleCollider2D),
                typeof(PlayerSP), typeof(PlayerInventory));
            try
            {
                player.transform.position = Origin;
                CircleCollider2D body = player.GetComponent<CircleCollider2D>();
                body.radius = PlayerFeet.BodyRadius;
                PlayerFeet.Ensure(player, out PlayerFeet feet);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                Invoke(player.GetComponent<Health>(), "Awake");
                Invoke(player.GetComponent<PlayerSP>(), "Awake");
                Invoke(stats, "Awake");
                Invoke(inventory, "Awake");
                Physics2D.SyncTransforms();
                Vector2 feetStart = feet.WorldCenter;
                float baseAttack = stats.AttackDamage;
                float baseSpeed = stats.MoveSpeed;
                float baseHealth = stats.MaxHealth;
                Health health = player.GetComponent<Health>();

                Assert(inventory.TryAcquire(potion), "The first 거대화 물약 must be accepted.");
                AssertState(player, body, feet, stats, feetStart, 1.3f, 0.65f, baseAttack, 1.2f, baseSpeed * 0.8f);
                Assert(Mathf.Approximately(stats.MaxHealth, baseHealth + 6f) &&
                       Mathf.Approximately(health.MaxHealth, baseHealth + 6f) &&
                       Mathf.Approximately(health.CurrentHealth, baseHealth + 6f),
                    "The first 거대화 물약 must add 3 hearts of maximum health and fill them.");

                Assert(inventory.TryAcquire(potion), "The second 거대화 물약 must be accepted.");
                AssertState(player, body, feet, stats, feetStart, 1.6f, 0.8f, baseAttack, 1.4f, baseSpeed * 0.6f);
                Assert(Mathf.Approximately(stats.MaxHealth, baseHealth + 12f),
                    "Two 거대화 물약 must add 6 hearts of maximum health.");

                Assert(!inventory.TryAcquire(potion) && inventory.GetStackCount(Week23Passive2Setup.PotionId) == 2 &&
                       inventory.AcquiredItems.Count == 2,
                    "A third 거대화 물약 must be rejected without an acquisition record.");
                AssertState(player, body, feet, stats, feetStart, 1.6f, 0.8f, baseAttack, 1.4f, baseSpeed * 0.6f);
                Assert(Mathf.Approximately(stats.MaxHealth, baseHealth + 12f),
                    "Two 거대화 물약 must add 6 hearts of maximum health.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static void AssertState(GameObject player, CircleCollider2D body, PlayerFeet feet, PlayerStats stats,
            Vector2 feetStart, float size, float hurtboxRadius, float attackDamage, float basicAttackMultiplier,
            float moveSpeed)
        {
            Physics2D.SyncTransforms();
            PlayerBodySize bodySize = player.GetComponent<PlayerBodySize>();
            Assert(bodySize != null && Mathf.Abs(bodySize.SizeMultiplier - size) < 0.001f &&
                   Mathf.Abs(stats.PlayerSizeMultiplier - size) < 0.001f &&
                   Mathf.Abs(player.transform.localScale.x - size) < 0.001f,
                $"The body must be x{size}, but is x{(bodySize != null ? bodySize.SizeMultiplier : 0f)}.");
            Assert(Mathf.Abs(body.bounds.extents.x - hurtboxRadius) < 0.001f,
                $"At x{size} the hurtbox radius must be {hurtboxRadius}, but is {body.bounds.extents.x:F2}.");
            Assert(Mathf.Abs(feet.Collider.bounds.extents.x - PlayerFeet.Radius) < 0.001f &&
                   Vector2.Distance(feet.WorldCenter, feetStart) < 0.001f,
                $"At x{size} the feet must keep their radius and place.");
            Assert(Mathf.Abs(stats.AttackDamage - attackDamage) < 0.001f &&
                   Mathf.Abs(stats.SkillDamageMultiplier - 1f) < 0.001f &&
                   Mathf.Abs(stats.BasicAttackRoomDamageMultiplier - basicAttackMultiplier) < 0.001f,
                $"At x{size} only basic attacks must deal x{basicAttackMultiplier}: the attack stat is " +
                $"{stats.AttackDamage}, the skill multiplier {stats.SkillDamageMultiplier} and the basic attack " +
                $"multiplier {stats.BasicAttackRoomDamageMultiplier}.");
            Assert(Mathf.Abs(stats.MoveSpeed - moveSpeed) < 0.001f,
                $"At x{size} movement speed must be {moveSpeed}, but is {stats.MoveSpeed}.");
        }

        private static ItemDefinition LoadPotion()
        {
            ItemDefinition potion = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week23Passive2Setup.PotionPath);
            Assert(potion != null && potion.ItemId == Week23Passive2Setup.PotionId && potion.IsValid,
                "Run Passive-2 setup first: 거대화 물약 is missing.");
            return potion;
        }

        private static void Invoke(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?
                .Invoke(target, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
