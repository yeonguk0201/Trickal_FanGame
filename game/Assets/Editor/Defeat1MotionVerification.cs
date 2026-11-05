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
            VerifyWalk(Defeat1MotionSetup.WalkPath, Defeat1MotionSetup.SelfDestructPrefab,
                Defeat1MotionSetup.SelfDestructStride, "Sansamo madness walk");
            VerifyWalk(Defeat1MotionSetup.LobbingWalkPath, Defeat1MotionSetup.LobbingPrefab,
                Defeat1MotionSetup.LobbingStride, "Lobbing fairy walk");
            VerifyCrumbHop();
            VerifyCrouch();
            VerifyAmbush();
            VerifyErpin();
            Debug.Log("Defeat-1 verification passed: Sansamo madness and the lobbing fairy walk four drawn frames at " +
                      "their idle height and feet, Sansamo madness crouches through four frames over its warning " +
                      "(and its blast keeps crouching), the crumb minion hops a lifted copy of its drawing, " +
                      "the ambusher shows mound → three burst steps → held → reversed at its idle width, and " +
                      "Erpin falls through four drawn poses on her idle ground line, keeps her shadow and holds the last.");
        }

        private static void VerifyWalk(Func<int, string> pathOf, string prefabPath, float stride, string label)
        {
            Sprite[] frames = Load(pathOf);
            Defeat1MotionSetup.IdleSize(prefabPath, out _, out float height);
            RequireStanding(frames[0], height, -height / 2f, label);
            for (int i = 1; i < frames.Length; i++)
                Require(frames[i].rect.size == frames[0].rect.size && frames[i].pivot == frames[0].pivot &&
                        Mathf.Approximately(frames[i].pixelsPerUnit, frames[0].pixelsPerUnit),
                    $"{label} frame {i} must share frame 0's canvas, scale and pivot.");
            WithInstance(prefabPath, (root, visual) =>
            {
                var walk = root.GetComponent<EnemyWalkFrames>();
                Require(root.GetComponents<EnemyWalkFrames>().Length == 1 && walk.Frames.Length == 4 &&
                        Mathf.Approximately(walk.StrideLength, stride),
                    $"{label}: the Prefab must carry one four-frame walk.");
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

        // 산사모-광기: the warning crouches through four frames, the first standing where the idle drawing stands.
        private static void VerifyCrouch()
        {
            Sprite[] frames = Load(Defeat1MotionSetup.CrouchPath);
            Defeat1MotionSetup.IdleSize(Defeat1MotionSetup.SelfDestructPrefab, out _, out float height);
            RequireStanding(frames[0], height, -height / 2f, "Sansamo madness crouch");
            for (int i = 1; i < frames.Length; i++)
                Require(frames[i].rect.size == frames[0].rect.size && frames[i].pivot == frames[0].pivot &&
                        Mathf.Approximately(frames[i].pixelsPerUnit, frames[0].pixelsPerUnit),
                    $"Crouch frame {i} must share frame 0's canvas, scale and pivot.");
            Require(SelfDestructCrouch.FrameIndex(0f, 4) == 0 && SelfDestructCrouch.FrameIndex(0.26f, 4) == 1 &&
                    SelfDestructCrouch.FrameIndex(0.6f, 4) == 2 && SelfDestructCrouch.FrameIndex(0.8f, 4) == 3 &&
                    SelfDestructCrouch.FrameIndex(1f, 4) == 3,
                "The crouch must spread its four frames evenly over the warning and hold the last.");
            WithInstance(Defeat1MotionSetup.SelfDestructPrefab, (root, visual) =>
            {
                var crouch = root.GetComponent<SelfDestructCrouch>();
                Require(root.GetComponents<SelfDestructCrouch>().Length == 1 && crouch.HasFrames &&
                        crouch.Frames.Length == 4, "SelfDestructFairy must carry one four-frame crouch.");
                for (int i = 0; i < 4; i++) Require(crouch.Frames[i] == frames[i], $"Crouch frame {i} must be the drawing.");
                Vector3 position = root.transform.position;
                for (int i = 0; i < 4; i++)
                {
                    crouch.RenderPose((i + 0.1f) / 4f);
                    Require(visual.sprite == frames[i], $"The warning must show crouch frame {i} in order.");
                }
                Require(root.transform.position == position, "Crouching must not move the enemy.");

                // Dying mid-warning: the blast's stopped body keeps crouching on the warning's clock and turns red.
                GameObject owner = new("Crouch Blast Verification");
                try
                {
                    visual.sprite = frames[1];
                    EnemyDeathBlast blast = EnemyDeathBlast.Spawn(Vector3.zero, owner.transform, root, visual, 1f, 1f, 1,
                        Time.time);
                    blast.ConfigureBodyFrames(crouch.Frames);
                    Require(blast.Body != null && blast.BodyFrameCount == 4 && blast.Body.sprite == frames[0],
                        "The blast body must take the crouch frames from the start of its warning.");
                    blast.Tick(Time.time + 0.6f);
                    Require(blast.Body.sprite == frames[2] && blast.Body.color.g < 0.9f,
                        "Mid-warning the blast body must be on the matching crouch frame and reddening.");
                }
                finally { Object.DestroyImmediate(owner); }
            });
        }

        // 부스러기 쫄따구: the body stays on the floor; a copy of its drawing rises once per stride and lands.
        private static void VerifyCrumbHop()
        {
            float h = Defeat1MotionSetup.CrumbHopHeight;
            Require(EnemyHopMotion.LiftAt(0f, h) == 0f && EnemyHopMotion.LiftAt(EnemyHopMotion.TakeOff - 0.01f, h) == 0f &&
                    Mathf.Abs(EnemyHopMotion.LiftAt((EnemyHopMotion.TakeOff + EnemyHopMotion.Landing) / 2f, h) - h) < 0.0001f &&
                    EnemyHopMotion.LiftAt(EnemyHopMotion.Landing, h) == 0f && Mathf.Abs(EnemyHopMotion.LiftAt(1.3f, h) -
                    EnemyHopMotion.LiftAt(0.3f, h)) < 0.0001f,
                "The hop must rest on the floor, peak at its height mid-air and repeat every stride.");
            WithInstance(Defeat1MotionSetup.CrumbMinionPrefab, (root, visual) =>
            {
                var hop = root.GetComponent<EnemyHopMotion>();
                Require(root.GetComponents<EnemyHopMotion>().Length == 1 &&
                        Mathf.Approximately(hop.StrideLength, Defeat1MotionSetup.CrumbHopStride) &&
                        Mathf.Approximately(hop.HopHeight, h), "The crumb minion must carry one configured hop.");
                Require(root.GetComponent<EnemyWalkFrames>() == null && root.GetComponent<EnemyMovementAnimator>() == null,
                    "The crumb minion must not also walk or deform.");
                Vector3 position = root.transform.position, visualAt = visual.transform.position;
                visual.flipX = true;
                visual.color = new Color(1f, 0.5f, 0.5f, 1f);
                hop.RenderLift(h);
                SpriteRenderer copy = hop.Artwork;
                Require(copy != null && copy.enabled && visual.forceRenderingOff && copy.sprite == visual.sprite &&
                        copy.flipX && copy.color == visual.color && copy.sortingOrder == visual.sortingOrder &&
                        Mathf.Abs(copy.transform.position.y - visualAt.y - h) < 0.001f &&
                        Mathf.Abs(copy.transform.position.x - visualAt.x) < 0.001f,
                    "In the air the drawing (with its colour and facing) must be drawn the hop height above the body.");
                Require(root.transform.position == position && visual.transform.position == visualAt,
                    "Hopping must not move the body, its collider or its shadow source.");
                hop.RenderLift(0f);
                Require(!copy.enabled && !visual.forceRenderingOff, "Landed, the body draws itself again.");
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
            Require(F(AmbushState.Attacking, 0.01f) == 1 && F(AmbushState.Attacking, 0.06f) == 2 &&
                    F(AmbushState.Attacking, 0.11f) == 3 && F(AmbushState.Attacking, 0.2f) == 3 &&
                    F(AmbushState.Attacking, 5f) == 3,
                "The burst must play its three steps within its first 0.15 s, then hold the last.");
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

                // Moving under the floor: four drawn frames on the mound's canvas, one cycle per stride.
                Sprite[] move = Load(Defeat1MotionSetup.AmbushMovePath);
                Require(motion.HasMoveFrames && Mathf.Approximately(motion.MoveStride, Defeat1MotionSetup.AmbushMoveStride),
                    "AmbushFairy must carry four move frames at its stride.");
                Rect still = WorldVisible(frames[AmbushEnemyMotion.MoundFrame]);
                for (int i = 0; i < 4; i++)
                {
                    Require(motion.MoveFrames[i] == move[i], $"Ambush move frame {i} must be the drawing.");
                    Require(Mathf.Approximately(move[i].pixelsPerUnit, frames[0].pixelsPerUnit) &&
                            move[i].pivot == frames[0].pivot && move[i].rect.size == frames[0].rect.size,
                        $"Ambush move frame {i} must share the mound's canvas, scale and pivot.");
                    Rect visible = WorldVisible(move[i]);
                    Require(Mathf.Abs(visible.yMin - still.yMin) < Tolerance && Mathf.Abs(visible.center.x - still.center.x) < Tolerance,
                        $"Ambush move frame {i} must stand where the still mound stands (got {visible}, mound {still}).");
                    Require(motion.RenderMovePose((i + 0.1f) / 4f) && visual.sprite == move[i] && motion.CurrentMoveFrame == i,
                        $"A move cycle must show frame {i} in order.");
                }
                Require(motion.RenderMovePose(1.05f) && visual.sprite == move[0], "The move cycle must loop.");

                // Warning: a shaking copy of the mound, the body's own drawing hidden but still enabled.
                typeof(AmbushEnemyController).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(controller, new object[] { AmbushState.Emerging, 0f });
                Vector3 bodyPosition = root.transform.position;
                float stepTime = AmbushEnemyMotion.RumbleStepSeconds;
                motion.RenderPose(stepTime * 0.5f);
                Vector3 first = motion.IsRumbling ? motion.Rumble.transform.position : Vector3.zero;
                Require(motion.IsRumbling && visual.enabled && visual.forceRenderingOff &&
                        motion.Rumble.sprite == frames[AmbushEnemyMotion.MoundFrame] &&
                        motion.Rumble.sortingOrder == visual.sortingOrder &&
                        motion.Rumble.GetComponent<Collider2D>() == null && controller.Mark == null,
                    "While warning the mound must rattle as a collider-free copy, with no red ellipse.");
                motion.RenderPose(stepTime * 1.5f);
                Vector3 second = motion.Rumble.transform.position;
                Require(Mathf.Abs(first.x - second.x) > AmbushEnemyMotion.RumbleAmplitude &&
                        Mathf.Abs(first.x - bodyPosition.x) <= AmbushEnemyMotion.RumbleAmplitude + 0.0001f &&
                        root.transform.position == bodyPosition,
                    "The rattle must swing side to side within its amplitude and leave the body in place.");
                typeof(AmbushEnemyController).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(controller, new object[] { AmbushState.Attacking, 1f });
                motion.RenderPose(1.01f);
                Require(!motion.IsRumbling && !visual.forceRenderingOff && visual.sprite == frames[1],
                    "Bursting out must stop the rattle and show the body's own drawing again.");
                typeof(AmbushEnemyController).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(controller, new object[] { AmbushState.Burrowed, 2f });

                // The shadow keeps the still mound's outline whatever move frame is shown.
                Require(motion.TryGetMoundBounds(out Bounds mound), "A buried ambusher must report its mound outline.");
                visual.sprite = frames[AmbushEnemyMotion.MoundFrame];
                Bounds rendered = visual.bounds;
                // Floor footprint only: x and y (the renderer's bounds also carry a small depth).
                Require(((Vector2)(mound.center - rendered.center)).sqrMagnitude < 1e-6f &&
                        ((Vector2)(mound.size - rendered.size)).sqrMagnitude < 1e-6f,
                    $"The mound outline must match the still mound drawing (got {mound}, drawn {rendered}).");
                GroundShadow shadow = GroundShadow.Ensure(root);
                shadow.Refresh();
                Vector3 stillPosition = shadow.Visual.transform.position, stillScale = shadow.Visual.transform.localScale;
                for (int i = 0; i < 4; i++)
                {
                    motion.RenderMovePose((i + 0.1f) / 4f);
                    shadow.Refresh();
                    Require((shadow.Visual.transform.position - stillPosition).sqrMagnitude < 1e-8f &&
                            (shadow.Visual.transform.localScale - stillScale).sqrMagnitude < 1e-8f,
                        $"The buried shadow must not move or resize on move frame {i}.");
                }
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
