using System;
using TrickalFanGame.Combat;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class ErpinProjectileArtworkSetup
    {
        public const string Folder = "Assets/Art/Characters/Projectiles";
        public const string BasicSpritePath = Folder + "/Erpin_Basic_Orb.png";
        public const string SkillSpritePath = Folder + "/Erpin_LowGrade_Orb.png";
        public const string BasicPrefabPath = "Assets/Prefabs/PlayerProjectile.prefab";
        public const string SkillPrefabPath = "Assets/Prefabs/HomingSkillProjectile.prefab";

        [MenuItem("Trickal Fan Game/Artwork/Setup Erpin Projectile Artwork")]
        public static void Setup()
        {
            Configure(BasicPrefabPath, Import(BasicSpritePath, 480f), ProjectileSizing.PlayerBasicScale);
            // Larger artwork only: homing contact and explosion radius retain their combat settings.
            Configure(SkillPrefabPath, Import(SkillSpritePath, 240f), ProjectileSizing.PlayerSkillScale);
            AssetDatabase.SaveAssets();
        }

        private static Sprite Import(string path, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing Erpin orb: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Configure(string path, Sprite sprite, float scale)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = Color.white;
                ProjectileSizing.Apply(root.transform, root.GetComponent<CircleCollider2D>(), scale);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException("Could not save Erpin projectile: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void SetupAndVerifyBatch()
        {
            Setup();
            Setup();
            Verify();
            Week8ProjectileSizingVerification.Verify();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            Week7LowerGradeSkillVerification.Verify();
            PhaseGProjectileEffectsVerification.Verify();
            Week21Range0Verification.Verify();
        }

        [MenuItem("Trickal Fan Game/Artwork/Verify Erpin Projectile Artwork")]
        public static void Verify()
        {
            GameObject basic = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BasicPrefabPath));
            GameObject skill = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SkillPrefabPath));
            try
            {
                SpriteRenderer a = basic.GetComponent<SpriteRenderer>(), b = skill.GetComponent<SpriteRenderer>();
                Require(AssetDatabase.GetAssetPath(a.sprite) == BasicSpritePath &&
                    AssetDatabase.GetAssetPath(b.sprite) == SkillSpritePath && a.color == Color.white && b.color == Color.white,
                    "Both Erpin projectile prefabs must render their yellow orb without tint.");
                Require(a.bounds.size.x < b.bounds.size.x && Mathf.Approximately(b.bounds.size.x / a.bounds.size.x, 1.4f),
                    "Low-grade orb artwork must be 40% larger than basic artwork.");
                Require(Mathf.Approximately(basic.GetComponent<CircleCollider2D>().bounds.extents.x, 0.2f) &&
                    Mathf.Approximately(skill.GetComponent<CircleCollider2D>().bounds.extents.x, 0.14f),
                    "Basic contact radius must decrease 20%; low-grade contact radius must remain unchanged.");
                Require(basic.GetComponent<Projectile>() != null && skill.GetComponent<HomingSkillProjectile>() != null &&
                    Mathf.Approximately(new SerializedObject(skill.GetComponent<HomingSkillProjectile>()).FindProperty("explosionRadius").floatValue, 1.25f),
                    "Projectile roles and low-grade explosion radius must remain unchanged.");
                Debug.Log("Erpin projectile artwork verification passed: yellow orbs, smaller basic artwork, 20% smaller basic hitbox and unchanged homing/explosion settings.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(basic);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        private static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }
    }
}
