using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class CrayonHeroAttackAnimationVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Crayon Hero Attack Animations")]
        public static void Verify()
        {
            VerifySlam(1f);
            VerifySlam(0.78f);
            VerifySwingAndDash();
            VerifySwingAndDash(2);
            VerifyAwakening();
            BossMovementAnimationVerification.Verify();
            Debug.Log("Crayon Hero attack animation verification passed: overhead swing, alternating dash, three timed charges, impact damage and cancellation.");
        }

        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week15Boss3Verification.Verify();
        }

        [MenuItem("Trickal Fan Game/Artwork/Verify Crayon Hero Golden Attacks")]
        public static void VerifyGoldenAttacks()
        {
            VerifySlam(0.78f);
            VerifySwingAndDash(2);
            VerifyAwakening();
            Debug.Log("Crayon Hero golden attack verification passed: overhead swing, alternating diagonals, three golden charge stages synchronized to damage, and awakened idle restoration.");
        }

        private static GameObject Create(out CrayonHeroBossPatternRuntime runtime,
            out BossMovementAnimator animator, out GameObject target)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss3Setup.BossPrefabPath);
            Require(prefab != null, "Crayon prefab missing.");
            GameObject root = UnityEngine.Object.Instantiate(prefab);
            runtime = root.GetComponent<CrayonHeroBossPatternRuntime>();
            animator = root.GetComponent<BossMovementAnimator>();
            Invoke(root.GetComponent<Health>(), "Awake");
            Invoke(root.GetComponent<BossController>(), "Awake");
            Invoke(runtime, "Awake");
            Invoke(animator, "Awake");
            Require(animator.CrayonAttackFrameCount == 14, "Attacks require three distinct accumulating charge stages.");
            for (int i = 0; i < 14; i++)
            {
                Sprite frame = animator.GetCrayonAttackFrame(i);
                Require(frame != null && frame.rect.size == new Vector2(1024, 1024) &&
                    frame.pivot == new Vector2(512, 512) && Mathf.Approximately(frame.pixelsPerUnit, 400f),
                    "Attack frames must share registered canvas and PPU.");
                VerifyIsolatedArtwork(frame);
            }
            target = new GameObject("Crayon attack timing target");
            target.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            Health health = target.AddComponent<Health>();
            Set(health, "maxHealth", 100f);
            target.AddComponent<PlayerMovement>();
            Invoke(health, "Awake");
            return root;
        }

        private static void VerifyIsolatedArtwork(Sprite frame)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Require(texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(frame))), "Cannot inspect attack PNG.");
                Color32[] pixels = texture.GetPixels32();
                int width = texture.width, height = texture.height;
                bool[] visited = new bool[pixels.Length];
                var components = new List<int>();
                var queue = new Queue<int>();
                for (int index = 0; index < pixels.Length; index++)
                {
                    if (pixels[index].a == 0 || visited[index]) continue;
                    int count = 0;
                    queue.Enqueue(index);
                    visited[index] = true;
                    while (queue.Count > 0)
                    {
                        int current = queue.Dequeue(), x = current % width, y = current / width;
                        count++;
                        Require(x >= 8 && y >= 8 && x < width - 8 && y < height - 8,
                            "Artwork/effect touches its canvas border: " + frame.name);
                        foreach (int next in new[] { x > 0 ? current - 1 : -1, x < width - 1 ? current + 1 : -1,
                            y > 0 ? current - width : -1, y < height - 1 ? current + width : -1 })
                        {
                            if (next < 0 || visited[next] || pixels[next].a == 0) continue;
                            visited[next] = true;
                            queue.Enqueue(next);
                        }
                    }
                    components.Add(count);
                }
                components.Sort((a, b) => b.CompareTo(a));
                Require(components.Count > 0 && (components.Count == 1 || components[1] <= 24),
                    "Detached fragment from another pose in " + frame.name);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void VerifySlam(float tempo)
        {
            GameObject root = Create(out var runtime, out var animator, out var target);
            try
            {
                if (tempo < 1f) Set(root.GetComponent<BossController>(), "currentPhase", 2);
                target.transform.position = Vector2.up * 8f;
                runtime.BeginCombat(target.transform, 42, 0f);
                var pattern = root.GetComponent<BossController>().Patterns.First(p => p.Execution == BossPatternExecution.CrayonHeroMapSlash);
                float telegraphEnd = pattern.GetTelegraphDuration(tempo);
                float impact = telegraphEnd + runtime.SlashLockedDelay;
                Require(Mathf.Abs(impact - (tempo < 1f ? 2.564f : 2.85f)) < 0.0001f,
                    "Both phases must gain the full 1.35 seconds of charge time.");
                runtime.OnPatternStateChanged(BossActionState.Telegraph, BossPatternExecution.CrayonHeroMapSlash, telegraphEnd);
                foreach (float aimX in new[] { -8f, 8f, -8f })
                {
                    target.transform.position = Vector2.right * aimX;
                    runtime.TickPattern(BossActionState.Telegraph, BossPatternExecution.CrayonHeroMapSlash, 0.01f);
                    animator.RenderPose(0f, false);
                    Require(animator.Source.flipX == (aimX > 0f) && animator.Artwork.flipX == (aimX > 0f),
                        "Stationary charging must turn toward the target in both phases.");
                }
                target.transform.position = Vector2.up * 8f;
                runtime.TickPattern(BossActionState.Telegraph, BossPatternExecution.CrayonHeroMapSlash, 0.01f);
                animator.RenderPose(0f, false);
                Require(!animator.Artwork.flipX, "Vertical charge aim must preserve the last horizontal facing.");
                bool active = false;
                void Tick(float now)
                {
                    if (!active && now >= telegraphEnd)
                    {
                        runtime.TickPattern(BossActionState.Telegraph, BossPatternExecution.CrayonHeroMapSlash, telegraphEnd);
                        runtime.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.CrayonHeroMapSlash, telegraphEnd + pattern.ActiveDuration * tempo);
                        runtime.TryExecute(BossPatternExecution.CrayonHeroMapSlash);
                        active = true;
                    }
                    runtime.TickPattern(active ? BossActionState.Active : BossActionState.Telegraph,
                        BossPatternExecution.CrayonHeroMapSlash, now);
                    animator.RenderPose(0.5f, true);
                }
                float hp = target.GetComponent<Health>().CurrentHealth;
                for (int beat = 0; beat < 3; beat++)
                {
                    int expectedPose = beat == 0 ? 9 : beat == 1 ? 12 : 13;
                    Tick(impact * beat / 3f + 0.001f);
                    Require(runtime.ChargeBeat == beat && runtime.AttackPoseIndex == expectedPose,
                        "Aura must grow at each real one-third charge boundary.");
                    Tick(impact * (beat + 0.5f) / 3f);
                    Require(runtime.ChargeBeat == beat && runtime.AttackPoseIndex == expectedPose &&
                        animator.Artwork.sprite == (tempo < 1f ? animator.GetGoldenAttackFrame(expectedPose) : animator.GetCrayonAttackFrame(expectedPose)), "Each timing interval must show its distinct accumulated aura.");
                    Tick(impact * (beat + 0.97f) / 3f);
                    Require(runtime.AttackPoseIndex == expectedPose, "Accumulated aura must remain visible instead of blinking off.");
                    Require(Mathf.Approximately(target.GetComponent<Health>().CurrentHealth, hp), "Charging cannot deal early damage.");
                }
                Tick(impact - 0.001f);
                Require(runtime.AttackPoseIndex == 13 && target.GetComponent<Health>().CurrentHealth == hp,
                    "Raised sword must persist until the real damage deadline.");
                Tick(impact + 0.001f);
                Require(Mathf.Approximately(runtime.ChargeImpactAt, impact) && runtime.AttackPoseIndex == 10 &&
                    animator.Artwork.sprite == (tempo < 1f ? animator.GetGoldenAttackFrame(10) : animator.GetCrayonAttackFrame(10)) && target.GetComponent<Health>().CurrentHealth < hp,
                    "Slam artwork and actual damage must occur together after three beats.");
                float after = target.GetComponent<Health>().CurrentHealth;
                Tick(impact + 0.09f);
                Require(runtime.AttackPoseIndex == 11 && target.GetComponent<Health>().CurrentHealth == after,
                    "Follow-through cannot duplicate damage.");
                runtime.OnPatternStateChanged(BossActionState.PhaseTransition, BossPatternExecution.CrayonHeroMapSlash, 3f);
                Require(runtime.AttackPoseIndex == -1 && runtime.ChargeBeat == -1, "Phase transition must clear charging.");
                Invoke(runtime, "OnDisable");
                Require(runtime.AttackPoseIndex == -1, "Disable must clear attack art.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(target); }
        }

        private static void VerifySwingAndDash(int phase = 1)
        {
            GameObject root = Create(out var runtime, out var animator, out var target);
            try
            {
                Set(root.GetComponent<BossController>(), "currentPhase", phase);
                target.transform.position = Vector2.up * 2f;
                runtime.BeginCombat(target.transform, 42, 0f);
                runtime.TryExecute(BossPatternExecution.CrayonHeroApproachSwing);
                runtime.TickPattern(BossActionState.Active, BossPatternExecution.CrayonHeroApproachSwing, 0f);
                Require(runtime.AttackPoseIndex == 0, "Swing must raise its sword.");
                runtime.TickPattern(BossActionState.Active, BossPatternExecution.CrayonHeroApproachSwing, 0.2f);
                Require(runtime.AttackPoseIndex == 1, "Swing must hold overhead until its windup completes.");
                runtime.TickPattern(BossActionState.Active, BossPatternExecution.CrayonHeroApproachSwing, 0.301f);
                Require(runtime.AttackPoseIndex == 2 && runtime.CompletedSwings == 1, "Downstroke must coincide with swing resolution.");
                runtime.TickPattern(BossActionState.Active, BossPatternExecution.CrayonHeroApproachSwing, 0.39f);
                Require(runtime.AttackPoseIndex == 3, "Swing must follow through.");
                runtime.BeginCombat(target.transform, 42, 0f);
                runtime.TryExecute(BossPatternExecution.CrayonHeroDashChain);
                Set(runtime, "dashCountTarget", 3);
                Require(runtime.AttackPoseIndex == 5, "First dash must cut down diagonally.");
                float now = 0f;
                for (int completed = 1; completed < 3; completed++)
                {
                    now += runtime.DashDuration + 0.001f;
                    runtime.TickPattern(BossActionState.Active, BossPatternExecution.CrayonHeroDashChain, now);
                    Require(runtime.AttackPoseIndex == (completed % 2 == 0 ? 4 : 6), "Retarget must prepare the opposite diagonal.");
                    now += runtime.DashRetargetDuration + 0.001f;
                    runtime.TickPattern(BossActionState.Active, BossPatternExecution.CrayonHeroDashChain, now);
                    int pose = completed % 2 == 0 ? 5 : 7;
                    animator.RenderPose(0.5f, true);
                    Require(runtime.AttackPoseIndex == pose && animator.Artwork.sprite == (phase >= 2
                        ? animator.GetGoldenAttackFrame(pose) : animator.GetCrayonAttackFrame(pose)),
                        "Each real dash must alternate diagonal artwork instead of walking.");
                }
                runtime.OnPatternStateChanged(BossActionState.Defeated, BossPatternExecution.CrayonHeroDashChain, now);
                Require(runtime.AttackPoseIndex == -1, "Defeat must clear attack artwork.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(target); }
        }

        [MenuItem("Trickal Fan Game/Artwork/Verify Crayon Hero Awakening")]
        public static void VerifyAwakening()
        {
            GameObject root = Create(out var runtime, out var animator, out var target);
            try
            {
                var boss = root.GetComponent<BossController>();
                Require(runtime.AwakenedSprite != null && animator.AwakeningFrameCount == 4,
                    "Awakening requires the supplied golden body and four transformation poses.");
                Require(animator.GoldenAttackFrameCount == 14, "Golden phase requires fourteen attack poses.");
                for (int i = 0; i < 14; i++)
                {
                    Sprite attack = animator.GetGoldenAttackFrame(i);
                    Require(attack != null && attack.rect.size == new Vector2(1024, 1024) &&
                        attack.pivot == new Vector2(512, 512) && Mathf.Approximately(attack.pixelsPerUnit, 400f),
                        "Golden attacks must preserve registered canvas and PPU.");
                    VerifyIsolatedArtwork(attack);
                }
                byte[] idle = File.ReadAllBytes(AssetDatabase.GetAssetPath(runtime.AwakenedSprite));
                Require(!idle.SequenceEqual(File.ReadAllBytes(AssetDatabase.GetAssetPath(animator.GetAwakeningFrame(2)))) &&
                    idle.SequenceEqual(File.ReadAllBytes(AssetDatabase.GetAssetPath(animator.GetAwakeningFrame(3)))),
                    "Awakening climax must have its own raised-sword artwork before returning to the supplied idle.");
                for (int i = 0; i < 5; i++)
                {
                    Sprite frame = i == 4 ? runtime.AwakenedSprite : animator.GetAwakeningFrame(i);
                    Require(frame != null && frame.rect.size == new Vector2(1024, 1024) &&
                        frame.pivot == new Vector2(512, 512) && Mathf.Approximately(frame.pixelsPerUnit, 400f),
                        "Awakening artwork must share canvas and PPU.");
                    VerifyIsolatedArtwork(frame);
                }
                Invoke(boss, "SetState", BossActionState.PhaseTransition, Time.time + boss.PhaseTransitionDuration);
                for (int i = 0; i < 4; i++)
                {
                    Set(boss, "stateEndsAt", Time.time + boss.PhaseTransitionDuration * (1f - (i + 0.1f) / 4f));
                    animator.RenderPose(0f, false);
                    Require(animator.Artwork.sprite == animator.GetAwakeningFrame(i),
                        "Transformation must follow the actual phase transition deadline.");
                }
                Set(boss, "currentPhase", 2);
                Invoke(boss, "SetState", BossActionState.Idle, 0f);
                Invoke(runtime, "ApplyPhasePresentation");
                Require(animator.Source.sprite == runtime.AwakenedSprite && animator.Source.color == Color.white,
                    "Phase two must replace the body sprite and remove the old golden color multiplication.");
                for (int i = -1; i < 14; i++)
                {
                    Set(runtime, "attackPoseIndex", i);
                    animator.RenderPose(0.5f, true);
                    Require(animator.Artwork.sprite == (i < 0 ? runtime.AwakenedSprite : animator.GetGoldenAttackFrame(i)) && animator.Artwork.color == Color.white,
                        "Golden attacks must use dedicated artwork and return to the correct awakened body.");
                }
                Set(runtime, "attackPoseIndex", -1);
                Set(boss, "currentPhase", 1);
                Invoke(runtime, "ApplyPhasePresentation");
                animator.RenderPose(0.1f, true);
                Require(animator.Artwork.sprite == animator.GetWalkFrame(0), "Normal phase must retain original artwork.");
                Debug.Log("Crayon Hero awakening verification passed: timed snap transformation, supplied golden body, no color multiplication and phase reset.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(target); }
        }
        private static void Invoke(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
