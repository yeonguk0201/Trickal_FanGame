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
    public static class Week15Boss2Setup
    {
        public const string BossPrefabPath = "Assets/Prefabs/SaemaeumVaultBoss.prefab";
        public const string BossSpritePath = Week15Boss1Setup.BossArtFolder + "/Boss_SaemaeumGeumgo.png";

        [MenuItem("Trickal Fan Game/Week 15/Setup Boss-2 Saemaeum Vault")]
        public static void Setup()
        {
            Week15Boss1Setup.Setup();
            ConfigureSpriteImport();
            EnsurePrefabExists();

            GameObject contents = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                contents.name = "SaemaeumVaultBoss";
                BuseureogiBossPatternRuntime oldRuntime = contents.GetComponent<BuseureogiBossPatternRuntime>();
                if (oldRuntime != null) UnityEngine.Object.DestroyImmediate(oldRuntime);
                ContactDamage contact = contents.GetComponent<ContactDamage>();
                if (contact == null) contact = contents.AddComponent<ContactDamage>();
                contact.Configure(2f, 0.8f);

                SpriteRenderer renderer = EnsureVisualChild(contents);
                renderer.sprite = Load<Sprite>(BossSpritePath);
                renderer.color = Color.white;
                contents.transform.localScale = new Vector3(
                    Week15Boss1Setup.BossScale, Week15Boss1Setup.BossScale, 1f);
                EnemyAttackPresentation commonPresentation = contents.GetComponent<EnemyAttackPresentation>();
                if (commonPresentation != null) UnityEngine.Object.DestroyImmediate(commonPresentation);

                CircleCollider2D collider = contents.GetComponent<CircleCollider2D>();
                collider.radius = 0.38f;
                Rigidbody2D body = contents.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.useFullKinematicContacts = true;
                body.mass = 30f;
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;

                SetFloat(contents.GetComponent<Health>(), "maxHealth", 80f);
                BossController boss = contents.GetComponent<BossController>();
                boss.ConfigureHud("새마음금고", 2);
                boss.ConfigurePhaseTwo(1.1f, 0.88f);
                boss.SetProjectileDamage(1.5f);
                boss.ConfigurePatterns(new[]
                {
                    new BossPatternDefinition("saemaeum-approach-treasure-throw",
                        BossPatternExecution.SaemaeumApproachThrow, 0.55f, 3.0f, 0.75f, 0.8f),
                    new BossPatternDefinition("saemaeum-rage-jump-sequence",
                        BossPatternExecution.SaemaeumJumpSequence, 0.95f, 3.8f, 1.15f, 1.8f),
                    new BossPatternDefinition("saemaeum-eat-treasure",
                        BossPatternExecution.SaemaeumTreasureHeal, 0.8f, 1.8f, 1.05f, 5.5f),
                });

                SaemaeumVaultBossPatternRuntime runtime =
                    contents.GetComponent<SaemaeumVaultBossPatternRuntime>();
                if (runtime == null) runtime = contents.AddComponent<SaemaeumVaultBossPatternRuntime>();
                runtime.ConfigureMovement(1.8f, 1.35f, 0.55f, 5.5f);
                runtime.ConfigureVolley(5, 5, 48f);
                runtime.ConfigureJumps(3, 5, 4, 6, 4.8f, 2.875f, 2f, 7f, 0.2f, 0.69f, 0.62f);
                runtime.ConfigureHealing(15f, 4);

                if (PrefabUtility.SaveAsPrefabAsset(contents, BossPrefabPath) == null)
                    throw new InvalidOperationException("Could not save the Saemaeum Vault boss prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            ConfigureFloorBossBinding();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Week 15 Boss-2 setup complete: Saemaeum Vault fires five fan volleys, uses longer " +
                      "3-5 / 4-6 jump sequences with landing knockback, heals 15 without a phase-one use cap, " +
                      "and enters an aggressive phase two.");
        }

        private static void EnsurePrefabExists()
        {
            if (Load<GameObject>(BossPrefabPath) != null) return;
            GameObject source = Load<GameObject>(Week15Boss0Setup.BossPrefabPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) throw new InvalidOperationException("Could not instantiate the Boss-0 source.");
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "SaemaeumVaultBoss";
            if (PrefabUtility.SaveAsPrefabAsset(instance, BossPrefabPath) == null)
                throw new InvalidOperationException("Could not create the Saemaeum Vault boss prefab.");
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static void ConfigureSpriteImport()
        {
            AssetDatabase.ImportAsset(BossSpritePath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(BossSpritePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException($"Boss-2 requires a PNG at {BossSpritePath}.");
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
            {
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            }
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>(
                FindObjectsInactive.Include);
            GameObject buseureogi = Load<GameObject>(Week15Boss0Setup.BossPrefabPath);
            GameObject saemaeum = Load<GameObject>(BossPrefabPath);
            if (assembler == null || buseureogi == null || saemaeum == null)
                throw new InvalidOperationException("Boss-2 could not bind the floor-specific boss prefabs.");
            Undo.RecordObject(assembler, "Bind floor-two Saemaeum Vault boss");
            assembler.ConfigureFloorBossPrefabs(new[] { buseureogi, saemaeum, buseureogi });
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Could not save the floor-two Saemaeum Vault binding.");
        }

        private static SpriteRenderer EnsureVisualChild(GameObject contents)
        {
            Transform visual = contents.transform.Find("Visual");
            if (visual == null)
            {
                GameObject visualObject = new("Visual");
                visual = visualObject.transform;
                visual.SetParent(contents.transform, false);
            }
            SpriteRenderer childRenderer = visual.GetComponent<SpriteRenderer>();
            if (childRenderer == null) childRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
            SpriteRenderer rootRenderer = contents.GetComponent<SpriteRenderer>();
            if (rootRenderer != null)
            {
                childRenderer.sharedMaterial = rootRenderer.sharedMaterial;
                childRenderer.sortingLayerID = rootRenderer.sortingLayerID;
                childRenderer.sortingOrder = rootRenderer.sortingOrder;
                UnityEngine.Object.DestroyImmediate(rootRenderer);
            }
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            return childRenderer;
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
