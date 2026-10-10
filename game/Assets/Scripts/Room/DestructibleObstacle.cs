using System;
using System.Collections.Generic;
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

        private RoomRunState runState;
        private RunProgress runProgress;
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

        public string ObstacleId => obstacleId;
        public string VariantId => variantId;
        public int RequiredHits => Mathf.Max(1, requiredHits);
        public int HitsTaken => hitsTaken;
        public bool IsBroken => isBroken;
        public bool BlocksFlight => blocksFlight;
        public int BurnHits => burnHits;
        // Burning is not kept across a room rebuild, like the hit count; only a broken obstacle is.
        public bool IsBurning => isBurning;
        // Whether the obstacle was burning when it broke.
        public bool BrokeWhileBurning => isBroken && isBurning;
        public ResourceDropTable DropTable => dropTable;
        public int DropSeed => dropSeed;
        public ObstacleBreakRule BreakRule => variant != null ? variant.BreakRule : ObstacleBreakRule.Hits;
        public int DropCount => variant != null ? variant.DropCount : 1;
        // The first pickup of the last break; LastDrops holds all of them, a rare item last.
        public GameObject LastDrop { get; private set; }
        public IReadOnlyList<GameObject> LastDrops => lastDrops;
        // The explosion the last break armed instead of dropping, until it goes off.
        public PlacedBomb LastExplosion { get; private set; }
        // The enemies the last break released instead of dropping.
        public IReadOnlyList<GameObject> LastEnemies => lastEnemies;
        public event Action<DestructibleObstacle> Broken;
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
                if (ArtifactRewardSelector.TryChoose(artifacts, rareItemInventory, dropSeed, obstacleId, out item))
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
            error = null;
            return true;
        }

        // One player attack or skill hit. Returns true when the hit counted.
        public bool RegisterPlayerHit()
        {
            if (isBroken || !isActiveAndEnabled || BreakRule != ObstacleBreakRule.Hits) return false;
            hitsTaken++;
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

            if (hitsTaken >= RequiredHits) Break();
            return true;
        }

        // Hits every destructible obstacle touched by a player attack area once.
        public static int HitInCircle(Vector2 center, float radius, ISet<DestructibleObstacle> alreadyHit = null)
        {
            int hits = 0;
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(center, radius, ObstacleMask))
            {
                if (TryHitCollider(collider, alreadyHit)) hits++;
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
                    !obstacle.TryDestroyByBomb())
                {
                    continue;
                }

                destroyed++;
            }

            return destroyed;
        }

        public bool TryDestroyByBomb()
        {
            if (isBroken || !isActiveAndEnabled) return false;
            hitsTaken = RequiredHits;
            if (visual != null) visual.color = crackedColor;
            Break();
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
            Break();
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

        public static bool TryHitCollider(Collider2D collider, ISet<DestructibleObstacle> alreadyHit = null)
        {
            DestructibleObstacle obstacle = collider != null ? collider.GetComponentInParent<DestructibleObstacle>() : null;
            if (obstacle == null || (alreadyHit != null && !alreadyHit.Add(obstacle))) return false;
            return obstacle.RegisterPlayerHit();
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

        private void Break()
        {
            isBroken = true;
            bool firstBreak = runState == null || runState.TryMarkObstacleDestroyed(obstacleId);
            if (firstBreak && isBurning && runProgress != null) runProgress.RecordBurnedObstacle();
            if (firstBreak && TryRollDevelopmentPit(out ResourceDropEntry forcedPit)) SpawnDrop(forcedPit);
            else if (firstBreak && TryForceDevelopmentEnemies()) SpawnEnemies();
            else if (firstBreak && TryRollExplosion()) SpawnExplosion();
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
        }

        private void SpawnExplosion()
        {
            LastExplosion = Instantiate(variant.ExplosionPrefab, transform.position, Quaternion.identity,
                dropParent != null ? dropParent : transform.parent);
            LastExplosion.name = $"Obstacle Explosion - {obstacleId}";
            LastExplosion.ConfigureUnowned(Time.time, variant.ExplosionFuse);
        }

        private void SpawnRolledDrops()
        {
            List<ResourceDropEntry> drops = RollDrops();
            bool hasRareItem = TryRollRareItem(out ItemDefinition rareItem);
            int total = drops.Count + (hasRareItem ? 1 : 0);
            // A secret pit is part of the room, so it stays on the obstacle's cell center.
            for (int index = 0; index < drops.Count; index++)
                SpawnDrop(drops[index], IsSecretPit(drops[index]) ? Vector2.zero : DropOffset(index, total));
            if (hasRareItem) SpawnRareItem(rareItem, DropOffset(total - 1, total));
        }

        public static Vector2 DropOffset(int index, int total)
        {
            if (total <= 1) return Vector2.zero;
            float angle = (0.25f + index / (float)total) * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * DropRingRadius;
        }

        private void SpawnDrop(ResourceDropEntry entry, Vector2 offset = default)
        {
            if (entry?.Prefab == null) return;
            GameObject drop = Instantiate(entry.Prefab, transform.position + (Vector3)offset, Quaternion.identity,
                dropParent != null ? dropParent : transform.parent);
            drop.name = $"Obstacle Drop {entry.DropId} - {obstacleId}";
            if (runProgress != null && drop.TryGetComponent(out RunResourcePickup resourcePickup))
            {
                resourcePickup.BindRunProgress(runProgress);
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
