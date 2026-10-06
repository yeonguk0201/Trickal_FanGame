using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class EnemyAttackAnimationVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Enemy Attack Animations")]
        public static void Verify()
        {
            string[] paths = EnemyMovementAnimationSetup.PrefabPaths.Concat(EnemyMovementAnimationSetup.MinionPrefabPaths).ToArray();
            for (int i = 0; i < paths.Length; i++)
            {
                GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    paths[i]));
                try
                {
                    SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                    Sprite original = renderer.sprite;
                    EnemyMovementAnimator movement = root.GetComponent<EnemyMovementAnimator>();
                    EnemyAttackArtwork artwork = root.GetComponent<EnemyAttackArtwork>();
                    Require(artwork != null && root.GetComponents<EnemyAttackArtwork>().Length == 1 && artwork.FrameCount == 4,
                        root.name + " needs four distinct attack poses and one artwork component.");
                    Invoke(root.GetComponent<Health>(), "Awake");
                    Invoke(root.GetComponent<KnockbackReceiver>(), "Awake");
                    Invoke(movement, "Awake");
                    Invoke(artwork, "Awake");
                    EnemyAttackPresentation presentation = root.GetComponent<EnemyAttackPresentation>();
                    Invoke(presentation, "Awake");
                    Vector3 scale = root.transform.localScale, position = root.transform.position;
                    Collider2D collider = root.GetComponent<Collider2D>();
                    Bounds bounds = collider.bounds;
                    for (int phase = 1; phase <= 3; phase++)
                    {
                        Sprite pose = artwork.GetFrame(phase);
                        Require(pose != null && pose.pixelsPerUnit == 400f && pose.rect.size == Vector2.one * 1024f,
                            "Attack poses need a common canvas and registration.");
                        Require(pose != artwork.GetFrame(phase - 1), "Attack poses must be distinct assets.");
                        presentation.SetPhase((EnemyAttackPhase)phase);
                        movement.RenderPose(0.5f, true);
                        Require(renderer.sprite == pose && !renderer.forceRenderingOff,
                            "Attack must override both stride and hop rendering.");
                        Invoke(movement, "LateUpdate");
                        Require(renderer.sprite == pose, "LateUpdate must retain attack pose.");
                        Require(root.transform.localScale == scale && root.transform.position == position && collider.bounds == bounds,
                            "Attack artwork must preserve body transforms and hitbox.");
                    }
                    GameObject target = new GameObject("Artwork facing target");
                    target.transform.SetParent(root.transform, false);
                    root.GetComponent<EnemyBehaviorContext>().SetTarget(target.transform);
                    foreach (Vector2 direction in new[] { Vector2.right, Vector2.left, Vector2.up })
                    {
                        target.transform.position = root.transform.position + (Vector3)direction;
                        MonoBehaviour controller = (MonoBehaviour)root.GetComponent<ChargingEnemyController>() ?? root.GetComponent<LongRangeSniperController>();
                        if (controller != null) controller.GetType().GetField("lockedDirection", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, direction);
                        movement.RenderPose(0f, false);
                        Require(renderer.flipX == (direction == Vector2.right),
                            "Attack must face its target/locked direction and retain facing for vertical attacks.");
                    }
                    presentation.SetPhase(EnemyAttackPhase.Idle);
                    movement.RenderPose(0f, false);
                    Require(renderer.sprite == original, "Recovery must restore original idle artwork.");
                    presentation.SetPhase(EnemyAttackPhase.Active);
                    Require(root.GetComponent<KnockbackReceiver>().Apply(Vector2.right, 2f), "Knockback setup failed.");
                    Require(!artwork.TryRender(), "Knockback must suppress attack artwork immediately.");
                    root.GetComponent<KnockbackReceiver>().Stop();
                    movement.RenderPose(0f, false);
                    Invoke(movement, "OnDisable");
                    Require(renderer.sprite == original && !renderer.forceRenderingOff, "Disable must restore artwork.");
                    Require((artwork.ProjectileSprite != null) == (i == 3 || i == 4 || i == 5),
                        "Only throwing fairy and ranged minions may have projectile artwork.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            foreach (string path in EnemyMovementAnimationSetup.MinionPrefabPaths)
            {
                GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    EnemyAttackArtwork inherited = root.GetComponent<EnemyAttackArtwork>();
                    Invoke(root.GetComponent<Health>(), "Awake");
                    Invoke(root.GetComponent<KnockbackReceiver>(), "Awake");
                    root.GetComponent<EnemyAttackPresentation>().SetPhase(EnemyAttackPhase.Active);
                    Require(inherited != null && inherited.TryRender() &&
                        root.GetComponent<SpriteRenderer>().sprite == inherited.GetFrame(2) &&
                        AssetDatabase.GetAssetPath(inherited.GetFrame(2)).StartsWith(EnemyAttackAnimationSetup.MinionFolder) &&
                        inherited.ProjectileSprite != AssetDatabase.LoadAssetAtPath<Sprite>(EnemyAttackAnimationSetup.ProjectilePath),
                        "Minions must use their own poses and never fairy/ginseng poses or onions.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            VerifyShot();
            VerifyMinionShots();
            Debug.Log("Enemy attack artwork verification passed: combat phases, movement priority, physics isolation, cancellation, minion ownership and actual onion/arrow/magic firing.");
        }

        private static void VerifyMinionShots()
        {
            for (int i = 0; i < 2; i++)
            {
                GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyMovementAnimationSetup.MinionPrefabPaths[i]));
                EnemyProjectile shot = null;
                try
                {
                    LongRangeSniperController sniper = root.GetComponent<LongRangeSniperController>();
                    Invoke(sniper, "Awake");
                    typeof(LongRangeSniperController).GetField("lockedDirection", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sniper, Vector2.left);
                    sniper.ProjectileFired += projectile => shot = projectile;
                    typeof(LongRangeSniperController).GetMethod("Fire", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sniper, new object[] { 0f });
                    SpriteRenderer renderer = shot.GetComponent<SpriteRenderer>();
                    Require(renderer.sprite == root.GetComponent<EnemyAttackArtwork>().ProjectileSprite && renderer.color == Color.white &&
                        renderer.sprite.name == (i == 0 ? "CrayonArrow_Projectile" : "CrayonMagic_Projectile"), "Minion firing must emit its own arrow/magic artwork.");
                    Require(shot.Velocity == Vector2.left * sniper.ProjectileSpeed &&
                        Mathf.Approximately(shot.GetComponent<CircleCollider2D>().bounds.extents.x, ProjectileSizing.WorldCollisionRadius(ProjectileSizing.RangedEnemyScale)),
                        "Minion projectile artwork must preserve speed and collision size.");
                    root.GetComponent<EnemyMovementAnimator>().RenderPose(0f, false);
                    Require(root.GetComponent<SpriteRenderer>().sprite == root.GetComponent<EnemyAttackArtwork>().GetFrame(2), "Release/cast pose must coincide with firing.");
                }
                finally
                {
                    if (shot != null) UnityEngine.Object.DestroyImmediate(shot.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void VerifyShot()
        {
            GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy4Setup.SniperPrefabPath));
            EnemyProjectile shot = null;
            try
            {
                LongRangeSniperController sniper = root.GetComponent<LongRangeSniperController>();
                Invoke(sniper, "Awake");
                typeof(LongRangeSniperController).GetField("lockedDirection", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(sniper, Vector2.right);
                sniper.ProjectileFired += projectile => shot = projectile;
                typeof(LongRangeSniperController).GetMethod("Fire", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(sniper, new object[] { 0f });
                SpriteRenderer renderer = shot.GetComponent<SpriteRenderer>();
                Require(renderer.sprite == root.GetComponent<EnemyAttackArtwork>().ProjectileSprite && renderer.color == Color.white,
                    "Firing must use the actual onion sprite with original colors.");
                Require(renderer.sprite.pixelsPerUnit == EnemyAttackAnimationSetup.ProjectilePixelsPerUnit &&
                    Mathf.Approximately(shot.GetComponent<CircleCollider2D>().bounds.extents.x,
                        ProjectileSizing.WorldCollisionRadius(ProjectileSizing.RangedEnemyScale)),
                    "Larger onion artwork must keep the original world collision radius.");
                Require(shot.Velocity == Vector2.right * sniper.ProjectileSpeed && shot.transform.localScale == Vector3.one * ProjectileSizing.RangedEnemyScale &&
                    shot.GetComponent<CircleCollider2D>().radius == ProjectileSizing.BaseColliderRadius,
                    "Onion projectile must preserve speed and collision radius.");
                Require(Mathf.Abs(Mathf.DeltaAngle(renderer.transform.eulerAngles.z, 180f)) < 0.01f,
                    "Onion must orient along its firing direction.");
                Require(root.GetComponent<EnemyAttackPresentation>().Phase == EnemyAttackPhase.Active,
                    "The throw pose must coincide with projectile emission.");
            }
            finally
            {
                if (shot != null) UnityEngine.Object.DestroyImmediate(shot.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void SetupAndVerifyBatch()
        {
            EnemyAttackAnimationSetup.Setup();
            EnemyAttackAnimationSetup.Setup();
            Verify();
            EnemyMovementAnimationVerification.Verify();
            // Controller fixtures need an empty physics world, otherwise live room walls block their rays.
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            // The older full Enemy-2 suite forbids the boss's own attack presentation, which is now
            // intentionally used by BossController. Run its relevant melee contracts directly.
            foreach (string method in new[] { "ValidatePrefabRoles", "ValidateMeleeLifecycleAndDamageSharing", "ValidateMeleeCancellation" })
                typeof(Week15Enemy2Verification).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Week15Enemy4Verification.Verify();
        }

        [MenuItem("Trickal Fan Game/Artwork/Export Enemy Attack Preview")]
        public static void ExportPreview()
        {
            SetupAndVerifyBatch();
            RenderPreview();
        }

        public static void RenderPreview()
        {
            RenderPreview(false);
        }

        // Replacing artwork with the same GUID needs no prefab/configuration rewrite.
        public static void VerifyAndRenderMinionPreview()
        {
            Verify();
            RenderPreview(true);
        }

        [MenuItem("Trickal Fan Game/Artwork/Export Crayon Minion Attack Preview")]
        public static void ExportMinionPreview()
        {
            SetupAndVerifyBatch();
            BossMovementAnimationVerification.Verify();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            Week15Boss3Verification.Verify();
            RenderPreview(true);
        }

        private static void RenderPreview(bool minions)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, minions ? "../Logs/CrayonMinionAttackPreview" : "../Logs/EnemyAttackPreview"));
            Directory.CreateDirectory(folder);
            GameObject[] roots = (minions ? EnemyMovementAnimationSetup.MinionPrefabPaths : EnemyMovementAnimationSetup.PrefabPaths).Select(path =>
                UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path))).ToArray();
            RenderTexture target = new RenderTexture(1600, 400, 0);
            RenderTexture previous = RenderTexture.active;
            Texture2D capture = new Texture2D(1600, 400, TextureFormat.RGB24, false);
            Material material = new Material(AssetDatabase.LoadAssetAtPath<Material>(EnemyMovementAnimationSetup.MaterialPath));
            try
            {
                target.Create();
                foreach (GameObject root in roots) Invoke(root.GetComponent<EnemyMovementAnimator>(), "Awake");
                for (int phase = 0; phase < 4; phase++)
                {
                    RenderTexture.active = target;
                    GL.Clear(true, true, new Color(0.15f, 0.19f, 0.17f));
                    GL.PushMatrix();
                    try
                    {
                        GL.LoadProjectionMatrix(Matrix4x4.Ortho(0f, 5.6f, -0.2f, 1.2f, -10f, 10f));
                        GL.modelview = Matrix4x4.identity;
                        for (int i = 0; i < 4; i++)
                        {
                            roots[i].GetComponent<EnemyAttackPresentation>().SetPhase((EnemyAttackPhase)phase);
                            roots[i].GetComponent<EnemyMovementAnimator>().RenderPose(0f, false);
                            Sprite sprite = roots[i].GetComponent<SpriteRenderer>().sprite;
                            Draw(sprite, new Vector3(i * 1.4f + 0.7f, 0.5f), 0.8f, material);
                            Sprite projectile = roots[i].GetComponent<EnemyAttackArtwork>().ProjectileSprite;
                            if (projectile != null && phase == 2)
                                Draw(projectile, new Vector3(i * 1.4f + 0.3f, 0.4f), 0.3f, material);
                        }
                    }
                    finally { GL.PopMatrix(); }
                    capture.ReadPixels(new Rect(0, 0, 1600, 400), 0, 0);
                    capture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, $"phase-{phase}.png"), capture.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous;
                foreach (GameObject root in roots) UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(capture);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void Draw(Sprite sprite, Vector3 position, float scale, Material material)
        {
            Mesh mesh = new Mesh();
            mesh.vertices = sprite.vertices.Select(v => new Vector3(v.x, v.y)).ToArray();
            mesh.uv = sprite.uv;
            mesh.triangles = sprite.triangles.Select(t => (int)t).ToArray();
            material.SetTexture("_MainTex", sprite.texture);
            material.SetColor("_Tint", Color.white);
            Require(material.SetPass(0), "Preview material failed.");
            Graphics.DrawMeshNow(mesh, Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * scale));
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        private static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }
    }
}
