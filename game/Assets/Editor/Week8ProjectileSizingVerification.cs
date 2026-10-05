using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week8ProjectileSizingVerification
    {
        private const string PlayerProjectilePath = "Assets/Prefabs/PlayerProjectile.prefab";
        private const string SkillProjectilePath = "Assets/Prefabs/HomingSkillProjectile.prefab";

        [MenuItem("Trickal Fan Game/Verify Pre-Phase G Projectile Sizing")]
        public static void Verify()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerProjectilePath);
            GameObject skillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillProjectilePath);
            Assert(playerPrefab != null, $"Missing {PlayerProjectilePath}.");
            Assert(skillPrefab != null, $"Missing {SkillProjectilePath}.");

            VerifyProjectile(
                "Player basic projectile",
                playerPrefab.transform,
                playerPrefab.GetComponent<CircleCollider2D>(),
                ProjectileSizing.PlayerBasicScale);
            VerifyProjectile(
                "Player skill projectile",
                skillPrefab.transform,
                skillPrefab.GetComponent<CircleCollider2D>(),
                ProjectileSizing.PlayerSkillScale);

            EnemyProjectile ranged = null;
            BossProjectile boss = null;
            try
            {
                ranged = EnemyProjectile.Create(
                    Vector2.zero, Vector2.right, null, EnemyDamageTier.Light, 1f, 1f, null);
                boss = BossProjectile.Create(
                    Vector2.zero, Vector2.right, null, EnemyDamageTier.Medium, null);

                VerifyProjectile(
                    "Ranged-enemy projectile",
                    ranged.transform,
                    ranged.GetComponent<CircleCollider2D>(),
                    ProjectileSizing.RangedEnemyScale);
                VerifyProjectile(
                    "Boss projectile",
                    boss.transform,
                    boss.GetComponent<CircleCollider2D>(),
                    ProjectileSizing.BossScale);

                Assert(
                    ProjectileSizing.WorldCollisionRadius(ProjectileSizing.PlayerBasicScale) < 0.3f,
                    "Player basic projectile must remain clearly smaller than a character-sized collider.");
                Assert(
                    ProjectileSizing.PlayerBasicScale > ProjectileSizing.BossScale &&
                    ProjectileSizing.BossScale > ProjectileSizing.RangedEnemyScale &&
                    ProjectileSizing.RangedEnemyScale > ProjectileSizing.PlayerSkillScale,
                    "Projectile collision-scale hierarchy must remain player basic > boss > ranged enemy > player skill; artwork PPU is independent.");
            }
            finally
            {
                if (ranged != null) ranged.StopAtBoundary();
                if (boss != null) boss.StopAtBoundary();
            }

            Debug.Log(
                "Pre-Phase G projectile sizing verified: player basic, player skill, ranged-enemy, and boss " +
                "projectiles use the shared root-scale and collision-radius contract; skill artwork can be larger independently.");
        }

        private static void VerifyProjectile(
            string label,
            Transform projectileTransform,
            CircleCollider2D collider,
            float expectedScale)
        {
            Assert(projectileTransform != null, $"{label} has no transform.");
            Assert(collider != null, $"{label} needs a CircleCollider2D.");
            Vector3 scale = projectileTransform.localScale;
            Assert(
                Approximately(scale.x, expectedScale) &&
                Approximately(scale.y, expectedScale) &&
                Approximately(scale.z, expectedScale),
                $"{label} scale must be uniformly {expectedScale}, got {scale}.");
            Assert(
                Approximately(collider.radius, ProjectileSizing.BaseColliderRadius),
                $"{label} collider radius must be {ProjectileSizing.BaseColliderRadius}, got {collider.radius}.");
        }

        private static bool Approximately(float left, float right)
        {
            return Mathf.Abs(left - right) <= 0.0001f;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
