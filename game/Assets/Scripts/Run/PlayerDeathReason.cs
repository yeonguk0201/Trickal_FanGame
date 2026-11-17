using UnityEngine;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;

namespace TrickalFanGame.Run
{
    public sealed class PlayerDeathReason : MonoBehaviour
    {
        public string CurrentReason { get; private set; } = "UNKNOWN";

        public static string Describe(DamageContext damage)
        {
            if (!string.IsNullOrEmpty(damage.SourceDisplayName)) return damage.SourceDisplayName;

            GameObject source = damage.Source;
            if (source != null)
            {
                if (source.GetComponent<PoisonGasCloud>() != null) return "독가스";
                if (source.GetComponent<RoomTrap>() is RoomTrap trap)
                    return trap.Kind switch
                    {
                        RoomTrapKind.FloorSpikes => "바닥 가시",
                        RoomTrapKind.PeriodicSpikes => "주기 가시",
                        RoomTrapKind.Fire => "불",
                        RoomTrapKind.DamagePuddle => "피해 장판",
                        RoomTrapKind.Spiderweb => "거미줄 함정",
                        RoomTrapKind.SpikedRock => "가시 돌",
                        RoomTrapKind.PoisonMushroom => "독버섯",
                        _ => "환경 피해"
                    };
                if (source.GetComponent<EnemyChampionPuddle>() != null) return "챔피언의 장판";
                if (source.GetComponent<SelfDestructEnemy>() != null) return "적의 자폭";
            }

            return DescribeAttack(damage.SourceType);
        }

        private static string DescribeAttack(DamageSourceType type) => type switch
        {
            DamageSourceType.EnemyContact or DamageSourceType.EnemyMelee or DamageSourceType.EnemyProjectile => "알 수 없는 적",
            DamageSourceType.Environment => "환경 피해",
            DamageSourceType.PlayerBomb => "설치한 폭탄의 폭발",
            _ => "알 수 없는 피해"
        };

        public static string DescribeRecordedReason(string reason) => reason?.Trim().ToUpperInvariant() switch
        {
            "ENEMY" or "ENEMY_CONTACT" or "ENEMY_MELEE" or "ENEMY_PROJECTILE" => "알 수 없는 적",
            "HAZARD" => "환경 피해",
            "BOSS" => "알 수 없는 보스",
            _ => "알 수 없는 피해"
        };

        public void SetReason(string reason)
        {
            CurrentReason = string.IsNullOrWhiteSpace(reason) ? "UNKNOWN" : reason;
        }
    }
}
