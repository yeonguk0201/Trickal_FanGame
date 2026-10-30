using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week19Difficulty1Setup
    {
        public const int EncounterContentVersion = 8;
        public const string TablePath = "Assets/Encounters/room-difficulty-table.asset";
        public const int ObstacleLayoutModifier = 1;

        public const int PressureMinimumThreat = 10;
        // Enemy-9: pressure is two waves of one melee and one rear slot, so the 4-point 누루링-요정전사 in its melee
        // slots raised the maximum from 14 to 16.
        public const int PressureMaximumThreat = 16;
        public const int CrossfireMinimumThreat = 15;
        public const int CrossfireMaximumThreat = 19;

        public static readonly EnemyThreatScore[] ThreatScores =
        {
            new(EncounterEnemyRole.Chaser, 2),
            new(EncounterEnemyRole.FastChaser, 3),
            new(EncounterEnemyRole.Ranged, 3),
            new(EncounterEnemyRole.Sniper, 4),
            new(EncounterEnemyRole.Charging, 4),
            // Enemy-7: every normal role needs a score, so 쥬비 lives here and a re-run keeps it.
            new(EncounterEnemyRole.Jyubi, 1),
            // Enemy-8: 저혈당 요정-활발.
            new(EncounterEnemyRole.FourWayShooter, 3),
            // Enemy-9: 누루링-요정전사. Its two splits are counted in this score.
            new(EncounterEnemyRole.SplittingChaser, 4),
            // Enemy-10: 자폭형.
            new(EncounterEnemyRole.SelfDestruct, 3),
            // Enemy-11: 대왕쥬비. It keeps summoning 쥬비 while it lives, so it weighs like two strong enemies.
            new(EncounterEnemyRole.JyubiKing, 6),
            // Enemy-12: 곡사형. Like the sniper it hurts from behind cover, so crossfire's range stays 15~19.
            new(EncounterEnemyRole.Lobber, 4),
            // Enemy-13: 잠복형. Out of reach under the floor most of the time, like 누루링 it is a 4-point melee slot,
            // so pressure keeps 10~16.
            new(EncounterEnemyRole.Ambusher, 4),
        };

        public static readonly RoomDifficultyTierBand[] TierBands =
        {
            new(RoomDifficultyTier.Easy, 0),
            new(RoomDifficultyTier.Normal, 12),
            new(RoomDifficultyTier.Hard, 16),
        };

        public static readonly FloorDifficultyRange[] FloorRanges =
        {
            new(1, 0, 18),
            new(2, 0, 20),
            new(3, 0, 22),
        };

        public static readonly RoomDifficultyDistanceWeight[] DistanceWeights =
        {
            new(RoomDifficultyTier.Easy, 40, 10),
            new(RoomDifficultyTier.Normal, 45, 40),
            new(RoomDifficultyTier.Hard, 15, 50),
        };

        [MenuItem("Trickal Fan Game/Week 19/Setup Difficulty-1 Room Difficulty")]
        public static void Setup()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null ||
                generator.EncounterContentVersion < Week19Spawn2Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Difficulty-1 requires the completed Spawn-2 Game Scene.");
            }

            RoomDifficultyTable table = EnsureTable();
            ConfigureDeclaredThreats(generator);
            ConfigureLayoutModifiers(generator);

            Undo.RecordObject(generator, "Configure Difficulty-1 room difficulty");
            generator.ConfigureEncounters(
                Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                generator.EncounterDefinitions.ToArray());
            generator.ConfigureDifficulty(table);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
            {
                throw new InvalidOperationException("Game Scene save failed during Difficulty-1 setup.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Difficulty-1 ready: threat scores 2/3/3/4/4, Jyubi 1, four-way 3 and splitting 4, Easy<=11/Normal 12~15/Hard>=16, " +
                      "floor caps 18/20/22, distance tier weights 40/45/15 -> 10/40/50, obstacle Layouts +1.");
        }

        private static RoomDifficultyTable EnsureTable()
        {
            RoomDifficultyTable table = AssetDatabase.LoadAssetAtPath<RoomDifficultyTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<RoomDifficultyTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            Undo.RecordObject(table, "Configure Difficulty-1 table");
            table.Configure(ThreatScores, TierBands, FloorRanges, DistanceWeights);
            if (!table.TryValidate(out string error))
            {
                throw new InvalidOperationException($"Difficulty-1 table is invalid. {error}");
            }

            EditorUtility.SetDirty(table);
            return table;
        }

        private static void ConfigureDeclaredThreats(FloorGenerator generator)
        {
            foreach (Week19Spawn2Setup.ProfileSpec spec in Week19Spawn2Setup.Profiles)
            {
                ConfigureDeclaredThreat(generator, Week19Spawn2Setup.EncounterId(spec.ProfileId, "pressure"),
                    PressureMinimumThreat, PressureMaximumThreat);
                ConfigureDeclaredThreat(generator, Week19Spawn2Setup.EncounterId(spec.ProfileId, "crossfire"),
                    CrossfireMinimumThreat, CrossfireMaximumThreat);
            }
        }

        private static void ConfigureDeclaredThreat(FloorGenerator generator, string encounterId, int minimum,
            int maximum)
        {
            EncounterDefinition definition = generator.EncounterDefinitions.SingleOrDefault(candidate =>
                candidate != null && candidate.EncounterId == encounterId);
            if (definition == null)
            {
                throw new InvalidOperationException($"Difficulty-1 is missing Encounter '{encounterId}'.");
            }

            Undo.RecordObject(definition, "Declare Difficulty-1 threat range");
            definition.ConfigureDeclaredThreat(minimum, maximum);
            EditorUtility.SetDirty(definition);
        }

        private static void ConfigureLayoutModifiers(FloorGenerator generator)
        {
            string[] obstacleLayouts = Week18Obstacle2Setup.Layouts.Select(layout => layout.TemplateId)
                .Concat(Week20Obstacle4Setup.Layouts.Select(layout => layout.TemplateId))
                .Concat(Week22Terrain0Setup.Layouts.Select(layout => layout.TemplateId))
                .Append(Week23Obstacle6Setup.TemplateId).ToArray();
            foreach (RoomTemplateDefinition template in generator.RoomTemplates.Where(template => template != null))
            {
                int modifier = obstacleLayouts.Contains(template.TemplateId, StringComparer.Ordinal)
                    ? ObstacleLayoutModifier
                    : 0;
                if (template.LayoutDifficultyModifier == modifier) continue;
                Undo.RecordObject(template, "Configure Difficulty-1 Layout modifier");
                template.ConfigureLayoutDifficultyModifier(modifier);
                EditorUtility.SetDirty(template);
            }
        }
    }
}
