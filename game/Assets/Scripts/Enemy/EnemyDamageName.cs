using UnityEngine;

namespace TrickalFanGame.Enemy
{
    internal static class EnemyDamageName
    {
        internal static string From(GameObject source)
        {
            if (source == null) return null;

            if (source.GetComponent<EnemyChampionVolley>() is EnemyChampionVolley volley)
                return volley.SourceDisplayName;

            for (Transform current = source.transform; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out BossController boss)) return boss.DisplayName;
                string name = current.name;
                int suffix = name.IndexOf('(');
                if (suffix >= 0) name = name.Substring(0, suffix).TrimEnd();
                string displayName = name switch
                {
                    "TestEnemy" => "불효자손",
                    "SansamoEnemy" => "산사모-활발",
                    "ChargingEnemy" => "저혈당 요정-순수",
                    "RangedEnemy" => "원거리 요정",
                    "QuickRangedFairy" => "산사모-순수",
                    "HighBloodSugarFairy" => "고혈당 요정",
                    "LivelyLowBloodSugarFairy" => "저혈당 요정-활발",
                    "LobbingFairy" => "저혈당 요정-광기",
                    "SelfDestructFairy" => "산사모-광기",
                    "AmbushFairy" => "불효자손-냉정",
                    "NururingFairyWarrior" => "누루링-요정전사",
                    "NururingSplit" => "작은 누루링",
                    "JyubiEnemy" => "쥬비",
                    "Buseureogi Crumb Minion" or "BuseureogiCrumbMinion" => "부스러기 쫄따구",
                    _ => null
                };
                if (displayName != null) return displayName;
            }

            return null;
        }
    }
}
