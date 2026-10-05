using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class VaultHealingAnimationVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Vault Healing Animation")]
        public static void Verify()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss2Setup.BossPrefabPath);
            Require(prefab != null, "Vault prefab missing.");
            GameObject root = UnityEngine.Object.Instantiate(prefab);
            GameObject target = new GameObject("Vault heal verification target");
            try
            {
                var animator = root.GetComponent<BossMovementAnimator>();
                var runtime = root.GetComponent<SaemaeumVaultBossPatternRuntime>();
                var boss = root.GetComponent<BossController>();
                var health = root.GetComponent<Health>();
                Invoke(health, "Awake");
                Invoke(boss, "Awake");
                Invoke(runtime, "Awake");
                Invoke(animator, "Awake");
                Require(animator.HealFrameCount == 4, "Vault needs four healing poses.");
                for (int i = 0; i < 4; i++)
                {
                    Sprite frame = animator.GetHealFrame(i);
                    Require(frame != null && frame.rect.size == new Vector2(640, 512) &&
                        frame.pivot == new Vector2(320, 256) && Mathf.Approximately(frame.pixelsPerUnit, 400f),
                        "Healing frames must retain the body canvas and scale.");
                }
                runtime.BeginCombat(target.transform, 42, 0f);
                Vector3 position = root.transform.position;
                Collider2D collider = root.GetComponent<Collider2D>();
                bool trigger = collider.isTrigger;
                Vector2 offset = collider.offset;
                runtime.OnPatternStateChanged(BossActionState.Telegraph, BossPatternExecution.SaemaeumTreasureHeal, 0.8f);
                animator.RenderPose(0f, false);
                Require(animator.Artwork.sprite == animator.GetHealFrame(0), "Heal telegraph must turn toward the gem.");
                health.TakeDamage(300f);
                float before = health.CurrentHealth;
                runtime.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.SaemaeumTreasureHeal, 1.8f);
                runtime.TryExecute(BossPatternExecution.SaemaeumTreasureHeal);
                foreach (float time in new[] { 0f, 0.13f, 0.45f, 0.9f, 1.35f, 1.79f })
                {
                    runtime.TickPattern(BossActionState.Active, BossPatternExecution.SaemaeumTreasureHeal, time);
                    animator.RenderPose(0.52f, true);
                    int expected = 1 + Mathf.FloorToInt(time * 8f) % 2;
                    Require(animator.Artwork.sprite == animator.GetHealFrame(expected), "Active healing must chew instead of hopping.");
                    Require(animator.Artwork.transform.localPosition == Vector3.zero &&
                        animator.Artwork.transform.localScale == Vector3.one, "Eating must stay grounded without stretching.");
                }
                Require(Mathf.Approximately(health.CurrentHealth - before, runtime.HealPerUse), "Artwork must preserve total healing.");
                runtime.OnPatternStateChanged(BossActionState.Recovery, BossPatternExecution.SaemaeumTreasureHeal, 2.85f);
                animator.RenderPose(0f, false);
                Require(animator.Artwork.sprite == animator.GetHealFrame(3), "Recovery must turn back toward the player.");
                runtime.TickPattern(BossActionState.Recovery, BossPatternExecution.SaemaeumTreasureHeal, 2f);
                animator.RenderPose(0f, false);
                Require(animator.Artwork.sprite == animator.GetHopFrame(0), "Recovery must restore the original front pose.");
                runtime.OnPatternStateChanged(BossActionState.Telegraph, BossPatternExecution.SaemaeumTreasureHeal, 3f);
                runtime.OnPatternStateChanged(BossActionState.PhaseTransition, BossPatternExecution.SaemaeumTreasureHeal, 3f);
                Require(runtime.HealPoseIndex == -1, "Phase transition must cancel eating.");
                runtime.OnPatternStateChanged(BossActionState.Telegraph, BossPatternExecution.SaemaeumTreasureHeal, 3f);
                Invoke(runtime, "OnDisable");
                Require(runtime.HealPoseIndex == -1, "Disable must clear healing artwork.");
                Require(root.transform.position == position && collider.isTrigger == trigger && collider.offset == offset,
                    "Healing artwork must preserve physical position and collision.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(target);
            }
            Debug.Log("Vault healing animation verification passed: turn, rear chewing, healing amount, front restoration and cancellation.");
        }

        private static void Invoke(object target, string method)
        {
            MethodInfo hook = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(hook != null, "Missing verification hook: " + method);
            hook.Invoke(target, null);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
