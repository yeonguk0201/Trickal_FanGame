using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static TrickalFanGame.Editor.MenuUiBuilder;

namespace TrickalFanGame.Editor
{
    // Menu-5 (docs/26 §3.5): the settings panel in the magic-library look. Only appearance and layout change; every
    // control, name and FrontendSettingsView reference stays, so LocalSettings behaviour is untouched. The in-game
    // pause settings are a copy of this panel, so UX-0 setup runs afterwards to copy the new look into the Game Scene.
    public static class Menu5SettingsSetup
    {
        public static readonly Vector2 PanelSize = new(1040, 760);
        private const float LabelX = -380f;      // left edge of row labels
        private const float ControlX = 70f;      // centre of sliders and the dropdown
        private const float ControlWidth = 420f;
        private const float ValueX = 360f;
        private const float ToggleX = -116f;     // checkboxes line up with the slider start

        private static readonly (string section, string ribbon, float y)[] Sections =
        {
            ("VolumeSection", "VolumeSectionRibbon", 268f),
            ("DisplaySection", "DisplaySectionRibbon", 22f),
            ("HudSection", "HudSectionRibbon", -168f),
        };

        [MenuItem("Trickal Fan Game/Menu/Setup Menu-5 Settings")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Menu-5 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            MenuArtSetup.Apply();

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendSettingsView view = FindSingle<FrontendSettingsView>(scene);
            FrontendHomeView home = FindSingle<FrontendHomeView>(scene);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Menu-5 Settings");

            // The settings panel opens over the home screen, so home gets the shared library background too.
            Skin(home.gameObject, MenuArtSetup.Background, Color.white);
            StylePanel(view);

            foreach (Component component in view.GetComponentsInChildren<Component>(true)) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(home.GetComponent<Image>());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Frontend save failed.");
            Undo.CollapseUndoOperations(group);

            // Copy the restyled panel into the pause menu (same parts, same LocalSettings).
            Week25Ux0Setup.Setup();
            Debug.Log("Menu-5 settings setup complete. Frontend and in-game settings use the magic-library parts.");
        }

        private static void StylePanel(FrontendSettingsView view)
        {
            var panel = (RectTransform)view.transform;
            Undo.RecordObject(panel, "Size settings panel");
            panel.sizeDelta = PanelSize;
            Skin(panel.gameObject, MenuArtSetup.PanelMain, Color.white, raycast: true);

            // Title plaque straddles the top edge like the reference screen.
            TMP_Text title = Require<TMP_Text>(panel, "SettingsTitle");
            Image plaque = Picture(panel, "TitlePlaque", MenuArtSetup.TitlePlaque, new Vector2(0, 392), new Vector2(500, 184));
            DrawBehind(plaque.transform, title.transform);
            Place(title.rectTransform, new Vector2(0, 396), new Vector2(320, 64));
            StyleText(title, 44, Ink);

            foreach ((string sectionName, string ribbonName, float y) in Sections)
            {
                TMP_Text section = Require<TMP_Text>(panel, sectionName);
                RectTransform ribbon = Node(panel, ribbonName, new Vector2(-330, y), new Vector2(260, 50));
                Skin(ribbon.gameObject, MenuArtSetup.SectionRibbon, Color.white, fitHeight: 50);
                DrawBehind(ribbon, section.transform);
                Picture(ribbon, "Clover", MenuArtSetup.Clover, new Vector2(-92, 1), new Vector2(34, 34));
                Place(section.rectTransform, new Vector2(-312, y + 1), new Vector2(170, 44));
                StyleText(section, 26, RibbonText, TextAlignmentOptions.Left);
            }

            StyleSliderRow(view, panel, "MasterVolume", view.MasterVolumeSlider, view.MasterVolumeLabel, MenuArtSetup.IconSpeaker, 208);
            StyleSliderRow(view, panel, "BgmVolume", view.BgmVolumeSlider, view.BgmVolumeLabel, MenuArtSetup.IconMusic, 152);
            StyleSliderRow(view, panel, "SfxVolume", view.SfxVolumeSlider, view.SfxVolumeLabel, MenuArtSetup.IconSparkles, 96);
            Divider(panel, "VolumeDivider", 60);

            RowLabel(panel, "ResolutionLabel", "ResolutionIcon", MenuArtSetup.IconMonitor, -36);
            StyleDropdown(view.ResolutionDropdown, new Vector2(ControlX - 50, -36), new Vector2(ControlWidth - 100, 50));
            RowLabel(panel, "FullscreenLabel", "FullscreenIcon", MenuArtSetup.IconFullscreen, -92);
            StyleToggle(view.FullscreenToggle, new Vector2(ToggleX, -92));
            Divider(panel, "DisplayDivider", -132);

            RowLabel(panel, "DetailedStatsHudLabel", "DetailedStatsHudIcon", MenuArtSetup.IconList, -226);
            StyleToggle(view.DetailedStatsHudToggle, new Vector2(ToggleX, -226));

            var back = (FrontendStartButton)view.BackButton;
            Place((RectTransform)back.transform, new Vector2(0, -318), new Vector2(300, 84));
            StyleButton(back, true, new Vector2(300, 84), 30);
        }

        private static void StyleSliderRow(FrontendSettingsView view, RectTransform panel, string prefix, Slider slider,
            TMP_Text value, string icon, float y)
        {
            RowLabel(panel, prefix + "Label", prefix + "Icon", icon, y);
            MuteButton((RectTransform)panel.Find(prefix + "Icon"), slider);
            var rect = (RectTransform)slider.transform;
            Place(rect, new Vector2(ControlX, y), new Vector2(ControlWidth, 30));
            // Clicking anywhere on the bar moves the handle there, not only dragging the clover.
            Skin(Require<RectTransform>(rect, "Background").gameObject, MenuArtSetup.SliderTrack, Color.white, raycast: true,
                fitHeight: 30);

            RectTransform fillArea = Require<RectTransform>(rect, "Fill Area");
            Undo.RecordObject(fillArea, "Inset fill");
            fillArea.offsetMin = new Vector2(3, 3);
            fillArea.offsetMax = new Vector2(-3, -3);
            Skin(Require<RectTransform>(fillArea, "Fill").gameObject, MenuArtSetup.SliderFill, Color.white, fitHeight: 24);

            RectTransform handleArea = Require<RectTransform>(rect, "Handle Slide Area");
            Undo.RecordObject(handleArea, "Inset handle");
            handleArea.offsetMin = new Vector2(14, 0);
            handleArea.offsetMax = new Vector2(-14, 0);
            RectTransform handle = Require<RectTransform>(handleArea, "Handle");
            Undo.RecordObject(handle, "Size handle");
            handle.sizeDelta = new Vector2(46, 16); // anchors span the 30px height, so the clover shows at 46px
            Image handleImage = Skin(handle.gameObject, MenuArtSetup.Clover, Color.white, raycast: true, preserveAspect: true);
            Undo.RecordObject(slider, "Handle graphic");
            slider.targetGraphic = handleImage;
            Highlight(slider, handle, 1.2f, 1.2f); // the handle stays large while dragging

            Image pill = Skin(Node(panel, prefix + "ValuePill", new Vector2(ValueX, y), new Vector2(108, 44)).gameObject,
                MenuArtSetup.ButtonSecondary, Color.white, fitHeight: 44);
            DrawBehind(pill.transform, value.transform);
            Place(value.rectTransform, new Vector2(ValueX, y), new Vector2(100, 40));
            StyleText(value, 22, Ink);
        }

        // Clicking a volume icon mutes that channel (slash over the icon); clicking again restores it.
        private static void MuteButton(RectTransform tile, Slider slider)
        {
            Image image = tile.GetComponent<Image>();
            Undo.RecordObject(image, "Mute hit area");
            image.raycastTarget = true;
            Button button = Ensure<Button>(tile.gameObject);
            button.targetGraphic = image;
            button.colors = Colors();
            button.navigation = new Navigation { mode = Navigation.Mode.None }; // keyboard changes volume on the slider
            Image slash = Picture(tile, "MutedSlash", MenuArtSetup.Slash, Vector2.zero, new Vector2(38, 38));
            slash.transform.SetAsLastSibling();
            slash.gameObject.SetActive(false);
            Ensure<FrontendVolumeMuteButton>(tile.gameObject).Configure(slider, slash.gameObject);
            Pop(button);
        }

        private static void RowLabel(RectTransform panel, string labelName, string iconName, string icon, float y)
        {
            TMP_Text label = Require<TMP_Text>(panel, labelName);
            RectTransform tile = Node(panel, iconName, new Vector2(LabelX + 20, y), new Vector2(44, 40));
            Skin(tile.gameObject, MenuArtSetup.ButtonSecondary, Color.white, fitHeight: 40);
            Picture(tile, "Glyph", icon, Vector2.zero, new Vector2(26, 26));
            DrawBehind(tile, label.transform);
            Place(label.rectTransform, new Vector2(LabelX + 140, y), new Vector2(180, 40));
            StyleText(label, 24, Ink, TextAlignmentOptions.Left);
        }

        private static void Divider(RectTransform panel, string name, float y)
        {
            RectTransform line = Node(panel, name, new Vector2(0, y), new Vector2(860, 2));
            Image image = Ensure<Image>(line.gameObject);
            image.sprite = null;
            image.color = MenuUiBuilder.Divider;
            image.raycastTarget = false;
            Picture(line, "Clover", MenuArtSetup.Clover, Vector2.zero, new Vector2(22, 22), new Color(0.93f, 0.76f, 0.55f));
        }

        private static void StyleDropdown(TMP_Dropdown dropdown, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)dropdown.transform;
            Place(rect, position, size);
            Image background = Skin(dropdown.gameObject, MenuArtSetup.SliderTrack, Color.white, raycast: true, fitHeight: size.y);
            Undo.RecordObject(dropdown, "Style dropdown");
            dropdown.targetGraphic = background;
            StyleText(dropdown.captionText, 22, Ink, TextAlignmentOptions.Left);
            dropdown.captionText.rectTransform.offsetMin = new Vector2(24, 4);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-48, -4);

            RectTransform arrow = Require<RectTransform>(rect, "Arrow");
            Undo.RecordObject(arrow, "Arrow");
            arrow.anchorMin = arrow.anchorMax = new Vector2(1, 0.5f);
            arrow.anchoredPosition = new Vector2(-26, 0);
            arrow.sizeDelta = new Vector2(14, 22);
            arrow.localRotation = Quaternion.Euler(0, 0, 90); // the left arrow turned to point down
            Skin(arrow.gameObject, MenuArtSetup.ArrowLeft, Accent, preserveAspect: true);

            RectTransform template = (RectTransform)dropdown.template;
            Undo.RecordObject(template, "Template");
            template.sizeDelta = new Vector2(size.x, 168); // three 40px items plus the frame
            template.anchoredPosition = new Vector2(0, -size.y / 2 - 4);
            Skin(template.gameObject, MenuArtSetup.PanelMain, Color.white, raycast: true);
            template.GetComponent<Image>().pixelsPerUnitMultiplier = 3f; // 24px corners on a short list
            RectTransform viewport = Require<RectTransform>(template, "Viewport");
            Undo.RecordObject(viewport, "Viewport");
            // TMP_Dropdown shrinks the list to the items' height, ignoring viewport insets, so a vertical inset
            // would clip the last item.
            viewport.offsetMin = new Vector2(8, 0);
            viewport.offsetMax = new Vector2(-8, 0);

            Toggle item = template.GetComponentsInChildren<Toggle>(true).Single();
            Image itemBackground = Require<Image>((RectTransform)item.transform, "Item Background");
            Undo.RecordObject(itemBackground, "Item background");
            itemBackground.sprite = null;
            itemBackground.color = new Color(1f, 1f, 1f, 0f);
            Undo.RecordObject(item, "Item colors");
            ColorBlock itemColors = Colors();
            itemColors.highlightedColor = new Color(0.93f, 0.86f, 0.97f, 1f);
            itemColors.selectedColor = new Color(0.93f, 0.86f, 0.97f, 1f);
            item.colors = itemColors;
            Image check = Require<Image>((RectTransform)item.transform, "Item Checkmark");
            Skin(check.gameObject, MenuArtSetup.Clover, Color.white, preserveAspect: true);
            Undo.RecordObject(check.rectTransform, "Check");
            check.rectTransform.anchoredPosition = new Vector2(18, 0);
            check.rectTransform.sizeDelta = new Vector2(22, 22);
            StyleText(dropdown.itemText, 22, Ink, TextAlignmentOptions.Left);
            dropdown.itemText.rectTransform.offsetMin = new Vector2(40, 2);

            Highlight(dropdown);
        }

        private static void StyleToggle(Toggle toggle, Vector2 position)
        {
            var rect = (RectTransform)toggle.transform;
            Place(rect, position, new Vector2(48, 48));
            RectTransform background = Require<RectTransform>(rect, "Background");
            Place(background, Vector2.zero, new Vector2(48, 48));
            Image box = Skin(background.gameObject, MenuArtSetup.Checkbox, Color.white, raycast: true, preserveAspect: true);
            RectTransform checkmark = Require<RectTransform>(rect, "Checkmark");
            Place(checkmark, new Vector2(0, 1), new Vector2(32, 26));
            Image check = Skin(checkmark.gameObject, MenuArtSetup.Checkmark, Color.white, preserveAspect: true);
            Undo.RecordObject(toggle, "Style toggle");
            toggle.targetGraphic = box;
            toggle.graphic = check;
            // A rounded-square ring looked like a box, so the checkbox glows behind and grows a little instead.
            FrontendSelectableHighlight highlight = Ensure<FrontendSelectableHighlight>(toggle.gameObject);
            highlight.ConfigureBorders(GlowLayer(rect, "HoverBorder", HoverGlow, 12f, 1),
                GlowLayer(rect, "FocusBorder", FocusGlow, 14f, 0));
            toggle.colors = Colors();
            Pop(toggle);
        }
    }
}
