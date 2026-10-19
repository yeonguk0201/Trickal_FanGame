using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class SansamoPureMotionVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Sansamo Pure Motion")]
        public static void Verify()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SansamoPureMotionSetup.PrefabPath));
            GameObject player = null;
            GameObject wall = null;
            EnemyProjectile projectile = null;
            try
            {
                root.transform.position = new Vector3(1800f, 1800f, 0f);
                var artwork = root.GetComponent<FairyKingdomArtworkView>();
                artwork.Refresh();
                var motion = root.GetComponent<SansamoPureMotion>();
                Require(motion != null && motion.WalkAtlas != null, "Missing pure Sansamo motion binding.");
                Invoke(motion, "OnEnable");
                var renderer = root.GetComponent<SpriteRenderer>();
                Vector3 scale = root.transform.localScale;
                var collider = root.GetComponent<Collider2D>();
                Vector2 offset = collider.offset;
                Vector3 position = root.transform.position;
                for (int i = 0; i < 4; i++)
                {
                    motion.RenderPose((i + 0.1f) / 4f, true, 10f, Vector2.right);
                    Require(motion.CurrentFrame == i, "Walk frames must advance independently of firing.");
                    Vector3[] rest = motion.PoseMesh.vertices;
                    motion.RenderPose((i + 0.1f) / 4f, true, 0.06f, Vector2.right);
                    Vector3[] fired = motion.PoseMesh.vertices;
                    Require(rest.Zip(fired, (a, b) => a != b).Any(different => different), "Flower must recoil.");
                    for (int vertex = 0; vertex < rest.Length; vertex++)
                        if (rest[vertex].y < 0f) Require(rest[vertex] == fired[vertex], "Shot must not deform the feet/body.");
                    Require(motion.CurrentFrame == i, "Shot must not replace the walk pose.");
                    motion.RenderPose((i + 0.1f) / 4f, true, 1f, Vector2.up);
                    Require(rest.SequenceEqual(motion.PoseMesh.vertices), "Recoil must fully recover.");
                }
                motion.RenderPose(1f, true, 10f, Vector2.left);
                Require(motion.CurrentFrame == 0, "Walk must wrap.");
                motion.RenderPose(0f, false, 10f, Vector2.right);
                Require(motion.CurrentFrame == -1 && motion.PoseSprite.name == "sansamo-pure", "Rest must use original art.");
                renderer.flipX = false;
                Vector2 left = motion.LaunchPosition - (Vector2)root.transform.position;
                renderer.flipX = true;
                Vector2 right = motion.LaunchPosition - (Vector2)root.transform.position;
                Require(Mathf.Abs(left.x + right.x) < 0.001f && Mathf.Abs(left.y - right.y) < 0.001f && left.y > 0.3f,
                    "Flower launch point must mirror horizontally without flipping height.");
                Require(root.transform.localScale == scale && root.transform.position == position && collider.offset == offset,
                    "Motion must preserve gameplay transforms/colliders.");

                var controller = root.GetComponent<MobileRangedEnemyController>();
                player = Week18Obstacle0Verification.CreatePlayer(null, (Vector2)root.transform.position + Vector2.right * 5f);
                controller.SetTarget(player.transform);
                controller.ProjectileFired += shot => projectile = shot;
                controller.TickBehavior(100f);
                Require(projectile != null && Vector2.Distance(projectile.transform.position, motion.LaunchPosition) < 0.001f,
                    "Projectile must spawn at the flower, not at the feet.");
                Require(root.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude > 0.01f,
                    "First shot must retain orbit movement.");
                var body = root.GetComponent<Rigidbody2D>();
                body.position += Vector2.right * 0.02f;
                Invoke(motion, "FixedUpdate");
                Require(motion.IsMoving, "Active firing must not suppress the walk clock.");
                Invoke(motion, "FixedUpdate");
                Require(!motion.IsMoving, "A blocked body must stop its feet even if velocity remains set.");
                wall = new GameObject("Flower launch obstruction") { layer = LayerMask.NameToLayer("Environment") };
                wall.transform.position = motion.LaunchPosition;
                wall.AddComponent<BoxCollider2D>().size = Vector2.one * 0.15f;
                Physics2D.SyncTransforms();
                int remaining = controller.ShotsRemaining;
                EnemyProjectile firstShot = projectile;
                controller.TickBehavior(101f);
                Require(controller.ShotsRemaining == remaining && projectile == firstShot,
                    "A blocked flower must not fire through a wall or consume a burst shot.");
                Invoke(motion, "OnDisable");
                Require(!renderer.forceRenderingOff && !root.GetComponentInChildren<MeshRenderer>().enabled,
                    "Disable must restore the source and hide the temporary mesh.");
                Debug.Log("Sansamo pure motion verification passed: four walk poses, independent flower recoil, " +
                    "idle/recovery, mirrored launch point, real projectile origin, moving fire, collider preservation and cleanup.");
            }
            finally
            {
                if (projectile != null) Object.DestroyImmediate(projectile.gameObject);
                if (wall != null) Object.DestroyImmediate(wall);
                if (player != null) Object.DestroyImmediate(player);
                Object.DestroyImmediate(root);
            }
        }

        public static void SetupVerifyAndExportBatch()
        {
            SansamoPureMotionSetup.Setup();
            Verify();
            ExportPreview();
        }

        [MenuItem("Trickal Fan Game/Artwork/Export Sansamo Pure Motion Preview")]
        public static void ExportPreview()
        {
            string folder = Path.GetFullPath("Logs/SansamoPureMotion");
            Directory.CreateDirectory(folder);
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SansamoPureMotionSetup.PrefabPath));
            RenderTexture target = new(960, 480, 0);
            Texture2D capture = new(960, 480, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            Material material = null;
            try
            {
                var motion = root.GetComponent<SansamoPureMotion>();
                material = new Material(motion.MotionMaterial);
                target.Create();
                for (int frame = 0; frame < 24; frame++)
                {
                    RenderTexture.active = target;
                    GL.Clear(true, true, new Color(0.15f, 0.19f, 0.17f));
                    GL.PushMatrix();
                    try
                    {
                        GL.LoadProjectionMatrix(Matrix4x4.Ortho(-1.8f, 1.8f, -0.9f, 0.9f, -10f, 10f));
                        GL.modelview = Matrix4x4.identity;
                        for (int pose = 0; pose < 3; pose++)
                        {
                            root.GetComponent<SpriteRenderer>().flipX = pose == 2;
                            motion.RenderPose(frame / 12f, pose != 0, pose == 1 ? 10f : frame / 30f % 0.4f, Vector2.right);
                            material.SetTexture("_MainTex", motion.PoseSprite.texture);
                            material.SetColor("_Tint", Color.white);
                            Require(material.SetPass(0), "Preview shader pass failed.");
                            Graphics.DrawMeshNow(motion.PoseMesh, Matrix4x4.Translate(new Vector3((pose - 1) * 1.15f, 0f, 0f)));
                        }
                    }
                    finally { GL.PopMatrix(); }
                    capture.ReadPixels(new Rect(0, 0, 960, 480), 0, 0); capture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, $"frame-{frame:D2}.png"), capture.EncodeToPNG());
                }
                Debug.Log("Sansamo pure preview exported: " + folder);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(root); Object.DestroyImmediate(capture);
                if (material != null) Object.DestroyImmediate(material);
                target.Release(); Object.DestroyImmediate(target);
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private static void Invoke(SansamoPureMotion motion, string method) =>
            typeof(SansamoPureMotion).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(motion, null);
    }
}
