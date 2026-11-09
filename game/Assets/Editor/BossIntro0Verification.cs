using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Character;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Boss-Intro-0: the intro stops game time, slides both panels in, slams VS, hands over to the HP bar fill, can be
    // skipped, and gives time back when it ends.
    public static class BossIntro0Verification
    {
        private const float Step = 0.02f;

        [MenuItem("Trickal Fan Game/Beta/Verify Boss-Intro-0 Boss Intro")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameBossIntroView view = FindAll<GameBossIntroView>(scene).SingleOrDefault();
            Assert(view != null, "Run Boss-Intro-0 setup: the Game Scene needs one boss intro view.");
            VerifyWiring(view, FindAll<GameBossHudView>(scene).Single());
            VerifyRuntime(view);
            Debug.Log("Boss-Intro-0 verification passed: entering a boss room stops time, the boss and player panels " +
                      "slide in, VS lands with a flash and shake, the HP bar fills from empty, skipping jumps to the " +
                      "fill and then to the end, and time comes back afterwards.");
        }

        public static void SetupAndVerifyBatch()
        {
            BossIntro0Setup.Setup();
            string materialGuid = AssetDatabase.AssetPathToGUID(BossIntro0Setup.VsMaterialPath);
            BossIntro0Setup.Setup();
            Assert(materialGuid == AssetDatabase.AssetPathToGUID(BossIntro0Setup.VsMaterialPath),
                "Rerunning Boss-Intro-0 setup must keep the same VS material asset.");
            Scene scene = SceneManager.GetActiveScene();
            Assert(FindAll<GameBossIntroView>(scene).Length == 1 &&
                   FindAll<RectTransform>(scene).Count(rect => rect.name == BossIntro0Setup.OverlayName) == 1,
                "Rerunning Boss-Intro-0 setup must not duplicate the overlay.");
            Verify();
        }

        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week13Hud5Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Boss-Intro-0 regression batch passed: Boss-Intro-0, HUD-5 and the development panel.");
        }

        private static void VerifyWiring(GameBossIntroView view, GameBossHudView bossHud)
        {
            Assert(view.BossHud == bossHud && view.RoomGraph != null && view.RunSession != null,
                "The intro must reference the boss HUD, the room graph and the run session.");
            Assert(view.CanvasGroup != null && view.ShakeRoot != null && view.BossPanel != null &&
                   view.BossPortrait != null && view.BossNameText != null && view.PlayerPanel != null &&
                   view.PlayerPortrait != null && view.PlayerNameText != null && view.VsText != null &&
                   view.Flash != null, "Every intro element must be wired.");
            Assert(view.transform.parent == bossHud.transform.parent &&
                   view.transform.GetSiblingIndex() > bossHud.transform.GetSiblingIndex(),
                "The intro must sit in the Game HUD frame above the boss HUD.");
            Assert(view.CanvasGroup.alpha == 0f && !view.CanvasGroup.blocksRaycasts,
                "The intro must start hidden and never block clicks.");
            Assert(view.Characters.Length > 0 && view.Characters.All(c => c != null && c.Portrait != null),
                "Every playable character needs a portrait for the player panel.");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            TMP_Text[] texts = { view.BossNameText, view.PlayerNameText, view.VsText };
            Assert(texts.All(text => text.font == font), "Intro texts must use the frontend TMP font.");
            Assert(font.HasCharacters(BossIntro0Setup.RequiredGlyphs(view.Characters)),
                "The frontend font is missing boss or character name glyphs.");
            Assert(BossIntro0Setup.LoadBossNames().Length >= 3,
                "All three bosses (부스러기, 새마음금고, 크레용 용사) must be found for their name glyphs.");
            Material vs = view.VsText.fontSharedMaterial;
            Assert(vs != null && AssetDatabase.GetAssetPath(vs) == BossIntro0Setup.VsMaterialPath &&
                   vs.IsKeywordEnabled(ShaderUtilities.Keyword_Outline) &&
                   vs.GetFloat(ShaderUtilities.ID_OutlineWidth) >= 0.2f,
                "VS must use the saved outlined material " + BossIntro0Setup.VsMaterialPath + ".");
            Assert(view.VsText.text == "VS", "The VS text must read VS.");
        }

        private static void VerifyRuntime(GameBossIntroView view)
        {
            CharacterDefinition character = view.Characters[0];
            GameObject bossObject = new("Boss-Intro-0 Verification Boss", typeof(BossController));
            SpriteRenderer bossRenderer = bossObject.AddComponent<SpriteRenderer>();
            bossRenderer.sprite = character.Portrait;
            BossController boss = bossObject.GetComponent<BossController>();
            boss.ConfigureHud("검증보스", 1);
            float originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            try
            {
                view.Begin(boss);
                Assert(view.Stage == BossIntroStage.Panels && Time.timeScale == 0f,
                    "Starting the intro must stop game time.");
                Assert(view.BossNameText.text == "검증보스" && view.BossPortrait.sprite == bossRenderer.sprite &&
                       view.BossPortrait.enabled, "The boss panel must show the boss name and its sprite.");
                Assert(view.PlayerNameText.text == character.DisplayName &&
                       view.PlayerPortrait.sprite == character.Portrait,
                    "The player panel must show the character name and portrait.");
                Assert(view.BossHud.IsIntroActive && view.BossHud.IntroFill == 0f,
                    "The boss HP bar must stay hidden and empty during the panels.");
                Assert(view.BossPanel.anchoredPosition.x < view.BossRestPosition.x - 1000f &&
                       view.PlayerPanel.anchoredPosition.x > view.PlayerRestPosition.x + 1000f,
                    "Both panels must start off screen on their own side.");
                Assert(!view.VsText.gameObject.activeSelf, "VS must wait until the panels are in.");

                view.Skip();
                Assert(view.Elapsed == 0f, "A key held while walking in must not skip the first moment.");

                AdvanceTo(view, GameBossIntroView.SlideInSeconds);
                Assert(Vector2.Distance(view.BossPanel.anchoredPosition, view.BossRestPosition) < 2f &&
                       Vector2.Distance(view.PlayerPanel.anchoredPosition, view.PlayerRestPosition) < 2f,
                    "Both panels must be in place when the slide ends.");

                AdvanceTo(view, GameBossIntroView.VsImpactAt + Step);
                Assert(view.VsText.gameObject.activeSelf &&
                       Mathf.Abs(view.VsText.transform.localScale.x - 1f) < 0.05f,
                    "VS must land at its normal size.");
                Assert(view.Flash.color.a > 0.4f && view.ShakeRoot.anchoredPosition.magnitude > 0.01f,
                    "VS landing must flash and shake.");

                AdvanceTo(view, GameBossIntroView.SlideOutAt - Step);
                Assert(view.Flash.color.a == 0f && view.ShakeRoot.anchoredPosition == Vector2.zero &&
                       view.CanvasGroup.alpha == 1f, "The flash and shake must settle before the panels leave.");

                AdvanceTo(view, GameBossIntroView.FillStartsAt + GameBossIntroView.FillSeconds * 0.5f);
                Assert(view.Stage == BossIntroStage.Fill && view.CanvasGroup.alpha == 0f && Time.timeScale == 0f,
                    "The HP bar fills with the panels gone and time still stopped.");
                Assert(view.BossHud.IntroFill > 0.5f && view.BossHud.IntroFill < 1f,
                    "Half way through, the HP bar must be partly filled.");

                AdvanceTo(view, GameBossIntroView.TotalSeconds + Step);
                Assert(view.Stage == BossIntroStage.Idle && Time.timeScale == 1f && !view.BossHud.IsIntroActive,
                    "When the bar is full the intro must give time back and release the HP bar.");

                view.Begin(boss);
                AdvanceTo(view, GameBossIntroView.SkipGuardSeconds + Step);
                view.Skip();
                Assert(view.Stage == BossIntroStage.Fill && view.CanvasGroup.alpha == 0f && Time.timeScale == 0f,
                    "Skipping the panels must jump to the HP bar fill.");
                view.Skip();
                Assert(view.Stage == BossIntroStage.Idle && Time.timeScale == 1f && !view.BossHud.IsIntroActive,
                    "Skipping the fill must start combat.");
            }
            finally
            {
                AdvanceTo(view, GameBossIntroView.TotalSeconds + 1f);
                Time.timeScale = originalTimeScale;
                view.BossHud.ClearIntroPresentation();
                Object.DestroyImmediate(bossObject);
            }
        }

        private static void AdvanceTo(GameBossIntroView view, float seconds)
        {
            while (view.IsPlaying && view.Elapsed < seconds - 0.0001f)
                view.Advance(Mathf.Min(Step, seconds - view.Elapsed));
        }

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[Boss-Intro-0] " + message);
        }
    }
}
