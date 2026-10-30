using System;

namespace TrickalFanGame.Room
{
    // Curve-1 (user decision, 2026-10-24): each normal enemy first appears on a set floor and keeps appearing after
    // it. Floor 1 asks for head-on fights, floor 2 for reading telegraphs, floor 3 for judgement and positioning.
    // An enemy new on the floor weighs x1.5 and one introduced two or more floors below weighs x0.5. Weights are
    // kept in halves (3/2/1) so the authored integer weights stay exact. Candidates a floor removes simply drop out
    // and the remaining weights share the roll.
    public static class EncounterEnemyFloorPool
    {
        public const int NewWeightHalves = 3;
        public const int StandardWeightHalves = 2;
        public const int OldWeightHalves = 1;
        // Floors at or beyond this many floors above an enemy's introduction use the old weight.
        public const int OldFloorGap = 2;

        public static int IntroductionFloor(EncounterEnemyRole role) => role switch
        {
            EncounterEnemyRole.Chaser => 1,          // 불효자손
            EncounterEnemyRole.FastChaser => 1,      // 산사모-활발
            EncounterEnemyRole.Sniper => 1,          // 고혈당 요정(저격)
            EncounterEnemyRole.Jyubi => 1,           // 쥬비
            EncounterEnemyRole.Ranged => 2,          // 산사모-순수
            EncounterEnemyRole.Charging => 2,        // 저혈당 요정-순수(돌진)
            EncounterEnemyRole.FourWayShooter => 2,  // 저혈당 요정-활발
            EncounterEnemyRole.SelfDestruct => 2,    // 자폭형 (Enemy-10)
            EncounterEnemyRole.SplittingChaser => 3, // 누루링-요정전사
            EncounterEnemyRole.JyubiKing => 3,       // 대왕쥬비 (Enemy-11)
            EncounterEnemyRole.Lobber => 3,          // 곡사형 (Enemy-12)
            EncounterEnemyRole.Ambusher => 3,        // 잠복형 (Enemy-13)
            EncounterEnemyRole.Boss => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role,
                "Every Encounter enemy role needs an introduction floor."),
        };

        public static bool IsAvailable(EncounterEnemyRole role, int floorNumber) =>
            floorNumber >= IntroductionFloor(role);

        // 0 when the role is not available on the floor.
        public static int WeightHalves(EncounterEnemyRole role, int floorNumber)
        {
            int introduction = IntroductionFloor(role);
            if (floorNumber < introduction) return 0;
            if (floorNumber == introduction) return NewWeightHalves;
            return floorNumber - introduction >= OldFloorGap ? OldWeightHalves : StandardWeightHalves;
        }

        public static int EffectiveWeight(EncounterEnemyCandidate candidate, int floorNumber) =>
            candidate.Weight * WeightHalves(candidate.EnemyRole, floorNumber);
    }
}
