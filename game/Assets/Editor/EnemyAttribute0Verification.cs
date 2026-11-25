using System;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Attribute-0: the five new enemies, their stats relative to the attribute guide, the rules of each attack, and
    // their place in the roster, difficulty table, floor pool and Encounters.
    public static class EnemyAttribute0Verification
    {
        [MenuItem("Trickal Fan Game/Beta/Verify Attribute-0 Enemies")]
        public static void Verify()
        {
            VerifyPrefabs();
            VerifyAttackRules();
            VerifyHealing();
            VerifySpawning();
            Debug.Log("Attribute-0 verification passed: five enemies, attack rules, healing limits and spawning.");
        }

        private static void VerifyPrefabs()
        {
            GameObject chaser = Load("TestEnemy");
            Assert(Mathf.Approximately(chaser.GetComponent<Health>().MaxHealth,
                EnemyAttribute0Setup.MadnessBulhyojasonHealth), "불효자손-광기 must use the lowered health.");
            Assert(Load("SansamoEnemy").GetComponent<Health>().MaxHealth == 40f,
                "Lowering 불효자손 must not change its variant 산사모-활발.");
            Assert(Load("TestBoss").GetComponent<Health>().MaxHealth == 250f,
                "Lowering 불효자손 must not change its variant 부스러기.");

            foreach ((string name, EncounterEnemyRole _) in EnemyAttribute0Setup.Enemies)
            {
                GameObject prefab = Load(name);
                Assert(prefab.GetComponent<EnemyChase>() == null && prefab.GetComponent<MeleeEnemyAttack>() == null,
                    $"{name} must not keep the chaser's AI.");
                Assert(prefab.GetComponent<TestEnemy>() != null, $"{name} must keep rewards and damage numbers.");
                Assert(prefab.GetComponent<EnemyDefeatAnimation>().Frames.Length == 4 &&
                       prefab.GetComponent<EnemyDefeatAnimation>().Frames.All(frame => frame != null &&
                           frame.name.StartsWith(name + "_Death", StringComparison.Ordinal)),
                    $"{name} must play its own four defeat frames.");
                Sprite idle = prefab.GetComponent<SpriteRenderer>().sprite;
                Assert(idle != null && idle.name == name + "_Idle", $"{name} must show its own idle drawing.");
                Assert(Mathf.Abs(idle.bounds.size.y - 1.192f) < 0.05f, $"{name} idle drawing has the wrong size.");
            }

            float pure = Load(EnemyAttribute0Setup.PurePrefab).GetComponent<Health>().MaxHealth;
            GameObject lasher = Load(EnemyAttribute0Setup.PurePrefab);
            Sprite tail = lasher.GetComponent<EnemyRibbonLashController>().TailSprite;
            Sprite lashBody = lasher.GetComponent<EnemyAttackArtwork>().GetFrame((int)EnemyAttackPhase.Active);
            Assert(lashBody != null && lashBody.name == "BulhyojasonPure_LashBody" &&
                   tail != null && tail.name == "BulhyojasonPure_LashTail" &&
                   Vector2.Distance(tail.pivot, tail.rect.size * 0.5f) < 1f,
                "The lash needs a separate body and a center-pivoted directional tail.");
            Assert(pure > EnemyAttribute0Setup.MadnessBulhyojasonHealth, "순수 must outlast 광기.");
            Assert(Load(EnemyAttribute0Setup.GloomyBulhyojasonPrefab).GetComponent<Health>().MaxHealth <
                   EnemyAttribute0Setup.LivelyHealth, "우울 must be frailer than 활발.");
            Assert(chaser.GetComponent<MeleeEnemyAttack>().DamageTier >
                   Load(EnemyAttribute0Setup.PurePrefab).GetComponent<EnemyRibbonLashController>().DamageTier,
                "광기 must hit harder than 순수.");
            Assert(Load(EnemyAttribute0Setup.LivelyPrefab).GetComponent<ContactDamage>().HalfHeartDamage,
                "활발 touches for half a heart; its landing is the real hit.");
            foreach (string name in new[] { EnemyAttribute0Setup.PurePrefab, EnemyAttribute0Setup.LivelyPrefab,
                         EnemyAttribute0Setup.GloomyBulhyojasonPrefab, EnemyAttribute0Setup.ColdSansamoPrefab })
                Assert(Load(name).GetComponent<EnemyAttackArtwork>().FrameCount == 4, $"{name} needs 4 attack frames.");
            Assert(Load(EnemyAttribute0Setup.ColdSansamoPrefab).GetComponent<EnemyMovementAnimator>().WalkFrameCount == 4 &&
                   Load(EnemyAttribute0Setup.GloomySansamoPrefab).GetComponent<EnemyMovementAnimator>().WalkFrameCount == 4,
                "Both new 산사모 need four walk frames.");
            EnemyBurrowHealerController healer =
                Load(EnemyAttribute0Setup.GloomySansamoPrefab).GetComponent<EnemyBurrowHealerController>();
            Assert(healer.BurrowFrameCount == 4 && healer.EmergeFrameCount == 4, "The healer needs its dig frames.");
        }

        private static void VerifyAttackRules()
        {
            const float reach = EnemyAttribute0Setup.RibbonReach, height = EnemyAttribute0Setup.RibbonHeight,
                width = EnemyAttribute0Setup.RibbonHalfWidth;
            foreach (Vector2 aim in new[] { Vector2.left, Vector2.up, new Vector2(1f, -1f).normalized })
            {
                Vector2 head = Vector2.up * height, across = new(-aim.y, aim.x);
                Assert(EnemyRibbonLashController.IsOnRibbon(head + aim * 1.7f, aim, reach, height, width),
                    "The ribbon must reach its tip in every direction.");
                Assert(!EnemyRibbonLashController.IsOnRibbon(head + aim * 1.9f, aim, reach, height, width),
                    "The ribbon must stop at its tip.");
                Assert(!EnemyRibbonLashController.IsOnRibbon(head - aim, aim, reach, height, width),
                    "The ribbon must not hit behind the enemy.");
                Assert(!EnemyRibbonLashController.IsOnRibbon(head + aim + across * (width + 0.05f), aim, reach, height,
                    width), "The ribbon must be dodgeable sideways.");
            }

            Vector2 middle = EnemyFanShooterController.FanDirection(Vector2.left, 1, 24f);
            Assert((middle - Vector2.left).sqrMagnitude < 0.0001f, "The fan's middle shot must aim at the player.");
            Assert(Mathf.Approximately(Vector2.Angle(EnemyFanShooterController.FanDirection(Vector2.left, 0, 24f),
                EnemyFanShooterController.FanDirection(Vector2.left, 2, 24f)), 48f), "The fan must span both sides.");

            Assert(EnemyBoomerangPath.PathOffset(0f, 6f, 2f).sqrMagnitude < 0.0001f &&
                   EnemyBoomerangPath.PathOffset(1f, 6f, 2f).sqrMagnitude < 0.0001f,
                "The boomerang must start and end on the thrower.");
            Assert(Mathf.Approximately(EnemyBoomerangPath.PathOffset(0.5f, 6f, 2f).x, 6f),
                "The boomerang must reach its range halfway.");
            float widest = Enumerable.Range(0, 201)
                .Max(step => Mathf.Abs(EnemyBoomerangPath.PathOffset(step / 200f, 6f, 2f).y));
            Assert(Mathf.Abs(widest - 2f) < 0.02f, "The boomerang's width must match its setting.");
            Assert(EnemyBoomerangPath.PathOffset(0.25f, 6f, 2f).y * EnemyBoomerangPath.PathOffset(0.75f, 6f, 2f).y < 0f,
                "The boomerang must return on the other side.");

            foreach (float distance in new[] { 2f, 4.1f, 5f, 5.9f, 7f })
            {
                float thrown = EnemyBoomerangController.ThrowRange(distance, 1.5f, 7.5f);
                Assert(Mathf.Approximately(EnemyBoomerangPath.PathOffset(0.5f, thrown,
                    thrown * EnemyAttribute0Setup.BoomerangWidthRatio).x, distance),
                    "The boomerang's far point must land on a player who stands still.");
            }

            EnemyBoomerangController thrower =
                Load(EnemyAttribute0Setup.ColdSansamoPrefab).GetComponent<EnemyBoomerangController>();
            Assert(thrower.WidthRatio <= 0.25f, "The teardrop must stay slim enough to clear room walls.");
            Assert(thrower.ProjectileSprite != null && thrower.ProjectileTint == Color.white,
                "산사모-냉정 must throw the pollen drawing untinted.");

            VerifyLeapAir();

            Vector2 straight = EnemyLeapController.ChooseDirection(Vector2.right, 0.5f);
            Assert((straight - Vector2.right).sqrMagnitude < 0.0001f, "A middle roll must leap at the player.");
            Assert(Mathf.Abs(Vector2.Angle(Vector2.right, EnemyLeapController.ChooseDirection(Vector2.right, 1f)) -
                             EnemyLeapController.MaximumTurnDegrees) < 0.01f, "The leap's veer must be limited.");
        }

        private static void VerifyLeapAir()
        {
            GameObject instance = Object.Instantiate(Load(EnemyAttribute0Setup.LivelyPrefab));
            try
            {
                EnemyLeapController leap = instance.GetComponent<EnemyLeapController>();
                Assert(instance.GetComponent<EnemyHopMotion>() != null, "활발 needs the view that lifts its picture.");
                Assert(leap.JumpHeight >= 1.4f && leap.AirDuration > 0.42f, "활발 must jump high and hang longer.");
                Assert(Mathf.Approximately(EnemyLeapController.ArcHeight(0.5f, leap.JumpHeight), leap.JumpHeight) &&
                       EnemyLeapController.ArcHeight(0f, 1f) == 0f && EnemyLeapController.ArcHeight(1f, 1f) == 0f,
                    "The jump must rise to its height and end on the floor.");
                Health health = instance.GetComponent<Health>();
                health.ResetHealth();
                Collider2D hitbox = instance.GetComponent<Collider2D>();
                EnemyFeet feet = instance.GetComponentInChildren<EnemyFeet>(true);
                leap.SetAirborne(true);
                Assert(health.IsInvulnerable && !hitbox.enabled && feet.Collider.enabled,
                    "In the air 활발 can neither be hit nor touch, but its feet still meet walls.");
                leap.SetAirborne(false);
                Assert(!health.IsInvulnerable && hitbox.enabled, "Landing must restore 활발's hitbox.");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static void VerifyHealing()
        {
            Assert(EnemyBurrowHealerController.MaximumLinks == 4, "The healer links at most four allies.");
            Shader aura = Resources.Load<Shader>("CombatEffects/" + EnemyHealAuraVisual.ShaderName);
            Assert(aura != null && !ShaderUtil.ShaderHasError(aura), "The healing aura shader must compile.");
            Assert(EnemyBurrowHealerController.CanHeal(Load("TestEnemy")), "The healer must heal normal enemies.");
            Assert(!EnemyBurrowHealerController.CanHeal(Load(EnemyAttribute0Setup.GloomySansamoPrefab)),
                "Healers must not heal each other.");
            Assert(!EnemyBurrowHealerController.CanHeal(Load("CrayonHeroBoss")), "Healers must not heal bosses.");
        }

        private static void VerifySpawning()
        {
            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(
                Week14Encounter2Setup.RosterPath);
            Assert(roster.TryValidate(out string error), error);
            RoomDifficultyTable table = Object.FindFirstObjectByType<FloorGenerator>()?.DifficultyTable;
            foreach ((string name, EncounterEnemyRole role) in EnemyAttribute0Setup.Enemies)
            {
                Assert(roster.TryResolve(role, out GameObject bound, out error) && bound == Load(name),
                    $"{role} must spawn {name}. {error}");
                Assert(RoomTemplateDefinition.RequiredPlacementRole(role) != SpawnPointPlacementRole.None,
                    $"{role} needs a spawn placement.");
                Assert(EncounterEnemyFloorPool.IntroductionFloor(role) >= 1, $"{role} needs an introduction floor.");
                if (table != null) Assert(table.TryGetThreat(role, out _), $"{role} needs a threat score.");
                Assert(AssetDatabase.FindAssets("t:EncounterDefinition", new[] { "Assets/Encounters/Definitions" })
                        .Select(guid => AssetDatabase.LoadAssetAtPath<EncounterDefinition>(
                            AssetDatabase.GUIDToAssetPath(guid)))
                        .Any(definition => definition.Waves.Any(wave => wave.SpawnRules.Any(rule =>
                            rule.Candidates.Any(candidate => candidate.EnemyRole == role)))),
                    $"{role} must appear in an Encounter.");
            }
        }

        private static GameObject Load(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{name}.prefab");
            Assert(prefab != null, $"Missing Prefab {name}.");
            return prefab;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Attribute-0 verification failed: " + message);
        }
    }
}
