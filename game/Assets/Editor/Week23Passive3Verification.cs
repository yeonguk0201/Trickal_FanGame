using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;
using static TrickalFanGame.Editor.ArtifactVerificationFixtures;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Passive-3: 칸타의 팽이. A basic attack shot that hit an enemy bounces to the nearest other enemy, or hits the
    // same enemy again for less when it is alone (Passive-0 §4.4).
    public static class Week23Passive3Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-7400f, 7400f);

        private static readonly ProjectileBounceSettings TopBounce = new(Week23Passive3Setup.BounceCount,
            Week23Passive3Setup.SearchRadius, Week23Passive3Setup.RepeatDamageRatio,
            Week23Passive3Setup.SameTargetDelaySeconds);

        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Passive-3 Kanta Top")]
        public static void SetupAndVerifyBatch()
        {
            Week23Passive3Setup.Setup();
            string path = ArtifactSetupUtility.ItemPath(Week23Passive3Setup.TopId);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Week23Passive3Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) && guid == AssetDatabase.AssetPathToGUID(path),
                "Passive-3 setup changed or lost the item GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Passive-3 Kanta Top")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            ValidateContractAssetAndPool();
            ValidateStacks();
            ValidateBounceBetweenEnemies();
            ValidateSameEnemyRepeat();
            ValidateCourseOrder();
            Debug.Log("Passive-3 verification passed: effect type 62 is appended, 칸타의 팽이 is a Rare two-stack " +
                      "artifact in the selection reward pool with 2 bounces (3 at two stacks), a shot that hit an " +
                      "enemy turns to the nearest other enemy within 3.5 and two enemies are hit in turn, an enemy " +
                      "hit again by the same shot takes half each time, a lone enemy is hit again after 0.15 " +
                      "seconds once per bounce, every bounce hit is a basic attack hit, a kill with no other " +
                      "enemy ends the shot, and bounces are used before pierces.");
        }

        private static void ValidateContractAssetAndPool()
        {
            Assert((int)ItemEffectType.GainGoldOnAcquire == 61 && (int)ItemEffectType.BounceBetweenEnemies == 62,
                "Passive-3 must append effect type 62 without renumbering earlier effects.");
            Assert(BounceEntry(2, 3.5f, 0.5f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(0, 3.5f, 0.5f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(2, 0f, 0.5f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(2, 3.5f, 0f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(2, 3.5f, 1.5f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(2, 3.5f, 0.5f, 0f).TryValidate(out _),
                "The bounce effect needs a bounce count, a search radius, a repeat ratio within (0, 1] and a delay.");

            ItemDefinition top = LoadItem(Week23Passive3Setup.TopId);
            ItemEffectEntry effect = top.Effects.Count == 1 ? top.Effects[0] : null;
            Assert(top.DisplayName == Week23Passive3Setup.TopName && top.Kind == ItemKind.Artifact &&
                   top.Rarity == ItemRarity.Rare && top.IsActive && top.MaxStacks == 2 && effect != null &&
                   effect.EffectType == ItemEffectType.BounceBetweenEnemies && effect.IntegerAmount == 2 &&
                   Mathf.Approximately(effect.Radius, 3.5f) && Mathf.Approximately(effect.SecondaryMagnitude, 0.5f) &&
                   Mathf.Approximately(effect.IntervalSeconds, 0.15f),
                "칸타의 팽이 must be a Rare two-stack artifact with 2 bounces, radius 3.5, 50% repeats and a 0.15s delay.");
            string description = ArtifactEffectDescription.Build(top);
            Assert(description == Week23Passive3Setup.TopDescription,
                $"칸타의 팽이 must read '{Week23Passive3Setup.TopDescription}', but reads '{description}'.");
            Assert(LoadAssembler().SelectionRewardPool.Count(item => item == top) == 1,
                "The selection reward pool must offer 칸타의 팽이 once.");
        }

        private static void ValidateStacks()
        {
            GameObject root = new("Passive-3 Stack Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                ItemDefinition top = LoadItem(Week23Passive3Setup.TopId);
                Assert(!stats.ProjectileBounce.IsEnabled, "Without 칸타의 팽이 shots must not bounce.");
                Assert(inventory.TryAcquire(top) && stats.ProjectileBounce.BounceCount == 2 &&
                       Near(stats.ProjectileBounce.SearchRadius, 3.5f) &&
                       Near(stats.ProjectileBounce.RepeatDamageRatio, 0.5f) &&
                       Near(stats.ProjectileBounce.SameTargetDelaySeconds, 0.15f),
                    "One 칸타의 팽이 must give 2 bounces.");
                Assert(inventory.TryAcquire(top) && stats.ProjectileBounce.BounceCount == 3 &&
                       !inventory.TryAcquire(top) && stats.ProjectileBounce.BounceCount == 3,
                    "A second 칸타의 팽이 must add one bounce and a third must be rejected.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateBounceBetweenEnemies()
        {
            GameObject root = new("Passive-3 Bounce Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                Health owner = player.GetComponent<Health>();
                int basicHits = 0;
                player.GetComponent<PlayerCombatEvents>().BasicAttackHit += _ => basicHits++;
                Health first = CreateEnemy(root.transform, Origin, 1000f);
                Health second = CreateEnemy(root.transform, Origin + Vector2.right * 2f, 1000f);
                Health far = CreateEnemy(root.transform, Origin + Vector2.left * 5f, 1000f);
                Physics2D.SyncTransforms();

                DamageContext damage = new(player, DamageSourceType.PlayerProjectile, 10f);
                Projectile shot = Launch(root.transform, Origin + Vector2.down, Vector2.up * 8f, owner, damage,
                    new ProjectileHitEffects(true), bounce: TopBounce);
                Hit(shot, first);
                Assert(Near(first.CurrentHealth, 990f) && shot != null && shot.BounceTarget == second &&
                       shot.RemainingBounces == 1 && Vector2.Dot(shot.Velocity.normalized, Vector2.right) > 0.7f &&
                       Near(shot.Velocity.magnitude, 8f),
                    "After its hit the shot must turn to the nearest other enemy within 3.5 at the same speed.");

                shot.transform.position = second.transform.position;
                Assert(!shot.TickBounce(0.02f) && Near(second.CurrentHealth, 990f) && shot.BounceTarget == first &&
                       shot.RemainingBounces == 0,
                    "The bounce must hit the second enemy whole and turn back to the first.");

                shot.transform.position = first.transform.position;
                shot.TickBounce(0.02f);
                Assert(shot == null && Near(first.CurrentHealth, 985f) && Near(second.CurrentHealth, 990f) &&
                       Near(far.CurrentHealth, 1000f) && basicHits == 3,
                    "With two enemies the shot hits them in turn: the first again for half, three basic attack " +
                    $"hits in all (first {first.CurrentHealth}, second {second.CurrentHealth}, hits {basicHits}).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateSameEnemyRepeat()
        {
            GameObject root = new("Passive-3 Repeat Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                Health owner = player.GetComponent<Health>();
                DamageContext damage = new(player, DamageSourceType.PlayerProjectile, 10f);
                Health lone = CreateEnemy(root.transform, Origin, 1000f);
                // Outside the 3.5 search radius, so the first enemy counts as alone.
                Health distant = CreateEnemy(root.transform, Origin + Vector2.right * 4.5f, 1000f);
                Physics2D.SyncTransforms();

                Projectile shot = Launch(root.transform, Origin + Vector2.down, Vector2.up * 8f, owner, damage,
                    bounce: TopBounce);
                Hit(shot, lone);
                Assert(Near(lone.CurrentHealth, 990f) && shot.RepeatTarget == lone && shot.RemainingBounces == 1 &&
                       shot.Velocity.sqrMagnitude < 0.0001f && shot.RemainingLifetime >= 0.15f,
                    "With no other enemy near, the shot must wait on the enemy it hit.");
                Assert(shot.TickBounce(0.1f) && Near(lone.CurrentHealth, 990f),
                    "The same enemy must not be hit again before 0.15 seconds.");
                shot.TickBounce(0.06f);
                Assert(Near(lone.CurrentHealth, 985f) && shot != null && shot.RepeatTarget == lone &&
                       shot.RemainingBounces == 0,
                    "The first repeat must deal half and use a bounce.");
                shot.TickBounce(0.2f);
                Assert(shot == null && Near(lone.CurrentHealth, 982.5f) && Near(distant.CurrentHealth, 1000f),
                    "Two bounces on a lone enemy must hit it three times: 100%, 50% and 25%.");

                Health weak = CreateEnemy(root.transform, Origin + Vector2.up * 20f, 5f);
                Physics2D.SyncTransforms();
                Projectile killer = Launch(root.transform, weak.transform.position, Vector2.up * 8f, owner, damage,
                    bounce: TopBounce);
                Hit(killer, weak);
                Assert(weak.IsDead && killer == null, "A kill with no other enemy near must end the shot.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateCourseOrder()
        {
            GameObject root = new("Passive-3 Course Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin + Vector2.down * 20f);
                Health owner = player.GetComponent<Health>();
                DamageContext damage = new(player, DamageSourceType.PlayerProjectile, 10f);
                Health lone = CreateEnemy(root.transform, Origin, 1000f);
                Physics2D.SyncTransforms();

                // One bounce and one pierce: the bounce is used first, then the shot flies on through the enemy.
                Projectile shot = Launch(root.transform, Origin + Vector2.down, Vector2.up * 8f, owner, damage,
                    pierces: 1, bounce: TopBounce.WithBounceCount(1));
                Hit(shot, lone);
                Assert(shot.RepeatTarget == lone && shot.RemainingBounces == 0, "The bounce must be used before the pierce.");
                shot.TickBounce(0.2f);
                Assert(shot != null && !shot.IsSpent && shot.RepeatTarget == null && Near(lone.CurrentHealth, 985f) &&
                       Near(shot.Velocity.magnitude, 8f),
                    "With its bounces used up the shot must pierce and fly on.");
                Hit(shot, lone);
                Assert(Near(lone.CurrentHealth, 985f), "A piercing shot must not hit the same enemy again.");

                Projectile plain = Launch(root.transform, Origin + Vector2.down, Vector2.up * 8f, owner, damage);
                Hit(plain, lone);
                Assert(plain == null && Near(lone.CurrentHealth, 975f), "A shot without bounces must vanish on its hit.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ItemEffectEntry BounceEntry(int bounces, float radius, float ratio, float delay) =>
            new(ItemEffectType.BounceBetweenEnemies, configuredSecondaryMagnitude: ratio,
                configuredIntegerAmount: bounces, configuredRadius: radius, configuredIntervalSeconds: delay);
    }
}
