using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Enemy-12 (user decisions, 2026-10-24 and 2026-10-30, docs/28 §8.3): 곡사형 stays at the back and lobs a
    // projectile at the spot the player stood on. The projectile arcs over obstacles, shows its landing circle for
    // the whole flight and on landing hurts only the player, at the medium tier of the floor. It first appears on
    // floor 3, scores threat 4 (like the sniper) and joins crossfire's three rear slots. It shows the user's
    // 저혈당 요정-광기 drawing (2026-10-30) as a still picture; motion comes later. Re-running updates the same Prefab
    // and assets and keeps their GUIDs.
    public static class Week27Enemy12Setup
    {
        public const int EncounterContentVersion = 19;
        public const string PrefabPath = "Assets/Prefabs/LobbingFairy.prefab";
        public const string PrefabName = "LobbingFairy";
        // The user's drawing is named 저혈당 요정-광기 (2026-10-30).
        public const string DisplayName = "저혈당 요정-광기";
        public const string ArtworkId = "low-blood-sugar-fairy-madness";
        public const string ArtworkPath = FairyKingdomArtworkSetup.ArtRoot + "/Enemies/" + ArtworkId + ".png";
        // The same canvas and body as 저혈당 요정-활발, so the same width stands about 1.2 tall.
        public const float ArtworkWidth = 1.07f;
        // The user's throw motion and crowbar (2026-10-30). The motion sheet was repacked into an even 2x2 atlas
        // (800 px cells) with one foot line, because the drawn frames crossed the sheet's midlines.
        public const string MotionAtlasPath = Week15Enemy5Setup.ArtFolder + "/Walking/LowBloodSugarMadness_Motion.png";
        public const string ProjectilePath = EnemyAttackAnimationSetup.Folder + "/LowBloodSugarMadnessCrowbar_Projectile.png";
        // The crowbar leaves the hand about this high above the root (the drawing's middle).
        public const float LaunchHeight = 0.2f;

        // Implementation defaults (2026-10-30): a slow back-line thrower, sturdier than 산사모-순수 is quick.
        public const float MaxHealth = 30f;
        public const float KnockbackWeight = 1f;
        public const float MoveSpeed = 2f;
        public const float DetectionRange = 10f;
        public const float MinimumAttackDistance = 3.5f;
        public const float MaximumAttackDistance = 7f;
        public const float AttackInterval = 2.4f;
        public const float WindUpDuration = 0.4f;
        public const float FlightDuration = 1f;
        public const float ArcHeight = 2.2f;
        public const float LandingRadius = 0.75f;

        // User decision (2026-10-30).
        public const EnemyDamageTier DamageTier = EnemyDamageTier.Medium;

        public const int Threat = 4;
        public const int RearWeight = 20;

        // Crossfire rear slots: Spawn-2's per-profile 산사모-순수/sniper weights plus 곡사형.
        public static EncounterEnemyCandidate[] CrossfireRearCandidates(Week19Spawn2Setup.ProfileSpec spec) => new[]
        {
            new EncounterEnemyCandidate(EncounterEnemyRole.Ranged, spec.QuickRangedWeight),
            new EncounterEnemyCandidate(EncounterEnemyRole.Sniper, spec.SniperWeight),
            new EncounterEnemyCandidate(EncounterEnemyRole.Lobber, RearWeight),
        };

        public static bool IsCrossfire(EncounterDefinition definition) =>
            definition != null && IsCrossfire(definition.EncounterId);

        public static bool IsCrossfire(string encounterId) =>
            encounterId != null && encounterId.EndsWith("-crossfire-v1", StringComparison.Ordinal);

        [MenuItem("Trickal Fan Game/Week 27/Setup Enemy-12 Lobbing Enemy")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Enemy-12 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Week27Enemy11Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Enemy-12 requires the completed Enemy-11 Game Scene.");
            }

            GameObject prefab = EnsurePrefab();
            RoomDifficultyTable table = ConfigureThreat(generator.DifficultyTable);
            EncounterEnemyRoster roster = ConfigureRoster(prefab);

            // Spawn-2 owns pressure and crossfire and reads the Enemy-12 rear list. Pressure's declared range is
            // restored as Enemy-9/10 do; crossfire's is kept by the definition (threat 4 leaves it at 15~19).
            EncounterDefinition[] updated = Week19Spawn2Setup.ConfigureEncounters();
            foreach (EncounterDefinition definition in updated.Where(Week25Enemy9Setup.IsPressure))
                definition.ConfigureDeclaredThreat(Week19Difficulty1Setup.PressureMinimumThreat,
                    Week19Difficulty1Setup.PressureMaximumThreat);
            foreach (EncounterDefinition definition in updated.Where(IsCrossfire))
                definition.ConfigureDeclaredThreat(Week19Difficulty1Setup.CrossfireMinimumThreat,
                    Week19Difficulty1Setup.CrossfireMaximumThreat);
            HashSet<string> updatedIds = new(updated.Select(definition => definition.EncounterId),
                StringComparer.Ordinal);
            EncounterDefinition[] definitions = generator.EncounterDefinitions
                .Where(definition => definition != null && !updatedIds.Contains(definition.EncounterId))
                .Concat(updated)
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();
            foreach (EncounterDefinition definition in definitions)
            {
                if (!definition.TryValidate(out string error) ||
                    !definition.TryValidateThreat(table, out error) ||
                    !table.TryValidateEncounterSize(definition, out error))
                {
                    throw new InvalidOperationException($"Enemy-12 catalog is invalid. {error}");
                }

                EditorUtility.SetDirty(definition);
            }

            Undo.RecordObjects(new Object[] { generator, assembler }, "Configure Enemy-12 Encounters");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            assembler.ConfigureEncounterRoster(roster);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Enemy-12 setup.");

            AssetDatabase.SaveAssets();
            Debug.Log($"Enemy-12 ready: {DisplayName} ({PrefabName}) has {MaxHealth} health and keeps " +
                      $"{MinimumAttackDistance}~{MaximumAttackDistance} from the player; every {AttackInterval} s it " +
                      $"winds up {WindUpDuration} s and lobs over obstacles to the player's spot ({FlightDuration} s " +
                      $"flight, landing radius {LandingRadius}, {DamageTier} damage to the player only). Threat " +
                      $"{Threat}, floor {EncounterEnemyFloorPool.IntroductionFloor(EncounterEnemyRole.Lobber)}+, " +
                      $"crossfire rear weight {RearWeight}, Encounter content version " +
                      $"{generator.EncounterContentVersion}.");
        }

        public static string[] CreatedAssetPaths() =>
            new[] { PrefabPath, FairyKingdomArtworkSetup.DisplayRoot + "/" + ArtworkId + ".prefab" };

        // The ready pose (top-left cell) fixes the scale and ground line of every frame.
        public static Rect ImportMotionAtlas(out Texture2D texture)
        {
            AssetDatabase.ImportAsset(MotionAtlasPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(MotionAtlasPath) is not TextureImporter importer)
                throw new InvalidOperationException("Missing Enemy-12 motion atlas: " + MotionAtlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MotionAtlasPath);
            if (texture == null || texture.width != texture.height || texture.width % 2 != 0)
                throw new InvalidOperationException("Enemy-12 motion must be a square 2x2 atlas.");
            Texture2D pixels = new(2, 2);
            Rect neutral = default;
            try
            {
                pixels.LoadImage(File.ReadAllBytes(MotionAtlasPath));
                int cell = pixels.width / 2;
                Color32[] colors = pixels.GetPixels32();
                for (int frame = 0; frame < 4; frame++)
                {
                    int originX = frame % 2 * cell, originY = (1 - frame / 2) * cell;
                    int minX = cell, minY = cell, maxX = -1, maxY = -1;
                    for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                    {
                        if (colors[(originY + y) * pixels.width + originX + x].a < 20) continue;
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
                    if (maxX < minX || minX <= 0 || minY <= 0 || maxX >= cell - 1 || maxY >= cell - 1)
                        throw new InvalidOperationException($"Enemy-12 motion frame {frame} is empty or clips its cell.");
                    if (frame == 0)
                        neutral = new Rect(minX / (float)cell, minY / (float)cell, (maxX - minX + 1f) / cell,
                            (maxY - minY + 1f) / cell);
                }
            }
            finally { Object.DestroyImmediate(pixels); }
            return neutral;
        }

        // Imported like the other enemy projectiles; the lob scales it to its drawn size.
        public static Sprite ImportProjectile()
        {
            AssetDatabase.ImportAsset(ProjectilePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(ProjectilePath) is not TextureImporter importer)
                throw new InvalidOperationException("Missing Enemy-12 projectile: " + ProjectilePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = EnemyAttackAnimationSetup.ProjectilePixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectilePath);
            if (sprite == null) throw new InvalidOperationException("Could not import " + ProjectilePath);
            return sprite;
        }

        public static GameObject EnsurePrefab()
        {
            GameObject contents = LoadFromRanged();
            try
            {
                contents.transform.localScale = Vector3.one;
                // The straight shooter's controller is replaced by the thrower.
                Remove<RangedEnemyController>(contents);
                Remove<EnemyAttackArtwork>(contents);
                Remove<EnemyMovementAnimator>(contents);
                Remove<ContactDamage>(contents);

                Health health = contents.GetComponent<Health>();
                SerializedObject serializedHealth = new(health);
                serializedHealth.FindProperty("maxHealth").floatValue = MaxHealth;
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();
                contents.GetComponent<KnockbackReceiver>().ConfigureWeight(KnockbackWeight);
                if (contents.GetComponent<EnemyBehaviorContext>() == null)
                    contents.AddComponent<EnemyBehaviorContext>();

                LobbingEnemyController controller = contents.GetComponent<LobbingEnemyController>();
                if (controller == null) controller = contents.AddComponent<LobbingEnemyController>();
                controller.Configure(MoveSpeed, DetectionRange, MinimumAttackDistance, MaximumAttackDistance,
                    AttackInterval, WindUpDuration, FlightDuration, ArcHeight, LandingRadius, DamageTier);

                AssetDatabase.ImportAsset(ArtworkPath, ImportAssetOptions.ForceSynchronousImport);
                FairyKingdomArtworkCatalog catalog = FairyKingdomArtworkSetup.RefreshCatalog();
                FairyKingdomArtworkSetup.BuildDisplayPrefab(catalog, ArtworkId);
                if (!FairyKingdomArtworkSetup.BindPrefabContents(contents, catalog))
                    throw new InvalidOperationException($"Enemy-12 could not bind '{ArtworkId}' to {PrefabPath}.");
                // Binding keeps a width already on the Prefab, so the placeholder's width is replaced here.
                FairyKingdomArtworkView view = contents.GetComponent<FairyKingdomArtworkView>();
                if (view.ArtworkId != ArtworkId || !Mathf.Approximately(view.LocalWidth, ArtworkWidth))
                    view.Configure(ArtworkId, view.Mode, view.Visual, ArtworkWidth);

                controller.ConfigureArtwork(ImportProjectile(), LaunchHeight);
                Rect content = ImportMotionAtlas(out Texture2D atlas);
                LobbingFairyMotion motion = contents.GetComponent<LobbingFairyMotion>();
                if (motion == null) motion = contents.AddComponent<LobbingFairyMotion>();
                motion.Configure(atlas, content, ArtworkId);
                if (PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath) == null)
                    throw new InvalidOperationException($"Could not save {PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        // Starts as an unpacked copy of the ranged enemy, so later ranged tuning does not leak into it.
        private static GameObject LoadFromRanged()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.RangedPrefabPath);
                if (source == null)
                    throw new InvalidOperationException($"Enemy-12 needs {Week15Enemy0Setup.RangedPrefabPath}.");
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                    instance.name = PrefabName;
                    if (PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath) == null)
                        throw new InvalidOperationException($"Could not create {PrefabPath}.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            contents.name = PrefabName;
            return contents;
        }

        private static RoomDifficultyTable ConfigureThreat(RoomDifficultyTable table)
        {
            Undo.RecordObject(table, "Configure Enemy-12 threat score");
            table.Configure(Week19Difficulty1Setup.ThreatScores, table.TierBands.ToArray(),
                table.FloorRanges.ToArray(), table.DistanceWeights.ToArray());
            if (!table.TryValidate(out string error) ||
                !table.TryGetThreat(EncounterEnemyRole.Lobber, out int threat) || threat != Threat)
            {
                throw new InvalidOperationException($"Enemy-12 difficulty table is invalid. {error}");
            }

            EditorUtility.SetDirty(table);
            return table;
        }

        private static EncounterEnemyRoster ConfigureRoster(GameObject prefab)
        {
            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(
                Week14Encounter2Setup.RosterPath);
            if (roster == null) throw new InvalidOperationException("Enemy-12 requires the enemy roster.");

            Undo.RecordObject(roster, "Configure Enemy-12 enemy roster");
            roster.Configure(roster.Bindings.Where(binding => binding.Role != EncounterEnemyRole.Lobber)
                .Append(new EncounterEnemyPrefabBinding(EncounterEnemyRole.Lobber, prefab)).ToArray());
            if (!roster.TryValidate(out string error))
                throw new InvalidOperationException($"Enemy-12 enemy roster is invalid. {error}");
            EditorUtility.SetDirty(roster);
            return roster;
        }

        private static void Remove<T>(GameObject target) where T : Component
        {
            foreach (T component in target.GetComponents<T>()) Object.DestroyImmediate(component, true);
        }
    }
}
