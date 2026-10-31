using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
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
        private ItemDefinition[] powerItems;
        private bool showArtifacts;
        private bool showLayouts;
        private RoomTrap[] trapPrefabs;
        private bool trapSecondPhase;
        private int placedTrapCount;

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
            if (!string.IsNullOrEmpty(RoomTemplateSelector.DevelopmentForcedTemplateId)) MarkAssisted("Forced layout");

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
            DrawPowerSection(assembler);
            DrawArtifactSection(assembler);
            DrawResourceSection(progress);
            DrawElifSection(assembler, progress);
            DrawChestSection(assembler);
            DrawObstacleSection(assembler);
            DrawTrapSection(assembler);
            DrawRoomSection(assembler);
            DrawLayoutSection(assembler);
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
            // Enemy-7: the 쥬비 only Encounter is one of several Easy candidates, so a seed is searched for it.
            if (GUILayout.Button("Find Jyubi swarm F1"))
                FindSeed(assembler, node => node.EncounterId != null &&
                                            node.EncounterId.EndsWith("-jyubi-swarm-v1", System.StringComparison.Ordinal),
                    "Jyubi swarm room");
            // Enemy-8/9/10: each searches the floor its enemy first appears on (Curve-1 EncounterEnemyFloorPool), so
            // a button never looks at a floor the enemy cannot reach.
            FindEnemySeedButton(assembler, EncounterEnemyRole.FourWayShooter, "lively fairy", "Lively fairy room");
            FindEnemySeedButton(assembler, EncounterEnemyRole.SplittingChaser, "nururing", "Nururing room");
            FindEnemySeedButton(assembler, EncounterEnemyRole.SelfDestruct, "self-destruct", "Self-destruct room");
            // Enemy-11: 대왕쥬비 only comes in the floor-3 jyubi-nest Encounter.
            FindEnemySeedButton(assembler, EncounterEnemyRole.JyubiKing, "Jyubi king", "Jyubi nest room");
            // Enemy-12: 곡사형 only comes in crossfire's rear slots from floor 3.
            FindEnemySeedButton(assembler, EncounterEnemyRole.Lobber, "lobber", "Lobber room");
            // Enemy-13: 잠복형 only comes in pressure's melee slots from floor 3.
            FindEnemySeedButton(assembler, EncounterEnemyRole.Ambusher, "ambusher", "Ambusher room");
            // Variant-0: elites start on floor 3 (20% of combat rooms).
            if (GUILayout.Button("Find elite F3"))
                FindSeed(assembler, node => EnemyVariantRules.CountElites(node.ResolvedEncounterWaves) > 0,
                    "elite room", 3);
            // Variant-1: champions start on floor 2 (10% of eligible enemies, 15% on floor 3).
            if (GUILayout.Button("Find champion F2"))
                FindSeed(assembler, node => EnemyVariantRules.CountChampions(node.ResolvedEncounterWaves) > 0,
                    "champion room", 2);
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

            // Every direct hit becomes critical, to see the critical hit stop and camera shake.
            PlayerStats stats = assembler.Graph?.Player != null
                ? assembler.Graph.Player.GetComponent<PlayerStats>()
                : null;
            if (stats != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"CRIT {stats.CriticalChance:P0}");
                if (AssistedButton("CRIT 100%"))
                {
                    stats.AddCriticalChance(1f);
                    status = "Critical chance 100% for this run.";
                }

                GUILayout.EndHorizontal();
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
                                : string.Empty) +
                            (effects != null && effects.IsPulseActive
                                ? $"   Pastel {effects.PulseRemainingSeconds:F1}s " +
                                  $"hit {effects.PulseDamageDeliveredTicks} heal {effects.PulseHealDeliveredTicks}"
                                : string.Empty) +
                            (effects != null && effects.IsEnemyDamageReductionActive
                                ? $"   Potion {effects.EnemyDamageReductionRemainingSeconds:F1}s " +
                                  $"({effects.EnemyDamageReductionRoomId})"
                                : string.Empty) +
                            (effects != null && effects.IsRoomDamageMultiplierActive
                                ? $"   Reason x{effects.RoomDamageMultiplier:0.#}"
                                : string.Empty) +
                            (effects != null && effects.RoomHomingCoefficient > 0f
                                ? $"   Homing {effects.RoomHomingCoefficient:0.##} r{effects.RoomHomingRange:0.#}"
                                : string.Empty));
            // Spell-6: the floor map state and the Run's tarot steps (엘리스의 타로카드).
            RunProgress progress = assembler.Progress;
            PlayerStats stats = slot.GetComponent<PlayerStats>();
            if (progress != null && stats != null)
            {
                string tarot = string.Join(" ", TarotCardDraw.Stats
                    .Where(stat => !Mathf.Approximately(stats.GetTarotStep(stat), 0f))
                    .Select(stat => $"{stat} {stats.GetTarotStep(stat):+0.##;-0.##}"));
                GUILayout.Label($"Map {(progress.IsFloorMapRevealed(progress.CurrentFloor) ? "revealed" : "hidden")}" +
                                $"   Tarot x{progress.TarotDrawCount}" +
                                (tarot.Length > 0 ? $"   {tarot}" : string.Empty));
            }
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

        // Power-1: drops a 권능 next to the player, so it is picked up through the normal slot path (new 권능 start
        // empty, swaps drop the held one with its gauge). The list collects every valid 권능 asset.
        private void DrawPowerSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Power (R) —");
            PlayerPowerSlot slot = assembler.Graph?.Player != null
                ? assembler.Graph.Player.GetComponent<PlayerPowerSlot>()
                : null;
            if (slot == null || slot.PickupPrefab == null)
            {
                GUILayout.Label("The player has no power slot.");
                return;
            }

            PlayerPowerEffects effects = slot.GetComponent<PlayerPowerEffects>();
            GUILayout.Label($"Held: {(slot.HasPower ? slot.HeldDefinition.DisplayName : "-")}" +
                            (slot.HasPower ? $"   gauge {slot.Charge}/{slot.RequiredCharge}   {slot.GetUseState()}" : string.Empty));
            if (effects != null)
            {
                string active = (effects.BlastDamageBonus > 0f
                                    ? $"Star DMG +{effects.BlastDamageBonus:P0} {effects.BlastRemainingSeconds:F1}s  "
                                    : string.Empty) +
                                (effects.IsBlanketActive ? $"Blanket {effects.BlanketRemainingSeconds:F1}s  " : string.Empty) +
                                (effects.IsOverdriveActive ? $"Overdrive {effects.OverdriveRemainingSeconds:F1}s  " : string.Empty) +
                                (effects.IsFreeloaderShieldActive
                                    ? $"Freeloader shield {effects.FreeloaderShieldRemaining:0.#}/{effects.FreeloaderShieldGranted:0.#}" +
                                      (effects.IsFreeloaderShieldLow ? " (blinking)" : string.Empty)
                                    : string.Empty);
                if (active.Length > 0) GUILayout.Label(active);
                // A one-heart hit through the normal damage path: ripples, then blinks and pops the shield bubble.
                Health health = slot.GetComponent<Health>();
                if (effects.IsFreeloaderShieldActive && health != null &&
                    AssistedButton(health.IsInvulnerable ? "Hit 1 heart (wait: invulnerable)" : "Hit 1 heart (shield bubble)") &&
                    !health.IsInvulnerable)
                    health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, HealthUnits.UnitsPerHeart));
            }

            GUILayout.BeginHorizontal();
            if (AssistedButton("+1 charge"))
                status = slot.AddCharge(1) > 0 ? $"Power gauge {slot.Charge}/{slot.RequiredCharge}." : "No charge added.";
            if (AssistedButton("Fill gauge"))
                status = slot.AddCharge(slot.RequiredCharge) > 0 ? "Power gauge filled." : "No charge added.";
            GUILayout.EndHorizontal();
#if UNITY_EDITOR
            powerItems ??= UnityEditor.AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null && item.IsActivePower && item.IsValid)
                .OrderBy(item => item.ItemId, System.StringComparer.Ordinal)
                .ToArray();
            foreach (ItemDefinition item in powerItems)
            {
                if (!AssistedButton($"Drop {item.DisplayName} ({item.ActiveChargeRooms})")) continue;
                RoomPrefab room = CurrentRoom(assembler);
                ActivePowerPickup pickup = Instantiate(slot.PickupPrefab,
                    slot.transform.position + Vector3.left * 1.2f, Quaternion.identity,
                    room != null ? room.transform : null);
                pickup.name = $"Active Power ({item.ItemId})";
                pickup.Configure(item, null, ActivePowerPickup.NoStoredCharge, false);
                status = $"Dropped {item.DisplayName} next to the player.";
            }
#else
            GUILayout.Label("Power drops are editor-only.");
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
            PlayerWaterStream water = player != null ? player.GetComponent<PlayerWaterStream>() : null;
            if (water != null)
            {
                bool realtime = GUILayout.Toggle(water.UseProceduralVisual, "Shasha: realtime water (off = original)");
                if (realtime != water.UseProceduralVisual) water.SetVisualMode(realtime);
                GUILayout.Label($"Water view: {(water.UseProceduralVisual ? "mesh + shader + droplets" : "original image")}");
            }
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
            if (ward != null)
            {
                float left = ward.InvulnerableUntil - Time.time;
                GUILayout.Label($"Dagger ward {(ward.IsSpent ? "spent" : "ready")}" +
                                (left > 0f ? $"  invulnerable {left:F1}s" : string.Empty) +
                                (player.GetComponent<PlayerHitPassThrough>()?.IsPassingThrough == true
                                    ? "  passing through" : string.Empty));
                // Lands a hit of exactly the remaining health + shield through the normal damage path.
                if (!ward.IsSpent && health != null && !health.IsDead &&
                    AssistedButton(health.IsInvulnerable ? "Lethal hit (wait: invulnerable)" : "Lethal hit (dagger ward)") &&
                    !health.IsInvulnerable)
                {
                    float hearts = health.UsesHealthUnits
                        ? HealthUnits.ToHearts(health.CurrentHealth + health.CurrentShield)
                        : health.CurrentHealth + health.CurrentShield;
                    health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, hearts));
                }
            }
            PlayerDamageAura aura = player.GetComponent<PlayerDamageAura>();
            if (aura != null && aura.StackCount > 0)
                GUILayout.Label($"Mask aura x{aura.StackCount}  {aura.Radius:0.##}m  next tick " +
                                $"{Mathf.Max(0f, aura.NextTickTime - Time.time):F1}s  field " +
                                (aura.Field != null && aura.Field.Field.enabled ? "on" : "off"));
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

        // Elif-2/3 (contract 27 §7.2): the permanent elif. Elif added here is usable in this Run but is not saved as
        // the Run's gain. Upgrade levels set here apply to this Run only and are never saved.
        private void DrawElifSection(RoomGraphAssembler assembler, RunProgress progress)
        {
            GUILayout.Label($"— Elif — usable {progress.AvailableElif}  (start {progress.ElifStartingBalance}  " +
                            $"gained {progress.ElifGained}  spent {progress.ElifSpent}  dev {progress.DevelopmentElif})");
            PlayerInventory inventory = assembler.Graph?.Player != null
                ? assembler.Graph.Player.GetComponent<PlayerInventory>()
                : null;
            if (inventory != null && inventory.LastPermanentElifRoll >= 0)
                GUILayout.Label($"Aisia wallet rolled {inventory.LastPermanentElifRoll} elif");
            GUILayout.BeginHorizontal();
            if (AssistedButton("+5 Elif (dev)")) progress.AddDevelopmentElif(5);
            if (AssistedButton("Drop Elif pickup"))
                status = TryDropElifPickup(assembler, progress, out string message)
                    ? message
                    : $"Elif drop failed: {message}";
            GUILayout.EndHorizontal();
            PermanentElif.DevelopmentForceNextConversion = GUILayout.Toggle(
                PermanentElif.DevelopmentForceNextConversion, "Next gold drop → Elif");
            // Gold drops are rare in obstacles and chests, so this drops gold through the real chest content path
            // (the same conversion roll as play). With the toggle on, the first one always becomes elif.
            if (AssistedButton("Chest with 4 gold (elif path)"))
                status = TrySpawnGoldChest(assembler, out string goldMessage)
                    ? goldMessage
                    : $"Gold chest failed: {goldMessage}";
            if (PermanentElif.DevelopmentForceNextConversion) MarkAssisted("Forced elif conversion");
            DrawShopUpgradeSection(assembler, progress);
        }

        // Elif-3: the shop upgrades and elif offers, testable without the server.
        private void DrawShopUpgradeSection(RoomGraphAssembler assembler, RunProgress progress)
        {
            GUILayout.Label(progress.IsElifSpendingAllowed
                ? "Elif spending: allowed"
                : "Elif spending: blocked (no fresh server balance)");
            bool allow = GUILayout.Toggle(progress.IsElifSpendingAllowed, "Allow elif spending (dev)");
            if (allow != progress.IsElifSpendingAllowed)
            {
                progress.SetDevelopmentElifSpendingAllowed(allow);
                MarkAssisted("Elif spending override");
            }

            GUILayout.Label("Set upgrade level (this Run only, not saved):");
            foreach (ShopUpgradeDefinition upgrade in ShopUpgrades.Catalog)
            {
                string id = upgrade.UpgradeId;
                GUILayout.Label($"{id}: Lv {progress.GetShopUpgradeLevel(id)}/{upgrade.MaxLevel} " +
                                $"(saved {progress.GetSavedShopUpgradeLevel(id)}" +
                                $"{(progress.HasDevelopmentUpgradeLevel(id) ? ", dev" : string.Empty)}" +
                                $"{(progress.IsUpgradeWaitingForPendingSave(id) ? ", pending save" : string.Empty)})");
                GUILayout.BeginHorizontal();
                for (int level = 0; level <= upgrade.MaxLevel; level++)
                    if (AssistedButton($"Lv{level}")) progress.SetDevelopmentShopUpgradeLevel(id, level);
                if (GUILayout.Button("Clear")) progress.SetDevelopmentShopUpgradeLevel(id, -1);
                GUILayout.EndHorizontal();
            }

            foreach (ShopUpgradePurchase purchase in progress.ShopUpgradePurchases)
                GUILayout.Label($"Bought #{purchase.Order}: {purchase.UpgradeId} Lv{purchase.Level} on F{purchase.Floor}");
            if (AssistedButton("Shop here: first item → elif price"))
                status = TryMakeElifOffer(assembler, progress, out string message)
                    ? message
                    : $"Elif offer failed: {message}";
        }

        // Makes the first artifact still on sale in the current room's shop an elif offer (the 25% roll, on demand).
        private static bool TryMakeElifOffer(RoomGraphAssembler assembler, RunProgress progress, out string message)
        {
            RoomPrefab room = CurrentRoom(assembler);
            ShopStockState stock = room != null && room.Node != null
                ? progress.GetShopStock(ShopStockBuilder.BuildShopId(room.Node.RoomId))
                : null;
            if (stock == null)
            {
                message = "stand in a shop room first.";
                return false;
            }

            ShopOffer offer = stock.Offers.FirstOrDefault(candidate => candidate.Kind == ShopOfferKind.Item &&
                                                                       !candidate.Item.IsActivePower &&
                                                                       !candidate.IsElifPriced &&
                                                                       !stock.IsPurchased(candidate.SlotIndex));
            if (offer == null || !stock.TryConvertToElifPrice(offer.SlotIndex))
            {
                message = "this shop has no artifact left to price in elif.";
                return false;
            }

            message = $"{offer.DisplayName} now costs {ShopUpgrades.ElifPriceOf(offer.Price)} elif.";
            return true;
        }

        // A normal development chest opened by touch, then four gold drops from it through RoomChestSpawner.
        private static bool TrySpawnGoldChest(RoomGraphAssembler assembler, out string message)
        {
            ChestContentTable table = assembler.ChestContentTable;
            ResourceDropEntry gold = table?.Consumables.FirstOrDefault(entry => entry?.Prefab != null &&
                entry.Prefab.TryGetComponent(out RunResourcePickup resource) &&
                resource.ResourceType == RunResourceType.Gold);
            PlayerMovement player = assembler.Graph?.Player;
            if (gold == null || player == null)
            {
                message = "needs the player and a gold chest drop.";
                return false;
            }

            if (!TrySpawnChest(assembler, ChestKind.Normal, out message)) return false;

            RoomPrefab room = CurrentRoom(assembler);
            GeneratedRoomNode node = assembler.GeneratedGraph.FindFloor(room.Node.FloorNumber).Nodes
                .First(candidate => candidate.RoomId == room.Node.RoomId);
            RoomChestSite site = new(room, node.Template, assembler.Progress.GetRoomState(room.Node.RoomId),
                assembler.Progress, player.transform);
            TreasureChest chest = site.Parent.GetComponentsInChildren<TreasureChest>(false)
                .Where(candidate => candidate.ChestId.StartsWith(RoomChestSpawner.DevelopmentChestPrefix,
                    System.StringComparison.Ordinal))
                .OrderByDescending(candidate => candidate.ChestId, System.StringComparer.Ordinal).First();
            chest.TryOpenByTouch(player);
            var drops = RoomChestSpawner.SpawnContents(table, chest,
                new ChestContents(ChestKind.Normal, Enumerable.Repeat(gold, 4).ToArray(), null), site);
            int elif = drops.Count(drop => drop.TryGetComponent(out RunResourcePickup resource) &&
                                           resource.ResourceType == RunResourceType.Elif);
            message = $"Chest {chest.ChestId} dropped 4 gold; {elif} became elif.";
            return true;
        }

        // A gold pickup from the chest table, turned into a development elif next to the player.
        private static bool TryDropElifPickup(RoomGraphAssembler assembler, RunProgress progress, out string message)
        {
            Transform player = assembler.Graph?.Player != null ? assembler.Graph.Player.transform : null;
            GameObject goldPrefab = assembler.ChestContentTable?.Consumables
                .Select(entry => entry?.Prefab)
                .FirstOrDefault(prefab => prefab != null &&
                                          prefab.TryGetComponent(out RunResourcePickup resource) &&
                                          resource.ResourceType == RunResourceType.Gold);
            if (player == null || goldPrefab == null)
            {
                message = "needs the player and a gold pickup in the chest content table.";
                return false;
            }

            RoomPrefab room = CurrentRoom(assembler);
            GameObject drop = Instantiate(goldPrefab, player.position + Vector3.right * 1.2f, Quaternion.identity,
                room != null ? room.transform : null);
            drop.name = $"Development Drop {PermanentElif.DropId}";
            RunResourcePickup pickup = drop.GetComponent<RunResourcePickup>();
            pickup.BindRunProgress(progress);
            pickup.ConvertToPermanentElif(true);
            message = "Dropped a development elif next to the player.";
            return true;
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

        // Trap-0: places a trap of each kind on the cell to the right of the player, bound the way the room build
        // binds an authored one (assisted Run). The list collects every trap Prefab. A placed trap lasts until the
        // floor is rebuilt.
        private void DrawTrapSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Trap —");
            RoomPrefab room = CurrentRoom(assembler);
            PlayerMovement player = assembler.Graph?.Player;
            if (room == null || player == null)
            {
                GUILayout.Label("No current room or player.");
                return;
            }

            RoomTrap nearest = room.GetComponentsInChildren<RoomTrap>(false)
                .OrderBy(trap => (trap.transform.position - player.transform.position).sqrMagnitude)
                .FirstOrDefault();
            DestructibleObstacle nearestObstacle = nearest != null ? nearest.GetComponent<DestructibleObstacle>() : null;
            GUILayout.Label(nearest == null
                ? "No trap in this room."
                : $"Nearest {nearest.Kind} {nearest.TrapId}" +
                  (nearest.IsFloorTrap
                      ? $"  hit {nearest.HitSize.x:0.##}x{nearest.HitSize.y:0.##}" +
                        (nearest.ContainsFeet(player.FeetPosition) ? " ON" : string.Empty)
                      : string.Empty) +
                  (nearest.Kind == RoomTrapKind.PeriodicSpikes
                      ? $"  {nearest.StateAt(Time.time)}{(nearest.SecondPhase ? " (B)" : " (A)")}"
                      : string.Empty) +
                  (nearestObstacle != null
                      ? $"  {nearestObstacle.HitsTaken}/{nearestObstacle.RequiredHits} hits"
                      : string.Empty));
            GUILayout.Label($"Web slow x{EnvironmentSlow.MultiplierOf(player):0.##}   " +
                            $"gas clouds {room.GetComponentsInChildren<PoisonGasCloud>(false).Length}");
#if UNITY_EDITOR
            const string trapFolder = "Assets/Prefabs/Traps";
            trapPrefabs ??= UnityEditor.AssetDatabase.IsValidFolder(trapFolder)
                ? UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { trapFolder })
                    .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                    .Select(prefab => prefab != null ? prefab.GetComponent<RoomTrap>() : null)
                    .Where(trap => trap != null)
                    .OrderBy(trap => trap.Kind)
                    .ToArray()
                : System.Array.Empty<RoomTrap>();
            if (trapPrefabs.Length == 0)
            {
                GUILayout.Label("No trap Prefab. Run the Trap-0 setup.");
                return;
            }

            trapSecondPhase = GUILayout.Toggle(trapSecondPhase, "Periodic spikes start raised (phase B)");
            foreach (RoomTrap prefab in trapPrefabs)
            {
                if (!AssistedButton($"Place {prefab.Kind}")) continue;
                Transform content = room.Node.ContentRoot.transform;
                Vector2 local = content.InverseTransformPoint(player.StandingPosition + Vector2.right * 1.5f);
                RoomTrap trap = Instantiate(prefab, content);
                trap.transform.localPosition = new Vector3(Mathf.Floor(local.x) + 0.5f, Mathf.Floor(local.y) + 0.5f);
                string id = $"dev-trap-{++placedTrapCount:000}";
                trap.name = $"{prefab.name} ({id})";
                trap.ConfigurePlacement(id, trapSecondPhase);
                RoomRunState state = assembler.Progress.GetRoomState(room.Node.RoomId);
                if (trap.TryGetComponent(out DestructibleObstacle obstacle))
                {
                    obstacle.Bind(state, 0, content, assembler.Progress, null, room.Node, room.Controller);
                    obstacle.BindBurnSource(player.GetComponent<PlayerStats>());
                }

                trap.Bind(state, player, Time.time);
                status = $"Placed {prefab.Kind} {id} to the right of the player.";
            }
#else
            GUILayout.Label("Trap placement is editor-only.");
#endif
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
            RoomPrefab room = CurrentRoom(assembler);
            RoomController controller = room?.Controller;
            if (AssistedButton("Kill current wave") && controller != null)
                status = $"Killed {controller.KillAliveEnemiesForDevelopment()} enemies.";
            // UX-0: dies through the normal damage path to watch the fall → GAME OVER → result sequence.
            Health playerHealth = assembler.Graph?.Player != null ? assembler.Graph.Player.GetComponent<Health>() : null;
            if (playerHealth != null && !playerHealth.IsDead && AssistedButton("Die (GAME OVER sequence)"))
            {
                playerHealth.TakeDamage(new DamageContext(null, DamageSourceType.Unknown,
                    playerHealth.MaxHealth + playerHealth.CurrentShield + 9999f));
                status = playerHealth.IsDead
                    ? "Player died. GAME OVER sequence started."
                    : "The hit was blocked (invulnerable or a death ward). Try again.";
            }

            // Enemy-7: this room's Encounter, and a 쫄따구 to kill without reaching 부스러기 (assisted Run). It is
            // an extra enemy of the room, so it never locks a cleared room.
            GeneratedRoomNode node = room != null
                ? assembler.GeneratedGraph?.FindFloor(room.Node.FloorNumber)?.Nodes
                    .FirstOrDefault(candidate => candidate.RoomId == room.Node.RoomId)
                : null;
            int eliteThreat = 0;
            if (node != null && assembler.Generator?.DifficultyTable != null)
                assembler.Generator.DifficultyTable.TryScoreVariants(node.ResolvedEncounterWaves, out eliteThreat,
                    out _);
            GUILayout.Label($"Encounter: {node?.EncounterId ?? "-"}" +
                            (node != null && node.HasDifficulty
                                ? $"  score {node.Difficulty.Score} {node.Difficulty.Tier}"
                                : string.Empty) +
                            (eliteThreat > 0 ? $"  +variants {eliteThreat}" : string.Empty));
            DrawEliteControls(assembler, room, controller, node);
            DrawChampionControls(assembler, controller, node);
            GUILayout.Label("Death blasts pending " +
                            FindObjectsByType<TrickalFanGame.Enemy.EnemyDeathBlast>(FindObjectsSortMode.None)
                                .Count(blast => !blast.HasExploded));
#if UNITY_EDITOR
            PlayerMovement player = assembler.Graph?.Player;
            if (AssistedButton("Spawn pure Sansamo (walk + flower shots)") && controller != null && player != null)
            {
                GameObject sansamo = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/QuickRangedFairy.prefab");
                status = controller.SpawnExtraEnemy(sansamo, player.transform.position + Vector3.right * 3f) != null
                    ? "Spawned pure Sansamo to the right of the player." : "Pure Sansamo spawn failed.";
            }
            if (AssistedButton("Spawn crumb minion (explodes on death)") && controller != null && player != null)
            {
                GameObject minion = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/BuseureogiCrumbMinion.prefab");
                status = controller.SpawnExtraEnemy(minion, player.transform.position + Vector3.right * 2.5f) != null
                    ? "Spawned a crumb minion to the right of the player."
                    : "Crumb minion spawn failed.";
            }

            // Enemy-8: 저혈당 요정-활발 three tiles above the player, so its left and right shots miss and the
            // downward one comes straight at the player.
            if (AssistedButton("Spawn lively fairy (4-way shots)") && controller != null && player != null)
            {
                GameObject fairy = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/LivelyLowBloodSugarFairy.prefab");
                status = controller.SpawnExtraEnemy(fairy, player.transform.position + Vector3.up * 3f) != null
                    ? "Spawned a lively fairy above the player."
                    : "Lively fairy spawn failed.";
            }
            GUILayout.Label("Lively fairies " + FindObjectsByType<TrickalFanGame.Enemy.FourWayShooterController>(
                FindObjectsSortMode.None).Length);

            // Enemy-9: 누루링-요정전사 two tiles to the right of the player. Killing it leaves two small 누루링
            // that hold the room until they die too.
            if (AssistedButton("Spawn nururing (splits on death)") && controller != null && player != null)
            {
                GameObject nururing = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/NururingFairyWarrior.prefab");
                status = controller.SpawnExtraEnemy(nururing, player.transform.position + Vector3.right * 2f) != null
                    ? "Spawned a nururing to the right of the player."
                    : "Nururing spawn failed.";
            }
            int nururingBodies = FindObjectsByType<TrickalFanGame.Enemy.EnemySplitOnDeath>(FindObjectsSortMode.None)
                .Count(body => body.TryGetComponent(out Health bodyHealth) && !bodyHealth.IsDead);
            int nururingSplits = FindObjectsByType<TrickalFanGame.Frontend.FairyKingdomArtworkView>(
                    FindObjectsSortMode.None)
                .Count(view => view.ArtworkId == "nururing" &&
                               view.GetComponent<TrickalFanGame.Enemy.EnemySplitOnDeath>() == null &&
                               view.TryGetComponent(out Health splitHealth) && !splitHealth.IsDead);
            GUILayout.Label($"Nururing bodies {nururingBodies}, splits {nururingSplits}");

            // Enemy-10: 자폭형 three tiles to the right of the player. It stops within 1.2 and warns one second
            // before blowing up; killing it any other way also leaves the blast.
            if (AssistedButton("Spawn self-destruct (blows up near you)") && controller != null && player != null)
            {
                GameObject bomber = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/SelfDestructFairy.prefab");
                status = controller.SpawnExtraEnemy(bomber, player.transform.position + Vector3.right * 3f) != null
                    ? "Spawned a self-destruct enemy to the right of the player."
                    : "Self-destruct spawn failed.";
            }
            TrickalFanGame.Enemy.SelfDestructEnemy[] bombers =
                FindObjectsByType<TrickalFanGame.Enemy.SelfDestructEnemy>(FindObjectsSortMode.None);
            GUILayout.Label($"Self-destruct alive {bombers.Count(bomber => bomber.TryGetComponent(out Health bomberHealth) && !bomberHealth.IsDead)}, " +
                            $"warning {bombers.Count(bomber => bomber.IsPrimed)}");

            // Enemy-11: 대왕쥬비 four tiles above the player, its preferred distance. Every 3 s it glows and releases
            // two 쥬비 until eight of its own are alive; they stay when it dies.
            if (AssistedButton("Spawn Jyubi king (summons Jyubi)") && controller != null && player != null)
            {
                GameObject king = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/JyubiKing.prefab");
                status = controller.SpawnExtraEnemy(king, player.transform.position + Vector3.up * 4f) != null
                    ? "Spawned a Jyubi king above the player."
                    : "Jyubi king spawn failed.";
            }
            TrickalFanGame.Enemy.JyubiKingController[] kings =
                FindObjectsByType<TrickalFanGame.Enemy.JyubiKingController>(FindObjectsSortMode.None);
            TrickalFanGame.Enemy.JyubiKingController[] livingKings = kings
                .Where(king => king.TryGetComponent(out Health kingHealth) && !kingHealth.IsDead).ToArray();
            GUILayout.Label($"Jyubi kings {livingKings.Length}, their Jyubi " +
                            $"{livingKings.Sum(king => king.AliveSummonCount)}, summoning " +
                            $"{livingKings.Count(king => king.IsSummoning)}");

            // Enemy-12: 곡사형 five tiles above the player, inside its 3.5~7 throwing distance. It winds up, then lobs
            // over obstacles to where the player stood; the landing circle shows for the whole flight.
            if (AssistedButton("Spawn lobber (throws over obstacles)") && controller != null && player != null)
            {
                GameObject lobber = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/LobbingFairy.prefab");
                status = controller.SpawnExtraEnemy(lobber, player.transform.position + Vector3.up * 5f) != null
                    ? "Spawned a lobber above the player."
                    : "Lobber spawn failed.";
            }
            TrickalFanGame.Enemy.LobbingEnemyController[] lobbers =
                FindObjectsByType<TrickalFanGame.Enemy.LobbingEnemyController>(FindObjectsSortMode.None);
            GUILayout.Label($"Lobbers alive {lobbers.Count(lobber => lobber.TryGetComponent(out Health lobberHealth) && !lobberHealth.IsDead)}, " +
                            $"winding up {lobbers.Count(lobber => lobber.IsWindingUp)}, in the air " +
                            $"{FindObjectsByType<TrickalFanGame.Enemy.EnemyLobProjectile>(FindObjectsSortMode.None).Length}");

            // Enemy-13: 잠복형 four tiles above the player. It starts under the floor (dim brown, cannot be hit),
            // closes in, warns with a red ellipse and bursts out (red), then stays up to be hit before sinking.
            if (AssistedButton("Spawn ambusher (bursts from the floor)") && controller != null && player != null)
            {
                GameObject ambusher = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/AmbushFairy.prefab");
                status = controller.SpawnExtraEnemy(ambusher, player.transform.position + Vector3.up * 4f) != null
                    ? "Spawned an ambusher above the player."
                    : "Ambusher spawn failed.";
            }
            TrickalFanGame.Enemy.AmbushEnemyController[] ambushers =
                FindObjectsByType<TrickalFanGame.Enemy.AmbushEnemyController>(FindObjectsSortMode.None)
                    .Where(ambusher => ambusher.TryGetComponent(out Health ambusherHealth) && !ambusherHealth.IsDead)
                    .ToArray();
            GUILayout.Label($"Ambushers alive {ambushers.Length}, buried " +
                            $"{ambushers.Count(ambusher => ambusher.State == TrickalFanGame.Enemy.AmbushEnemyController.AmbushState.Burrowed)}, " +
                            $"warning {ambushers.Count(ambusher => ambusher.State == TrickalFanGame.Enemy.AmbushEnemyController.AmbushState.Emerging)}, " +
                            $"up {ambushers.Count(ambusher => !ambusher.IsBurrowed)}");
#else
            GUILayout.Label("Crumb minion, lively fairy, nururing, self-destruct, Jyubi king, lobber and ambusher spawns are editor-only.");
#endif
        }

        // T7: restarts the Run on its seed with one Layout forced into every combat room it fits, so a new room is
        // seen without searching seeds (assisted Run). The force stays until it is cleared here.
        private void DrawLayoutSection(RoomGraphAssembler assembler)
        {
            GUILayout.Label("— Layout —");
            RoomPrefab room = CurrentRoom(assembler);
            GeneratedRoomNode node = room != null
                ? assembler.GeneratedGraph?.FindFloor(room.Node.FloorNumber)?.Nodes
                    .FirstOrDefault(candidate => candidate.RoomId == room.Node.RoomId)
                : null;
            string forced = RoomTemplateSelector.DevelopmentForcedTemplateId;
            GUILayout.Label($"This room: {node?.TemplateId ?? "-"}" +
                            (node?.Template != null ? $"  weight {node.Template.SelectionWeight}" : string.Empty));
            GUILayout.Label($"Forced: {(string.IsNullOrEmpty(forced) ? "-" : forced)}");
            if (!string.IsNullOrEmpty(forced) && GUILayout.Button("Clear forced layout and restart"))
            {
                RoomTemplateSelector.DevelopmentForcedTemplateId = null;
                RestartWithSeed(assembler.Progress.RunSeed);
                return;
            }

            if (assembler.Generator == null) return;
            RoomTemplateDefinition[] layouts = assembler.Generator.RoomTemplates
                .Where(template => template != null && template.SupportsRoomType(RoomType.Normal))
                .OrderBy(template => template.TemplateId, System.StringComparer.Ordinal)
                .ToArray();
            showLayouts = GUILayout.Toggle(showLayouts, $"Show layouts ({layouts.Length})");
            if (!showLayouts) return;
            foreach (RoomTemplateDefinition layout in layouts)
            {
                if (!AssistedButton($"Force {layout.TemplateId}")) continue;
                RoomTemplateSelector.DevelopmentForcedTemplateId = layout.TemplateId;
                RestartWithSeed(assembler.Progress.RunSeed);
                return;
            }
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

        // Variant-0: turns the living enemy nearest to the player into an elite (bigger, x2 health, x2 knockback
        // weight, one damage tier up, x1.2 movement, x1.5 shots). Its kill drops the elite reward once per room.
        private void DrawEliteControls(RoomGraphAssembler assembler, RoomPrefab room, RoomController controller,
            GeneratedRoomNode node)
        {
            PlayerMovement player = assembler.Graph?.Player;
            if (AssistedButton("Make nearest enemy elite") && controller != null && player != null)
            {
                Health nearest = controller.AliveEnemies
                    .Where(enemy => enemy != null && !enemy.IsDead &&
                                    TrickalFanGame.Enemy.EnemyVariant.CanApply(enemy.gameObject))
                    .OrderBy(enemy => (enemy.transform.position - player.transform.position).sqrMagnitude)
                    .FirstOrDefault();
                if (nearest == null)
                {
                    status = "No living normal enemy in this room to make elite.";
                }
                else
                {
                    controller.EnsureEliteRewardForDevelopment(assembler.EliteKillDropTable,
                        room != null ? room.Node.ContentRoot.transform : controller.transform,
                        EliteKillReward.DeriveSeed(node?.ContentSeed ?? 0));
                    status = TrickalFanGame.Enemy.EnemyVariant.Apply(nearest.gameObject,
                        TrickalFanGame.Enemy.EnemyVariantKind.Elite)
                        ? $"{nearest.name} is now elite ({nearest.MaxHealth:0} health)."
                        : "That enemy cannot become elite.";
                }
            }

            int elitesAlive = controller == null ? 0 : controller.AliveEnemies.Count(enemy =>
                enemy != null && !enemy.IsDead && TrickalFanGame.Enemy.EnemyVariant.IsEliteEnemy(enemy.gameObject));
            string reward = controller?.EliteKillReward == null ? "none"
                : controller.EliteKillReward.LastSpawnedReward != null
                    ? $"dropped {controller.EliteKillReward.LastSpawnedReward.name}"
                    : "waiting";
            GUILayout.Label($"Elites planned {EnemyVariantRules.CountElites(node?.ResolvedEncounterWaves)}, " +
                            $"alive {elitesAlive}, reward {reward}");
        }

        // Variant-1: turns the living enemy nearest to the player into a champion with the chosen death property
        // (green puddle, purple 4-way shots, yellow 쫄따구 blast, Variant-2 white undying). 자폭형 refuses the blast.
        private void DrawChampionControls(RoomGraphAssembler assembler, RoomController controller,
            GeneratedRoomNode node)
        {
            PlayerMovement player = assembler.Graph?.Player;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Make nearest champion");
            foreach ((TrickalFanGame.Enemy.EnemyVariantKind kind, string label) in new[]
                     {
                         (TrickalFanGame.Enemy.EnemyVariantKind.ChampionPuddle, "Puddle"),
                         (TrickalFanGame.Enemy.EnemyVariantKind.ChampionShots, "Shots"),
                         (TrickalFanGame.Enemy.EnemyVariantKind.ChampionBlast, "Blast"),
                         (TrickalFanGame.Enemy.EnemyVariantKind.ChampionUndying, "Undying"),
                     })
            {
                if (!AssistedButton(label) || controller == null || player == null) continue;
                Health nearest = controller.AliveEnemies
                    .Where(enemy => enemy != null && !enemy.IsDead &&
                                    TrickalFanGame.Enemy.EnemyVariant.CanApply(enemy.gameObject, kind))
                    .OrderBy(enemy => (enemy.transform.position - player.transform.position).sqrMagnitude)
                    .FirstOrDefault();
                status = nearest == null
                    ? $"No living normal enemy in this room can become a {label.ToLowerInvariant()} champion."
                    : TrickalFanGame.Enemy.EnemyVariant.Apply(nearest.gameObject, kind)
                        ? $"{nearest.name} is now a {label.ToLowerInvariant()} champion."
                        : "That enemy cannot become a champion.";
            }

            GUILayout.EndHorizontal();
            int championsAlive = controller == null ? 0 : controller.AliveEnemies.Count(enemy =>
                enemy != null && !enemy.IsDead && TrickalFanGame.Enemy.EnemyVariant.IsChampionEnemy(enemy.gameObject));
            GUILayout.Label($"Champions planned {EnemyVariantRules.CountChampions(node?.ResolvedEncounterWaves)}, " +
                            $"alive {championsAlive}, puddles {TrickalFanGame.Enemy.EnemyChampionPuddle.Live.Count}, " +
                            "volleys pending " +
                            TrickalFanGame.Enemy.EnemyChampionVolley.Live.Count(volley => !volley.HasFired) +
                            ", undying now " + UndyingStatus(controller));
        }

        // Variant-2: each undying champion running on undying life, with its life left and seconds left.
        private static string UndyingStatus(RoomController controller)
        {
            if (controller == null) return "0";
            string[] running = controller.AliveEnemies
                .Where(enemy => enemy != null && !enemy.IsDead &&
                                TrickalFanGame.Enemy.EnemyChampionUndying.IsActiveOn(enemy))
                .Select(enemy => $"{enemy.CurrentHealth:0}/{enemy.MaxHealth:0} " +
                                 $"{Mathf.Max(0f, enemy.GetComponent<TrickalFanGame.Enemy.EnemyChampionUndying>().EndTime - Time.time):0.0}s")
                .ToArray();
            return running.Length == 0 ? "0" : $"{running.Length} ({string.Join(", ", running)})";
        }

        private void FindEnemySeedButton(RoomGraphAssembler assembler, EncounterEnemyRole role, string buttonName,
            string label)
        {
            int floor = EncounterEnemyFloorPool.IntroductionFloor(role);
            if (!GUILayout.Button($"Find {buttonName} F{floor}")) return;
            FindSeed(assembler, node => node.ResolvedEncounterWaves != null &&
                                        node.ResolvedEncounterWaves.Any(wave => wave.Any(spawn => spawn.Role == role)),
                label, floor);
        }

        private void FindSeed(RoomGraphAssembler assembler, System.Func<GeneratedRoomNode, bool> match, string label,
            int floorNumber = 1)
        {
            if (assembler.Generator == null) return;
            int start = int.TryParse(seedText, out int typed) ? typed + 1 :
                assembler.Progress.HasRunSeed ? assembler.Progress.RunSeed + 1 : 1;
            for (int offset = 0; offset < SeedSearchLimit; offset++)
            {
                int seed = unchecked(start + offset);
                if (!assembler.Generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                if (graph.FindFloor(floorNumber)?.Nodes.Any(match) != true) continue;
                seedText = seed.ToString();
                status = $"Seed {seed} has a floor-{floorNumber} {label}.";
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

            RestartWithSeed(seed);
        }

        private static void RestartWithSeed(int seed)
        {
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
