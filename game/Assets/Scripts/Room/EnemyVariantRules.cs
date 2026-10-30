using System;
using System.Collections.Generic;
using TrickalFanGame.Enemy;

namespace TrickalFanGame.Room
{
    // Variant-0 (user decision, 2026-10-24, docs/28 §8·§9): an elite is rolled once per combat room (at most one per
    // room) from the room's content seed after its Encounter is chosen, so the same seed keeps the same Encounter and
    // the same elite. The chance rises with depth. 쥬비, split bodies, boss summons and bosses never become variants;
    // only 쥬비 appears in generated spawns, the others are made at runtime and never pass through here.
    public static class EnemyVariantRules
    {
        public const uint EliteRollSalt = 0x6E1A7E01u;
        public const uint ElitePickSalt = 0x6E1A7E02u;

        // Depth 1~3 is the current Run. 4~9 are kept for the expansion (docs/28 §9).
        public static int ElitePercent(int floorNumber) => floorNumber switch
        {
            <= 2 => 0,
            3 => 20,
            <= 6 => 30,
            _ => 40,
        };

        public static bool IsEligible(EncounterEnemyRole role) =>
            role != EncounterEnemyRole.Jyubi && role != EncounterEnemyRole.Boss;

        public static bool RollsElite(int floorNumber, int contentSeed, int contentVersion)
        {
            int percent = ElitePercent(floorNumber);
            if (percent <= 0) return false;
            int seed = FloorGenerator.DeriveSeed(contentSeed, contentVersion, EliteRollSalt);
            return unchecked((uint)seed) % 100u < (uint)percent;
        }

        // Returns new wave arrays with one eligible spawn marked elite when the room rolls one; otherwise the same
        // arrays. A room with no eligible enemy (a 쥬비 swarm) gets none.
        public static ResolvedEncounterSpawn[][] AssignElite(ResolvedEncounterSpawn[][] waves, int floorNumber,
            int contentSeed, int contentVersion)
        {
            if (waves == null || !RollsElite(floorNumber, contentSeed, contentVersion)) return waves;

            List<(int Wave, int Index)> eligible = new();
            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                ResolvedEncounterSpawn[] wave = waves[waveIndex] ?? Array.Empty<ResolvedEncounterSpawn>();
                for (int index = 0; index < wave.Length; index++)
                    if (IsEligible(wave[index].Role)) eligible.Add((waveIndex, index));
            }

            if (eligible.Count == 0) return waves;
            int pickSeed = FloorGenerator.DeriveSeed(contentSeed, contentVersion, ElitePickSalt);
            (int pickedWave, int pickedIndex) = eligible[(int)(unchecked((uint)pickSeed) % (uint)eligible.Count)];

            ResolvedEncounterSpawn[][] result = new ResolvedEncounterSpawn[waves.Length][];
            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
                result[waveIndex] = waves[waveIndex] == null
                    ? null
                    : (ResolvedEncounterSpawn[])waves[waveIndex].Clone();
            result[pickedWave][pickedIndex] = result[pickedWave][pickedIndex].WithVariant(EnemyVariantKind.Elite);
            return result;
        }

        public static int CountElites(IReadOnlyList<ResolvedEncounterSpawn[]> waves)
        {
            int count = 0;
            if (waves == null) return 0;
            foreach (ResolvedEncounterSpawn[] wave in waves)
                if (wave != null)
                    foreach (ResolvedEncounterSpawn spawn in wave)
                        if (spawn.Variant == EnemyVariantKind.Elite) count++;
            return count;
        }
    }
}
