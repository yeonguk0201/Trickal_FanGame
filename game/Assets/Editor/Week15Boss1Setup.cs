using System;
using TrickalFanGame.Character;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week15Boss1Setup
    {
        public const string BossArtFolder = "Assets/Art/Bosses/FairyKingdom";
        public const string CharacterArtFolder = "Assets/Art/Characters/Player";
        public const string BossSpritePath = BossArtFolder + "/Boss_Buseureogi.png";
        public const string CreamSpritePath = BossArtFolder + "/Boss_Buseureogi_CreamObstacle.png";
        public const string DoughSpritePath = BossArtFolder + "/Boss_Buseureogi_DoughObstacle.png";
        public const string ErpinSpritePath = CharacterArtFolder + "/Character_Erpin.png";
        public const string MinionPrefabPath = "Assets/Prefabs/BuseureogiCrumbMinion.prefab";
        public const string CreamPrefabPath = "Assets/Prefabs/BuseureogiCreamObstacle.prefab";
        public const string DoughPrefabPath = "Assets/Prefabs/BuseureogiDoughObstacle.prefab";
        public const float BossScale = Week15Boss0Setup.BossVisualScale * 2f;

        public static readonly Vector2[] MinionOffsets =
        {
            new(1.8f, 0f), new(1.5588f, 0.9f), new(0.9f, 1.5588f), new(0f, 1.8f),
            new(-0.9f, 1.5588f), new(-1.5588f, 0.9f), new(-1.8f, 0f), new(-1.5588f, -0.9f),
            new(-0.9f, -1.5588f), new(0f, -1.8f), new(0.9f, -1.5588f), new(1.5588f, -0.9f),
        };

        public static readonly Vector2[] ObstacleOffsets =
        {
            new(-3.7f, -2.15f), new(3.7f, -2.15f), new(-3.7f, 2.15f), new(3.7f, 2.15f),
        };

        [MenuItem("Trickal Fan Game/Week 15/Setup Boss-1 Buseureogi")]
        public static void Setup()
        {
            if (!string.Equals(SceneManager.GetActiveScene().path, Week13FrontendSetup.GameScenePath,
                    StringComparison.Ordinal))
            {
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            }

            Week15Boss0Setup.Setup();
            ConfigureSpriteImport(BossSpritePath);
            ConfigureSpriteImport(CreamSpritePath);
            ConfigureSpriteImport(DoughSpritePath);
            ConfigureSpriteImport(ErpinSpritePath);

            Sprite bossSprite = Load<Sprite>(BossSpritePath);
            GameObject minion = CreateOrUpdateMinion(bossSprite);
            GameObject cream = CreateOrUpdateObstacle(CreamPrefabPath, Load<Sprite>(CreamSpritePath), "Cream");
            GameObject dough = CreateOrUpdateObstacle(DoughPrefabPath, Load<Sprite>(DoughSpritePath), "Dough");
            ConfigureBoss(bossSprite, minion, cream, dough);
            ConfigureErpin(Load<Sprite>(ErpinSpritePath));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Week 15 Boss-1 setup complete: Buseureogi uses approach volleys, four nearby crumb summons, " +
                      "three no-SP destructible obstacles, a faster seeded-random phase two, and direction-aware Erpin art.");
        }

        private static void ConfigureSpriteImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException($"Boss-1 requires a PNG at {path}.");
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

        private static GameObject CreateOrUpdateMinion(Sprite sprite)
        {
            GameObject source = Load<GameObject>(Week15Enemy2Setup.BulhyojasonPrefabPath);
            if (source == null || sprite == null) throw new InvalidOperationException("Boss-1 minion source is missing.");
            if (Load<GameObject>(MinionPrefabPath) == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
                if (instance == null) throw new InvalidOperationException("Could not instantiate crumb minion source.");
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "BuseureogiCrumbMinion";
                if (PrefabUtility.SaveAsPrefabAsset(instance, MinionPrefabPath) == null)
                    throw new InvalidOperationException("Could not create crumb minion prefab.");
                UnityEngine.Object.DestroyImmediate(instance);
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(MinionPrefabPath);
            try
            {
                contents.name = "BuseureogiCrumbMinion";
                contents.transform.localScale = Vector3.one * 0.72f;
                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = Color.white;
                SetFloat(contents.GetComponent<Health>(), "maxHealth", 30f);
                contents.GetComponent<EnemyChase>()?.Configure(2.8f, 20f, 0.7f);
                contents.GetComponent<ContactDamage>()?.Configure(EnemyDamageTier.Light, 1f);
                contents.GetComponent<MeleeEnemyAttack>()?.Configure(1.05f, 0.35f, 0.1f, 0.55f, 1f, EnemyDamageTier.Light);
                if (PrefabUtility.SaveAsPrefabAsset(contents, MinionPrefabPath) == null)
                    throw new InvalidOperationException("Could not save crumb minion prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return Load<GameObject>(MinionPrefabPath);
        }

        private static GameObject CreateOrUpdateObstacle(string path, Sprite sprite, string label)
        {
            if (sprite == null) throw new InvalidOperationException($"Missing {label} obstacle sprite.");
            if (Load<GameObject>(path) == null)
            {
                GameObject created = new($"Buseureogi{label}Obstacle");
                created.layer = LayerMask.NameToLayer("Enemy");
                created.AddComponent<SpriteRenderer>();
                Rigidbody2D body = created.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                created.AddComponent<CircleCollider2D>();
                created.AddComponent<Health>();
                created.AddComponent<TestEnemy>();
                if (PrefabUtility.SaveAsPrefabAsset(created, path) == null)
                    throw new InvalidOperationException($"Could not create {label} obstacle prefab.");
                UnityEngine.Object.DestroyImmediate(created);
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.layer = LayerMask.NameToLayer("Enemy");
                contents.transform.localScale = Vector3.one;
                SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = Color.white;
                CircleCollider2D collider = contents.GetComponent<CircleCollider2D>();
                collider.radius = 0.42f;
                collider.isTrigger = false;
                collider.includeLayers = LayerMask.GetMask("Enemy");
                collider.excludeLayers = 0;
                collider.layerOverridePriority = 1;
                SetFloat(contents.GetComponent<Health>(), "maxHealth", 50f);
                TestEnemy obstacle = contents.GetComponent<TestEnemy>();
                obstacle.ConfigureReward(false);
                obstacle.ConfigureBossCollision(true);
                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                    throw new InvalidOperationException($"Could not save {label} obstacle prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return Load<GameObject>(path);
        }

        private static void ConfigureBoss(Sprite sprite, GameObject minion, params GameObject[] obstacles)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(Week15Boss0Setup.BossPrefabPath);
            try
            {
                contents.name = "BuseureogiBoss";
                contents.transform.localScale = new Vector3(BossScale, BossScale, 1f);
                SpriteRenderer renderer = contents.GetComponentInChildren<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = Color.white;
                BossController boss = contents.GetComponent<BossController>();
                Rigidbody2D body = contents.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.mass = 25f;
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                ContactDamage contactDamage = contents.GetComponent<ContactDamage>();
                if (contactDamage == null) contactDamage = contents.AddComponent<ContactDamage>();
                contactDamage.Configure(EnemyDamageTier.Heavy, 0.8f);
                boss.ConfigureHud("부스러기", 2);
                boss.ConfigurePhaseTwo(1.25f, 0.72f);
                boss.SetProjectileDamageTier(EnemyDamageTier.Medium);
                boss.ConfigurePatterns(new[]
                {
                    new BossPatternDefinition("buseureogi-approach-volley",
                        BossPatternExecution.BuseureogiApproachVolley, 0.6f, 1.7f, 0.75f, 0.8f),
                    new BossPatternDefinition("buseureogi-summon-crumbs",
                        BossPatternExecution.BuseureogiSummonMinions, 0.9f, 0.15f, 0.85f, 4.2f),
                    new BossPatternDefinition("buseureogi-throw-obstacle",
                        BossPatternExecution.BuseureogiThrowObstacle, 0.85f, 0.15f, 0.7f, 2.6f),
                });
                BuseureogiBossPatternRuntime runtime = contents.GetComponent<BuseureogiBossPatternRuntime>();
                if (runtime == null) runtime = contents.AddComponent<BuseureogiBossPatternRuntime>();
                runtime.Configure(minion, obstacles, MinionOffsets, ObstacleOffsets);
                runtime.ConfigureLimits(4, 3);
                runtime.ConfigureObstacleImpact(1.1f, EnemyDamageTier.Light, 7f, 0.2f);
                runtime.ConfigureMovement(2.25f, 1.35f);
                if (PrefabUtility.SaveAsPrefabAsset(contents, Week15Boss0Setup.BossPrefabPath) == null)
                    throw new InvalidOperationException("Could not save the Buseureogi boss prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureErpin(Sprite sprite)
        {
            if (sprite == null) throw new InvalidOperationException("Missing Erpin sprite.");
            PlayerMovement player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            SpriteRenderer renderer = player != null ? player.GetComponent<SpriteRenderer>() : null;
            if (renderer == null) throw new InvalidOperationException("Game Scene Player requires a SpriteRenderer.");
            if (player.GetComponent<KnockbackReceiver>() == null)
                Undo.AddComponent<KnockbackReceiver>(player.gameObject);
            Undo.RecordObject(renderer, "Assign Erpin sprite");
            renderer.sprite = sprite;
            renderer.color = Color.white;
            EditorUtility.SetDirty(renderer);

            CharacterDefinition erpin = Load<CharacterDefinition>(Week13FrontendSetup.CharacterFolder + "/erpin.asset");
            if (erpin == null) throw new InvalidOperationException("Missing Erpin CharacterDefinition.");
            SerializedObject serialized = new(erpin);
            serialized.FindProperty("portrait").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(erpin);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Could not save Erpin sprite binding in the Game Scene.");

            Week13FrontendSetup.Setup();
        }

        private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
