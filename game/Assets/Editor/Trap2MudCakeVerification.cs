using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Trap-2: 진흙케이크. A floor trap bound to its drawing; the first grounded step bursts it once and hurts what
    // stands on the cake by half a heart, flying bodies skip it, the last burst frame is a puddle that slows grounded
    // bodies by 30% for 4 seconds and fades, a bomb bursts it without the hit, and a burst cake stays gone on a rebuild.
    // The burst frames share one scale and the cake's ground line, and the puddle constants match the last frame.
    public static class Trap2MudCakeVerification
    {
        // Far from every authored scene and from the Trap-0 verification origin.
        private static readonly Vector2 Origin = new(-6400f, 6400f);

        [MenuItem("Trickal Fan Game/Traps/Setup and Verify Trap-2 Mud Cake")]
        public static void SetupAndVerifyBatch()
        {
            Trap2MudCakeSetup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(Trap2MudCakeSetup.PrefabPath);
            Trap2MudCakeSetup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) && guid == AssetDatabase.AssetPathToGUID(Trap2MudCakeSetup.PrefabPath),
                "Trap-2 setup changed or lost the 진흙케이크 Prefab GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Traps/Verify Trap-2 Mud Cake")]
        public static void Verify()
        {
            ValidatePrefab();
            ValidateBurstFrames();
            ValidateStepBurst();
            ValidateFlyingAndBomb();
            Debug.Log("Trap-2 verification passed: 진흙케이크 is a collider-free floor trap bound to its drawing; the " +
                      "first grounded step bursts it once for half a heart to what stands on it (enemies uncredited), " +
                      "flying bodies skip it, the burst plays four frames on the cake's ground line and holds a puddle " +
                      "that slows grounded bodies by 30% for 4s while fading, a bomb bursts it without the hit and a " +
                      "burst cake stays gone on a rebuild. Play art review pending.");
        }

        [MenuItem("Trickal Fan Game/Traps/Verify Trap-2 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            // The shared trap path, its artwork, shadows and the development panel.
            Week25Trap0Verification.Verify();
            Week25Trap1Verification.Verify();
            EnvironmentHazardArtworkVerification.Verify();
            FairyKingdomArtworkVerification.Verify();
            GroundShadowVerification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Trap-2 regression verification passed.");
        }

        private static void ValidatePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Trap2MudCakeSetup.PrefabPath);
            RoomTrap trap = prefab != null ? prefab.GetComponent<RoomTrap>() : null;
            Assert(trap != null && trap.Kind == RoomTrapKind.MudCake && trap.IsFloorTrap &&
                   trap.TryValidate(out _) && prefab.GetComponentInChildren<Collider2D>(true) == null &&
                   prefab.GetComponent<DestructibleObstacle>() == null &&
                   trap.HitSize == Week25Trap0Setup.DrawnSize(RoomTrapKind.MudCake) &&
                   trap.HitSize == Vector2.one * RoomTrap.CellSize && RoomTrap.MudCakeWidth < RoomTrap.CellSize &&
                   trap.Visual != null && trap.Visual.sortingOrder == Week25Trap0Setup.FloorSortingOrder,
                "The 진흙케이크 Prefab must be a valid collider-free floor trap drawn smaller than the whole cell it " +
                "is judged on.");
            FairyKingdomArtworkView view = prefab.GetComponent<FairyKingdomArtworkView>();
            Assert(view != null && view.ArtworkId == Trap2MudCakeSetup.ArtworkId &&
                   view.LocalWidth == RoomTrap.MudCakeWidth &&
                   view.Mode == FairyKingdomArtworkMode.Fixed &&
                   RoomTrap.ArtworkIdOf(RoomTrapKind.MudCake) == Trap2MudCakeSetup.ArtworkId,
                "The 진흙케이크 Prefab must show its own drawing.");
            Assert(Week25Trap0Setup.Kinds.Contains(RoomTrapKind.MudCake) &&
                   (int)RoomTrapKind.MudCake == 7 && (int)RoomTrapKind.PoisonMushroom == 6,
                "진흙케이크 must be appended after the seven Trap-0 kinds.");
            Assert(RoomTrap.MudSpeedMultiplier == 0.7f && RoomTrap.MudPuddleSeconds == 4f &&
                   RoomTrap.MudPuddleFadeSeconds > 0f && RoomTrap.MudPuddleFadeSeconds < RoomTrap.MudPuddleSeconds,
                "The puddle slows by 30% for 4 seconds and fades at the end.");
        }

        private static void ValidateBurstFrames()
        {
            string id = EnvironmentHazardArtwork.MudCakeBurst;
            Sprite intact = FairyKingdomArtwork.Fit(Trap2MudCakeSetup.ArtworkId, 1f);
            Assert(intact != null, "The 진흙케이크 drawing is missing from the artwork catalog.");
            var seen = new HashSet<Sprite>();
            Sprite first = null;
            for (int frame = 0; frame < EnvironmentHazardArtwork.MudCakeBurstFrames; frame++)
            {
                Sprite sprite = EnvironmentHazardArtwork.Fit(id,
                    (frame + 0.5f) * EnvironmentHazardArtwork.MudCakeBurstFrameSeconds, 1f);
                Assert(sprite != null && seen.Add(sprite), $"Missing or repeated burst frame {frame}.");
                first ??= sprite;
                Assert(Mathf.Abs(sprite.pixelsPerUnit - first.pixelsPerUnit) < 0.001f &&
                       sprite.rect.size == first.rect.size,
                    "Every burst frame must keep one cell size and scale.");
                Assert(Mathf.Abs(sprite.bounds.min.y - intact.bounds.min.y) < 0.001f &&
                       Mathf.Abs(sprite.bounds.center.x) < 0.001f,
                    "Every burst frame must stand on the cake's drawn ground line, centered on the cell.");
            }

            Sprite last = EnvironmentHazardArtwork.Fit(id,
                (EnvironmentHazardArtwork.MudCakeBurstFrames - 0.5f) * EnvironmentHazardArtwork.MudCakeBurstFrameSeconds,
                1f);
            Assert(EnvironmentHazardArtwork.Fit(id, 100f, 1f) == last && EnvironmentHazardArtwork.Fit(id, 0f, 1f) == first,
                "The burst must start on its first frame and hold the puddle frame.");

            // The puddle constants (per unit of drawing width) are the visible part of the last frame fitted 1 wide.
            Texture2D texture = new(2, 2);
            try
            {
                string path = "Assets/Resources/" + EnvironmentHazardArtwork.ResourceRoot + id + ".png";
                Assert(texture.LoadImage(File.ReadAllBytes(path)), "Could not read " + path);
                RectInt rect = new((int)last.rect.x, (int)last.rect.y, (int)last.rect.width, (int)last.rect.height);
                Color32[] pixels = texture.GetPixels32();
                int left = int.MaxValue, right = -1, bottom = int.MaxValue, top = -1;
                for (int y = rect.yMin; y < rect.yMax; y++)
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (pixels[y * texture.width + x].a < 96) continue;
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                }

                Assert(right >= left, "The puddle frame is empty.");
                float ppu = last.pixelsPerUnit;
                Vector2 size = new((right - left + 1) / ppu, (top - bottom + 1) / ppu);
                Vector2 center = new(last.bounds.min.x + ((left + right + 1) * 0.5f - rect.xMin) / ppu,
                    last.bounds.min.y + ((bottom + top + 1) * 0.5f - rect.yMin) / ppu);
                Assert((size - RoomTrap.MudPuddleSizePerWidth).magnitude < 0.05f &&
                       (center - RoomTrap.MudPuddleOffsetPerWidth).magnitude < 0.04f,
                    $"The puddle area {RoomTrap.MudPuddleSizePerWidth}@{RoomTrap.MudPuddleOffsetPerWidth} must match the drawn " +
                    $"puddle {size}@{center}.");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static void ValidateStepBurst()
        {
            Week25Trap0Verification.Fixture fixture = new("Trap-2 Mud Cake");
            try
            {
                float time = Time.time;
                RoomRunState state = new("floor-01-room-09");
                RoomTrap cake = fixture.PlaceTrap(RoomTrapKind.MudCake, Origin, "mud-01", false, state);
                PlayerStats stats = fixture.Player.GetComponent<PlayerStats>();
                float baseSpeed = stats.MoveSpeed;

                fixture.StandOn(Origin + Vector2.right * 3f);
                Health enemy = fixture.CreateEnemy(Origin + Vector2.down * 0.1f, false);
                Health flyingEnemy = fixture.CreateEnemy(Origin + Vector2.up * 0.1f, true);
                Health bystander = fixture.CreateEnemy(Origin + Vector2.right * 2f, false);
                Physics2D.SyncTransforms();
                cake.Tick(time);
                Assert(cake.HasBurst && cake.BurstTime == time && state.IsTrapRemoved("mud-01") &&
                       enemy.CurrentHealth == enemy.MaxHealth - RoomTrap.EnemyDamage &&
                       flyingEnemy.CurrentHealth == flyingEnemy.MaxHealth &&
                       bystander.CurrentHealth == bystander.MaxHealth && fixture.PlayerUnits == 10f &&
                       fixture.CreditedKills == 0,
                    "A grounded enemy stepping on the cake must burst it and take the trap hit; a flying enemy, a " +
                    "body off the cake and the distant player are not hurt.");

                // The cake is spent: standing on it later never hurts again.
                fixture.StandOn(Origin);
                cake.Tick(time + 0.1f);
                Assert(fixture.PlayerUnits == 10f && enemy.CurrentHealth == enemy.MaxHealth - RoomTrap.EnemyDamage,
                    "A burst cake must not hurt again.");
                Assert(cake.Visual.transform.localScale == Vector3.one &&
                       Mathf.Approximately(RoomTrap.MudPuddleScaleAt(0.2f), 1f),
                    "The first three burst frames must keep the cake's size.");
                float halfway = RoomTrap.MudPuddleScaleAt(3f * EnvironmentHazardArtwork.MudCakeBurstFrameSeconds +
                                                          RoomTrap.MudPuddleGrowSeconds * 0.5f);
                Assert(halfway > 1f && halfway < RoomTrap.MudPuddleGrownScale &&
                       Mathf.Approximately(RoomTrap.MudPuddleScaleAt(1f), RoomTrap.MudPuddleGrownScale) &&
                       RoomTrap.MudPuddleSize == RoomTrap.MudPuddleSizePerWidth,
                    "The puddle frame must spread smoothly to the size it has drawn one cell wide.");

                // The puddle slows grounded bodies on it, not the flyer, and fades out before it ends.
                fixture.StandOn(Origin + RoomTrap.MudPuddleOffset);
                cake.Tick(time + 1f);
                Vector3 grown = cake.Visual.transform.localPosition +
                                cake.Visual.transform.localScale.x * (Vector3)RoomTrap.MudPuddleOffset;
                Assert(Mathf.Approximately(cake.Visual.transform.localScale.x, RoomTrap.MudPuddleGrownScale) &&
                       (grown - (Vector3)RoomTrap.MudPuddleOffset).magnitude < 0.001f,
                    "The grown puddle must stay centered where the cake's puddle was.");
                Assert(Mathf.Approximately(stats.MoveSpeed, baseSpeed * RoomTrap.MudSpeedMultiplier) &&
                       Mathf.Approximately(EnemyStatusEffects.MoveSpeedMultiplierOf(enemy), RoomTrap.MudSpeedMultiplier) &&
                       Mathf.Approximately(EnemyStatusEffects.MoveSpeedMultiplierOf(flyingEnemy), 1f) &&
                       Mathf.Approximately(EnemyStatusEffects.MoveSpeedMultiplierOf(bystander), 1f) &&
                       cake.Visual.color.a == 1f,
                    "The puddle must slow the player and a grounded enemy on it by 30% and leave others alone.");
                cake.Tick(time + RoomTrap.MudPuddleSeconds - RoomTrap.MudPuddleFadeSeconds * 0.5f);
                Assert(cake.Visual.color.a > 0.4f && cake.Visual.color.a < 0.6f && cake.gameObject.activeSelf,
                    "The puddle must fade during its last half second.");
                cake.Tick(time + RoomTrap.MudPuddleSeconds);
                Assert(cake.IsRemoved && !cake.gameObject.activeSelf &&
                       fixture.Player.GetComponent<EnvironmentSlow>().MultiplierAt(time + RoomTrap.MudPuddleSeconds + 1f) == 1f,
                    "The puddle must be gone after 4 seconds and its slow must end.");

                RoomTrap rebuilt = fixture.PlaceTrap(RoomTrapKind.MudCake, Origin, "mud-01", false, state);
                Assert(rebuilt.IsRemoved && !rebuilt.gameObject.activeSelf, "A rebuilt room must keep a burst cake gone.");

                // A player stepping on a fresh cake bursts it for half a heart.
                RoomTrap playerCake = fixture.PlaceTrap(RoomTrapKind.MudCake, Origin + Vector2.right * 6f, "mud-02",
                    false, state);
                fixture.EndHitWindow();
                fixture.StandOn(Origin + Vector2.right * 6f);
                playerCake.Tick(time + 5f);
                Assert(playerCake.HasBurst && fixture.PlayerUnits == 10f - HealthUnits.HalfHeartDamageUnits,
                    "A player stepping on the cake must burst it and lose half a heart.");
            }
            finally { fixture.Dispose(); }
        }

        private static void ValidateFlyingAndBomb()
        {
            Week25Trap0Verification.Fixture fixture = new("Trap-2 Mud Cake Bomb");
            try
            {
                float time = Time.time;
                RoomRunState state = new("floor-01-room-10");
                RoomTrap flown = fixture.PlaceTrap(RoomTrapKind.MudCake, Origin, "mud-11", false, state);
                fixture.StandOn(Origin);
                Assert(fixture.Player.gameObject.AddComponent<PlayerFlight>().TryStartFlying(),
                    "The fixture player must start flying.");
                flown.Tick(time);
                Assert(!flown.HasBurst && fixture.PlayerUnits == 10f, "A flying player must not burst the cake.");

                RoomTrap bombed = fixture.PlaceTrap(RoomTrapKind.MudCake, Origin + Vector2.right * 4f, "mud-12", false,
                    state);
                RoomTrap far = fixture.PlaceTrap(RoomTrapKind.MudCake, Origin + Vector2.right * 9f, "mud-13", false,
                    state);
                Health enemy = fixture.CreateEnemy(Origin + Vector2.right * 4f, false);
                Physics2D.SyncTransforms();
                Assert(RoomTrap.RemoveWebsByBombInCircle(Origin + Vector2.right * 5.4f, 1f) == 0 && bombed.HasBurst &&
                       state.IsTrapRemoved("mud-12") && enemy.CurrentHealth == enemy.MaxHealth && !far.HasBurst &&
                       !state.IsTrapRemoved("mud-13"),
                    "A bomb must burst only the cakes it reaches, without the trap hit, and not count them as webs.");
            }
            finally { fixture.Dispose(); }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
