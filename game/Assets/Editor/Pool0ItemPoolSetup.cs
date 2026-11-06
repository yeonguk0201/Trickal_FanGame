using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Pool-0: tags each reward-pool artifact with the sources that draw it first. Boss rewards lean on stats so the
    // next floor stays survivable, treasure rooms on build-defining effects, shops on utility and recovery. A source
    // whose own pool runs short fills the rest of its cards from the shared pool (ArtifactRewardSelector).
    public static class Pool0ItemPoolSetup
    {
        public const string ItemFolder = "Assets/Items";

        private const ItemPoolSource PureStat = ItemPoolSource.Boss | ItemPoolSource.Shop;
        private const ItemPoolSource MixedStat = ItemPoolSource.Boss | ItemPoolSource.Treasure;
        private const ItemPoolSource Build = ItemPoolSource.Treasure;
        private const ItemPoolSource Utility = ItemPoolSource.Shop;

        // Every active reward-pool artifact must appear here. Golden chest exclusives stay out of every pool.
        public static readonly IReadOnlyDictionary<string, ItemPoolSource> Sources =
            new Dictionary<string, ItemPoolSource>(StringComparer.Ordinal)
            {
                // Pure stats: a number and nothing else.
                ["item-01"] = PureStat, // 급조한 목검: 공격력
                ["item-12"] = PureStat, // 녹슨 송곳: 치명 확률
                ["item-04"] = PureStat, // 낡은 화살: 공격 속도
                ["artifact-pork-cutlet-hairpin"] = PureStat, // 돈까스 모양 머리핀: 최대 체력
                ["artifact-sylla-wind-arrow"] = PureStat, // 실라의 바람살: 공속·이속·탄속
                ["artifact-kanna-cannon"] = PureStat, // 칸나의 대포: 투사체 크기
                // Pool-2: pure-stat artifacts (priority 1 and 2).
                ["artifact-towel-headband"] = PureStat, // 수건 머리띠: 최대 체력
                ["artifact-old-tree-dagger"] = PureStat, // 고목나무 비수: 치명 확률·치명 피해
                ["artifact-patched-staff"] = PureStat, // 덧댄 지팡이: 스킬 피해
                ["artifact-mist-dagger"] = PureStat, // 안개 비수: 치명 피해
                ["artifact-obsidian-shuriken"] = PureStat, // 흑요석 수리검: 치명 피해·기본 공격 피해
                ["artifact-fox-dagger"] = PureStat, // 여우 단검: 공속·치명 확률·공격력
                ["artifact-combat-manual"] = PureStat, // 전투 지침서: 치명 피해
                ["artifact-jade-grimoire"] = PureStat, // 옥빛 마법서: 공격력·치명 확률·스킬 피해
                ["artifact-sharp-staff"] = PureStat, // 날카로운 지팡이: 공격력·치명 피해·스킬 피해
                ["artifact-golden-crown"] = PureStat, // 황금 왕관: 치명 확률·치명 피해·스킬 피해
                ["artifact-belita-staff"] = PureStat, // 벨리타의 지팡이: 공격력·치명 피해
                ["artifact-weighted-vest"] = PureStat, // 중량 조끼: 최대 체력 + 이속 감소
                ["artifact-fairy-robe"] = PureStat, // 요정 로브: 최대 체력·이속
                ["artifact-scale-armor"] = PureStat, // 비늘 갑옷: 최대 체력
                ["artifact-jade-fountain-pen"] = PureStat, // 제이드의 비싼 만년필: 공격력·공속·스킬 피해
                ["artifact-shining-tiara"] = PureStat, // 찬란한 티아라: 최대 체력·방어막 획득 보너스
                ["artifact-priest-censer"] = PureStat, // 제사장의 향로: 최대 체력·방어막 획득 보너스
                ["artifact-blanchet-glass-bluebird"] = PureStat, // 블랑셰의 유리 파랑새: 고학년 스킬 피해 + 기본 공격 감소

                // Stats with a trade-off or a conditional bonus: boss growth that can also shape a build.
                ["artifact-30kg-kettlebell"] = MixedStat, // 스킬 피해 + 이속 감소
                ["item-02"] = MixedStat, // 풍선 갑옷: 최대 체력 + 획득 보호막
                ["item-13"] = MixedStat, // 에르핀의 지팡이: 최대 SP + SP 조건 투사체
                ["artifact-greed-ring"] = MixedStat, // 탐욕의 반지: 치명 피해 + 상태이상 피해
                ["artifact-amelia-epad-classic"] = MixedStat, // 스킬 피해 + 감전 대상 보너스
                ["artifact-ner-eldr-flag"] = MixedStat, // 최대 체력 + 스킬 회복
                ["artifact-giant-potion"] = MixedStat, // 거대화: 체력·기본공격 + 크기·이속 감소
                ["item-15"] = MixedStat, // 장난감 망원경: 공격력 + 거리 피해·사거리

                // Build-defining effects.
                ["artifact-atta-sword"] = Build, // 앗따검: 상태이상 피해
                ["artifact-attatta-gun"] = Build, // 앗딱따건: 감전
                ["artifact-blazing-bow"] = Build, // 활활 불타활: 화상
                ["artifact-burning-branch"] = Build, // 불타는 가지: 화상 대상 피해
                ["artifact-bibi-snot"] = Build, // 비비의 수은: 독
                ["artifact-clear-weather-card"] = Build, // 날씨는 맑음 카드: 번개
                ["artifact-explosive-muffin"] = Build, // 폭발 머핀: 폭발
                ["artifact-kanta-top"] = Build, // 칸타의 팽이: 적 사이 튕김
                ["artifact-levi-dagger"] = Build, // 레비의 단도: 치명상 1회 무효
                ["artifact-rim-scythe"] = Build, // 림의 낫: 처형
                ["artifact-shasha-jar"] = Build, // 샤샤의 항아리: 물줄기
                ["artifact-shushushushuk-glove"] = Build, // 슈슈슈슉 글러브: 처치 광란
                ["item-11"] = Build, // 다야의 다이아몬드 커터: 분열
                ["item-14"] = Build, // 광기의 가면: 공격 오라

                // Utility and recovery.
                ["item-03"] = Utility, // 도깨비 감투: 낮은 체력 이속
                ["item-08"] = Utility, // 코미의 베개: 처치 회복
                ["artifact-life-gem"] = Utility, // 생명의 보석: 위기 회복
                ["artifact-emergency-protection-belt"] = Utility, // 긴급 보호 벨트: 전투방 보호막
            };

        public static readonly IReadOnlyList<ItemPoolSource> DrawSources =
            new[] { ItemPoolSource.Boss, ItemPoolSource.Treasure, ItemPoolSource.Shop };

        // A 3택1 needs three cards from its own pool before the shared fallback kicks in.
        public const int MinimumPoolSize = ArtifactRewardSelector.MaximumCandidateCount;

        [MenuItem("Trickal Fan Game/Week 28/Setup Pool-0 Item Pools")]
        public static void Setup()
        {
            int changed = 0;
            foreach (ItemDefinition item in LoadArtifacts())
            {
                ItemPoolSource expected = ExpectedSources(item);
                if (item.PoolSources == expected) continue;
                Undo.RecordObject(item, "Configure Pool-0 item pools");
                item.ConfigurePoolSources(expected);
                EditorUtility.SetDirty(item);
                changed++;
            }

            string[] missing = Sources.Keys.Where(id => LoadArtifacts().All(item => item.ItemId != id)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("Pool-0 lists artifacts that have no asset: " +
                                                    string.Join(", ", missing));

            if (changed > 0) AssetDatabase.SaveAssets();
            Debug.Log($"Pool-0 item pools ready ({changed} artifact(s) updated).");
        }

        // Inactive artifacts and golden chest exclusives belong to no pool; every other active artifact must be
        // classified, so a new artifact fails here until it gets a pool.
        public static ItemPoolSource ExpectedSources(ItemDefinition item)
        {
            if (item == null || !item.IsActive || GoldenChestExclusivePool.IsExclusive(item)) return ItemPoolSource.None;
            if (Sources.TryGetValue(item.ItemId, out ItemPoolSource sources)) return sources;
            throw new InvalidOperationException(
                $"Pool-0: active artifact {item.ItemId} has no reward pool. Add it to Pool0ItemPoolSetup.Sources.");
        }

        public static ItemDefinition[] LoadArtifacts() =>
            AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null && item.Kind == ItemKind.Artifact)
                .OrderBy(item => item.ItemId, StringComparer.Ordinal)
                .ToArray();
    }
}
