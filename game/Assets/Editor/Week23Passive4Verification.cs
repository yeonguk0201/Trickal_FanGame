using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using static TrickalFanGame.Editor.ArtifactVerificationFixtures;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Passive-4: 샤샤의 항아리. The basic attack becomes a water stream that reaches the wall, hits every enemy on it
    // once per attack cooldown and passes over obstacles (Passive-0 §4.5), with the stream synergies of §9.
    public static class Week23Passive4Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-7800f, 7800f);
        private const float Start = 100f;
        private const float WallDistance = 10f;

        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Passive-4 Shasha Jar")]
        public static void SetupAndVerifyBatch()
        {
            Week23Passive4Setup.Setup();
            string path = ArtifactSetupUtility.ItemPath(Week23Passive4Setup.JarId);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Week23Passive4Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) && guid == AssetDatabase.AssetPathToGUID(path),
                "Passive-4 setup changed or lost the item GUID.");
            Verify();
        }

        // One Unity launch for the three pieces added together: 아이시아의 지갑, 칸타의 팽이 and 샤샤의 항아리.
        public static void SetupAndVerifyWalletTopAndJarBatch()
        {
            Week23Artifact3Verification.SetupAndVerifyBatch();
            Week23Passive3Verification.SetupAndVerifyBatch();
            SetupAndVerifyBatch();
        }

        // Re-runs the checks of what the three pieces changed underneath: the health and shield limit, the shot's
        // course after a hit and the golden chest exclusive pool.
        public static void VerifyWalletTopAndJarWithRegressionsBatch()
        {
            Week23Artifact3Verification.Verify();
            Week23Passive3Verification.Verify();
            Verify();
            Week23Artifact2Verification.VerifyWithRegressionsBatch();
            Week22Flight0Verification.Verify();
            Week22Chest2Verification.Verify();
            Week22Jjangsem1Verification.Verify();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            Hp1HealthUnitsVerification.VerifyRegressionBatch();
            Debug.Log("Artifact-3, Passive-3 and Passive-4 regression verification passed.");
        }

        // Passive-5: rebuilds item-11 (다야의 다이아몬드 커터) with the four-way split through the Phase G catalog
        // setup, then checks the cutter, the shots it shares code with and the stream that fires in bursts.
        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Passive-5 Cutter Four-Way Split")]
        public static void SetupAndVerifyCutterAndStreamBatch()
        {
            string cutterPath = ArtifactSetupUtility.ItemPath("item-11");
            string guid = AssetDatabase.AssetPathToGUID(cutterPath);
            PhaseGArtifactContractSetup.Setup();
            ArtifactSetupUtility.EnsureGlyphs("Passive-5", LoadItem("item-11"));
            AssetDatabase.SaveAssets();
            Assert(!string.IsNullOrWhiteSpace(guid) && guid == AssetDatabase.AssetPathToGUID(cutterPath),
                "Passive-5 setup changed or lost the item-11 GUID.");
            ItemDefinition cutter = LoadItem("item-11");
            Assert(cutter.Effects.Count == 1 && cutter.Effects[0].EffectType == ItemEffectType.SplitOnHit &&
                   (int)ItemEffectType.SplitOnHit == 64 && (int)ItemEffectType.SplitAfterPierce == 16 &&
                   ArtifactEffectDescription.Build(cutter) == "적을 맞히면 그 적의 상하좌우 4방향으로 분열 (분열탄 피해 50%)",
                "item-11 must hold only the four-way split on hit (effect 64) and describe it: " +
                ArtifactEffectDescription.Build(cutter));

            Week23Passive1Verification.VerifyWithRegressionsBatch();
            Week23Passive3Verification.Verify();
            Verify();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            Week16Content0Verification.Verify();
            // UserArtworkVerification renders a preview, so it needs a graphics device: run it from the Editor
            // menu or a batch without -nographics.
            Debug.Log("Passive-5 verification passed: item-11 splits four ways on every hit and the stream fires in bursts.");
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Passive-4 Shasha Jar")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            DamageCalculator.SetCriticalRollProviderForTesting(() => 1f);
            try
            {
                ValidateContractAssetAndPool();
                ValidateStreamHits();
                ValidateLengthAndObstacles();
                ValidateShotModifiers();
                ValidateSplitSynergy();
                ValidateBounceSynergy();
            }
            finally
            {
                DamageCalculator.ResetCriticalRollProvider();
                Physics2D.SyncTransforms();
            }

            Debug.Log("Passive-4 verification passed: effect type 63 is appended, 샤샤의 항아리 is an Epic one-stack " +
                      "artifact in the selection reward pool, the stream reaches the wall whatever the range and " +
                      "shot speed are, passes over obstacles while hitting them and stops at walls and doors, is " +
                      "0.2 wide, fires one burst per attack cooldown that hits every enemy on it as a basic attack hit with 30% " +
                      "knockback, 다중 투사체 adds parallel streams, 칸나의 대포 widens it, 다야의 다이아몬드 커터 " +
                      "sends four short streams from the enemy hit and 칸타의 팽이 jumps to nearby enemies.");
        }

        private static void ValidateContractAssetAndPool()
        {
            Assert((int)ItemEffectType.BounceBetweenEnemies == 62 && (int)ItemEffectType.WaterStreamAttack == 63,
                "Passive-4 must append effect type 63 without renumbering earlier effects.");
            Assert(new ItemEffectEntry(ItemEffectType.WaterStreamAttack, 0.2f, configuredSecondaryMagnitude: 0.3f)
                       .TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.WaterStreamAttack).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.WaterStreamAttack, 0.2f, configuredSecondaryMagnitude: 1.5f)
                       .TryValidate(out _),
                "The water stream effect needs a positive width and a knockback share within 0..1.");

            ItemDefinition jar = LoadItem(Week23Passive4Setup.JarId);
            Assert(jar.DisplayName == Week23Passive4Setup.JarName && jar.Kind == ItemKind.Artifact &&
                   jar.Rarity == ItemRarity.Epic && jar.IsActive && jar.MaxStacks == 1 && jar.Effects.Count == 1 &&
                   jar.Effects[0].EffectType == ItemEffectType.WaterStreamAttack &&
                   Mathf.Approximately(jar.Effects[0].Magnitude, 0.2f) &&
                   Mathf.Approximately(jar.Effects[0].SecondaryMagnitude, 0.3f),
                "샤샤의 항아리 must be an Epic one-stack artifact with a 0.2 wide stream and 30% knockback.");
            string description = ArtifactEffectDescription.Build(jar);
            Assert(description == Week23Passive4Setup.JarDescription,
                $"샤샤의 항아리 must read '{Week23Passive4Setup.JarDescription}', but reads '{description}'.");
            Assert(LoadAssembler().SelectionRewardPool.Count(item => item == jar) == 1,
                "The selection reward pool must offer 샤샤의 항아리 once.");
        }

        private static void ValidateStreamHits()
        {
            GameObject root = new("Passive-4 Hit Verification");
            try
            {
                GameObject player = CreateStreamPlayer(root.transform, Origin, out PlayerWaterStream stream);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                Assert(stats.HasWaterStream && stream.IsConfigured && Near(stream.BeamWidth, 0.2f) &&
                       stream.BeamCount == 1 && Near(stream.HitInterval, 0.35f),
                    "샤샤의 항아리 must turn the basic attack into one 0.2 wide stream that hits every 0.35 seconds.");
                int basicHits = 0;
                player.GetComponent<PlayerCombatEvents>().BasicAttackHit += _ => basicHits++;

                Health near = CreateEnemy(root.transform, Origin + Vector2.right * 3f, 1000f);
                Health behind = CreateEnemy(root.transform, Origin + Vector2.right * 6f, 1000f);
                // Its edge is 0.15 above the centre line: outside the 0.1 half width.
                Health beside = CreateEnemy(root.transform, Origin + new Vector2(3f, 0.65f), 1000f);
                Health back = CreateEnemy(root.transform, Origin + Vector2.left * 3f, 1000f);
                CreateWall(root.transform, Origin + Vector2.right * (WallDistance + 0.5f));
                Physics2D.SyncTransforms();

                Assert(stream.Tick(Vector2.right, Start) && stream.BurstCount == 1,
                    "Holding the attack direction must fire the first burst at once.");
                Assert(stream.IsFiring && Near(near.CurrentHealth, 990f) && Near(behind.CurrentHealth, 990f) &&
                       Near(beside.CurrentHealth, 1000f) && Near(back.CurrentHealth, 1000f) && basicHits == 2,
                    "The stream must hit every enemy on its line once, as basic attack hits: " +
                    $"near {near.CurrentHealth}, behind {behind.CurrentHealth}, beside {beside.CurrentHealth}.");
                KnockbackReceiver pushed = near.GetComponent<KnockbackReceiver>();
                Assert(pushed.IsPushed && Near(pushed.PushVelocity.x, 0.6f) && Near(pushed.PushVelocity.y, 0f),
                    "A stream hit must push with 30% of the basic attack knockback along the stream.");

                Assert(!stream.Tick(Vector2.right, Start + 0.34f) && stream.IsFiring &&
                       Near(near.CurrentHealth, 990f) && basicHits == 2,
                    "A held stream must not fire again before the attack cooldown.");
                Assert(stream.Tick(Vector2.right, Start + 0.36f) && stream.BurstCount == 2 &&
                       Near(near.CurrentHealth, 980f) && Near(behind.CurrentHealth, 980f) && basicHits == 4,
                    "A held stream must fire one burst per attack cooldown.");

                // Releasing and pressing again, or turning, does not skip the cooldown.
                stream.Tick(null, Start + 0.4f);
                Assert(!stream.IsFiring && !stream.Tick(Vector2.left, Start + 0.5f) && Near(back.CurrentHealth, 1000f),
                    "Releasing the attack must stop the stream without resetting its cooldown.");
                Assert(stream.Tick(Vector2.left, Start + 0.72f) && Near(back.CurrentHealth, 990f) &&
                       Near(near.CurrentHealth, 980f),
                    "The next burst must follow the attack direction.");
                stream.Tick(null, Start + 0.8f);
                Assert(!stream.IsFiring, "Without an attack direction the stream must stop.");

                // Hit effects ride on a stream hit like on a shot.
                Assert(player.GetComponent<PlayerInventory>().TryAcquire(LoadItem(Week23Artifact2Setup.GunId)),
                    "앗땃따건 must be acquirable.");
                stats.AddBasicAttackShock(1f, 0.1f, 2.5f, 4);
                stream.Tick(Vector2.right, Start + 2f);
                EnemyStatusEffects status = near.GetComponent<EnemyStatusEffects>();
                Assert(status != null && status.IsShocked, "A stream hit must apply the basic attack status effects.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateLengthAndObstacles()
        {
            GameObject root = new("Passive-4 Length Verification");
            try
            {
                CreateStreamPlayer(root.transform, Origin, out PlayerWaterStream stream);
                CreateWall(root.transform, Origin + Vector2.right * (WallDistance + 0.5f));
                Physics2D.SyncTransforms();
                stream.Tick(Vector2.right, Start);
                Assert(Near(stream.LastLength, WallDistance), "The stream must end at the wall.");
                stream.Tick(Vector2.up, Start + 1f);
                Assert(Near(stream.LastLength, PlayerWaterStream.MaximumLength),
                    "Without a wall the stream must reach its maximum length.");

                // An obstacle and a chest on the line do not stop the stream; the obstacle takes a hit per cooldown.
                GameObject rock = CreateSolid(root.transform, "Passive-4 Rock", Origin + Vector2.right * 4f,
                    typeof(DestructibleObstacle));
                DestructibleObstacle obstacle = rock.GetComponent<DestructibleObstacle>();
                CreateSolid(root.transform, "Passive-4 Fixed Obstacle", Origin + Vector2.right * 6f,
                    typeof(RoomStaticObstacle));
                Health beyond = CreateEnemy(root.transform, Origin + Vector2.right * 8f, 1000f);
                Physics2D.SyncTransforms();
                stream.Tick(Vector2.right, Start + 2f);
                Assert(Near(stream.LastLength, WallDistance) && obstacle.HitsTaken == 1 &&
                       Near(beyond.CurrentHealth, 990f),
                    "The stream must pass over obstacles, hit a destructible one and reach the enemy behind them.");
                stream.Tick(Vector2.right, Start + 2.2f);
                Assert(obstacle.HitsTaken == 1, "An obstacle must not be hit again before the attack cooldown.");
                stream.Tick(Vector2.right, Start + 2.4f);
                Assert(obstacle.HitsTaken == 2, "An obstacle on the stream must be hit once per burst.");

                // A door stops the stream even where its collider is a trigger.
                GameObject door = CreateSolid(root.transform, "Passive-4 Door", Origin + Vector2.right * 2.5f,
                    typeof(DoorController));
                door.GetComponent<BoxCollider2D>().isTrigger = true;
                Physics2D.SyncTransforms();
                stream.Tick(Vector2.right, Start + 4f);
                Assert(Near(stream.LastLength, 2f) && Near(beyond.CurrentHealth, 980f),
                    "The stream must end at a door and not reach the enemy behind it.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateShotModifiers()
        {
            GameObject root = new("Passive-4 Modifier Verification");
            try
            {
                GameObject player = CreateStreamPlayer(root.transform, Origin, out PlayerWaterStream stream);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                CreateWall(root.transform, Origin + Vector2.right * (WallDistance + 0.5f));
                Health target = CreateEnemy(root.transform, Origin + Vector2.right * 3f, 1000f);
                // Its edge is 0.15 above the centre line: reached only by a stream at least 0.3 wide.
                Health beside = CreateEnemy(root.transform, Origin + new Vector2(6f, 0.65f), 1000f);
                Physics2D.SyncTransforms();

                // Range and shot speed do nothing for the stream.
                stats.AddProjectileLifetimePercent(0.3f);
                Assert(inventory.TryAcquire(LoadItem(Week23Artifact2Setup.WindArrowId)),
                    "실라의 바람살 must be acquirable.");
                stream.Tick(Vector2.right, Start);
                Assert(Near(stream.LastLength, WallDistance) && Near(target.CurrentHealth, 990f) &&
                       Near(target.GetComponent<KnockbackReceiver>().PushVelocity.x, 0.6f) &&
                       Near(beside.CurrentHealth, 1000f),
                    "Range and shot speed must not change the stream's length or knockback.");
                Assert(Near(stream.HitInterval, 0.35f / 1.15f),
                    "Attack speed must shorten the time between stream hits.");

                // 칸나의 대포: +50% per stack widens the stream.
                ItemDefinition cannon = LoadItem(Week23Passive1Setup.CannonId);
                Assert(inventory.TryAcquire(cannon) && inventory.TryAcquire(cannon) && Near(stream.BeamWidth, 0.4f),
                    "Two 칸나의 대포 must double the stream's width.");
                stream.Tick(Vector2.right, Start + 1f);
                Assert(Near(beside.CurrentHealth, 990f) && Near(target.CurrentHealth, 980f),
                    "A wider stream must reach an enemy beside the centre line.");

                // 다중 투사체: one parallel stream per shot, each with its own hit timer.
                stats.AddProjectiles(1);
                Assert(stream.BeamCount == 2, "One extra shot must add one parallel stream.");
                stream.Tick(Vector2.right, Start + 2f);
                Assert(Near(target.CurrentHealth, 960f),
                    $"Two parallel streams must both hit an enemy they cross, but it has {target.CurrentHealth}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateSplitSynergy()
        {
            GameObject root = new("Passive-4 Split Verification");
            try
            {
                GameObject player = CreateStreamPlayer(root.transform, Origin, out PlayerWaterStream stream);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                Assert(player.GetComponent<PlayerInventory>().TryAcquire(LoadItem("item-11")) &&
                       stats.ProjectileSplitSettings.SplitsOnHit,
                    "다야의 다이아몬드 커터 must be acquirable and split on every hit.");
                Health target = CreateEnemy(root.transform, Origin + Vector2.right * 3f, 1000f);
                Health above = CreateEnemy(root.transform, Origin + new Vector2(3f, 1.2f), 1000f);
                Health below = CreateEnemy(root.transform, Origin + new Vector2(3f, -1.2f), 1000f);
                // Its edge is 2.5 above the enemy hit: beyond the 1.5 short stream.
                Health high = CreateEnemy(root.transform, Origin + new Vector2(3f, 3f), 1000f);
                Physics2D.SyncTransforms();

                stream.Tick(Vector2.right, Start);
                Assert(Near(target.CurrentHealth, 990f) && Near(above.CurrentHealth, 995f) &&
                       Near(below.CurrentHealth, 995f) && Near(high.CurrentHealth, 1000f),
                    "The enemy hit must send four short streams that deal the split damage to others near it: " +
                    $"target {target.CurrentHealth}, above {above.CurrentHealth}, below {below.CurrentHealth}, " +
                    $"high {high.CurrentHealth}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateBounceSynergy()
        {
            GameObject root = new("Passive-4 Bounce Verification");
            try
            {
                GameObject player = CreateStreamPlayer(root.transform, Origin, out PlayerWaterStream stream);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                stats.AddProjectileBounce(2, 3.5f, 0.5f, 0.15f);
                Health target = CreateEnemy(root.transform, Origin + Vector2.right * 3f, 1000f);
                Health first = CreateEnemy(root.transform, Origin + new Vector2(3f, -1.5f), 1000f);
                Health second = CreateEnemy(root.transform, Origin + new Vector2(3f, -3f), 1000f);
                Health third = CreateEnemy(root.transform, Origin + new Vector2(3f, -4.5f), 1000f);
                // Further than the short jump range of 2 from every enemy in the chain.
                Health distant = CreateEnemy(root.transform, Origin + new Vector2(6.5f, 3f), 1000f);
                Physics2D.SyncTransforms();

                stream.Tick(Vector2.right, Start);
                Assert(Near(target.CurrentHealth, 990f) && Near(first.CurrentHealth, 990f) &&
                       Near(second.CurrentHealth, 990f) && Near(third.CurrentHealth, 1000f) &&
                       Near(distant.CurrentHealth, 1000f),
                    "A stream hit must jump to the nearest enemies in turn, once per bounce and only over a short " +
                    $"range: first {first.CurrentHealth}, second {second.CurrentHealth}, third {third.CurrentHealth}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateStreamPlayer(Transform parent, Vector2 position, out PlayerWaterStream stream)
        {
            GameObject player = CreatePlayer(parent, position);
            Assert(player.GetComponent<PlayerInventory>().TryAcquire(LoadItem(Week23Passive4Setup.JarId)),
                "샤샤의 항아리 must be acquirable.");
            stream = player.GetComponent<PlayerWaterStream>();
            Assert(stream != null, "샤샤의 항아리 must add the water stream to the player.");
            return player;
        }

        private static void CreateWall(Transform parent, Vector2 position)
        {
            GameObject wall = CreateSolid(parent, "Passive-4 Wall", position);
            wall.GetComponent<BoxCollider2D>().size = new Vector2(1f, 20f);
        }

        private static GameObject CreateSolid(Transform parent, string name, Vector2 position,
            params System.Type[] components)
        {
            GameObject solid = new(name, new[] { typeof(BoxCollider2D) }.Concat(components).ToArray());
            solid.transform.SetParent(parent);
            solid.transform.position = position;
            solid.layer = LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName);
            return solid;
        }
    }
}
