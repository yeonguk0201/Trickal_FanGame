using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week15Enemy5Setup
    {
        public const int EncounterContentVersion = 5;
        public const string ArtFolder = "Assets/Art/Enemies/FairyKingdom";
        public const string BulhyojasonSpritePath = ArtFolder + "/Enemy_Bulhyojason_Idle.png";
        public const string SansamoSpritePath = ArtFolder + "/Enemy_Sansamo_Idle.png";
        public const string LowBloodSugarSpritePath = ArtFolder + "/Enemy_LowBloodSugarFairy_Idle.png";
        public const string HighBloodSugarSpritePath = ArtFolder + "/Enemy_HighBloodSugarFairy_Idle.png";

        public const string SmallEncounterId = "fairy-small-melee";
        public const string StandardEncounterId = "fairy-standard-pressure";
        public const string LargeEncounterId = "fairy-large-sniper-pressure";
        public const string SmallEncounterPath = Week14Encounter1Setup.EncounterFolder + "/" + SmallEncounterId + ".asset";
        public const string StandardEncounterPath = Week14Encounter1Setup.EncounterFolder + "/" + StandardEncounterId + ".asset";
        public const string LargeEncounterPath = Week14Encounter1Setup.EncounterFolder + "/" + LargeEncounterId + ".asset";

        private const float PixelsPerUnit = 1000f;

        [MenuItem("Trickal Fan Game/Week 15/Setup Enemy-5 Fairy Enemy Assets and Encounters")]
        public static void Setup()
        {
            if (!string.Equals(SceneManager.GetActiveScene().path, Week13FrontendSetup.GameScenePath,
                    StringComparison.Ordinal))
            {
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            }
            Week15Enemy2Setup.Setup();
            Week15Enemy3Setup.Setup();
            Week15Enemy4Setup.Setup();

            ConfigureSpriteImport(BulhyojasonSpritePath);
            ConfigureSpriteImport(SansamoSpritePath);
            ConfigureSpriteImport(LowBloodSugarSpritePath);
            ConfigureSpriteImport(HighBloodSugarSpritePath);

            AssignSprite(Week15Enemy2Setup.BulhyojasonPrefabPath, BulhyojasonSpritePath);
            AssignSprite(Week15Enemy2Setup.SansamoPrefabPath, SansamoSpritePath);
            AssignSprite(Week15Enemy0Setup.ChargingPrefabPath, LowBloodSugarSpritePath);
            AssignSprite(Week15Enemy4Setup.SniperPrefabPath, HighBloodSugarSpritePath);

            ConfigureRosterAndEncounters();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Week 15 Enemy-5 setup complete: four fairy-kingdom sprites are connected to their verified " +
                "behaviors; Small rooms exclude snipers, while Large rooms separate sniper and charge pressure " +
                "into different waves.");
        }

        private static void ConfigureSpriteImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Enemy-5 requires a PNG texture at {path}.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void AssignSprite(string prefabPath, string spritePath)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (sprite == null || contents == null)
            {
                throw new InvalidOperationException(
                    $"Enemy-5 could not load prefab '{prefabPath}' or sprite '{spritePath}'.");
            }

            try
            {
                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    throw new InvalidOperationException($"Enemy-5 prefab '{prefabPath}' requires a SpriteRenderer.");
                }

                renderer.sprite = sprite;
                renderer.color = Color.white;
                renderer.drawMode = SpriteDrawMode.Simple;
                if (PrefabUtility.SaveAsPrefabAsset(contents, prefabPath) == null)
                {
                    throw new InvalidOperationException($"Enemy-5 could not save prefab '{prefabPath}'.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureRosterAndEncounters()
        {
            GameObject bulhyojason = Load<GameObject>(Week15Enemy2Setup.BulhyojasonPrefabPath);
            GameObject sansamo = Load<GameObject>(Week15Enemy2Setup.SansamoPrefabPath);
            GameObject lowBloodSugar = Load<GameObject>(Week15Enemy0Setup.ChargingPrefabPath);
            GameObject highBloodSugar = Load<GameObject>(Week15Enemy4Setup.SniperPrefabPath);
            GameObject legacyRanged = Load<GameObject>(Week15Enemy0Setup.RangedPrefabPath);
            EncounterEnemyRoster roster = Load<EncounterEnemyRoster>(Week14Encounter2Setup.RosterPath);
            if (bulhyojason == null || sansamo == null || lowBloodSugar == null ||
                highBloodSugar == null || legacyRanged == null || roster == null)
            {
                throw new InvalidOperationException("Enemy-5 requires all four enemy prefabs and the Encounter roster.");
            }

            roster.Configure(new[]
            {
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.Chaser, bulhyojason),
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.FastChaser, sansamo),
                // Preserve completed Room-7/8 encounters. This is the shorter-range legacy ranged role,
                // not the Large-only high-blood-sugar sniper.
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.Ranged, legacyRanged),
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.Charging, lowBloodSugar),
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.Sniper, highBloodSugar),
            });
            EditorUtility.SetDirty(roster);

            RoomProfile small = Load<RoomProfile>(Week14Room3Setup.SmallProfilePath);
            RoomProfile basic = Load<RoomProfile>(Week14Room1Setup.BasicProfilePath);
            RoomProfile wide = Load<RoomProfile>(Week14Room3Setup.WideProfilePath);
            RoomProfile tall = Load<RoomProfile>(Week14Room6Setup.TallProfilePath);
            RoomProfile large = Load<RoomProfile>(Week14Room6Setup.LargeProfilePath);
            if (small == null || basic == null || wide == null || tall == null || large == null)
            {
                throw new InvalidOperationException("Enemy-5 requires all verified normal Room Profiles.");
            }

            EncounterDefinition smallEncounter = CreateOrUpdate(
                SmallEncounterPath,
                SmallEncounterId,
                new[] { small },
                new[]
                {
                    Wave(1,
                        Rule(EncounterEnemyRole.Chaser),
                        Rule(EncounterEnemyRole.FastChaser)),
                    Wave(2, Rule(EncounterEnemyRole.Charging)),
                });
            EncounterDefinition standardEncounter = CreateOrUpdate(
                StandardEncounterPath,
                StandardEncounterId,
                new[] { basic, wide, tall },
                new[]
                {
                    Wave(1,
                        Rule(EncounterEnemyRole.Chaser),
                        Rule(EncounterEnemyRole.FastChaser)),
                    Wave(2,
                        Rule(EncounterEnemyRole.Charging),
                        Rule(EncounterEnemyRole.FastChaser)),
                });
            EncounterDefinition largeEncounter = CreateOrUpdate(
                LargeEncounterPath,
                LargeEncounterId,
                new[] { large },
                new[]
                {
                    // The sniper and charger never overlap in one wave.
                    Wave(1,
                        Rule(EncounterEnemyRole.Chaser),
                        Rule(EncounterEnemyRole.Sniper)),
                    Wave(2,
                        Rule(EncounterEnemyRole.FastChaser),
                        Rule(EncounterEnemyRole.Charging)),
                });

            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null)
            {
                throw new InvalidOperationException("Enemy-5 requires the configured generator and assembler.");
            }

            EncounterDefinition[] definitions = generator.EncounterDefinitions
                .Concat(new[] { smallEncounter, standardEncounter, largeEncounter })
                .GroupBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();
            Undo.RecordObjects(new UnityEngine.Object[] { generator, assembler }, "Configure Enemy-5 content");
            generator.ConfigureEncounters(EncounterContentVersion, definitions);
            assembler.ConfigureEncounterRoster(roster);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
            {
                throw new InvalidOperationException("Game Scene save failed during Enemy-5 setup.");
            }
        }

        private static EncounterDefinition CreateOrUpdate(
            string path,
            string encounterId,
            RoomProfile[] profiles,
            EncounterWaveDefinition[] waves)
        {
            EncounterDefinition definition = Load<EncounterDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EncounterDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.Configure(
                encounterId,
                profiles,
                1,
                3,
                Week14Encounter1Setup.MinimumPlayerDistance,
                Week14Encounter1Setup.MinimumDoorDistance,
                waves,
                EncounterClearCondition.AllWavesCleared);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static EncounterWaveDefinition Wave(int number, params EncounterSpawnRule[] rules) =>
            new(number,
                number == 1
                    ? EncounterWaveStartCondition.RoomEntered
                    : EncounterWaveStartCondition.PreviousWaveCleared,
                EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated,
                rules);

        private static EncounterSpawnRule Rule(EncounterEnemyRole role) =>
            new(role, 1, null, "all");

        public static string[] SpritePaths() => new[]
        {
            BulhyojasonSpritePath,
            SansamoSpritePath,
            LowBloodSugarSpritePath,
            HighBloodSugarSpritePath,
        };

        public static string[] EncounterPaths() => new[]
        {
            SmallEncounterPath,
            StandardEncounterPath,
            LargeEncounterPath,
        };

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
