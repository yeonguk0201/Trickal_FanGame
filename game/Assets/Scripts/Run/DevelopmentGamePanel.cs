using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Run
{
    // Development-only test panel for the Game Scene (F1). Every action goes through the same runtime paths as play
    // (resource wallet, health, room death handling, room teleport), so it does not bypass state or reward rules.
    public sealed class DevelopmentGamePanel : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int SeedSearchLimit = 4096;
        private const float HighlightRefreshInterval = 0.5f;

        private bool visible;
        private bool invulnerable;
        private bool revealSecrets;
        private string seedText = string.Empty;
        private string status = string.Empty;
        private float nextHighlightRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForDevelopment()
        {
            if (FindFirstObjectByType<DevelopmentGamePanel>() != null) return;
            GameObject owner = new(nameof(DevelopmentGamePanel));
            DontDestroyOnLoad(owner);
            owner.AddComponent<DevelopmentGamePanel>();
        }

        private void Update()
        {
            if (Keyboard.current?.f1Key.wasPressedThisFrame == true) visible = !visible;
            RoomGraphAssembler assembler = FindFirstObjectByType<RoomGraphAssembler>();
            if (assembler == null) return;

            // Other systems clear explicit invulnerability (reset, ultimate end), so the toggle is re-applied.
            if (invulnerable) PlayerHealth(assembler)?.SetInvulnerable(true);
            if (revealSecrets && Time.unscaledTime >= nextHighlightRefresh)
            {
                nextHighlightRefresh = Time.unscaledTime + HighlightRefreshInterval;
                ApplyWallHighlight(true);
            }
        }

        private void OnGUI()
        {
            RoomGraphAssembler assembler = FindFirstObjectByType<RoomGraphAssembler>();
            if (!visible || assembler == null || assembler.Progress == null) return;

            RunProgress progress = assembler.Progress;
            GUILayout.BeginArea(new Rect(12f, 60f, 340f, Screen.height - 72f), GUI.skin.box);
            GUILayout.Label($"DEV PANEL (F1)   seed {(progress.HasRunSeed ? progress.RunSeed.ToString() : "-")}");
            GUILayout.Label($"Floor {progress.CurrentFloor} Room {progress.CurrentRoom}");

            DrawSeedSection(assembler);
            DrawPlayerSection(assembler);
            DrawResourceSection(progress);
            DrawRoomSection(assembler);
            DrawSecretSection(assembler);
            DrawShopSection(assembler);

            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);
            GUILayout.EndArea();
        }

        private void DrawSeedSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Seed —");
            GUILayout.BeginHorizontal();
            seedText = GUILayout.TextField(seedText, GUILayout.Width(150f));
            if (GUILayout.Button("Find secret F1")) FindSeed(assembler, GeneratedRoomRole.Secret);
            if (GUILayout.Button("Find shop F1")) FindSeed(assembler, GeneratedRoomRole.Shop);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Restart Run with this seed")) RestartWithSeed();
        }

        private void DrawPlayerSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Player —");
            Health health = PlayerHealth(assembler);
            PlayerSP sp = assembler.Graph?.Player != null ? assembler.Graph.Player.GetComponent<PlayerSP>() : null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Full HP") && health != null)
                status = $"Healed {health.Heal(health.MaxHealth)}.";
            if (GUILayout.Button("Full SP") && sp != null)
                status = sp.TryAdd(sp.MaxSP - sp.CurrentSP) ? "SP filled." : "SP already full.";
            GUILayout.EndHorizontal();
            bool nextInvulnerable = GUILayout.Toggle(invulnerable, "Invulnerable");
            if (nextInvulnerable != invulnerable)
            {
                invulnerable = nextInvulnerable;
                if (!invulnerable) health?.SetInvulnerable(false);
            }
        }

        private void DrawResourceSection(RunProgress progress)
        {
            GUILayout.Label($"— Resources — bomb {progress.GetResourceCount(RunResourceType.Bomb)}  " +
                            $"key {progress.GetResourceCount(RunResourceType.Key)}  " +
                            $"elif {progress.GetResourceCount(RunResourceType.Elif)}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10 Bomb")) progress.TryAddResource(RunResourceType.Bomb, 10);
            if (GUILayout.Button("+10 Key")) progress.TryAddResource(RunResourceType.Key, 10);
            if (GUILayout.Button("+10 Elif")) progress.TryAddResource(RunResourceType.Elif, 10);
            GUILayout.EndHorizontal();
        }

        private void DrawRoomSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Room —");
            RoomController controller = CurrentRoom(assembler)?.Controller;
            if (GUILayout.Button("Kill current wave") && controller != null)
                status = $"Killed {controller.KillAliveEnemiesForDevelopment()} enemies.";
        }

        private void DrawSecretSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Secret room —");
            GeneratedFloor floor = assembler.GeneratedGraph?.FindFloor(assembler.Progress.CurrentFloor);
            GeneratedRoomNode secret = floor?.Nodes.FirstOrDefault(node => node.Role == GeneratedRoomRole.Secret);
            if (secret == null)
            {
                GUILayout.Label("This floor has no secret room.");
            }
            else
            {
                RoomRunState state = assembler.Progress.GetRoomState(secret.RoomId);
                GUILayout.Label($"{secret.RoomId} at {secret.GridPosition}, " +
                                $"{(state?.IsSecretDiscovered == true ? "discovered" : "hidden")}");
                GUILayout.Label($"Walls in: {string.Join(", ", secret.ConnectedRoomIds)}");
            }

            bool nextReveal = GUILayout.Toggle(revealSecrets, "Show secret room and walls");
            if (nextReveal != revealSecrets)
            {
                revealSecrets = nextReveal;
                GameMinimapView.DevelopmentRevealSecrets = revealSecrets;
                FindFirstObjectByType<GameMinimapView>()?.RefreshNow();
                ApplyWallHighlight(revealSecrets);
            }

            DestructibleObstacle.DevelopmentForceNextSecretPit = GUILayout.Toggle(
                DestructibleObstacle.DevelopmentForceNextSecretPit, "Next broken obstacle drops a pit");
            if (secret == null) return;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Go to secret neighbor"))
                Teleport(assembler, secret.DirectionalConnections[0].DestinationRoomId);
            if (GUILayout.Button("Go to secret room")) Teleport(assembler, secret.RoomId);
            GUILayout.EndHorizontal();
        }

        private void DrawShopSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Shop —");
            GeneratedFloor floor = assembler.GeneratedGraph?.FindFloor(assembler.Progress.CurrentFloor);
            GeneratedRoomNode shop = floor?.Nodes.FirstOrDefault(node => node.Role == GeneratedRoomRole.Shop);
            if (shop == null)
            {
                GUILayout.Label("This floor has no shop.");
                return;
            }

            RoomRunState state = assembler.Progress.GetRoomState(shop.RoomId);
            GUILayout.Label($"{shop.RoomId} at {shop.GridPosition}, {(state?.IsKeyLockOpen == true ? "unlocked" : "locked")}");
            if (GUILayout.Button("Go to shop door"))
                Teleport(assembler, shop.DirectionalConnections[0].DestinationRoomId);
        }

        private void FindSeed(RoomGraphAssembler assembler, GeneratedRoomRole role)
        {
            if (assembler.Generator == null) return;
            int start = int.TryParse(seedText, out int typed) ? typed + 1 :
                assembler.Progress.HasRunSeed ? assembler.Progress.RunSeed + 1 : 1;
            for (int offset = 0; offset < SeedSearchLimit; offset++)
            {
                int seed = unchecked(start + offset);
                if (!assembler.Generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                if (graph.FindFloor(1)?.Nodes.Any(node => node.Role == role) != true) continue;
                seedText = seed.ToString();
                status = $"Seed {seed} has a floor-1 {role} room.";
                return;
            }

            status = $"No {role} room seed found nearby.";
        }

        private void RestartWithSeed()
        {
            if (!int.TryParse(seedText, out int seed))
            {
                status = "Enter a numeric seed first.";
                return;
            }

            RunSession.DevelopmentSeedOverride = seed;
            RunSession session = FindFirstObjectByType<RunSession>();
            if (session != null && session.TryRestartRun()) return;

            // A Game Scene played directly has no Frontend launch identity, so reload it as-is.
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
        }

        private void Teleport(RoomGraphAssembler assembler, string roomId)
        {
            RoomGraphController graph = assembler.Graph;
            RoomPrefab current = CurrentRoom(assembler);
            RoomNode target = graph?.Nodes.FirstOrDefault(node => node.RoomId == roomId);
            if (current == null || target == null || graph.Player == null)
            {
                status = "Teleport target is missing.";
                return;
            }

            if (current.Controller != null && current.Controller.State == RoomState.Combat)
            {
                status = "Finish or kill the current wave before moving.";
                return;
            }

            status = graph.TryTeleport(current.Node, target, target.InitialSpawnPosition, graph.Player)
                ? $"Moved to {roomId}."
                : "Teleport was rejected (cooldown, reward selection, or action state).";
        }

        private static void ApplyWallHighlight(bool highlighted)
        {
            foreach (SecretPassageWall wall in FindObjectsByType<SecretPassageWall>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                wall.SetDevelopmentHighlight(highlighted && !wall.IsOpen);
        }

        private static RoomPrefab CurrentRoom(RoomGraphAssembler assembler) =>
            assembler.Graph?.CurrentNode != null ? assembler.Graph.CurrentNode.GetComponent<RoomPrefab>() : null;

        private static Health PlayerHealth(RoomGraphAssembler assembler) =>
            assembler.Graph?.Player != null ? assembler.Graph.Player.GetComponent<Health>() : null;
#endif
    }
}
