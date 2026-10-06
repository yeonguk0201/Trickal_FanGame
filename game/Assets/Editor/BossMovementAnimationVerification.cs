using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class BossMovementAnimationVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Boss Movement Animations")]
        public static void Verify()
        {
            for (int i = 0; i < BossMovementAnimationSetup.PrefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossMovementAnimationSetup.PrefabPaths[i]);
                Require(prefab != null && prefab.GetComponents<BossMovementAnimator>().Length == 1, "Each boss needs exactly one movement animator.");
                GameObject root = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    BossMovementAnimator animator = root.GetComponent<BossMovementAnimator>();
                    Require(animator.Style == (BossMovementStyle)i && animator.Source != null, root.name + " has invalid animation configuration.");
                    Sprite original = animator.Source.sprite;
                    Vector3 position = root.transform.localPosition, scale = root.transform.localScale;
                    Quaternion rotation = root.transform.localRotation;
                    Vector3 sourcePosition = animator.Source.transform.localPosition, sourceScale = animator.Source.transform.localScale;
                    Collider2D collider = root.GetComponent<Collider2D>();
                    Vector2 colliderOffset = collider.offset;
                    bool trigger = collider.isTrigger;
                    Invoke(animator, "Awake");
                    Invoke(animator, "OnEnable");
                    animator.RenderPose(0.52f, true);
                    Require(animator.Artwork != null && animator.Source.forceRenderingOff && animator.Artwork.enabled,
                        "The original sprite must have exactly one visible presentation.");
                    if (i == 2)
                    {
                        Require(animator.WalkFrameCount == 4, "Crayon Hero needs four drawn walking poses.");
                        for (int frame = 0; frame < 4; frame++)
                        {
                            Sprite sprite = animator.GetWalkFrame(frame);
                            Require(sprite != null && sprite.rect.size == new Vector2(512f, 512f) &&
                                    sprite.pivot == new Vector2(256f, 256f) && Mathf.Approximately(sprite.pixelsPerUnit, 400f),
                                "Crayon frames must share scale and registration.");
                            for (int other = 0; other < frame; other++) Require(sprite != animator.GetWalkFrame(other), "Crayon walk poses must be distinct.");
                            animator.RenderPose((frame + 0.1f) / 4f, true);
                            Require(animator.Artwork.sprite == sprite && animator.Artwork.transform.localScale == Vector3.one,
                                "Crayon walking must play poses without elastic deformation.");
                        }
                        animator.RenderPose(1f, true);
                        Require(animator.Artwork.sprite == animator.GetWalkFrame(0), "Crayon walk must loop.");
                        animator.RenderPose(0.5f, false);
                        Require(animator.Artwork.sprite == original, "Crayon must restore the idle artwork.");
                    }
                    else
                    {
                        Require(animator.HopFrameCount == 4, "Hopping bosses require four drawn poses.");
                        for (int frame = 0; frame < 4; frame++)
                        {
                            Sprite pose = animator.GetHopFrame(frame);
                            Require(pose != null && Mathf.Approximately(pose.pixelsPerUnit, 400f), "Hop frames must share scale.");
                            for (int earlier = 0; earlier < frame; earlier++)
                                Require(pose != animator.GetHopFrame(earlier), "Hop poses must be distinct artwork.");
                        }
                        animator.RenderPose(0.08f, true);
                        Require(animator.Artwork.sprite == animator.GetHopFrame(1), "Preparation must use drawn anticipation artwork.");
                        animator.RenderPose(0.52f, true);
                        Require(animator.Artwork.sprite == animator.GetHopFrame(2), "Flight must use drawn airborne artwork.");
                        Require(animator.Artwork.transform.localPosition.y > 0f && animator.Artwork.transform.localScale == Vector3.one,
                            "Hopping bosses must rise with the artwork's original proportions.");
                        animator.RenderPose(0.93f, true);
                        Require(animator.Artwork.sprite == animator.GetHopFrame(3), "Landing must use drawn landing artwork.");
                        Require(animator.Artwork.transform.localScale == Vector3.one, "Hop landing must retain original proportions while deformation is disabled.");
                        animator.RenderPose(0.5f, false);
                        Require(animator.Artwork.transform.localPosition == Vector3.zero && animator.Artwork.transform.localScale == Vector3.one,
                            "Stationary bosses must restore the ordinary silhouette.");
                        foreach (float boundary in new[] { 0f, 0.18f, 0.86f, 1f })
                        {
                            animator.RenderPose(boundary - 0.0001f, true);
                            Vector3 before = animator.Artwork.transform.localPosition;
                            Vector3 beforeScale = animator.Artwork.transform.localScale;
                            animator.RenderPose(boundary + 0.0001f, true);
                            Require(Vector3.Distance(before, animator.Artwork.transform.localPosition) < 0.0001f &&
                                    Vector3.Distance(beforeScale, animator.Artwork.transform.localScale) < 0.0001f,
                                "Hop segment boundaries must have smooth position/scale and zero contact velocity.");
                        }
                    }
                    Require(root.transform.localPosition == position && root.transform.localScale == scale && root.transform.localRotation == rotation &&
                            collider.offset == colliderOffset && collider.isTrigger == trigger &&
                            animator.Source.transform.localPosition == sourcePosition && animator.Source.transform.localScale == sourceScale,
                        "Movement art must not change physics or the existing pattern presentation transform.");
                    Require(animator.Source.sprite == original, "Keep original source artwork for projectile and legacy consumers.");
                    animator.Source.color = new Color(1f, 0.7f, 0.25f, 1f);
                    animator.Source.sortingOrder = 19;
                    animator.RenderPose(0.5f, true);
                    Require(animator.Artwork.color == animator.Source.color && animator.Artwork.sortingOrder == 19,
                        "Movement art must retain phase tint and sorting.");
                    if (i == 1) VerifyTreasureIsolation(root, animator);
                    Rigidbody2D body = root.GetComponent<Rigidbody2D>();
                    body.position += Vector2.right * 0.03f;
                    Invoke(animator, "FixedUpdate");
                    Require(animator.IsAnimating, "Actual movement must advance the boss stride.");
                    animator.RenderPose(0.5f, true);
                    Require(animator.Source.flipX && animator.Artwork.flipX, "Boss artwork must face right during rightward travel.");
                    for (int pile = 0; pile < animator.TreasureCount; pile++)
                        Require(animator.GetTreasure(pile).flipX, "Vault ground layers must mirror together with its body.");
                    Invoke(animator, "FixedUpdate");
                    Require(!animator.IsAnimating, "A stationary/blocked boss must stop the stride.");
                    Require(animator.Source.flipX, "A stationary boss must keep facing.");
                    body.position += Vector2.left * 0.03f;
                    Invoke(animator, "FixedUpdate");
                    animator.RenderPose(0.5f, true);
                    Require(!animator.Source.flipX && !animator.Artwork.flipX, "Boss artwork must face left during leftward travel.");
                    for (int pile = 0; pile < animator.TreasureCount; pile++)
                        Require(!animator.GetTreasure(pile).flipX, "Vault ground layers must restore together with its body.");
                    body.position += Vector2.up * 0.03f;
                    Invoke(animator, "FixedUpdate");
                    Require(!animator.Source.flipX, "Vertical boss travel must keep facing.");
                    body.position += Vector2.right * 10f;
                    Invoke(animator, "FixedUpdate");
                    Require(!animator.Source.flipX, "Boss room placement must not change facing.");
                    animator.RenderPose(0.5f, true);
                    Invoke(animator, "OnDisable");
                    Require(!animator.Source.forceRenderingOff && !animator.Artwork.enabled, "Disabling motion must restore source rendering.");
                    for (int pile = 0; pile < animator.TreasureCount; pile++)
                        Require(!animator.GetTreasure(pile).enabled, "Disabled motion must not double the original ground treasure.");
                    Invoke(animator, "OnEnable");
                    animator.RenderPose(0.5f, true);
                    Require(root.GetComponentsInChildren<SpriteRenderer>(true).Count(r => r.name == "Boss Movement Artwork") == 1,
                        "Reactivation must reuse a single motion renderer.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            VerifyRealVaultJump();
            VerifyMinionArtwork();
            Debug.Log("Boss movement verification passed: three profiles, drawn knight poses, visual-only hops, independent treasure, " +
                "landing reactions, real jump callback, idle/blocking, phase tint, physics preservation, minion identity and reactivation.");
        }

        private static void VerifyTreasureIsolation(GameObject root, BossMovementAnimator animator)
        {
            Require(animator.TreasureCount == 2 && animator.BodySprite != null, "Vault requires body and two ground treasure layers.");
            Vector3[] positions = new Vector3[2];
            for (int i = 0; i < 2; i++)
            {
                Transform pile = animator.GetTreasure(i).transform;
                Require(pile.parent == root.transform && !pile.IsChildOf(animator.Source.transform), "Ground treasure must not inherit jump height or squash.");
                positions[i] = pile.localPosition;
            }
            animator.RenderPose(0.52f, true);
            animator.TickLanding(1f);
            for (int i = 0; i < 2; i++) Require(animator.GetTreasure(i).transform.localPosition == positions[i], "Airborne chest must leave loose treasure grounded.");
            animator.NotifyLanding();
            animator.TickLanding(0.055f);
            Require(animator.GetTreasure(0).transform.localScale.y < 1f, "Treasure must react to the impact with a short compression.");
            animator.TickLanding(0.08f);
            Require(animator.GetTreasure(0).transform.localPosition.y > positions[0].y, "Loose treasure must rebound after impact.");
            animator.TickLanding(1f);
            for (int i = 0; i < 2; i++) Require(animator.GetTreasure(i).transform.localPosition == positions[i] &&
                animator.GetTreasure(i).transform.localScale == Vector3.one, "Treasure must settle exactly after impact.");
        }

        private static void VerifyRealVaultJump()
        {
            GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss2Setup.BossPrefabPath));
            GameObject target = new GameObject("Vault movement verification target");
            try
            {
                target.AddComponent<Health>();
                target.transform.position = new Vector3(100f, 100f, 0f);
                BossMovementAnimator animator = root.GetComponent<BossMovementAnimator>();
                Invoke(animator, "Awake");
                animator.RenderPose(0f, false);
                SaemaeumVaultBossPatternRuntime runtime = root.GetComponent<SaemaeumVaultBossPatternRuntime>();
                Invoke(runtime, "Awake");
                runtime.BeginCombat(target.transform, 42, 0f);
                runtime.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence, 5f);
                Require(runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence), "Could not start real vault jump.");
                runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence, 0.02f);
                animator.RenderPose(0f, false);
                Require(animator.Artwork.sprite == animator.GetHopFrame(1), "Real jump preparation must select the drawn pose.");
                Vector3 crouchFloor = animator.Source.transform.TransformPoint(Vector3.up * animator.BodySprite.bounds.min.y);
                Require(runtime.CurrentVisualScale == Vector3.one, "Vault preparation must keep original proportions while deformation is disabled.");
                float floorY = root.transform.position.y + animator.BodySprite.bounds.min.y * root.transform.lossyScale.y;
                Require(Mathf.Abs(crouchFloor.y - floorY) < 0.001f, "Crouching must keep the body bottom grounded.");
                runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence, 0.345f);
                animator.RenderPose(0f, false);
                Require(animator.Artwork.sprite == animator.GetHopFrame(2), "Real attack flight must select the drawn airborne pose.");
                Require(runtime.IsJumping && animator.Source.transform.position.y - root.transform.position.y > 1.9f,
                    "Pattern jump must preserve its authored visual height.");
                Require(animator.GetTreasure(0).transform.localPosition == Vector3.zero && animator.GetTreasure(1).transform.localPosition == Vector3.zero,
                    "Ground treasure must stay on the root trajectory during a real attack jump.");
                Require(runtime.CurrentVisualScale.y >= 1f, "Airborne vault must not use crouch/landing compression.");
                runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence, 0.69f * 0.86f);
                animator.RenderPose(0f, false);
                Require(animator.Artwork.sprite == animator.GetHopFrame(3), "Real attack landing must select the drawn landing pose.");
                Vector3 landingFloor = animator.Source.transform.TransformPoint(Vector3.up * animator.BodySprite.bounds.min.y);
                Require(runtime.CurrentVisualScale == Vector3.one && Mathf.Abs(landingFloor.y -
                        (root.transform.position.y + animator.BodySprite.bounds.min.y * root.transform.lossyScale.y)) < 0.001f,
                    "Landing must keep original proportions and a fixed bottom while deformation is disabled.");
                Require(animator.HasLandingImpulse, "Treasure impulse must begin at visible contact.");
                runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence, 0.7f);
                Require(animator.HasLandingImpulse, "Actual landing must notify treasure even when the player is outside damage range.");
                Invoke(runtime, "OnDisable");
                Require(!root.GetComponent<Collider2D>().isTrigger, "Motion integration must preserve jump collider restoration.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(target); }
        }

        private static void VerifyMinionArtwork()
        {
            foreach (string path in new[] { Week15Boss3Setup.ArcherPrefabPath, Week15Boss3Setup.MagePrefabPath,
                Week15Boss3Setup.AxePrefabPath, Week15Boss3Setup.ShieldPrefabPath })
            {
                GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                    Sprite original = renderer.sprite;
                    EnemyMovementAnimator animator = root.GetComponent<EnemyMovementAnimator>();
                    if (animator != null)
                    {
                        Invoke(animator, "Awake");
                        animator.RenderPose(0.5f, true);
                        Require(animator.WalkFrameCount == 4 && renderer.sprite == animator.GetWalkFrame(2),
                            "Each boss minion must play its own four-pose walk cycle.");
                        animator.RenderPose(0f, false);
                    }
                    Require(renderer.sprite == original, "Boss minions must not inherit another species' walking artwork.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }

        public static void SetupAndVerifyBatch()
        {
            EnemyMovementAnimationSetup.Setup();
            BossMovementAnimationSetup.Setup();
            string[] guids = BossMovementAnimationSetup.PrefabPaths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            BossMovementAnimationSetup.Setup();
            for (int i = 0; i < guids.Length; i++) Require(guids[i] == AssetDatabase.AssetPathToGUID(BossMovementAnimationSetup.PrefabPaths[i]), "Boss GUID changed on setup rerun.");
            Verify();
            EnemyMovementAnimationVerification.Verify();
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            Week15Boss3Verification.Verify();
        }

        [MenuItem("Trickal Fan Game/Artwork/Export Boss Movement Preview")]
        public static void ExportPreview()
        {
            SetupAndVerifyBatch();
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/BossDrawnHopPreview"));
            Directory.CreateDirectory(folder);
            GameObject[] roots = new GameObject[3];
            RenderTexture target = new RenderTexture(1200, 570, 0);
            RenderTexture previous = RenderTexture.active;
            Texture2D capture = new Texture2D(1200, 570, TextureFormat.RGB24, false);
            Material material = new Material(AssetDatabase.LoadAssetAtPath<Material>(EnemyMovementAnimationSetup.MaterialPath));
            try
            {
                target.Create();
                for (int i = 0; i < roots.Length; i++)
                {
                    roots[i] = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BossMovementAnimationSetup.PrefabPaths[i]));
                    Invoke(roots[i].GetComponent<BossMovementAnimator>(), "Awake");
                }
                for (int frame = 0; frame < 120; frame++)
                {
                    for (int i = 0; i < roots.Length; i++)
                    {
                        BossMovementAnimator animator = roots[i].GetComponent<BossMovementAnimator>();
                        // Match the prefab's actual travel speed instead of showing an arbitrarily slow stride.
                        float cyclesPerSecond = i == 0 ? 2.25f / 1.35f : i == 1 ? 1.8f / 1.35f : 1f;
                        float phase = Mathf.Repeat(frame * 0.025f * cyclesPerSecond, 1f);
                        float previousPhase = Mathf.Repeat((frame - 1) * 0.025f * cyclesPerSecond, 1f);
                        animator.RenderPose(phase, true);
                        if (i == 1 && previousPhase < 0.86f && phase >= 0.86f) animator.NotifyLanding(0.55f);
                        animator.TickLanding(0.025f);
                    }
                    DrawFrame(roots, target, capture, material);
                    File.WriteAllBytes(Path.Combine(folder, $"move-{frame:D2}.png"), capture.EncodeToPNG());
                }
                // A second sequence follows the real attack runtime: floor gems stay put until ResolveLanding.
                SaemaeumVaultBossPatternRuntime vault = roots[1].GetComponent<SaemaeumVaultBossPatternRuntime>();
                Invoke(vault, "Awake");
                vault.BeginCombat(null, 42, 0f);
                vault.ConfigureJumps(1, 1, 1, 1, 4.8f, 2.875f, EnemyDamageTier.Medium, 7f, 0.2f);
                vault.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence, 5f);
                vault.TryExecute(BossPatternExecution.SaemaeumJumpSequence);
                roots[1].GetComponent<BossMovementAnimator>().TickLanding(1f);
                for (int frame = 0; frame < 36; frame++)
                {
                    float now = frame * 0.025f;
                    if (now < 0.7f) vault.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence, now);
                    else if (frame == 28)
                    {
                        vault.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence, now);
                        vault.OnPatternStateChanged(BossActionState.Recovery, BossPatternExecution.SaemaeumJumpSequence, now + 1f);
                    }
                    for (int i = 0; i < roots.Length; i++)
                    {
                        BossMovementAnimator animator = roots[i].GetComponent<BossMovementAnimator>();
                        animator.RenderPose(frame / 32f, i != 1);
                        animator.TickLanding(0.025f);
                    }
                    DrawFrame(roots, target, capture, material, true);
                    File.WriteAllBytes(Path.Combine(folder, $"jump-{frame:D2}.png"), capture.EncodeToPNG());
                }
                Debug.Log("Boss movement previews exported to " + folder);
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

        private static void DrawFrame(GameObject[] roots, RenderTexture target, Texture2D capture, Material material, bool attackJump = false)
        {
            RenderTexture.active = target;
            GL.Clear(true, true, new Color(0.15f, 0.19f, 0.17f));
            GL.PushMatrix();
            try
            {
                GL.LoadProjectionMatrix(Matrix4x4.Ortho(0f, 6f, -0.3f, attackJump ? 3.2f : 2.55f, -10f, 10f));
                GL.modelview = Matrix4x4.identity;
                for (int i = 0; i < roots.Length; i++)
                {
                    SpriteRenderer[] renderers = roots[i].GetComponentsInChildren<SpriteRenderer>()
                        .Where(r => r.enabled && !r.forceRenderingOff).OrderBy(r => r.sortingOrder).ToArray();
                    foreach (SpriteRenderer renderer in renderers)
                    {
                        Sprite sprite = renderer.sprite;
                        if (sprite == null) continue;
                        Mesh mesh = new Mesh();
                        mesh.vertices = sprite.vertices.Select(v => new Vector3(v.x, v.y, 0f)).ToArray();
                        mesh.uv = sprite.uv;
                        mesh.triangles = sprite.triangles.Select(t => (int)t).ToArray();
                        material.SetTexture("_MainTex", sprite.texture);
                        material.SetColor("_Tint", renderer.color);
                        Require(material.SetPass(0), "Boss preview material failed.");
                        Matrix4x4 display = Matrix4x4.TRS(new Vector3(i * 2f + 1f, 0.82f, 0f), Quaternion.identity,
                            Vector3.one * (attackJump ? 1f : 1.2f));
                        Graphics.DrawMeshNow(mesh, display * roots[i].transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix);
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }
                }
            }
            finally { GL.PopMatrix(); }
            capture.ReadPixels(new Rect(0f, 0f, 1200f, 570f), 0, 0);
            capture.Apply();
        }

        private static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }
    }
}
