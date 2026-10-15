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
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Jjangsem-2: the four jjangsem spells of plan 26 §4.2 and their boundaries (docs/12 "짱셈스펠 4종 경계 조건").
    // 파스텔빛 pulses room damage, heals and SP for 18 seconds and ends on clear or exit, 수상한 물약 halves enemy hits in
    // its room for 10 seconds (rounded up, never below half a heart) and poisons the room's enemies three stacks deep,
    // 이성의 끈 doubles direct player damage in its room but not 권능, status ticks, bombs or pulses, and 깜짝상자 stuns
    // two random enemies and cuts half their maximum health (a boss 20% of its maximum) without killing,
    // shown like a hit; a stunned boss drops its pattern and restarts it from the telegraph. Each is refused and kept
    // where it would do nothing, and only diamond chests hold them.
    public static class Week26Jjangsem2Verification
    {
        public static void SetupAndVerifyBatch()
        {
            Week26Jjangsem2Setup.Setup();
            string[] guids = Week26Jjangsem2Setup.ItemIds
                .Select(id => AssetDatabase.AssetPathToGUID(Week26Jjangsem2Setup.PathOf(id))).ToArray();
            string tableGuid = AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath);
            Week26Jjangsem2Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   guids.SequenceEqual(Week26Jjangsem2Setup.ItemIds
                       .Select(id => AssetDatabase.AssetPathToGUID(Week26Jjangsem2Setup.PathOf(id)))) &&
                   tableGuid == AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath),
                "Jjangsem-2 setup changed an item or the chest content table GUID.");
            Verify();
        }

        // Also re-runs the other jjangsem spells and room-bound spells on the same executor, the 권능 that share the
        // enemy stun and room enemy list, the bosses whose patterns a stun now cancels, the chest pools and the panel.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week22Jjangsem0Verification.Verify();
            Week22Jjangsem1Verification.Verify();
            Week21Slot0Verification.Verify();
            Week21Spell1Verification.Verify();
            Week27Power1Verification.Verify();
            // Enemy-7 fails after Boss-4 in one session (Run state left behind), so it runs first. Boss-4 runs
            // Boss-0 to Boss-3 itself.
            Week25Enemy7Verification.Verify();
            Week15Boss4Verification.Verify();
            Week22Chest2Verification.Verify();
            Week22Chest1Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Jjangsem-2 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 26/Verify Jjangsem-2 Four Jjangsem Spells")]
        public static void Verify()
        {
            ValidateContract();
            ValidateAcquisitionPath();
            // The runtime checks create players and enemies; keep them out of the Game Scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            DamageCalculator.SetCriticalRollProviderForTesting(() => 1f);
            try
            {
                ValidatePastelLight();
                ValidateSuspiciousPotion();
                ValidateThreadOfReason();
                ValidateSurpriseBox();
                ValidateBossStunRestart();
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                DamageCalculator.ResetCriticalRollProvider();
                PlayerSingleUseEffects.ResetEnemyPickProvider();
            }

            Debug.Log("Jjangsem-2 verification passed: 파스텔빛 hits every enemy for 100% attack every 2.5s, heals " +
                      "half a heart every 3s and gives half an SP slot every 6s for 18s until the room is cleared or " +
                      "left, " +
                      "수상한 물약 halves enemy hits in its room for 10s (rounded up, at least half a heart) and " +
                      "poisons its enemies three stacks deep, 이성의 끈 doubles direct player damage in its room but " +
                      "not 권능, status ticks, bombs or pulses, 깜짝상자 stuns two random enemies for 2s and cuts " +
                      "half their maximum health (a boss 20%) without killing, shown like a hit, a " +
                      "stunned boss restarts its pattern from the telegraph, each is refused and kept where it would " +
                      "do nothing, and only diamond chests hold them.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.CurrentRoomPulseDamage == 73 && (int)ItemEffectType.CurrentRoomPulseHeal == 74 &&
                   (int)ItemEffectType.CurrentRoomEnemyDamageReduction == 75 &&
                   (int)ItemEffectType.CurrentRoomDamageMultiplier == 76 &&
                   (int)ItemEffectType.StunAndCutRandomEnemies == 77 && (int)ItemEffectType.PowerRestoreSP == 72 &&
                   (int)ItemEffectType.CurrentRoomPulseSP == 78 && (int)ItemEffectType.CurrentRoomEnemyPoison == 79,
                "Jjangsem-2 must add effect types 73-79 after the existing effects without renumbering them.");
            Assert((int)DamageSourceType.PlayerSpell == (int)DamageSourceType.PlayerPower + 1 &&
                   PlayerCombatEvents.IsPlayerDamage(DamageSourceType.PlayerSpell),
                "The pulse damage source must be appended and count as player damage.");
            Assert(!new ItemEffectEntry(ItemEffectType.CurrentRoomPulseDamage, 0.5f, configuredIntervalSeconds: 6f,
                       configuredDurationSeconds: 3f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentRoomPulseHeal, configuredIntervalSeconds: 3f,
                       configuredDurationSeconds: 18f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentRoomEnemyDamageReduction, 1f,
                       configuredDurationSeconds: 10f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentRoomEnemyDamageReduction, 0.5f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentRoomDamageMultiplier, 1f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.StunAndCutRandomEnemies, 0.5f,
                       configuredDurationSeconds: 2f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.StunAndCutRandomEnemies, 1f, configuredIntegerAmount: 2,
                       configuredDurationSeconds: 2f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.StunAndCutRandomEnemies, 0.5f, configuredSecondaryMagnitude: 1f,
                       configuredIntegerAmount: 2, configuredDurationSeconds: 2f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentRoomPulseSP, configuredIntervalSeconds: 6f,
                       configuredDurationSeconds: 18f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentRoomEnemyPoison, 0.15f, configuredIntervalSeconds: 1f,
                       configuredDurationSeconds: 10f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentRoomEnemyPoison, 0f, configuredIntegerAmount: 3,
                       configuredIntervalSeconds: 1f, configuredDurationSeconds: 10f).TryValidate(out _),
                "Each Jjangsem-2 effect must reject missing or out-of-range values.");

            AssertItem(Week26Jjangsem2Setup.PastelLightId, "파스텔빛",
                "현재 방에서 18초 동안 2.5초마다 모든 적에게 공격력 100% 피해 · 6초마다 SP 반 칸 회복 · 3초마다 HP 반 칸 회복 " +
                "(방 클리어·이탈 시 종료)",
                item => item.Effects.Count == 3 &&
                        item.Effects[0].EffectType == ItemEffectType.CurrentRoomPulseDamage &&
                        Approximately(item.Effects[0].Magnitude, 1f) &&
                        Approximately(item.Effects[0].IntervalSeconds, 2.5f) &&
                        Approximately(item.Effects[0].DurationSeconds, 18f) &&
                        item.Effects[1].EffectType == ItemEffectType.CurrentRoomPulseSP &&
                        item.Effects[1].IntegerAmount == 1 && Approximately(item.Effects[1].IntervalSeconds, 6f) &&
                        Approximately(item.Effects[1].DurationSeconds, 18f) &&
                        item.Effects[2].EffectType == ItemEffectType.CurrentRoomPulseHeal &&
                        item.Effects[2].IntegerAmount == 1 && Approximately(item.Effects[2].IntervalSeconds, 3f) &&
                        Approximately(item.Effects[2].DurationSeconds, 18f));
            AssertItem(Week26Jjangsem2Setup.SuspiciousPotionId, "수상한 물약",
                "현재 방의 적에게 받는 피해 10초간 50% 감소 (최소 반 칸) · 현재 방의 모든 적에게 중독 3중첩 (10초간 1초마다 " +
                "중첩당 공격력 15% 피해)",
                item => item.Effects.Count == 2 &&
                        item.Effects[0].EffectType == ItemEffectType.CurrentRoomEnemyDamageReduction &&
                        Approximately(item.Effects[0].Magnitude, 0.5f) &&
                        Approximately(item.Effects[0].DurationSeconds, 10f) &&
                        item.Effects[1].EffectType == ItemEffectType.CurrentRoomEnemyPoison &&
                        Approximately(item.Effects[1].Magnitude, 0.15f) && item.Effects[1].IntegerAmount == 3 &&
                        Approximately(item.Effects[1].IntervalSeconds, 1f) &&
                        Approximately(item.Effects[1].DurationSeconds, 10f));
            AssertItem(Week26Jjangsem2Setup.ThreadOfReasonId, "이성의 끈",
                "현재 방에서 주는 피해 2배 (권능·상태이상·폭탄 제외, 방 이탈 시 해제)",
                item => item.Effects.Count == 1 &&
                        item.Effects[0].EffectType == ItemEffectType.CurrentRoomDamageMultiplier &&
                        Approximately(item.Effects[0].Magnitude, 2f));
            AssertItem(Week26Jjangsem2Setup.SurpriseBoxId, "깜짝상자",
                "무작위 적 2명 2초 기절, 최대 체력 50% 감소 (보스는 20%)",
                item => item.Effects.Count == 1 &&
                        item.Effects[0].EffectType == ItemEffectType.StunAndCutRandomEnemies &&
                        Approximately(item.Effects[0].Magnitude, 0.5f) && item.Effects[0].IntegerAmount == 2 &&
                        Approximately(item.Effects[0].SecondaryMagnitude, 0.2f) &&
                        Approximately(item.Effects[0].DurationSeconds, 2f));
        }

        private static void AssertItem(string itemId, string displayName, string description,
            Func<ItemDefinition, bool> effectsMatch)
        {
            ItemDefinition item = Load(itemId);
            Assert(item.ItemId == itemId && item.DisplayName == displayName && item.Kind == ItemKind.JjangsemSpell &&
                   item.Rarity == ItemRarity.Rare && item.IsActive && item.MaxStacks == 1 && item.IsValid &&
                   item.IsSingleUse,
                $"{itemId} must be a valid active Rare jjangsem spell named {displayName}.");
            Assert(ItemDefinition.IsItemIdValidForKind(itemId, ItemKind.JjangsemSpell) &&
                   !ItemDefinition.IsItemIdValidForKind(itemId, ItemKind.SingleUseSpell),
                $"{itemId} must be a jjangsem spell ID only (Contract-0 §2.1).");
            Assert(effectsMatch(item), $"{itemId} does not carry the confirmed effect values (plan 26 §4.2).");
            string built = ArtifactEffectDescription.Build(item);
            Assert(built == description, $"Unexpected {itemId} description: {built}");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null && font.HasCharacters(displayName + built),
                $"The Frontend font is missing a {itemId} glyph.");
        }

        // Contract-0 §3: a jjangsem spell comes only from diamond chests. The F1 panel's spell slot Drop list
        // gathers every valid single-use asset, so the four appear there too.
        private static void ValidateAcquisitionPath()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ChestContentTable table =
                AssetDatabase.LoadAssetAtPath<ChestContentTable>(Week22Chest1Setup.ChestContentTablePath);
            Assert(table != null && table.TryValidate(out _), "The chest content table is missing or invalid.");
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            foreach (string itemId in Week26Jjangsem2Setup.ItemIds)
            {
                ItemDefinition item = Load(itemId);
                Assert(table.JjangsemSpells.Contains(item) && !table.Spells.Contains(item) &&
                       !table.GoldenExclusiveArtifacts.Contains(item),
                    $"{itemId} must be in the chest table's jjangsem spell list only.");
                bool diamondHolds = false;
                for (int index = 0; index < 8192 && !diamondHolds; index++)
                    diamondHolds = table.RollContents(RoomClearRewardSpawner.DeriveChestSeed(index * 104729 + 7),
                        ChestKind.Diamond).Spell == item;
                Assert(diamondHolds, $"A diamond chest must be able to hold {itemId}.");
                Assert(!assembler.SelectionRewardPool.Contains(item),
                    $"{itemId} must not join the selection reward pool or the shop stock that shares it.");
                Assert(item.IsSingleUse && item.IsValid, $"{itemId} must appear in the F1 spell slot Drop list.");
            }

            Assert(table.JjangsemSpells.Count == 6 &&
                   table.JjangsemSpells.Contains(Load(Week22Jjangsem0Setup.BigwoodFruitId)) &&
                   table.JjangsemSpells.Contains(Load(Week22Jjangsem1Setup.MeluneCardId)),
                "The diamond chest jjangsem list must hold the six implemented jjangsem spells.");
        }

        private static void ValidatePastelLight()
        {
            ItemDefinition pastel = Load(Week26Jjangsem2Setup.PastelLightId);
            using TestPlayer test = new();
            PlayerSingleUseEffects effects = test.Effects;
            List<Health> enemies = new() { test.CreateEnemy("Pastel A", 100f, false), test.CreateEnemy("Pastel Boss", 100f, true) };
            effects.ConfigureEnemySourceForVerification(() => enemies);

            test.Progress.RecordRoomEntry(1, 1);
            test.Give(pastel, "pastel-start");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HeldDefinition == pastel &&
                   !effects.IsPulseActive, "The start room must refuse 파스텔빛 and keep it.");
            test.Progress.RecordRoomEntry(1, 4);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HasItem,
                "A treasure room must refuse 파스텔빛.");
            test.Progress.GetRoomState(FloorGenerator.BuildRoomId(1, 2)).MarkPreCleared();
            test.Progress.RecordRoomEntry(1, 2);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HasItem,
                "A cleared combat room must refuse 파스텔빛.");

            test.Progress.RecordRoomEntry(1, 3);
            test.Health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, 6f));
            float hurt = test.Health.CurrentHealth;
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem && effects.IsPulseActive &&
                   effects.PulseRoomId == FloorGenerator.BuildRoomId(1, 3),
                "An uncleared combat room must consume 파스텔빛 and start its pulses.");
            test.Give(pastel, "pastel-again");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HasItem,
                "A second 파스텔빛 must be refused and kept while one runs in the same room.");

            PlayerSP sp = test.Health.GetComponent<PlayerSP>();
            Assert(sp.CurrentSP == 0 && !sp.HasHalfSP, "The test player must start with no SP.");
            effects.AdvanceJjangsemRoomEffects(2.4f);
            Assert(effects.PulseDamageDeliveredTicks == 0 && Approximately(enemies[0].CurrentHealth, 100f),
                "The first hit must wait one 2.5 second interval.");
            effects.AdvanceJjangsemRoomEffects(0.1f);
            Assert(effects.PulseDamageDeliveredTicks == 1 && Approximately(enemies[0].CurrentHealth, 90f) &&
                   Approximately(enemies[1].CurrentHealth, 90f) && effects.PulseHealDeliveredTicks == 0 &&
                   Approximately(test.Health.CurrentHealth, hurt),
                "Every 2.5 seconds every enemy, bosses included, must take 100% attack damage without a critical " +
                "hit, before the first heal.");
            effects.AdvanceJjangsemRoomEffects(0.5f);
            Assert(effects.PulseHealDeliveredTicks == 1 && Approximately(test.Health.CurrentHealth, hurt + 1f) &&
                   effects.PulseDamageDeliveredTicks == 1 && effects.PulseSPDeliveredTicks == 0,
                "Every 3 seconds half a heart (1 HP unit) must be healed.");
            Time.timeScale = 0f;
            effects.AdvanceJjangsemRoomEffects(5f);
            Time.timeScale = 1f;
            Assert(effects.PulseHealDeliveredTicks == 1 && effects.PulseDamageDeliveredTicks == 1,
                "Paused game time must not advance the pulses.");
            effects.AdvanceJjangsemRoomEffects(3f);
            Assert(effects.PulseDamageDeliveredTicks == 2 && effects.PulseHealDeliveredTicks == 2 &&
                   effects.PulseSPDeliveredTicks == 1 && sp.CurrentSP == 0 && sp.HasHalfSP &&
                   Approximately(enemies[0].CurrentHealth, 80f),
                "At 6 seconds half an SP slot must be gained.");

            Health late = test.CreateEnemy("Pastel Late", 100f, false);
            enemies.Add(late);
            effects.AdvanceJjangsemRoomEffects(12f);
            Assert(!effects.IsPulseActive && effects.PulseDamageDeliveredTicks == 0 &&
                   Approximately(enemies[0].CurrentHealth, 30f) && Approximately(late.CurrentHealth, 50f),
                "The pulse must deal seven hits in 18 seconds, reach enemies that appeared later and then end.");
            Assert(Approximately(test.Health.CurrentHealth, Mathf.Min(test.Health.MaxHealth, hurt + 6f)),
                "The pulse must heal six times (three hearts at most) in 18 seconds.");
            Assert(sp.CurrentSP == 1 && sp.HasHalfSP, "The pulse must give 1.5 SP slots in 18 seconds.");

            // Clearing the room ends it early; so does leaving it.
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && effects.IsPulseActive,
                "파스텔빛 must be usable again once the previous one ended.");
            test.Progress.GetRoomState(FloorGenerator.BuildRoomId(1, 3)).MarkCleared();
            effects.AdvanceJjangsemRoomEffects(6f);
            Assert(!effects.IsPulseActive && Approximately(enemies[0].CurrentHealth, 30f),
                "Clearing the room must end the pulse before its next hit.");

            test.Give(pastel, "pastel-boss");
            test.Progress.RecordRoomEntry(1, 5);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && effects.IsPulseActive,
                "An uncleared boss room must accept 파스텔빛.");
            test.Progress.RecordRoomEntry(1, 1);
            effects.AdvanceJjangsemRoomEffects(6f);
            Assert(!effects.IsPulseActive && Approximately(enemies[0].CurrentHealth, 30f),
                "Leaving the room must end the pulse.");

            test.Give(pastel, "pastel-death");
            test.Progress.RecordRoomEntry(1, 5);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "Start a pulse before death.");
            test.Health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, 1000f));
            effects.AdvanceJjangsemRoomEffects(6f);
            Assert(!effects.IsPulseActive && Approximately(enemies[0].CurrentHealth, 30f), "Death must end the pulse.");
        }

        private static void ValidateSuspiciousPotion()
        {
            ItemDefinition potion = Load(Week26Jjangsem2Setup.SuspiciousPotionId);
            using TestPlayer test = new();
            PlayerSingleUseEffects effects = test.Effects;
            SerializedObject serialized = new(test.Health);
            serialized.FindProperty("maxHealth").floatValue = 30f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            test.Health.ResetHealth();
            Health target = test.CreateEnemy("Potion Target", 1000f, false);
            Health late = test.CreateEnemy("Potion Late", 1000f, false);
            List<Health> enemies = new() { target };
            effects.ConfigureEnemySourceForVerification(() => enemies);

            test.Progress.RecordRoomEntry(1, 1);
            test.Give(potion, "potion-start");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HasItem &&
                   !effects.IsEnemyDamageReductionActive, "The start room must refuse 수상한 물약 and keep it.");

            test.Progress.RecordRoomEntry(1, 3);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && effects.IsEnemyDamageReductionActive &&
                   effects.EnemyDamageReductionRoomId == FloorGenerator.BuildRoomId(1, 3) &&
                   Approximately(test.Health.EnemyDamageReduction, 0.5f),
                "An uncleared combat room must consume 수상한 물약 and halve enemy hits there.");
            EnemyStatusEffects poison = target.GetComponent<EnemyStatusEffects>();
            Assert(poison != null && poison.PoisonStacks == 3 && Approximately(poison.PoisonTickDamagePerStack, 1.5f) &&
                   Approximately(poison.PoisonEndTime - Time.time, 10f),
                "수상한 물약 must poison every enemy in the room three stacks deep (15% attack per stack for 10 seconds).");
            poison.Tick(poison.NextPoisonTickTime);
            Assert(Approximately(target.CurrentHealth, 995.5f), "One poison tick must deal 3 x 1.5 damage.");
            enemies.Add(late);
            AssertHit(test, DamageSourceType.EnemyContact, 2f, false, 1f, "A one heart hit must become half a heart.");
            AssertHit(test, DamageSourceType.EnemyProjectile, 3f, false, 2f,
                "A 1.5 heart projectile must become one heart (rounded up to half hearts).");
            AssertHit(test, DamageSourceType.EnemyMelee, 7f, false, 4f, "A 3.5 heart melee hit must become 2 hearts.");
            AssertHit(test, DamageSourceType.EnemyContact, 1f, true, 1f,
                "A half heart hit must stay half a heart (never below half a heart).");
            AssertHit(test, DamageSourceType.Environment, 2f, false, 2f, "Trap damage is not an enemy hit.");

            test.Progress.RecordRoomEntry(1, 1);
            effects.AdvanceJjangsemRoomEffects(1f);
            Assert(effects.IsEnemyDamageReductionActive && Approximately(test.Health.EnemyDamageReduction, 0f),
                "Away from its room the potion must not reduce hits, while its timer keeps running.");
            AssertHit(test, DamageSourceType.EnemyContact, 2f, false, 2f, "Hits in another room must be whole.");
            test.Progress.RecordRoomEntry(1, 3);
            effects.AdvanceJjangsemRoomEffects(1f);
            Assert(Approximately(test.Health.EnemyDamageReduction, 0.5f) &&
                   Approximately(effects.EnemyDamageReductionRemainingSeconds, 8f),
                "Coming back within 10 seconds must find the room's effect again.");
            Time.timeScale = 0f;
            effects.AdvanceJjangsemRoomEffects(5f);
            Time.timeScale = 1f;
            Assert(Approximately(effects.EnemyDamageReductionRemainingSeconds, 8f),
                "Paused game time must not advance the potion.");

            test.Give(potion, "potion-refresh");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used &&
                   Approximately(effects.EnemyDamageReductionRemainingSeconds, 10f),
                "Using it again in the same room must restart the 10 seconds.");
            Assert(poison.PoisonStacks == 3 && late.GetComponent<EnemyStatusEffects>()?.PoisonStacks == 3,
                "Poison must stop at three stacks and reach the enemies in the room at use.");
            effects.AdvanceJjangsemRoomEffects(10f);
            Assert(!effects.IsEnemyDamageReductionActive && Approximately(test.Health.EnemyDamageReduction, 0f),
                "After 10 seconds the potion must end.");
            AssertHit(test, DamageSourceType.EnemyContact, 2f, false, 2f, "After the potion hits must be whole.");

            test.Give(potion, "potion-death");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "Start a potion before death.");
            test.Health.TakeDamage(new DamageContext(null, DamageSourceType.Environment, 1000f));
            effects.AdvanceJjangsemRoomEffects(0.1f);
            Assert(!effects.IsEnemyDamageReductionActive, "Death must end the potion.");
        }

        private static void AssertHit(TestPlayer test, DamageSourceType sourceType, float damage, bool halfHeart,
            float expected, string message)
        {
            float before = test.Health.CurrentHealth;
            test.Health.TakeDamage(new DamageContext(null, sourceType, damage, allowsHalfHeart: halfHeart));
            Assert(Approximately(before - test.Health.CurrentHealth, expected),
                $"{message} Took {before - test.Health.CurrentHealth}.");
        }

        private static void ValidateThreadOfReason()
        {
            ItemDefinition reason = Load(Week26Jjangsem2Setup.ThreadOfReasonId);
            using TestPlayer test = new();
            PlayerSingleUseEffects effects = test.Effects;
            Health enemy = test.CreateEnemy("Reason Target", 10000f, false);
            GameObject player = test.Stats.gameObject;

            test.Progress.RecordRoomEntry(1, 4);
            test.Give(reason, "reason-treasure");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HasItem &&
                   !effects.IsRoomDamageMultiplierActive, "A treasure room must refuse 이성의 끈 and keep it.");

            test.Progress.RecordRoomEntry(1, 3);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && effects.IsRoomDamageMultiplierActive &&
                   Approximately(test.Stats.SingleUseRoomDamageMultiplier, 2f),
                "An uncleared combat room must consume 이성의 끈.");
            AssertEnemyHit(enemy, test.Stats.CreateDirectDamageContext(player, DamageSourceType.PlayerProjectile), 20f,
                "A basic attack must deal double damage.");
            AssertEnemyHit(enemy, test.Stats.CreateDirectDamageContext(player, DamageSourceType.PlayerSkillExplosion),
                20f, "A skill hit must deal double damage.");
            AssertEnemyHit(enemy, new DamageContext(player, DamageSourceType.PlayerItemLightning, 10f), 20f,
                "Artifact lightning must deal double damage.");
            AssertEnemyHit(enemy, new DamageContext(player, DamageSourceType.PlayerDamageAura, 5f,
                deliveryType: DamageDeliveryType.Periodic), 10f, "An artifact aura must deal double damage.");
            AssertEnemyHit(enemy, new DamageContext(player, DamageSourceType.PlayerPower, 10f), 10f,
                "권능 damage must not be doubled.");
            AssertEnemyHit(enemy, new DamageContext(player, DamageSourceType.PlayerStatusEffect, 5f,
                deliveryType: DamageDeliveryType.Periodic), 5f, "Status ticks must not be doubled.");
            AssertEnemyHit(enemy, new DamageContext(player, DamageSourceType.PlayerBomb, 10f), 10f,
                "Bombs must not be doubled.");
            AssertEnemyHit(enemy, new DamageContext(player, DamageSourceType.PlayerSpell, 5f,
                deliveryType: DamageDeliveryType.Periodic), 5f, "파스텔빛 pulses must not be doubled.");
            enemy.ApplyIncomingDamageBonus(0.5f, 10f);
            AssertEnemyHit(enemy, new DamageContext(player, DamageSourceType.PlayerItemLightning, 10f), 30f,
                "이성의 끈 must multiply with other damage bonuses.");

            test.Give(reason, "reason-again");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HasItem,
                "A second 이성의 끈 must be refused and kept in a room where it is active.");
            test.Progress.GetRoomState(FloorGenerator.BuildRoomId(1, 3)).MarkCleared();
            effects.AdvanceJjangsemRoomEffects(1f);
            Assert(effects.IsRoomDamageMultiplierActive, "Clearing the room must keep it until the player leaves.");
            test.Progress.RecordRoomEntry(1, 1);
            effects.AdvanceJjangsemRoomEffects(0f);
            Assert(!effects.IsRoomDamageMultiplierActive && Approximately(test.Stats.SingleUseRoomDamageMultiplier, 1f),
                "Leaving the room must end 이성의 끈.");
            test.Progress.RecordRoomEntry(1, 3);
            effects.AdvanceJjangsemRoomEffects(0f);
            Assert(!effects.IsRoomDamageMultiplierActive, "Revisiting the room must not bring it back.");

            test.Progress.RecordRoomEntry(1, 5);
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used, "An uncleared boss room must accept 이성의 끈.");
            test.Progress.StopProgression();
            effects.AdvanceJjangsemRoomEffects(0f);
            Assert(!effects.IsRoomDamageMultiplierActive && Approximately(test.Stats.SingleUseRoomDamageMultiplier, 1f),
                "The end of the Run must end 이성의 끈.");
        }

        private static void AssertEnemyHit(Health enemy, DamageContext context, float expected, string message)
        {
            float before = enemy.CurrentHealth;
            enemy.TakeDamage(context);
            Assert(Approximately(before - enemy.CurrentHealth, expected),
                $"{message} Took {before - enemy.CurrentHealth}.");
        }

        private static void ValidateSurpriseBox()
        {
            ItemDefinition box = Load(Week26Jjangsem2Setup.SurpriseBoxId);
            using TestPlayer test = new();
            PlayerSingleUseEffects effects = test.Effects;
            List<Health> enemies = new();
            effects.ConfigureEnemySourceForVerification(() => enemies);

            test.Progress.RecordRoomEntry(1, 3);
            test.Give(box, "box-empty");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && test.Slot.HasItem,
                "A room without enemies must refuse 깜짝상자 and keep it.");

            Health first = test.CreateEnemy("Box A", 100f, false);
            Health boss = test.CreateEnemy("Box Boss", 400f, true);
            Health third = test.CreateEnemy("Box C", 100f, false);
            enemies.AddRange(new[] { first, boss, third });
            Queue<int> picks = new(new[] { 1, 0 });
            PlayerSingleUseEffects.SetEnemyPickProviderForTesting(count => picks.Count > 0 ? picks.Dequeue() : 0);
            List<(Health target, DamageContext context, float damage)> shown = new();
            void Record(Health target, DamageContext context, DamageResult result) =>
                shown.Add((target, context, result.FinalDamage));
            int applied = 0;
            first.DamageApplied += (_, damage, _) => applied += damage > 0f ? 1 : 0;
            Health.AnyDamageResolved += Record;
            try
            {
                Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && !test.Slot.HasItem,
                    "A room with enemies must consume 깜짝상자.");
            }
            finally
            {
                Health.AnyDamageResolved -= Record;
            }

            Assert(effects.LastSurpriseTargets.Count == 2 && effects.LastSurpriseTargets[0] == boss &&
                   effects.LastSurpriseTargets[1] == first,
                "깜짝상자 must pick two different random enemies, bosses included.");
            Assert(Approximately(boss.CurrentHealth, 320f) && Approximately(first.CurrentHealth, 50f) &&
                   Approximately(third.CurrentHealth, 100f),
                "A picked enemy must lose half its maximum health, a boss 20% of its maximum, and the rest none.");
            Assert(shown.Count == 2 && shown[0].target == boss && Approximately(shown[0].damage, 80f) &&
                   shown[1].target == first && Approximately(shown[1].damage, 50f) &&
                   shown.All(entry => entry.context.SourceType == DamageSourceType.PlayerSpell &&
                                      entry.context.Source == test.Stats.gameObject) && applied == 1,
                "The cut must be reported like a player hit so the damage number, health bar and hit flash show.");
            Assert(Approximately(boss.CutHealth(0.2f, default), 80f) && Approximately(boss.CurrentHealth, 240f),
                "A second cut must take the same share of the maximum health, not of the current health.");
            Assert(boss.GetComponent<KnockbackReceiver>().IsStunned && first.GetComponent<KnockbackReceiver>().IsStunned &&
                   !third.GetComponent<KnockbackReceiver>().IsActive,
                "The picked enemies, bosses included, must be stunned.");
            KnockbackReceiver receiver = first.GetComponent<KnockbackReceiver>();
            receiver.Tick(Time.time + 1.9f);
            Assert(receiver.IsStunned, "The stun must last 2 seconds.");
            receiver.Tick(Time.time + 2.05f);
            Assert(!receiver.IsActive, "The stun must end after 2 seconds.");

            // Never a kill: a weak enemy keeps a sliver.
            enemies.Clear();
            Health weak = test.CreateEnemy("Box Weak", 100f, false);
            weak.TakeDamage(new DamageContext(null, DamageSourceType.Unknown, 99f));
            enemies.Add(weak);
            test.Give(box, "box-single");
            Assert(test.Slot.TryUse() == SpellSlotUseResult.Used && effects.LastSurpriseTargets.Count == 1 &&
                   effects.LastSurpriseTargets[0] == weak,
                "One enemy must be the only target.");
            float sliver = weak.CurrentHealth;
            Assert(!weak.IsDead && sliver > 0f && sliver < 0.01f,
                "A cut larger than the remaining health must leave a sliver and never kill.");
            weak.SetInvulnerable(true);
            Assert(Approximately(weak.CutHealth(0.5f, default), 0f) && Approximately(weak.CurrentHealth, sliver),
                "An invulnerable enemy must not lose health to the cut.");
        }

        // The shared stun (명치 꽂기, 깜짝상자) cancels a boss pattern under way; it restarts from the telegraph.
        private static void ValidateBossStunRestart()
        {
            GameObject root = new("Jjangsem-2 Boss Stun");
            try
            {
                GameObject target = new("Target");
                target.transform.SetParent(root.transform);
                GameObject bossObject = new("Boss", typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver),
                    typeof(BossController));
                bossObject.transform.SetParent(root.transform);
                bossObject.GetComponent<Rigidbody2D>().gravityScale = 0f;
                InvokeLifecycle(bossObject.GetComponent<Health>(), "Awake");
                InvokeLifecycle(bossObject.GetComponent<KnockbackReceiver>(), "Awake");
                BossController boss = bossObject.GetComponent<BossController>();
                InvokeLifecycle(boss, "Awake");
                KnockbackReceiver receiver = bossObject.GetComponent<KnockbackReceiver>();
                boss.ConfigurePatterns(new[]
                {
                    new BossPatternDefinition("verify-a", BossPatternExecution.SignalOnly, 1f, 1f, 1f, 0f),
                    new BossPatternDefinition("verify-b", BossPatternExecution.SignalOnly, 1f, 1f, 1f, 0f),
                });

                float now = Time.time;
                boss.BeginCombat(target.transform, 7, now);
                string pattern = boss.CurrentPatternId;
                Assert(boss.State == BossActionState.Telegraph && pattern != null, "The boss must start a telegraph.");
                Assert(receiver.ApplyStun(2f) && boss.State == BossActionState.Idle && boss.HasStunInterruptedPattern,
                    "A stun must cancel the telegraphed pattern.");
                boss.TickBehavior(now + 1.5f);
                Assert(boss.State == BossActionState.Idle, "The boss must not act while stunned.");
                receiver.Tick(now + 2.05f);
                boss.TickBehavior(now + 2.1f);
                Assert(boss.State == BossActionState.Telegraph && boss.CurrentPatternId == pattern &&
                       Approximately(boss.StateEndsAt, now + 3.1f) && !boss.HasStunInterruptedPattern,
                    "After the stun the same pattern must start again from its telegraph.");

                boss.TickBehavior(now + 3.2f);
                Assert(boss.State == BossActionState.Active, "The restarted telegraph must lead to the pattern.");
                Assert(receiver.ApplyStun(2f) && boss.State == BossActionState.Idle,
                    "A stun during the pattern must cancel it.");
                receiver.Tick(now + 10f);
                boss.TickBehavior(now + 10f);
                Assert(boss.State == BossActionState.Telegraph && boss.CurrentPatternId == pattern,
                    "A pattern cancelled while running must restart from its telegraph.");

                boss.TickBehavior(now + 11.1f);
                boss.TickBehavior(now + 12.2f);
                Assert(boss.State == BossActionState.Recovery, "The boss must reach its recovery.");
                Assert(receiver.ApplyStun(0.5f) && boss.State == BossActionState.Recovery &&
                       !boss.HasStunInterruptedPattern, "A stun during recovery has no pattern to cancel.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ItemDefinition Load(string itemId)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Items/{itemId}.asset");
            Assert(item != null, $"Run the jjangsem setups first: {itemId} is missing.");
            return item;
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException($"Field {fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static bool Approximately(float actual, float expected) => Mathf.Abs(actual - expected) <= 0.001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        // A player in health units with the real slot and executor on a two-floor test graph: start, two combat
        // rooms, treasure and boss on floor 1, and a start room on floor 2.
        private sealed class TestPlayer : IDisposable
        {
            private readonly GameObject progressObject;
            private readonly GameObject floor;
            private readonly GameObject player;
            private readonly GameObject enemyRoot;
            private readonly SingleUseItemPickup prefab;

            public RunProgress Progress { get; }
            public Health Health { get; }
            public PlayerStats Stats { get; }
            public PlayerSpellSlot Slot { get; }
            public PlayerSingleUseEffects Effects { get; }

            public TestPlayer()
            {
                prefab = AssetDatabase.LoadAssetAtPath<SingleUseItemPickup>(Week21Slot0Setup.PickupPrefabPath);
                Assert(prefab != null, "Run Slot-0 setup first: the single-use pickup Prefab is missing.");
                progressObject = new GameObject("Jjangsem-2 Progress", typeof(RunProgress));
                floor = new GameObject("Jjangsem-2 Floor");
                enemyRoot = new GameObject("Jjangsem-2 Enemies");
                Progress = progressObject.GetComponent<RunProgress>();
                ConfigureTestGraph(Progress);
                player = new GameObject("Jjangsem-2 Player", typeof(Health), typeof(PlayerSP), typeof(PlayerStats),
                    typeof(PlayerInventory), typeof(PlayerSpellSlot));
                Health = player.GetComponent<Health>();
                Stats = player.GetComponent<PlayerStats>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                Slot = player.GetComponent<PlayerSpellSlot>();
                Effects = player.GetComponent<PlayerSingleUseEffects>();
                SetField(inventory, "runProgress", Progress);
                Health.EnableHealthUnits();
                InvokeLifecycle(Health, "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(Stats, "Awake");
                InvokeLifecycle(inventory, "Awake");
                Slot.Configure(Progress, prefab);
                InvokeLifecycle(Slot, "Awake");
                InvokeLifecycle(Effects, "Awake");
                Assert(Approximately(Stats.AttackDamage, 10f), "The test player must start with 10 attack.");
            }

            public void Give(ItemDefinition item, string instanceId)
            {
                SingleUseItemPickup pickup = Object.Instantiate(prefab, floor.transform);
                pickup.Configure(item, instanceId, false);
                Assert(Slot.TryCollect(pickup) && Slot.HeldDefinition == item, $"The slot must take {item.ItemId}.");
            }

            public Health CreateEnemy(string name, float maximumHealth, bool isBoss)
            {
                GameObject enemy = new(name, typeof(Rigidbody2D), typeof(Health), typeof(KnockbackReceiver));
                enemy.transform.SetParent(enemyRoot.transform);
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

            public void Dispose()
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(enemyRoot);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(progressObject);
            }

            private static void ConfigureTestGraph(RunProgress progress)
            {
                GeneratedRoomNode[] firstNodes =
                {
                    Node(1, 1, GeneratedRoomRole.Start),
                    Node(1, 2, GeneratedRoomRole.Intermediate),
                    Node(1, 3, GeneratedRoomRole.Intermediate),
                    Node(1, 4, GeneratedRoomRole.Treasure),
                    Node(1, 5, GeneratedRoomRole.Boss),
                };
                GeneratedRoomNode[] secondNodes = { Node(2, 1, GeneratedRoomRole.Start) };
                GeneratedFloor first = new(1, 0, 0, 0, firstNodes[0].RoomId, firstNodes[4].RoomId, firstNodes);
                GeneratedFloor second = new(2, 0, 0, 0, secondNodes[0].RoomId, null, secondNodes);
                FieldInfo graphField = typeof(RunProgress).GetField("<GeneratedGraph>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (graphField == null) throw new InvalidOperationException("RunProgress graph storage was not found.");
                graphField.SetValue(progress, new GeneratedFloorGraph(new[] { first, second }, 1));

                FieldInfo statesField = typeof(RunProgress).GetField("roomStates",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Dictionary<string, RoomRunState> states =
                    (Dictionary<string, RoomRunState>)statesField?.GetValue(progress);
                if (states == null) throw new InvalidOperationException("RunProgress room state storage was not found.");
                foreach (GeneratedRoomNode node in firstNodes.Concat(secondNodes))
                    states.Add(node.RoomId, new RoomRunState(node.RoomId));
            }

            private static GeneratedRoomNode Node(int floorNumber, int roomNumber, GeneratedRoomRole role) =>
                new(FloorGenerator.BuildRoomId(floorNumber, roomNumber), floorNumber, roomNumber, role, null);
        }
    }
}
