using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Shop;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [DefaultExecutionOrder(-200)]
    public sealed class RoomGraphAssembler : MonoBehaviour
    {
        [SerializeField] private FloorGenerator generator;
        [SerializeField] private RoomGraphController graph;
        [SerializeField] private RunProgress runProgress;
        [SerializeField] private RoomPrefab roomPrefab;
        [SerializeField] private EncounterEnemyRoster encounterEnemyRoster;
        [SerializeField] private ResourceDropTable encounterClearDropTable;
        [SerializeField] private ChestContentTable chestContentTable;
        [SerializeField] private GameObject[] floorBossPrefabs = Array.Empty<GameObject>();
        [SerializeField] private ItemRewardSelectionSession rewardSelectionSession;
        [SerializeField] private ItemDefinition[] selectionRewardPool = Array.Empty<ItemDefinition>();
        [SerializeField] private ShopRoom shopRoomPrefab;
        [SerializeField] private ShopCatalog shopCatalog;
        [SerializeField] private ShopSession shopSession;

        private GeneratedFloorGraph generatedGraph;
        private GameObject currentFloorRoot;

        private bool hasAppliedRuntimeGraph;
        private int appliedRunSeed;

        public FloorGenerator Generator => generator;
        public RoomGraphController Graph => graph;
        public RunProgress Progress => runProgress;
        public bool HasAppliedRuntimeGraph => hasAppliedRuntimeGraph;
        public int AppliedRunSeed => appliedRunSeed;
        public RoomPrefab ConfiguredRoomPrefab => roomPrefab;
        public EncounterEnemyRoster EnemyRoster => encounterEnemyRoster;
        public ResourceDropTable EncounterClearDropTable => encounterClearDropTable;
        public ChestContentTable ChestContentTable => chestContentTable;
        public IReadOnlyList<GameObject> FloorBossPrefabs => floorBossPrefabs;
        public GeneratedFloorGraph GeneratedGraph => generatedGraph;
        public GameObject CurrentFloorRoot => currentFloorRoot;
        public ItemRewardSelectionSession RewardSelectionSession => rewardSelectionSession;
        public IReadOnlyList<ItemDefinition> SelectionRewardPool => selectionRewardPool;
        public ShopRoom ShopRoomPrefab => shopRoomPrefab;
        public ShopCatalog ShopCatalog => shopCatalog;
        public ShopSession ShopSession => shopSession;

        public void Configure(
            FloorGenerator configuredGenerator,
            RoomGraphController configuredGraph,
            RunProgress configuredProgress)
        {
            generator = configuredGenerator;
            graph = configuredGraph;
            runProgress = configuredProgress;
        }

        public void Configure(FloorGenerator configuredGenerator, RoomGraphController configuredGraph,
            RunProgress configuredProgress, RoomPrefab configuredRoomPrefab)
        {
            Configure(configuredGenerator, configuredGraph, configuredProgress);
            roomPrefab = configuredRoomPrefab;
        }

        public void ConfigureEncounterRoster(EncounterEnemyRoster configuredRoster)
        {
            encounterEnemyRoster = configuredRoster;
        }

        public void ConfigureEncounterClearDrop(ResourceDropTable configuredTable)
        {
            encounterClearDropTable = configuredTable;
        }

        // Chest-1: combat room clears roll a chest instead of the old single-pickup drop table.
        public void ConfigureChestContents(ChestContentTable configuredTable)
        {
            chestContentTable = configuredTable;
        }

        public void ConfigureFloorBossPrefabs(GameObject[] configuredPrefabs)
        {
            floorBossPrefabs = configuredPrefabs ?? Array.Empty<GameObject>();
        }

        public void ConfigureSelectionRewards(ItemRewardSelectionSession configuredSession,
            ItemDefinition[] configuredPool)
        {
            rewardSelectionSession = configuredSession;
            selectionRewardPool = configuredPool ?? Array.Empty<ItemDefinition>();
        }

        public void ConfigureShop(ShopRoom configuredPrefab, ShopCatalog configuredCatalog)
        {
            shopRoomPrefab = configuredPrefab;
            shopCatalog = configuredCatalog;
        }

        public void ConfigureShopSession(ShopSession configuredSession)
        {
            shopSession = configuredSession;
        }

        public GameObject ResolveBossPrefab(int floorNumber)
        {
            int index = floorNumber - 1;
            if (index < 0 || index >= floorBossPrefabs.Length) return null;
            GameObject prefab = floorBossPrefabs[index];
            return prefab != null && prefab.GetComponent<BossController>() != null &&
                   prefab.GetComponent<Health>() != null
                ? prefab
                : null;
        }

        private void Awake()
        {
            if (runProgress == null && graph != null)
            {
                runProgress = graph.Progress;
            }

            if (roomPrefab != null)
            {
                for (int index = transform.childCount - 1; index >= 0; index--)
                {
                    GameObject child = transform.GetChild(index).gameObject;
                    if (child.name.StartsWith("Generated Floor ", StringComparison.Ordinal))
                        DestroyFloor(child);
                }
            }

            if (!TryApplyGeneratedGraph(out string error))
            {
                Debug.LogError($"{name}: Failed to assemble generated room graph. {error}", this);
                enabled = false;
            }
        }

        public bool TryApplyGeneratedGraph(out string error)
        {
            if (generator == null || graph == null || runProgress == null)
            {
                error = "FloorGenerator, RoomGraphController, and RunProgress references are required.";
                return false;
            }

            if (!runProgress.HasRunSeed)
            {
                error = "RunProgress must initialize the Run seed before graph assembly.";
                return false;
            }

            if (hasAppliedRuntimeGraph && appliedRunSeed != runProgress.RunSeed)
            {
                error = $"The assembled Run seed {appliedRunSeed} cannot change to {runProgress.RunSeed}.";
                return false;
            }

            if (!generator.TryInitializeRunSeed(runProgress.RunSeed, out error))
            {
                return false;
            }

            if (!TryApplyGeneratedGraphForSeed(runProgress.RunSeed, out error))
            {
                return false;
            }

            appliedRunSeed = runProgress.RunSeed;
            hasAppliedRuntimeGraph = true;
            return true;
        }

        public bool TryApplyGeneratedGraphForVerification(int fixedSeed, out string error)
        {
            return TryApplyGeneratedGraphForSeed(fixedSeed, out error);
        }

        private bool TryApplyGeneratedGraphForSeed(int seed, out string error)
        {
            if (generator == null || graph == null)
            {
                error = "FloorGenerator and RoomGraphController references are required.";
                return false;
            }

            if (!generator.TryGenerateForSeed(seed, out GeneratedFloorGraph generatedGraph, out error))
            {
                return false;
            }

            this.generatedGraph = generatedGraph;
            if (roomPrefab != null)
            {
                if (!roomPrefab.TryValidate(out error)) return false;
                if (runProgress != null && runProgress.GeneratedGraph == null &&
                    !runProgress.TrySetGeneratedGraph(generatedGraph, out error)) return false;
                return TryBuildFloor(1, null, out error);
            }

            if (!graph.TryValidateConfiguration(out error))
            {
                error = $"RoomGraphController is invalid before generated binding. {error}";
                return false;
            }

            if (!TryCreateBindings(generatedGraph, out List<RoomBinding> bindings, out error))
            {
                return false;
            }

            foreach (RoomBinding binding in bindings)
            {
                ApplyDefinition(binding);
                ApplyDoorways(binding);
            }

            return graph.TryValidateConfiguration(out error);
        }

        public bool TryLoadFloor(int floorNumber, PlayerMovement transitioningPlayer, out string error)
        {
            if (runProgress?.IsRewardSelectionPending == true)
            {
                error = "The pending reward selection must be completed before loading another floor.";
                return false;
            }

            if (generatedGraph == null)
            {
                error = "No generated graph is available for floor loading.";
                return false;
            }

            if (!TryBuildFloor(floorNumber, transitioningPlayer, out error)) return false;
            return true;
        }

        private bool TryBuildFloor(int floorNumber, PlayerMovement transitioningPlayer, out string error)
        {
            GeneratedFloor floor = generatedGraph?.FindFloor(floorNumber);
            if (floor == null || roomPrefab == null)
            {
                error = $"Generated floor {floorNumber} or its verified Room Prefab is missing.";
                return false;
            }

            GameObject nextRoot = new($"Generated Floor {floorNumber:00}");
            nextRoot.transform.SetParent(transform, false);
            Dictionary<string, RoomPrefab> instances = new(StringComparer.Ordinal);
            Dictionary<string, GeneratedRoomNode> generatedNodes = new(StringComparer.Ordinal);
            List<RoomNode> nodes = new(floor.Nodes.Count);
            GeneratedRoomNode secretRoom = null;
            foreach (GeneratedRoomNode generatedNode in floor.Nodes)
                if (generatedNode.Role == GeneratedRoomRole.Secret) secretRoom = generatedNode;
            SecretRoomLink secretLink = secretRoom != null ? new SecretRoomLink(graph) : null;
            foreach (GeneratedRoomNode generatedNode in floor.Nodes)
            {
                RoomPrefab sourcePrefab = ResolveRoomPrefab(generatedNode, out error);
                if (sourcePrefab == null) { DestroyFloor(nextRoot); return false; }
                RoomPrefab instance = Instantiate(sourcePrefab, nextRoot.transform);
                instance.name = generatedNode.RoomId;
                instance.transform.localPosition = new Vector3(
                    generatedNode.GridPosition.X * RoomLayout.RoomSpacingX,
                    generatedNode.GridPosition.Y * RoomLayout.RoomSpacingY,
                    0f);
                if (!instance.TryValidate(out error)) { DestroyFloor(nextRoot); return false; }

                RoomNode node = instance.Node;
                node.Configure(generatedNode.RoomId, generatedNode.FloorNumber, generatedNode.RoomNumber,
                    node.ContentRoot, node.CameraAnchor, node.DefaultEntryPoint, Array.Empty<RoomDoorway>());
                node.ApplyGeneratedDefinition(generatedNode.Definition);
                node.ApplyRoomProfile(generatedNode.Template != null ? generatedNode.Template.Profile : null);
                RoomRunState state = runProgress?.GetRoomState(generatedNode.RoomId);
                DoorController[] blockers = new DoorController[instance.DoorSlots.Length];
                for (int i = 0; i < blockers.Length; i++) blockers[i] = instance.DoorSlots[i].Blocker;
                Transform[] spawnPoints = CopySpawnPoints(instance.Controller.SpawnPoints,
                    generatedNode.Role == GeneratedRoomRole.Boss ? 1 : int.MaxValue);
                instance.Controller.Configure(generatedNode.FloorNumber, generatedNode.RoomNumber, runProgress,
                    null, spawnPoints, blockers);
                bool safeRoom = generatedNode.Role is GeneratedRoomRole.Start or GeneratedRoomRole.Treasure or
                    GeneratedRoomRole.Secret or GeneratedRoomRole.Shop;
                instance.Controller.BindRunState(state, safeRoom);
                if (!BindDestructibleObstacles(instance, generatedNode, state, secretLink, out error))
                {
                    DestroyFloor(nextRoot);
                    return false;
                }
                if (!ApplyEncounter(instance, generatedNode, state, out error))
                {
                    DestroyFloor(nextRoot);
                    return false;
                }
                if ((generatedNode.Role == GeneratedRoomRole.Shop || generatedNode.IsGoldiShop) &&
                    !TryBuildShop(instance, generatedNode, out error))
                {
                    DestroyFloor(nextRoot);
                    return false;
                }
                if (generatedNode.Role == GeneratedRoomRole.Boss)
                    ConfigureBossDrop(
                        instance,
                        generatedNode.FloorNumber >= generatedGraph.Floors.Count,
                        runProgress,
                        generatedNode.RoomId,
                        generatedNode.ContentSeed,
                        selectionRewardPool);
                if (instance.RewardRoom != null)
                {
                    // Shop-1: a secret room holding the 골디 shop has no treasure-style reward.
                    instance.RewardRoom.gameObject.SetActive(!generatedNode.IsGoldiShop &&
                        generatedNode.Role is GeneratedRoomRole.Treasure or GeneratedRoomRole.Secret);
                    instance.RewardRoom.Configure(generatedNode.FloorNumber, generatedNode.RoomNumber,
                        runProgress, instance.RewardRoom.GetComponent<ItemDropSource>(), instance.Controller,
                        rewardSelectionSession, selectionRewardPool);
                    instance.RewardRoom.BindRunState(state);
                }
                instances.Add(generatedNode.RoomId, instance);
                generatedNodes.Add(generatedNode.RoomId, generatedNode);
                nodes.Add(node);
            }

            if (secretLink != null) secretLink.Target = instances[secretRoom.RoomId].Node;
            foreach (GeneratedRoomNode generatedNode in floor.Nodes)
            {
                RoomPrefab instance = instances[generatedNode.RoomId];
                List<RoomDoorway> doorways = new();
                foreach (RoomDoorDirection direction in Enum.GetValues(typeof(RoomDoorDirection)))
                {
                    RoomDoorSlot slot = instance.FindSlot(direction);
                    if (slot == null) { error = $"{generatedNode.RoomId} is missing its {direction} slot."; DestroyFloor(nextRoot); return false; }
                    RoomNode destination = null; Transform entry = null;
                    if (generatedNode.TryGetConnection(direction, out GeneratedRoomConnection connection))
                    {
                        RoomPrefab destinationPrefab = instances[connection.DestinationRoomId];
                        GeneratedRoomNode destinationGenerated = generatedNodes[connection.DestinationRoomId];
                        destination = destinationPrefab.Node;
                        entry = destinationPrefab.FindSlot(GeneratedFloorGraph.Opposite(direction)).EntryPoint;
                        doorways.Add(slot.Doorway);
                        bool requiresKey = destinationGenerated.RequiresKey;
                        slot.Blocker.ConfigureVisualKind(ConnectionVisualKind(generatedNode, destinationGenerated, connection.IsSecret));
                        if (connection.IsSecret)
                            BindSecretPassage(slot, instance, generatedNode, destination, entry, destinationGenerated);
                        else
                            slot.Bind(graph, instance.Node, destination, entry, instance.Controller,
                                requiresKey, runProgress?.GetRoomState(destinationGenerated.RoomId));
                    }
                    else
                    {
                        slot.Bind(graph, instance.Node, null, null, instance.Controller);
                    }
                }
                instance.Node.SetDoorways(doorways.ToArray());
                instance.Node.SetVisible(false);
            }

            RoomNode startingNode = instances[floor.StartingRoomId].Node;
            graph.Configure(nodes.ToArray(), startingNode, graph.Player, graph.RoomCamera, runProgress);
            if (!graph.TryInitializeStartingRoom(transitioningPlayer, out error))
            {
                DestroyFloor(nextRoot);
                return false;
            }

            ConfigureFloorCompletion(floor, instances[floor.BossRoomId], nextRoot);
            GameObject previousRoot = currentFloorRoot;
            currentFloorRoot = nextRoot;
            if (previousRoot != null) DestroyFloor(previousRoot);
            return true;
        }

        // Special-3: a hidden passage stays a sealed wall until the secret room state opens it, from a bomb on either
        // wall or from entering the secret room. The same state drives both sides, and it survives floor rebuilds.
        private void BindSecretPassage(RoomDoorSlot slot, RoomPrefab instance, GeneratedRoomNode generatedNode,
            RoomNode destination, Transform entry, GeneratedRoomNode destinationGenerated)
        {
            bool fromSecret = generatedNode.Role == GeneratedRoomRole.Secret;
            string secretId = fromSecret ? generatedNode.RoomId : destinationGenerated.RoomId;
            string neighborId = fromSecret ? destinationGenerated.RoomId : generatedNode.RoomId;
            RoomRunState secretState = runProgress?.GetRoomState(secretId);
            bool requiresKey = destinationGenerated.RequiresKey;
            RoomRunState keyLockState = runProgress?.GetRoomState(destinationGenerated.RoomId);
            RoomGraphController graphController = graph;
            RoomNode source = instance.Node;
            RoomController sourceRoom = instance.Controller;
            void Bind(bool isSealed) => slot.Bind(graphController, source, destination, entry, sourceRoom,
                requiresKey, keyLockState, isSealed);

            Bind(secretState?.IsSecretPassageOpen(neighborId) != true);
            SecretPassageWall wall = slot.Seal.GetComponent<SecretPassageWall>();
            if (wall == null) wall = slot.Seal.AddComponent<SecretPassageWall>();
            wall.Bind(secretState, neighborId, () => Bind(false));
        }

        // Special-4: the shopkeeper goes into the room content; the stock rolls on the first build and is reused after.
        // Shop-1: a 골디 secret room gets the same shop with the 골디 stock.
        private bool TryBuildShop(RoomPrefab instance, GeneratedRoomNode node, out string error)
        {
            if (shopRoomPrefab == null || shopCatalog == null || !shopCatalog.TryValidate(out error))
            {
                error = $"Shop room {node.RoomId} requires a ShopRoom Prefab and a valid ShopCatalog.";
                return false;
            }

            PlayerInventory inventory = graph.Player != null ? graph.Player.GetComponent<PlayerInventory>() : null;
            ShopKind kind = node.IsGoldiShop ? ShopKind.Goldi : ShopKind.General;
            ShopStockState Build() => ShopStockBuilder.Build(node.RoomId, node.ContentSeed, shopCatalog,
                selectionRewardPool, inventory, kind);
            ShopStockState stock = runProgress != null
                ? runProgress.GetOrCreateShopStock(ShopStockBuilder.BuildShopId(node.RoomId), Build)
                : Build();
            Transform content = instance.Node.ContentRoot.transform;
            ShopRoom shop = Instantiate(shopRoomPrefab, content);
            shop.name = $"Shop - {node.RoomId}";
            shop.transform.localPosition = Vector3.zero;
            shop.Configure(runProgress, stock, content, shopSession);
            error = null;
            return true;
        }

        private RoomPrefab ResolveRoomPrefab(GeneratedRoomNode generatedNode, out string error)
        {
            GameObject templateAsset = generatedNode?.Template?.RoomPrefabAsset;
            RoomPrefab resolved = templateAsset != null ? templateAsset.GetComponent<RoomPrefab>() : roomPrefab;
            if (resolved == null)
            {
                error = $"Room {generatedNode?.RoomId ?? "<missing>"} has no usable Room Prefab.";
                return null;
            }

            if (generatedNode?.Template != null && generatedNode.Template.Profile == null)
            {
                error = $"Room {generatedNode.RoomId} template '{generatedNode.TemplateId}' has no Room Profile.";
                return null;
            }

            error = null;
            return resolved;
        }

        private void ConfigureFloorCompletion(GeneratedFloor floor, RoomPrefab bossRoom, GameObject floorRoot)
        {
            if (floor.FloorNumber >= generatedGraph.Floors.Count)
            {
                bossRoom.Controller.StateChanged += state =>
                {
                    if (state == RoomState.Cleared) runProgress?.RecordFinalBossCleared();
                };
                return;
            }

            GameObject portalObject = new($"Floor {floor.FloorNumber} Exit");
            portalObject.transform.SetParent(bossRoom.Node.ContentRoot.transform, false);
            portalObject.transform.localPosition = Vector3.up * RoomLayout.FloorExitVerticalOffset;
            portalObject.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            SpriteRenderer indicator = portalObject.AddComponent<SpriteRenderer>();
            RoomDoorSlot visualSource = bossRoom.FindSlot(RoomDoorDirection.Up);
            indicator.sprite = visualSource != null && visualSource.Seal != null
                ? visualSource.Seal.GetComponent<SpriteRenderer>()?.sprite
                : null;
            indicator.sortingOrder = 1;
            BoxCollider2D collider = portalObject.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            FloorAdvancePortal portal = portalObject.AddComponent<FloorAdvancePortal>();
            portal.Configure(this, bossRoom.Controller, floor.FloorNumber + 1, indicator,
                runProgress, null);
        }

        private static Transform[] CopySpawnPoints(IReadOnlyList<Transform> source, int maximumCount)
        {
            Transform[] result = new Transform[Math.Min(source.Count, maximumCount)];
            for (int i = 0; i < result.Length; i++) result[i] = source[i];
            return result;
        }

        private static void ConfigureBossDrop(
            RoomPrefab instance,
            bool isFinalBoss,
            RunProgress progress,
            string roomId,
            int contentSeed,
            ItemDefinition[] configuredSelectionPool)
        {
            ItemDropSource template = instance.RewardRoom != null
                ? instance.RewardRoom.GetComponent<ItemDropSource>()
                : instance.GetComponentInChildren<ItemDropSource>(true);
            if (template == null)
            {
                Debug.LogError($"{roomId}: boss reward source is missing from the instantiated boss room.", instance);
                return;
            }
            instance.Controller.EnemySpawned += enemy =>
            {
                BossController boss = enemy != null ? enemy.GetComponent<BossController>() : null;
                if (boss == null) return;
                boss.ConfigureEncounterSeed(contentSeed);
                ItemDropSource source = enemy.GetComponent<ItemDropSource>();
                if (source == null) source = enemy.AddComponent<ItemDropSource>();
                IReadOnlyList<ItemDefinition> sourcePool = configuredSelectionPool != null &&
                                                           configuredSelectionPool.Length > 0
                    ? configuredSelectionPool
                    : template.ItemPool;
                ItemDefinition[] items = new ItemDefinition[sourcePool.Count];
                for (int i = 0; i < items.Length; i++) items[i] = sourcePool[i];
                source.Configure(template.PickupPrefab, items, enemy.transform, instance.Node.ContentRoot.transform);
                source.ConfigureRewardContext(progress, $"{roomId}:boss");
                BossItemDrop drop = enemy.GetComponent<BossItemDrop>();
                if (drop == null) drop = enemy.AddComponent<BossItemDrop>();
                drop.Configure(isFinalBoss, source);
                Health health = enemy.GetComponent<Health>();
                health.Died += () => drop.TryHandleBossDefeated();
            };
        }

        private bool BindDestructibleObstacles(RoomPrefab instance, GeneratedRoomNode node, RoomRunState state,
            SecretRoomLink secretLink, out string error)
        {
            if (!RoomObstacleVariantSlot.TryResolveForRoom(instance, node.ContentSeed, out error))
            {
                error = $"Room {node.RoomId} could not resolve its obstacle variants. {error}";
                return false;
            }

            PlayerInventory inventory = graph.Player != null ? graph.Player.GetComponent<PlayerInventory>() : null;
            PlayerStats stats = graph.Player != null ? graph.Player.GetComponent<PlayerStats>() : null;
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (DestructibleObstacle obstacle in instance.GetComponentsInChildren<DestructibleObstacle>(true))
            {
                if (!obstacle.TryValidate(out error) || !ids.Add(obstacle.ObstacleId))
                {
                    error = $"Room {node.RoomId} has an invalid or duplicate destructible obstacle " +
                            $"'{obstacle.ObstacleId}'. {error}";
                    return false;
                }

                obstacle.Bind(state, node.ContentSeed, instance.Node.ContentRoot.transform, runProgress, secretLink,
                    instance.Node, instance.Controller);
                obstacle.BindRareItems(selectionRewardPool, inventory);
                obstacle.BindBurnSource(stats);
            }

            error = null;
            return true;
        }

        private bool ApplyEncounter(RoomPrefab instance, GeneratedRoomNode node,
            RoomRunState state, out string error)
        {
            RoomController controller = instance.Controller;
            if (node.Encounter != null)
            {
                if (encounterEnemyRoster == null)
                { error = $"Room {node.RoomId} requires an Encounter enemy roster."; return false; }
                if (!encounterEnemyRoster.TryValidate(out error))
                { error = $"Room {node.RoomId} requires a valid Encounter enemy roster. {error}"; return false; }
                EncounterRuntimeWave[] waves = new EncounterRuntimeWave[node.Encounter.Waves.Count];
                for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
                {
                    if (node.ResolvedEncounterWaves.Count != waves.Length)
                    { error = $"Room {node.RoomId} is missing its persisted Encounter candidate selection."; return false; }
                    ResolvedEncounterSpawn[] resolved = node.ResolvedEncounterWaves[waveIndex];

                    GameObject[] encounterPrefabs = new GameObject[resolved.Length];
                    Transform[] encounterSpawnPoints = new Transform[resolved.Length];
                    for (int index = 0; index < resolved.Length; index++)
                    {
                        ResolvedEncounterSpawn spawn = resolved[index];
                        if (!encounterEnemyRoster.TryResolve(spawn.Role, out encounterPrefabs[index], out error))
                        { error = $"Room {node.RoomId} could not resolve role '{spawn.Role}'. {error}"; return false; }
                        if (spawn.SpawnPointIndex < 0 || spawn.SpawnPointIndex >= controller.SpawnPoints.Count)
                        { error = $"Room {node.RoomId} resolved an invalid SpawnPoint index {spawn.SpawnPointIndex}."; return false; }
                        encounterSpawnPoints[index] = controller.SpawnPoints[spawn.SpawnPointIndex];
                    }

                    waves[waveIndex] = new EncounterRuntimeWave(encounterPrefabs, encounterSpawnPoints);
                }

                controller.ConfigurePreplacedEnemies(Array.Empty<Health>());
                controller.ConfigureEncounterWaves(waves);
                if (chestContentTable != null)
                {
                    if (!chestContentTable.TryValidate(out error))
                    { error = $"Room {node.RoomId} requires a valid chest content table. {error}"; return false; }
                    if (node.Template == null || runProgress == null)
                    { error = $"Room {node.RoomId} needs its Template and Run progress to place a chest."; return false; }
                    RoomClearRewardSpawner rewardSpawner = controller.GetComponent<RoomClearRewardSpawner>();
                    if (rewardSpawner == null)
                        rewardSpawner = controller.gameObject.AddComponent<RoomClearRewardSpawner>();
                    rewardSpawner.ConfigureChest(chestContentTable,
                        new RoomChestSite(instance, node.Template, state, runProgress,
                            graph != null && graph.Player != null ? graph.Player.transform : null),
                        RoomClearRewardSpawner.DeriveChestSeed(node.ContentSeed));
                    controller.ConfigureClearReward(rewardSpawner);
                }
                else if (encounterClearDropTable != null)
                {
                    RoomClearRewardSpawner rewardSpawner = controller.GetComponent<RoomClearRewardSpawner>();
                    if (rewardSpawner == null)
                        rewardSpawner = controller.gameObject.AddComponent<RoomClearRewardSpawner>();
                    rewardSpawner.Configure(encounterClearDropTable, controller.transform,
                        instance.Node.ContentRoot.transform, state,
                        RoomClearRewardSpawner.DeriveDropSeed(node.ContentSeed), runProgress);
                    controller.ConfigureClearReward(rewardSpawner);
                }
                error = null;
                return true;
            }

            RoomDefinition definition = node.Definition;
            GameObject[] prefabs = new GameObject[controller.SpawnPoints.Count];
            GameObject floorBossPrefab = node.Role == GeneratedRoomRole.Boss
                ? ResolveBossPrefab(node.FloorNumber)
                : null;
            for (int i = 0; i < prefabs.Length; i++)
                prefabs[i] = floorBossPrefab != null
                    ? floorBossPrefab
                    : definition.EncounterPrefabs[i % definition.EncounterPrefabs.Count];
            controller.ConfigurePreplacedEnemies(Array.Empty<Health>());
            controller.ConfigureEnemyPrefabs(prefabs);
            error = null;
            return true;
        }

        private static void DestroyFloor(GameObject floorRoot)
        {
            if (floorRoot == null) return;
            foreach (FloorAdvancePortal portal in floorRoot.GetComponentsInChildren<FloorAdvancePortal>(true))
                portal.ReleaseRuntimeBindings();
            foreach (RewardRoom rewardRoom in floorRoot.GetComponentsInChildren<RewardRoom>(true))
                rewardRoom.ReleaseRuntimeBindings();
            foreach (BossItemDrop bossDrop in floorRoot.GetComponentsInChildren<BossItemDrop>(true))
                bossDrop.ReleaseRuntimeBindings();
            floorRoot.SetActive(false);
            if (Application.isPlaying) Destroy(floorRoot); else DestroyImmediate(floorRoot);
        }

        private bool TryCreateBindings(
            GeneratedFloorGraph generatedGraph,
            out List<RoomBinding> bindings,
            out string error)
        {
            bindings = new List<RoomBinding>(generatedGraph.Nodes.Count);
            if (graph.Nodes.Count != generatedGraph.Nodes.Count)
            {
                error = $"Generated graph has {generatedGraph.Nodes.Count} rooms but the scene graph has {graph.Nodes.Count}.";
                return false;
            }

            Dictionary<string, RoomNode> sceneNodes = new(StringComparer.Ordinal);
            foreach (RoomNode sceneNode in graph.Nodes)
            {
                if (sceneNode == null || !sceneNodes.TryAdd(sceneNode.RoomId, sceneNode))
                {
                    error = $"Scene room ID '{sceneNode?.RoomId}' is empty or duplicated.";
                    return false;
                }
            }

            foreach (GeneratedRoomNode generatedNode in generatedGraph.Nodes)
            {
                if (!sceneNodes.TryGetValue(generatedNode.RoomId, out RoomNode sceneNode) ||
                    sceneNode.FloorNumber != generatedNode.FloorNumber ||
                    sceneNode.RoomNumber != generatedNode.RoomNumber)
                {
                    error = $"Generated room {generatedNode.RoomId} has no matching scene room instance.";
                    return false;
                }

                RoomController controller = sceneNode.ContentRoot != null
                    ? sceneNode.ContentRoot.GetComponentInChildren<RoomController>(true)
                    : null;
                if (controller == null)
                {
                    error = $"Scene room {sceneNode.RoomId} is missing RoomController.";
                    return false;
                }

                RewardRoom reward = sceneNode.ContentRoot.GetComponentInChildren<RewardRoom>(true);
                if (generatedNode.Definition.RoomType == RoomType.Reward && reward == null)
                {
                    error = $"Generated reward room {sceneNode.RoomId} has no verified RewardRoom instance.";
                    return false;
                }

                if (generatedNode.Definition.RoomType == RoomType.Boss)
                {
                    if (controller.PreplacedEnemies.Count != 1 ||
                        controller.PreplacedEnemies[0] == null ||
                        controller.PreplacedEnemies[0].GetComponent<BossController>() == null)
                    {
                        error = $"Generated boss room {sceneNode.RoomId} has no verified preplaced boss.";
                        return false;
                    }
                }
                else if (controller.SpawnPoints.Count == 0)
                {
                    error = $"Generated encounter room {sceneNode.RoomId} has no spawn points.";
                    return false;
                }

                if (!TryResolveDoorways(
                        sceneNode,
                        generatedNode,
                        sceneNodes,
                        out RoomDoorway[] orderedDoorways,
                        out error))
                {
                    return false;
                }

                bindings.Add(new RoomBinding(
                    generatedNode,
                    sceneNode,
                    controller,
                    reward,
                    orderedDoorways));
            }

            error = null;
            return true;
        }

        private static bool TryResolveDoorways(
            RoomNode sceneNode,
            GeneratedRoomNode generatedNode,
            IReadOnlyDictionary<string, RoomNode> sceneNodes,
            out RoomDoorway[] orderedDoorways,
            out string error)
        {
            orderedDoorways = null;
            Dictionary<string, RoomDoorway> existing = new(StringComparer.Ordinal);
            foreach (RoomDoorway doorway in sceneNode.Doorways)
            {
                if (doorway == null || doorway.Destination == null ||
                    !existing.TryAdd(doorway.Destination.RoomId, doorway))
                {
                    error = $"Scene room {sceneNode.RoomId} has a missing or duplicate doorway destination.";
                    return false;
                }
            }

            if (existing.Count != generatedNode.ConnectedRoomIds.Count)
            {
                error = $"Scene room {sceneNode.RoomId} has {existing.Count} doorways but generated room needs " +
                        $"{generatedNode.ConnectedRoomIds.Count}.";
                return false;
            }

            orderedDoorways = new RoomDoorway[generatedNode.ConnectedRoomIds.Count];
            for (int index = 0; index < generatedNode.ConnectedRoomIds.Count; index++)
            {
                string destinationId = generatedNode.ConnectedRoomIds[index];
                if (!sceneNodes.ContainsKey(destinationId) ||
                    !existing.TryGetValue(destinationId, out RoomDoorway doorway))
                {
                    error = $"Scene room {sceneNode.RoomId} is missing generated connection to {destinationId}.";
                    return false;
                }

                orderedDoorways[index] = doorway;
            }

            error = null;
            return true;
        }

        private static void ApplyDefinition(RoomBinding binding)
        {
            RoomDefinition definition = binding.GeneratedNode.Definition;
            binding.SceneNode.ApplyGeneratedDefinition(definition);

            if (binding.Reward != null)
            {
                binding.Reward.gameObject.SetActive(definition.RoomType == RoomType.Reward);
            }

            if (definition.RoomType == RoomType.Boss)
            {
                return;
            }

            GameObject[] encounterPrefabs = new GameObject[binding.Controller.SpawnPoints.Count];
            for (int index = 0; index < encounterPrefabs.Length; index++)
            {
                encounterPrefabs[index] =
                    definition.EncounterPrefabs[index % definition.EncounterPrefabs.Count];
            }

            binding.Controller.ConfigurePreplacedEnemies(Array.Empty<Health>());
            binding.Controller.ConfigureEnemyPrefabs(encounterPrefabs);
        }

        private void ApplyDoorways(RoomBinding binding)
        {
            foreach (RoomDoorway doorway in binding.OrderedDoorways)
            {
                GeneratedRoomNode destinationGenerated = FindGeneratedNode(doorway.Destination.RoomId);
                bool requiresKey = destinationGenerated?.RequiresKey == true;
                DoorController visual = doorway.GetComponentInChildren<DoorController>();
                visual?.ConfigureVisualKind(ConnectionVisualKind(binding.GeneratedNode, destinationGenerated));
                doorway.Configure(
                    graph,
                    binding.SceneNode,
                    doorway.Destination,
                    doorway.DestinationEntryPoint,
                    doorway.RequiredClearedRoom,
                    doorway.AllowsOneWay,
                    requiresKey,
                    runProgress?.GetRoomState(destinationGenerated?.RoomId),
                    visual);
            }

            binding.SceneNode.SetDoorways(binding.OrderedDoorways);
        }

        public static DoorVisualKind ConnectionVisualKind(GeneratedRoomNode source, GeneratedRoomNode destination,
            bool isSecret = false)
        {
            bool HasRole(GeneratedRoomRole role) => source?.Role == role || destination?.Role == role;
            if (isSecret || HasRole(GeneratedRoomRole.Secret)) return DoorVisualKind.SecretPassage;
            if (HasRole(GeneratedRoomRole.Boss)) return DoorVisualKind.Boss;
            if (HasRole(GeneratedRoomRole.Treasure)) return DoorVisualKind.KeyLockedTreasure;
            if (HasRole(GeneratedRoomRole.Shop)) return DoorVisualKind.Shop;
            return DoorVisualKind.Normal;
        }

        private GeneratedRoomNode FindGeneratedNode(string roomId)
        {
            if (generatedGraph != null)
            {
                foreach (GeneratedRoomNode node in generatedGraph.Nodes)
                    if (string.Equals(node.RoomId, roomId, StringComparison.Ordinal)) return node;
            }
            return null;
        }

        private sealed class RoomBinding
        {
            public RoomBinding(
                GeneratedRoomNode generatedNode,
                RoomNode sceneNode,
                RoomController controller,
                RewardRoom reward,
                RoomDoorway[] orderedDoorways)
            {
                GeneratedNode = generatedNode;
                SceneNode = sceneNode;
                Controller = controller;
                Reward = reward;
                OrderedDoorways = orderedDoorways;
            }

            public GeneratedRoomNode GeneratedNode { get; }
            public RoomNode SceneNode { get; }
            public RoomController Controller { get; }
            public RewardRoom Reward { get; }
            public RoomDoorway[] OrderedDoorways { get; }
        }
    }
}
