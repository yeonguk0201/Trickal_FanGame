using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class BossSummonJumpAnimationVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Boss Summon And Jump Animations")]
        public static void Verify()
        {
            VerifySummon(1);
            VerifySummon(2);
            VerifyJump();
            Debug.Log("Boss summon/jump animation verification passed: normal/golden commands, summon event, cancellation, eight jump poses and grounded recovery.");
        }

        private static void VerifySummon(int phase)
        {
            GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss3Setup.BossPrefabPath));
            GameObject target = new GameObject("Summon artwork target");
            try
            {
                var health = root.GetComponent<Health>();
                var boss = root.GetComponent<BossController>();
                var runtime = root.GetComponent<CrayonHeroBossPatternRuntime>();
                var animator = root.GetComponent<BossMovementAnimator>();
                Invoke(health, "Awake"); Invoke(boss, "Awake"); Invoke(runtime, "Awake"); Invoke(animator, "Awake");
                Set(boss, "currentPhase", phase);
                target.transform.position = root.transform.position + Vector3.left * 3f;
                runtime.BeginCombat(target.transform, 42, 0f);
                // Avoid creating gameplay minions in an artwork check; TryExecute still owns the summon event.
                runtime.ConfigureSummons(Array.Empty<GameObject>(), 4, 2.5f, 0.15f);
                Require(animator.SummonFrameCount == 4 && animator.GoldenSummonFrameCount == 4, "Both command sheets need four poses.");
                for (int i = 0; i < 4; i++) CheckSprite(animator.GetSummonFrame(i, phase == 2), 1024);
                var execution = BossPatternExecution.CrayonHeroSummonMinions;
                runtime.OnPatternStateChanged(BossActionState.Telegraph, execution, 1f);
                animator.RenderPose(0f, false);
                Require(animator.Artwork.sprite == animator.GetSummonFrame(0, phase == 2), "Preparation must select phase artwork.");
                runtime.TickPattern(BossActionState.Telegraph, execution, 0.6f);
                animator.RenderPose(0f, false);
                Require(animator.Artwork.sprite == animator.GetSummonFrame(1, phase == 2), "Command must precede the actual summon.");
                Require(runtime.TryExecute(execution) && !runtime.SummonAvailable, "Summon event must still consume availability.");
                animator.RenderPose(0f, false);
                Require(animator.Artwork.sprite == animator.GetSummonFrame(2, phase == 2), "Actual summon must hold the extended sword.");
                runtime.OnPatternStateChanged(BossActionState.Recovery, execution, 2f);
                animator.RenderPose(0f, false);
                Require(animator.Artwork.sprite == animator.GetSummonFrame(3, phase == 2), "Recovery must retract the sword.");
                runtime.OnPatternStateChanged(BossActionState.Idle, execution, 3f);
                animator.RenderPose(0f, false);
                Require(runtime.SummonPoseIndex == -1 && animator.Artwork.sprite != animator.GetSummonFrame(3, phase == 2), "Cancellation must restore phase idle.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(target); }
        }

        private static void VerifyJump()
        {
            GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss2Setup.BossPrefabPath));
            try
            {
                var runtime = root.GetComponent<SaemaeumVaultBossPatternRuntime>();
                var animator = root.GetComponent<BossMovementAnimator>();
                Invoke(root.GetComponent<Health>(), "Awake"); Invoke(root.GetComponent<BossController>(), "Awake");
                Invoke(runtime, "Awake"); Invoke(animator, "Awake");
                Require(animator.JumpCycleFrameCount == 8, "Vault needs all eight jump poses.");
                for (int i = 0; i < 8; i++) CheckSprite(animator.GetJumpCycleFrame(i), 768);
                runtime.BeginCombat(null, 42, 0f);
                var execution = BossPatternExecution.SaemaeumJumpSequence;
                runtime.OnPatternStateChanged(BossActionState.Active, execution, 5f);
                Require(runtime.TryExecute(execution), "Vault jump must start.");
                float[] samples = { 0f, 0.08f, 0.25f, 0.48f, 0.7f, 0.86f, 0.93f, 0.98f };
                for (int i = 0; i < samples.Length; i++)
                {
                    runtime.TickPattern(BossActionState.Active, execution, samples[i] * runtime.PhaseOneJumpDuration);
                    animator.RenderPose(0f, false);
                    Require(animator.Artwork.sprite == animator.GetJumpCycleFrame(i), "Jump must show ordered anticipation, flight, impact and recovery.");
                    Require(animator.Artwork.transform.localScale == Vector3.one, "Drawn deformation must not be scaled twice.");
                    Require(animator.GetTreasure(0).transform.localPosition == Vector3.zero, "Ground treasure must not follow flight artwork.");
                }
                runtime.OnPatternStateChanged(BossActionState.Idle, execution, 2f);
                Require(runtime.JumpCycleFrameIndex == -1 && !root.GetComponent<Collider2D>().isTrigger, "Cancellation must reset pose and physics.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CheckSprite(Sprite sprite, int size) => Require(sprite != null &&
            sprite.rect.size == new Vector2(size, size) && sprite.pivot == new Vector2(size / 2f, size / 2f) &&
            Mathf.Approximately(sprite.pixelsPerUnit, 400f), "Artwork must share a centered canvas and 400 PPU.");
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
