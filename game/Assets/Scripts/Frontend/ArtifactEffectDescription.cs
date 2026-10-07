using System.Collections.Generic;
using TrickalFanGame.Item;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    public static class ArtifactEffectDescription
    {
        public static string Build(ItemDefinition definition)
        {
            if (definition == null) return string.Empty;

            IReadOnlyList<ItemEffectEntry> effects = definition.Effects;
            if (effects.Count == 0)
            {
                return BuildLegacy(definition.EffectType, definition.EffectValue);
            }

            List<string> parts = new();
            foreach (ItemEffectEntry effect in effects)
            {
                string part = BuildEffect(effect);
                if (!string.IsNullOrEmpty(part)) parts.Add(part);
            }

            return string.Join(" · ", parts);
        }

        private static string BuildLegacy(ItemEffectType type, float value)
        {
            return type switch
            {
                ItemEffectType.AttackDamage => $"공격력 +{Number(value)}",
                ItemEffectType.MaxHealth => $"최대 HP +{Hearts(value)}",
                ItemEffectType.MoveSpeed => $"이동속도 +{Number(value)}",
                ItemEffectType.MultiShot => $"투사체 +{Mathf.RoundToInt(value)}",
                ItemEffectType.Pierce => $"관통 +{Mathf.RoundToInt(value)}",
                ItemEffectType.HealOnKill => $"처치 시 HP {Hearts(value)} 회복",
                _ => type.ToString(),
            };
        }

        private static string BuildEffect(ItemEffectEntry effect)
        {
            if (effect == null) return string.Empty;

            return effect.EffectType switch
            {
                ItemEffectType.AttackDamage => $"공격력 +{Number(effect.Magnitude)}",
                ItemEffectType.MaxHealth => $"최대 HP +{Hearts(effect.Magnitude)}",
                ItemEffectType.MoveSpeed => $"이동속도 +{Number(effect.Magnitude)}",
                ItemEffectType.MultiShot => $"투사체 +{effect.IntegerAmount}",
                ItemEffectType.Pierce => $"관통 +{effect.IntegerAmount}",
                ItemEffectType.HealOnKill => $"처치 시 HP {Hearts(effect.Magnitude)} 회복",
                ItemEffectType.AttackDamagePercent => $"공격력 +{Percent(effect.Magnitude)}",
                ItemEffectType.SkillDamagePercent => $"스킬 피해 +{Percent(effect.Magnitude)}",
                ItemEffectType.CriticalChance => $"치명타 확률 +{PercentPoint(effect.Magnitude)}",
                ItemEffectType.AttackSpeedPercent => $"공격속도 +{Percent(effect.Magnitude)}",
                ItemEffectType.MaxHealthDamageAura => $"주변 적에게 초당 최대 HP의 {Percent(effect.Magnitude)} 피해",
                ItemEffectType.MaxHealthFlat => $"최대 HP +{Hearts(effect.Magnitude)}",
                ItemEffectType.ShieldOnAcquireMaxHealthPercent => $"획득 시 최대 HP {Percent(effect.Magnitude)} 방어막",
                ItemEffectType.MoveSpeedPercentBelowHealth =>
                    $"HP {Percent(effect.HealthThreshold)} 이하 이동속도 +{Percent(effect.Magnitude)}",
                ItemEffectType.HealOnKillMaxHealthPercent =>
                    $"처치 시 최대 HP의 {Percent(effect.Magnitude)} 회복",
                ItemEffectType.DistanceDamage =>
                    $"{Number(effect.MinimumDistance)}~{Number(effect.MaximumDistance)}m 거리 비례 피해 최대 +{Percent(effect.Magnitude)}",
                ItemEffectType.SplitAfterPierce => $"첫 관통 시 분열탄 {effect.IntegerAmount}개",
                ItemEffectType.MaxSP => $"최대 SP +{effect.IntegerAmount}",
                ItemEffectType.SkillProjectileBonusAtSP => $"저학년 투사체 +{effect.IntegerAmount}",
                ItemEffectType.MoveSpeedPercent => $"이동속도 +{Percent(effect.Magnitude)}",
                ItemEffectType.MoveSpeedPenaltyPercent => $"이동속도 -{Percent(effect.Magnitude)}",
                ItemEffectType.NextCombatRoomAttackDamagePercent =>
                    $"다음 전투방 기본 공격 피해 +{Percent(effect.Magnitude)}",
                ItemEffectType.BossRoomAttackSpeedPercent =>
                    $"보스방 공격속도 +{Percent(effect.Magnitude)}",
                ItemEffectType.BossRoomMoveSpeedPercent =>
                    $"보스방 이동속도 +{Percent(effect.Magnitude)}",
                ItemEffectType.HealOverTimeBelowHealthOnce =>
                    $"HP {Percent(effect.HealthThreshold)} 이하 시 {Number(effect.DurationSeconds)}초간 최대 HP의 {Percent(effect.Magnitude)} 회복 (층당 1회)",
                ItemEffectType.HealOnKillEveryN =>
                    $"적 {effect.IntegerAmount}마리 처치마다 HP {Hearts(effect.Magnitude)} 회복 (스택마다 필요 처치 -1)",
                ItemEffectType.AttackDamageAura =>
                    $"반경 {Number(effect.Radius)}m 적에게 {Number(effect.IntervalSeconds)}초마다 공격력의 {Percent(effect.Magnitude)} 피해",
                ItemEffectType.BasicAttackHitLightning =>
                    $"기본 공격 {effect.IntegerAmount}회 적중마다 공격력의 {Percent(effect.Magnitude)} 번개 피해",
                ItemEffectType.RestoreAllSPWithOvercharge => effect.IntegerAmount > 0
                    ? $"SP를 최대치 +{effect.IntegerAmount}까지 즉시 회복 (초과분은 SP 사용 시 사라짐)"
                    : "SP를 최대치까지 즉시 회복",
                ItemEffectType.RegenerateSPHalvesOverTime =>
                    $"{Number(effect.DurationSeconds)}초간 {Number(effect.IntervalSeconds)}초마다 SP {SPHalves(effect.IntegerAmount)} 회복",
                ItemEffectType.CurrentRoomBasicAttackDamagePercent =>
                    $"사용한 전투방에서 기본 공격 피해 +{Percent(effect.Magnitude)} (방을 떠나면 해제)",
                ItemEffectType.CurrentBossRoomSpeedPercent =>
                    $"사용한 보스방에서 공격속도 +{Percent(effect.Magnitude)}·이동속도 +{Percent(effect.SecondaryMagnitude)} (방을 떠나면 해제)",
                ItemEffectType.ProjectileSpeedPercent => $"탄속 +{Percent(effect.Magnitude)} (사거리 증가)",
                ItemEffectType.ProjectileLifetimePercent => $"사거리 +{Percent(effect.Magnitude)}",
                ItemEffectType.EscapeToFloorStartRoom =>
                    "전투 중 현재 층 시작방으로 탈출 (탈출한 방은 다시 들어가면 처음부터 시작)",
                ItemEffectType.Flight => "Run이 끝날 때까지 비행 (구덩이·장애물 위 이동, 벽·문은 통과 불가)",
                ItemEffectType.ProjectileSizePercent => $"투사체 크기 +{Percent(effect.Magnitude)}",
                ItemEffectType.PlayerSizePercent => $"몸 크기 +{Percent(effect.Magnitude)}",
                ItemEffectType.BasicAttackDamagePercent => $"기본 공격 피해 +{Percent(effect.Magnitude)}",
                ItemEffectType.BasicAttackPoison =>
                    $"기본 공격 적중 시 {Percent(effect.Magnitude)} 확률로 중독 ({Number(effect.DurationSeconds)}초간 " +
                    $"{Number(effect.IntervalSeconds)}초마다 공격력의 {Percent(effect.SecondaryMagnitude)} 피해, " +
                    $"최대 {effect.IntegerAmount}중첩)",
                ItemEffectType.HealOnLowerGradeSkillEveryN =>
                    $"저학년 스킬 {effect.IntegerAmount}회 시전마다 HP {Hearts(effect.Magnitude)} 회복 (스택마다 필요 횟수 -1)",
                ItemEffectType.ShieldOnCombatRoomEntry =>
                    $"전투방에 들어갈 때마다 방어막 {Hearts(effect.Magnitude)} 획득 (HP와 방어막 합 15칸까지)",
                ItemEffectType.ExecuteBelowHealth =>
                    $"피해를 준 적의 HP가 {Percent(effect.Magnitude)} 이하면 즉시 처치 (보스 제외)",
                ItemEffectType.BasicAttackHitExplosion =>
                    $"기본 공격 {effect.IntegerAmount}회 적중마다 반경 {Number(effect.Radius)}m에 공격력의 " +
                    $"{Percent(effect.Magnitude)} 폭발 피해",
                ItemEffectType.BasicAttackBurn =>
                    $"기본 공격 적중 시 {Percent(effect.Magnitude)} 확률로 화상 ({Number(effect.DurationSeconds)}초간 " +
                    $"{Number(effect.IntervalSeconds)}초마다 공격력의 {Percent(effect.SecondaryMagnitude)} 피해)",
                ItemEffectType.BasicAttackShock =>
                    $"기본 공격 적중 시 {Percent(effect.Magnitude)} 확률로 감전 ({Number(effect.DurationSeconds)}초간 " +
                    $"이동속도 -{Percent(effect.SecondaryMagnitude)}, 최대 {effect.IntegerAmount}중첩)",
                ItemEffectType.StatusTickDamagePercent => $"상태이상 피해 +{Percent(effect.Magnitude)}",
                ItemEffectType.DirectDamagePercentVsBurning =>
                    $"화상에 걸린 적에게 주는 직접 피해 +{Percent(effect.Magnitude)}",
                ItemEffectType.SkillDamagePercentVsShocked =>
                    $"감전된 적에게 스킬 피해 +{Percent(effect.Magnitude)}",
                ItemEffectType.CriticalBonusVsShocked =>
                    $"감전된 적에게 치명타 확률 +{PercentPoint(effect.SecondaryMagnitude)}·치명타 피해 " +
                    $"+{PercentPoint(effect.Magnitude)}",
                ItemEffectType.CriticalDamage => $"치명타 피해 +{PercentPoint(effect.Magnitude)}",
                ItemEffectType.NegateLethalDamageOnce =>
                    $"Run당 1회 사망 피해 무효 후 {Number(effect.DurationSeconds)}초간 무적",
                ItemEffectType.KillFrenzy =>
                    $"적 처치 시 {Number(effect.DurationSeconds)}초간 기본 공격 피해 +{Percent(effect.Magnitude)}·공격속도 " +
                    $"+{Percent(effect.SecondaryMagnitude)} (최대 {effect.IntegerAmount}중첩, 끝난 뒤 " +
                    $"{Number(effect.IntervalSeconds)}초간 재발동 불가)",
                ItemEffectType.KillFrenzyKnockbackPercent => $"중첩당 넉백 +{Percent(effect.Magnitude)}",
                ItemEffectType.GainGoldOnAcquire => $"획득 시 골드 +{Number(effect.Magnitude)} (보유 한도 99까지)",
                ItemEffectType.BounceBetweenEnemies =>
                    $"기본 공격이 적을 맞히면 가까운 다른 적에게 {effect.IntegerAmount}회 튕김 (스택마다 +1회, 같은 적을 " +
                    $"다시 맞히면 피해 {Percent(effect.SecondaryMagnitude)}씩)",
                ItemEffectType.SplitOnHit =>
                    $"적을 맞히면 그 적의 상하좌우 {effect.IntegerAmount}방향으로 분열 (분열탄 피해 " +
                    $"{Percent(effect.SecondaryMagnitude)})",
                ItemEffectType.WaterStreamAttack =>
                    "기본 공격이 벽까지 뻗는 물줄기로 변경 (줄기 위의 모든 적과 장애물을 타격)",
                ItemEffectType.GainShield => $"방어막 {Hearts(effect.Magnitude)} 획득",
                ItemEffectType.SpawnHealthPickups => $"주변에 하트 {effect.IntegerAmount}개 생성",
                ItemEffectType.GainRandomGold =>
                    $"골드 {effect.IntegerAmount}~{Number(effect.Magnitude)} 무작위 획득",
                ItemEffectType.CurrentRoomCriticalBonus =>
                    $"사용한 전투방에서 치명타 확률 +{PercentPoint(effect.SecondaryMagnitude)}·치명타 피해 " +
                    $"+{PercentPoint(effect.Magnitude)} (방을 떠나면 해제)",
                ItemEffectType.ReduceAndRecoverDamageTaken =>
                    $"{Number(effect.DurationSeconds)}초간 받는 피해 -{Hearts(effect.Magnitude)}, HP 피해를 받으면 " +
                    $"{Number(effect.IntervalSeconds)}초 뒤 그 피해 +{Hearts(effect.IntegerAmount)} 회복",
                ItemEffectType.DuplicateRoomChestsAndPickups =>
                    "현재 방의 열지 않은 상자와 바닥 소모품을 하나씩 복제 (복제 상자는 내용물을 따로 추첨)",
                ItemEffectType.FreeCurrentShopOffers =>
                    "현재 상점의 남은 상품을 모두 무료로 변경 (상점 밖에서는 사용 불가)",
                _ => effect.EffectType.ToString(),
            };
        }

        private static string SPHalves(int halves) => halves == 1
            ? "반 칸"
            : halves % 2 == 0 ? $"{halves / 2}칸" : $"{halves / 2}.5칸";

        private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";
        // Player HP values are half-heart units: 1 unit = half a heart.
        private static string Hearts(float units)
        {
            int roundedUnits = Mathf.RoundToInt(units);
            if (roundedUnits == 1) return "반 칸";
            return roundedUnits % 2 == 0
                ? $"{roundedUnits / 2}칸"
                : $"{roundedUnits / 2}.5칸";
        }

        // Keeps fractions such as 33.61%p (아멜리아의 E-Pad 클래식).
        private static string PercentPoint(float value) => Number(Mathf.Round(value * 10000f) / 100f) + "%p";
        private static string Number(float value) => Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.##");
    }
}
