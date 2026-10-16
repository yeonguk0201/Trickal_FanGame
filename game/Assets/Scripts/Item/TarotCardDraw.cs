using System;
using System.Collections.Generic;
using TrickalFanGame.Room;

namespace TrickalFanGame.Item
{
    // The stats 엘리스의 타로카드 can move. HP and SP are never drawn.
    public enum TarotStat
    {
        AttackDamage = 0,
        AttackSpeed = 1,
        MoveSpeed = 2,
        Range = 3,
        ShotSpeed = 4,
        CriticalChance = 5,
        CriticalDamage = 6,
    }

    public readonly struct TarotStatChange
    {
        public TarotStatChange(TarotStat stat, float amount)
        {
            Stat = stat;
            Amount = amount;
        }

        public TarotStat Stat { get; }
        // Signed: a raise is positive, a drop negative.
        public float Amount { get; }
    }

    // Spell-6 (plan 26 §4.3, 2026-10-09): one or two stats go up (60/40), and with 30% one or two other stats go down
    // (70/30). Each stat moves by its own fixed step either way, kept for the rest of the Run. The draw comes from the
    // Run seed and how many cards were used before, so a seed replays the same cards in order.
    public static class TarotCardDraw
    {
        public const uint Salt = 0x5441524Fu;
        public const int SingleRaisePercent = 60;
        public const int DropPercent = 30;
        public const int SingleDropPercent = 70;

        public static readonly TarotStat[] Stats =
        {
            TarotStat.AttackDamage, TarotStat.AttackSpeed, TarotStat.MoveSpeed, TarotStat.Range,
            TarotStat.ShotSpeed, TarotStat.CriticalChance, TarotStat.CriticalDamage,
        };

        // 공격력 ±8%, 공격 속도 ±8%, 이동 속도 ±5%, 사거리 ±10%, 탄속 ±10%, 치명타 확률 ±5%p, 치명타 피해 ±20%p.
        public static float StepOf(TarotStat stat) => stat switch
        {
            TarotStat.AttackDamage => 0.08f,
            TarotStat.AttackSpeed => 0.08f,
            TarotStat.MoveSpeed => 0.05f,
            TarotStat.Range => 0.1f,
            TarotStat.ShotSpeed => 0.1f,
            TarotStat.CriticalChance => 0.05f,
            TarotStat.CriticalDamage => 0.2f,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
        };

        public static string NameOf(TarotStat stat) => stat switch
        {
            TarotStat.AttackDamage => "공격력",
            TarotStat.AttackSpeed => "공격 속도",
            TarotStat.MoveSpeed => "이동 속도",
            TarotStat.Range => "사거리",
            TarotStat.ShotSpeed => "탄속",
            TarotStat.CriticalChance => "치명타 확률",
            TarotStat.CriticalDamage => "치명타 피해",
            _ => stat.ToString(),
        };

        // Raises first, then drops; a dropped stat is never one that was raised.
        public static List<TarotStatChange> Draw(int runSeed, int drawIndex)
        {
            if (drawIndex < 0) throw new ArgumentOutOfRangeException(nameof(drawIndex));
            uint state = unchecked((uint)FloorGenerator.DeriveSeed(runSeed, drawIndex, Salt));
            if (state == 0) state = 0x6D2B79F5u;
            List<TarotStat> remaining = new(Stats);
            List<TarotStatChange> changes = new();
            int raises = Next(ref state, 100) < SingleRaisePercent ? 1 : 2;
            for (int index = 0; index < raises; index++) changes.Add(Take(ref state, remaining, 1f));
            if (Next(ref state, 100) < DropPercent)
            {
                int drops = Next(ref state, 100) < SingleDropPercent ? 1 : 2;
                for (int index = 0; index < drops; index++) changes.Add(Take(ref state, remaining, -1f));
            }

            return changes;
        }

        private static TarotStatChange Take(ref uint state, List<TarotStat> remaining, float sign)
        {
            int index = Next(ref state, remaining.Count);
            TarotStat stat = remaining[index];
            remaining.RemoveAt(index);
            return new TarotStatChange(stat, sign * StepOf(stat));
        }

        private static int Next(ref uint state, int count)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state % (uint)count);
        }
    }
}
