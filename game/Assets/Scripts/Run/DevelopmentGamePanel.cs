using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
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
        private Vector2 scrollPosition;
        private ItemDefinition[] singleUseItems;

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
            if (invulnerable) MarkAssisted("Invulnerable");
            if (revealSecrets) MarkAssisted("Reveal secrets");
            if (DestructibleObstacle.DevelopmentForceNextSecretPit) MarkAssisted("Forced pit");

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
            if (!visible) return;
            if (assembler == null || assembler.Progress == null)
            {
                if (string.IsNullOrEmpty(RunSession.LastDevelopmentPlaytestReport)) return;
                GUILayout.BeginArea(new Rect(12f, 60f, 340f, 160f), GUI.skin.box);
                GUILayout.Label(RunSession.LastDevelopmentPlaytestReport);
                if (GUILayout.Button("Copy last Play-1 record"))
                    GUIUtility.systemCopyBuffer = RunSession.LastDevelopmentPlaytestReport;
                GUILayout.EndArea();
                return;
            }

            RunProgress progress = assembler.Progress;
            GUILayout.BeginArea(new Rect(12f, 60f, 340f, Screen.height - 72f), GUI.skin.box);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label($"DEV PANEL (F1)   seed {(progress.HasRunSeed ? progress.RunSeed.ToString() : "-")}");
            GUILayout.Label($"Floor {progress.CurrentFloor} Room {progress.CurrentRoom}");
            DrawPlaytestSection();

            DrawSeedSection(assembler);
            DrawPlayerSection(assembler);
            DrawSpellSlotSection(assembler);
            DrawResourceSection(progress);
            DrawRoomSection(assembler);
            DrawSecretSection(assembler);
            DrawShopSection(assembler);

            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPlaytestSection()
        {
            DevelopmentPlaytestRecord record = FindFirstObjectByType<RunSession>()?.DevelopmentPlaytest;
            if (record == null) return;
            GUILayout.Label("— Play-1: real time, includes pauses —");
            GUILayout.Label(string.Join("  ", Enumerable.Range(1, 3).Select(floor =>
                $"F{floor} {record.GetFloorSeconds(floor, Time.realtimeSinceStartupAsDouble):F1}s")));
            GUILayout.Label(record.IsAssisted ? "Assisted run" : "No test assistance recorded");
            if (GUILayout.Button("Copy Play-1 record"))
                GUIUtility.systemCopyBuffer = record.Format(Time.realtimeSinceStartupAsDouble);
        }

        private static void MarkAssisted(string action) =>
            FindFirstObjectByType<RunSession>()?.DevelopmentPlaytest?.MarkAssisted(action);

        private static bool AssistedButton(string label)
        {
            if (!GUILayout.Button(label)) return false;
            MarkAssisted(label);
            return true;
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
            if (AssistedButton("Full HP") && health != null)
                status = $"Healed {health.Heal(health.MaxHealth)}.";
            if (AssistedButton("Full SP") && sp != null)
                status = sp.TryAdd(sp.MaxSP - sp.CurrentSP) ? "SP filled." : "SP already full.";
            GUILayout.EndHorizontal();
            bool nextInvulnerable = GUILayout.Toggle(invulnerable, "Invulnerable");
            if (nextInvulnerable != invulnerable)
            {
                invulnerable = nextInvulnerable;
                if (invulnerable) MarkAssisted("Invulnerable");
                if (!invulnerable) health?.SetInvulnerable(false);
            }
        }

        // Drops a single-use item next to the player, so it is picked up through the normal slot path.
        private void DrawSpellSlotSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Spell slot —");
            PlayerSpellSlot slot = assembler.Graph?.Player != null
                ? assembler.Graph.Player.GetComponent<PlayerSpellSlot>()
                : null;
            if (slot == null || slot.PickupPrefab == null)
            {
                GUILayout.Label("The player has no spell slot.");
                return;
            }

            PlayerSingleUseEffects effects = slot.GetComponent<PlayerSingleUseEffects>();
            GUILayout.Label($"Held: {(slot.HasItem ? slot.HeldDefinition.DisplayName : "-")}" +
                            (effects != null && effects.IsRegenerating
                                ? $"   SP regen {effects.RegenerationRemainingSeconds:F1}s"
                                : string.Empty) +
                            (effects != null && effects.RoomAttackDamagePercent > 0f
                                ? $"   Room ATK +{effects.RoomAttackDamagePercent:P0}"
                                : string.Empty) +
                            (effects != null && effects.RoomAttackSpeedPercent > 0f
                                ? $"   Room ASPD +{effects.RoomAttackSpeedPercent:P0} " +
                                  $"MS +{effects.RoomMoveSpeedPercent:P0}"
                                : string.Empty));
#if UNITY_EDITOR
            singleUseItems ??= UnityEditor.AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null && item.IsSingleUse && item.IsValid)
                .OrderBy(item => item.ItemId, System.StringComparer.Ordinal)
                .ToArray();
            foreach (ItemDefinition item in singleUseItems)
            {
                if (!AssistedButton($"Drop {item.DisplayName}")) continue;
                RoomPrefab room = CurrentRoom(assembler);
                SingleUseItemPickup pickup = Instantiate(slot.PickupPrefab,
                    slot.transform.position + Vector3.right * 1.2f, Quaternion.identity,
                    room != null ? room.transform : null);
                pickup.name = $"Single Use Item ({item.ItemId})";
                pickup.Configure(item, null, false);
                status = $"Dropped {item.DisplayName} next to the player.";
            }
#else
            GUILayout.Label("Single-use drops are editor-only.");
#endif
        }

        private void DrawResourceSection(RunProgress progress)
        {
            GUILayout.Label($"— Resources — bomb {progress.GetResourceCount(RunResourceType.Bomb)}  " +
                            $"key {progress.GetResourceCount(RunResourceType.Key)}  " +
                            $"gold {progress.GetResourceCount(RunResourceType.Gold)}");
            GUILayout.BeginHorizontal();
            if (AssistedButton("+10 Bomb")) progress.TryAddResource(RunResourceType.Bomb, 10);
            if (AssistedButton("+10 Key")) progress.TryAddResource(RunResourceType.Key, 10);
            if (AssistedButton("+10 Gold")) progress.TryAddResource(RunResourceType.Gold, 10);
            GUILayout.EndHorizontal();
        }

        private void DrawRoomSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Room —");
            RoomController controller = CurrentRoom(assembler)?.Controller;
            if (AssistedButton("Kill current wave") && controller != null)
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
                if (revealSecrets) MarkAssisted("Reveal secrets");
                GameMinimapView.DevelopmentRevealSecrets = revealSecrets;
                FindFirstObjectByType<GameMinimapView>()?.RefreshNow();
                ApplyWallHighlight(revealSecrets);
            }

            DestructibleObstacle.DevelopmentForceNextSecretPit = GUILayout.Toggle(
                DestructibleObstacle.DevelopmentForceNextSecretPit, "Next broken obstacle drops a pit");
            if (DestructibleObstacle.DevelopmentForceNextSecretPit) MarkAssisted("Forced pit");
            if (secret == null) return;

            GUILayout.BeginHorizontal();
            if (AssistedButton("Go to secret neighbor"))
                Teleport(assembler, secret.DirectionalConnections[0].DestinationRoomId);
            if (AssistedButton("Go to secret room")) Teleport(assembler, secret.RoomId);
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
            if (AssistedButton("Go to shop door"))
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
