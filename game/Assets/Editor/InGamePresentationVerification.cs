using System;
using System.IO;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // docs/ingame-presentation-asset-guide.md: the art is connected where the guide says, nothing else changed its
    // job, and reruns (this setup and the older pause/HUD setups) keep the look without duplicates.
    // Behaviour stays covered by HUD-3C/4C/7B/7C/7D, UX-0/1, Setting-1C, Power-1 and Reward-3.
    public static class InGamePresentationVerification
    {
        [MenuItem("Trickal Fan Game/Menu/Verify In-game Presentation")]
        public static void Verify()
        {
            VerifyImports();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RectTransform frame = Single<GameHudView>(scene).transform.Find("ReferenceFrame") as RectTransform;
            Assert(frame != null, "Game HUD ReferenceFrame is missing.");
            VerifyBanner(frame);
            VerifyPowerHud(frame);
            VerifyStatsHud(frame);
            VerifyPause(scene);
            VerifyRewardAltars();
            Debug.Log("In-game presentation verification passed: banner, 권능 slot, stat icons, pause window, reward altar.");
        }

        // Setup twice (no duplicates), the older pause setups rerun on top (look kept), then the verifiers.
        public static void SetupAndVerifyBatch() => Batch(() =>
        {
            InGamePresentationSetup.Setup();
            int sceneCount = CountTransforms();
            int prefabCount = CountRewardChildren();
            InGamePresentationSetup.Setup();
            Assert(sceneCount == CountTransforms() && prefabCount == CountRewardChildren(),
                "In-game presentation setup created duplicate objects when run twice.");
            Week13Hud7DSetup.Setup(); // reruns HUD-7C and HUD-7B, which rebuild the pause panel in the old look
            Week25Ux0Setup.Setup();
            Assert(sceneCount == CountTransforms(), "Rerunning the pause setups changed the pause hierarchy.");
            Verify();
            CaptureInternal();
        });

        // Commit check: this slice plus every verifier whose expectations it changed or whose objects it touches.
        public static void VerifyWithRegressionsBatch() => Batch(() =>
        {
            Verify();
            Week13Hud4CVerification.Verify();
            Week13Hud3CVerification.Verify();
            Week13Hud7BVerification.Verify();
            Week13Hud7CVerification.Verify();
            Week13Hud7DVerification.Verify();
            Week25Ux0Verification.Verify();
            Week13Setting1CVerification.Verify();
            Week27Power1Verification.Verify();
            Week27Ux1Verification.Verify();
            Week16Reward3Verification.Verify();
            Debug.Log("In-game presentation regression verification passed.");
        });

        public static void CaptureBatch() => Batch(CaptureInternal);

        [MenuItem("Trickal Fan Game/Menu/Capture In-game Presentation")]
        public static void CaptureMenu() => CaptureInternal();

        private static void VerifyImports()
        {
            foreach (string name in InGamePresentationSetup.All)
            {
                var importer = AssetImporter.GetAtPath(InGamePresentationSetup.PathOf(name)) as TextureImporter;
                Assert(importer != null && importer.textureType == TextureImporterType.Sprite,
                    "In-game presentation art must import as a Sprite: " + name);
                Assert(InGamePresentationSetup.LoadSprite(name) != null, "Missing sprite: " + name);
            }
            Sprite altar = InGamePresentationSetup.LoadSprite(InGamePresentationSetup.RewardAltar);
            Assert(Mathf.Approximately(altar.pivot.x / altar.rect.width, 0.5f) && Mathf.Approximately(altar.pivot.y, 0f),
                "The reward altar sprite pivot must be bottom centre.");
        }

        private static void VerifyBanner(RectTransform frame)
        {
            GameFloorNameView view = frame.GetComponentsInChildren<GameFloorNameView>(true).Single();
            var panel = (RectTransform)view.transform;
            Image banner = panel.GetComponent<Image>();
            Assert(Uses(banner, InGamePresentationSetup.MapIntroBanner) && banner.color == Color.white &&
                   !banner.raycastTarget && banner.preserveAspect,
                "The floor banner must show the winged banner art, untinted and not catching clicks.");
            Assert(Mathf.Approximately(panel.sizeDelta.x / panel.sizeDelta.y, 1536f / 320f),
                "The floor banner must keep the art's aspect ratio.");
            Assert(view.AnnouncementCanvasGroup.gameObject == panel.gameObject &&
                   view.AnnouncementText.transform.parent == panel,
                "Banner and name must fade as one group.");
            TMP_Text title = view.AnnouncementText;
            float plaque = panel.sizeDelta.x * 0.775f;
            Assert(title.rectTransform.sizeDelta.x <= plaque * 0.7f + 1f && title.enableAutoSizing &&
                   title.font == AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath),
                "The floor name must fit the middle 70% inside the wings and shrink for long names.");

            Assert(panel.anchoredPosition == InGamePresentationSetup.BannerPosition &&
                   title.rectTransform.anchoredPosition == InGamePresentationSetup.BannerTitleOffset,
                "The floor banner and its name must sit at the tuned positions.");

            Assert(Mathf.Approximately(view.FadeSeconds, 0.5f) && Mathf.Approximately(view.DisplayDuration, 2f),
                "The banner must fog in 0.5 s, hold 1.0 s and fog out 0.5 s.");
            float duration = view.DisplayDuration, fade = view.FadeSeconds;
            Assert(Mathf.Approximately(GameFloorNameView.FadeAlpha(duration, duration, fade), 0f) &&
                   Mathf.Approximately(GameFloorNameView.FadeAlpha(duration - 0.25f, duration, fade), 0.5f) &&
                   Mathf.Approximately(GameFloorNameView.FadeAlpha(duration - 0.5f, duration, fade), 1f) &&
                   Mathf.Approximately(GameFloorNameView.FadeAlpha(1f, duration, fade), 1f) &&
                   Mathf.Approximately(GameFloorNameView.FadeAlpha(0.25f, duration, fade), 0.5f) &&
                   Mathf.Approximately(GameFloorNameView.FadeAlpha(0f, duration, fade), 0f),
                "Banner reveal must follow 0.5 s in, 1.0 s hold, 0.5 s out.");
            Assert(GameFloorNameView.MaxFrameStep <= 0.05f,
                "A slow first frame after the scene loads must not use up the banner before it is drawn.");

            // Fog: fully hazy and invisible at the edges, sharp and opaque while held, hazy while still faint.
            BannerMist mist = view.Mist;
            Assert(mist != null && mist.gameObject == panel.gameObject && mist.Banner == banner && mist.Title == title,
                "The banner must clear in and out through its mist.");
            Assert(Resources.Load<Shader>(BannerMist.ShaderPath) != null, "The banner mist shader is missing.");
            Assert(Mathf.Approximately(BannerMist.Alpha(0f), 0f) && Mathf.Approximately(BannerMist.Alpha(1f), 1f) &&
                   Mathf.Approximately(BannerMist.Haze(0f), 1f) && Mathf.Approximately(BannerMist.Haze(1f), 0f) &&
                   BannerMist.Haze(0.4f) > 0.4f && BannerMist.Alpha(0.4f) > 0.5f,
                "The banner must appear as a blur first and then clear.");
            Assert(banner.material == null || banner.material == Canvas.GetDefaultCanvasMaterial(),
                "The mist material must be a runtime copy, not saved in the scene.");
        }

        private static void VerifyPowerHud(RectTransform frame)
        {
            GamePowerHudView view = frame.GetComponentsInChildren<GamePowerHudView>(true).Single();
            Image empty = view.EmptySlot != null ? view.EmptySlot.GetComponent<Image>() : null;
            Assert(Uses(empty, InGamePresentationSetup.PowerEmptySlot) && empty.color == Color.white &&
                   empty.rectTransform.sizeDelta == view.Icon.rectTransform.sizeDelta &&
                   empty.rectTransform.anchoredPosition == view.Icon.rectTransform.anchoredPosition &&
                   empty.transform.GetSiblingIndex() < view.Icon.transform.GetSiblingIndex(),
                "The empty 권능 slot frame must sit behind the icon in the same 108 x 108 area.");
            RectTransform bar = (RectTransform)view.GaugeCells[0].transform.parent;
            Image track = bar.parent.GetComponent<Image>();
            Assert(Uses(track, InGamePresentationSetup.PowerGaugeTrack) && track.color == Color.white &&
                   bar.GetComponent<Mask>() != null,
                "The gauge must use the track art outside the existing cell mask.");
            Assert(view.FilledCellSprite == InGamePresentationSetup.LoadSprite(InGamePresentationSetup.PowerGaugeFill) &&
                   view.FilledColor == Color.white,
                "Charged cells must use the cell art untinted (the art already holds #73D1FF).");

            // Empty slot: the frame shows and the icon hides; the gauge is not shown.
            GameObject clone = Object.Instantiate(view.gameObject);
            try
            {
                GamePowerHudView hud = clone.GetComponent<GamePowerHudView>();
                hud.Configure(null, hud.Icon, hud.NameText, hud.GaugeText, hud.KeyText, hud.GaugeCells.ToArray());
                Assert(hud.EmptySlot.activeSelf && !hud.Icon.enabled,
                    "With no 권능 held only the empty slot frame must show.");
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        private static void VerifyStatsHud(RectTransform frame)
        {
            GameDetailedStatsHudView view = frame.GetComponentsInChildren<GameDetailedStatsHudView>(true).Single();
            string[] keys = InGamePresentationSetup.HudStatKeys;
            Image[] icons = keys.Select(key =>
                view.transform.Find(InGamePresentationSetup.HudStatIconPrefix + key)?.GetComponent<Image>()).ToArray();
            Assert(icons.All(icon => icon != null && icon.color == Color.white && icon.preserveAspect),
                "The detailed stats HUD needs four untinted stat icons.");
            Assert(Uses(icons[0], MenuArtSetup.IconSword) && Uses(icons[1], InGamePresentationSetup.StatAttackSpeed) &&
                   Uses(icons[2], InGamePresentationSetup.StatMoveSpeed) && view.CriticalChanceIcon == icons[3],
                "HUD stat icons must be sword, bow, shoe and the critical burst, in A · AS · M · C order.");
            Assert(icons.All(icon => icon.transform.Find("Glyph") == null || !icon.transform.Find("Glyph").gameObject.activeSelf),
                "The temporary letters must be hidden behind the final icons.");
            float[] middles = InGamePresentationSetup.MeasureLineMiddles(view.ValuesText,
                InGamePresentationSetup.HudValueSample);
            for (int i = 0; i < icons.Length; i++)
                Assert(Mathf.Abs(icons[i].rectTransform.anchoredPosition.y - middles[i]) < 2f,
                    "Each stat icon must line up with its value line: " + InGamePresentationSetup.HudStatKeys[i]);
            Assert(middles[0] - middles[1] >= InGamePresentationSetup.HudStatIconSize,
                "Value lines must be at least one icon apart so the icons do not overlap.");
        }

        private static void VerifyPause(Scene scene)
        {
            GamePauseArtifactView view = Single<GamePauseArtifactView>(scene);
            RectTransform panel = view.FocusScope;
            Assert(panel != null && panel.sizeDelta == InGamePresentationSetup.PausePanelSize &&
                   panel.sizeDelta.x <= 1120f && panel.sizeDelta.y <= 760f && Uses(panel.GetComponent<Image>(),
                       MenuArtSetup.PanelMain) && panel.GetComponent<Image>().type == Image.Type.Sliced,
                "The pause window must be the sliced main panel inside the docs/16 popup size.");
            Image backdrop = view.GetComponent<Image>();
            Assert(backdrop != null && backdrop.color.a == 0f && backdrop.raycastTarget,
                "Outside the window must stay clear while still blocking clicks to the game.");
            Assert(Uses(panel.Find(InGamePresentationSetup.TitlePlaqueName)?.GetComponent<Image>(),
                       InGamePresentationSetup.PauseTitlePlaque) &&
                   panel.Find("Title").GetComponent<TMP_Text>().text == "일시정지" &&
                   panel.Find(InGamePresentationSetup.TitlePlaqueName).GetSiblingIndex() <
                   panel.Find("Title").GetSiblingIndex(),
                "The title must sit on the title plaque.");
            Assert(Uses(panel.Find(InGamePresentationSetup.SeedPlateName)?.GetComponent<Image>(),
                       InGamePresentationSetup.PauseSeedPlate) && view.SeedValueText != null &&
                   view.SeedValueText.transform.IsChildOf(panel.Find(InGamePresentationSetup.SeedPlateName)),
                "The current seed must sit on the seed plate.");
            Assert(view.RunProgress == Single<RunProgress>(scene), "The seed must read the scene's RunProgress.");

            // Stats: an icon and a right-aligned number per row, no names.
            Assert(view.StatValueTexts.Count == GamePauseArtifactView.StatRowCount &&
                   view.StatValueTexts.All(text => text != null && text.alignment == TextAlignmentOptions.Right),
                "The pause stats need seven right-aligned value rows.");
            Transform stats = panel.Find("Detailed Stats");
            for (int i = 0; i < InGamePresentationSetup.PauseStatKeys.Length; i++)
            {
                Image icon = stats.Find(InGamePresentationSetup.PauseStatIconPrefix +
                                        InGamePresentationSetup.PauseStatKeys[i])?.GetComponent<Image>();
                Assert(icon != null && icon.color == Color.white &&
                       (i == InGamePresentationSetup.CriticalChanceRow
                           ? view.CriticalChanceIcon == icon
                           : icon.sprite == InGamePresentationSetup.PauseStatSprite(i)),
                    "Pause stat icon is wrong in row " + i);
            }
            Assert(!stats.Find("Values").gameObject.activeSelf, "The old text list must not show beside the rows.");
            Assert(view.PlayerStats != null, "The pause stats must read the player's stats.");
            view.RefreshStats();
            string[] expected = GamePauseArtifactView.BuildStatValues(view.PlayerStats);
            Assert(view.StatValueTexts.Select(text => text.text).SequenceEqual(expected) &&
                   expected[4] == $"{view.PlayerStats.CriticalDamageMultiplier * 100f:0.#}%" &&
                   expected[3] != expected[4],
                "Pause stat rows must follow RefreshStats order, with critical damage as its own percentage row.");

            // Seed: the value once the Run has one, a dash before.
            GameObject progressObject = new("In-game Presentation Seed Probe", typeof(RunProgress));
            try
            {
                RunProgress probe = progressObject.GetComponent<RunProgress>();
                Assert(GamePauseArtifactView.BuildSeed(probe) == GamePauseArtifactView.SeedPlaceholder &&
                       GamePauseArtifactView.BuildSeed(null) == GamePauseArtifactView.SeedPlaceholder,
                    "Before the seed is set the pause window must show a dash.");
                Assert(probe.TryInitializeRunSeed(381726, out string error) &&
                       GamePauseArtifactView.BuildSeed(probe) == "381726", "The pause window must show the Run seed. " + error);
            }
            finally
            {
                Object.DestroyImmediate(progressObject);
            }

            // Section ribbons and the artifact rows.
            Assert(Uses(stats.Find("Section Ribbon")?.GetComponent<Image>(), MenuArtSetup.SectionRibbon) &&
                   Uses(panel.Find("Artifact Section Ribbon")?.GetComponent<Image>(), MenuArtSetup.SectionRibbon) &&
                   panel.Find("Artifact Section Title").GetComponent<TMP_Text>().text == "찾은 아티팩트",
                "Both sections need the section ribbon.");
            RectTransform statsRect = (RectTransform)stats;
            RectTransform viewport = (RectTransform)view.EntriesRoot.parent;
            Assert(statsRect.sizeDelta.x <= panel.sizeDelta.x * 0.3f && Right(statsRect) < Left(viewport),
                "The stats take about the left 30% and the artifact list the rest.");
            ArtifactPauseListEntryView template = view.EntryTemplate;
            Image row = template.GetComponent<Image>();
            Assert(Uses(row, MenuArtSetup.RoundedFill) && row.color == InGamePresentationSetup.ArtifactRowTint &&
                   template.Icon != null && template.NameText != null && template.StackText != null &&
                   template.DescriptionText != null,
                "Artifact rows keep the icon, name, count and effect on the peach rounded fill.");
            Assert(viewport.GetComponent<ScrollRect>().content == view.EntriesRoot,
                "The artifact list must keep the existing scroll list.");

            // Buttons: 설정, 홈으로 나가기, 게임 재개, 처음부터 다시하기 from the left; only 게임 재개 is primary.
            Button[] order = Week25Ux0Setup.PauseButtonOrder(view, view.SettingsButton);
            string[] labels = { "설정", "홈으로 나가기", "게임 재개", "처음부터 다시하기" };
            for (int i = 0; i < order.Length; i++)
            {
                Assert(order[i] != null && order[i].GetComponentInChildren<TMP_Text>().text == labels[i] &&
                       Uses(order[i].GetComponent<Image>(), i == 2 ? MenuArtSetup.ButtonPrimary : MenuArtSetup.ButtonSecondary) &&
                       order[i].GetComponent<Outline>() == null && order[i].GetComponent<MenuPopEffect>() != null,
                    "Pause button look or order is wrong: " + labels[i]);
                if (i > 0)
                    Assert(order[i].transform.localPosition.x > order[i - 1].transform.localPosition.x,
                        "Pause buttons must read left to right in the mock-up order.");
            }

            TMP_FontAsset expectedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(panel.GetComponentsInChildren<TMP_Text>(true).All(text => text.font == expectedFont),
                "Every pause label must use the ONE Mobile POP TMP font.");
            int duplicates = panel.GetComponentsInChildren<Transform>(true)
                .GroupBy(t => (t.parent, t.name)).Count(group => group.Count() > 1);
            Assert(duplicates == 0, $"The pause panel has {duplicates} duplicated child name(s).");
        }

        private static void VerifyRewardAltars()
        {
            Sprite altarSprite = InGamePresentationSetup.LoadSprite(InGamePresentationSetup.RewardAltar);
            Sprite giftSprite = InGamePresentationSetup.LoadSprite(InGamePresentationSetup.RewardGift);
            int count = 0;
            foreach (string path in RoomPrefabPaths())
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                RewardRoom reward = prefab.GetComponentInChildren<RewardRoom>(true);
                if (reward == null) continue;
                count++;
                Transform visual = reward.transform.Find(InGamePresentationSetup.AltarVisualName);
                SortingGroup group = visual != null ? visual.GetComponent<SortingGroup>() : null;
                SpriteRenderer altar = visual?.Find(InGamePresentationSetup.AltarSpriteName)?.GetComponent<SpriteRenderer>();
                SpriteRenderer gift = visual?.Find(InGamePresentationSetup.GiftName)?.GetComponent<SpriteRenderer>();
                Assert(group != null && altar != null && gift != null && altar.sprite == altarSprite &&
                       gift.sprite == giftSprite && gift.sortingOrder > altar.sortingOrder &&
                       group.sortingOrder < PlayerMovement.BodySortingOrder,
                    "Reward altar and gift must share one sorting group with the gift drawn on top: " + path);
                Assert(gift.GetComponentInChildren<Collider2D>(true) == null &&
                       altar.GetComponentInChildren<Collider2D>(true) == null &&
                       reward.GetComponent<BoxCollider2D>() != null && reward.InteractionMarker == gift.gameObject &&
                       reward.transform.Find("Interaction Marker") == null,
                    "The reward root keeps the trigger; the gift has no collider and replaces the old marker: " + path);
                RewardGiftFloat bob = gift.GetComponent<RewardGiftFloat>();
                Assert(bob != null && Mathf.Approximately(bob.Amplitude, 0.05f) && Mathf.Approximately(bob.Period, 1.6f),
                    "The gift must bob ±0.05 every 1.6 s: " + path);
            }
            Assert(count > 0, "No room prefab has a treasure reward.");

            // Sizes from the art's opaque bounds (100 px per unit) against the player's visible height.
            float altarHeight = InGamePresentationSetup.AltarVisiblePixels.y / 100f * InGamePresentationSetup.AltarScale;
            float ratio = altarHeight / InGamePresentationSetup.PlayerVisibleHeight;
            Assert(ratio >= 0.75f && ratio <= 0.85f, $"The altar must be 75~85% of the player's height ({ratio:0.00}).");
            float altarWidth = InGamePresentationSetup.AltarVisiblePixels.x / 100f * InGamePresentationSetup.AltarScale;
            float giftWidth = InGamePresentationSetup.GiftVisiblePixels.x / 100f * InGamePresentationSetup.GiftScale;
            Assert(Mathf.Abs(giftWidth / altarWidth - 0.5f) < 0.01f, "The gift must be about half the altar's width.");
            float lowest = InGamePresentationSetup.GiftBaseY - RewardGiftFloat.DefaultAmplitude -
                           InGamePresentationSetup.GiftHalfHeight;
            Assert(lowest > InGamePresentationSetup.AltarTopY, "The gift must still float at its lowest point.");
        }

        // ---------- Review captures (output/menu-review) ----------

        private static void CaptureInternal()
        {
            MenuScreenCapture.Capture("ingame-pause", Week13FrontendSetup.GameScenePath, Week13Hud1Setup.HudRootName,
                frame =>
                {
                    GamePauseArtifactView view = frame.GetComponentInChildren<GamePauseArtifactView>(true);
                    view.Overlay.alpha = 1f;
                    view.SeedValueText.text = "381726";
                    string[] sample = { "12", "1.25", "5.2", "15%", "150%", "1", "0" };
                    for (int i = 0; i < sample.Length; i++) view.StatValueTexts[i].text = sample[i];
                    view.CriticalChanceIcon.sprite = CriticalBurstSprites.Get(0);
                    ItemDefinition[] items = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                        .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                        .Where(item => item != null && item.Kind == ItemKind.Artifact &&
                                       UserArtwork.Load(item.ItemId) != null)
                        .Take(5).ToArray();
                    for (int i = 0; i < items.Length; i++)
                    {
                        ArtifactPauseListEntryView entry = Object.Instantiate(view.EntryTemplate, view.EntriesRoot);
                        entry.gameObject.SetActive(true);
                        entry.Bind(items[i], i == 2 ? 3 : 1);
                    }
                    view.EmptyText.gameObject.SetActive(items.Length == 0);
                    EventSystemFocus(view.ResumeButton);
                });
            MenuScreenCapture.Capture("ingame-hud", Week13FrontendSetup.GameScenePath, Week13Hud1Setup.HudRootName,
                frame =>
                {
                    GameFloorNameView banner = frame.GetComponentInChildren<GameFloorNameView>(true);
                    banner.AnnouncementCanvasGroup.alpha = 1f;
                    banner.AnnouncementText.text = banner.ResolveFloorName(1);
                    GameDetailedStatsHudView stats = frame.GetComponentInChildren<GameDetailedStatsHudView>(true);
                    stats.Content.alpha = 1f;
                    stats.ValuesText.text = "12\n1.25\n5.2\n15%";
                    stats.CriticalChanceIcon.sprite = CriticalBurstSprites.Get(0);
                    GamePowerHudView power = frame.GetComponentInChildren<GamePowerHudView>(true);
                    power.Configure(null, power.Icon, power.NameText, power.GaugeText, power.KeyText,
                        power.GaugeCells.ToArray());
                });
            // The banner a moment into its fog-in (reveal 0.35), to review the haze. The scene is reopened afterwards.
            MenuScreenCapture.Capture("ingame-banner-mist", Week13FrontendSetup.GameScenePath,
                Week13Hud1Setup.HudRootName, frame =>
                {
                    GameFloorNameView banner = frame.GetComponentInChildren<GameFloorNameView>(true);
                    const float reveal = 0.35f;
                    banner.AnnouncementCanvasGroup.alpha = BannerMist.Alpha(reveal);
                    banner.AnnouncementText.text = banner.ResolveFloorName(1);
                    banner.Mist.Render(reveal);
                });
            CaptureAltar();
        }

        private static void EventSystemFocus(Selectable selectable)
        {
            // Edit-mode capture: show the keyboard-focus growth on the primary button.
            MenuPopEffect pop = selectable != null ? selectable.GetComponent<MenuPopEffect>() : null;
            if (pop != null) pop.Target.localScale = Vector3.one * pop.HoverScale;
        }

        // The altar beside the player sprite at the player's scale, for checking size and the gift's height.
        private static void CaptureAltar()
        {
            string path = RoomPrefabPaths().First(candidate =>
                AssetDatabase.LoadAssetAtPath<GameObject>(candidate).GetComponentInChildren<RewardRoom>(true) != null);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Transform visual = prefab.GetComponentInChildren<RewardRoom>(true).transform
                .Find(InGamePresentationSetup.AltarVisualName);
            GameObject altar = Object.Instantiate(visual.gameObject);
            altar.transform.position = new Vector3(0f, InGamePresentationSetup.AltarBaseY, 0f);
            altar.GetComponentInChildren<RewardGiftFloat>().enabled = false;
            var player = new GameObject("Player", typeof(SpriteRenderer));
            player.GetComponent<SpriteRenderer>().sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/Player/Character_Erpin.png");
            player.GetComponent<SpriteRenderer>().sortingOrder = PlayerMovement.BodySortingOrder;
            player.transform.position = new Vector3(-1.3f, 0f, 0f);
            var cameraObject = new GameObject("Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.4f;
            camera.transform.position = new Vector3(-0.4f, 0.2f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.36f, 0.55f, 0.33f);
            var texture = new RenderTexture(960, 720, 24);
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                image.ReadPixels(new UnityEngine.Rect(0, 0, texture.width, texture.height), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory(MenuScreenCapture.OutputFolder);
                string output = Path.Combine(MenuScreenCapture.OutputFolder, "ingame-reward-altar.png");
                File.WriteAllBytes(output, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log("In-game presentation capture: " + output);
            }
            finally
            {
                camera.targetTexture = null;
                texture.Release();
                Object.DestroyImmediate(texture);
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            }
        }

        // ---------- Helpers ----------

        private static string[] RoomPrefabPaths() => AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Rooms/Prefabs" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();

        private static int CountTransforms() => EditorSceneManager.GetActiveScene().GetRootGameObjects()
            .Sum(root => root.GetComponentsInChildren<Transform>(true).Length);

        private static int CountRewardChildren() => RoomPrefabPaths()
            .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<RewardRoom>(true))
            .Where(reward => reward != null)
            .Sum(reward => reward.GetComponentsInChildren<Transform>(true).Length);

        private static float Left(RectTransform rect) => rect.anchoredPosition.x - rect.sizeDelta.x * 0.5f;
        private static float Right(RectTransform rect) => rect.anchoredPosition.x + rect.sizeDelta.x * 0.5f;

        private static bool Uses(Image image, string spriteName) =>
            image != null && image.sprite != null && image.sprite.name == spriteName;

        private static T Single<T>(Scene scene) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            Assert(matches.Length == 1, $"Game Scene requires one {typeof(T).Name}; found {matches.Length}.");
            return matches[0];
        }

        private static void Batch(Action action)
        {
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
