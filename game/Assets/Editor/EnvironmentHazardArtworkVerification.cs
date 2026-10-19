using System;
using System.Collections.Generic;
using TrickalFanGame.Frontend;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class EnvironmentHazardArtworkVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Environment Hazard Frames")]
        public static void Verify()
        {
            foreach (string id in new[] { EnvironmentHazardArtwork.Tree, EnvironmentHazardArtwork.Fire,
                         EnvironmentHazardArtwork.Gas, EnvironmentHazardArtwork.FireMedium,
                         EnvironmentHazardArtwork.FireLow, EnvironmentHazardArtwork.FireExtinguish })
            {
                var seen = new HashSet<Sprite>();
                float fps = id == EnvironmentHazardArtwork.Gas ? 6f : 10f;
                float baseline = 0f;
                float ppu = 0f;
                for (int frame = 0; frame < 8; frame++)
                {
                    Sprite sprite = EnvironmentHazardArtwork.Fit(id, (frame + 0.1f) / fps, 1f);
                    Require(sprite != null && seen.Add(sprite), "Missing/distinct frame: " + id);
                    if (frame == 0) { baseline = sprite.bounds.min.y; ppu = sprite.pixelsPerUnit; }
                    Require(Mathf.Abs(sprite.pixelsPerUnit - ppu) < 0.001f, "Frame scale changed: " + id);
                    if (id != EnvironmentHazardArtwork.Gas)
                        Require(Mathf.Abs(sprite.bounds.min.y - baseline) < 0.001f,
                            "Ground anchor changed: " + id);
                    Require(sprite.rect.xMin >= 0 && sprite.rect.yMin >= 0 &&
                        sprite.rect.xMax <= sprite.texture.width && sprite.rect.yMax <= sprite.texture.height,
                        "Frame outside texture: " + id);
                }
                if (id == EnvironmentHazardArtwork.FireExtinguish)
                    Require(EnvironmentHazardArtwork.Fit(id, 0.75f, 1f) ==
                            EnvironmentHazardArtwork.Fit(id, 100f, 1f), "Extinction must hold its cold last frame.");
                else
                    Require(EnvironmentHazardArtwork.Fit(id, 0f, 1f) ==
                            EnvironmentHazardArtwork.Fit(id, 8f / fps, 1f), "Loop/cache failed: " + id);
            }
            VerifySpikes();
            VerifyObstacle(RoomTrapKind.Fire);
            VerifyObstacle(RoomTrapKind.PoisonMushroom);
            VerifyTree();
            VerifyGas();
            VerifyFireRemnants();
            Week25Trap0Verification.Verify();
            Debug.Log("Environment hazard artwork verification passed: 48 animation frames, fire damage stages, safe " +
                "persistent cold remnants, retracted spikes, " +
                "first-hit mushroom, burning tree, stable colliders/anchors, gas fade and expiry. Play art review pending.");
        }

        private static GameObject Trap(RoomTrapKind kind)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week25Trap0Setup.PrefabPath(kind));
            Require(prefab != null, "Missing trap prefab: " + kind);
            return Object.Instantiate(prefab);
        }

        private static void VerifySpikes()
        {
            GameObject root = Trap(RoomTrapKind.PeriodicSpikes);
            try
            {
                RoomTrap trap = root.GetComponent<RoomTrap>();
                Vector2 hitSize = trap.HitSize;
                trap.RestartCycle(0f);
                trap.Tick(0f);
                Require(trap.Visual.sprite.name.StartsWith(EnvironmentHazardArtwork.Spikes) &&
                    trap.Visual.color.a == 1f, "Lowered spikes must show the solid retracted base.");
                Sprite lowered = trap.Visual.sprite;
                root.GetComponent<FairyKingdomArtworkView>().Refresh();
                trap.Tick(1.6f);
                Require(trap.Visual.sprite == lowered && trap.Visual.color.a < 1f,
                    "Warning must keep spikes retracted with warning tint.");
                trap.Tick(2.1f);
                Require(trap.Visual.sprite.name == "trap-periodic-spikes" && trap.Visual.color == Color.white,
                    "Raised spikes must restore original artwork.");
                Require(Mathf.Abs(lowered.bounds.min.y - trap.Visual.sprite.bounds.min.y) < 0.001f &&
                    trap.HitSize == hitSize && root.GetComponentInChildren<Collider2D>() == null,
                    "Spike artwork moved baseline or changed hit area/collision.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void VerifyObstacle(RoomTrapKind kind)
        {
            GameObject root = Trap(kind);
            try
            {
                var view = root.GetComponent<FairyKingdomArtworkView>();
                var obstacle = root.GetComponent<DestructibleObstacle>();
                var collider = root.GetComponent<BoxCollider2D>();
                Vector2 size = collider.size;
                view.Refresh();
                string expected = kind == RoomTrapKind.Fire ? EnvironmentHazardArtwork.Fire : "trap-poison-mushroom";
                Require(view.Visual.sprite.name.StartsWith(expected), "Wrong initial trap art: " + kind);
                Require(obstacle.RegisterPlayerHit(), "First hit rejected.");
                view.Refresh();
                if (kind == RoomTrapKind.PoisonMushroom)
                    Require(view.Visual.sprite.name.StartsWith(EnvironmentHazardArtwork.Mushroom),
                        "First hit must show preburst mushroom.");
                else
                    Require(view.Visual.sprite.name.StartsWith(EnvironmentHazardArtwork.FireMedium),
                        "First fire hit must show medium flames.");
                Require(!obstacle.IsBroken && collider.size == size && root.transform.localScale == Vector3.one &&
                    view.Visual.color == Color.white, "Artwork changed collision or tinted authored art.");
                for (int i = 1; i < RoomTrap.RequiredHitsOf(kind); i++) obstacle.RegisterPlayerHit();
                Require(obstacle.IsBroken && !root.activeSelf, "Broken trap animation must disappear.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void VerifyTree()
        {
            GameObject root = new("Burning tree artwork verification");
            try
            {
                var stats = root.AddComponent<PlayerStats>();
                stats.AddBurnSource();
                var renderer = new GameObject("Visual").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(root.transform, false);
                var collider = root.AddComponent<BoxCollider2D>();
                var obstacle = root.AddComponent<DestructibleObstacle>();
                obstacle.Configure("tree-probe", 4, null, renderer);
                obstacle.ConfigureFixedKind("tree", Color.white, Color.gray, true, 2, Color.red);
                obstacle.BindBurnSource(stats);
                var view = root.AddComponent<FairyKingdomArtworkView>();
                view.Configure("obstacle-tree", FairyKingdomArtworkMode.Obstacle, renderer, 1f);
                view.Refresh();
                Vector3 scale = renderer.transform.localScale;
                obstacle.RegisterPlayerHit();
                view.Refresh();
                Require(renderer.sprite.name == "obstacle-tree", "Tree burned before second hit.");
                obstacle.RegisterPlayerHit();
                view.Refresh();
                Require(obstacle.IsBurning && renderer.sprite.name.StartsWith(EnvironmentHazardArtwork.Tree) &&
                    renderer.color == Color.white && renderer.transform.localScale == scale &&
                    collider.size == Vector2.one && root.transform.localScale == Vector3.one,
                    "Burning tree artwork/scale/collider mismatch.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void VerifyGas()
        {
            PoisonGasCloud gas = PoisonGasCloud.Spawn(Vector3.zero, null, null, 100f);
            try
            {
                var renderer = gas.GetComponent<SpriteRenderer>();
                Require(renderer != null && renderer.sprite.name.StartsWith(EnvironmentHazardArtwork.Gas),
                    "Gas must load selected second-version animation.");
                Sprite first = renderer.sprite;
                gas.Tick(100.2f);
                Require(renderer.sprite != first && renderer.sortingOrder == 11 && gas.EndTime == 103f,
                    "Gas did not animate or changed duration/sorting.");
                gas.Tick(102.75f);
                Require(Mathf.Abs(renderer.color.a - 0.35f) < 0.001f, "Gas fade changed.");
                gas.Tick(103f);
                Require(gas == null, "Expired gas must be removed.");
            }
            finally { if (gas != null) Object.DestroyImmediate(gas.gameObject); }
        }

        private static void VerifyFireRemnants()
        {
            GameObject room = new("Fire remnant verification");
            room.transform.position = new Vector3(-7000f, 7000f);
            try
            {
                var state = new RoomRunState("fire-remnant-room");
                GameObject root = Trap(RoomTrapKind.Fire);
                root.transform.SetParent(room.transform, false);
                var trap = root.GetComponent<RoomTrap>();
                var obstacle = root.GetComponent<DestructibleObstacle>();
                trap.ConfigurePlacement("fire-remnant-probe", false);
                obstacle.Bind(state, 123, room.transform, null);
                trap.Bind(state, null, Time.time);
                var view = root.GetComponent<FairyKingdomArtworkView>();
                obstacle.RegisterPlayerHit();
                obstacle.RegisterPlayerHit();
                view.Refresh();
                Require(view.Visual.sprite.name.StartsWith(EnvironmentHazardArtwork.FireLow),
                    "Second fire hit must show low flames.");
                float baseline = view.Visual.bounds.min.y;
                obstacle.RegisterPlayerHit();
                Require(obstacle.IsBroken && !root.activeSelf && state.IsObstacleDestroyed(trap.TrapId),
                    "Final hit must disable and persist the original fire obstacle.");
                ExtinguishedFireArtwork remnant = trap.FireRemnant;
                Require(remnant != null && remnant.transform.parent == room.transform &&
                    remnant.GetComponentInChildren<Collider2D>() == null &&
                    remnant.GetComponentInChildren<RoomTrap>() == null,
                    "Remnant must be room-owned artwork with no hazard or collider.");
                remnant.Tick(Time.time + 5f);
                Require(remnant.Visual.sprite.name == EnvironmentHazardArtwork.FireExtinguish + "-7" &&
                    remnant.Visual.sortingOrder == ExtinguishedFireArtwork.SortingOrder &&
                    Mathf.Abs(remnant.Visual.bounds.min.y - baseline) < 0.01f,
                    "Extinction must keep ground position and hold cold stones under actors.");
                var player = new GameObject("Health probe").AddComponent<Health>();
                player.transform.SetParent(room.transform, false);
                player.ResetHealth();
                float health = player.CurrentHealth;
                Require(health > 0f && !trap.TryDamagePlayer(player, Time.time + 10f) && player.CurrentHealth == health &&
                    !trap.TryExtinguish() && !obstacle.RegisterPlayerHit(),
                    "Broken fire must reject damage and repeated destruction.");
                trap.Bind(state, null, Time.time);
                Require(room.GetComponentsInChildren<ExtinguishedFireArtwork>(true).Length == 1,
                    "Rebinding must not duplicate remnants.");
                room.SetActive(false);
                room.SetActive(true);
                remnant.Tick(Time.time + 5f);
                Require(!root.activeSelf && remnant.Visual.sprite.name.EndsWith("-7"),
                    "Returning to a room must not relight a fire.");
                GameObject rebuilt = Trap(RoomTrapKind.Fire);
                rebuilt.transform.SetParent(room.transform, false);
                var restored = rebuilt.GetComponent<RoomTrap>();
                restored.ConfigurePlacement("fire-remnant-probe", false);
                rebuilt.GetComponent<DestructibleObstacle>().Bind(state, 123, room.transform, null);
                Require(restored.TryValidate(out string error), "Restored trap validation failed: " + error);
                restored.Bind(state, null, Time.time);
                Require(!rebuilt.activeSelf && restored.FireRemnant != null &&
                    restored.FireRemnant.Visual.sprite.name.EndsWith("-7"),
                    "Rebuilt room must restore the cold final frame immediately.");

                foreach (bool water in new[] { true, false })
                {
                    GameObject fresh = Trap(RoomTrapKind.Fire);
                    fresh.transform.SetParent(room.transform, false);
                    var freshTrap = fresh.GetComponent<RoomTrap>();
                    freshTrap.Bind(null, null, Time.time);
                    bool broken = water ? freshTrap.TryExtinguish() :
                        fresh.GetComponent<DestructibleObstacle>().TryDestroyByBomb();
                    Require(broken && !fresh.activeSelf && freshTrap.FireRemnant != null,
                        "Water and bomb destruction must also leave safe remnants.");
                }
                // Room rebuild destroys the shared content parent. This also works in Edit Mode, where
                // OnDestroy on a never-played inactive component is not a supported lifecycle simulation.
                Object.DestroyImmediate(room);
                Require(remnant == null, "Removing room content must also remove its remnants.");
            }
            finally { if (room != null) Object.DestroyImmediate(room); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
