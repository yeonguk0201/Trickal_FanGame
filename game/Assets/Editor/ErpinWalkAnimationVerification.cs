using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TrickalFanGame.Editor
{
    public static class ErpinWalkAnimationVerification
    {
        private const string ResourcePath = "Characters/Erpin_Walking";
        private const string SkillResourcePath = "Characters/Erpin_lowGrade_skill";

        [MenuItem("Tools/Trickal/Verify Erpin Walk Animation")]
        public static void Verify()
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(ResourcePath);
            Assert(sprites.Length == 12, $"Expected 12 Erpin walking frames, but found {sprites.Length}.");

            string[] directions = { "Down", "Side", "Up" };
            foreach (string direction in directions)
            {
                for (int frame = 0; frame < 4; frame++)
                {
                    string expectedName = $"Erpin_Walk_{direction}_{frame}";
                    Sprite sprite = sprites.SingleOrDefault(candidate => candidate.name == expectedName);
                    Assert(sprite != null, $"Missing walking frame '{expectedName}'.");
                    Assert(sprite.rect.width == 362f && sprite.rect.height == 362f,
                        $"Walking frame '{expectedName}' must be 362x362 pixels.");
                }
            }

            Sprite[] skillSprites = Resources.LoadAll<Sprite>(SkillResourcePath);
            Assert(skillSprites.Length == 4, $"Expected 4 Erpin lower-grade skill frames, but found {skillSprites.Length}.");
            for (int frame = 0; frame < 4; frame++)
            {
                string expectedName = $"Erpin_lowGrade_skill_{frame}";
                Sprite sprite = skillSprites.SingleOrDefault(candidate => candidate.name == expectedName);
                Assert(sprite != null, $"Missing skill frame '{expectedName}'.");
                Assert(sprite.rect.width == 605f && sprite.rect.height == 724f,
                    $"Skill frame '{expectedName}' must be 605x724 pixels so the pose does not jitter.");
                Assert(Mathf.Approximately(sprite.pixelsPerUnit, 430f),
                    $"Skill frame '{expectedName}' must use 430 pixels per unit to match the walking scale.");
            }

            GameObject player = new("ErpinWalkVerification")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                player.AddComponent<SpriteRenderer>();
                PlayerMovement movement = player.AddComponent<PlayerMovement>();
                MethodInfo awake = typeof(PlayerMovement).GetMethod("Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(awake != null, "PlayerMovement.Awake could not be found.");
                awake.Invoke(movement, null);
                PlayerWalkAnimator animator = player.GetComponent<PlayerWalkAnimator>();
                Assert(animator != null,
                    "PlayerMovement must attach PlayerWalkAnimator when a SpriteRenderer is present.");
                MethodInfo animatorAwake = typeof(PlayerWalkAnimator).GetMethod("Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(animatorAwake != null, "PlayerWalkAnimator.Awake could not be found.");
                animatorAwake.Invoke(animator, null);
                VerifyAttackFacing(player, movement, animator);
                animator.PlaySkill();
                Assert(animator.IsPlayingSkill, "PlayerWalkAnimator must start skill playback when skill frames are loaded.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }

            Debug.Log("Erpin walk animation verification passed: 12 directional frames, 4 skill frames and runtime wiring are present.");
        }

        private static void VerifyAttackFacing(GameObject player, PlayerMovement movement, PlayerWalkAnimator animator)
        {
            Keyboard previousKeyboard = Keyboard.current;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                keyboard.MakeCurrent();
                PlayerProjectileAttack attack = player.AddComponent<PlayerProjectileAttack>();
                SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
                MethodInfo update = typeof(PlayerMovement).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo render = typeof(PlayerWalkAnimator).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                InputState.Change(keyboard, new KeyboardState(Key.D, Key.LeftArrow));
                update.Invoke(movement, null);
                render.Invoke(animator, null);
                Assert(movement.MovementIntent == Vector2.right && movement.FacingDirection == Vector2.left &&
                       !renderer.flipX && renderer.sprite.name.StartsWith("Erpin_Walk_Side_"),
                    "Moving right while attacking left must keep rightward movement and left-facing side artwork.");
                InputState.Change(keyboard, new KeyboardState(Key.A, Key.RightArrow));
                update.Invoke(movement, null);
                Assert(movement.MovementIntent == Vector2.left && movement.FacingDirection == Vector2.right && renderer.flipX,
                    "Moving left while attacking right must face right without reversing movement.");
                InputState.Change(keyboard, new KeyboardState(Key.D, Key.UpArrow));
                update.Invoke(movement, null);
                render.Invoke(animator, null);
                Assert(renderer.sprite.name.StartsWith("Erpin_Walk_Up_"), "Upward attacks must select the rear-facing walk frames.");
                InputState.Change(keyboard, new KeyboardState(Key.LeftArrow));
                update.Invoke(movement, null);
                render.Invoke(animator, null);
                Assert(!movement.IsMoving && movement.IsAimingAttack && !renderer.flipX &&
                       renderer.sprite.name == "Erpin_Walk_Side_0", "Stationary attacks must also face the attack direction.");
                InputState.Change(keyboard, new KeyboardState(Key.D));
                update.Invoke(movement, null);
                Assert(!movement.IsAimingAttack && renderer.flipX, "Releasing attack must restore movement facing.");
                attack.enabled = false;
                InputState.Change(keyboard, new KeyboardState(Key.D, Key.LeftArrow));
                update.Invoke(movement, null);
                Assert(!movement.IsAimingAttack && renderer.flipX, "Disabled attacks must not override movement facing.");
                player.AddComponent<PlayerAttack>();
                update.Invoke(movement, null);
                Assert(movement.IsAimingAttack && !renderer.flipX, "The melee attack must share the same facing priority.");
                PlayerActionState state = player.GetComponent<PlayerActionState>();
                Assert(state.TryBeginUltimate(Vector2.right, 2f), "Dash setup failed.");
                update.Invoke(movement, null);
                Assert(!movement.IsAimingAttack && movement.FacingDirection == Vector2.right,
                    "Ultimate dash direction must retain priority while basic attacks are blocked.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (previousKeyboard != null) previousKeyboard.MakeCurrent();
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
