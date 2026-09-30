using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

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
                animator.PlaySkill();
                Assert(animator.IsPlayingSkill, "PlayerWalkAnimator must start skill playback when skill frames are loaded.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }

            Debug.Log("Erpin walk animation verification passed: 12 directional frames, 4 skill frames and runtime wiring are present.");
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
