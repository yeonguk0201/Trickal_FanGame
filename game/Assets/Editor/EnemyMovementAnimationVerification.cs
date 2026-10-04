using System;
using System.Reflection;
using System.IO;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class EnemyMovementAnimationVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Enemy Movement Animations")]
        public static void Verify()
        {
            string[] paths = EnemyMovementAnimationSetup.PrefabPaths.Concat(EnemyMovementAnimationSetup.MinionPrefabPaths).ToArray();
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(prefab != null, path + " is missing.");
                Require(prefab.GetComponents<EnemyMovementAnimator>().Length == 1, path + " needs one animator.");
                GameObject root = UnityEngine.Object.Instantiate(prefab);
                root.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    EnemyMovementAnimator animator = root.GetComponent<EnemyMovementAnimator>();
                    Require(animator.Motion == (i == 0 ? EnemyMovementMotion.Hop : EnemyMovementMotion.Walk),
                        path + " has the wrong movement style (Sansamo must walk).");
                    Require(animator.MotionMaterial != null && !ShaderUtil.ShaderHasError(animator.MotionMaterial.shader),
                        path + " needs a valid movement material.");
                    SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                    Sprite original = renderer.sprite;
                    Vector3 position = root.transform.localPosition, scale = root.transform.localScale;
                    Quaternion rotation = root.transform.localRotation;
                    Collider2D collider = root.GetComponent<Collider2D>();
                    Vector2 offset = collider.offset;
                    Invoke(animator, "Awake");
                    Invoke(animator, "OnEnable");
                    animator.RenderPose(0f, false);
                    if (i == 0)
                    {
                        Mesh mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                        Vector3[] rest = mesh.vertices;
                        animator.RenderPose(0.25f, true);
                        Vector3[] firstPose = mesh.vertices;
                        animator.RenderPose(0.75f, true);
                        Require(Different(rest, firstPose) && Different(firstPose, mesh.vertices), path + " has no hop poses.");
                        animator.RenderPose(0.25f, false);
                        Require(!Different(rest, mesh.vertices), path + " must restore the idle silhouette.");
                    }
                    else
                    {
                        Require(animator.WalkFrameCount == EnemyMovementAnimationSetup.WalkFrameCount,
                            path + " requires four drawn walking frames.");
                        for (int frame = 0; frame < animator.WalkFrameCount; frame++)
                        {
                            Sprite sprite = animator.GetWalkFrame(frame);
                            float frameSize = i < 4 ? 512f : 640f;
                            Require(sprite != null && sprite.rect.size == Vector2.one * frameSize &&
                                    Mathf.Approximately(sprite.pixelsPerUnit, 400f) && sprite.pivot == Vector2.one * frameSize * 0.5f,
                                path + " frames must use matching size, scale and registration.");
                            for (int earlier = 0; earlier < frame; earlier++)
                                Require(sprite != animator.GetWalkFrame(earlier), path + " must use distinct pose assets.");
                            animator.RenderPose((frame + 0.1f) / animator.WalkFrameCount, true);
                            Require(renderer.sprite == sprite, path + " must play each frame in order.");
                        }
                        animator.RenderPose(1f, true);
                        Require(renderer.sprite == animator.GetWalkFrame(0), path + " must loop back to the first frame.");
                        animator.RenderPose(0.75f, false);
                        Require(renderer.sprite == original, path + " must restore the original idle sprite.");
                        Require(root.GetComponentInChildren<MeshFilter>() == null && !renderer.forceRenderingOff,
                            path + " walking must use the SpriteRenderer directly without mesh deformation.");
                    }
                    Require(root.transform.localPosition == position && root.transform.localScale == scale &&
                            root.transform.localRotation == rotation && collider.offset == offset,
                        path + " movement artwork must not change gameplay transforms/colliders.");
                    Require(renderer.sprite == original, path + " must retain the shared source sprite.");
                    Invoke(animator, "LateUpdate");
                    MeshRenderer visual = root.GetComponentInChildren<MeshRenderer>();
                    if (i == 0)
                        Require(renderer.forceRenderingOff && visual.enabled && visual.sharedMaterial == animator.MotionMaterial,
                            path + " must render exactly one hopping visual.");
                    else Require(!renderer.forceRenderingOff && visual == null, path + " walks with its existing renderer.");
                    renderer.color = new Color(0.4f, 0.7f, 1f, 0.8f);
                    renderer.sortingOrder = 17;
                    Invoke(animator, "LateUpdate");
                    if (i == 0)
                    {
                        MaterialPropertyBlock block = new MaterialPropertyBlock();
                        visual.GetPropertyBlock(block);
                        Require(block.GetColor("_Tint") == renderer.color && visual.sortingOrder == 17,
                            path + " must preserve attack tint and sorting.");
                    }
                    else Require(renderer.color == new Color(0.4f, 0.7f, 1f, 0.8f) && renderer.sortingOrder == 17,
                        path + " must retain attack tint and sorting.");
                    Rigidbody2D body = root.GetComponent<Rigidbody2D>();
                    body.linearVelocity = Vector2.right;
                    body.position += Vector2.right * 0.02f;
                    Invoke(animator, "FixedUpdate");
                    Require(animator.IsAnimating, path + " must animate real movement.");
                    Require(renderer.flipX, path + " must face right during rightward travel.");
                    Invoke(animator, "FixedUpdate");
                    Require(!animator.IsAnimating, path + " must stop when blocked even with a requested velocity.");
                    Require(renderer.flipX, path + " must keep facing when blocked.");
                    body.linearVelocity = Vector2.left;
                    body.position += Vector2.left * 0.02f;
                    Invoke(animator, "FixedUpdate");
                    Require(!renderer.flipX, path + " must face left during leftward travel.");
                    body.linearVelocity = Vector2.up;
                    body.position += Vector2.up * 0.02f;
                    Invoke(animator, "FixedUpdate");
                    Require(!renderer.flipX, path + " must retain facing during vertical travel.");
                    body.position += Vector2.right * 10f;
                    Invoke(animator, "FixedUpdate");
                    Require(!renderer.flipX, path + " room placement must not change facing.");
                    EnemyAttackPresentation attack = root.GetComponent<EnemyAttackPresentation>();
                    if (attack == null) attack = root.AddComponent<EnemyAttackPresentation>();
                    attack.SetPhase(EnemyAttackPhase.Telegraph);
                    body.position += Vector2.right * 0.02f;
                    Invoke(animator, "FixedUpdate");
                    Require(!animator.IsAnimating, path + " must preserve attack telegraphs.");
                    attack.SetPhase(EnemyAttackPhase.Idle);
                    KnockbackReceiver knockback = root.GetComponent<KnockbackReceiver>();
                    Require(knockback.Apply(Vector2.right, 2f), path + " knockback setup failed.");
                    body.position += Vector2.right * 0.02f;
                    Invoke(animator, "FixedUpdate");
                    Require(!animator.IsAnimating, path + " must not walk during knockback.");
                    Require(!renderer.flipX, path + " knockback must not reverse facing.");
                    animator.RenderPose(0.25f, true);
                    Invoke(animator, "OnDisable");
                    Require(!renderer.forceRenderingOff && (visual == null || !visual.enabled) && renderer.sprite == original,
                        path + " must restore source rendering and idle sprite on disable.");
                    Invoke(animator, "OnEnable");
                    Invoke(animator, "LateUpdate");
                    Require(root.GetComponentsInChildren<MeshRenderer>(true).Length == (i == 0 ? 1 : 0),
                        path + " must reuse its visual after room reactivation.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab.GetComponent<BossController>() != null)
                    Require(prefab.GetComponent<EnemyMovementAnimator>() == null,
                        prefab.name + " must retain its boss-specific animation without inherited normal-enemy motion.");
            }
            Debug.Log("Enemy movement verification passed: boss exclusion, three drawn walk cycles without deformation, hop poses, idle restoration, " +
                "physics isolation, tint/sorting, walls, attack/knockback suppression and reactivation.");
        }

        public static void SetupAndVerifyBatch()
        {
            EnemyMovementAnimationSetup.Setup();
            EnemyMovementAnimationSetup.Setup();
            Verify();
        }

        // Produces a contact strip for checking the original artwork at sixteen points in a stride.
        [MenuItem("Trickal Fan Game/Artwork/Export Enemy Movement Preview")]
        public static void ExportPreview()
        {
            ExportPreview(false);
        }

        [MenuItem("Trickal Fan Game/Artwork/Export Crayon Minion Movement Preview")]
        public static void ExportMinionPreview()
        {
            ExportPreview(true);
        }

        private static void ExportPreview(bool minions)
        {
            if (minions) BossMovementAnimationVerification.SetupAndVerifyBatch();
            else SetupAndVerifyBatch();
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath,
                minions ? "../Logs/CrayonMinionMovementPreview" : "../Logs/EnemyMovementPreviewV2"));
            Directory.CreateDirectory(folder);
            GameObject[] roots = new GameObject[4];
            int width = minions ? 1200 : 1024, height = minions ? 480 : 320;
            RenderTexture target = new RenderTexture(width, height, 0);
            RenderTexture previous = RenderTexture.active;
            Texture2D capture = new Texture2D(width, height, TextureFormat.RGB24, false);
            Material material = new Material(AssetDatabase.LoadAssetAtPath<Material>(EnemyMovementAnimationSetup.MaterialPath));
            try
            {
                target.Create();
                for (int i = 0; i < roots.Length; i++)
                {
                    roots[i] = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        (minions ? EnemyMovementAnimationSetup.MinionPrefabPaths : EnemyMovementAnimationSetup.PrefabPaths)[i]));
                    Invoke(roots[i].GetComponent<EnemyMovementAnimator>(), "Awake");
                }
                for (int frame = 0; frame < 16; frame++)
                {
                    RenderTexture.active = target;
                    GL.Clear(true, true, new Color(0.15f, 0.19f, 0.17f));
                    GL.PushMatrix();
                    try
                    {
                        GL.LoadProjectionMatrix(Matrix4x4.Ortho(0f, 4f, minions ? -0.2f : 0f, minions ? 1.5f : 1.25f, -10f, 10f));
                        GL.modelview = Matrix4x4.identity;
                        for (int i = 0; i < roots.Length; i++)
                        {
                            roots[i].GetComponent<EnemyMovementAnimator>().RenderPose(frame / 16f, true);
                            Sprite sprite = roots[i].GetComponent<SpriteRenderer>().sprite;
                            material.SetTexture("_MainTex", sprite.texture);
                            material.SetColor("_Tint", Color.white);
                            Require(material.SetPass(0), "Enemy preview material pass failed.");
                            Mesh preview = !minions && i == 0 ? roots[i].GetComponentInChildren<MeshFilter>().sharedMesh : SpriteMesh(sprite);
                            Graphics.DrawMeshNow(preview,
                                Matrix4x4.TRS(new Vector3(i + 0.5f, 0.6f, 0f), Quaternion.identity, Vector3.one * 0.7f));
                            if (minions || i != 0) UnityEngine.Object.DestroyImmediate(preview);
                        }
                    }
                    finally { GL.PopMatrix(); }
                    capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    capture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, $"frame-{frame:D2}.png"), capture.EncodeToPNG());
                }
                Debug.Log("Enemy movement preview exported to " + folder);
            }
            finally
            {
                RenderTexture.active = previous;
                foreach (GameObject root in roots) if (root != null) UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(capture);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static Mesh SpriteMesh(Sprite sprite)
        {
            Mesh mesh = new Mesh { name = "Walking sprite preview" };
            mesh.vertices = sprite.vertices.Select(v => new Vector3(v.x, v.y, 0f)).ToArray();
            mesh.uv = sprite.uv;
            mesh.triangles = sprite.triangles.Select(t => (int)t).ToArray();
            return mesh;
        }

        private static bool Different(Vector3[] a, Vector3[] b)
        {
            for (int i = 0; i < a.Length; i++) if ((a[i] - b[i]).sqrMagnitude > 0.000001f) return true;
            return false;
        }

        private static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }
    }
}
