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
            Week23Passive3Setup.SearchRadius, Week23Passive3Setup.OtherEnemyDamageRatio,
            Week23Passive3Setup.RepeatDamageRatio, Week23Passive3Setup.SameTargetDelaySeconds);

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
            Debug.Log("Passive-3 verification passed: effect type 62 is appended, 칸타의 팽이 is a Rare artifact " +
                      "without a stack limit in the selection reward pool with 2 bounces and one more per stack, " +
                      "every stack costs 5% basic attack damage, a shot that hit an enemy turns to the nearest " +
                      "other enemy within 3.5 and two enemies are hit in turn, each bounce to another enemy keeps " +
                      "55% and each bounce back to a lone enemy keeps 35% (compounding), a hit after a bounce pushes " +
                      "with 25% knockback, a shot on a lone enemy hops off it, slows for 0.15 seconds and flies back " +
                      "to hit it once per bounce, every bounce hit is a basic attack hit, a kill with no other enemy " +
                      "ends the shot, and bounces are used before pierces.");
        }

        private static void ValidateContractAssetAndPool()
        {
            Assert((int)ItemEffectType.GainGoldOnAcquire == 61 && (int)ItemEffectType.BounceBetweenEnemies == 62,
                "Passive-3 must append effect type 62 without renumbering earlier effects.");
            Assert(BounceEntry(2, 3.5f, 0.5f, 0.35f, 0.15f, 0.15f).TryValidate(out _) &&
                   BounceEntry(2, 3.5f, 0.5f, 0.35f, 0.15f, 0f).TryValidate(out _) &&
                   !BounceEntry(0, 3.5f, 0.5f, 0.35f, 0.15f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(2, 0f, 0.5f, 0.35f, 0.15f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(2, 3.5f, 0f, 0.35f, 0.15f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(2, 3.5f, 1.5f, 0.35f, 0.15f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(2, 3.5f, 0.5f, 0f, 0.15f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(2, 3.5f, 0.5f, 1.5f, 0.15f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(2, 3.5f, 0.5f, 0.35f, 0f, 0.15f).TryValidate(out _) &&
                   !BounceEntry(2, 3.5f, 0.5f, 0.35f, 0.15f, -0.1f).TryValidate(out _) &&
                   !BounceEntry(2, 3.5f, 0.5f, 0.35f, 0.15f, 1f).TryValidate(out _),
                "The bounce effect needs a bounce count, a search radius, other-enemy and same-enemy ratios within " +
                "(0, 1], a delay and a basic attack penalty within [0, 1).");

            ItemDefinition top = LoadItem(Week23Passive3Setup.TopId);
            ItemEffectEntry effect = top.Effects.Count == 1 ? top.Effects[0] : null;
            Assert(top.DisplayName == Week23Passive3Setup.TopName && top.Kind == ItemKind.Artifact &&
                   top.Rarity == ItemRarity.Rare && top.IsActive && top.MaxStacks == 0 && effect != null &&
                   effect.EffectType == ItemEffectType.BounceBetweenEnemies && effect.IntegerAmount == 2 &&
                   Mathf.Approximately(effect.Radius, 3.5f) && Mathf.Approximately(effect.ScaleMultiplier, 0.55f) &&
                   Mathf.Approximately(effect.SecondaryMagnitude, 0.35f) &&
                   Mathf.Approximately(effect.IntervalSeconds, 0.15f) && Mathf.Approximately(effect.Magnitude, 0.05f),
                "칸타의 팽이 must be a Rare artifact without a stack limit with 2 bounces, radius 3.5, 55% kept per " +
                "bounce to another enemy, 35% kept per repeat, a 0.15s delay and a 5% basic attack penalty per stack.");
            Assert(Mathf.Approximately(ProjectileBounceSettings.BouncedKnockbackScale, 0.25f),
                "A hit after a bounce must push with 25% of the normal knockback.");
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
                Assert(!stats.ProjectileBounce.IsEnabled && Near(stats.BasicAttackDamagePenalty, 0f),
                    "Without 칸타의 팽이 shots must not bounce nor lose basic attack damage.");
                Assert(inventory.TryAcquire(top) && stats.ProjectileBounce.BounceCount == 2 &&
                       Near(stats.ProjectileBounce.SearchRadius, 3.5f) &&
                       Near(stats.ProjectileBounce.OtherEnemyDamageRatio, 0.55f) &&
                       Near(stats.ProjectileBounce.RepeatDamageRatio, 0.35f) &&
                       Near(stats.ProjectileBounce.SameTargetDelaySeconds, 0.15f) &&
                       Near(stats.BasicAttackDamagePenalty, 0.05f),
                    "One 칸타의 팽이 must give 2 bounces and cost 5% basic attack damage.");
                Assert(inventory.TryAcquire(top) && stats.ProjectileBounce.BounceCount == 3 &&
                       Near(stats.BasicAttackDamagePenalty, 0.1f) &&
                       inventory.TryAcquire(top) && stats.ProjectileBounce.BounceCount == 4 &&
                       Near(stats.BasicAttackDamagePenalty, 0.15f),
                    "Every later 칸타의 팽이 must add one bounce and another 5% penalty without a stack limit " +
                    $"(bounces {stats.ProjectileBounce.BounceCount}, penalty {stats.BasicAttackDamagePenalty}).");
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
                float wholePush = first.GetComponent<KnockbackReceiver>().PushVelocity.magnitude;
                Assert(Near(first.CurrentHealth, 990f) && wholePush > 0.01f && shot != null &&
                       shot.BounceTarget == second && shot.RemainingBounces == 1 &&
                       Near(shot.BounceDamageMultiplier, 0.55f) &&
                       Vector2.Dot(shot.Velocity.normalized, Vector2.right) > 0.7f && Near(shot.Velocity.magnitude, 8f),
                    "After its whole first hit the shot must turn to the nearest other enemy within 3.5 at the same " +
                    "speed, keeping 55% of its damage.");

                shot.transform.position = second.transform.position;
                Assert(!shot.TickBounce(0.02f), "The bounce hit must end the bounce flight.");
                float bouncedPush = second.GetComponent<KnockbackReceiver>().PushVelocity.magnitude;
                Assert(Near(second.CurrentHealth, 994.5f) && shot.BounceTarget == first && shot.RemainingBounces == 0 &&
                       Near(shot.BounceDamageMultiplier, 0.3025f) &&
                       Near(bouncedPush, wholePush * ProjectileBounceSettings.BouncedKnockbackScale),
                    "The bounce must hit the second enemy for 55% with a quarter of the push and turn back to the " +
                    $"first (second {second.CurrentHealth}, push {bouncedPush} of {wholePush}).");

                shot.transform.position = first.transform.position;
                shot.TickBounce(0.02f);
                Assert(shot == null && Near(first.CurrentHealth, 986.975f) && Near(second.CurrentHealth, 994.5f) &&
                       Near(far.CurrentHealth, 1000f) && basicHits == 3,
                    "With two enemies the shot hits them in turn for 100%, 55% and 30.25%, three basic attack hits in " +
                    $"all (first {first.CurrentHealth}, second {second.CurrentHealth}, hits {basicHits}).");
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
                float hopSpeed = 8f * Projectile.RepeatHopSpeedRatio;
                Assert(Near(lone.CurrentHealth, 990f) && shot.RepeatTarget == lone && shot.RemainingBounces == 1 &&
                       Near(shot.Velocity.magnitude, hopSpeed) &&
                       Vector2.Dot(shot.Velocity.normalized, Vector2.down) > 0.8f && shot.RemainingLifetime >= 0.15f,
                    "With no other enemy near, the shot must hop back off the enemy it hit instead of stopping " +
                    $"(velocity {shot.Velocity}).");
                Assert(shot.TickBounce(0.1f) && Near(lone.CurrentHealth, 990f) && shot.Velocity.magnitude > 0.01f &&
                       shot.Velocity.magnitude < hopSpeed - 0.01f,
                    "The hop must slow down and the same enemy must not be hit again before 0.15 seconds.");
                Assert(!shot.TickBounce(0.06f) && shot.RepeatTarget == null && shot.BounceTarget == lone &&
                       Near(lone.CurrentHealth, 990f) && Vector2.Dot(shot.Velocity.normalized, Vector2.up) > 0.99f &&
                       Near(shot.Velocity.magnitude, 8f),
                    "At the top of the hop the shot must fly back to the enemy at full speed without hitting it yet.");
                shot.transform.position = lone.transform.position;
                shot.TickBounce(0.02f);
                Assert(Near(lone.CurrentHealth, 986.5f) && shot != null && shot.RepeatTarget == lone &&
                       shot.RemainingBounces == 0,
                    $"Reaching the enemy must hit it for 35%, use a bounce and hop again (health {lone.CurrentHealth}).");
                // Batch verification runs no physics, so move the shot along its hop by hand.
                shot.transform.position = (Vector2)lone.transform.position + shot.Velocity.normalized;
                Physics2D.SyncTransforms();
                shot.TickBounce(0.2f);
                Assert(shot != null && shot.BounceTarget == lone && Near(lone.CurrentHealth, 986.5f),
                    "The second hop must fly back too.");
                shot.transform.position = lone.transform.position;
                shot.TickBounce(0.02f);
                Assert(shot == null && Near(lone.CurrentHealth, 985.275f) && Near(distant.CurrentHealth, 1000f),
                    "Two bounces on a lone enemy must hit it three times: 100%, 35% and 12.25% " +
                    $"(health {lone.CurrentHealth}).");

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
                shot.transform.position = lone.transform.position;
                shot.TickBounce(0.02f);
                Assert(shot != null && !shot.IsSpent && shot.RepeatTarget == null && Near(lone.CurrentHealth, 986.5f) &&
                       Near(shot.Velocity.magnitude, 8f),
                    "With its bounces used up the shot must pierce and fly on.");
                Hit(shot, lone);
                Assert(Near(lone.CurrentHealth, 986.5f), "A piercing shot must not hit the same enemy again.");

                Projectile plain = Launch(root.transform, Origin + Vector2.down, Vector2.up * 8f, owner, damage);
                Hit(plain, lone);
                Assert(plain == null && Near(lone.CurrentHealth, 976.5f), "A shot without bounces must vanish on its hit.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ItemEffectEntry BounceEntry(int bounces, float radius, float otherRatio, float repeatRatio,
            float delay, float penalty) =>
            new(ItemEffectType.BounceBetweenEnemies, configuredMagnitude: penalty,
                configuredSecondaryMagnitude: repeatRatio, configuredIntegerAmount: bounces, configuredRadius: radius,
                configuredIntervalSeconds: delay, configuredScaleMultiplier: otherRatio);
    }
}
