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
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Spell-6: the five spells of plan 26 §4.3. 네티의 지도 reveals the current floor on the minimap (the secret room by
    // its position only) and gives 5 gold even with a full wallet, 에슈르의 마도서 makes basic attack shots home in on
    // enemies in its room (coefficient 0.5 added per use, range 4, front 180°), 티그의 도전장 moves to the boss room and
    // starts the fight, 골디의 초대장 turns the floor's secret room into the 골디 shop and moves inside, both resetting a
    // room left mid-fight without a second transition, and 엘리스의 타로카드 shifts stats for the Run from the Run seed and
    // the use count. Each is refused and kept where it would do nothing, and chests and rare obstacle drops hold them.
    public static class Week26Spell6Verification
    {
        private const int MaximumSeedSearch = 4096;

        public static void SetupAndVerifyBatch()
        {
            SpriteRenderer chestVisual = AssetDatabase.LoadAssetAtPath<GameObject>(Week22Chest0Setup.ChestPrefabPath)
                .GetComponent<TreasureChest>().Visual;
            Sprite chestSprite = chestVisual.sprite;
            Color chestColor = chestVisual.color;
            Week26Spell6Setup.Setup();
            string[] guids = Week26Spell6Setup.ItemIds
                .Select(id => AssetDatabase.AssetPathToGUID(Week26Spell6Setup.PathOf(id))).ToArray();
            string tableGuid = AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath);
            Week26Spell6Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   guids.SequenceEqual(Week26Spell6Setup.ItemIds
                       .Select(id => AssetDatabase.AssetPathToGUID(Week26Spell6Setup.PathOf(id)))) &&
                   tableGuid == AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath),
                "Spell-6 setup changed an item or the chest content table GUID.");
            chestVisual = AssetDatabase.LoadAssetAtPath<GameObject>(Week22Chest0Setup.ChestPrefabPath)
                .GetComponent<TreasureChest>().Visual;
            Assert(chestVisual.sprite == chestSprite && chestVisual.color == chestColor,
                "Rebuilding the chest table must keep the chest Prefab's drawn sprite and colour.");
            Verify();
        }

        // Also re-runs what the five spells share: the escape reset and room boosts on the same executor, the gold
        // spell, the secret room, 골디 shop and minimap, the basic attack shot (range, bounce), the chest and obstacle
        // spell pools and the panel.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week21Spell1Verification.Verify();
            Week21Spell2Verification.Verify();
            Week22Spell5Verification.Verify();
            Week26Jjangsem2Verification.Verify();
            Week20Special3Verification.Verify();
            Week23Shop1Verification.Verify();
            Week14Room5Verification.Verify();
            Week21Range0Verification.Verify();
            Week23Passive3Verification.Verify();
            Week23Obstacle5Verification.Verify();
            Week22Chest2Verification.Verify();
            Week22Chest1Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Spell-6 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 26/Verify Spell-6 Five Spells")]
        public static void Verify()
        {
            ValidateContract();
            RoomGraphAssembler assembler = ValidateAcquisitionPath();
            ValidateTarotDraw();
            ValidateNettiMap(assembler);
            ValidateEshurGrimoire(assembler);
            ValidateTigChallenge(assembler);
            ValidateGoldiInvitation(assembler);
            ValidateEllisTarot(assembler);
            // The shot and stat checks create their own objects; keep them out of the Game Scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ValidateHomingSteering();
            ValidateTarotStats();
            Debug.Log("Spell-6 verification passed: 네티의 지도 reveals the floor map (secret room position only) and " +
                      "gives 5 gold even at 99, 에슈르의 마도서 homes basic attack shots in its uncleared room " +
                      "(0.5 per use, range 4, front 180°, keeps speed) until the room is left, 티그의 도전장 moves to " +
                      "the boss room door entry and starts the fight, 골디의 초대장 turns the secret room into the 골디 " +
                      "shop and moves inside, both resetting a room left mid-fight once, 엘리스의 타로카드 replays its " +
                      "seeded draws in order and shifts stats for the Run, each is refused and kept where it would do " +
                      "nothing, and chests and rare obstacle drops hold the five.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.CurrentRoomEnemyPoison == 79 &&
                   (int)ItemEffectType.RevealCurrentFloorMap == 80 && (int)ItemEffectType.GainGold == 81 &&
                   (int)ItemEffectType.CurrentRoomBasicAttackHoming == 82 &&
                   (int)ItemEffectType.TeleportToBossRoom == 83 && (int)ItemEffectType.TeleportToGoldiShop == 84 &&
                   (int)ItemEffectType.RandomStatShift == 85,
                "Spell-6 must add effect types 80-85 after the existing effects without renumbering them.");
            Assert(new ItemEffectEntry(ItemEffectType.RevealCurrentFloorMap).TryValidate(out _) &&
                   new ItemEffectEntry(ItemEffectType.TeleportToBossRoom).TryValidate(out _) &&
                   new ItemEffectEntry(ItemEffectType.TeleportToGoldiShop).TryValidate(out _) &&
                   new ItemEffectEntry(ItemEffectType.RandomStatShift).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.GainGold).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentRoomBasicAttackHoming, 0.5f).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.CurrentRoomBasicAttackHoming,
                       configuredRadius: 4f).TryValidate(out _),
                "Spell-6 effects must reject missing gold, homing coefficient or range values.");

            AssertItem(Week26Spell6Setup.NettiMapId, "네티의 지도", ItemRarity.Uncommon,
                "현재 층 지도 전체 공개 (비밀방은 위치만 표시) · 골드 +5 (보유 한도 99까지)",
                item => item.Effects.Count == 2 &&
                        item.Effects[0].EffectType == ItemEffectType.RevealCurrentFloorMap &&
                        item.Effects[1].EffectType == ItemEffectType.GainGold && item.Effects[1].IntegerAmount == 5);
            AssertItem(Week26Spell6Setup.EshurGrimoireId, "에슈르의 마도서", ItemRarity.Uncommon,
                "현재 방에서 기본 공격이 앞쪽 4m 안의 적을 유도 추적 (유도 계수 0.5, 방 이탈 시 해제)",
                item => item.Effects.Count == 1 &&
                        item.Effects[0].EffectType == ItemEffectType.CurrentRoomBasicAttackHoming &&
                        Approximately(item.Effects[0].Magnitude, 0.5f) && Approximately(item.Effects[0].Radius, 4f));
            AssertItem(Week26Spell6Setup.TigChallengeId, "티그의 도전장", ItemRarity.Rare,
                "현재 층 보스방으로 즉시 이동해 보스전 시작 (보스방 안에서는 사용 불가)",
                item => item.Effects.Count == 1 && item.Effects[0].EffectType == ItemEffectType.TeleportToBossRoom);
            AssertItem(Week26Spell6Setup.GoldiInvitationId, "골디의 초대장", ItemRarity.Rare,
                "현재 층 비밀방을 골디 상점으로 바꾸고 그 안으로 이동 (전투 중·비밀방이 없거나 보상을 받았으면 사용 불가)",
                item => item.Effects.Count == 1 && item.Effects[0].EffectType == ItemEffectType.TeleportToGoldiShop);
            AssertItem(Week26Spell6Setup.EllisTarotId, "엘리스의 타로카드", ItemRarity.Rare,
                "Run 동안 무작위 능력치 1~2개 증가, 30% 확률로 다른 1~2개 감소 (공격력 ±8%, 공격 속도 ±8%, " +
                "이동 속도 ±5%, 사거리 ±10%, 탄속 ±10%, 치명타 확률 ±5%p, 치명타 피해 ±20%p)",
                item => item.Effects.Count == 1 && item.Effects[0].EffectType == ItemEffectType.RandomStatShift);
        }

        private static void AssertItem(string itemId, string displayName, ItemRarity rarity, string description,
            Func<ItemDefinition, bool> effectsMatch)
        {
            ItemDefinition item = Load(itemId);
            Assert(item.ItemId == itemId && item.DisplayName == displayName && item.Kind == ItemKind.SingleUseSpell &&
                   item.Rarity == rarity && item.IsActive && item.MaxStacks == 1 && item.IsValid && item.IsSingleUse,
                $"{itemId} must be a valid active {rarity} single-use spell named {displayName}.");
            Assert(ItemDefinition.IsItemIdValidForKind(itemId, ItemKind.SingleUseSpell) &&
                   !ItemDefinition.IsItemIdValidForKind(itemId, ItemKind.JjangsemSpell),
                $"{itemId} must be a single-use spell ID only (Contract-0 §2.1).");
            Assert(effectsMatch(item), $"{itemId} does not carry the confirmed effect values (plan 26 §4.3).");
            string built = ArtifactEffectDescription.Build(item);
            Assert(built == description, $"Unexpected {itemId} description: {built}");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null && font.HasCharacters(displayName + built),
                $"The Frontend font is missing a {itemId} glyph.");
        }

        // Contract-0 §3: a spell comes from chests (and the rare obstacle drops that hold every spell). The F1 panel's
        // spell slot Drop list gathers every valid single-use asset, so the five appear there too.
        private static RoomGraphAssembler ValidateAcquisitionPath()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ChestContentTable table =
                AssetDatabase.LoadAssetAtPath<ChestContentTable>(Week22Chest1Setup.ChestContentTablePath);
            Assert(table != null && table.TryValidate(out _), "The chest content table is missing or invalid.");
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            ObstacleVariantDefinition[] rareVariants = AssetDatabase.FindAssets("t:ObstacleVariantDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ObstacleVariantDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(variant => variant != null && variant.RareItemChance > 0f).ToArray();
            Assert(rareVariants.Length > 0, "Some obstacle variant must drop a rare spell.");
            foreach (string itemId in Week26Spell6Setup.ItemIds)
            {
                ItemDefinition item = Load(itemId);
                Assert(table.Spells.Contains(item) && !table.JjangsemSpells.Contains(item) &&
                       !table.GoldenExclusiveArtifacts.Contains(item),
                    $"{itemId} must be in the chest table's spell list only.");
                bool chestHolds = false;
                for (int index = 0; index < 16384 && !chestHolds; index++)
                    chestHolds = table.RollContents(RoomClearRewardSpawner.DeriveChestSeed(index * 104729 + 7),
                        ChestKind.Diamond).Spell == item;
                Assert(chestHolds, $"A chest must be able to hold {itemId}.");
                Assert(rareVariants.All(variant => variant.RareSpells.Contains(item)),
                    $"Every rare-spell obstacle variant must be able to drop {itemId}.");
                Assert(!assembler.SelectionRewardPool.Contains(item),
                    $"{itemId} must not join the selection reward pool or the shop stock that shares it.");
                Assert(item.IsSingleUse && item.IsValid, $"{itemId} must appear in the F1 spell slot Drop list.");
            }

            return assembler;
        }

        // The draw is a pure function of the Run seed and the draw index.
        private static void ValidateTarotDraw()
        {
            const int samples = 20000;
            int singleRaises = 0, withDrops = 0, singleDrops = 0;
            HashSet<TarotStat> raisedStats = new(), droppedStats = new();
            bool indexChangesDraw = false;
            for (int seed = 1; seed <= samples; seed++)
            {
                List<TarotStatChange> draw = TarotCardDraw.Draw(seed, 0);
                Assert(draw.Select(change => change.Stat).SequenceEqual(TarotCardDraw.Draw(seed, 0)
                           .Select(change => change.Stat)),
                    "The same Run seed and draw index must replay the same card.");
                indexChangesDraw |= !Same(draw, TarotCardDraw.Draw(seed, 1));
                int raises = draw.Count(change => change.Amount > 0f);
                int drops = draw.Count(change => change.Amount < 0f);
                Assert(raises is 1 or 2 && drops is 0 or 1 or 2 && draw.Count == raises + drops &&
                       draw.Take(raises).All(change => change.Amount > 0f) &&
                       draw.Select(change => change.Stat).Distinct().Count() == draw.Count,
                    "A card raises one or two stats, then may lower one or two other stats.");
                Assert(draw.All(change => Approximately(Mathf.Abs(change.Amount), TarotCardDraw.StepOf(change.Stat))),
                    "Each stat moves by its fixed step.");
                singleRaises += raises == 1 ? 1 : 0;
                withDrops += drops > 0 ? 1 : 0;
                singleDrops += drops == 1 ? 1 : 0;
                foreach (TarotStatChange change in draw)
                    (change.Amount > 0f ? raisedStats : droppedStats).Add(change.Stat);
            }

            Assert(indexChangesDraw, "Later cards of the same Run must draw again.");
            Assert(Near(singleRaises, samples, 0.6f, 0.02f) && Near(withDrops, samples, 0.3f, 0.02f) &&
                   Near(singleDrops, withDrops, 0.7f, 0.03f),
                $"Draw odds must be 60/40 raises, 30% drops and 70/30 drops: {singleRaises}, {withDrops}, " +
                $"{singleDrops} of {samples}.");
            Assert(raisedStats.Count == 7 && droppedStats.Count == 7 &&
                   !raisedStats.Concat(droppedStats).Any(stat => stat.ToString().Contains("Health")),
                "Every one of the seven stats (HP and SP excluded) must be drawable both ways.");
            Assert(Approximately(TarotCardDraw.StepOf(TarotStat.AttackDamage), 0.08f) &&
                   Approximately(TarotCardDraw.StepOf(TarotStat.AttackSpeed), 0.08f) &&
                   Approximately(TarotCardDraw.StepOf(TarotStat.MoveSpeed), 0.05f) &&
                   Approximately(TarotCardDraw.StepOf(TarotStat.Range), 0.1f) &&
                   Approximately(TarotCardDraw.StepOf(TarotStat.ShotSpeed), 0.1f) &&
                   Approximately(TarotCardDraw.StepOf(TarotStat.CriticalChance), 0.05f) &&
                   Approximately(TarotCardDraw.StepOf(TarotStat.CriticalDamage), 0.2f),
                "The tarot steps must be the confirmed values (plan 26 §4.3).");
        }

        private static void ValidateNettiMap(RoomGraphAssembler assembler)
        {
            int seed = FindSeed(assembler.Generator, floor =>
                floor.Nodes.Any(node => node.Role == GeneratedRoomRole.Secret) && floor.Nodes.Count >= 6);
            using TestRun run = new(assembler, seed);
            ItemDefinition map = Load(Week26Spell6Setup.NettiMapId);
            GameMinimapView minimap = Object.FindFirstObjectByType<GameMinimapView>();
            Assert(minimap != null && minimap.Progress == run.Progress,
                "The Game Scene minimap must show the Run the spells change.");
            GeneratedRoomNode secret = run.Floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Secret);

            minimap.RefreshNow();
            Assert(!minimap.IsFloorRevealed && minimap.VisibleMarkers.Count < run.Floor.Nodes.Count &&
                   minimap.VisibleMarkers.All(marker => marker.RoomId != secret.RoomId),
                "Before the map only the visited rooms and their neighbors show, without the secret room.");

            run.Give(map, "spell6-map");
            Assert(run.Slot.TryUse() == SpellSlotUseResult.Used && !run.Slot.HasItem &&
                   run.Progress.IsFloorMapRevealed(1) && run.Progress.GetResourceCount(RunResourceType.Gold) == 5,
                "네티의 지도 must reveal the current floor and give 5 gold.");
            minimap.RefreshNow();
            int publicConnections = run.Floor.Nodes.SelectMany(node => node.DirectionalConnections
                    .Where(connection => !connection.IsSecret)
                    .Select(connection => string.CompareOrdinal(node.RoomId, connection.DestinationRoomId) < 0
                        ? node.RoomId + "|" + connection.DestinationRoomId
                        : connection.DestinationRoomId + "|" + node.RoomId))
                .Distinct().Count();
            MinimapRoomMarkerView secretMarker =
                minimap.VisibleMarkers.SingleOrDefault(marker => marker.RoomId == secret.RoomId);
            Assert(minimap.IsFloorRevealed && minimap.VisibleMarkers.Count == run.Floor.Nodes.Count &&
                   secretMarker != null && !secretMarker.HasVisited &&
                   secretMarker.SymbolText.text == (secret.IsGoldiShop ? "G" : "H") &&
                   minimap.VisibleConnectionCount == publicConnections,
                "The revealed map must show every room and every ordinary passage, the secret room by position " +
                "only.");
            RoomRunState secretState = run.Progress.GetRoomState(secret.RoomId);
            Assert(secretState.OpenedSecretPassages.Count == 0 && !secretState.HasVisited,
                "Revealing the map must not open a hidden passage or visit the secret room.");

            Assert(run.Progress.TryAddResource(RunResourceType.Gold, RunResourceWallet.MaxCount) > 0 &&
                   run.Progress.GetResourceCount(RunResourceType.Gold) == RunResourceWallet.MaxCount,
                "Fill the wallet before the second map.");
            run.Give(map, "spell6-map-full");
            Assert(run.Slot.TryUse() == SpellSlotUseResult.Used && !run.Slot.HasItem &&
                   run.Progress.GetResourceCount(RunResourceType.Gold) == RunResourceWallet.MaxCount,
                "네티의 지도 must be usable with 99 gold, dropping the gold beyond the limit.");
        }

        private static void ValidateEshurGrimoire(RoomGraphAssembler assembler)
        {
            int seed = FindSeed(assembler.Generator, floor =>
                floor.Nodes.Count(node => node.Role == GeneratedRoomRole.Intermediate) >= 2);
            using TestRun run = new(assembler, seed);
            ItemDefinition grimoire = Load(Week26Spell6Setup.EshurGrimoireId);
            PlayerStats stats = run.Graph.Player.GetComponent<PlayerStats>();
            PlayerSingleUseEffects effects = run.Effects;
            GeneratedRoomNode[] combatRooms =
                run.Floor.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate).Take(2).ToArray();

            run.Give(grimoire, "spell6-grimoire-start");
            Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HasItem &&
                   !stats.BasicAttackHoming.IsEnabled,
                "The start room must refuse 에슈르의 마도서 and keep it.");
            run.Progress.GetRoomState(combatRooms[1].RoomId).MarkCleared();
            run.MoveTo(run.FindRoom(combatRooms[1].RoomId));
            Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HasItem,
                "A cleared combat room must refuse 에슈르의 마도서.");

            run.MoveTo(run.FindRoom(combatRooms[0].RoomId));
            Assert(run.Slot.TryUse() == SpellSlotUseResult.Used && !run.Slot.HasItem &&
                   Approximately(stats.BasicAttackHoming.Coefficient, 0.5f) &&
                   Approximately(stats.BasicAttackHoming.DetectionRange, 4f) &&
                   Approximately(effects.RoomHomingCoefficient, 0.5f),
                "An uncleared combat room must consume 에슈르의 마도서 and home basic attacks at 0.5 within 4.");
            run.Give(grimoire, "spell6-grimoire-again");
            Assert(run.Slot.TryUse() == SpellSlotUseResult.Used &&
                   Approximately(stats.BasicAttackHoming.Coefficient, 1f) &&
                   Approximately(stats.BasicAttackHoming.DetectionRange, 4f),
                "A second use in the same room must add the coefficient (1.0) and keep the range.");

            // The real basic attack takes the room homing at launch.
            PlayerProjectileAttack attack = run.Graph.Player.GetComponent<PlayerProjectileAttack>();
            InvokeLifecycle(run.Graph.Player, "Awake");
            InvokeLifecycle(attack, "Awake");
            List<Projectile> before = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).ToList();
            typeof(PlayerProjectileAttack).GetMethod("SpawnProjectile", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(attack, new object[] { Vector2.right });
            Projectile shot = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                .SingleOrDefault(projectile => !before.Contains(projectile));
            Assert(shot != null && Approximately(shot.Homing.Coefficient, 1f) &&
                   Approximately(shot.Homing.DetectionRange, 4f),
                "A basic attack shot fired in the room must carry the room homing.");
            Object.DestroyImmediate(shot.gameObject);

            run.MoveTo(run.Graph.StartingNode.GetComponent<RoomPrefab>());
            effects.RefreshRoomBoost();
            Assert(!stats.BasicAttackHoming.IsEnabled && Approximately(effects.RoomHomingCoefficient, 0f),
                "Leaving the room must end the homing.");
        }

        private static void ValidateTigChallenge(RoomGraphAssembler assembler)
        {
            int seed = FindSeed(assembler.Generator, floor => floor.Nodes.Any(node =>
                node.Role == GeneratedRoomRole.Intermediate && node.Encounter != null &&
                node.Encounter.Waves.Count >= 2));
            using TestRun run = new(assembler, seed);
            ItemDefinition challenge = Load(Week26Spell6Setup.TigChallengeId);
            GeneratedRoomNode bossNode = run.Floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Boss);
            RoomPrefab bossRoom = run.FindRoom(bossNode.RoomId);
            Vector2 bossEntry = bossRoom.FindSlot(bossNode.DirectionalConnections.Single().Direction).EntryPoint.position;
            GeneratedRoomNode combatNode = run.Floor.Nodes.First(node =>
                node.Role == GeneratedRoomRole.Intermediate && node.Encounter?.Waves.Count >= 2);
            RoomPrefab combatRoom = run.FindRoom(combatNode.RoomId);
            RoomRunState combatState = run.Progress.GetRoomState(combatNode.RoomId);

            // From a fight: the left room resets like 그건 내 잔상 and the boss fight starts on arrival.
            run.MoveTo(combatRoom);
            run.BeginCombat(combatRoom.Controller);
            run.KillAll(combatRoom.Controller);
            Assert(combatState.CompletedWaveCount == 1 && combatRoom.Controller.State == RoomState.Combat,
                "The second wave must be running before the challenge.");
            int killsBefore = run.Progress.KillCount;
            run.Give(challenge, "spell6-tig");
            run.BlockTransitions();
            Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HasItem &&
                   run.Graph.CurrentNode == combatRoom.Node && combatRoom.Controller.State == RoomState.Combat,
                "A blocked room move must refuse 티그의 도전장 without consuming it or resetting the room.");
            run.ResetTransitionCooldown();
            int roomChanges = 0;
            void CountRoomChange(int _, int __) => roomChanges++;
            run.Progress.RoomChanged += CountRoomChange;
            SpellSlotUseResult result = run.Slot.TryUse();
            run.Progress.RoomChanged -= CountRoomChange;
            Assert(result == SpellSlotUseResult.Used && !run.Slot.HasItem && run.Graph.CurrentNode == bossRoom.Node &&
                   run.Progress.CurrentRoom == bossNode.RoomNumber && bossRoom.Node.IsVisible &&
                   !combatRoom.Node.IsVisible &&
                   (run.Graph.Player.FeetPosition - RoomLayout.ArrivalFeetOffset - bossEntry).sqrMagnitude < 0.0001f,
                "티그의 도전장 must move the player to the boss room door's safe entry point.");
            Assert(bossRoom.Controller.State == RoomState.Combat && bossRoom.Controller.HasStarted &&
                   run.Spawned.Any(enemy => enemy != null && enemy.GetComponent<BossController>() != null),
                "Arriving must start the boss fight at once.");
            Assert(roomChanges is >= 1 and <= 2 && run.Progress.CurrentRoom == bossNode.RoomNumber &&
                   run.Effects.LastAbandonedRoomId == combatNode.RoomId &&
                   run.Graph.IsReturnDoorwayBlocked(combatRoom.Node) == false,
                "The challenge must enter the boss room once (the fight start records the same room) and report " +
                "the room it left.");
            Assert(combatRoom.Controller.State == RoomState.Waiting && !combatRoom.Controller.HasStarted &&
                   combatState.CompletedWaveCount == 0 && !combatState.IsCleared &&
                   run.Progress.KillCount == killsBefore,
                "The room left mid-fight must reset like 그건 내 잔상 without counting kills.");

            run.Give(challenge, "spell6-tig-boss");
            run.ResetTransitionCooldown();
            Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HasItem &&
                   run.Graph.CurrentNode == bossRoom.Node,
                "The boss room must refuse 티그의 도전장 and keep it.");

            // After the boss is cleared the challenge still moves there, without a new fight.
            Assert(bossRoom.Controller.KillAliveEnemiesForDevelopment() > 0 &&
                   bossRoom.Controller.State == RoomState.Cleared, "The boss must be cleared for the next check.");
            // A boss reward choice left open would block every room move; this check is about the move only.
            if (run.Progress.PendingRewardSelection != null)
                run.Progress.DeactivateRewardSelection(run.Progress.PendingRewardSelection);
            run.MoveTo(run.Graph.StartingNode.GetComponent<RoomPrefab>());
            Assert(run.Slot.TryUse() == SpellSlotUseResult.Used && run.Graph.CurrentNode == bossRoom.Node &&
                   bossRoom.Controller.State == RoomState.Cleared && run.Effects.LastAbandonedRoomId == null,
                "A cleared boss room must still be reachable without restarting the fight.");
        }

        private static void ValidateGoldiInvitation(RoomGraphAssembler assembler)
        {
            int noSecretSeed = FindSeed(assembler.Generator, floor =>
                floor.Nodes.All(node => node.Role != GeneratedRoomRole.Secret));
            ItemDefinition invitation = Load(Week26Spell6Setup.GoldiInvitationId);
            using (TestRun run = new(assembler, noSecretSeed))
            {
                run.Give(invitation, "spell6-goldi-none");
                Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HasItem,
                    "A floor without a secret room must refuse 골디의 초대장 and keep it.");
            }

            int seed = FindSeed(assembler.Generator, floor => floor.Nodes.Any(node =>
                node.Role == GeneratedRoomRole.Secret && !node.IsGoldiShop) && floor.Nodes.Any(node =>
                node.Role == GeneratedRoomRole.Intermediate && node.Encounter != null));
            using (TestRun run = new(assembler, seed))
            {
                GeneratedRoomNode secret = run.Floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Secret);
                run.Progress.GetRoomState(secret.RoomId).MarkArtifactClaimed();
                run.Give(invitation, "spell6-goldi-claimed");
                Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HasItem &&
                       !secret.IsGoldiShop,
                    "A secret room whose reward was taken must refuse 골디의 초대장.");
            }

            using (TestRun run = new(assembler, seed))
            {
                GeneratedRoomNode secret = run.Floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Secret);
                RoomPrefab secretRoom = run.FindRoom(secret.RoomId);
                Vector2 secretEntry = secretRoom.FindSlot(secret.DirectionalConnections[0].Direction).EntryPoint.position;
                GeneratedRoomNode combatNode = run.Floor.Nodes.First(node =>
                    node.Role == GeneratedRoomRole.Intermediate && node.Encounter != null);
                RoomPrefab combatRoom = run.FindRoom(combatNode.RoomId);
                Assert(secretRoom.RewardRoom != null && secretRoom.RewardRoom.gameObject.activeSelf &&
                       secretRoom.Node.ContentRoot.GetComponentInChildren<ShopRoom>(true) == null,
                    "The verification secret room must start as a treasure-style room.");

                run.MoveTo(combatRoom);
                run.BeginCombat(combatRoom.Controller);
                Assert(combatRoom.Controller.State == RoomState.Combat, "The combat room must be fighting.");
                run.Give(invitation, "spell6-goldi");
                Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HasItem &&
                       run.Graph.CurrentNode == combatRoom.Node && combatRoom.Controller.State == RoomState.Combat &&
                       !secret.IsGoldiShop,
                    "골디의 초대장 must be refused and kept during combat (user decision, 2026-10-16).");
                run.KillAll(combatRoom.Controller);
                for (int wave = 0; wave < 8 && combatRoom.Controller.State == RoomState.Combat; wave++)
                    run.KillAll(combatRoom.Controller);
                Assert(combatRoom.Controller.State == RoomState.Cleared, "The combat room must be cleared.");
                if (run.Progress.PendingRewardSelection != null)
                    run.Progress.DeactivateRewardSelection(run.Progress.PendingRewardSelection);
                run.ResetTransitionCooldown();
                Assert(run.Slot.TryUse() == SpellSlotUseResult.Used && !run.Slot.HasItem &&
                       run.Graph.CurrentNode == secretRoom.Node &&
                       (run.Graph.Player.FeetPosition - RoomLayout.ArrivalFeetOffset - secretEntry).sqrMagnitude < 0.0001f,
                    "골디의 초대장 must move the player into the secret room at a door entry.");
                ShopRoom shop = secretRoom.Node.ContentRoot.GetComponentInChildren<ShopRoom>(true);
                ShopStockState stock = run.Progress.GetShopStock(ShopStockBuilder.BuildShopId(secret.RoomId));
                Assert(secret.IsGoldiShop && shop != null && shop.Kind == ShopKind.Goldi && stock != null &&
                       stock.Kind == ShopKind.Goldi && !secretRoom.RewardRoom.gameObject.activeSelf,
                    "The secret room must become the 골디 shop with 골디 stock and no treasure-style reward.");
                Assert(run.Progress.GetRoomState(secret.RoomId).OpenedSecretPassages.Count ==
                       secret.DirectionalConnections.Count,
                    "Arriving in a sealed secret room must open its passages so the player is never trapped.");
                Assert(combatRoom.Controller.State == RoomState.Cleared && run.Effects.LastAbandonedRoomId == null,
                    "Leaving a cleared room must not reset it.");
                // The shop was built while the secret room was hidden, so its keeper wakes only when the room shows.
                ShopKeeper keeper = shop.Keeper;
                InvokeLifecycle(keeper, "Awake");
                Assert(keeper != null && keeper.Portrait.sprite == UserArtwork.Load(ShopKeeper.GoldiArtworkKey) &&
                       keeper.Portrait.sprite != UserArtwork.Load(ShopKeeper.ArtworkKey) &&
                       keeper.NameLabel.text == ShopKeeper.GoldiDisplayName,
                    "The converted shop's keeper must stay 골디 after it wakes, not the 시스트 default.");
                GameMinimapView minimap = Object.FindFirstObjectByType<GameMinimapView>();
                minimap.RefreshNow();
                Assert(minimap.VisibleMarkers.Any(marker => marker.RoomId == secret.RoomId &&
                                                            marker.SymbolText.text == "P"),
                    "The minimap must show the player in the secret room.");

                run.Give(invitation, "spell6-goldi-inside");
                run.ResetTransitionCooldown();
                Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HasItem,
                    "Inside the 골디 shop the invitation must be refused and kept.");

                // Already the 골디 shop: another invitation only moves there, keeping the same stock.
                run.MoveTo(run.Graph.StartingNode.GetComponent<RoomPrefab>());
                Assert(run.Slot.TryUse() == SpellSlotUseResult.Used && run.Graph.CurrentNode == secretRoom.Node &&
                       ReferenceEquals(run.Progress.GetShopStock(ShopStockBuilder.BuildShopId(secret.RoomId)),
                           stock) &&
                       secretRoom.Node.ContentRoot.GetComponentsInChildren<ShopRoom>(true).Length == 1,
                    "A second invitation must only move to the existing 골디 shop.");
            }
        }

        private static void ValidateEllisTarot(RoomGraphAssembler assembler)
        {
            int seed = FindSeed(assembler.Generator, floor => floor.Nodes.Count > 0);
            using TestRun run = new(assembler, seed);
            ItemDefinition tarot = Load(Week26Spell6Setup.EllisTarotId);
            PlayerStats stats = run.Graph.Player.GetComponent<PlayerStats>();
            Dictionary<TarotStat, float> before = TarotCardDraw.Stats.ToDictionary(stat => stat, stats.GetTarotStep);
            Dictionary<TarotStat, float> expected = new(before);
            try
            {
                Assert(run.Progress.TarotDrawCount == 0, "A new Run must start with no tarot card drawn.");
                for (int use = 0; use < 3; use++)
                {
                    run.Give(tarot, "spell6-tarot-" + use);
                    Assert(run.Slot.TryUse() == SpellSlotUseResult.Used && !run.Slot.HasItem,
                        "엘리스의 타로카드 must be usable anywhere in the Run.");
                    List<TarotStatChange> drawn = TarotCardDraw.Draw(run.Progress.RunSeed, use);
                    Assert(Same(run.Effects.LastTarotChanges.ToList(), drawn) &&
                           run.Progress.TarotDrawCount == use + 1,
                        $"Card {use + 1} must be the draw for the Run seed and use index {use}.");
                    foreach (TarotStatChange change in drawn) expected[change.Stat] += change.Amount;
                }

                Assert(TarotCardDraw.Stats.All(stat => Approximately(stats.GetTarotStep(stat), expected[stat])),
                    "The cards' steps must add up for the rest of the Run.");
                run.Progress.ResetProgress();
                Assert(run.Progress.TarotDrawCount == 0, "A new Run must count its tarot cards from zero again.");
            }
            finally
            {
                foreach (TarotStat stat in TarotCardDraw.Stats)
                    stats.AddTarotStep(stat, before[stat] - stats.GetTarotStep(stat));
            }
        }

        // Contract 24 §4.2 on a lone shot: it keeps its speed and turns at coefficient x 360°/s toward the nearest
        // enemy in front within range, skipping enemies behind it, out of range or already hit.
        private static void ValidateHomingSteering()
        {
            List<GameObject> created = new();
            try
            {
                Health owner = CreateHealth("Homing Owner", Vector2.zero, 0, created);
                Health ahead = CreateHealth("Homing Ahead", new Vector2(2f, 2f), 6, created);
                Projectile shot = CreateShot(owner, new ProjectileHomingSettings(0.5f, 4f), created);
                Physics2D.SyncTransforms();
                shot.TickHoming(0.1f);
                Assert(shot.HomingTarget == ahead && Approximately(AngleOf(shot.Velocity), 18f) &&
                       Approximately(shot.Velocity.magnitude, 8f),
                    "Coefficient 0.5 must turn the shot 18° toward the enemy in 0.1s and keep its speed.");
                shot.TickHoming(1f);
                Assert(Approximately(AngleOf(shot.Velocity), 45f),
                    "The turn must stop once the shot points at the enemy.");

                Projectile doubled = CreateShot(owner, new ProjectileHomingSettings(1f, 4f), created);
                doubled.TickHoming(0.1f);
                Assert(Approximately(AngleOf(doubled.Velocity), 36f), "Coefficient 1.0 must turn 360° per second.");

                ahead.transform.position = new Vector2(-2f, 0.5f);
                Projectile behind = CreateShot(owner, new ProjectileHomingSettings(0.5f, 4f), created);
                Physics2D.SyncTransforms();
                behind.TickHoming(0.1f);
                Assert(behind.HomingTarget == null && Approximately(AngleOf(behind.Velocity), 0f),
                    "An enemy behind the shot (outside the front 180°) must not pull it.");

                ahead.transform.position = new Vector2(4.5f, 0.5f);
                Physics2D.SyncTransforms();
                behind.TickHoming(0.1f);
                Assert(behind.HomingTarget == null, "An enemy beyond range 4 must not pull the shot.");

                Health near = CreateHealth("Homing Near", new Vector2(1f, -1f), 6, created);
                ahead.transform.position = new Vector2(3f, 1f);
                Projectile pierce = CreateShot(owner, new ProjectileHomingSettings(0.5f, 4f), created, 1);
                Physics2D.SyncTransforms();
                pierce.TickHoming(0.02f);
                Assert(pierce.HomingTarget == near, "The nearest enemy in front must be chosen.");
                typeof(Projectile).GetMethod("HitTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(pierce, new object[] { near });
                pierce.TickHoming(0.02f);
                Assert(!pierce.IsSpent && pierce.HomingTarget == ahead,
                    "An enemy the shot already hit must leave the homing targets.");

                Projectile plain = CreateShot(owner, default, created);
                plain.TickHoming(0.1f);
                Assert(plain.HomingTarget == null && Approximately(AngleOf(plain.Velocity), 0f),
                    "A shot without homing must fly straight.");
            }
            finally
            {
                foreach (GameObject target in created)
                    if (target != null) Object.DestroyImmediate(target);
            }
        }

        // The tarot steps join each stat's own bonuses and may lower it, with shot speed and range kept above zero.
        private static void ValidateTarotStats()
        {
            GameObject player = new("Tarot Stats");
            try
            {
                PlayerStats stats = player.AddComponent<PlayerStats>();
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(stats, "Awake");
                stats.AddTarotStep(TarotStat.AttackDamage, 0.08f);
                stats.AddTarotStep(TarotStat.AttackSpeed, -0.08f);
                stats.AddTarotStep(TarotStat.MoveSpeed, -0.05f);
                stats.AddTarotStep(TarotStat.Range, 0.1f);
                stats.AddTarotStep(TarotStat.ShotSpeed, -0.1f);
                stats.AddTarotStep(TarotStat.CriticalChance, 0.05f);
                stats.AddTarotStep(TarotStat.CriticalDamage, -0.2f);
                Assert(Approximately(stats.AttackDamage, 10.8f) && Approximately(stats.AttackSpeed, 0.92f) &&
                       Approximately(stats.MoveSpeed, 4.75f) && Approximately(stats.ProjectileLifetimeMultiplier, 1.1f) &&
                       Approximately(stats.ProjectileSpeedMultiplier, 0.9f) && Approximately(stats.CriticalChance, 0.1f) &&
                       Approximately(stats.CriticalDamageMultiplier, 1.3f),
                    "Each tarot step must raise or lower its own stat from the base values.");
                stats.AddTarotStep(TarotStat.ShotSpeed, -5f);
                stats.AddTarotStep(TarotStat.Range, -5f);
                Assert(Approximately(stats.ProjectileSpeedMultiplier, PlayerStats.MinimumTarotMultiplier) &&
                       Approximately(stats.ProjectileLifetimeMultiplier, PlayerStats.MinimumTarotMultiplier),
                    "Shot speed and range must stay above zero however many drops stack.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static Health CreateHealth(string name, Vector2 position, int layer, List<GameObject> created)
        {
            GameObject target = new(name) { layer = layer };
            target.transform.position = position;
            target.AddComponent<CircleCollider2D>().radius = 0.3f;
            Health health = target.AddComponent<Health>();
            InvokeLifecycle(health, "Awake");
            created.Add(target);
            return health;
        }

        private static Projectile CreateShot(Health owner, ProjectileHomingSettings homing, List<GameObject> created,
            int pierces = 0)
        {
            GameObject shotObject = new("Homing Shot");
            Rigidbody2D body = shotObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            shotObject.AddComponent<CircleCollider2D>().radius = 0.2f;
            Projectile shot = shotObject.AddComponent<Projectile>();
            InvokeLifecycle(shot, "Awake");
            shot.Launch(new Vector2(8f, 0f), owner,
                new DamageContext(owner.gameObject, DamageSourceType.PlayerProjectile, 1f), pierces,
                configuredHoming: homing);
            created.Add(shotObject);
            return shot;
        }

        private static float AngleOf(Vector2 velocity) => Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;

        private static bool Same(IReadOnlyList<TarotStatChange> left, IReadOnlyList<TarotStatChange> right) =>
            left.Count == right.Count && left.Zip(right, (a, b) => a.Stat == b.Stat && Approximately(a.Amount, b.Amount))
                .All(match => match);

        private static bool Near(int count, int total, float share, float tolerance) =>
            Mathf.Abs((float)count / total - share) <= tolerance;

        private static int FindSeed(FloorGenerator generator, Func<GeneratedFloor, bool> wanted)
        {
            Assert(generator != null, "The Game Scene assembler needs its FloorGenerator.");
            for (int seed = 1; seed <= MaximumSeedSearch; seed++)
            {
                if (!generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _)) continue;
                GeneratedFloor floor = graph.FindFloor(1);
                if (floor != null && wanted(floor)) return seed;
            }

            throw new InvalidOperationException("No seed produced the Spell-6 verification floor.");
        }

        private static ItemDefinition Load(string itemId)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week26Spell6Setup.PathOf(itemId));
            Assert(item != null, $"Run Spell-6 setup first: {itemId} is missing.");
            return item;
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            if (target == null) return;
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(target, null);
        }

        private static bool Approximately(float actual, float expected) => Mathf.Abs(actual - expected) <= 0.001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        // The Game Scene Run on a fixed seed, with the real player, slot, executor and room graph. Edit mode runs no
        // lifecycle callbacks, so the player components and each spawned enemy are woken here as Play Mode would.
        private sealed class TestRun : IDisposable
        {
            private readonly RoomGraphAssembler assembler;
            private readonly float previousTimeScale;
            private readonly List<RoomController> watchedRooms = new();
            private int givenCount;

            public RunProgress Progress => assembler.Progress;
            public RoomGraphController Graph => assembler.Graph;
            public GeneratedFloor Floor { get; }
            public PlayerSpellSlot Slot { get; }
            public PlayerSingleUseEffects Effects { get; }
            public SingleUseItemPickup PickupPrefab { get; }
            public List<GameObject> Spawned { get; } = new();

            public TestRun(RoomGraphAssembler configuredAssembler, int seed)
            {
                assembler = configuredAssembler;
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;
                Progress.ResetProgress();
                Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
                Floor = assembler.GeneratedGraph.FindFloor(1);

                GameObject player = Graph.Player.gameObject;
                Slot = player.GetComponent<PlayerSpellSlot>();
                Effects = player.GetComponent<PlayerSingleUseEffects>();
                PickupPrefab = Slot != null ? Slot.PickupPrefab : null;
                Assert(Slot != null && Effects != null && PickupPrefab != null,
                    "Run Slot-0 setup first: the Game Scene player needs the spell slot.");
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerInventory>(), "Awake");
                InvokeLifecycle(Slot, "Awake");
                InvokeLifecycle(Effects, "Awake");
                Effects.ConfigureRoomAssembler(assembler);
                ResetTransitionCooldown();

                foreach (RoomNode node in Graph.Nodes)
                {
                    RoomController controller = node.GetComponent<RoomPrefab>()?.Controller;
                    if (controller == null) continue;
                    controller.EnemySpawned += Wake;
                    watchedRooms.Add(controller);
                }
            }

            public void Give(ItemDefinition item, string instanceId)
            {
                SingleUseItemPickup pickup = Object.Instantiate(PickupPrefab, Graph.CurrentNode.ContentRoot.transform);
                pickup.Configure(item, $"{instanceId}-{++givenCount}", false);
                Assert(Slot.TryCollect(pickup) && Slot.HeldDefinition == item, $"The slot must take {item.ItemId}.");
            }

            public RoomPrefab FindRoom(string roomId)
            {
                foreach (RoomNode node in Graph.Nodes)
                    if (node.RoomId == roomId) return node.GetComponent<RoomPrefab>();
                throw new InvalidOperationException($"Runtime room {roomId} is missing.");
            }

            public void MoveTo(RoomPrefab room)
            {
                ResetTransitionCooldown();
                Assert(Graph.TryTeleport(Graph.CurrentNode, room.Node, room.Node.DefaultEntryPoint.position,
                        Graph.Player) && Graph.CurrentNode == room.Node,
                    $"The verification player must reach {room.Node.RoomId}.");
                Physics2D.SyncTransforms();
                ResetTransitionCooldown();
            }

            // The room trigger does not fire in edit mode; entering starts the fight the same way.
            public void BeginCombat(RoomController controller)
            {
                controller.BeginCombat(Graph.Player.GetComponent<Health>());
            }

            public void KillAll(RoomController controller)
            {
                Assert(controller.KillAliveEnemiesForDevelopment() > 0, "A running wave must have enemies to kill.");
            }

            public void ResetTransitionCooldown() => SetCooldown(0f);

            public void BlockTransitions() => SetCooldown(float.MaxValue);

            private void Wake(GameObject enemy)
            {
                InvokeLifecycle(enemy.GetComponent<Health>(), "Awake");
                Spawned.Add(enemy);
            }

            public void Dispose()
            {
                foreach (RoomController controller in watchedRooms)
                    if (controller != null) controller.EnemySpawned -= Wake;
                Time.timeScale = previousTimeScale;
                ResetTransitionCooldown();
                Progress.ResetProgress();
            }

            private void SetCooldown(float until)
            {
                typeof(RoomGraphController).GetField("nextTransitionTime", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(Graph, until);
            }
        }
    }
}
