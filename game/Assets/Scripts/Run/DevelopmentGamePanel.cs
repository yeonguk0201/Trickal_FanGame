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
        private ItemDefinition[] artifactItems;
        private bool showArtifacts;

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
            if (PlayerStats.DevelopmentForceBurnSource) MarkAssisted("Forced burn source");

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
            DrawArtifactSection(assembler);
            DrawResourceSection(progress);
            DrawChestSection(assembler);
            DrawObstacleSection(assembler);
            DrawRoomSection(assembler);
            DrawBossSection(assembler);
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

        private static readonly float[] SizeOptions = { 0.5f, 1f, 1.5f, 2f, 3f };

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
            if (GUILayout.Button("Find secret F1"))
                FindSeed(assembler, node => node.Role == GeneratedRoomRole.Secret, "Secret room");
            if (GUILayout.Button("Find shop F1"))
                FindSeed(assembler, node => node.Role == GeneratedRoomRole.Shop, "Shop room");
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Find Goldi shop F1")) FindSeed(assembler, node => node.IsGoldiShop, "Goldi shop");
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

            // Hitbox-1: the look follows the size, the hurtbox stops at its limit and the feet never change.
            PlayerBodySize size = assembler.Graph?.Player != null
                ? assembler.Graph.Player.GetComponent<PlayerBodySize>()
                : null;
            if (size == null) return;
            GUILayout.Label($"Size x{size.SizeMultiplier:0.##}   hurtbox r {size.HurtboxRadius:0.##}   " +
                            $"feet r {PlayerFeet.Radius:0.##}");
            GUILayout.BeginHorizontal();
            foreach (float option in SizeOptions)
            {
                if (AssistedButton($"x{option:0.##}") && size.SetSizeMultiplier(option))
                    status = $"Player size x{size.SizeMultiplier:0.##}.";
            }

            GUILayout.EndHorizontal();
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
                            (effects != null && (effects.IsDamageRecoveryActive || effects.PendingRecoveryCount > 0)
                                ? $"   Fruit {effects.DamageRecoveryRemainingSeconds:F1}s " +
                                  $"heals {effects.PendingRecoveryCount}"
                                : string.Empty) +
                            (effects != null && effects.RoomAttackDamagePercent > 0f
                                ? $"   Room ATK +{effects.RoomAttackDamagePercent:P0}"
                                : string.Empty) +
                            (effects != null && effects.RoomCriticalDamagePercent > 0f
                                ? $"   Room CRIT +{effects.RoomCriticalChancePercent:P0} " +
                                  $"DMG +{effects.RoomCriticalDamagePercent:P0}"
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

        // Drops an artifact next to the player as a normal floor pickup, so it is acquired through the usual path
        // (stack limit, Run record, HUD). The list collects every active artifact asset, so a new artifact appears
        // here without panel changes. Golden chest exclusives keep their own buttons in the chest section.
        private void DrawArtifactSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Artifact —");
            ChestContentTable table = assembler.ChestContentTable;
            Transform player = assembler.Graph?.Player != null ? assembler.Graph.Player.transform : null;
            DrawArtifactStateRows(player);
            if (table == null || table.ArtifactPickupPrefab == null || player == null)
            {
                GUILayout.Label("No artifact pickup Prefab or player.");
                return;
            }

#if UNITY_EDITOR
            artifactItems ??= UnityEditor.AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null && item.Kind == ItemKind.Artifact && item.IsActive && item.IsValid)
                .OrderBy(item => item.ItemId, System.StringComparer.Ordinal)
                .ToArray();
            showArtifacts = GUILayout.Toggle(showArtifacts, $"Show artifact drops ({artifactItems.Length})");
            if (!showArtifacts) return;

            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            foreach (ItemDefinition item in artifactItems)
            {
                if (table.GoldenExclusiveArtifacts.Contains(item)) continue;
                int stacks = inventory != null ? inventory.GetStackCount(item.ItemId) : 0;
                string limit = item.MaxStacks > 0 ? item.MaxStacks.ToString() : "-";
                if (!AssistedButton($"Drop {item.DisplayName} ({stacks}/{limit})")) continue;
                RoomPrefab room = CurrentRoom(assembler);
                ItemPickup pickup = Instantiate(table.ArtifactPickupPrefab,
                    player.position + Vector3.right * 1.2f, Quaternion.identity,
                    room != null ? room.transform : null);
                pickup.name = $"Item Pickup ({item.ItemId})";
                pickup.Configure(item);
                status = $"Dropped {item.DisplayName} next to the player.";
            }
#else
            GUILayout.Label("Artifact drops are editor-only.");
#endif
        }

        // Artifact-2: the state of artifacts whose effect is not visible on the HUD, and the status effects on
        // the enemy nearest to the player.
        private static void DrawArtifactStateRows(Transform player)
        {
            if (player == null) return;
            Health health = player.GetComponent<Health>();
            PlayerKillFrenzy frenzy = player.GetComponent<PlayerKillFrenzy>();
            PlayerDeathWard ward = player.GetComponent<PlayerDeathWard>();
            PlayerBasicAttackExplosion explosion = player.GetComponent<PlayerBasicAttackExplosion>();
            PlayerSkillCastHeal castHeal = player.GetComponent<PlayerSkillCastHeal>();
            if (health != null)
                GUILayout.Label($"HP {HealthUnits.FormatHearts(health.CurrentHealth)}  shield " +
                                $"{HealthUnits.FormatHearts(health.CurrentShield)}" +
                                (health.IsInvulnerable ? "  INVULNERABLE" : string.Empty));
            if (frenzy != null)
                GUILayout.Label($"Glove stacks {frenzy.Stacks}  " +
                                (frenzy.Stacks > 0
                                    ? $"ends in {Mathf.Max(0f, frenzy.EndTime - Time.time):F1}s"
                                    : $"cooldown {Mathf.Max(0f, frenzy.CooldownEndTime - Time.time):F1}s"));
            if (ward != null) GUILayout.Label($"Dagger ward {(ward.IsSpent ? "spent" : "ready")}");
            if (explosion != null) GUILayout.Label($"Muffin hits {explosion.HitProgress}  explosions {explosion.TriggerCount}");
            if (castHeal != null)
                GUILayout.Label($"Flag casts {castHeal.CastProgress}/{castHeal.RequiredCastCount}");

            EnemyStatusEffects nearest = FindObjectsByType<EnemyStatusEffects>(FindObjectsSortMode.None)
                .Where(status => status.IsBurning || status.IsPoisoned || status.IsShocked)
                .OrderBy(status => (status.transform.position - player.position).sqrMagnitude)
                .FirstOrDefault();
            if (nearest != null)
                GUILayout.Label($"Nearest status: {nearest.name}" +
                                (nearest.IsBurning ? "  burn" : string.Empty) +
                                (nearest.IsPoisoned ? $"  poison x{nearest.PoisonStacks}" : string.Empty) +
                                (nearest.IsShocked
                                    ? $"  shock x{nearest.ShockStacks} (speed x{nearest.MoveSpeedMultiplier:F2})"
                                    : string.Empty));
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

        // Chest-1: forces a chest of each kind into the current room through the normal chest path (assisted Run).
        private void DrawChestSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Chest —");
            if (assembler.ChestContentTable == null)
            {
                GUILayout.Label("No chest content table is configured.");
                return;
            }

            GUILayout.BeginHorizontal();
            foreach (ChestKind kind in new[] { ChestKind.Normal, ChestKind.Golden, ChestKind.Diamond })
            {
                if (!AssistedButton($"Chest {kind}")) continue;
                status = TrySpawnChest(assembler, kind, out string message) ? message : $"Chest failed: {message}";
            }

            GUILayout.EndHorizontal();

            // Flight-0: golden chest exclusive artifacts dropped directly as a normal floor pickup, without waiting
            // for a golden chest's 25% special reward (Chest-2).
            ChestContentTable table = assembler.ChestContentTable;
            PlayerFlight flight = assembler.Graph?.Player != null
                ? assembler.Graph.Player.GetComponent<PlayerFlight>()
                : null;
            GUILayout.Label($"Golden exclusive   flying {(flight != null && flight.IsFlying ? "yes" : "no")}");
            if (table.ArtifactPickupPrefab == null || assembler.Graph?.Player == null) return;
            foreach (ItemDefinition artifact in table.GoldenExclusiveArtifacts)
            {
                if (artifact == null || !AssistedButton($"Drop {artifact.DisplayName}")) continue;
                RoomPrefab room = CurrentRoom(assembler);
                ItemPickup pickup = Instantiate(table.ArtifactPickupPrefab,
                    assembler.Graph.Player.transform.position + Vector3.right * 1.2f, Quaternion.identity,
                    room != null ? room.transform : null);
                pickup.name = $"Item Pickup ({artifact.ItemId})";
                pickup.Configure(artifact);
                status = $"Dropped {artifact.DisplayName} next to the player.";
            }
        }

        // Obstacle-5: turns the unbroken candidate obstacle nearest to the player into a chosen kind, because each
        // special kind is rare in a normal Run (assisted Run). A rebuilt room shows its seeded kind again.
        private void DrawObstacleSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Obstacle —");
            RoomPrefab room = CurrentRoom(assembler);
            Transform player = assembler.Graph?.Player != null ? assembler.Graph.Player.transform : null;
            DrawTreeBurnRows(assembler, room, player);
            RoomObstacleVariantSlot nearest = room == null || player == null
                ? null
                : room.GetComponentsInChildren<RoomObstacleVariantSlot>(false)
                    .Where(slot => slot.VariantTable != null && !slot.GetComponent<DestructibleObstacle>().IsBroken)
                    .OrderBy(slot => (slot.transform.position - player.position).sqrMagnitude)
                    .FirstOrDefault();
            if (nearest == null)
            {
                GUILayout.Label("No unbroken obstacle slot in this room.");
                return;
            }

            DestructibleObstacle obstacle = nearest.GetComponent<DestructibleObstacle>();
            GUILayout.Label($"Nearest {obstacle.ObstacleId}: {obstacle.VariantId}");
            // Enemy-6: the random box releases its 쥬비 on the next break instead of its seeded outcome.
            DestructibleObstacle.DevelopmentForceNextEnemies = GUILayout.Toggle(
                DestructibleObstacle.DevelopmentForceNextEnemies, "Next broken random box releases enemies");
            if (DestructibleObstacle.DevelopmentForceNextEnemies) MarkAssisted("Forced enemies");
            foreach (ObstacleVariantEntry entry in nearest.VariantTable.Entries)
            {
                if (entry?.Variant == null || !AssistedButton($"Make {entry.Variant.VariantId}")) continue;
                obstacle.ApplyVariant(entry.Variant);
                status = $"{obstacle.ObstacleId} is now {entry.Variant.VariantId}.";
            }
        }

        // Obstacle-7: the toggle stands in for holding a burn artifact (활활 불타활, 불타는 가지) (assisted Run).
        private static void DrawTreeBurnRows(RoomGraphAssembler assembler, RoomPrefab room, Transform player)
        {
            PlayerStats.DevelopmentForceBurnSource = GUILayout.Toggle(
                PlayerStats.DevelopmentForceBurnSource, "Hold a burn artifact (trees catch fire)");
            DestructibleObstacle tree = room == null || player == null
                ? null
                : room.GetComponentsInChildren<DestructibleObstacle>(false)
                    .Where(obstacle => obstacle.BurnHits > 0 && !obstacle.IsBroken)
                    .OrderBy(obstacle => (obstacle.transform.position - player.position).sqrMagnitude)
                    .FirstOrDefault();
            GUILayout.Label((tree != null
                                ? $"Nearest tree {tree.ObstacleId}: {tree.HitsTaken}/{tree.RequiredHits} hits" +
                                  (tree.IsBurning ? " BURNING" : string.Empty)
                                : "No unbroken tree in this room.") +
                            $"   burned this Run {assembler.Progress.BurnedObstacleCount}");
        }

        private static bool TrySpawnChest(RoomGraphAssembler assembler, ChestKind kind, out string message)
        {
            RoomPrefab room = CurrentRoom(assembler);
            GeneratedRoomNode node = room != null
                ? assembler.GeneratedGraph?.FindFloor(room.Node.FloorNumber)?.Nodes
                    .FirstOrDefault(candidate => candidate.RoomId == room.Node.RoomId)
                : null;
            RoomRunState state = room != null ? assembler.Progress.GetRoomState(room.Node.RoomId) : null;
            if (node?.Template == null || state == null)
            {
                message = "The current room has no generated Template or Run state.";
                return false;
            }

            TreasureChest chest = RoomChestSpawner.SpawnDevelopmentChest(assembler.ChestContentTable,
                new RoomChestSite(room, node.Template, state, assembler.Progress,
                    assembler.Graph.Player != null ? assembler.Graph.Player.transform : null), node.ContentSeed, kind,
                out string error);
            message = chest != null ? $"Placed {kind} chest {chest.ChestId}." : error;
            return chest != null;
        }

        private void DrawRoomSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Room —");
            RoomController controller = CurrentRoom(assembler)?.Controller;
            if (AssistedButton("Kill current wave") && controller != null)
                status = $"Killed {controller.KillAliveEnemiesForDevelopment()} enemies.";
        }

        private void DrawBossSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Boss room —");
            if (assembler.GeneratedGraph == null)
            {
                GUILayout.Label("No generated floors.");
                return;
            }

            GUILayout.BeginHorizontal();
            foreach (GeneratedFloor floor in assembler.GeneratedGraph.Floors)
            {
                if (AssistedButton($"Boss F{floor.FloorNumber}"))
                    TryGoToBossRoom(assembler, floor.FloorNumber, out status);
            }

            GUILayout.EndHorizontal();
        }

        // Jumps to a floor's boss room. Another floor is loaded the way its portal loads it and the room change is
        // the normal teleport, so floor replacement, transition guards and boss spawning stay the same as in play.
        public static bool TryGoToBossRoom(RoomGraphAssembler assembler, int floorNumber, out string message)
        {
            RoomGraphController graph = assembler != null ? assembler.Graph : null;
            GeneratedFloor floor = assembler?.GeneratedGraph?.FindFloor(floorNumber);
            RoomPrefab current = assembler != null ? CurrentRoom(assembler) : null;
            if (graph == null || floor == null || current == null || graph.Player == null)
            {
                message = $"Floor {floorNumber} or the current room is missing.";
                return false;
            }

            if (current.Controller != null && current.Controller.State == RoomState.Combat)
            {
                message = "Finish or kill the current wave before moving.";
                return false;
            }

            if (current.Node.RoomId == floor.BossRoomId)
            {
                message = $"Already in the floor {floorNumber} boss room.";
                return false;
            }

            if (assembler.Progress.CurrentFloor != floorNumber &&
                !assembler.TryLoadFloor(floorNumber, graph.Player, out string error))
            {
                message = $"Could not load floor {floorNumber}. {error}";
                return false;
            }

            RoomNode bossRoom = graph.Nodes.FirstOrDefault(node => node.RoomId == floor.BossRoomId);
            if (bossRoom == null ||
                !graph.TryTeleport(graph.CurrentNode, bossRoom, bossRoom.InitialSpawnPosition, graph.Player))
            {
                message = $"Loaded floor {floorNumber}, but the boss room teleport was rejected " +
                          "(cooldown, reward selection, or action state).";
                return false;
            }

            message = $"Moved to the floor {floorNumber} boss room {floor.BossRoomId}.";
            return true;
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
                                $"{(state?.IsSecretDiscovered == true ? "discovered" : "hidden")}" +
                                (secret.IsGoldiShop ? ", Goldi shop" : string.Empty));
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
            // Shop-1: the 골디 shop is the floor's secret room; reach it with the secret room buttons above too.
            GeneratedRoomNode goldi = floor?.Nodes.FirstOrDefault(node => node.IsGoldiShop);
            if (goldi == null)
            {
                GUILayout.Label($"This floor has no Goldi shop ({FloorGenerator.GoldiShopPercent}% of secret rooms).");
            }
            else
            {
                GUILayout.Label($"Goldi shop in secret room {goldi.RoomId}");
                if (AssistedButton("Go to Goldi shop")) Teleport(assembler, goldi.RoomId);
            }

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

        private void FindSeed(RoomGraphAssembler assembler, System.Func<GeneratedRoomNode, bool> match, string label)
        {
            if (assembler.Generator == null) return;
            int start = int.TryParse(seedText, out int typed) ? typed + 1 :
                assembler.Progress.HasRunSeed ? assembler.Progress.RunSeed + 1 : 1;
            for (int offset = 0; offset < SeedSearchLimit; offset++)
            {
                int seed = unchecked(start + offset);
                if (!assembler.Generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                if (graph.FindFloor(1)?.Nodes.Any(match) != true) continue;
                seedText = seed.ToString();
                status = $"Seed {seed} has a floor-1 {label}.";
                return;
            }

            status = $"No {label} seed found nearby.";
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
