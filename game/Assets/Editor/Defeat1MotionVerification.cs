using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using AmbushState = TrickalFanGame.Enemy.AmbushEnemyController.AmbushState;

namespace TrickalFanGame.Editor
{
    // Defeat-1: 산사모-광기 walks with drawn frames as tall as its idle drawing, 잠복형 shows its mound and burst in
    // state order at its idle width, and 에르핀 plays four drawn defeat poses on her idle ground line, then holds the
    // last. Every drawing stands where the idle drawing stood and nothing changes physics.
    public static class Defeat1MotionVerification
    {
        private const float Tolerance = 0.02f;

        [MenuItem("Trickal Fan Game/Artwork/Verify Defeat-1 Motions")]
        public static void Verify()
        {
            VerifyWalk();
            VerifyAmbush();
            VerifyErpin();
            Debug.Log("Defeat-1 verification passed: Sansamo madness walks four drawn frames at its idle height and " +
                      "feet, the ambusher shows mound → three burst steps → held → reversed at its idle width, and " +
                      "Erpin falls through four drawn poses on her idle ground line, keeps her shadow and holds the last.");
        }

        private static void VerifyWalk()
        {
            Sprite[] frames = Load(Defeat1MotionSetup.WalkPath);
            Defeat1MotionSetup.IdleSize(Defeat1MotionSetup.SelfDestructPrefab, out _, out float height);
            RequireStanding(frames[0], height, -height / 2f, "Sansamo madness walk");
            WithInstance(Defeat1MotionSetup.SelfDestructPrefab, (root, visual) =>
            {
                var walk = root.GetComponent<EnemyWalkFrames>();
                Require(root.GetComponents<EnemyWalkFrames>().Length == 1 && walk.Frames.Length == 4,
                    "SelfDestructFairy must carry one four-frame walk.");
                for (int i = 0; i < 4; i++) Require(walk.Frames[i] == frames[i], $"Walk frame {i} must be the drawing.");
                Vector3 position = root.transform.position, scale = root.transform.localScale;
                for (int i = 0; i < 4; i++)
                {
                    walk.RenderPose((i + 0.1f) / 4f);
                    Require(visual.sprite == frames[i], $"A walk cycle must show frame {i} in order.");
                }
                walk.RenderPose(1.05f);
                Require(visual.sprite == frames[0], "The walk must loop.");
                Require(root.transform.position == position && root.transform.localScale == scale,
                    "Walking frames must not move or rescale the enemy.");
            });
        }

        private static void VerifyAmbush()
        {
            Sprite[] frames = Load(Defeat1MotionSetup.AmbushPath);
            Defeat1MotionSetup.IdleSize(Defeat1MotionSetup.AmbushPrefab, out float width, out float height);
            Rect burst = WorldVisible(frames[AmbushEnemyMotion.SurfacedFrame]);
            Require(Mathf.Abs(burst.width - width) < Tolerance && Mathf.Abs(burst.yMin + height / 2f) < Tolerance &&
                    Mathf.Abs(burst.center.x) < Tolerance,
                $"The full burst must be as wide as the idle drawing and stand on its feet (got {burst}).");

            const float attack = 0.3f, sink = 0.4f;
            int F(AmbushState state, float t) => AmbushEnemyMotion.FrameFor(state, t, attack, sink);
            Require(F(AmbushState.Burrowed, 0f) == 0 && F(AmbushState.Emerging, 0.6f) == 0,
                "Under the floor and while warning it shows the mound.");
            Require(F(AmbushState.Attacking, 0.01f) == 1 && F(AmbushState.Attacking, 0.11f) == 2 &&
                    F(AmbushState.Attacking, 0.21f) == 3 && F(AmbushState.Attacking, 5f) == 3,
                "The burst must play its three steps in order during the attack.");
            Require(F(AmbushState.Surfaced, 1f) == 3, "Up out of the ground it must stay on the last step.");
            Require(F(AmbushState.Burrowing, 0.01f) == 3 && F(AmbushState.Burrowing, 0.15f) == 2 &&
                    F(AmbushState.Burrowing, 0.3f) == 1, "Sinking must play the steps back.");

            WithInstance(Defeat1MotionSetup.AmbushPrefab, (root, visual) =>
            {
                var motion = root.GetComponent<AmbushEnemyMotion>();
                Require(root.GetComponents<AmbushEnemyMotion>().Length == 1 && motion.HasFrames,
                    "AmbushFairy must carry one four-frame motion.");
                for (int i = 0; i < 4; i++) Require(motion.Frames[i] == frames[i], $"Ambush frame {i} must be the drawing.");
                var controller = root.GetComponent<AmbushEnemyController>();
                motion.RenderPose(controller.StateStartedAt);
                Require(controller.State == AmbushState.Burrowed && visual.sprite == frames[0],
                    "A fresh ambusher must show the mound.");
            });
        }

        private static void VerifyErpin()
        {
            Sprite idle = Defeat1MotionSetup.ErpinIdle();
            Rect idleVisible = Defeat1MotionSetup.VisibleRect(idle);
            float idleHeight = idleVisible.height / idle.pixelsPerUnit;
            float idleBottom = (idleVisible.yMin - idle.pivot.y) / idle.pixelsPerUnit;
            Sprite[] frames = Load(Defeat1MotionSetup.ErpinDeathPath);
            RequireStanding(frames[0], idleHeight, idleBottom, "Erpin's first defeat pose");
            for (int i = 1; i < 4; i++)
                Require(Mathf.Approximately(frames[i].pixelsPerUnit, frames[0].pixelsPerUnit) &&
                        frames[i].pivot == frames[0].pivot, "Erpin's defeat poses must share one scale and pivot.");

            GameObject player = new("Defeat-1 Erpin fixture");
            try
            {
                SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
                renderer.sprite = idle;
                Health health = player.AddComponent<Health>();
                Invoke(health, "Awake");
                PlayerWalkAnimator animator = player.AddComponent<PlayerWalkAnimator>();
                Invoke(animator, "Awake");
                Require(animator.HasDefeatFrames, "Erpin must load four defeat poses from Resources.");
                for (int i = 0; i < 4; i++)
                    Require(animator.GetDefeatFrame(i).name == $"Erpin_Death_{i}", $"Defeat pose {i} must be in order.");
                Require(!animator.TickDefeat(0f), "A living Erpin must not show a defeat pose.");
                GroundShadow ground = GroundShadow.Ensure(player);
                ground.Refresh();
                Require(ground.Visual != null && ground.Visual.enabled, "Fixture setup: a living Erpin casts a shadow.");
                Vector3 position = player.transform.position;
                Quaternion rotation = player.transform.rotation;
                health.TakeDamage(health.MaxHealth * 10f);
                Require(health.IsDead, "Fixture setup: Erpin must die.");
                ground.Refresh();
                Require(ground.Visual.enabled && ground.Visual.GetComponent<Collider2D>() == null,
                    "Erpin's shadow must stay under her drawn fall.");
                float step = PlayerWalkAnimator.DefeatFrameSeconds;
                for (int i = 0; i < 4; i++)
                {
                    Require(animator.TickDefeat(step * (i + 0.1f)) && renderer.sprite == animator.GetDefeatFrame(i),
                        $"Defeat pose {i} must follow at {step:F2} s steps.");
                }
                Require(animator.TickDefeat(5f) && renderer.sprite == animator.GetDefeatFrame(3),
                    "The last pose must stay through the GAME OVER hold.");
                Require(Mathf.Abs(animator.DefeatDuration - step * 4f) < 0.0001f,
                    "The drawn fall must last four steps.");
                Require(player.transform.position == position && player.transform.rotation == rotation,
                    "The drawn fall must not move or tilt the player.");
            }
            finally { Object.DestroyImmediate(player); }
        }

        // The sprite's visible part in local units, relative to its pivot.
        private static Rect WorldVisible(Sprite sprite)
        {
            Rect visible = Defeat1MotionSetup.VisibleRect(sprite);
            float ppu = sprite.pixelsPerUnit;
            return new Rect((visible.xMin - sprite.pivot.x) / ppu, (visible.yMin - sprite.pivot.y) / ppu,
                visible.width / ppu, visible.height / ppu);
        }

        private static void RequireStanding(Sprite frame, float height, float bottom, string subject)
        {
            Rect visible = WorldVisible(frame);
            Require(Mathf.Abs(visible.height - height) < Tolerance && Mathf.Abs(visible.yMin - bottom) < Tolerance &&
                    Mathf.Abs(visible.center.x) < Tolerance,
                $"{subject} must be as tall as the idle drawing and stand on its feet (got {visible}, " +
                $"expected height {height:F3}, bottom {bottom:F3}).");
        }

        private static void WithInstance(string prefabPath, Action<GameObject, SpriteRenderer> check)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Require(prefab != null, "Missing " + prefabPath);
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                root.transform.position = new Vector3(500f, 500f, 0f); // away from any open Scene
                var view = root.GetComponent<FairyKingdomArtworkView>();
                Require(view != null && view.Visual != null, prefabPath + " must draw through its artwork view.");
                view.Refresh();
                check(root, view.Visual);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Sprite[] Load(Func<int, string> pathOf)
        {
            var sprites = new Sprite[Defeat1MotionSetup.FrameCount];
            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(pathOf(i));
                Require(sprites[i] != null, "Missing frame " + pathOf(i));
            }
            return sprites;
        }

        private static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
