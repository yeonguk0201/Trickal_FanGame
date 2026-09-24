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
                Assert(player.GetComponent<PlayerWalkAnimator>() != null,
                    "PlayerMovement must attach PlayerWalkAnimator when a SpriteRenderer is present.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }

            Debug.Log("Erpin walk animation verification passed: 12 directional frames imported and runtime wiring is present.");
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
