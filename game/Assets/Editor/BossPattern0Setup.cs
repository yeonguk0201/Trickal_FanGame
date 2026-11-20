using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Boss-Pattern-0 (2026-11-18): 새마음금고 패턴 개편과 보스 장애물의 맞은 횟수 판정.
    // - 새마음금고: 부채꼴 투척을 빼고 걷기만 남긴다. 점프는 1페이즈 2번·2페이즈 3번, 착지마다 사방 탄막. 황금 기둥과
    //   2페이즈 황금 메테오를 더하고, 슬라임 감속을 0.75배에서 0.55배로 강화한다.
    // - 부스러기의 크림·반죽 장애물: Health 대신 맞은 횟수(2타)로 부서지는 DestructibleObstacle로 바꾼다. 적이 아니므로
    //   처치 수·적중/처치 아이템·상태 이상·유도가 대상으로 삼지 않는다.
    // - 보스 3종(부스러기·새마음금고·크레용 용사)에 발 콜라이더(EnemyFeet)를 붙인다(2026-11-19). 몸 원은 피격·접촉만
    //   맡고 벽·장애물·구덩이는 발이 닿는다. 발 크기는 보스마다 그림 바닥에 맞추고 일반 적(0.35)보다 크다.
    //   Week27Hitbox2Setup도 같은 규칙(EnemyFeet.Needs·RadiusFor)으로 유지한다.
    // Only the fields this slice owns are written, so re-running it keeps later tuning of the other fields.
    public static class BossPattern0Setup
    {
        public const int BuseureogiObstacleRequiredHits = 2;
        // World feet per boss, chosen by the user in the boss feet tuner (2026-11-19, output/boss-feet-review): half
        // width and half height (a wide capsule when they differ), sideways offset for the unflipped drawing and drop
        // below the body circle. The capsules follow the wide flat bottoms (부스러기's cup, 새마음금고's chest) while
        // keeping the upper-wall gap small; 크레용 용사's feet move under its boots, off the cape side.
        public static readonly (string Path, float HalfWidth, float HalfHeight, float OffsetX, float Drop)[] BossFeet =
        {
            (Week15Boss0Setup.BossPrefabPath, 0.70f, 0.42f, 0f, 0f),
            (Week15Boss2Setup.BossPrefabPath, 1.33f, 0.55f, 0f, 0f),
            (Week15Boss3Setup.BossPrefabPath, 0.83f, 0.40f, 0.24f, 0.37f),
        };
        public static readonly Color BuseureogiObstacleCrackedColor = new(0.72f, 0.72f, 0.72f, 1f);

        public static BossPatternDefinition[] SaemaeumPatterns() => new BossPatternDefinition[]
        {
            // The fan throw was removed; the walk keeps its slot under a new ID so the old one is not reused.
            new("saemaeum-approach-walk", BossPatternExecution.SaemaeumApproachWalk, 0.55f, 3.0f, 0.75f, 0.8f),
            // The warning is the first jump's 0.8s crouch, fixed so phase two's faster tempo does not shorten it
            // (0.01 scaled + 0.79 fixed). The active time follows the real landings (2 / 3 jumps).
            new("saemaeum-rage-jump-sequence", BossPatternExecution.SaemaeumJumpSequence, 0.01f, 5.9f, 1.15f, 2.5f,
                0.79f),
            new("saemaeum-eat-treasure", BossPatternExecution.SaemaeumTreasureHeal, 0.8f, 1.8f, 1.05f, 5.5f),
            new("saemaeum-slime-scatter", BossPatternExecution.SaemaeumSlimeScatter, 0.8f, 1.4f, 0.8f, 4.5f),
            new("saemaeum-golden-pillars", BossPatternExecution.SaemaeumGoldPillars, 0.8f, 0.6f, 0.6f, 5.0f),
            // Phase two only. The runtime holds it back until 8s after the last lump of a rain lands.
            new("saemaeum-gold-meteor", BossPatternExecution.SaemaeumGoldMeteor, 0.9f, 0.3f, 0.4f, 0f),
        };

        [MenuItem("Trickal Fan Game/Beta/Setup Boss-Pattern-0 Saemaeum Patterns")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Boss-Pattern-0 setup.");
            ConfigureBuseureogiObstacle(Week15Boss1Setup.CreamPrefabPath, "cream");
            ConfigureBuseureogiObstacle(Week15Boss1Setup.DoughPrefabPath, "dough");
            ConfigureSaemaeum();
            foreach (var feet in BossFeet)
                ConfigureBossFeet(feet.Path, feet.HalfWidth, feet.HalfHeight, feet.OffsetX, feet.Drop);
            AssetDatabase.SaveAssets();
            Debug.Log("Boss-Pattern-0 setup complete: Saemaeum Vault walks, jumps 2 / 3 times with a landing ring, " +
                      "raises gold pillars, rains gold in phase two and slows to 0.55x; Buseureogi obstacles break " +
                      "after two hits.");
        }

        private static void ConfigureBuseureogiObstacle(string path, string label)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // TestEnemy requires Health, so it goes first.
                TestEnemy legacyEnemy = contents.GetComponent<TestEnemy>();
                if (legacyEnemy != null) UnityEngine.Object.DestroyImmediate(legacyEnemy, true);
                Health legacyHealth = contents.GetComponent<Health>();
                if (legacyHealth != null) UnityEngine.Object.DestroyImmediate(legacyHealth, true);

                contents.layer = LayerMask.NameToLayer("Environment");
                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                CircleCollider2D collider = contents.GetComponent<CircleCollider2D>();
                if (renderer == null || collider == null)
                    throw new InvalidOperationException($"Boss-Pattern-0: {path} lost its renderer or collider.");
                renderer.color = Color.white;
                collider.isTrigger = false;
                // On the Environment layer the default matrix already lets players, enemies and bosses collide.
                collider.includeLayers = 0;
                collider.excludeLayers = 0;
                collider.layerOverridePriority = 0;

                DestructibleObstacle obstacle = contents.GetComponent<DestructibleObstacle>();
                if (obstacle == null) obstacle = contents.AddComponent<DestructibleObstacle>();
                obstacle.Configure($"buseureogi-{label}-obstacle", BuseureogiObstacleRequiredHits, null, renderer);
                obstacle.ConfigureFixedKind($"buseureogi-{label}", Color.white, BuseureogiObstacleCrackedColor,
                    false, 0, Color.white);
                BossSummonedObstacle summoned = contents.GetComponent<BossSummonedObstacle>();
                if (summoned == null) summoned = contents.AddComponent<BossSummonedObstacle>();
                // Buseureogi's own shots keep flying over its obstacles, as before.
                summoned.Configure(false);
                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                    throw new InvalidOperationException($"Boss-Pattern-0 could not save {path}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureSaemaeum()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(Week15Boss2Setup.BossPrefabPath);
            try
            {
                BossController boss = contents.GetComponent<BossController>();
                SaemaeumVaultBossPatternRuntime runtime = contents.GetComponent<SaemaeumVaultBossPatternRuntime>();
                if (boss == null || runtime == null)
                    throw new InvalidOperationException("Boss-Pattern-0 requires the Saemaeum Vault boss Prefab.");
                boss.ConfigurePatterns(SaemaeumPatterns());

                SerializedObject serialized = new(runtime);
                SetInt(serialized, "phaseOneMinimumJumps", 2);
                SetInt(serialized, "phaseOneMaximumJumps", 2);
                SetInt(serialized, "phaseTwoMinimumJumps", 3);
                SetInt(serialized, "phaseTwoMaximumJumps", 3);
                SetFloat(serialized, "phaseOneJumpDuration", 1.5f);
                SetFloat(serialized, "phaseTwoJumpDuration", 1.4f);
                SetFloat(serialized, "preJumpCrouchDuration", 0.8f);
                SetFloat(serialized, "phaseOneJumpRest", 1.0f);
                SetFloat(serialized, "phaseTwoJumpRest", 0.85f);

                SetInt(serialized, "phaseOneLandingBurstCount", 8);
                SetInt(serialized, "phaseTwoLandingBurstCount", 12);
                SetFloat(serialized, "phaseOneLandingBurstSpeed", 4.5f);
                SetFloat(serialized, "phaseTwoLandingBurstSpeed", 5.0f);
                SetEnum(serialized, "landingBurstDamageTier", (int)EnemyDamageTier.Light);
                SetFloat(serialized, "landingBurstSpawnMargin", 0.2f);

                SetInt(serialized, "phaseOnePillars", 4);
                SetInt(serialized, "phaseTwoPillars", 6);
                SetFloat(serialized, "phaseOnePillarWarning", 0.8f);
                SetFloat(serialized, "phaseTwoPillarWarning", 0.7f);
                SetFloat(serialized, "pillarStagger", 0.12f);
                SetFloat(serialized, "pillarHitRadius", 0.6f);
                SetEnum(serialized, "pillarDamageTier", (int)EnemyDamageTier.Light);
                SetFloat(serialized, "pillarKnockbackSpeed", 7f);
                SetFloat(serialized, "pillarKnockbackDuration", 0.2f);
                SetInt(serialized, "pillarRequiredHits", 3);
                SetInt(serialized, "pillarTargetEvery", 3);

                SetFloat(serialized, "meteorMinimumDuration", 8f);
                SetFloat(serialized, "meteorMaximumDuration", 10f);
                SetInt(serialized, "meteorOpeningCount", 5);
                SetFloat(serialized, "meteorOpeningInterval", 0.35f);
                SetFloat(serialized, "meteorInterval", 0.45f);
                SetInt(serialized, "meteorTargetEvery", 3);
                SetFloat(serialized, "meteorWarning", 0.9f);
                SetFloat(serialized, "meteorRadius", 0.9f);
                SetEnum(serialized, "meteorDamageTier", (int)EnemyDamageTier.Light);
                SetFloat(serialized, "meteorKnockbackSpeed", 7f);
                SetFloat(serialized, "meteorKnockbackDuration", 0.2f);
                SetFloat(serialized, "meteorCooldownAfterRain", 8f);

                SetFloat(serialized, "slimeSpeedMultiplier", 0.55f);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(contents, Week15Boss2Setup.BossPrefabPath) == null)
                    throw new InvalidOperationException("Boss-Pattern-0 could not save the Saemaeum Vault Prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureBossFeet(string path, float halfWidth, float halfHeight, float offsetX, float drop)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                BossController boss = contents.GetComponent<BossController>();
                if (boss == null || !EnemyFeet.Needs(contents))
                    throw new InvalidOperationException($"Boss-Pattern-0: {path} is not a walking boss for feet.");
                bool changed = !Mathf.Approximately(boss.FeetHalfWidth, halfWidth) ||
                               !Mathf.Approximately(boss.FeetHalfHeight, halfHeight) ||
                               !Mathf.Approximately(boss.FeetOffsetX, offsetX) ||
                               !Mathf.Approximately(boss.FeetDrop, drop);
                boss.ConfigureFeet(halfWidth, halfHeight, offsetX, drop);
                // Unchanged feet leave the Prefab file alone.
                if (!EnemyFeet.Ensure(contents, out _) && !changed) return;
                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                    throw new InvalidOperationException($"Boss-Pattern-0 could not save {path}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static SerializedProperty Property(SerializedObject serialized, string name) =>
            serialized.FindProperty(name) ??
            throw new InvalidOperationException($"Boss-Pattern-0: missing serialized field '{name}'.");

        private static void SetInt(SerializedObject serialized, string name, int value) =>
            Property(serialized, name).intValue = value;

        private static void SetFloat(SerializedObject serialized, string name, float value) =>
            Property(serialized, name).floatValue = value;

        private static void SetEnum(SerializedObject serialized, string name, int value) =>
            Property(serialized, name).enumValueIndex = value;
    }
}
