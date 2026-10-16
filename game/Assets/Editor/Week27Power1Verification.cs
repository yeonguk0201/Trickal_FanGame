using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Power-1: verifies the 교주의 권능 contract (Active = 5, active- prefix, six assets), the slot (one 권능, a new one
    // starts empty, a swapped one drops and comes back with its gauge, one Run record per instance, combat and boss
    // room clears charge once each up to the requirement, R needs a full gauge and is blocked while paused, dead or
    // stopped), every effect's numbers, the shared enemy stun, the pools that must not hold 권능, and the Game Scene.
    public static class Week27Power1Verification
    {
        private const float Step = 0.5f;

        public static void SetupAndVerifyBatch()
        {
            Week27Power1Setup.Setup();
            string[] paths = Week27Power1Setup.Specs.Select(spec => spec.AssetPath)
                .Append(Week27Power1Setup.PickupPrefabPath).Append(Week13FrontendSetup.GameScenePath).ToArray();
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week27Power1Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID)),
                "Power-1 setup changed a 권능 asset, the pickup Prefab or the Game Scene GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Power-1 Active Powers")]
        public static void Verify()
        {
            ValidateKindContract();
            ValidateAssets();
            ValidateSlotRuntime();
            ValidateEffects();
            ValidateStun();
            ValidateHudRuntime();
            ValidatePrefabAndPools();
            ValidateGameScene();
            Debug.Log("Power-1 verification passed: Active = 5 with the active- prefix, six 권능 assets, one held 권능 " +
                      "that starts empty and drops with its gauge on a swap, one Run record per instance, one charge " +
                      "per combat or boss room clear up to the requirement, R blocked until full and while paused, " +
                      "dead or stopped, every effect's numbers, the shared stun, no 권능 in shared pools, and the " +
                      "Game Scene slot and HUD.");
        }

        private static void ValidateKindContract()
        {
            Assert((int)ItemKind.Active == 5 && !Enum.IsDefined(typeof(ItemKind), 4),
                "ItemKind.Active must be 5, and Trinket 4 stays reserved.");
            Assert(ItemDefinition.IsItemIdValidForKind("active-poppin-star", ItemKind.Active) &&
                   !ItemDefinition.IsItemIdValidForKind("active-poppin-star", ItemKind.Artifact) &&
                   !ItemDefinition.IsItemIdValidForKind("active-poppin-star", ItemKind.SingleUseSpell) &&
                   !ItemDefinition.IsItemIdValidForKind("artifact-life-gem", ItemKind.Active) &&
                   !ItemDefinition.IsItemIdValidForKind("single-spell-random-coin", ItemKind.Active),
                "The active- prefix must belong to 권능 only.");
            Assert(ItemKindText.GetDisplayName(ItemKind.Active) == "교주의 권능" &&
                   ItemKindText.IsActivePower(ItemKind.Active) && !ItemKindText.IsSingleUse(ItemKind.Active),
                "A 권능 reads 교주의 권능 and is not a single-use item.");

            ItemDefinition noCharge = CreatePower("active-power1-no-charge", 0);
            ItemDefinition stacked = CreatePower("active-power1-stacked", 2, 2);
            try
            {
                Assert(!noCharge.IsValid, "A 권능 needs a positive charge requirement.");
                Assert(!stacked.IsValid, "A 권능 must have maxStacks = 1.");
            }
            finally
            {
                Object.DestroyImmediate(noCharge);
                Object.DestroyImmediate(stacked);
            }
        }

        private static void ValidateAssets()
        {
            string[] expectedIds =
            {
                "active-poppin-star", "active-security-blanket", "active-divine-punishment",
                "active-solar-plexus-strike", "active-freeloader-shield", "active-sudden-acceleration",
            };
            Assert(Week27Power1Setup.Specs.Select(spec => spec.Id).SequenceEqual(expectedIds),
                "The 권능 IDs must match contract 23 §9.2.");
            int[] expectedCharge = { 2, 3, 3, 4, 4, 5 };
            for (int index = 0; index < expectedIds.Length; index++)
            {
                Week27Power1Setup.PowerSpec spec = Week27Power1Setup.Specs[index];
                ItemDefinition power = AssetDatabase.LoadAssetAtPath<ItemDefinition>(spec.AssetPath);
                Assert(power != null, $"Run Power-1 setup first: {spec.AssetPath} is missing.");
                Assert(power.ItemId == spec.Id && power.DisplayName == spec.Name && power.Kind == ItemKind.Active &&
                       power.Rarity == ItemRarity.Rare && power.IsActive && power.MaxStacks == 1 &&
                       power.ActiveChargeRooms == expectedCharge[index] && power.IsValid,
                    $"{spec.Id} must be a valid Rare 권능 needing {expectedCharge[index]} rooms.");
                Assert(power.Effects.Count == spec.Effects.Length &&
                       power.Effects.All(effect => effect.EffectType >= ItemEffectType.PowerRoomBlast),
                    $"{spec.Id} must use only the 권능 effect types (65~).");
                string description = ArtifactEffectDescription.Build(power);
                Assert(!string.IsNullOrEmpty(description) && !description.Contains("Power"),
                    $"{spec.Id} needs a Korean effect description.");
            }

            ItemEffectType[] powerTypes = Enum.GetValues(typeof(ItemEffectType)).Cast<ItemEffectType>()
                .Where(type => (int)type is >= 65 and <= 72).ToArray();
            Assert(powerTypes.Length == 8 && (int)ItemEffectType.PowerRoomBlast == 65 &&
                   (int)ItemEffectType.PowerRestoreSP == 72,
                "The 권능 effect types must be appended as 65~72.");
            // Contract 23 §9.5: no other item uses a 권능 effect type.
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
            {
                ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || item.IsActivePower) continue;
                Assert(item.Effects.All(effect => effect == null || (int)effect.EffectType is < 65 or > 72),
                    $"{item.ItemId} must not use a 권능 effect type.");
            }
        }

        private static void ValidateSlotRuntime()
        {
            ActivePowerPickup prefab = LoadPrefab();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            FloorGenerator generator = FindAll<FloorGenerator>(scene).Single();
            Assert(generator.TryGenerateForSeed(20261015, out GeneratedFloorGraph graph, out string error),
                "Power-1 test graph generation failed: " + error);

            TestRig rig = TestRig.Create("Slot");
            GameObject floor = new("Power-1 Verification Floor");
            ItemDefinition star = Load(Week27Power1Setup.PoppinStarId);
            ItemDefinition shield = Load(Week27Power1Setup.FreeloaderShieldId);
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                RunProgress progress = rig.Progress;
                Assert(progress.TrySetGeneratedGraph(graph, out error), "Power-1 progress rejected the graph: " + error);
                PlayerPowerSlot slot = rig.Slot;
                slot.Configure(progress, prefab);
                InvokeLifecycle(slot, "OnEnable");

                // A new 권능 starts with an empty gauge and is recorded once.
                Assert(slot.TryCollect(Spawn(prefab, floor, star, "power1-star")) && slot.HeldDefinition == star &&
                       slot.Charge == 0 && slot.RequiredCharge == 2,
                    "A new 권능 must start with an empty gauge.");
                Assert(rig.Inventory.AcquiredItems.Count == 1 && rig.Inventory.AcquiredItems[0].ItemId == star.ItemId &&
                       rig.Inventory.AcquiredDefinitions.Count == 0,
                    "A 권능 is recorded for the Run once and never joins the artifact list.");
                Assert(!rig.Inventory.TryAcquire(star), "PlayerInventory.TryAcquire must reject a 권능.");
                Assert(slot.TryUse() == PowerSlotUseResult.NotCharged, "An empty gauge must not be used.");

                // Only combat and boss room clears charge, once per room, up to the requirement.
                GeneratedFloor first = graph.FindFloor(1);
                string start = first.StartingRoomId;
                string[] combat = first.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate)
                    .Select(node => node.RoomId).ToArray();
                Assert(combat.Length >= 3, "The test floor needs three combat rooms.");
                progress.RecordRoomCleared(start);
                Assert(slot.Charge == 0, "The start room must not charge the gauge.");
                progress.RecordRoomCleared(combat[0]);
                progress.RecordRoomCleared(combat[0]);
                Assert(slot.Charge == 1, "A combat room charges once, even when reported twice.");
                progress.RecordRoomCleared(combat[1]);
                progress.RecordRoomCleared(combat[2]);
                Assert(slot.Charge == 2 && slot.IsFullyCharged, "The gauge stops at its requirement.");

                // Swap: the held 권능 drops with its gauge; the new one starts empty; swapping back restores it.
                ActivePowerPickup shieldPickup = Spawn(prefab, floor, shield, "power1-shield");
                shieldPickup.transform.position = new Vector3(2f, 1f, 0f);
                Assert(slot.TryCollect(shieldPickup) && slot.HeldDefinition == shield && slot.Charge == 0,
                    "A swapped-in new 권능 must start empty.");
                ActivePowerPickup[] onFloor = FloorPickups(floor);
                Assert(onFloor.Length == 1 && onFloor[0].Definition == star && onFloor[0].InstanceId == "power1-star" &&
                       onFloor[0].HasStoredCharge && onFloor[0].StoredCharge == 2 && onFloor[0].WaitsForPlayerExit &&
                       onFloor[0].transform.position == new Vector3(2f, 1f, 0f),
                    "A swap must drop the held 권능 at the pickup position with its gauge.");
                Assert(slot.AddCharge(1) == 1 && slot.Charge == 1, "The new 권능 must charge on its own.");
                Assert(slot.TryCollect(onFloor[0]) && slot.HeldDefinition == star && slot.Charge == 2,
                    "Picking a dropped 권능 up again must restore its gauge.");
                onFloor = FloorPickups(floor);
                Assert(onFloor.Length == 1 && onFloor[0].Definition == shield && onFloor[0].StoredCharge == 1,
                    "The other 권능 must drop with its own gauge.");
                Assert(rig.Inventory.AcquiredItems.Count == 2, "Swapping back must not record a 권능 again.");

                // A swap that cannot drop the held 권능 must not happen.
                slot.Configure(progress, null);
                Assert(!slot.TryCollect(onFloor[0]) && !onFloor[0].IsCollected && slot.HeldDefinition == star,
                    "A swap without the pickup Prefab must be refused.");
                slot.Configure(progress, prefab);

                // A full gauge with no enemy in the room is refused and kept (팝핀 스타 needs a target).
                List<Health> enemies = new();
                rig.Effects.ConfigureEnemySourceForVerification(() => enemies);
                Assert(slot.TryUse() == PowerSlotUseResult.ConditionNotMet && slot.Charge == 2,
                    "A 권능 with no target must not spend the gauge.");
                enemies.Add(CreateEnemy(rig.Root, "Target", 1000f, false));
                Time.timeScale = 0f;
                Assert(slot.TryUse() == PowerSlotUseResult.Paused && slot.Charge == 2, "R is blocked while paused.");
                Time.timeScale = 1f;
                Assert(slot.TryUse() == PowerSlotUseResult.Used && slot.Charge == 0 && slot.HeldDefinition == star,
                    "A use empties the gauge and keeps the 권능.");
                Assert(slot.TryUse() == PowerSlotUseResult.NotCharged, "A second press must not use it again.");

                slot.AddCharge(5);
                progress.StopProgression();
                Assert(slot.TryUse() == PowerSlotUseResult.RunStopped && slot.Charge == 2 &&
                       !slot.TryCollect(onFloor[0]),
                    "After the Run ends a 권능 cannot be used or collected.");
                progress.ResetProgress();
                Health health = rig.Player.GetComponent<Health>();
                health.TakeDamage(health.MaxHealth * 10f);
                Assert(health.IsDead && slot.TryUse() == PowerSlotUseResult.PlayerDead && slot.Charge == 2,
                    "A dead player cannot use a 권능.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                rig.Dispose();
                Object.DestroyImmediate(floor);
            }
        }

        private static void ValidateEffects()
        {
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            try
            {
                ValidatePoppinStar();
                ValidateBlanket();
                ValidateDivinePunishment();
                ValidateSolarPlexus();
                ValidateShield();
                ValidateOverdrive();
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
        }

        private static void ValidatePoppinStar()
        {
            TestRig rig = TestRig.Create("Star");
            try
            {
                Health boss = CreateEnemy(rig.Root, "Boss", 1000f, true);
                Health[] minions = Enumerable.Range(0, 6).Select(index => CreateEnemy(rig.Root, $"Minion {index}", 1000f, false))
                    .ToArray();
                rig.Effects.ConfigureEnemySourceForVerification(() => minions.Append(boss));
                float attack = rig.Stats.AttackDamage;
                rig.Use(Week27Power1Setup.PoppinStarId);
                Assert(Mathf.Approximately(boss.CurrentHealth, 1000f - attack) &&
                       minions.All(minion => Mathf.Approximately(minion.CurrentHealth, 1000f - attack * 2f)),
                    "팝핀 스타 deals 100% attack to a boss and 200% to other enemies.");
                Assert(rig.Effects.LastHitCount == 7 && Mathf.Approximately(rig.Effects.BlastDamageBonus, 0.4f) &&
                       Mathf.Approximately(rig.Stats.AttackDamage, attack * 1.4f),
                    "Seven hits give the +40% damage cap.");
                rig.Effects.Advance(5.9f);
                Assert(rig.Effects.BlastDamageBonus > 0f, "The damage bonus lasts six seconds.");
                rig.Effects.Advance(0.2f);
                Assert(rig.Effects.BlastDamageBonus == 0f && Mathf.Approximately(rig.Stats.AttackDamage, attack),
                    "The damage bonus ends after six seconds.");
            }
            finally
            {
                rig.Dispose();
            }
        }

        private static void ValidateBlanket()
        {
            TestRig rig = TestRig.Create("Blanket");
            try
            {
                Health health = rig.Player.GetComponent<Health>();
                health.TakeDamage(6f);
                float before = health.CurrentHealth;
                rig.Use(Week27Power1Setup.SecurityBlanketId);
                health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, 2f, allowsHalfHeart: true));
                Assert(Mathf.Approximately(health.CurrentHealth, before - 1f),
                    "애착 담요 takes half a heart off a one heart hit.");
                SkipHitInvulnerability(health);
                health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, 1f, allowsHalfHeart: true));
                Assert(Mathf.Approximately(health.CurrentHealth, before - 2f),
                    "A half heart hit stays half a heart (the reduction never goes below half a heart).");
                float afterHits = health.CurrentHealth;
                rig.Effects.Advance(3f);
                Assert(Mathf.Approximately(health.CurrentHealth, afterHits + 1f) && rig.Effects.BlanketDeliveredTicks == 1,
                    "애착 담요 heals half a heart every three seconds.");
                rig.Effects.Advance(3f);
                Assert(Mathf.Approximately(health.CurrentHealth, afterHits + 2f) && !rig.Effects.IsBlanketActive &&
                       health.PowerDamageReduction == 0f,
                    "애착 담요 heals one heart in total and ends after six seconds.");
            }
            finally
            {
                rig.Dispose();
            }
        }

        private static void ValidateDivinePunishment()
        {
            TestRig rig = TestRig.Create("Smite");
            try
            {
                Health tough = CreateEnemy(rig.Root, "Tough", 900f, false);
                Health weak = CreateEnemy(rig.Root, "Weak", 300f, false);
                rig.Effects.ConfigureEnemySourceForVerification(() => new[] { weak, tough });
                float attack = rig.Stats.AttackDamage;
                rig.Use(Week27Power1Setup.DivinePunishmentId);
                Assert(rig.Effects.LastSmiteTarget == tough && Mathf.Approximately(tough.CurrentHealth, 900f - attack * 5f) &&
                       Mathf.Approximately(weak.CurrentHealth, 300f),
                    "찐짜천벌 strikes the enemy with the most health for 500% attack.");
                EnemyStatusEffects status = tough.GetComponent<EnemyStatusEffects>();
                Assert(status != null && status.ShockStacks == 4, "찐짜천벌 adds four shock stacks.");

                Health boss = CreateEnemy(rig.Root, "Boss", 100f, true);
                rig.Effects.ConfigureEnemySourceForVerification(() => new[] { weak, tough, boss });
                rig.Slot.AddCharge(10);
                Assert(rig.Slot.TryUse() == PowerSlotUseResult.Used && rig.Effects.LastSmiteTarget == boss,
                    "찐짜천벌 strikes a boss first.");
            }
            finally
            {
                rig.Dispose();
            }
        }

        private static void ValidateSolarPlexus()
        {
            TestRig rig = TestRig.Create("Stun");
            try
            {
                Health boss = CreateEnemy(rig.Root, "Boss", 1000f, true);
                Health minion = CreateEnemy(rig.Root, "Minion", 1000f, false);
                rig.Effects.ConfigureEnemySourceForVerification(() => new[] { boss, minion });
                rig.Use(Week27Power1Setup.SolarPlexusStrikeId);
                Assert(boss.GetComponent<KnockbackReceiver>().IsStunned && minion.GetComponent<KnockbackReceiver>().IsStunned,
                    "명치 꽂기 stuns every enemy, bosses included.");
                Assert(Mathf.Approximately(minion.IncomingDamageBonus, 0.5f) &&
                       Mathf.Approximately(boss.IncomingDamageBonus, 0.5f),
                    "명치 꽂기 makes every enemy take 50% more damage.");
                minion.TakeDamage(new DamageContext(rig.Player, DamageSourceType.PlayerAttack, 10f));
                Assert(Mathf.Approximately(minion.CurrentHealth, 985f), "A 10 damage hit deals 15 while vulnerable.");
            }
            finally
            {
                rig.Dispose();
            }
        }

        private static void ValidateShield()
        {
            TestRig rig = TestRig.Create("Shield");
            try
            {
                Health health = rig.Player.GetComponent<Health>();
                health.SetHealthAndShieldLimit(30f);
                rig.Use(Week27Power1Setup.FreeloaderShieldId);
                Assert(Mathf.Approximately(health.CurrentShield, 4f), "날먹보호막 gives two hearts of shield.");
                health.SetHealthAndShieldLimit(health.CurrentHealth + health.CurrentShield);
                rig.Slot.AddCharge(10);
                Assert(rig.Slot.TryUse() == PowerSlotUseResult.ConditionNotMet && rig.Slot.IsFullyCharged,
                    "At the health + shield limit 날먹보호막 is refused and keeps its gauge.");
            }
            finally
            {
                rig.Dispose();
            }
        }

        private static void ValidateOverdrive()
        {
            TestRig rig = TestRig.Create("Overdrive", true);
            try
            {
                PlayerSP sp = rig.Player.GetComponent<PlayerSP>();
                sp.TrySpend(sp.CurrentSP);
                float attackSpeed = rig.Stats.AttackSpeed;
                float moveSpeed = rig.Stats.MoveSpeed;
                PlayerUltimate ultimate = rig.Player.GetComponent<PlayerUltimate>();
                FieldInfo nextReady = typeof(PlayerUltimate).GetField("nextReadyTime",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                nextReady.SetValue(ultimate, Time.time + 30f);
                int spBefore = sp.CurrentSP;
                rig.Use(Week27Power1Setup.SuddenAccelerationId);
                Assert(sp.CurrentSP == spBefore + 1, "급발진 restores one SP at once.");
                Assert(Mathf.Approximately(rig.Stats.AttackSpeed, attackSpeed * 1.6f) &&
                       Mathf.Approximately(rig.Stats.MoveSpeed, moveSpeed * 1.2f),
                    "급발진 gives attack speed +60% and move speed +20%.");
                rig.Effects.Advance(6f);
                // 6 seconds at 1 / 0.6 speed spend 10 seconds of cooldown: 4 beyond the 6 that real time passes.
                // Editor time does not move here, so only the extra 4 show.
                Assert(Mathf.Abs(ultimate.NextReadyTime - (Time.time + 26f)) < 0.05f,
                    "급발진 makes the high-grade skill cooldown run 40% shorter.");
                rig.Effects.Advance(9.1f);
                Assert(!rig.Effects.IsOverdriveActive && Mathf.Approximately(rig.Stats.AttackSpeed, attackSpeed) &&
                       Mathf.Approximately(rig.Stats.MoveSpeed, moveSpeed),
                    "급발진 ends after fifteen seconds.");
            }
            finally
            {
                rig.Dispose();
            }
        }

        // The shared stun (깜짝상자 will reuse it): a knockback during the stun never shortens it.
        private static void ValidateStun()
        {
            GameObject root = new("Power-1 Stun Verification");
            try
            {
                Health enemy = CreateEnemy(root, "Enemy", 100f, false);
                KnockbackReceiver receiver = enemy.GetComponent<KnockbackReceiver>();
                Assert(receiver.ApplyStun(3f) && receiver.IsStunned && receiver.IsActive,
                    "ApplyStun must stun the enemy.");
                receiver.Apply(Vector2.right, 5f, 0.1f, 0f);
                receiver.Tick(Time.time + 0.2f);
                Assert(receiver.IsStunned, "A knockback during a stun must not end the stun early.");
                receiver.Tick(Time.time + 3.1f);
                Assert(!receiver.IsActive, "The stun must end after its duration.");
                Assert(!receiver.ApplyStun(0f), "A zero stun must be ignored.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateHudRuntime()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GamePowerHudView configured = FindAll<GamePowerHudView>(scene).Single();
            TestRig rig = TestRig.Create("HUD");
            GameObject hudClone = Object.Instantiate(configured.gameObject);
            GameObject floor = new("Power-1 HUD Floor");
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                rig.Slot.Configure(rig.Progress, LoadPrefab());
                GamePowerHudView hud = hudClone.GetComponent<GamePowerHudView>();
                hud.Configure(rig.Slot, hud.Icon, hud.NameText, hud.GaugeText, hud.KeyText, hud.GaugeCells.ToArray());
                Assert(hud.NameText.text == GamePowerHudView.EmptyText && !hud.KeyText.gameObject.activeSelf &&
                       hud.GaugeCells.All(cell => !cell.gameObject.activeSelf),
                    "An empty 권능 slot must read 없음 without gauge cells or a use key.");

                ItemDefinition blanket = Load(Week27Power1Setup.SecurityBlanketId);
                Assert(rig.Slot.TryCollect(Spawn(LoadPrefab(), floor, blanket, "power1-hud")), "HUD slot must collect.");
                hud.RefreshNow();
                Assert(hud.NameText.text == blanket.DisplayName && hud.GaugeText.text == "0/3" &&
                       hud.GaugeCells.Count(cell => cell.gameObject.activeSelf) == 3 &&
                       hud.KeyText.text == GamePowerHudView.ChargingText,
                    "The HUD must show the name, three empty cells and that it is charging.");
                rig.Slot.AddCharge(3);
                hud.RefreshNow();
                Assert(hud.GaugeText.text == "3/3" &&
                       hud.GaugeCells.Take(3).All(cell => cell.color == GamePowerHudView.FilledCellColor) &&
                       hud.KeyText.text == GamePowerHudView.UsableText,
                    "A full gauge must show filled cells and [R] 사용.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                Object.DestroyImmediate(hudClone);
                Object.DestroyImmediate(floor);
                rig.Dispose();
            }
        }

        private static void ValidatePrefabAndPools()
        {
            ActivePowerPickup prefab = LoadPrefab();
            Assert(prefab.Display != null && prefab.Definition == null && string.IsNullOrEmpty(prefab.InstanceId) &&
                   !prefab.HasStoredCharge,
                "The 권능 pickup Prefab must be an empty template with a display renderer.");
            Collider2D collider = prefab.GetComponent<Collider2D>();
            Assert(collider != null && collider.isTrigger, "The 권능 pickup Prefab needs a trigger collider.");

            // Contract 23 §3, §9.3: 권능 never join the shared pools.
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = FindAll<RoomGraphAssembler>(scene).Single();
            Assert(assembler.SelectionRewardPool.All(item => item == null || !item.IsActivePower),
                "The selection reward pool (and the shop that shares it) must not hold a 권능.");
            ChestContentTable table = assembler.ChestContentTable;
            if (table != null)
            {
                Assert(table.Spells.Concat(table.JjangsemSpells).Concat(table.GoldenExclusiveArtifacts)
                        .All(item => item == null || !item.IsActivePower),
                    "Chest pools must not hold a 권능.");
            }
        }

        private static void ValidateGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerMovement player = FindAll<PlayerMovement>(scene).Single();
            PlayerPowerSlot[] slots = FindAll<PlayerPowerSlot>(scene);
            Assert(slots.Length == 1 && slots[0].gameObject == player.gameObject,
                "The Game Scene must have exactly one 권능 slot, on the player.");
            RunProgress progress = FindAll<RunProgress>(scene).Single();
            Assert(slots[0].Progress == progress &&
                   AssetDatabase.GetAssetPath(slots[0].PickupPrefab) == Week27Power1Setup.PickupPrefabPath,
                "The 권능 slot must reference the scene RunProgress and the pickup Prefab.");
            PlayerPowerEffects effects = player.GetComponent<PlayerPowerEffects>();
            Assert(effects != null && effects.RoomGraph == FindAll<RoomGraphController>(scene).Single(),
                "The 권능 executor must reference the scene room graph.");

            GamePowerHudView[] huds = FindAll<GamePowerHudView>(scene);
            Assert(huds.Length == 1 && huds[0].Slot == slots[0] && huds[0].Icon != null && huds[0].NameText != null &&
                   huds[0].GaugeText != null && huds[0].KeyText != null &&
                   huds[0].GaugeCells.Count == Week27Power1Setup.MaximumGaugeCells &&
                   huds[0].GaugeCells.All(cell => cell != null),
                "The Game HUD must have exactly one 권능 view bound to the player slot with five gauge cells.");
            Assert(huds[0].name == Week27Power1Setup.HudPanelName && huds[0].transform.parent != null &&
                   huds[0].transform.parent.name == "ReferenceFrame",
                "The 권능 HUD must live in the game HUD reference frame.");
            TMP_FontAsset font = huds[0].NameText.font;
            Assert(font != null && font.HasCharacters(GamePowerHudView.AllFixedText) &&
                   Week27Power1Setup.Specs.All(spec => font.HasCharacters(spec.Name)),
                "The HUD font must contain every fixed label and 권능 name glyph.");
            Assert(huds[0].GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget),
                "The 권능 HUD must not block input.");
        }

        private sealed class TestRig : IDisposable
        {
            public GameObject Root;
            public GameObject Player;
            public RunProgress Progress;
            public PlayerPowerSlot Slot;
            public PlayerPowerEffects Effects;
            public PlayerStats Stats;
            public PlayerInventory Inventory;
            private readonly List<ItemDefinition> loaded = new();
            private GameObject floor;

            public static TestRig Create(string label, bool withUltimate = false)
            {
                TestRig rig = new() { Root = new GameObject($"Power-1 {label} Verification") };
                GameObject progressObject = new("Progress", typeof(RunProgress));
                progressObject.transform.SetParent(rig.Root.transform);
                rig.Progress = progressObject.GetComponent<RunProgress>();
                rig.Player = new GameObject("Player", typeof(Rigidbody2D), typeof(Health), typeof(PlayerSP),
                    typeof(PlayerStats), typeof(PlayerInventory));
                rig.Player.GetComponent<Rigidbody2D>().gravityScale = 0f;
                rig.Player.transform.SetParent(rig.Root.transform);
                if (withUltimate) rig.Player.AddComponent<PlayerUltimate>();
                rig.Slot = rig.Player.AddComponent<PlayerPowerSlot>();
                rig.Effects = rig.Player.GetComponent<PlayerPowerEffects>();
                rig.Stats = rig.Player.GetComponent<PlayerStats>();
                rig.Inventory = rig.Player.GetComponent<PlayerInventory>();
                Health health = rig.Player.GetComponent<Health>();
                health.EnableHealthUnits();
                InvokeLifecycle(health, "Awake");
                InvokeLifecycle(rig.Player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(rig.Stats, "Awake");
                InvokeLifecycle(rig.Inventory, "Awake");
                InvokeLifecycle(rig.Effects, "Awake");
                rig.Slot.Configure(rig.Progress, null);
                InvokeLifecycle(rig.Slot, "Awake");
                rig.floor = new GameObject("Floor");
                rig.floor.transform.SetParent(rig.Root.transform);
                return rig;
            }

            // Collects the 권능, fills its gauge and uses it through the slot.
            public void Use(string itemId)
            {
                ItemDefinition power = Load(itemId);
                Slot.Configure(Progress, LoadPrefab());
                ActivePowerPickup pickup = Spawn(LoadPrefab(), floor, power, "power1-" + itemId);
                Assert(Slot.TryCollect(pickup) && Slot.HeldDefinition == power, $"The rig must hold {itemId}.");
                Slot.AddCharge(power.ActiveChargeRooms);
                PowerSlotUseResult result = Slot.TryUse();
                if (result != PowerSlotUseResult.Used)
                {
                    Effects.CanExecute(power, out string reason);
                    throw new InvalidOperationException($"{itemId} could not be used: {result} {reason}");
                }
            }

            public void Dispose() => Object.DestroyImmediate(Root);
        }

        private static Health CreateEnemy(GameObject root, string name, float maximumHealth, bool isBoss)
        {
            GameObject enemy = new(name, typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver));
            enemy.transform.SetParent(root.transform);
            enemy.GetComponent<Rigidbody2D>().gravityScale = 0f;
            Health health = enemy.GetComponent<Health>();
            SerializedObject serialized = new(health);
            serialized.FindProperty("maxHealth").floatValue = maximumHealth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(enemy.GetComponent<KnockbackReceiver>(), "Awake");
            if (isBoss) enemy.AddComponent<BossController>();
            return health;
        }

        private static void SkipHitInvulnerability(Health health)
        {
            DamageInvulnerability window = health.GetComponent<DamageInvulnerability>();
            if (window != null) window.ResetHitWindow();
        }

        private static ItemDefinition CreatePower(string itemId, int chargeRooms, int maxStacks = 1)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = itemId;
            definition.ConfigureContract(itemId, "검증용 권능", ItemKind.Active, ItemRarity.Rare, true, maxStacks,
                new ItemEffectEntry(ItemEffectType.PowerGainShield, 2f));
            definition.ConfigureActiveCharge(chargeRooms);
            return definition;
        }

        private static ItemDefinition Load(string itemId)
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Items/{itemId}.asset");
            Assert(definition != null, $"Run Power-1 setup first: {itemId} is missing.");
            return definition;
        }

        private static ActivePowerPickup LoadPrefab()
        {
            ActivePowerPickup prefab = AssetDatabase.LoadAssetAtPath<ActivePowerPickup>(Week27Power1Setup.PickupPrefabPath);
            Assert(prefab != null, "Run Power-1 setup first: the 권능 pickup Prefab is missing.");
            return prefab;
        }

        private static ActivePowerPickup Spawn(ActivePowerPickup prefab, GameObject floor, ItemDefinition definition,
            string instanceId)
        {
            ActivePowerPickup pickup = Object.Instantiate(prefab, floor.transform);
            pickup.Configure(definition, instanceId, ActivePowerPickup.NoStoredCharge, false);
            return pickup;
        }

        private static ActivePowerPickup[] FloorPickups(GameObject floor) => floor
            .GetComponentsInChildren<ActivePowerPickup>(true).Where(pickup => !pickup.IsCollected).ToArray();

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
