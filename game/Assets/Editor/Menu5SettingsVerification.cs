using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    // Menu-5 (docs/26 §3.5): the settings panel uses the menu parts, every control shows hover and focus outlines,
    // and nothing was duplicated by a rerun. Behaviour is covered by the Setting-1A/1B/1C and UX-0 verifiers.
    public static class Menu5SettingsVerification
    {
        [MenuItem("Trickal Fan Game/Menu/Verify Menu-5 Settings")]
        public static void Verify()
        {
            foreach (string name in MenuArtSetup.All)
            {
                var importer = AssetImporter.GetAtPath(MenuArtSetup.PathOf(name)) as TextureImporter;
                Assert(importer != null && importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled,
                    "Menu sprite import settings are wrong: " + name);
                Vector4 expected = MenuArtSetup.Borders.TryGetValue(name, out Vector4 border) ? border : Vector4.zero;
                Assert(importer.spriteBorder == expected, $"9-slice border of {name} is {importer.spriteBorder}, expected {expected}.");
            }

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendSettingsView view = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FrontendSettingsView>(true)).Single();
            var panel = (RectTransform)view.transform;
            Assert(panel.sizeDelta == Menu5SettingsSetup.PanelSize, "Settings panel size changed.");
            Assert(UsesSprite(panel.GetComponent<Image>(), MenuArtSetup.PanelMain), "Settings panel must use the main panel art.");
            Assert(UsesSprite(panel.Find("TitlePlaque")?.GetComponent<Image>(), MenuArtSetup.TitlePlaque),
                "Settings title must sit on the title plaque.");
            foreach (string ribbon in new[] { "VolumeSectionRibbon", "DisplaySectionRibbon", "HudSectionRibbon" })
                Assert(UsesSprite(panel.Find(ribbon)?.GetComponent<Image>(), MenuArtSetup.SectionRibbon), "Missing " + ribbon);

            foreach (Slider slider in new[] { view.MasterVolumeSlider, view.BgmVolumeSlider, view.SfxVolumeSlider })
            {
                Assert(UsesSprite(slider.fillRect.GetComponent<Image>(), MenuArtSetup.SliderFill) &&
                       UsesSprite(slider.handleRect.GetComponent<Image>(), MenuArtSetup.Clover),
                    "Slider must use the track, fill and clover handle art: " + slider.name);
                AssertGrows(slider, slider.handleRect);
                Assert(slider.transform.Find("Background").GetComponent<Image>().raycastTarget,
                    "Clicking anywhere on the volume bar must move the handle: " + slider.name);
            }
            AssertGrows(view.ResolutionDropdown, view.ResolutionDropdown.transform);
            AssertHighlight(view.FullscreenToggle);
            AssertHighlight(view.DetailedStatsHudToggle);
            Assert(UsesSprite(view.FullscreenToggle.graphic as Image, MenuArtSetup.Checkmark),
                "Checked state must show the checkmark, not only a colour change.");

            // Volume icons mute their channel and show a slash; a second click restores the earlier value.
            foreach ((string icon, Slider slider) in new[]
                     {
                         ("MasterVolumeIcon", view.MasterVolumeSlider), ("BgmVolumeIcon", view.BgmVolumeSlider),
                         ("SfxVolumeIcon", view.SfxVolumeSlider)
                     })
            {
                FrontendVolumeMuteButton mute = panel.Find(icon)?.GetComponent<FrontendVolumeMuteButton>();
                Assert(mute != null && mute.Slider == slider && mute.Slash != null && !mute.Slash.activeSelf,
                    icon + " must be a mute button for its own slider.");
            }
            GameObject clone = UnityEngine.Object.Instantiate(view.gameObject);
            try
            {
                FrontendVolumeMuteButton mute = clone.transform.Find("BgmVolumeIcon").GetComponent<FrontendVolumeMuteButton>();
                // Only the mute button runs: the settings view would load and save real LocalSettings.
                clone.GetComponent<FrontendSettingsView>().enabled = false;
                mute.Slider.SetValueWithoutNotify(0.6f);
                clone.SetActive(true);
                mute.Toggle();
                Assert(Mathf.Approximately(mute.Slider.value, 0f) && mute.Slash.activeSelf, "Mute must set 0 and show the slash.");
                mute.Toggle();
                Assert(Mathf.Approximately(mute.Slider.value, 0.6f) && !mute.Slash.activeSelf,
                    "Unmute must restore the value from before muting.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }

            var back = (FrontendStartButton)view.BackButton;
            Assert(UsesSprite(back.GetComponent<Image>(), MenuArtSetup.ButtonPrimary), "Back button must use the primary button art.");
            AssertGrows(back, back.transform);

            // Every text must stay inside the 1920 × 1080 frame and use the shared font.
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
                Assert(text.font == font && text.fontSize >= 20f, "Settings text must use the menu font at 20px or more: " + text.name);

            // Art behind text must draw first, or the text disappears under it.
            foreach ((string behind, string front) in new[]
                     {
                         ("TitlePlaque", "SettingsTitle"), ("VolumeSectionRibbon", "VolumeSection"),
                         ("DisplaySectionRibbon", "DisplaySection"), ("HudSectionRibbon", "HudSection"),
                         ("MasterVolumeValuePill", "MasterVolumeValue"), ("BgmVolumeValuePill", "BgmVolumeValue"),
                         ("SfxVolumeValuePill", "SfxVolumeValue")
                     })
                Assert(panel.Find(behind).GetSiblingIndex() < panel.Find(front).GetSiblingIndex(),
                    $"{behind} must draw behind {front}.");

            int duplicates = panel.GetComponentsInChildren<Transform>(true)
                .GroupBy(t => (t.parent, t.name)).Count(group => group.Count() > 1);
            Assert(duplicates == 0, $"Settings panel has {duplicates} duplicated child name(s) after a rerun.");
            Debug.Log("Menu-5 settings verification passed.");
        }

        public static void SetupAndVerifyBatch() => Batch(() =>
        {
            Menu5SettingsSetup.Setup();
            Menu5SettingsSetup.Setup(); // rerun must not add objects
            Verify();
            Week13Setting1CVerification.Verify();
            Week25Ux0Verification.Verify();
            CaptureBatchInternal();
        });

        public static void CaptureBatch() => Batch(CaptureBatchInternal);

        [MenuItem("Trickal Fan Game/Menu/Capture Menu-5 Settings")]
        public static void CaptureMenu() => CaptureBatchInternal();

        private static void CaptureBatchInternal()
        {
            MenuScreenCapture.Capture("menu5-settings", frame =>
            {
                MenuScreenCapture.ShowOnly(frame, "HomeScreen", "SettingsPanel");
                Transform home = frame.Find("HomeScreen");
                foreach (Transform child in home) child.gameObject.SetActive(false);
                FrontendSettingsView view = frame.GetComponentInChildren<FrontendSettingsView>(true);
                view.gameObject.SetActive(true);
                foreach (Slider slider in new[] { view.MasterVolumeSlider, view.BgmVolumeSlider, view.SfxVolumeSlider })
                    slider.SetValueWithoutNotify(0.8f);
                foreach (TMP_Text label in new[] { view.MasterVolumeLabel, view.BgmVolumeLabel, view.SfxVolumeLabel })
                    label.text = "80%";
                view.ResolutionDropdown.captionText.text = "1920 x 1080";
                view.SfxVolumeSlider.SetValueWithoutNotify(0f); // show the muted look on one row
                view.SfxVolumeLabel.text = "0%";
                view.transform.Find("SfxVolumeIcon/MutedSlash").gameObject.SetActive(true);
                view.FullscreenToggle.SetIsOnWithoutNotify(true);
                view.DetailedStatsHudToggle.SetIsOnWithoutNotify(false);
            });
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

        private static void AssertHighlight(Selectable selectable)
        {
            FrontendSelectableHighlight highlight = selectable.GetComponent<FrontendSelectableHighlight>();
            Assert(highlight != null, "Missing hover/focus outline on " + selectable.name);
            AssertRing(highlight.HoverBorder);
            AssertRing(highlight.FocusBorder);
        }

        // User feedback: buttons, sliders and the dropdown show hover/focus by growing, with no outline.
        private static void AssertGrows(Selectable selectable, Transform grown)
        {
            MenuPopEffect pop = selectable.GetComponent<MenuPopEffect>();
            Assert(pop != null && pop.Target == grown && selectable.transform.Find("HoverBorder") == null &&
                   selectable.transform.Find("FocusBorder") == null,
                "Hover/focus must grow without an outline: " + selectable.name);
        }

        private static void AssertRing(GameObject ring)
        {
            Image image = ring != null ? ring.GetComponent<Image>() : null;
            Assert(image != null && !image.raycastTarget && !ring.activeSelf &&
                   (UsesSprite(image, MenuArtSetup.RingPill) || UsesSprite(image, MenuArtSetup.Glow)),
                "Hover/focus outline must be a closed, non-blocking ring image: " + (ring != null ? ring.name : "null"));
        }

        private static bool UsesSprite(Image image, string name) =>
            image != null && image.sprite != null && image.sprite.name == name;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
