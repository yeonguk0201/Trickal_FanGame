using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week15Boss3Setup
    {
        public const string BossPrefabPath = "Assets/Prefabs/CrayonHeroBoss.prefab";
        public const string ArcherPrefabPath = "Assets/Prefabs/CrayonArcherMinion.prefab";
        public const string MagePrefabPath = "Assets/Prefabs/CrayonMageMinion.prefab";
        public const string AxePrefabPath = "Assets/Prefabs/CrayonAxeMinion.prefab";
        public const string ShieldPrefabPath = "Assets/Prefabs/CrayonShieldMinion.prefab";
        public const string BossSpritePath = Week15Boss1Setup.BossArtFolder + "/Boss_크레용사용.png";
        public const string ArcherSpritePath = Week15Boss1Setup.BossArtFolder + "/Boss_크레용사용_소환몹_궁병.png";
        public const string MageSpritePath = Week15Boss1Setup.BossArtFolder + "/Boss_크레용사용_소환몹_마법사.png";
        public const string AxeSpritePath = Week15Boss1Setup.BossArtFolder + "/Boss_크레용사용_소환몹_도끼병.png";
        public const string ShieldSpritePath = Week15Boss1Setup.BossArtFolder + "/Boss_크레용사용_소환몹_방패병.png";

        [MenuItem("Trickal Fan Game/Week 15/Setup Boss-3 Crayon Hero")]
        public static void Setup()
        {
            Week15Boss2Setup.Setup();
            ConfigureSpriteImport(BossSpritePath);
            ConfigureSpriteImport(ArcherSpritePath);
            ConfigureSpriteImport(MageSpritePath);
            ConfigureSpriteImport(AxeSpritePath);
            ConfigureSpriteImport(ShieldSpritePath);

            CreateVariantIfMissing(Week15Enemy4Setup.SniperPrefabPath, ArcherPrefabPath, "CrayonArcherMinion");
            CreateVariantIfMissing(Week15Enemy4Setup.SniperPrefabPath, MagePrefabPath, "CrayonMageMinion");
            CreateVariantIfMissing(Week15Enemy2Setup.SansamoPrefabPath, AxePrefabPath, "CrayonAxeMinion");
            CreateVariantIfMissing(Week15Enemy2Setup.SansamoPrefabPath, ShieldPrefabPath, "CrayonShieldMinion");
            ConfigureMinion(ArcherPrefabPath, ArcherSpritePath, "CrayonArcherMinion");
            ConfigureMinion(MagePrefabPath, MageSpritePath, "CrayonMageMinion");
            ConfigureMinion(AxePrefabPath, AxeSpritePath, "CrayonAxeMinion");
            ConfigureMinion(ShieldPrefabPath, ShieldSpritePath, "CrayonShieldMinion");

            EnsureBossPrefabExists();
            ConfigureBossPrefab();
            ConfigureFloorBossBinding();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Week 15 Boss-3 setup complete: Crayon Hero uses a readable map slash, capped " +
                      "four-role summons, a forward sword hitbox after approach, and a one-time golden phase.");
        }

        private static void ConfigureBossPrefab()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                contents.name = "CrayonHeroBoss";
                RemoveIfPresent<BuseureogiBossPatternRuntime>(contents);
                RemoveIfPresent<SaemaeumVaultBossPatternRuntime>(contents);
                RemoveIfPresent<EnemyAttackPresentation>(contents);

                SpriteRenderer renderer = EnsureVisualChild(contents);
                renderer.sprite = Load<Sprite>(BossSpritePath);
                renderer.color = Color.white;
                contents.transform.localScale = new Vector3(
                    Week15Boss1Setup.BossScale, Week15Boss1Setup.BossScale, 1f);

                CircleCollider2D collider = contents.GetComponent<CircleCollider2D>();
                collider.radius = 0.38f;
                Rigidbody2D body = contents.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.useFullKinematicContacts = true;
                body.mass = 30f;
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                ContactDamage contact = contents.GetComponent<ContactDamage>();
                if (contact == null) contact = contents.AddComponent<ContactDamage>();
                contact.Configure(EnemyDamageTier.Heavy, 0.8f);

                SetFloat(contents.GetComponent<Health>(), "maxHealth", 1100f);
                BossController boss = contents.GetComponent<BossController>();
                boss.ConfigureHud("크레용사용", 2);
                boss.ConfigurePhaseTwo(1.25f, 0.78f);
                boss.ConfigurePatterns(new[]
                {
                    new BossPatternDefinition("crayon-map-cleaving-slash",
                        BossPatternExecution.CrayonHeroMapSlash, 1.05f, 0.32f, 1.05f, 0f),
                    new BossPatternDefinition("crayon-summon-soldiers",
                        BossPatternExecution.CrayonHeroSummonMinions, 0.425f, 0.1f, 0.4f, 0f),
                    new BossPatternDefinition("crayon-approach-sword-swing",
                        BossPatternExecution.CrayonHeroApproachSwing, 0.05f, 2.2f, 0.2f, 0.2f),
                    new BossPatternDefinition("crayon-short-dash-chain",
                        BossPatternExecution.CrayonHeroDashChain, 0.2f, 0.75f, 0.3f, 0.45f),
                });

                CrayonHeroBossPatternRuntime runtime = contents.GetComponent<CrayonHeroBossPatternRuntime>();
                if (runtime == null) runtime = contents.AddComponent<CrayonHeroBossPatternRuntime>();
                runtime.ConfigureSlash(18f, 1.15f, 0.2f, EnemyDamageTier.Critical);
                runtime.ConfigureSummons(new[]
                {
                    Load<GameObject>(ArcherPrefabPath),
                    Load<GameObject>(MagePrefabPath),
                    Load<GameObject>(AxePrefabPath),
                    Load<GameObject>(ShieldPrefabPath),
                }, 4, 2.5f, 0.15f);
                runtime.ConfigureSwing(2.5f, 1.35f, 2.4f, 3.2f, 0.15f, 0.3f, 0.7f, 0.12f, EnemyDamageTier.Heavy);
                runtime.ConfigureDash(15f, 0.17f, 0.1f, 1.8f, EnemyDamageTier.Heavy);
                runtime.ConfigureSelection(3, 9f, 7.5f, 45, 30, 10, 15);

                if (PrefabUtility.SaveAsPrefabAsset(contents, BossPrefabPath) == null)
                    throw new InvalidOperationException("Could not save the Crayon Hero boss prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void EnsureBossPrefabExists()
        {
            if (Load<GameObject>(BossPrefabPath) != null) return;
            CreateVariantIfMissing(Week15Boss0Setup.BossPrefabPath, BossPrefabPath, "CrayonHeroBoss");
        }

        private static void CreateVariantIfMissing(string sourcePath, string destinationPath, string objectName)
        {
            if (Load<GameObject>(destinationPath) != null) return;
            GameObject source = Load<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException($"Missing source prefab at {sourcePath}.");
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) throw new InvalidOperationException($"Could not instantiate {sourcePath}.");
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                instance.name = objectName;
                if (PrefabUtility.SaveAsPrefabAsset(instance, destinationPath) == null)
                    throw new InvalidOperationException($"Could not create prefab at {destinationPath}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ConfigureMinion(string prefabPath, string spritePath, string objectName)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                contents.name = objectName;
                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                if (renderer == null) throw new InvalidOperationException($"{prefabPath} requires a SpriteRenderer.");
                renderer.sprite = Load<Sprite>(spritePath);
                renderer.color = Color.white;
                renderer.drawMode = SpriteDrawMode.Simple;
                contents.GetComponent<TestEnemy>()?.ConfigureReward(false);
                if (PrefabUtility.SaveAsPrefabAsset(contents, prefabPath) == null)
                    throw new InvalidOperationException($"Could not save minion prefab at {prefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureSpriteImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException($"Boss-3 requires a PNG at {path}.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1000f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void ConfigureFloorBossBinding()
        {
            if (!string.Equals(SceneManager.GetActiveScene().path, Week13FrontendSetup.GameScenePath,
                    StringComparison.Ordinal))
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>(
                FindObjectsInactive.Include);
            GameObject buseureogi = Load<GameObject>(Week15Boss0Setup.BossPrefabPath);
            GameObject saemaeum = Load<GameObject>(Week15Boss2Setup.BossPrefabPath);
            GameObject crayonHero = Load<GameObject>(BossPrefabPath);
            if (assembler == null || buseureogi == null || saemaeum == null || crayonHero == null)
                throw new InvalidOperationException("Boss-3 could not bind the three floor boss prefabs.");
            Undo.RecordObject(assembler, "Bind floor-three Crayon Hero boss");
            assembler.ConfigureFloorBossPrefabs(new[] { buseureogi, saemaeum, crayonHero });
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Could not save the floor-three Crayon Hero binding.");
        }

        private static SpriteRenderer EnsureVisualChild(GameObject contents)
        {
            Transform visual = contents.transform.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(contents.transform, false);
            }
            SpriteRenderer child = visual.GetComponent<SpriteRenderer>();
            if (child == null) child = visual.gameObject.AddComponent<SpriteRenderer>();
            SpriteRenderer root = contents.GetComponent<SpriteRenderer>();
            if (root != null)
            {
                child.sharedMaterial = root.sharedMaterial;
                child.sortingLayerID = root.sortingLayerID;
                child.sortingOrder = root.sortingOrder;
                UnityEngine.Object.DestroyImmediate(root);
            }
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            return child;
        }

        private static void RemoveIfPresent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null) UnityEngine.Object.DestroyImmediate(component);
        }

        private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
