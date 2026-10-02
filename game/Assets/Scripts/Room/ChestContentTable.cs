using System;
using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Chest-1: how many consumables a chest kind holds and how likely it adds a spell. countWeights[i] is the weight of
    // holding minimumCount + i consumables.
    [Serializable]
    public sealed class ChestKindRule
    {
        [SerializeField] private ChestKind kind;
        [SerializeField, Min(0)] private int weight = 1;
        [SerializeField, Min(1)] private int minimumCount = 1;
        [SerializeField] private int[] countWeights = { 1 };
        [SerializeField, Range(0f, 1f)] private float spellChance;

        public ChestKindRule(ChestKind configuredKind, int configuredWeight, int configuredMinimumCount,
            int[] configuredCountWeights, float configuredSpellChance)
        {
            kind = configuredKind;
            weight = configuredWeight;
            minimumCount = configuredMinimumCount;
            countWeights = configuredCountWeights ?? Array.Empty<int>();
            spellChance = configuredSpellChance;
        }

        public ChestKind Kind => kind;
        public int Weight => weight;
        public int MinimumCount => minimumCount;
        public IReadOnlyList<int> CountWeights => countWeights;
        public int MaximumCount => minimumCount + countWeights.Length - 1;
        public float SpellChance => spellChance;

        public float ExpectedCount
        {
            get
            {
                int total = 0;
                float sum = 0f;
                for (int index = 0; index < countWeights.Length; index++)
                {
                    total += countWeights[index];
                    sum += countWeights[index] * (minimumCount + index);
                }

                return total > 0 ? sum / total : 0f;
            }
        }
    }

    // What one chest holds: consumable drop candidates (one pickup each) and an optional single-use spell.
    public sealed class ChestContents
    {
        public ChestContents(ChestKind kind, IReadOnlyList<ResourceDropEntry> consumables, ItemDefinition spell)
        {
            Kind = kind;
            Consumables = consumables ?? Array.Empty<ResourceDropEntry>();
            Spell = spell;
        }

        public ChestKind Kind { get; }
        public IReadOnlyList<ResourceDropEntry> Consumables { get; }
        public ItemDefinition Spell { get; }
    }

    // Chest-1: the seeded chest rolls. A combat room clear first rolls whether a chest appears (33%) and its kind
    // (75/20/5 of those); the contents come from a separate roll of the same seed, so a revisit or floor rebuild
    // replays the same chest and contents. Golden special rewards and diamond spells are Chest-2.
    [CreateAssetMenu(menuName = "Trickal Fan Game/Chest Content Table", fileName = "ChestContentTable")]
    public sealed class ChestContentTable : ScriptableObject
    {
        public const uint ContentSeedSalt = 0x3C6EF372u;

        [SerializeField, Range(0f, 1f)] private float chestChance;
        [SerializeField] private ChestKindRule[] kinds = Array.Empty<ChestKindRule>();
        [SerializeField] private ResourceDropEntry[] consumables = Array.Empty<ResourceDropEntry>();
        [SerializeField] private ItemDefinition[] spells = Array.Empty<ItemDefinition>();
        [SerializeField] private TreasureChest chestPrefab;
        [SerializeField] private SingleUseItemPickup spellPickupPrefab;

        public float ChestChance => chestChance;
        public IReadOnlyList<ChestKindRule> Kinds => kinds;
        public IReadOnlyList<ResourceDropEntry> Consumables => consumables;
        public IReadOnlyList<ItemDefinition> Spells => spells;
        public TreasureChest ChestPrefab => chestPrefab;
        public SingleUseItemPickup SpellPickupPrefab => spellPickupPrefab;

        public void Configure(float configuredChestChance, ChestKindRule[] configuredKinds,
            ResourceDropEntry[] configuredConsumables, ItemDefinition[] configuredSpells,
            TreasureChest configuredChestPrefab, SingleUseItemPickup configuredSpellPickupPrefab)
        {
            chestChance = Mathf.Clamp01(configuredChestChance);
            kinds = configuredKinds ?? Array.Empty<ChestKindRule>();
            consumables = configuredConsumables ?? Array.Empty<ResourceDropEntry>();
            spells = configuredSpells ?? Array.Empty<ItemDefinition>();
            chestPrefab = configuredChestPrefab;
            spellPickupPrefab = configuredSpellPickupPrefab;
        }

        public ChestKindRule FindRule(ChestKind kind)
        {
            foreach (ChestKindRule rule in kinds)
                if (rule != null && rule.Kind == kind) return rule;
            return null;
        }

        // The chance that a clear yields this kind of chest.
        public float KindChancePerRoll(ChestKind kind)
        {
            int total = TotalKindWeight();
            ChestKindRule rule = FindRule(kind);
            return total > 0 && rule != null ? chestChance * rule.Weight / total : 0f;
        }

        public bool TryValidate(out string error)
        {
            if (chestChance < 0f || chestChance > 1f)
            {
                error = $"{name} chest chance must be within 0..1.";
                return false;
            }

            if (kinds == null || kinds.Length != Enum.GetValues(typeof(ChestKind)).Length)
            {
                error = $"{name} needs exactly one rule per chest kind.";
                return false;
            }

            HashSet<ChestKind> seenKinds = new();
            foreach (ChestKindRule rule in kinds)
            {
                if (rule == null || !Enum.IsDefined(typeof(ChestKind), rule.Kind) || !seenKinds.Add(rule.Kind))
                {
                    error = $"{name} needs exactly one rule per chest kind.";
                    return false;
                }

                if (rule.Weight < 0 || rule.MinimumCount < 1 || rule.CountWeights.Count == 0 ||
                    rule.SpellChance < 0f || rule.SpellChance > 1f)
                {
                    error = $"{name} {rule.Kind} rule has an invalid weight, count or spell chance.";
                    return false;
                }

                int countTotal = 0;
                foreach (int countWeight in rule.CountWeights)
                {
                    if (countWeight < 0)
                    {
                        error = $"{name} {rule.Kind} count weights must not be negative.";
                        return false;
                    }

                    countTotal += countWeight;
                }

                if (countTotal <= 0)
                {
                    error = $"{name} {rule.Kind} needs a positive count weight.";
                    return false;
                }

                if (rule.SpellChance > 0f && spells.Length == 0)
                {
                    error = $"{name} {rule.Kind} has a spell chance but no spells.";
                    return false;
                }
            }

            if (TotalKindWeight() <= 0)
            {
                error = $"{name} needs a positive chest kind weight.";
                return false;
            }

            if (consumables == null || consumables.Length == 0)
            {
                error = $"{name} needs at least one consumable.";
                return false;
            }

            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (ResourceDropEntry entry in consumables)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.DropId) || !ids.Add(entry.DropId) ||
                    entry.Weight <= 0 || !IsConsumablePrefab(entry.Prefab))
                {
                    error = $"{name} consumables need unique IDs, positive weights and a heart, SP or Run " +
                            "resource pickup Prefab.";
                    return false;
                }
            }

            HashSet<ItemDefinition> seenSpells = new();
            foreach (ItemDefinition spell in spells)
            {
                // Contract-0 §3: chests outside Chest-2 hold single-use spells only, never jjangsem spells.
                if (spell == null || !spell.IsValid || !spell.IsActive || spell.Kind != ItemKind.SingleUseSpell ||
                    !seenSpells.Add(spell))
                {
                    error = $"{name} spells must be distinct, active and valid single-use spells.";
                    return false;
                }
            }

            if (chestPrefab == null)
            {
                error = $"{name} needs the chest Prefab.";
                return false;
            }

            if (!chestPrefab.TryValidate(out error))
            {
                error = $"{name} needs a valid chest Prefab. {error}";
                return false;
            }

            if (spellPickupPrefab == null)
            {
                error = $"{name} needs the single-use item pickup Prefab.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool IsConsumablePrefab(GameObject prefab) =>
            prefab != null && (prefab.GetComponent<RunResourcePickup>() != null ||
                               prefab.GetComponent<HealthPickup>() != null || prefab.GetComponent<SPPickup>() != null);

        // Whether a combat room clear yields a chest, and of which kind.
        public bool TryRollChest(int seed, out ChestKind kind)
        {
            kind = ChestKind.Normal;
            if (!TryValidate(out _)) return false;
            uint state = unchecked((uint)seed);
            if (NextUnit(ref state) >= chestChance) return false;
            int pick = PickIndex(ref state, TotalKindWeight());
            foreach (ChestKindRule rule in kinds)
            {
                if (pick < rule.Weight)
                {
                    kind = rule.Kind;
                    return true;
                }

                pick -= rule.Weight;
            }

            return false;
        }

        public ChestContents RollContents(int seed, ChestKind kind)
        {
            if (!TryValidate(out string error)) throw new InvalidOperationException(error);
            ChestKindRule rule = FindRule(kind);
            uint state = unchecked((uint)seed) ^ ContentSeedSalt;

            int countTotal = 0;
            foreach (int countWeight in rule.CountWeights) countTotal += countWeight;
            int countPick = PickIndex(ref state, countTotal);
            int count = rule.MinimumCount;
            for (int index = 0; index < rule.CountWeights.Count; index++)
            {
                if (countPick < rule.CountWeights[index])
                {
                    count = rule.MinimumCount + index;
                    break;
                }

                countPick -= rule.CountWeights[index];
            }

            int consumableTotal = 0;
            foreach (ResourceDropEntry entry in consumables) consumableTotal += entry.Weight;
            ResourceDropEntry[] picked = new ResourceDropEntry[count];
            for (int slot = 0; slot < count; slot++)
            {
                int pick = PickIndex(ref state, consumableTotal);
                foreach (ResourceDropEntry entry in consumables)
                {
                    if (pick < entry.Weight)
                    {
                        picked[slot] = entry;
                        break;
                    }

                    pick -= entry.Weight;
                }
            }

            ItemDefinition spell = rule.SpellChance > 0f && NextUnit(ref state) < rule.SpellChance
                ? spells[PickIndex(ref state, spells.Length)]
                : null;
            return new ChestContents(kind, picked, spell);
        }

        private int TotalKindWeight()
        {
            int total = 0;
            if (kinds == null) return 0;
            foreach (ChestKindRule rule in kinds)
                if (rule != null) total += Mathf.Max(0, rule.Weight);
            return total;
        }

        private static int PickIndex(ref uint state, int total) =>
            Mathf.Min(total - 1, (int)(NextUnit(ref state) * total));

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
