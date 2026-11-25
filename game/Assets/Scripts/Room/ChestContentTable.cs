using System;
using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Chest-1: how many consumables a chest kind holds and how likely it adds a spell. countWeights[i] is the weight of
    // holding minimumCount + i consumables. Chest-2: specialRewardChance is the golden chest's chance of one exclusive
    // artifact, and jjangsemShare is how often a diamond chest's spell is a jjangsem spell instead.
    [Serializable]
    public sealed class ChestKindRule
    {
        [SerializeField] private ChestKind kind;
        [SerializeField, Min(0)] private int weight = 1;
        [SerializeField, Min(1)] private int minimumCount = 1;
        [SerializeField] private int[] countWeights = { 1 };
        [SerializeField, Range(0f, 1f)] private float spellChance;
        [SerializeField, Range(0f, 1f)] private float specialRewardChance;
        [SerializeField, Range(0f, 1f)] private float jjangsemShare;

        public ChestKindRule(ChestKind configuredKind, int configuredWeight, int configuredMinimumCount,
            int[] configuredCountWeights, float configuredSpellChance, float configuredSpecialRewardChance = 0f,
            float configuredJjangsemShare = 0f)
        {
            kind = configuredKind;
            weight = configuredWeight;
            minimumCount = configuredMinimumCount;
            countWeights = configuredCountWeights ?? Array.Empty<int>();
            spellChance = configuredSpellChance;
            specialRewardChance = configuredSpecialRewardChance;
            jjangsemShare = configuredJjangsemShare;
        }

        public ChestKind Kind => kind;
        public int Weight => weight;
        public int MinimumCount => minimumCount;
        public IReadOnlyList<int> CountWeights => countWeights;
        public int MaximumCount => minimumCount + countWeights.Length - 1;
        public float SpellChance => spellChance;
        public float SpecialRewardChance => specialRewardChance;
        public float JjangsemShare => jjangsemShare;

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

    // Balance-1 (2026-11-24): how generous chests are for a Run of a given length. The 3-floor beta needs a build to
    // come together by about the middle of floor 2, so its rates are high; longer Runs get their own row instead of
    // retuning the short one. A normal chest holds an artifact or a spell at these chances, and a run of combat clears
    // without a chest item guarantees one on the next clear (itemPityClears, ResourcePity).
    [Serializable]
    public sealed class ChestRewardRates
    {
        [SerializeField, Min(1)] private int floorCount = 3;
        [SerializeField, Range(0f, 1f)] private float normalArtifactChance;
        [SerializeField, Range(0f, 1f)] private float normalSpellChance;
        [SerializeField, Min(1)] private int itemPityClears = 3;

        public ChestRewardRates(int configuredFloorCount, float configuredNormalArtifactChance,
            float configuredNormalSpellChance, int configuredItemPityClears)
        {
            floorCount = configuredFloorCount;
            normalArtifactChance = configuredNormalArtifactChance;
            normalSpellChance = configuredNormalSpellChance;
            itemPityClears = configuredItemPityClears;
        }

        // The longest Run this row covers; a Run uses the first row at least as long as it.
        public int FloorCount => floorCount;
        public float NormalArtifactChance => normalArtifactChance;
        public float NormalSpellChance => normalSpellChance;
        public float NormalItemChance => normalArtifactChance + normalSpellChance;
        public int ItemPityClears => itemPityClears;
    }

    // What one chest holds: consumable drop candidates (one pickup each), an optional single-use item (a spell or a
    // jjangsem spell) and whether the golden special reward hit. The special reward's artifact is chosen when the chest
    // opens (ChestContentTable.ResolveSpecialReward), from the seeded SpecialRewardPick.
    public sealed class ChestContents
    {
        public ChestContents(ChestKind kind, IReadOnlyList<ResourceDropEntry> consumables, ItemDefinition spell,
            bool hasSpecialReward = false, float specialRewardPick = 0f, bool hasArtifact = false,
            ItemDefinition fallbackSpell = null)
        {
            Kind = kind;
            Consumables = consumables ?? Array.Empty<ResourceDropEntry>();
            Spell = spell;
            HasSpecialReward = hasSpecialReward;
            SpecialRewardPick = specialRewardPick;
            HasArtifact = hasArtifact;
            FallbackSpell = fallbackSpell;
        }

        // Balance-1: a normal chest's artifact, chosen from the treasure pool when the chest opens. FallbackSpell
        // takes its place when the player can acquire no artifact.
        public bool HasArtifact { get; }
        public ItemDefinition FallbackSpell { get; }
        public bool HasItem => Spell != null || HasSpecialReward || HasArtifact;

        public ChestKind Kind { get; }
        public IReadOnlyList<ResourceDropEntry> Consumables { get; }
        public ItemDefinition Spell { get; }
        public bool HasSpecialReward { get; }
        public float SpecialRewardPick { get; }
    }

    // Chest-1: the seeded chest rolls. A combat room clear first rolls whether a chest appears (33%) and its kind
    // (75/20/5 of those); the contents come from a separate roll of the same seed, so a revisit or floor rebuild
    // replays the same chest and contents. Chest-2 (D2, 2026-10-05): a golden chest has a 25% special reward, one
    // golden exclusive artifact the player can still acquire when it opens (none if every candidate is at its stack
    // limit), and a diamond chest always holds one spell, 25% of the time a jjangsem spell once one is implemented.
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
        // Flight-0: artifacts only a golden chest's special reward can give (Contract-0 §2.1, pool membership instead
        // of a kind). They never join the selection reward pool or shop stock. Chest-2 rolls the special reward.
        [SerializeField] private ItemDefinition[] goldenExclusiveArtifacts = Array.Empty<ItemDefinition>();
        [SerializeField] private ItemPickup artifactPickupPrefab;
        // Chest-2: implemented jjangsem spells a diamond chest may hold. Empty until Jjangsem-0; spells fill in.
        [SerializeField] private ItemDefinition[] jjangsemSpells = Array.Empty<ItemDefinition>();
        // Balance-1: per Run length. Empty keeps the pre-Balance-1 rule chances and no item pity.
        [SerializeField] private ChestRewardRates[] rewardRates = Array.Empty<ChestRewardRates>();

        public float ChestChance => chestChance;
        public IReadOnlyList<ChestRewardRates> RewardRates => rewardRates ?? Array.Empty<ChestRewardRates>();
        public IReadOnlyList<ChestKindRule> Kinds => kinds;
        public IReadOnlyList<ResourceDropEntry> Consumables => consumables;
        public IReadOnlyList<ItemDefinition> Spells => spells;
        public TreasureChest ChestPrefab => chestPrefab;
        public SingleUseItemPickup SpellPickupPrefab => spellPickupPrefab;
        public IReadOnlyList<ItemDefinition> GoldenExclusiveArtifacts =>
            goldenExclusiveArtifacts ?? Array.Empty<ItemDefinition>();
        public ItemPickup ArtifactPickupPrefab => artifactPickupPrefab;
        public IReadOnlyList<ItemDefinition> JjangsemSpells => jjangsemSpells ?? Array.Empty<ItemDefinition>();

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

        public void ConfigureGoldenExclusivePool(ItemDefinition[] configuredArtifacts,
            ItemPickup configuredArtifactPickupPrefab)
        {
            goldenExclusiveArtifacts = configuredArtifacts ?? Array.Empty<ItemDefinition>();
            artifactPickupPrefab = configuredArtifactPickupPrefab;
        }

        public void ConfigureJjangsemSpells(ItemDefinition[] configuredJjangsemSpells)
        {
            jjangsemSpells = configuredJjangsemSpells ?? Array.Empty<ItemDefinition>();
        }

        public void ConfigureRewardRates(ChestRewardRates[] configuredRates)
        {
            rewardRates = configuredRates ?? Array.Empty<ChestRewardRates>();
        }

        // The row for a Run of this many floors: the shortest row that covers it, or else the longest row. Null
        // without rows. A Run length of 0 (unknown) gets the shortest row.
        public ChestRewardRates RatesFor(int runFloorCount)
        {
            ChestRewardRates chosen = null;
            ChestRewardRates longest = null;
            foreach (ChestRewardRates rates in RewardRates)
            {
                if (rates == null) continue;
                if (longest == null || rates.FloorCount > longest.FloorCount) longest = rates;
                if (rates.FloorCount >= runFloorCount && (chosen == null || rates.FloorCount < chosen.FloorCount))
                    chosen = rates;
            }

            return chosen ?? longest;
        }

        public bool IsGoldenExclusive(ItemDefinition definition) =>
            definition != null && Array.IndexOf(goldenExclusiveArtifacts ?? Array.Empty<ItemDefinition>(), definition) >= 0;

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
                    rule.SpellChance < 0f || rule.SpellChance > 1f || rule.SpecialRewardChance < 0f ||
                    rule.SpecialRewardChance > 1f || rule.JjangsemShare < 0f || rule.JjangsemShare > 1f)
                {
                    error = $"{name} {rule.Kind} rule has an invalid weight, count or chance.";
                    return false;
                }

                // Contract-0 §3: only golden chests give the special reward and only diamond chests jjangsem spells.
                if ((rule.SpecialRewardChance > 0f && rule.Kind != ChestKind.Golden) ||
                    (rule.JjangsemShare > 0f && rule.Kind != ChestKind.Diamond))
                {
                    error = $"{name} {rule.Kind} must not give a golden special reward or a jjangsem spell.";
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
                // Contract-0 §3: the spell list holds single-use spells only; jjangsem spells have their own list.
                if (spell == null || !spell.IsValid || !spell.IsActive || spell.Kind != ItemKind.SingleUseSpell ||
                    !seenSpells.Add(spell))
                {
                    error = $"{name} spells must be distinct, active and valid single-use spells.";
                    return false;
                }
            }

            HashSet<ItemDefinition> seenJjangsemSpells = new();
            foreach (ItemDefinition jjangsem in JjangsemSpells)
            {
                if (jjangsem == null || !jjangsem.IsValid || !jjangsem.IsActive ||
                    jjangsem.Kind != ItemKind.JjangsemSpell || !seenJjangsemSpells.Add(jjangsem))
                {
                    error = $"{name} jjangsem spells must be distinct, active and valid jjangsem spells.";
                    return false;
                }
            }

            HashSet<int> rateLengths = new();
            foreach (ChestRewardRates rates in RewardRates)
            {
                if (rates == null || rates.FloorCount < 1 || !rateLengths.Add(rates.FloorCount) ||
                    rates.NormalArtifactChance < 0f || rates.NormalSpellChance < 0f || rates.NormalItemChance > 1f ||
                    rates.ItemPityClears < 1)
                {
                    error = $"{name} reward rates need distinct Run lengths, chances within 0..1 and a positive " +
                            "pity count.";
                    return false;
                }

                if (rates.NormalSpellChance > 0f && spells.Length == 0)
                {
                    error = $"{name} reward rates give normal chest spells but there are no spells.";
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

            HashSet<ItemDefinition> seenArtifacts = new();
            foreach (ItemDefinition artifact in GoldenExclusiveArtifacts)
            {
                if (artifact == null || !artifact.IsValid || !artifact.IsActive || artifact.Kind != ItemKind.Artifact ||
                    !seenArtifacts.Add(artifact))
                {
                    error = $"{name} golden exclusive artifacts must be distinct, active and valid artifacts.";
                    return false;
                }
            }

            if (seenArtifacts.Count > 0 && artifactPickupPrefab == null)
            {
                error = $"{name} needs the artifact pickup Prefab for its golden exclusive artifacts.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool IsConsumablePrefab(GameObject prefab) =>
            prefab != null && (prefab.GetComponent<RunResourcePickup>() != null ||
                               prefab.GetComponent<HealthPickup>() != null || prefab.GetComponent<SPPickup>() != null);

        // Drop-1: on the key-free floor (FloorGenerator.KeyFreeFloor) a golden chest from a clear roll or a Layout is a
        // normal chest instead, with normal contents from the same seed. Development and copied chests keep their kind.
        public static ChestKind ResolveKindForFloor(ChestKind kind, bool keyFreeFloor) =>
            keyFreeFloor && kind == ChestKind.Golden ? ChestKind.Normal : kind;

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

        public ChestContents RollContents(int seed, ChestKind kind) => RollContents(seed, kind, null, false);

        // Balance-1: with reward rates a normal chest's item comes from them instead of its rule's spell chance, and
        // forceItem (the item pity) makes sure it holds one. The draws keep a fixed order, so the same seed, rates
        // and forceItem always give the same chest.
        public ChestContents RollContents(int seed, ChestKind kind, ChestRewardRates rates, bool forceItem)
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

            // Every draw below comes after the Chest-1 draws, so a chest keeps the consumables and spell hit it had.
            ItemDefinition spell = null;
            bool hasArtifact = false;
            ItemDefinition fallbackSpell = null;
            if (kind == ChestKind.Normal && rates != null)
            {
                float roll = NextUnit(ref state);
                if (forceItem || roll < rates.NormalItemChance)
                {
                    float artifactShare = rates.NormalItemChance > 0f
                        ? rates.NormalArtifactChance / rates.NormalItemChance
                        : 0.5f;
                    hasArtifact = spells.Length == 0 || NextUnit(ref state) < artifactShare;
                    ItemDefinition pickedSpell = spells.Length > 0
                        ? spells[PickIndex(ref state, spells.Length)]
                        : null;
                    if (hasArtifact) fallbackSpell = pickedSpell;
                    else spell = pickedSpell;
                }
            }
            else if (rule.SpellChance > 0f && (forceItem || NextUnit(ref state) < rule.SpellChance))
            {
                IReadOnlyList<ItemDefinition> jjangsemPool = JjangsemSpells;
                bool isJjangsem = rule.JjangsemShare > 0f && jjangsemPool.Count > 0 &&
                                  NextUnit(ref state) < rule.JjangsemShare;
                spell = isJjangsem
                    ? jjangsemPool[PickIndex(ref state, jjangsemPool.Count)]
                    : spells[PickIndex(ref state, spells.Length)];
            }

            bool hasSpecialReward = false;
            float specialRewardPick = 0f;
            if (rule.SpecialRewardChance > 0f && GoldenExclusiveArtifacts.Count > 0)
            {
                hasSpecialReward = NextUnit(ref state) < rule.SpecialRewardChance;
                specialRewardPick = NextUnit(ref state);
            }

            return new ChestContents(kind, picked, spell, hasSpecialReward, specialRewardPick, hasArtifact,
                fallbackSpell);
        }

        // Balance-1: the artifact a chest gives when it opens. A golden chest's special reward takes a golden
        // exclusive artifact first and otherwise one from the treasure pool; a normal chest's artifact always comes
        // from the treasure pool. Null when the chest holds no artifact or the player can acquire none.
        public ItemDefinition ResolveArtifact(ChestContents contents, PlayerInventory inventory,
            IReadOnlyList<ItemDefinition> artifactPool, int runSeed, string rewardId)
        {
            if (contents == null || (!contents.HasSpecialReward && !contents.HasArtifact)) return null;
            if (contents.HasSpecialReward)
            {
                ItemDefinition exclusive = ResolveSpecialReward(contents, inventory);
                if (exclusive != null) return exclusive;
            }

            if (artifactPool == null) return null;
            List<ItemDefinition> artifacts = new();
            foreach (ItemDefinition candidate in artifactPool)
                if (candidate != null && candidate.Kind == ItemKind.Artifact && !IsGoldenExclusive(candidate))
                    artifacts.Add(candidate);
            return ArtifactRewardSelector.TryChoose(artifacts, inventory, runSeed, rewardId, ItemPoolSource.Treasure,
                out ItemDefinition chosen)
                ? chosen
                : null;
        }

        // The golden special reward for a chest that is opening: one exclusive artifact the player can still acquire,
        // chosen by the seeded pick. Null when the roll missed or every candidate is at its stack limit, so no pickup
        // the player cannot take is left on the floor (D2, 2026-10-05).
        public ItemDefinition ResolveSpecialReward(ChestContents contents, PlayerInventory inventory)
        {
            if (contents == null || !contents.HasSpecialReward) return null;
            List<ItemDefinition> candidates = new();
            foreach (ItemDefinition artifact in GoldenExclusiveArtifacts)
                if (ArtifactRewardSelector.IsEligible(artifact, inventory)) candidates.Add(artifact);
            return candidates.Count > 0
                ? candidates[Mathf.Min(candidates.Count - 1, (int)(contents.SpecialRewardPick * candidates.Count))]
                : null;
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
