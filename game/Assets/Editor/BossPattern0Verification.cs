using System;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Boss-Pattern-0: 새마음금고 패턴 개편과 보스 장애물의 맞은 횟수 판정. The detailed checks live with each boss's own
    // verification (Week15Boss2Verification for Saemaeum Vault, Week15Boss1Verification for Buseureogi's obstacles).
    public static class BossPattern0Verification
    {
        [MenuItem("Trickal Fan Game/Beta/Verify Boss-Pattern-0 Saemaeum Patterns")]
        public static void Verify()
        {
            // Saemaeum Vault first; it runs the Buseureogi verification at its end.
            Week15Boss2Verification.Verify();
            ValidateBossFeet();
            Debug.Log("Boss-Pattern-0 verification passed: Saemaeum Vault walks throwing three-shot fans, jumps 2 / 3 times " +
                      "with a turning treasure ring, raises hit-counted gold pillars, rains gold in phase two and " +
                      "slows to 0.55x; Buseureogi obstacles are two-hit obstacles, not enemies.");
        }

        private static void ValidateBossFeet()
        {
            foreach (var expected in BossPattern0Setup.BossFeet)
            {
                string path = expected.Path;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                TrickalFanGame.Enemy.EnemyFeet feet = prefab != null
                    ? prefab.GetComponentInChildren<TrickalFanGame.Enemy.EnemyFeet>(true) : null;
                Vector2 half = TrickalFanGame.Enemy.EnemyFeet.HalfSizeFor(prefab);
                if (feet == null || !feet.gameObject.activeSelf ||
                    Mathf.Abs(half.x - expected.HalfWidth) > 0.001f || Mathf.Abs(half.y - expected.HalfHeight) > 0.001f ||
                    Mathf.Abs(TrickalFanGame.Enemy.EnemyFeet.OffsetXFor(prefab) - expected.OffsetX) > 0.001f ||
                    Mathf.Abs(TrickalFanGame.Enemy.EnemyFeet.DropFor(prefab) - expected.Drop) > 0.001f ||
                    Mathf.Abs(feet.WorldRadius - Mathf.Min(expected.HalfWidth, expected.HalfHeight)) > 0.001f)
                    throw new InvalidOperationException(
                        $"Boss-Pattern-0: {path} must stand on feet {expected.HalfWidth}x{expected.HalfHeight} " +
                        $"offset {expected.OffsetX} dropped {expected.Drop}.");
                // A wide flat capsule around the circle; its world size is the chosen half sizes doubled.
                CapsuleCollider2D capsule = feet.Capsule;
                Vector2 capsuleWorld = capsule != null
                    ? Vector2.Scale(capsule.size, feet.transform.lossyScale)
                    : Vector2.zero;
                if (capsule == null || capsule.direction != CapsuleDirection2D.Horizontal || capsule.isTrigger ||
                    (capsuleWorld - half * 2f).sqrMagnitude > 0.0001f)
                    throw new InvalidOperationException(
                        $"Boss-Pattern-0: {path} feet need a horizontal capsule {half * 2f}, found {capsuleWorld}.");
                TrickalFanGame.Enemy.EnemyFeet.TryGetBody(prefab, out CircleCollider2D body);
                Vector2 root = prefab.transform.position;
                float bodyBottom = root.y - TrickalFanGame.Enemy.EnemyFeet.WorldRadiusOf(body);
                if (Mathf.Abs(feet.WorldCenter.y - half.y - (bodyBottom - expected.Drop)) > 0.001f ||
                    Mathf.Abs(feet.WorldCenter.x - root.x - expected.OffsetX) > 0.001f)
                    throw new InvalidOperationException($"Boss-Pattern-0: {path} feet are not at their chosen place.");
                TrickalFanGame.Enemy.EnemyFeet.GroundShape(prefab, out Vector2 center, out Vector2 shape);
                if ((shape - half).sqrMagnitude > 0.000001f || Vector2.Distance(center, feet.WorldCenter) > 0.001f)
                    throw new InvalidOperationException($"Boss-Pattern-0: {path} must stand on its capsule.");
            }
        }

        public static void SetupAndVerifyBatch()
        {
            string[] paths =
            {
                Week15Boss2Setup.BossPrefabPath, Week15Boss1Setup.CreamPrefabPath, Week15Boss1Setup.DoughPrefabPath,
                Week15Boss0Setup.BossPrefabPath, Week15Boss3Setup.BossPrefabPath,
            };
            BossPattern0Setup.Setup();
            string[] guids = Array.ConvertAll(paths, AssetDatabase.AssetPathToGUID);
            BossPattern0Setup.Setup();
            for (int index = 0; index < paths.Length; index++)
                if (string.IsNullOrWhiteSpace(guids[index]) || guids[index] != AssetDatabase.AssetPathToGUID(paths[index]))
                    throw new InvalidOperationException($"Boss-Pattern-0 setup changed the GUID of {paths[index]}.");
            Verify();
        }

        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Hp5EnemyScaleVerification.Verify();
            BossMovementAnimationVerification.Verify();
            BossSummonJumpAnimationVerification.Verify();
            VaultHealingAnimationVerification.Verify();
            Week25Enemy7Verification.Verify();
            // Bosses walk on feet since 2026-11-19: the feet contract and the crayon hero's own checks.
            Week27Hitbox2Verification.Verify();
            // Boss-4 runs Boss-3 first; calling Boss-3 again here would leave its Run seed behind for Boss-4.
            Week15Boss4Verification.Verify();
            CrayonHeroAttackAnimationVerification.Verify();
            BossIntro0Verification.VerifyBossEntryBatch();
            Debug.Log("Boss-Pattern-0 regression batch passed: Boss-Pattern-0, HP-5, boss motion, vault healing, " +
                      "Enemy-7, Hitbox-2 feet, Boss-3/4 crayon hero, crayon attack animation and the boss entry.");
        }
    }
}
