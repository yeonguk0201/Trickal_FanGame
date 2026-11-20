using System;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class CrayonHeroTruckDefeatVerification
    {
        [MenuItem("Trickal Fan Game/Verification/Verify Truck Defeat And Erpin Victory")]
        public static void Verify()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrayonHeroTruckDefeatSetup.BossPrefabPath);
            Require(prefab != null, "CrayonHeroBoss Prefab is missing.");
            Require(prefab.GetComponents<CrayonHeroTruckDefeat>().Length == 1, "CrayonHeroBoss needs one truck defeat.");
            Require(prefab.GetComponent<EnemyDefeatAnimation>() == null,
                "CrayonHeroBoss must not also leave the generic knocked-away defeat picture.");
            var defeat = prefab.GetComponent<CrayonHeroTruckDefeat>();
            Require(defeat.HasFrames && defeat.DriveFrames.Length == CrayonHeroTruckDefeatSetup.DriveFrameCount &&
                    defeat.ImpactFrames.Length == CrayonHeroTruckDefeatSetup.ImpactFrameCount,
                "The truck needs its 2 wheel frames and 8 crash frames, each with a place and (crash) a time.");
            for (int i = 0; i < defeat.ImpactFrames.Length; i++)
                Require(defeat.ImpactFrames[i].name == $"CrayonHero_TruckImpact_{i}", $"Crash frame {i} is out of order.");
            // A rect drawn at another aspect would stretch the drawing: the rects belong to these PNGs.
            CheckAspect(defeat.DriveFrames, defeat.DriveRects, "wheel");
            CheckAspect(defeat.ImpactFrames, defeat.ImpactRects, "crash");
            Require(defeat.AnchorFromFeet.x < 0f, "The truck must come from the left of the boss.");
            Require(defeat.ApproachDistance >= 12.5f && defeat.ExitDistance >= 25f,
                "The truck must start and end outside the 25-unit-wide boss room.");
            float expectedVictory = defeat.ImpactStart + defeat.ImpactDuration +
                                    CrayonHeroTruckDefeatSetup.VictoryAfterImpact;
            Require(Mathf.Abs(defeat.VictoryDelay - expectedVictory) < 0.001f && defeat.VictoryDelay < defeat.Duration,
                "에르핀 must jump after the crash frames and before the truck has gone.");

            VerifyView(prefab, defeat);
            VerifyErpinVictory();
            Debug.Log("Truck defeat verification passed: 황금 용사 stands, the truck drives in from the left, plays " +
                      "8 crash frames in order, drives out and removes itself; 에르핀's six victory frames stand on " +
                      "her idle ground line at her idle height.");
        }

        private static void CheckAspect(Sprite[] sprites, Rect[] rects, string kind)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                Require(Mathf.Abs(sprites[i].pivot.x / sprites[i].rect.width - 0.5f) < 0.001f &&
                        Mathf.Abs(sprites[i].pivot.y / sprites[i].rect.height - 0.5f) < 0.001f,
                    $"The {kind} frame {i} needs a centre pivot.");
                float sprite = sprites[i].rect.width / sprites[i].rect.height;
                float rect = rects[i].width / rects[i].height;
                Require(Mathf.Abs(sprite / rect - 1f) < 0.02f, $"The {kind} frame {i} rect does not match its drawing.");
            }
        }

        private static void VerifyView(GameObject prefab, CrayonHeroTruckDefeat source)
        {
            var room = new GameObject("Truck Defeat Verification Room");
            try
            {
                var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room.transform);
                boss.transform.position = new Vector3(2f, -1f, 0f);
                SpriteRenderer body = EnemyDefeatAnimation.FindBody(boss);
                Require(body != null && body.sprite != null, "CrayonHeroBoss needs a visible body drawing.");
                CrayonHeroTruckDefeat defeat = boss.GetComponent<CrayonHeroTruckDefeat>();
                CrayonHeroTruckDefeatView view = defeat.SpawnView();
                Require(view != null && view.transform.parent == room.transform, "The truck scene must stay with the room.");
                Require(view.GetComponentsInChildren<Collider2D>().Length == 0, "The truck scene must have no collider.");
                Require(view.CurrentStage == CrayonHeroTruckDefeatView.Stage.Waiting && view.Standing.enabled &&
                        !view.Truck.enabled && view.Standing.sprite == body.sprite,
                    "The boss must first stand, as it last looked, with no truck yet.");
                Require(Mathf.Abs(view.BossHeight - body.bounds.size.y * defeat.BodyVisibleShare) < 0.001f,
                    "The scene must be sized from the boss's visible height.");

                float t = 0f;
                void To(float time) { view.Advance(time - t); t = time; }

                To(defeat.WaitSeconds + 0.01f);
                float enteringRight = view.Truck.bounds.max.x;
                Require(view.CurrentStage == CrayonHeroTruckDefeatView.Stage.Approaching && view.Truck.enabled &&
                        enteringRight < view.Feet.x - defeat.ApproachDistance * 0.9f,
                    "The truck must enter from far to the left.");
                To(defeat.ImpactStart - 0.01f);
                Require(view.Truck.bounds.max.x > enteringRight, "The truck must drive toward the boss.");
                Require(Mathf.Abs(view.Truck.bounds.size.y - view.BossHeight * defeat.DriveRects[0].height) < 0.01f ||
                        Mathf.Abs(view.Truck.bounds.size.y - view.BossHeight * defeat.DriveRects[1].height) < 0.01f,
                    "The truck must be drawn at its rect's height.");

                float acc = defeat.ImpactStart;
                for (int i = 0; i < defeat.ImpactFrames.Length; i++)
                {
                    To(acc + defeat.ImpactSeconds[i] * 0.5f);
                    Require(view.CurrentStage == CrayonHeroTruckDefeatView.Stage.Impact && view.ImpactFrame == i &&
                            view.Truck.sprite == defeat.ImpactFrames[i] && !view.Standing.enabled,
                        $"Crash frame {i} must show in its turn, with the standing boss gone.");
                    acc += defeat.ImpactSeconds[i];
                }
                To(acc + 0.05f);
                float leaving = view.Truck.transform.position.x;
                Require(view.CurrentStage == CrayonHeroTruckDefeatView.Stage.Leaving, "The truck must drive on after the crash.");
                To(defeat.Duration - 0.02f);
                Require(view.Truck.bounds.min.x > leaving && view.Truck.bounds.min.x > view.Feet.x + 12f,
                    "The truck must leave the room to the right.");
                To(defeat.Duration + 0.05f);
                Require(view == null, "The truck scene must remove itself at the end.");
            }
            finally { UnityEngine.Object.DestroyImmediate(room); }
        }

        private static void VerifyErpinVictory()
        {
            Sprite idle = Defeat1MotionSetup.ErpinIdle();
            Rect idleVisible = Defeat1MotionSetup.VisibleRect(idle);
            float idleBottom = (idleVisible.yMin - idle.pivot.y) / idle.pixelsPerUnit;
            float idleHeight = idleVisible.height / idle.pixelsPerUnit;
            Sprite[] frames = Resources.LoadAll<Sprite>("Characters/Erpin_Victory");
            for (int i = 0; i < PlayerWalkAnimator.VictoryFrameCount; i++)
            {
                Sprite frame = frames.FirstOrDefault(s => s.name == $"Erpin_Victory_{i}");
                Require(frame != null, $"Erpin_Victory_{i} is missing from Resources/Characters/Erpin_Victory.");
                if (i != 0) continue;
                Rect visible = Defeat1MotionSetup.VisibleRect(frame);
                Require(Mathf.Abs(visible.height / frame.pixelsPerUnit - idleHeight) < 0.01f &&
                        Mathf.Abs((visible.yMin - frame.pivot.y) / frame.pixelsPerUnit - idleBottom) < 0.01f,
                    "에르핀's first victory pose must stand as tall as her idle frame, on the same ground line.");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Truck defeat verification failed: " + message);
        }
    }
}
