using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class ErpinHighGradeArtworkSetup
    {
        public const string Folder = "Assets/Resources/Characters/Erpin_HighGrade";

        [MenuItem("Trickal Fan Game/Artwork/Setup Erpin High Grade Artwork")]
        public static void Setup()
        {
            for (int i = 0; i < 8; i++)
            {
                string path = $"{Folder}/Erpin_HighGrade_{i}.png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing high-grade pose: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 400f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }

        public static void SetupAndVerifyBatch()
        {
            Setup();
            Setup();
            Verify();
            ErpinWalkAnimationVerification.Verify();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            Week7HighGradeSkillVerification.Verify();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            Week7LowerGradeSkillVerification.Verify();
        }

        [MenuItem("Trickal Fan Game/Artwork/Verify Erpin High Grade Artwork")]
        public static void Verify()
        {
            Sprite[] frames = Resources.LoadAll<Sprite>("Characters/Erpin_HighGrade");
            Require(frames.Length == 8, "High-grade artwork needs eight distinct poses.");
            GameObject player = new GameObject("High-grade artwork fixture");
            try
            {
                SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
                Sprite idle = Resources.LoadAll<Sprite>("Characters/Erpin_Walking")[0];
                renderer.sprite = idle;
                player.AddComponent<CircleCollider2D>();
                Health health = player.AddComponent<Health>();
                Invoke(health, "Awake");
                PlayerMovement movement = player.AddComponent<PlayerMovement>();
                Invoke(movement, "Awake");
                player.AddComponent<PlayerUltimate>();
                PlayerActionState state = player.GetComponent<PlayerActionState>();
                PlayerWalkAnimator animator = player.GetComponent<PlayerWalkAnimator>();
                Invoke(animator, "Awake");
                Bounds hitbox = player.GetComponent<Collider2D>().bounds;
                Vector3 scale = player.transform.localScale, position = player.transform.position;
                Require(state.TryBeginUltimate(Vector2.left, 2f), "Dash setup failed.");
                animator.PlaySkill();
                Require(Tick(animator, 0.01f) && renderer.sprite.name == "Erpin_HighGrade_0" && !animator.IsPlayingSkill,
                    "High grade must show staff preparation and interrupt lower-grade artwork.");
                Tick(animator, 0.1f);
                Require(renderer.sprite.name == "Erpin_HighGrade_1" && !renderer.flipX, "Dash must run facing left.");
                Sprite frozen = renderer.sprite;
                Tick(animator, 0f);
                Require(renderer.sprite == frozen, "Pause must freeze high-grade artwork.");
                Tick(animator, 0.11f);
                Require(renderer.sprite.name == "Erpin_HighGrade_2", "Dash stride must advance.");
                state.TryUpdateDashDirection(Vector2.right);
                Tick(animator, 0.11f);
                Require(renderer.sprite.name == "Erpin_HighGrade_3" && renderer.flipX, "Dash artwork must track changed direction.");
                Require(state.TryBeginImpactRecovery(), "Impact setup failed.");
                Tick(animator, 0.05f);
                Require(renderer.sprite.name == "Erpin_HighGrade_4", "Impact must start with recoil.");
                Tick(animator, 0.1f);
                Require(renderer.sprite.name == "Erpin_HighGrade_5", "Impact must tumble.");
                Tick(animator, 0.1f);
                Require(renderer.sprite.name == "Erpin_HighGrade_6", "Impact must lie fallen.");
                Tick(animator, 0.1f);
                Require(renderer.sprite.name == "Erpin_HighGrade_7", "Impact must get up within existing recovery time.");
                Require(hitbox == player.GetComponent<Collider2D>().bounds && position == player.transform.position && scale == player.transform.localScale,
                    "Drawn fall must preserve player root and hitbox.");
                state.TryCompleteRecovery();
                Require(!Tick(animator, 0f), "Completed recovery must release artwork ownership.");
                state.TryBeginUltimate(Vector2.left, 2f);
                state.TryBeginCoastRecovery();
                Require(!Tick(animator, 0.1f), "Cancel/timeout coasting must not play a collision fall.");
                Invoke(animator, "OnDisable");
                Require(renderer.sprite == idle, "Disable must restore idle.");
                Debug.Log("Erpin high-grade artwork verification passed: staff dash, changing direction, pause, collision tumble/fall/get-up, lower-grade priority, coast exclusion and unchanged physics.");
            }
            finally { UnityEngine.Object.DestroyImmediate(player); }
        }

        private static bool Tick(PlayerWalkAnimator animator, float delta) =>
            (bool)typeof(PlayerWalkAnimator).GetMethod("TickHighGrade", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(animator, new object[] { delta });
        private static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }
    }
}
