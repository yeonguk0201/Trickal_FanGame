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

            string first = BuildEffect(effects[0]);
            if (effects.Count == 1) return first;

            string second = BuildEffect(effects[1]);
            return string.IsNullOrEmpty(second) ? first : first + " · " + second;
        }

        private static string BuildLegacy(ItemEffectType type, float value)
        {
            return type switch
            {
                ItemEffectType.AttackDamage => $"공격력 +{Number(value)}",
                ItemEffectType.MaxHealth => $"최대 HP +{Number(value)}",
                ItemEffectType.MoveSpeed => $"이동속도 +{Number(value)}",
                ItemEffectType.MultiShot => $"투사체 +{Mathf.RoundToInt(value)}",
                ItemEffectType.Pierce => $"관통 +{Mathf.RoundToInt(value)}",
                ItemEffectType.HealOnKill => $"처치 시 HP +{Number(value)} 회복",
                _ => type.ToString(),
            };
        }

        private static string BuildEffect(ItemEffectEntry effect)
        {
            if (effect == null) return string.Empty;

            return effect.EffectType switch
            {
                ItemEffectType.AttackDamage => $"공격력 +{Number(effect.Magnitude)}",
                ItemEffectType.MaxHealth => $"최대 HP +{Number(effect.Magnitude)}",
                ItemEffectType.MoveSpeed => $"이동속도 +{Number(effect.Magnitude)}",
                ItemEffectType.MultiShot => $"투사체 +{effect.IntegerAmount}",
                ItemEffectType.Pierce => $"관통 +{effect.IntegerAmount}",
                ItemEffectType.HealOnKill => $"처치 시 HP +{Number(effect.Magnitude)} 회복",
                ItemEffectType.AttackDamagePercent => $"공격력 +{Percent(effect.Magnitude)}",
                ItemEffectType.SkillDamagePercent => $"스킬 피해 +{Percent(effect.Magnitude)}",
                ItemEffectType.CriticalChance => $"치명타 확률 +{PercentPoint(effect.Magnitude)}",
                ItemEffectType.AttackSpeedPercent => $"공격속도 +{Percent(effect.Magnitude)}",
                ItemEffectType.MaxHealthDamageAura => $"주변 적에게 초당 최대 HP의 {Percent(effect.Magnitude)} 피해",
                ItemEffectType.MaxHealthFlat => $"최대 HP +{Number(effect.Magnitude)}",
                ItemEffectType.ShieldOnAcquireMaxHealthPercent => $"획득 시 최대 HP {Percent(effect.Magnitude)} 방어막",
                ItemEffectType.MoveSpeedPercentBelowHealth =>
                    $"HP {Percent(effect.HealthThreshold)} 이하 이동속도 +{Percent(effect.Magnitude)}",
                ItemEffectType.HealOnKillMaxHealthPercent =>
                    $"처치 시 최대 HP의 {Percent(effect.Magnitude)} 회복",
                ItemEffectType.DistanceDamage => $"거리 비례 피해 최대 +{Percent(effect.Magnitude)}",
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
                    $"HP {Percent(effect.HealthThreshold)} 이하 시 {Number(effect.DurationSeconds)}초간 최대 HP의 {Percent(effect.Magnitude)} 회복 (Run당 1회)",
                ItemEffectType.BasicAttackHitLightning =>
                    $"기본 공격 {effect.IntegerAmount}회 적중마다 공격력의 {Percent(effect.Magnitude)} 번개 피해",
                _ => effect.EffectType.ToString(),
            };
        }

        private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";
        private static string PercentPoint(float value) => Mathf.RoundToInt(value * 100f) + "%p";
        private static string Number(float value) => Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.##");
    }
}
