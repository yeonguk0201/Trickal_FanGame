using System;
using System.Collections.Generic;
using TrickalFanGame.Audio;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // A 1x1 room obstacle on the Environment layer: it blocks players, enemies, projectiles and charges like a wall,
    // but player attacks and skills break it after a fixed number of hits. Hits are counted, not damage, so attack
    // power never lowers the count. Breaking rolls the obstacle's drop table once from the room seed, and the room
    // state keeps it broken across revisits and floor reloads.
    // Obstacle-5: a resource variant may leave several pickups from one successful drop roll, and a vault variant
    // ignores hits and opens only from a player bomb or one key spent on touch, with a rare extra spell or artifact.
    // Obstacle-7: a tree is a fixed kind that still blocks a flying player until it breaks, drops nothing, and
    // catches fire on its 2nd hit while the player holds a burn artifact. Breaking a burning one is recorded on the
    // Run for a later character unlock.
    // Bomb-Chain: an exploding obstacle without a fuse explodes the moment it breaks, with no bomb left behind. One
    // broken by another explosion waits the kind's short chain delay, so a row of them goes off one after another. A
    // kind with a fuse (Shady's random box) still leaves a visible bomb that explodes after it.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DestructibleObstacle : MonoBehaviour
    {
        private void OnEnable() => TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);

        public const int DefaultRequiredHits = 4;
        public const int TreeBurnHits = 2;
        public const string TreeVariantId = "tree";
        public const uint DropSeedSalt = 0x27D4EB2Fu;
        public const uint ExtraDropSeedSalt = 0x51ED270Bu;
        public const uint DropCountSeedSalt = 0x2F6B9A1Du;
        public const uint RareItemSeedSalt = 0x6A09E667u;
        public const uint ExplosionSeedSalt = 0x1F83D9ABu;
        public const uint EnemySeedSalt = 0x5BE0CD19u;
        // Several pickups from one obstacle spread on a ring inside its own cell, which is free once it breaks.
        public const float DropRingRadius = 0.28f;

        [SerializeField] private string obstacleId = "obstacle-01";
        [SerializeField] private string variantId = "rock";
        [SerializeField, Min(1)] private int requiredHits = DefaultRequiredHits;
        [SerializeField] private ResourceDropTable dropTable;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Color intactColor = new(0.62f, 0.45f, 0.3f);
        [SerializeField] private Color crackedColor = new(0.3f, 0.2f, 0.14f);
        [Tooltip("나무 같은 높은 장애물입니다. 켜면 부서지기 전까지 비행 중인 플레이어도 지나가지 못합니다.")]
        [SerializeField] private bool blocksFlight;
        [Tooltip("화상 아티팩트를 가진 플레이어가 이만큼 때리면 불이 붙습니다. 0이면 불타지 않습니다.")]
        [SerializeField, Min(0)] private int burnHits;
        [SerializeField] private Color burningColor = new(0.95f, 0.45f, 0.1f);
        [Tooltip("Layout에 종류를 고정한 장애물입니다. 비어 있으면 종류는 방의 변형 표가 seed로 정합니다.")]
        [SerializeField] private ObstacleVariantDefinition fixedVariant;

        private RoomRunState runState;
        private RunProgress runProgress;
        private int dropCountMinimum;
        private int dropCountMaximum;
        private SecretRoomLink secretLink;
        private RoomNode sourceNode;
        private RoomController sourceRoom;
        private Transform dropParent;
        private ObstacleVariantDefinition variant;
        private IReadOnlyList<ItemDefinition> rareArtifactPool;
        private PlayerInventory rareItemInventory;
        private PlayerStats burnSource;
        private readonly List<GameObject> lastDrops = new();
        private readonly List<GameObject> lastEnemies = new();
        private int dropSeed;
        private int hitsTaken;
        private bool isBroken;
        private bool isBurning;
        private Vector2 lastHitPoint;
        private bool hasLastHitPoint;

        public string ObstacleId => obstacleId;
        public string VariantId => variantId;
        public ObstacleVariantDefinition FixedVariant => fixedVariant;
        public int RequiredHits => Mathf.Max(1, requiredHits);
        public int HitsTaken => hitsTaken;
        public bool IsBroken => isBroken;
        public bool BlocksFlight => blocksFlight;
        public int BurnHits => burnHits;
        // Burning is not kept across a room rebuild, like the hit count; only a broken obstacle is.
        public bool IsBurning => isBurning;
        // Whether the obstacle was burning when it broke.
        public bool BrokeWhileBurning => isBroken && isBurning;
        // Where the last counted hit or the breaking explosion came from, when the caller knew it. A tree falls away
        // from it (TreeFallEffect).
        public bool HasLastHitPoint => hasLastHitPoint;
        public Vector2 LastHitPoint => lastHitPoint;
        public ResourceDropTable DropTable => dropTable;
        public int DropSeed => dropSeed;
        public ObstacleBreakRule BreakRule => variant != null ? variant.BreakRule : ObstacleBreakRule.Hits;
        public int DropCount
        {
            get
            {
                // Balance-1: a seeded count within an explicit range (shop decor) instead of the variant's count.
                if (dropCountMaximum > 0) return ResolveDropCount(dropSeed, dropCountMinimum, dropCountMaximum);
                return variant != null ? variant.DropCount : 1;
            }
        }

        public static int ResolveDropCount(int seed, int minimum, int maximum)
        {
            minimum = Mathf.Max(1, minimum);
            int span = Mathf.Max(minimum, maximum) - minimum + 1;
            uint roll = unchecked((uint)FloorGenerator.DeriveSeed(seed, 0, DropCountSeedSalt));
            return minimum + (int)(roll % (uint)span);
        }

        public bool HasDropCountRange => dropCountMaximum > 0;
        public int DropCountMinimum => dropCountMinimum;
        public int DropCountMaximum => dropCountMaximum;
        // The first pickup of the last break; LastDrops holds all of them, a rare item last.
        public GameObject LastDrop { get; private set; }
        public IReadOnlyList<GameObject> LastDrops => lastDrops;
        // The explosion the last break set off instead of dropping. A chained one is still waiting on its delay.
        public PlacedBomb LastExplosion { get; private set; }
        // The enemies the last break released instead of dropping.
        public IReadOnlyList<GameObject> LastEnemies => lastEnemies;
        public event Action<DestructibleObstacle> Broken;
        // A counted hit that leaves the obstacle standing; the breaking hit raises Broken instead.
        public event Action<DestructibleObstacle> Hit;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development panel only: the next obstacle broken on a floor with a secret room leaves a secret pit.
        public static bool DevelopmentForceNextSecretPit { get; set; }
        // Development panel only: the next broken obstacle whose kind can release enemies does so.
        public static bool DevelopmentForceNextEnemies { get; set; }
#endif

        public static int ObstacleMask => LayerMask.GetMask("Environment");

        public void Configure(string configuredObstacleId, int configuredRequiredHits, ResourceDropTable configuredTable,
            SpriteRenderer configuredVisual)
        {
            obstacleId = configuredObstacleId;
            variantId = "rock";
            requiredHits = Mathf.Max(1, configuredRequiredHits);
            dropTable = configuredTable;
            visual = configuredVisual;
        }

        // Balance-1: every drop of this obstacle leaves minimum..maximum pickups, picked from its drop seed.
        public void ConfigureDropCountRange(int minimum, int maximum)
        {
            dropCountMinimum = Mathf.Max(1, minimum);
            dropCountMaximum = Mathf.Max(dropCountMinimum, maximum);
        }

        // Trap-0: a placed copy of an obstacle trap Prefab takes its own ID and keeps the Prefab's kind.
        public void ConfigureObstacleId(string configuredObstacleId) => obstacleId = configuredObstacleId;

        // A fixed kind authored in a Layout instead of resolved from a variant table.
        public void ConfigureFixedKind(string configuredVariantId, Color configuredIntactColor,
            Color configuredCrackedColor, bool configuredBlocksFlight, int configuredBurnHits,
            Color configuredBurningColor)
        {
            variantId = configuredVariantId;
            intactColor = configuredIntactColor;
            crackedColor = configuredCrackedColor;
            blocksFlight = configuredBlocksFlight;
            burnHits = Mathf.Max(0, configuredBurnHits);
            burningColor = configuredBurningColor;
            if (visual != null) visual.color = intactColor;
        }

        // A kind authored into a Layout cell (T7). Unlike a seeded variant slot it is the same in every Run, and it
        // does not count toward the one seeded special obstacle of a room.
        public void ConfigureFixedVariant(ObstacleVariantDefinition configuredVariant)
        {
            fixedVariant = configuredVariant;
            if (configuredVariant != null) ApplyVariant(configuredVariant);
        }

        public void ApplyVariant(ObstacleVariantDefinition variant)
        {
            if (variant == null) throw new ArgumentNullException(nameof(variant));
            this.variant = variant;
            variantId = variant.VariantId;
            requiredHits = variant.RequiredHits;
            dropTable = variant.DropTable;
            intactColor = variant.IntactColor;
            crackedColor = variant.CrackedColor;
            if (visual != null) visual.color = intactColor;
        }

        // The artifacts a variant's rare item may be, and the inventory that decides which are still acquirable.
        public void BindRareItems(IReadOnlyList<ItemDefinition> artifactPool, PlayerInventory inventory)
        {
            rareArtifactPool = artifactPool;
            rareItemInventory = inventory;
        }

        // The player whose burn artifacts set this obstacle on fire.
        public void BindBurnSource(PlayerStats stats) => burnSource = stats;

        // Called when the room is built. A broken state from an earlier visit removes the obstacle immediately, and a
        // secret pit it left is restored because the pit is part of the room, not a one-time pickup. Without a
        // secret room on the floor (null link) the secret pit candidate leaves the drop roll.
        public void Bind(RoomRunState configuredState, int roomContentSeed, Transform configuredDropParent,
            RunProgress configuredProgress, SecretRoomLink configuredSecretLink = null,
            RoomNode configuredSourceNode = null, RoomController configuredSourceRoom = null)
        {
            // The break rule, drop count and rare rolls live on the variant, which is not serialized on its own.
            if (fixedVariant != null) ApplyVariant(fixedVariant);
            runState = configuredState;
            dropSeed = DeriveDropSeed(roomContentSeed, obstacleId);
            dropParent = configuredDropParent;
            runProgress = configuredProgress;
            secretLink = configuredSecretLink;
            sourceNode = configuredSourceNode;
            sourceRoom = configuredSourceRoom;
            if (runState != null && runState.IsObstacleDestroyed(obstacleId))
            {
                isBroken = true;
                hitsTaken = RequiredHits;
                if (TryRollDrop(out ResourceDropEntry entry) && IsSecretPit(entry)) SpawnDrop(entry);
                gameObject.SetActive(false);
            }
        }

        public static bool IsSecretPit(ResourceDropEntry entry) =>
            entry?.Prefab != null && entry.Prefab.GetComponent<SecretPit>() != null;

        public bool TryRollDrop(out ResourceDropEntry entry)
        {
            entry = null;
            return dropTable != null &&
                   dropTable.TryRoll(dropSeed, candidate => secretLink != null || !IsSecretPit(candidate), out entry);
        }

        // Every pickup this obstacle leaves when it breaks. The first is the seeded drop roll; a variant with a drop
        // count above one adds weighted picks without another chance roll, and never a second secret pit.
        public List<ResourceDropEntry> RollDrops()
        {
            List<ResourceDropEntry> drops = new();
            if (!TryRollDrop(out ResourceDropEntry first)) return drops;
            drops.Add(first);
            for (int index = 1; index < DropCount; index++)
            {
                if (dropTable.TryPick(FloorGenerator.DeriveSeed(dropSeed, index, ExtraDropSeedSalt),
                        candidate => !IsSecretPit(candidate), out ResourceDropEntry extra))
                {
                    drops.Add(extra);
                }
            }

            return drops;
        }

        // Whether this obstacle explodes when it breaks. An exploding obstacle drops nothing.
        public bool TryRollExplosion()
        {
            if (variant == null || variant.ExplosionChance <= 0f || variant.ExplosionPrefab == null) return false;
            uint state = unchecked((uint)FloorGenerator.DeriveSeed(dropSeed, 0, ExplosionSeedSalt));
            return NextUnit(ref state) < variant.ExplosionChance;
        }

        // Whether this obstacle releases its kind's enemies when it breaks, instead of dropping. An obstacle that
        // explodes never does.
        public bool TryRollEnemies()
        {
            if (variant == null || variant.EnemyChance <= 0f || variant.EnemyPrefab == null) return false;
            uint state = unchecked((uint)FloorGenerator.DeriveSeed(dropSeed, 0, EnemySeedSalt));
            return NextUnit(ref state) < variant.EnemyChance;
        }

        // The variant's rare item roll, independent of the drop table. An artifact roll without an acquirable
        // artifact gives a spell instead, so the roll never leaves a pickup the player cannot take.
        public bool TryRollRareItem(out ItemDefinition item)
        {
            item = null;
            if (variant == null || variant.RareItemChance <= 0f || variant.RareSpells.Count == 0) return false;
            uint state = unchecked((uint)FloorGenerator.DeriveSeed(dropSeed, 0, RareItemSeedSalt));
            if (NextUnit(ref state) >= variant.RareItemChance) return false;
            bool wantsArtifact = NextUnit(ref state) < variant.RareArtifactShare;
            float spellPick = NextUnit(ref state);
            if (wantsArtifact && variant.ArtifactPickupPrefab != null && rareArtifactPool != null)
            {
                List<ItemDefinition> artifacts = new();
                foreach (ItemDefinition candidate in rareArtifactPool)
                    if (candidate != null && candidate.Kind == ItemKind.Artifact) artifacts.Add(candidate);
                // Pool-0: a rare obstacle artifact draws from the treasure pool first, like a treasure room.
                if (ArtifactRewardSelector.TryChoose(artifacts, rareItemInventory, dropSeed, obstacleId,
                        ItemPoolSource.Treasure, out item))
                    return true;
            }

            item = variant.RareSpells[Mathf.Min(variant.RareSpells.Count - 1,
                (int)(spellPick * variant.RareSpells.Count))];
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(obstacleId, "Obstacle", out error)) return false;
            if (!StableRoomId.TryValidate(variantId, "Obstacle variant", out error)) return false;
            Collider2D obstacleCollider = GetComponent<Collider2D>();
            if (obstacleCollider == null || !obstacleCollider.enabled || obstacleCollider.isTrigger)
            {
                error = $"Destructible obstacle '{obstacleId}' requires one enabled solid Collider2D.";
                return false;
            }

            if (gameObject.layer != LayerMask.NameToLayer("Environment"))
            {
                error = $"Destructible obstacle '{obstacleId}' must use the Environment layer.";
                return false;
            }

            if (dropTable != null && !dropTable.TryValidate(out error)) return false;
            if (fixedVariant != null)
            {
                if (!fixedVariant.TryValidate(out error)) return false;
                if (GetComponent<RoomObstacleVariantSlot>() != null)
                {
                    error = $"Destructible obstacle '{obstacleId}' cannot be both a fixed kind and a seeded slot.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        // One player attack or skill hit. Returns true when the hit counted.
        public bool RegisterPlayerHit() => RegisterPlayerHit(null);

        // hitPoint: the attack center or the shot's position, so a tree can fall away from the hit.
        public bool RegisterPlayerHit(Vector2? hitPoint)
        {
            if (isBroken || !isActiveAndEnabled || BreakRule != ObstacleBreakRule.Hits) return false;
            RememberHitPoint(hitPoint);
            hitsTaken++;
            CombatSfx.Play(CombatSfxCue.ObstacleHit);
            // Holding a burn artifact is enough; its burn chance is not rolled. The hit that breaks the obstacle
            // does not light it.
            if (!isBurning && burnHits > 0 && hitsTaken >= burnHits && hitsTaken < RequiredHits &&
                burnSource != null && burnSource.HasBurnSource)
            {
                isBurning = true;
            }

            if (visual != null)
            {
                visual.color = isBurning
                    ? burningColor
                    : Color.Lerp(intactColor, crackedColor, hitsTaken / (float)RequiredHits);
            }

            if (hitsTaken >= RequiredHits) Break(false);
            else Hit?.Invoke(this);
            return true;
        }

        // Hits every destructible obstacle touched by a player attack area once.
        public static int HitInCircle(Vector2 center, float radius, ISet<DestructibleObstacle> alreadyHit = null)
        {
            int hits = 0;
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(center, radius, ObstacleMask))
            {
                if (TryHitCollider(collider, alreadyHit, center)) hits++;
            }

            return hits;
        }

        public static int DestroyByBombInCircle(Vector2 center, float radius,
            ISet<DestructibleObstacle> alreadyDestroyed = null)
        {
            int destroyed = 0;
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(center, radius, ObstacleMask))
            {
                DestructibleObstacle obstacle = collider != null
                    ? collider.GetComponentInParent<DestructibleObstacle>()
                    : null;
                if (obstacle == null ||
                    (alreadyDestroyed != null && !alreadyDestroyed.Add(obstacle)) ||
                    !obstacle.TryDestroyByBomb(center))
                {
                    continue;
                }

                destroyed++;
            }

            return destroyed;
        }

        public bool TryDestroyByBomb() => TryDestroyByBomb(null);

        public bool TryDestroyByBomb(Vector2? explosionCenter)
        {
            if (isBroken || !isActiveAndEnabled) return false;
            RememberHitPoint(explosionCenter);
            hitsTaken = RequiredHits;
            if (visual != null) visual.color = crackedColor;
            Break(true);
            return true;
        }

        // A vault opens when the player touches it holding a key, which it spends. Nothing happens without one.
        public bool TryOpenWithKey()
        {
            if (isBroken || !isActiveAndEnabled || BreakRule != ObstacleBreakRule.BombOrKey || runProgress == null ||
                runProgress.IsProgressionStopped || !runProgress.TrySpendResource(RunResourceType.Key))
            {
                return false;
            }

            hitsTaken = RequiredHits;
            if (visual != null) visual.color = crackedColor;
            Break(false);
            return true;
        }

        private void OnCollisionEnter2D(Collision2D collision) => HandlePlayerContact(collision);

        // Stay lets a player still pressing against a vault open it once they pick up a key.
        private void OnCollisionStay2D(Collision2D collision) => HandlePlayerContact(collision);

        private void HandlePlayerContact(Collision2D collision)
        {
            if (BreakRule != ObstacleBreakRule.BombOrKey) return;
            if (collision.collider.GetComponentInParent<PlayerMovement>() != null) TryOpenWithKey();
        }

        public static bool TryHitCollider(Collider2D collider, ISet<DestructibleObstacle> alreadyHit = null,
            Vector2? hitPoint = null)
        {
            DestructibleObstacle obstacle = collider != null ? collider.GetComponentInParent<DestructibleObstacle>() : null;
            if (obstacle == null || (alreadyHit != null && !alreadyHit.Add(obstacle))) return false;
            return obstacle.RegisterPlayerHit(hitPoint);
        }

        private void RememberHitPoint(Vector2? hitPoint)
        {
            if (!hitPoint.HasValue) return;
            lastHitPoint = hitPoint.Value;
            hasLastHitPoint = true;
        }

        // Stable across runs and platforms (FNV-1a over the stable obstacle ID), unlike string.GetHashCode.
        public static int DeriveDropSeed(int roomContentSeed, string configuredObstacleId)
        {
            uint hash = 2166136261u;
            foreach (char character in configuredObstacleId ?? string.Empty)
            {
                hash = unchecked((hash ^ character) * 16777619u);
            }

            return FloorGenerator.DeriveSeed(roomContentSeed, unchecked((int)hash), DropSeedSalt);
        }

        // byExplosion: a bomb or another obstacle's explosion broke it, which delays its own explosion for the chain.
        private void Break(bool byExplosion)
        {
            isBroken = true;
            // Sound pass 2026-11-22: rocks, trees, boxes and the 부스러기 boss's obstacles crumble. A fire trap is put
            // out instead; its loop dies away in CombatAmbienceAudio.
            RoomTrap trap = GetComponent<RoomTrap>();
            if (trap == null || trap.Kind != RoomTrapKind.Fire) CombatSfx.Play(CombatSfxCue.ObstacleBreak);
            bool firstBreak = runState == null || runState.TryMarkObstacleDestroyed(obstacleId);
            if (firstBreak && isBurning && runProgress != null) runProgress.RecordBurnedObstacle();
            if (firstBreak && TryRollDevelopmentPit(out ResourceDropEntry forcedPit)) SpawnDrop(forcedPit);
            else if (firstBreak && TryForceDevelopmentEnemies()) SpawnEnemies();
            else if (firstBreak && TryRollExplosion()) SpawnExplosion(byExplosion);
            else if (firstBreak && TryRollEnemies()) SpawnEnemies();
            else if (firstBreak) SpawnRolledDrops();

            Broken?.Invoke(this);
            gameObject.SetActive(false);
        }

        private bool TryRollDevelopmentPit(out ResourceDropEntry entry)
        {
            entry = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!DevelopmentForceNextSecretPit || secretLink == null || dropTable == null) return false;
            foreach (ResourceDropEntry candidate in dropTable.Entries)
            {
                if (!IsSecretPit(candidate)) continue;
                entry = candidate;
                DevelopmentForceNextSecretPit = false;
                return true;
            }
#endif
            return false;
        }

        private bool TryForceDevelopmentEnemies()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevelopmentForceNextEnemies && variant != null && variant.EnemyPrefab != null)
            {
                DevelopmentForceNextEnemies = false;
                return true;
            }
#endif
            return false;
        }

        // The enemies leave the broken cell on the same ring as pickups. The room registers them when it is known,
        // so they scale with the floor and count as kills.
        private void SpawnEnemies()
        {
            for (int index = 0; index < variant.EnemyCount; index++)
            {
                Vector3 position = transform.position + (Vector3)DropOffset(index, variant.EnemyCount);
                GameObject enemy;
                if (sourceRoom != null)
                {
                    Health spawned = sourceRoom.SpawnExtraEnemy(variant.EnemyPrefab, position);
                    enemy = spawned != null ? spawned.gameObject : null;
                }
                else
                {
                    enemy = Instantiate(variant.EnemyPrefab, position, Quaternion.identity,
                        dropParent != null ? dropParent : transform.parent);
                }

                if (enemy == null) continue;
                enemy.name = $"Obstacle Enemy {index + 1} - {obstacleId}";
                lastEnemies.Add(enemy);
            }
            if (lastEnemies.Count > 0) CombatSfx.Play(CombatSfxCue.EnemyAppear);
        }

        private void SpawnExplosion(bool byExplosion)
        {
            LastExplosion = Instantiate(variant.ExplosionPrefab, transform.position, Quaternion.identity,
                dropParent != null ? dropParent : transform.parent);
            LastExplosion.name = $"Obstacle Explosion - {obstacleId}";
            if (variant.ExplosionFuse > 0f) LastExplosion.ConfigureUnowned(Time.time, variant.ExplosionFuse);
            else LastExplosion.ConfigureUnownedBlast(Time.time, byExplosion ? variant.ExplosionChainDelay : 0f);
        }

        private void SpawnRolledDrops()
        {
            List<ResourceDropEntry> drops = RollDrops();
            bool hasRareItem = TryRollRareItem(out ItemDefinition rareItem);
            int total = drops.Count + (hasRareItem ? 1 : 0);
            // A secret pit is part of the room, so it stays on the obstacle's cell center.
            for (int index = 0; index < drops.Count; index++)
                SpawnDrop(drops[index], IsSecretPit(drops[index]) ? Vector2.zero : DropOffset(index, total), index);
            if (hasRareItem) SpawnRareItem(rareItem, DropOffset(total - 1, total));
        }

        public static Vector2 DropOffset(int index, int total)
        {
            if (total <= 1) return Vector2.zero;
            float angle = (0.25f + index / (float)total) * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * DropRingRadius;
        }

        // pickupIndex: the drop's place among this break's drops; each gold drop rolls on its own for elif.
        private void SpawnDrop(ResourceDropEntry entry, Vector2 offset = default, int pickupIndex = 0)
        {
            if (entry?.Prefab == null) return;
            GameObject drop = Instantiate(entry.Prefab, transform.position + (Vector3)offset, Quaternion.identity,
                dropParent != null ? dropParent : transform.parent);
            drop.name = $"Obstacle Drop {entry.DropId} - {obstacleId}";
            if (runProgress != null && drop.TryGetComponent(out RunResourcePickup resourcePickup))
            {
                resourcePickup.BindRunProgress(runProgress);
                PermanentElif.TryConvertSpawnedGold(drop, runProgress.RunSeed, runProgress.HasRunSeed,
                    runState != null ? runState.RoomId : "room", obstacleId, pickupIndex);
            }

            if (drop.TryGetComponent(out SecretPit pit))
            {
                pit.Bind(secretLink, sourceNode, sourceRoom);
            }

            RecordDrop(drop);
        }

        private void SpawnRareItem(ItemDefinition item, Vector2 offset)
        {
            Vector3 position = transform.position + (Vector3)offset;
            Transform parent = dropParent != null ? dropParent : transform.parent;
            if (item.Kind == ItemKind.Artifact)
            {
                ItemPickup artifact = Instantiate(variant.ArtifactPickupPrefab, position, Quaternion.identity, parent);
                artifact.name = $"Obstacle Artifact {item.ItemId} - {obstacleId}";
                artifact.Configure(item);
                RecordDrop(artifact.gameObject);
                return;
            }

            SingleUseItemPickup spell = Instantiate(variant.SpellPickupPrefab, position, Quaternion.identity, parent);
            spell.name = $"Obstacle Spell {item.ItemId} - {obstacleId}";
            // A key opens the vault by touch, so a full slot does not swap until the player steps off and back on.
            spell.Configure(item, $"{(runState != null ? runState.RoomId : "room")}-{obstacleId}-spell", true);
            RecordDrop(spell.gameObject);
        }

        private void RecordDrop(GameObject drop)
        {
            if (LastDrop == null) LastDrop = drop;
            lastDrops.Add(drop);
        }

        // SplitMix32 step mapped to [0, 1), the same generator as ResourceDropTable, so seeds replay exactly.
        private static float NextUnit(ref uint state)
        {
            unchecked
            {
                state += 0x9E3779B9u;
                uint value = state;
                value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
                value = (value ^ (value >> 13)) * 0xC2B2AE35u;
                value ^= value >> 16;
                return (value >> 8) * (1f / 16777216f);
            }
        }
    }
}
