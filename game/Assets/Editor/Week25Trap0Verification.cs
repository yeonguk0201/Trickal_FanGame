using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Trap-0: the seven room traps. Player hits are half a heart, judged by the feet center against the ellipse the
    // drawing covers, and share the post-hit window,
    // floor traps treat enemies alike without crediting the kill, flying bodies skip floor traps, and only an
    // extinguished fire, a broken spiked rock or mushroom and a bombed web are kept for a revisit.
    public static class Week25Trap0Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-6100f, 6100f);
        private const float HalfHeart = HealthUnits.HalfHeartDamageUnits;

        [MenuItem("Trickal Fan Game/Week 25/Setup and Verify Trap-0 Traps")]
        public static void SetupAndVerifyBatch()
        {
            Week25Trap0Setup.Setup();
            Dictionary<string, string> guids = Week25Trap0Setup.CreatedAssetPaths()
                .ToDictionary(path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week25Trap0Setup.Setup();
            foreach (KeyValuePair<string, string> entry in guids)
                Assert(!string.IsNullOrWhiteSpace(entry.Value) && entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Trap-0 setup changed or lost the GUID for {entry.Key}.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 25/Verify Trap-0 Traps")]
        public static void Verify()
        {
            ValidatePrefabs();
            ValidateFloorSpikes();
            ValidatePeriodicSpikes();
            ValidateDamagePuddle();
            ValidateSpiderweb();
            ValidateFireAndSpikedRock();
            ValidatePoisonMushroom();
            Debug.Log("Trap-0 verification passed: seven trap Prefabs bound to their drawings; every player hit is " +
                      "half a heart judged by the feet center down to the soles against the drawn ellipse, shares the post-hit window and " +
                      "repeats after 1 " +
                      "second; floor traps hurt or slow grounded enemies by the same line from their feet without crediting the " +
                      "kill and ignore flying bodies; periodic spikes run 2s down and 2s up with no warning in two " +
                      "opposite phases and restart on entry; fire blocks flight, goes out after 3 hits or one " +
                      "water hit, a spiked rock breaks in 4 and is flown over, a poison mushroom breaks in 2 and " +
                      "leaves a 3s gas that hits the player once and poisons enemies up to 3 stacks as player " +
                      "damage; only a web is removed by a bomb; and the removed or broken ones stay so on a rebuild.");
        }

        [MenuItem("Trickal Fan Game/Week 25/Verify Trap-0 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            // The systems Trap-0 changed: obstacles and room binding, flight, bombs, the water stream, status slows,
            // artwork and the development panel.
            Week18Obstacle1Verification.Verify();
            Week23Obstacle7Verification.Verify();
            Week23Enemy6Verification.Verify();
            Week20Special2Verification.Verify();
            Week23Passive4Verification.Verify();
            StatusEffectAnimationVerification.Verify();
            FairyKingdomArtworkVerification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Trap-0 regression verification passed.");
        }

        private static void ValidatePrefabs()
        {
            Assert(Week25Trap0Setup.Kinds.Length == 7, "Trap-0 defines exactly seven trap kinds.");
            Assert(!PlayerCombatEvents.IsPlayerDamage(DamageSourceType.Environment),
                "Environment damage must not count as player damage.");
            FairyKingdomArtworkCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FairyKingdomArtworkCatalog>(FairyKingdomArtworkSetup.CatalogPath);
            Assert(catalog != null, "Trap-0 needs the fairy kingdom artwork catalog.");
            Assert(catalog.Entries.Any(entry => entry.id == PoisonGasCloud.ArtworkId && entry.sprite != null),
                "The poison gas drawing is missing from the artwork catalog.");
            int environment = LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName);
            foreach (RoomTrapKind kind in Week25Trap0Setup.Kinds)
            {
                RoomTrap trap = LoadPrefab(kind);
                string artworkId = RoomTrap.ArtworkIdOf(kind);
                FairyKingdomArtworkView view = trap.GetComponent<FairyKingdomArtworkView>();
                Assert(trap.TryValidate(out string error) && trap.Kind == kind && trap.Visual != null &&
                       trap.Visual.sprite != null && view != null && view.ArtworkId == artworkId &&
                       view.Visual == trap.Visual &&
                       catalog.Entries.Any(entry => entry.id == artworkId && entry.sprite == trap.Visual.sprite),
                    $"Trap Prefab {kind} must be valid and bound to the drawing '{artworkId}'. {error}");

                DestructibleObstacle obstacle = trap.GetComponent<DestructibleObstacle>();
                if (RoomTrap.IsFloorKind(kind))
                {
                    Assert(obstacle == null && trap.GetComponentInChildren<Collider2D>(true) == null &&
                           view.Mode == FairyKingdomArtworkMode.Fixed &&
                           trap.Visual.sortingOrder == Week25Trap0Setup.FloorSortingOrder &&
                           trap.HitSize == Week25Trap0Setup.DrawnSize(artworkId) &&
                           trap.HitSize.x == RoomTrap.CellSize && trap.HitSize.y > 0.1f &&
                           trap.HitSize.y < RoomTrap.CellSize,
                        $"Floor trap {kind} must have no collider, draw under actors and take its hit size " +
                        "from its drawing.");
                    continue;
                }

                BoxCollider2D box = trap.GetComponent<BoxCollider2D>();
                Assert(obstacle != null && box != null && !box.isTrigger && box.size == Vector2.one &&
                       trap.gameObject.layer == environment && view.Mode == FairyKingdomArtworkMode.Obstacle &&
                       obstacle.RequiredHits == RoomTrap.RequiredHitsOf(kind) &&
                       RoomMovementClass.IsLowObstacle(box) != RoomTrap.BlocksFlightOf(kind) &&
                       (obstacle.DropTable != null) == (kind == RoomTrapKind.SpikedRock),
                    $"Obstacle trap {kind} must be a solid 1x1 Environment obstacle with its hits, flight rule " +
                    "and drop rule.");
            }

            Assert(RoomTrap.FireHits == 3 && RoomTrap.SpikedRockHits == 4 && RoomTrap.PoisonMushroomHits == 2 &&
                   RoomTrap.BlocksFlightOf(RoomTrapKind.Fire) && !RoomTrap.BlocksFlightOf(RoomTrapKind.SpikedRock) &&
                   !RoomTrap.BlocksFlightOf(RoomTrapKind.PoisonMushroom),
                "Fire takes 3 hits and blocks flight; a spiked rock takes 4 and a mushroom 2, both flown over.");
        }

        private static void ValidateFloorSpikes()
        {
            Fixture fixture = new("Trap-0 Floor Spikes");
            try
            {
                float time = Time.time;
                Vector2 cell = Origin;
                RoomTrap trap = fixture.PlaceTrap(RoomTrapKind.FloorSpikes, cell, "spikes-01");
                fixture.StandOn(cell);
                trap.Tick(time);
                Assert(fixture.PlayerUnits == 10f - HalfHeart &&
                       fixture.Player.GetComponent<PlayerDeathReason>().CurrentReason == RoomTrap.DeathReason,
                    "Floor spikes must take exactly half a heart and record a hazard.");

                trap.Tick(time + 5f);
                Assert(fixture.PlayerUnits == 10f - HalfHeart,
                    "A trap must not hurt the player inside the shared post-hit window.");
                fixture.EndHitWindow();
                trap.Tick(time + 0.5f);
                Assert(fixture.PlayerUnits == 10f - HalfHeart, "The same spikes must wait 1 second before hurting again.");
                trap.Tick(time + 1.05f);
                Assert(fixture.PlayerUnits == 10f - HalfHeart * 2f, "The same spikes must hurt again after 1 second.");

                // The line from the feet center down to the soles against the ellipse the drawing covers: the
                // drawing reaches a feet radius further up than down, and no further sideways.
                fixture.EndHitWindow();
                Vector2 half = trap.HitSize * 0.5f;
                foreach (Vector2 outside in new[]
                         {
                             Vector2.right * (half.x + 0.02f), Vector2.up * (half.y + PlayerFeet.Radius + 0.02f),
                             Vector2.down * (half.y + 0.02f), new Vector2(half.x - 0.05f, -(half.y - 0.05f)),
                         })
                {
                    fixture.StandOn(cell + outside);
                    trap.Tick(time + 3f);
                    Assert(fixture.PlayerUnits == 10f - HalfHeart * 2f,
                        $"Feet off the drawing must not be hurt ({outside}), even inside the cell or with the " +
                        "feet circle touching.");
                }

                fixture.StandOn(cell + Vector2.up * (half.y + PlayerFeet.Radius - 0.02f));
                trap.Tick(time + 3f);
                Assert(fixture.PlayerUnits == 10f - HalfHeart * 3f,
                    "Soles on the drawing's upper edge must be hurt although the feet center is above it.");
                fixture.EndHitWindow();
                fixture.StandOn(cell + Vector2.down * (half.y - 0.02f));
                trap.Tick(time + 4.1f);
                Assert(fixture.PlayerUnits == 10f - HalfHeart * 4f, "A feet center on the drawing's lower edge must be hurt.");

                fixture.EndHitWindow();
                fixture.StandOn(cell);
                Assert(fixture.Player.gameObject.AddComponent<PlayerFlight>().TryStartFlying(),
                    "The fixture player must start flying.");
                trap.Tick(time + 6f);
                Assert(fixture.PlayerUnits == 10f - HalfHeart * 4f, "A flying player must ignore floor spikes.");

                GameObject roomOwner = new("Trap-0 Room", typeof(BoxCollider2D), typeof(RoomController));
                roomOwner.transform.SetParent(fixture.Root, false);
                RoomController room = roomOwner.GetComponent<RoomController>();
                Health enemy = fixture.CreateEnemy(cell + new Vector2(0.2f, -0.2f), false);
                // A body no wider than the feet stands on its own bottom, like the player's feet circle: soles on
                // the drawing's upper edge are hurt although the center is above it, soles above it are not.
                Health solesEnemy = fixture.CreateEnemy(
                    cell + Vector2.up * (half.y + Fixture.EnemyRadius - 0.05f), false);
                Health edgeEnemy = fixture.CreateEnemy(
                    cell + Vector2.up * (half.y + Fixture.EnemyRadius + 0.02f), false);
                Health flyingEnemy = fixture.CreateEnemy(cell + new Vector2(-0.2f, 0.2f), true);
                // A normal enemy with Hitbox-2 feet: the sprite stands on the body bottom, 0.5 under its center.
                const float feetBody = 0.5f;
                Health feetEnemy = fixture.CreateFeetEnemy(cell + Vector2.up * (half.y + feetBody - 0.05f), feetBody);
                Health feetAboveEnemy =
                    fixture.CreateFeetEnemy(cell + Vector2.up * (half.y + feetBody + 0.02f), feetBody);
                room.RegisterEnemy(enemy);
                Physics2D.SyncTransforms();
                trap.Tick(time + 6f);
                Assert(Mathf.Approximately(enemy.CurrentHealth, enemy.MaxHealth - RoomTrap.EnemyDamage) &&
                       Mathf.Approximately(solesEnemy.CurrentHealth, solesEnemy.MaxHealth - RoomTrap.EnemyDamage) &&
                       edgeEnemy.CurrentHealth == edgeEnemy.MaxHealth &&
                       flyingEnemy.CurrentHealth == flyingEnemy.MaxHealth,
                    "Spikes must take a fixed 5 from a grounded enemy whose soles are on the drawing, and nothing " +
                    "from one whose soles are above it or from a flying one.");
                Assert(Mathf.Approximately(feetEnemy.CurrentHealth, feetEnemy.MaxHealth - RoomTrap.EnemyDamage) &&
                       feetAboveEnemy.CurrentHealth == feetAboveEnemy.MaxHealth,
                    "An enemy with feet must be hurt once its drawn soles (the body bottom) reach the drawing, " +
                    "although its center is still above it, and not while its soles are above it.");
                trap.Tick(time + 6.5f);
                Assert(Mathf.Approximately(enemy.CurrentHealth, enemy.MaxHealth - RoomTrap.EnemyDamage),
                    "The same spikes must wait 1 second before hurting an enemy again.");
                trap.Tick(time + 7.05f);
                Assert(enemy.IsDead && room.AliveEnemyCount == 0 && fixture.CreditedKills == 0,
                    "An enemy killed by spikes must count for the room and not be credited to the player.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void ValidatePeriodicSpikes()
        {
            Fixture fixture = new("Trap-0 Periodic Spikes");
            try
            {
                float time = Time.time;
                RoomTrap first = fixture.PlaceTrap(RoomTrapKind.PeriodicSpikes, Origin, "periodic-a");
                RoomTrap second = fixture.PlaceTrap(RoomTrapKind.PeriodicSpikes, Origin + Vector2.right * 3f,
                    "periodic-b", true);
                Assert(RoomTrap.PeriodicLoweredSeconds == 2f && RoomTrap.PeriodicRaisedSeconds == 2f,
                    "Periodic spikes are 2 seconds down, 2 seconds up.");
                Assert(first.StateAt(time + 0.1f) == PeriodicSpikeState.Lowered &&
                       first.StateAt(time + 1.4f) == PeriodicSpikeState.Lowered &&
                       first.StateAt(time + 1.9f) == PeriodicSpikeState.Lowered &&
                       first.StateAt(time + 2.1f) == PeriodicSpikeState.Raised &&
                       first.StateAt(time + 3.9f) == PeriodicSpikeState.Raised &&
                       first.StateAt(time + 4.1f) == PeriodicSpikeState.Lowered &&
                       first.StateAt(time + 5.9f) == PeriodicSpikeState.Lowered,
                    "Phase A must start lowered, rise without a warning and repeat every 4 seconds.");
                for (float offset = 0.1f; offset < 8f; offset += 0.25f)
                {
                    Assert((first.StateAt(time + offset) == PeriodicSpikeState.Raised) !=
                           (second.StateAt(time + offset) == PeriodicSpikeState.Raised),
                        $"Phase B must be half a cycle from phase A (offset {offset}).");
                }

                fixture.StandOn(Origin);
                first.Tick(time + 0.1f);
                Sprite loweredSprite = first.Visual.sprite;
                first.Tick(time + 1.9f);
                Assert(fixture.PlayerUnits == 10f && first.Visual.color == Color.white &&
                       first.Visual.sprite == loweredSprite,
                    "Spikes about to rise must not hurt and must look the same as lowered spikes (no warning tint).");
                first.Tick(time + 2.1f);
                Assert(fixture.PlayerUnits == 10f - HalfHeart && first.Visual.color.a == 1f,
                    "Raised spikes must hurt for half a heart.");

                first.RestartCycle(time + 2.5f);
                Assert(first.StateAt(time + 2.6f) == PeriodicSpikeState.Lowered,
                    "Entering the room must restart the cycle from lowered.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void ValidateDamagePuddle()
        {
            Fixture fixture = new("Trap-0 Damage Puddle");
            try
            {
                float time = Time.time;
                RoomTrap trap = fixture.PlaceTrap(RoomTrapKind.DamagePuddle, Origin, "puddle-01");
                fixture.StandOn(Origin);
                Health enemy = fixture.CreateEnemy(Origin + Vector2.right * 0.3f, false);
                Physics2D.SyncTransforms();
                trap.Tick(time);
                fixture.EndHitWindow();
                trap.Tick(time + 0.9f);
                Assert(fixture.PlayerUnits == 10f - HalfHeart &&
                       Mathf.Approximately(enemy.CurrentHealth, enemy.MaxHealth - RoomTrap.EnemyDamage),
                    "A puddle must hurt once inside the same second, the player by half a heart and an enemy by 5.");
                trap.Tick(time + 1.05f);
                Assert(fixture.PlayerUnits == 10f - HalfHeart * 2f && enemy.IsDead,
                    "A puddle must hurt again every second while a body stays on it.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void ValidateSpiderweb()
        {
            Fixture fixture = new("Trap-0 Spiderweb");
            Fixture flyer = new("Trap-0 Spiderweb Flyer");
            try
            {
                float time = Time.time;
                RoomRunState state = new("floor-01-room-02");
                RoomTrap web = fixture.PlaceTrap(RoomTrapKind.Spiderweb, Origin, "web-01", false, state);
                RoomTrap farWeb = fixture.PlaceTrap(RoomTrapKind.Spiderweb, Origin + Vector2.right * 6f, "web-02",
                    false, state);
                PlayerStats stats = fixture.Player.GetComponent<PlayerStats>();
                float baseSpeed = stats.MoveSpeed;
                fixture.StandOn(Origin);
                Health enemy = fixture.CreateEnemy(Origin + Vector2.down * 0.2f, false);
                Health flyingEnemy = fixture.CreateEnemy(Origin + Vector2.up * 0.2f, true);
                Physics2D.SyncTransforms();
                web.Tick(time);
                Assert(fixture.PlayerUnits == 10f && enemy.CurrentHealth == enemy.MaxHealth,
                    "A web must not deal damage.");
                Assert(RoomTrap.WebSpeedMultiplier == 0.6f &&
                       Mathf.Approximately(stats.MoveSpeed, baseSpeed * RoomTrap.WebSpeedMultiplier) &&
                       Mathf.Approximately(EnemyStatusEffects.MoveSpeedMultiplierOf(enemy),
                           RoomTrap.WebSpeedMultiplier) &&
                       Mathf.Approximately(EnemyStatusEffects.MoveSpeedMultiplierOf(flyingEnemy), 1f),
                    "A web must slow the player and a grounded enemy by 40% and leave a flying enemy alone.");
                Assert(fixture.Player.GetComponent<EnvironmentSlow>().MultiplierAt(time + 1f) == 1f,
                    "The web slow must end shortly after leaving the web.");

                flyer.Player.transform.position = fixture.Player.transform.position + Vector3.right * 3f;
                RoomTrap flownWeb = flyer.PlaceTrap(RoomTrapKind.Spiderweb, Origin + Vector2.right * 3f, "web-03");
                flyer.StandOn(Origin + Vector2.right * 3f);
                Assert(flyer.Player.gameObject.AddComponent<PlayerFlight>().TryStartFlying(),
                    "The fixture player must start flying.");
                flownWeb.Tick(time);
                Assert(flyer.Player.GetComponent<EnvironmentSlow>() == null, "A flying player must ignore a web.");
                flownWeb.gameObject.SetActive(false);

                Assert(DestructibleObstacle.HitInCircle(Origin, 1f) == 0 && web.gameObject.activeSelf && !web.IsRemoved,
                    "Player attacks must not remove a web.");
                // The explosion reaches the web cell's edge, not its center.
                Assert(RoomTrap.RemoveWebsByBombInCircle(Origin + Vector2.right * 1.4f, 1f) == 1 && web.IsRemoved &&
                       !web.gameObject.activeSelf && state.IsTrapRemoved("web-01") && !farWeb.IsRemoved &&
                       !state.IsTrapRemoved("web-02") &&
                       RoomTrap.RemoveWebsByBombInCircle(Origin + Vector2.right * 1.4f, 1f) == 0,
                    "A bomb must remove exactly the webs it reaches, once, and record them on the room.");

                RoomTrap rebuilt = fixture.PlaceTrap(RoomTrapKind.Spiderweb, Origin, "web-01", false, state);
                Assert(rebuilt.IsRemoved && !rebuilt.gameObject.activeSelf,
                    "A rebuilt room must keep a bombed web removed.");
            }
            finally
            {
                fixture.Dispose();
                flyer.Dispose();
            }
        }

        private static void ValidateFireAndSpikedRock()
        {
            Fixture fixture = new("Trap-0 Fire");
            try
            {
                float time = Time.time;
                RoomRunState state = new("floor-01-room-03");
                RoomTrap fire = fixture.PlaceTrap(RoomTrapKind.Fire, Origin, "fire-01", false, state);
                RoomTrap wetFire = fixture.PlaceTrap(RoomTrapKind.Fire, Origin + Vector2.right * 3f, "fire-02", false,
                    state);
                RoomTrap rock = fixture.PlaceTrap(RoomTrapKind.SpikedRock, Origin + Vector2.right * 6f, "spiked-01",
                    false, state);
                DestructibleObstacle fireObstacle = fire.GetComponent<DestructibleObstacle>();
                DestructibleObstacle rockObstacle = rock.GetComponent<DestructibleObstacle>();
                Assert(fireObstacle.BlocksFlight && !RoomMovementClass.IsLowObstacle(fire.GetComponent<Collider2D>()) &&
                       RoomMovementClass.IsLowObstacle(rock.GetComponent<Collider2D>()),
                    "Fire must block a flying player and a spiked rock must be flown over.");

                Health player = fixture.Player.GetComponent<Health>();
                Assert(fire.TryDamagePlayer(player, time) && fixture.PlayerUnits == 10f - HalfHeart,
                    "Touching fire must take half a heart.");
                Assert(!rock.TryDamagePlayer(player, time) && fixture.PlayerUnits == 10f - HalfHeart,
                    "Fire and a spiked rock must share the player's post-hit window.");
                fixture.EndHitWindow();
                Assert(!fire.TryDamagePlayer(player, time + 0.5f) && rock.TryDamagePlayer(player, time + 0.5f) &&
                       fixture.PlayerUnits == 10f - HalfHeart * 2f,
                    "The same fire must wait 1 second, while a spiked rock touched for the first time hurts.");

                for (int hit = 1; hit < RoomTrap.FireHits; hit++)
                    Assert(fireObstacle.RegisterPlayerHit() && !fireObstacle.IsBroken, $"Fire must survive hit {hit}.");
                Assert(fireObstacle.RegisterPlayerHit() && fireObstacle.IsBroken && !fire.gameObject.activeSelf &&
                       state.IsObstacleDestroyed("fire-01") && fireObstacle.LastDrops.Count == 0,
                    "The 3rd hit must put a fire out without leaving anything.");

                Assert(!rock.TryExtinguish() && !rockObstacle.IsBroken, "Water must only put out a fire.");
                Assert(wetFire.TryExtinguish() && wetFire.GetComponent<DestructibleObstacle>().IsBroken &&
                       state.IsObstacleDestroyed("fire-02"),
                    "One water stream hit must put a fire out.");

                for (int hit = 1; hit < RoomTrap.SpikedRockHits; hit++)
                    Assert(rockObstacle.RegisterPlayerHit() && !rockObstacle.IsBroken,
                        $"A spiked rock must survive hit {hit}.");
                Assert(rockObstacle.RegisterPlayerHit() && rockObstacle.IsBroken &&
                       state.IsObstacleDestroyed("spiked-01"),
                    "The 4th hit must break a spiked rock.");

                RoomTrap rebuiltFire = fixture.PlaceTrap(RoomTrapKind.Fire, Origin, "fire-01", false, state);
                RoomTrap rebuiltRock = fixture.PlaceTrap(RoomTrapKind.SpikedRock, Origin + Vector2.right * 6f,
                    "spiked-01", false, state);
                Assert(!rebuiltFire.gameObject.activeSelf && !rebuiltRock.gameObject.activeSelf,
                    "A rebuilt room must keep an extinguished fire and a broken spiked rock gone.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void ValidatePoisonMushroom()
        {
            Fixture fixture = new("Trap-0 Poison Mushroom");
            try
            {
                float time = Time.time;
                RoomRunState state = new("floor-01-room-04");
                RoomTrap mushroom = fixture.PlaceTrap(RoomTrapKind.PoisonMushroom, Origin, "mushroom-01", false, state);
                DestructibleObstacle obstacle = mushroom.GetComponent<DestructibleObstacle>();
                PlayerStats stats = fixture.Player.GetComponent<PlayerStats>();
                Assert(stats.AttackDamage > 0f, "The fixture player needs attack damage.");
                Assert(PoisonGasCloud.Radius == 1.5f && PoisonGasCloud.Duration == 3f &&
                       PoisonGasCloud.Poison.MaximumStacks == 3 && PoisonGasCloud.Poison.DurationSeconds == 4f &&
                       PoisonGasCloud.Poison.IntervalSeconds == 1f && PoisonGasCloud.Poison.TickDamageRatio == 0.15f,
                    "The gas is radius 1.5 for 3 seconds and poisons with the 비비의 콧물 values.");

                fixture.StandOn(Origin + Vector2.left * 1.6f);
                Assert(obstacle.RegisterPlayerHit() && !obstacle.IsBroken && mushroom.LastGas == null,
                    "A mushroom must survive its 1st hit without gas.");
                PoisonGasCloud outsideGas = PoisonGasCloud.Spawn(Origin, fixture.Root, fixture.Player, time);
                outsideGas.Tick(time);
                Assert(!outsideGas.HasDamagedPlayer && fixture.PlayerUnits == 10f,
                    "A feet center outside the gas radius must not be hurt, even with the feet circle touching.");
                Object.DestroyImmediate(outsideGas.gameObject);
                fixture.StandOn(Origin + Vector2.left * 1.4f);
                Health enemy = fixture.CreateEnemy(Origin + Vector2.right, false);
                Health flyingEnemy = fixture.CreateEnemy(Origin + Vector2.up * 1.2f, true);
                Health farEnemy = fixture.CreateEnemy(Origin + Vector2.right * 3f, false);
                Physics2D.SyncTransforms();

                Assert(obstacle.RegisterPlayerHit() && obstacle.IsBroken && obstacle.LastDrops.Count == 0 &&
                       state.IsObstacleDestroyed("mushroom-01") && mushroom.LastGas != null &&
                       (Vector2)mushroom.LastGas.transform.position == Origin,
                    "The 2nd hit must break a mushroom, drop nothing and leave gas on its cell.");

                PoisonGasCloud gas = mushroom.LastGas;
                gas.Tick(time);
                Assert(gas.HasDamagedPlayer && fixture.PlayerUnits == 10f - HalfHeart,
                    "The gas must take half a heart from a player inside it.");
                Assert(Stacks(enemy) == 1 && Stacks(flyingEnemy) == 1 && Stacks(farEnemy) == 0,
                    "The gas must poison grounded and flying enemies inside it, and none outside.");
                fixture.EndHitWindow();
                gas.Tick(time + 1.1f);
                gas.Tick(time + 1.5f);
                Assert(fixture.PlayerUnits == 10f - HalfHeart && Stacks(enemy) == 2,
                    "One gas must hurt the player once only and add one poison stack each second.");
                gas.Tick(time + 2.1f);
                EnemyStatusEffects effects = enemy.GetComponent<EnemyStatusEffects>();
                Assert(Stacks(enemy) == 3 &&
                       Mathf.Approximately(effects.PoisonTickDamagePerStack, stats.AttackDamage * 0.15f),
                    "Gas poison must reach 3 stacks of 15% of the player's attack damage.");

                // The poison is the player's damage: a kill by its tick is credited.
                enemy.TakeDamage(enemy.CurrentHealth - 0.01f);
                Assert(!enemy.IsDead && fixture.CreditedKills == 0, "The fixture enemy must survive until the tick.");
                effects.Tick(effects.NextPoisonTickTime + 0.01f);
                Assert(enemy.IsDead && fixture.CreditedKills == 1,
                    "An enemy killed by gas poison must be credited to the player.");

                gas.Tick(time + PoisonGasCloud.Duration + 0.1f);
                Assert(gas == null, "The gas must be gone after 3 seconds.");

                Health player = fixture.Player.GetComponent<Health>();
                PoisonGasCloud guarded = PoisonGasCloud.Spawn(Origin, fixture.Root, fixture.Player, time);
                player.SetInvulnerable(true);
                guarded.Tick(time);
                Assert(!guarded.HasDamagedPlayer && fixture.PlayerUnits == 10f - HalfHeart,
                    "The gas must keep its one hit while the player is invulnerable.");
                player.SetInvulnerable(false);
                guarded.Tick(time + 0.5f);
                Assert(guarded.HasDamagedPlayer && fixture.PlayerUnits == 10f - HalfHeart * 2f,
                    "The gas must land its one hit once the player can be hurt.");

                RoomTrap rebuilt = fixture.PlaceTrap(RoomTrapKind.PoisonMushroom, Origin, "mushroom-01", false, state);
                Assert(!rebuilt.gameObject.activeSelf && rebuilt.LastGas == null,
                    "A rebuilt room must keep a broken mushroom gone without new gas.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static int Stacks(Health enemy) =>
            enemy.TryGetComponent(out EnemyStatusEffects effects) ? effects.PoisonStacks : 0;

        private static RoomTrap LoadPrefab(RoomTrapKind kind)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week25Trap0Setup.PrefabPath(kind));
            RoomTrap trap = prefab != null ? prefab.GetComponent<RoomTrap>() : null;
            Assert(trap != null, $"The {kind} trap Prefab is missing. Run Trap-0 setup.");
            return trap;
        }

        // A player with 5 hearts and the feet collider, plus the traps and enemies of one scenario.
        private sealed class Fixture
        {
            public const float EnemyRadius = 0.3f;

            private readonly GameObject root;
            private int creditedKills;

            public Fixture(string name)
            {
                root = new GameObject(name);
                GameObject playerObject = new(name + " Player", typeof(PlayerMovement), typeof(PlayerCombatEvents),
                    typeof(PlayerDeathReason));
                playerObject.transform.SetParent(root.transform, false);
                playerObject.layer = LayerMask.NameToLayer("Player");
                playerObject.GetComponent<Rigidbody2D>().gravityScale = 0f;
                Player = playerObject.GetComponent<PlayerMovement>();
                PlayerFeet.Ensure(playerObject, out _);
                InvokeAwake(playerObject.GetComponent<Health>());
                InvokeAwake(playerObject.GetComponent<PlayerStats>());
                playerObject.GetComponent<PlayerCombatEvents>().EnemyKilled += _ => creditedKills++;
                Assert(PlayerUnits == 10f, "The fixture player must start with 5 hearts.");
            }

            public PlayerMovement Player { get; }
            public Transform Root => root.transform;
            public int CreditedKills => creditedKills;
            public float PlayerUnits => Player.GetComponent<Health>().CurrentHealth;

            // Puts the feet circle's center on the point.
            public void StandOn(Vector2 point)
            {
                Player.transform.position = Player.RootPositionForStanding(point - PlayerFeet.LocalPosition);
                Physics2D.SyncTransforms();
                Assert(Vector2.Distance(Player.FeetPosition, point) < 0.001f, "The fixture feet must stand on the point.");
            }

            public void EndHitWindow() => Player.GetComponent<DamageInvulnerability>().ResetHitWindow();

            public RoomTrap PlaceTrap(RoomTrapKind kind, Vector2 position, string id, bool secondPhase = false,
                RoomRunState state = null)
            {
                RoomTrap trap = Object.Instantiate(LoadPrefab(kind), position, Quaternion.identity, root.transform);
                trap.ConfigurePlacement(id, secondPhase);
                Assert(trap.TryValidate(out string error), error);
                Physics2D.SyncTransforms();
                if (trap.TryGetComponent(out DestructibleObstacle obstacle))
                    obstacle.Bind(state, 7, root.transform, null);
                trap.Bind(state, Player, Time.time);
                return trap;
            }

            public Health CreateEnemy(Vector2 position, bool flying)
            {
                GameObject enemy = new("Trap-0 Enemy", typeof(CircleCollider2D), typeof(Health));
                enemy.transform.SetParent(root.transform, false);
                enemy.transform.position = position;
                enemy.layer = LayerMask.NameToLayer(RoomTrap.EnemyLayerName);
                enemy.GetComponent<CircleCollider2D>().radius = EnemyRadius;
                if (flying)
                {
                    enemy.AddComponent<Rigidbody2D>().gravityScale = 0f;
                    enemy.AddComponent<EnemyFlight>();
                }

                Health health = enemy.GetComponent<Health>();
                InvokeAwake(health);
                return health;
            }

            // A grounded enemy body with the Hitbox-2 feet child.
            public Health CreateFeetEnemy(Vector2 position, float bodyRadius)
            {
                Health health = CreateEnemy(position, false);
                health.GetComponent<CircleCollider2D>().radius = bodyRadius;
                EnemyFeet.Ensure(health.gameObject, out _);
                return health;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void InvokeAwake(Component target)
        {
            try
            {
                target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(target, null);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
