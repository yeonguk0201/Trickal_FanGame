using System;
using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Obstacle-5: how an obstacle kind breaks. A vault ignores attack and skill hits; a player bomb explosion or one
    // key spent on touch opens it.
    public enum ObstacleBreakRule
    {
        Hits = 0,
        BombOrKey = 1,
    }

    // One obstacle kind a candidate slot can become. Obstacle-5 adds the break rule, how many pickups a successful
    // drop roll leaves, a rare single item (a spell or an artifact) rolled separately from the drop table, and a
    // chance to explode like a player bomb instead of dropping anything.
    [CreateAssetMenu(fileName = "ObstacleVariant", menuName = "Trickal Fan Game/Obstacle Variant")]
    public sealed class ObstacleVariantDefinition : ScriptableObject
    {
        [SerializeField] private string variantId;
        [SerializeField, Min(1)] private int requiredHits = DestructibleObstacle.DefaultRequiredHits;
        [SerializeField] private ResourceDropTable dropTable;
        [SerializeField] private Color intactColor = new(0.62f, 0.45f, 0.3f);
        [SerializeField] private Color crackedColor = new(0.3f, 0.2f, 0.14f);
        [SerializeField] private ObstacleBreakRule breakRule = ObstacleBreakRule.Hits;
        [SerializeField, Min(1)] private int dropCount = 1;
        [SerializeField, Range(0f, 1f)] private float rareItemChance;
        [Tooltip("희귀 아이템이 나올 때 스펠 대신 아티팩트가 나오는 비율입니다.")]
        [SerializeField, Range(0f, 1f)] private float rareArtifactShare;
        [SerializeField] private ItemDefinition[] rareSpells = Array.Empty<ItemDefinition>();
        [SerializeField] private SingleUseItemPickup spellPickupPrefab;
        [SerializeField] private ItemPickup artifactPickupPrefab;
        [Tooltip("부서질 때 드롭 대신 폭발할 확률입니다. 폭발은 플레이어 폭탄과 같습니다.")]
        [SerializeField, Range(0f, 1f)] private float explosionChance;
        [SerializeField, Min(0f)] private float explosionFuse = 0.5f;
        [SerializeField] private PlacedBomb explosionPrefab;
        [Tooltip("폭발하지 않았을 때 드롭 대신 적이 나올 확률입니다.")]
        [SerializeField, Range(0f, 1f)] private float enemyChance;
        [SerializeField, Min(1)] private int enemyCount = 1;
        [SerializeField] private GameObject enemyPrefab;

        public string VariantId => variantId;
        public int RequiredHits => Mathf.Max(1, requiredHits);
        public ResourceDropTable DropTable => dropTable;
        public Color IntactColor => intactColor;
        public Color CrackedColor => crackedColor;
        public ObstacleBreakRule BreakRule => breakRule;
        public int DropCount => Mathf.Max(1, dropCount);
        public float RareItemChance => rareItemChance;
        public float RareArtifactShare => rareArtifactShare;
        public IReadOnlyList<ItemDefinition> RareSpells => rareSpells ?? Array.Empty<ItemDefinition>();
        public SingleUseItemPickup SpellPickupPrefab => spellPickupPrefab;
        public ItemPickup ArtifactPickupPrefab => artifactPickupPrefab;
        public float ExplosionChance => explosionChance;
        public float ExplosionFuse => explosionFuse;
        public PlacedBomb ExplosionPrefab => explosionPrefab;
        public float EnemyChance => enemyChance;
        public int EnemyCount => Mathf.Max(1, enemyCount);
        public GameObject EnemyPrefab => enemyPrefab;

        public void Configure(string configuredVariantId, int configuredRequiredHits,
            ResourceDropTable configuredDropTable, Color configuredIntactColor, Color configuredCrackedColor)
        {
            variantId = configuredVariantId;
            requiredHits = Mathf.Max(1, configuredRequiredHits);
            dropTable = configuredDropTable;
            intactColor = configuredIntactColor;
            crackedColor = configuredCrackedColor;
        }

        public void ConfigureBreakAndDrops(ObstacleBreakRule configuredBreakRule, int configuredDropCount)
        {
            breakRule = configuredBreakRule;
            dropCount = Mathf.Max(1, configuredDropCount);
        }

        public void ConfigureRareItems(float configuredChance, float configuredArtifactShare,
            ItemDefinition[] configuredSpells, SingleUseItemPickup configuredSpellPickupPrefab,
            ItemPickup configuredArtifactPickupPrefab)
        {
            rareItemChance = Mathf.Clamp01(configuredChance);
            rareArtifactShare = Mathf.Clamp01(configuredArtifactShare);
            rareSpells = configuredSpells ?? Array.Empty<ItemDefinition>();
            spellPickupPrefab = configuredSpellPickupPrefab;
            artifactPickupPrefab = configuredArtifactPickupPrefab;
        }

        public void ConfigureExplosion(float configuredChance, float configuredFuse, PlacedBomb configuredPrefab)
        {
            explosionChance = Mathf.Clamp01(configuredChance);
            explosionFuse = Mathf.Max(0f, configuredFuse);
            explosionPrefab = configuredPrefab;
        }

        public void ConfigureEnemies(float configuredChance, int configuredCount, GameObject configuredPrefab)
        {
            enemyChance = Mathf.Clamp01(configuredChance);
            enemyCount = Mathf.Max(1, configuredCount);
            enemyPrefab = configuredPrefab;
        }

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(variantId, "Obstacle variant", out error)) return false;
            if (requiredHits < 1)
            {
                error = $"Obstacle variant '{variantId}' needs at least one hit.";
                return false;
            }

            // Only a kind that always explodes may go without a drop table.
            if (dropTable == null ? explosionChance < 1f : !dropTable.TryValidate(out error))
            {
                error = $"Obstacle variant '{variantId}' needs a valid drop table. {error}";
                return false;
            }

            if (explosionChance > 0f && explosionPrefab == null)
            {
                error = $"Obstacle variant '{variantId}' needs the bomb Prefab for its explosion.";
                return false;
            }

            if (enemyChance > 0f && (enemyPrefab == null || enemyPrefab.GetComponent<Combat.Health>() == null))
            {
                error = $"Obstacle variant '{variantId}' needs an enemy Prefab with Health for its enemies.";
                return false;
            }

            if (!Enum.IsDefined(typeof(ObstacleBreakRule), breakRule) || dropCount < 1)
            {
                error = $"Obstacle variant '{variantId}' needs a defined break rule and at least one drop.";
                return false;
            }

            if (rareItemChance > 0f)
            {
                HashSet<ItemDefinition> seenSpells = new();
                foreach (ItemDefinition spell in RareSpells)
                {
                    if (spell == null || !spell.IsValid || !spell.IsActive || spell.Kind != ItemKind.SingleUseSpell ||
                        !seenSpells.Add(spell))
                    {
                        error = $"Obstacle variant '{variantId}' rare spells must be distinct, active and valid " +
                                "single-use spells.";
                        return false;
                    }
                }

                // An artifact roll falls back to a spell when no artifact is available, so spells are always needed.
                if (seenSpells.Count == 0 || spellPickupPrefab == null)
                {
                    error = $"Obstacle variant '{variantId}' needs rare spells and the single-use item pickup Prefab.";
                    return false;
                }

                if (rareArtifactShare > 0f && artifactPickupPrefab == null)
                {
                    error = $"Obstacle variant '{variantId}' needs the artifact pickup Prefab.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
