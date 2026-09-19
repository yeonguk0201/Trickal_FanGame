using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15EnemyRoleColorVerification
    {
        private readonly struct RoleColor
        {
            public RoleColor(string name, string path, Color expected)
            {
                Name = name;
                Path = path;
                Expected = expected;
            }

            public string Name { get; }
            public string Path { get; }
            public Color Expected { get; }
        }

        [MenuItem("Trickal Fan Game/Week 15/Verify Temporary Enemy Role Colors")]
        public static void Verify()
        {
            RoleColor[] roles = GetRoles();
            List<Color> actualColors = new();
            foreach (RoleColor role in roles)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(role.Path);
                Assert(prefab != null, $"Missing {role.Name} prefab at {role.Path}.");
                SpriteRenderer[] renderers = prefab.GetComponents<SpriteRenderer>();
                Assert(renderers.Length == 1,
                    $"{role.Name} prefab must have exactly one root SpriteRenderer for temporary role color.");
                Color actual = renderers[0].color;
                Assert(ColorDistance(actual, role.Expected) < 0.001f,
                    $"{role.Name} temporary color does not match the agreed role palette.");
                actualColors.Add(actual);
            }

            for (int first = 0; first < roles.Length; first++)
            {
                for (int second = first + 1; second < roles.Length; second++)
                {
                    Assert(ColorDistance(actualColors[first], actualColors[second]) >= 0.5f,
                        $"{roles[first].Name} and {roles[second].Name} are too similar to distinguish quickly.");
                }
            }

            Color lowBloodSugar = actualColors[2];
            Assert(ColorDistance(lowBloodSugar, new Color(1f, 0.85f, 0.2f, 1f)) >= 0.8f &&
                   ColorDistance(lowBloodSugar, new Color(1f, 0.2f, 0.15f, 1f)) >= 0.8f,
                "LowBloodSugar idle blue must remain distinct from charging telegraph yellow and danger red.");

            GameObject legacyBoss = AssetDatabase.LoadAssetAtPath<GameObject>(
                Week15Enemy2Setup.LegacyBossPrefabPath);
            Assert(legacyBoss != null &&
                   ColorDistance(legacyBoss.GetComponent<SpriteRenderer>().color,
                       Week15EnemyRoleColorSetup.LegacyBossColor) < 0.001f,
                "Changing normal-enemy role colors must not recolor the legacy boss inherited from TestEnemy.");

            Debug.Log(
                "Week 15 temporary enemy role color verification passed: all four idle colors match the " +
                "brown/green/blue/pink palette, remain pairwise distinct, and preserve charge-state readability.");
        }

        public static void SetupAndVerifyBatch()
        {
            RoleColor[] roles = GetRoles();
            string[] guids = new string[roles.Length];
            string legacyBossGuid = AssetDatabase.AssetPathToGUID(Week15Enemy2Setup.LegacyBossPrefabPath);
            for (int index = 0; index < roles.Length; index++)
            {
                guids[index] = AssetDatabase.AssetPathToGUID(roles[index].Path);
            }

            Week15EnemyRoleColorSetup.Setup();
            Week15EnemyRoleColorSetup.Setup();
            for (int index = 0; index < roles.Length; index++)
            {
                Assert(!string.IsNullOrWhiteSpace(guids[index]) &&
                       AssetDatabase.AssetPathToGUID(roles[index].Path) == guids[index],
                    $"Role color Setup changed the {roles[index].Name} prefab GUID.");
            }
            Assert(!string.IsNullOrWhiteSpace(legacyBossGuid) &&
                   AssetDatabase.AssetPathToGUID(Week15Enemy2Setup.LegacyBossPrefabPath) == legacyBossGuid,
                "Role color Setup changed the legacy boss prefab GUID.");

            Verify();
            Week15Enemy2Verification.Verify();
            Week15Enemy3Verification.Verify();
            Week15Enemy4Verification.Verify();
        }

        private static RoleColor[] GetRoles()
        {
            return new[]
            {
                new RoleColor("Bulhyojason", Week15Enemy2Setup.BulhyojasonPrefabPath,
                    Week15EnemyRoleColorSetup.BulhyojasonColor),
                new RoleColor("Sansamo", Week15Enemy2Setup.SansamoPrefabPath,
                    Week15EnemyRoleColorSetup.SansamoColor),
                new RoleColor("LowBloodSugar", Week15Enemy0Setup.ChargingPrefabPath,
                    Week15EnemyRoleColorSetup.LowBloodSugarColor),
                new RoleColor("HighBloodSugar", Week15Enemy4Setup.SniperPrefabPath,
                    Week15EnemyRoleColorSetup.HighBloodSugarColor),
            };
        }

        private static float ColorDistance(Color first, Color second)
        {
            Vector3 delta = new(first.r - second.r, first.g - second.g, first.b - second.b);
            return delta.magnitude;
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
