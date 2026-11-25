using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Attribute-0 (docs/28 §11, user request 2026-11-23): five new enemies from the user's drawings. The old 불효자손
    // becomes 불효자손-광기 (same TestEnemy id) with less health. Attributes are a loose guide: 순수 balanced, 광기 strong
    // but frail, 우울 weak, 활발 weak but fast. Debuffs on the player do not exist yet, so 우울 applies none.
    // Re-running updates the same Prefabs and assets and keeps their GUIDs.
    public static class EnemyAttribute0Setup
    {
        public const int EncounterContentVersion = 26;
        public const string ArtFolder = "Assets/Art/Enemies/FairyKingdom/Attributes";
        public const string TemporaryShotPath = SansamoPureMotionSetup.ProjectilePath;
        public const float PixelsPerUnit = 500f;
        // scripts/split-attribute-enemies.py: ground 8 px above the canvas bottom, 580 px tall idle body.
        public const float RootPixelsAboveCanvasBottom = 8f + 290f;

        // Balance-1: 45 → 35.
        public const float MadnessBulhyojasonHealth = 35f;
        public const float PureHealth = 50f;
        public const float LivelyHealth = 40f;
        public const float GloomyBulhyojasonHealth = 25f;
        public const float ColdSansamoHealth = 30f;
        public const float GloomySansamoHealth = 25f;

        // The warning, lash endpoint and hit test share this reach.
        public const float RibbonReach = 1.8f;
        public const float RibbonHeight = 0.29f;
        public const float RibbonHalfWidth = 0.4f;
        public const float LeapHeight = 1.5f;
        public const float LeapAirDuration = 0.58f;
        public const float BoomerangWidthRatio = 0.2f;
        public const float FanSpreadDegrees = 24f;
        public const float HealRadius = 3.6f;
        public const float HealShare = 0.05f;

        public const string PurePrefab = "BulhyojasonPure";
        public const string LivelyPrefab = "BulhyojasonLively";
        public const string GloomyBulhyojasonPrefab = "BulhyojasonGloomy";
        public const string ColdSansamoPrefab = "SansamoCold";
        public const string GloomySansamoPrefab = "SansamoGloomy";

        public static readonly (string prefab, EncounterEnemyRole role)[] Enemies =
        {
            (PurePrefab, EncounterEnemyRole.RibbonLasher),
            (LivelyPrefab, EncounterEnemyRole.Leaper),
            (GloomyBulhyojasonPrefab, EncounterEnemyRole.FanShooter),
            (ColdSansamoPrefab, EncounterEnemyRole.BoomerangThrower),
            (GloomySansamoPrefab, EncounterEnemyRole.BurrowHealer),
        };

        public static string PrefabPath(string prefab) => $"Assets/Prefabs/{prefab}.prefab";

        // New enemies join only the slots whose role they share, at weights below the slot's main enemy.
        public static EncounterEnemyCandidate[] Extend(Density0Setup.SlotKind kind,
            EncounterEnemyCandidate[] candidates)
        {
            (EncounterEnemyRole role, int weight)[] added = kind switch
            {
                Density0Setup.SlotKind.PressureMelee => new[]
                {
                    (EncounterEnemyRole.RibbonLasher, 30), (EncounterEnemyRole.Leaper, 20),
                    (EncounterEnemyRole.BurrowHealer, 10),
                },
                Density0Setup.SlotKind.Chaser => new[]
                    { (EncounterEnemyRole.RibbonLasher, 30), (EncounterEnemyRole.Leaper, 20) },
                Density0Setup.SlotKind.PressureRear or Density0Setup.SlotKind.CrossfireRear => new[]
                    { (EncounterEnemyRole.FanShooter, 25), (EncounterEnemyRole.BoomerangThrower, 20) },
                _ => Array.Empty<(EncounterEnemyRole, int)>(),
            };
            return candidates.Concat(added.Select(entry => new EncounterEnemyCandidate(entry.role, entry.weight)))
                .ToArray();
        }

        [MenuItem("Trickal Fan Game/Beta/Setup Attribute-0 Enemies")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Attribute-0 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Density1Setup.EncounterContentVersion)
                throw new InvalidOperationException("Attribute-0 requires the completed Density-1 Game Scene.");

            SetChaserHealth();
            Sprite shot = AssetDatabase.LoadAssetAtPath<Sprite>(TemporaryShotPath);
            if (shot == null) throw new InvalidOperationException("Missing temporary shot " + TemporaryShotPath);
            BuildPure();
            BuildLively();
            BuildGloomyBulhyojason(shot);
            BuildColdSansamo(shot);
            BuildGloomySansamo();
            EnemyDefeatArtworkSetup.Apply();

            RoomDifficultyTable table = generator.DifficultyTable;
            Undo.RecordObject(table, "Configure Attribute-0 threat scores");
            table.Configure(Week19Difficulty1Setup.ThreatScores, table.TierBands.ToArray(),
                table.FloorRanges.ToArray(), table.DistanceWeights.ToArray());
            if (!table.TryValidate(out string tableError))
                throw new InvalidOperationException($"Attribute-0 difficulty table is invalid. {tableError}");
            EditorUtility.SetDirty(table);

            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(
                Week14Encounter2Setup.RosterPath);
            if (roster == null) throw new InvalidOperationException("Attribute-0 requires the enemy roster.");
            Undo.RecordObject(roster, "Configure Attribute-0 roster");
            HashSet<EncounterEnemyRole> roles = new(Enemies.Select(enemy => enemy.role));
            roster.Configure(roster.Bindings.Where(binding => !roles.Contains(binding.Role))
                .Concat(Enemies.Select(enemy => new EncounterEnemyPrefabBinding(enemy.role,
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(enemy.prefab))))).ToArray());
            if (!roster.TryValidate(out string rosterError))
                throw new InvalidOperationException($"Attribute-0 roster is invalid. {rosterError}");
            EditorUtility.SetDirty(roster);

            EncounterDefinition[] updated = Density0Setup.ConfigureEncounters(Week27Curve1Setup.Patterns, table);
            HashSet<string> updatedIds = new(updated.Select(definition => definition.EncounterId),
                StringComparer.Ordinal);
            EncounterDefinition[] definitions = generator.EncounterDefinitions
                .Where(definition => definition != null && !updatedIds.Contains(definition.EncounterId))
                .Concat(updated)
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();
            foreach (EncounterDefinition definition in definitions)
            {
                if (!definition.TryValidate(out string error) || !definition.TryValidateThreat(table, out error) ||
                    !table.TryValidateEncounterSize(definition, out error))
                    throw new InvalidOperationException($"Attribute-0 catalog is invalid. {error}");
            }

            Undo.RecordObject(generator, "Configure Attribute-0 Encounters");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            EditorUtility.SetDirty(generator);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Attribute-0 setup.");
            AssetDatabase.SaveAssets();
            Debug.Log($"Attribute-0 ready: {Enemies.Length} enemies, 불효자손-광기 health {MadnessBulhyojasonHealth}, " +
                      $"Encounter content version {generator.EncounterContentVersion}.");
        }

        public static void SetupAndVerifyBatch()
        {
            Setup();
            EnemyAttribute0Verification.Verify();
        }

        private static void SetChaserHealth()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Week15Enemy0Setup.ChaserPrefabPath);
            try
            {
                SerializedObject health = new(root.GetComponent<Health>());
                health.FindProperty("maxHealth").floatValue = MadnessBulhyojasonHealth;
                if (health.ApplyModifiedPropertiesWithoutUndo())
                    PrefabUtility.SaveAsPrefabAsset(root, Week15Enemy0Setup.ChaserPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BuildPure() => Build(PurePrefab, PureHealth, false, "Attack", root =>
        {
            root.AddComponent<EnemyRibbonLashController>().Configure(2.4f, 12f, RibbonReach, RibbonHeight,
                RibbonHalfWidth, 0.5f, 0.16f, 0.5f, 0.5f, EnemyDamageTier.Light,
                ImportSprite($"{ArtFolder}/{PurePrefab}_LashTail.png", centered: true));
            SetContact(root, EnemyDamageTier.Light, false);
        });

        private static void BuildLively() => Build(LivelyPrefab, LivelyHealth, false, "Attack", root =>
        {
            root.AddComponent<EnemyLeapController>().Configure(12f, 3.2f, 0.22f, LeapAirDuration, LeapHeight, 0.2f, 0.15f,
                0.45f, 0.85f, EnemyDamageTier.Light);
            // The jump's picture rises through the shared hop view; it adds no stride hop of its own.
            EnemyHopMotion hop = root.GetComponent<EnemyHopMotion>();
            if (hop == null) hop = root.AddComponent<EnemyHopMotion>();
            hop.Configure(0.7f, 0f);
            SetContact(root, EnemyDamageTier.Light, true);
        });

        private static void BuildGloomyBulhyojason(Sprite shot) => Build(GloomyBulhyojasonPrefab,
            GloomyBulhyojasonHealth, false, "Attack", root =>
            {
                EnemyFanShooterController shooter = root.AddComponent<EnemyFanShooterController>();
                shooter.ConfigureShooter(2.2f, 12f, 5f, 0.9f, 0.5f, 0.14f, 0.4f, 1.6f, 5.5f, EnemyDamageTier.Light, shot,
                    new Color(0.82f, 0.62f, 1f));
                shooter.ConfigureFan(FanSpreadDegrees, 1.6f);
                SetContact(root, EnemyDamageTier.Light, true);
            });

        private static void BuildColdSansamo(Sprite shot) => Build(ColdSansamoPrefab, ColdSansamoHealth, true,
            "Attack", root =>
            {
                EnemyBoomerangController thrower = root.AddComponent<EnemyBoomerangController>();
                thrower.ConfigureShooter(2.3f, 12f, 5f, 0.9f, 0.55f, 0.14f, 0.4f, 2f, 5f, EnemyDamageTier.Light, shot,
                    Color.white);
                thrower.ConfigureBoomerang(1.5f, 7.5f, BoomerangWidthRatio, 2.4f);
                SetContact(root, EnemyDamageTier.Light, true);
            });

        private static void BuildGloomySansamo() => Build(GloomySansamoPrefab, GloomySansamoHealth, true, null, root =>
        {
            root.AddComponent<EnemyBurrowHealerController>().Configure(2.2f, 14f, HealRadius, 1.8f, 0.4f, 3f, 3f, 0.5f,
                HealShare, Frames(GloomySansamoPrefab, "Burrow"), Frames(GloomySansamoPrefab, "Emerge"));
            SetContact(root, EnemyDamageTier.Light, true);
        });

        private static void Build(string prefab, float maxHealth, bool walks, string attackFrames,
            Action<GameObject> configure)
        {
            string path = PrefabPath(prefab);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) CreateFromChaser(prefab, path);
            Sprite idle = ImportSprite($"{ArtFolder}/{prefab}_Idle.png");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(EnemyMovementAnimationSetup.MaterialPath);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = prefab;
                Remove<EnemyRibbonLashController>(root);
                Remove<EnemyLeapController>(root);
                Remove<EnemyFanShooterController>(root);
                Remove<EnemyBoomerangController>(root);
                Remove<EnemyBurrowHealerController>(root);
                Remove<MeleeEnemyAttack>(root);
                Remove<EnemyChase>(root);
                root.GetComponent<SpriteRenderer>().sprite = idle;
                SerializedObject health = new(root.GetComponent<Health>());
                health.FindProperty("maxHealth").floatValue = maxHealth;
                health.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<EnemyMovementAnimator>().Configure(
                    walks ? EnemyMovementMotion.Walk : EnemyMovementMotion.Hop, material,
                    walks ? Frames(prefab, "Walk") : Array.Empty<Sprite>(), walks ? 1.05f : 1f, walks ? 0f : 0.075f,
                    idle);
                root.GetComponent<EnemyAttackArtwork>().Configure(idle,
                    attackFrames != null ? Frames(prefab, attackFrames) : Array.Empty<Sprite>());
                configure(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException("Could not save " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // An unpacked copy of the chaser, so later chaser tuning does not leak into it.
        private static void CreateFromChaser(string prefab, string path)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.ChaserPrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                instance.name = prefab;
                if (PrefabUtility.SaveAsPrefabAsset(instance, path) == null)
                    throw new InvalidOperationException("Could not create " + path);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static void SetContact(GameObject root, EnemyDamageTier tier, bool halfHeart)
        {
            ContactDamage contact = root.GetComponent<ContactDamage>();
            if (contact == null) contact = root.AddComponent<ContactDamage>();
            contact.Configure(tier, 1f);
            contact.ConfigureHalfHeart(halfHeart);
        }

        private static Sprite[] Frames(string prefab, string kind) => Enumerable.Range(0, 4)
            .Select(index => prefab == PurePrefab && kind == "Attack" && index == (int)EnemyAttackPhase.Active
                ? ImportSprite($"{ArtFolder}/{prefab}_LashBody.png")
                : ImportSprite($"{ArtFolder}/{prefab}_{kind}_{index}.png")).ToArray();

        private static Sprite ImportSprite(string path, bool centered = false)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing artwork " + path);
            Texture2D size = new(2, 2);
            float height;
            try
            {
                size.LoadImage(File.ReadAllBytes(path));
                height = size.height;
            }
            finally { Object.DestroyImmediate(size); }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, centered ? 0.5f : RootPixelsAboveCanvasBottom / height);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Could not import " + path);
            return sprite;
        }

        private static void Remove<T>(GameObject target) where T : Component
        {
            foreach (T component in target.GetComponents<T>()) Object.DestroyImmediate(component, true);
        }
    }
}
