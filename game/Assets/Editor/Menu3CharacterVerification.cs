using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Character;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Menu-3 (docs/26 §3.5): character select and skill upgrade use the menu parts, the roster fills its grid with
    // "준비중" slots, selection shows a ring and a "선택됨" badge, and the info panel follows the selection.
    // Selection flow and upgrade requests are covered by Week13Frontend/Flow-4/Flow-5/SkillUpgrade verifiers.
    public static class Menu3CharacterVerification
    {
        [MenuItem("Trickal Fan Game/Menu/Verify Menu-3 Character Select and Skill Upgrade")]
        public static void Verify()
        {
            CharacterDefinition erpin = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Menu3CharacterSetup.ErpinPath);
            Assert(erpin != null && !string.IsNullOrWhiteSpace(erpin.Epithet) && erpin.SlotPortrait != null &&
                   erpin.BasicSkillIcon != null && !string.IsNullOrWhiteSpace(erpin.BasicSkillName) &&
                   erpin.HealthRating is > 0 and <= CharacterDefinition.MaxRating,
                "erpin needs its menu presentation data (epithet, slot portrait, ratings, basic skill).");

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendHomeView home = MenuUiBuilder.FindSingle<FrontendHomeView>(scene);
            FrontendCharacterSelectionView selection = home.CharacterSelectionView;
            var root = selection.transform;
            Assert(UsesSprite(root.GetComponent<Image>(), MenuArtSetup.Background), "Character select needs the library background.");
            Assert(UsesSprite(root.Find("RosterPanel")?.GetComponent<Image>(), MenuArtSetup.PanelRoster), "Missing roster panel.");
            Assert(UsesSprite(selection.DetailsView.GetComponent<Image>(), MenuArtSetup.PanelDetails), "Missing info panel.");
            Assert(selection.CardContainer.GetComponent<GridLayoutGroup>() != null &&
                   selection.CardContainer.GetComponent<HorizontalLayoutGroup>() == null,
                "Roster slots must sit in a grid.");
            Assert(selection.ComingSoonTemplate.GetComponent<Selectable>() == null &&
                   selection.ComingSoonTemplate.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == "준비중") &&
                   selection.ComingSoonTemplate.GetComponentsInChildren<Image>(true)
                       .All(image => image.sprite == null || image.sprite.name != MenuArtSetup.IconLock),
                "Coming-soon slots must say 준비중, use no lock and not be clickable.");
            Assert(selection.PreviousButton != null && selection.NextButton != null, "Missing previous/next arrows.");
            Transform slot = selection.CardTemplate.transform;
            Assert(slot.Find("PortraitMask")?.GetComponent<Mask>() != null && slot.Find("PortraitMask/CharacterIcon") != null,
                "The slot portrait must be clipped to the rounded slot.");
            foreach (Transform layer in new[] { slot.Find("HoverBorder"), slot.Find("FocusBorder"), slot.Find("SelectionBorder") })
                Assert(UsesSprite(layer?.GetComponent<Image>(), MenuArtSetup.Glow) &&
                       layer.GetSiblingIndex() < slot.Find("Frame").GetSiblingIndex(),
                    "Slot hover, focus and selection must glow behind the frame, not draw a box.");
            foreach (Button arrow in new[] { selection.PreviousButton, selection.NextButton })
                Assert(arrow.transform.Find("HoverBorder") == null && arrow.GetComponent<MenuPopEffect>() != null,
                    "Arrows show hover by growing, without a box.");

            // Build the screen on a throwaway copy, as the player sees it.
            GameObject clone = Object.Instantiate(selection.gameObject);
            try
            {
                FrontendCharacterSelectionView copy = clone.GetComponent<FrontendCharacterSelectionView>();
                copy.Show();
                Assert(copy.Cards.Count + copy.ComingSoonSlots.Count == Menu3CharacterSetup.RosterSlots,
                    "Roster must fill all slots with characters first, then 준비중 slots.");
                Assert(copy.SelectedCharacter == copy.Cards[0].Character && copy.ConfirmButton.interactable &&
                       copy.DetailsView.Character == copy.Cards[0].Character,
                    "Opening character select must start with the first character selected.");
                Assert(string.IsNullOrEmpty(copy.StatusText.text) && !copy.transform.Find("StatusPill").gameObject.activeSelf,
                    "Selecting must not show a status line.");
                copy.SelectNext();
                FrontendCharacterCardView card = copy.Cards.Single(c => c.Character == copy.SelectedCharacter);
                Transform border = card.transform.Find("SelectionBorder");
                Transform badge = card.transform.Find("SelectionBadge");
                Assert(border != null && border.gameObject.activeSelf && badge != null && badge.gameObject.activeSelf &&
                       badge.GetComponentInChildren<TMP_Text>(true).text == "선택됨",
                    "Selected slot must show the ring and the 선택됨 badge, not only a colour.");
                Assert(copy.DetailsView.NameText.text == copy.SelectedCharacter.DisplayName,
                    "Info panel must follow the selection.");
                copy.Show(); // reopening must not add slots
                Assert(copy.Cards.Count + copy.ComingSoonSlots.Count == Menu3CharacterSetup.RosterSlots,
                    "Reopening character select duplicated roster slots.");
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }

            FrontendSkillUpgradeView skill = home.SkillUpgradeView;
            Assert(UsesSprite(skill.GetComponent<Image>(), MenuArtSetup.Background), "Skill upgrade needs the library background.");
            Assert(skill.CharacterNameText != null, "Skill upgrade needs the character card name.");
            foreach (string panel in new[] { "LowGradeSkillPanel", "HighGradeSkillPanel" })
            {
                Transform card = skill.transform.Find(panel);
                Assert(UsesSprite(card.GetComponent<Image>(), MenuArtSetup.PanelMain) &&
                       card.Find("SkillTile/Icon")?.GetComponent<Image>().sprite != null,
                    panel + " must use the card art and show its skill icon.");
                Assert(card.Find("HeadingRibbon").GetSiblingIndex() < card.Find("Heading").GetSiblingIndex() &&
                       card.Find("LevelPill").GetSiblingIndex() < card.Find("SkillLevel").GetSiblingIndex(),
                    panel + " art must draw behind its text.");
            }
            foreach (Button button in new[] { skill.LowGradeUpgradeButton, skill.HighGradeUpgradeButton, skill.RetryButton,
                         skill.BackButton, selection.ConfirmButton, selection.BackButton })
                Assert(button.GetComponent<Image>().sprite != null && button.GetComponent<MenuPopEffect>() != null &&
                       button.transform.Find("FocusBorder") == null,
                    "Button must use the menu art and grow on hover/focus without an outline: " + button.name);

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true).Concat(skill.GetComponentsInChildren<TMP_Text>(true)))
                Assert(text.font == font && text.fontSize >= 16f, "Menu-3 text must use the menu font at 16px or more: " + text.name);
            foreach (Transform screen in new[] { root, skill.transform })
            {
                int duplicates = screen.GetComponentsInChildren<Transform>(true)
                    .GroupBy(t => (t.parent, t.name)).Count(group => group.Count() > 1);
                Assert(duplicates == 0, $"{screen.name} has {duplicates} duplicated child name(s) after a rerun.");
            }
            Debug.Log("Menu-3 verification passed.");
        }

        public static void SetupAndVerifyBatch() => Batch(() =>
        {
            Menu3CharacterSetup.Setup();
            Menu3CharacterSetup.Setup(); // rerun must not add objects
            Verify();
            Week13FrontendVerification.Verify();
            Week13SkillUpgradeVerification.Verify();
            Capture();
        });

        public static void CaptureBatch() => Batch(Capture);

        [MenuItem("Trickal Fan Game/Menu/Capture Menu-3 Screens")]
        public static void Capture()
        {
            MenuScreenCapture.Capture("menu3-character-select", frame =>
            {
                MenuScreenCapture.ShowOnly(frame, "CharacterSelectionPanel");
                FrontendCharacterSelectionView view = frame.GetComponentInChildren<FrontendCharacterSelectionView>(true);
                view.Show();
                view.SelectNext();
            });
            MenuScreenCapture.Capture("menu3-skill-upgrade", frame =>
            {
                MenuScreenCapture.ShowOnly(frame, "SkillUpgradePanel");
                FrontendSkillUpgradeView view = frame.GetComponentInChildren<FrontendSkillUpgradeView>(true);
                CharacterDefinition erpin = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Menu3CharacterSetup.ErpinPath);
                Transform card = view.transform.Find("CharacterCard");
                card.Find("CharacterName").GetComponent<TMP_Text>().text = erpin.DisplayName;
                card.Find("Portrait").GetComponent<Image>().sprite = erpin.Portrait;
                view.transform.Find("SkillUpgradeTitle").GetComponent<TMP_Text>().text = "에르핀 강화";
                view.SkillPointsText.text = "남은 스킬 포인트  2";
                view.CharacterLevelText.text = "캐릭터 레벨  Lv. 3 / 30";
                view.ExperienceText.text = "경험치  120 / 400";
                view.LowGradeLevelText.text = "Lv. 2 / 5";
                view.HighGradeLevelText.text = "Lv. 1 / 5";
                view.transform.Find("LowGradeSkillPanel/SkillEffect").GetComponent<TMP_Text>().text = "피해 110% · Lv.5에 투사체 +1";
                view.transform.Find("HighGradeSkillPanel/SkillEffect").GetComponent<TMP_Text>().text = "피해 200% · Lv.5에 쿨타임 -15%";
                view.transform.Find("LowGradeSkillPanel/Summary").GetComponent<TMP_Text>().text = erpin.LowGradeSkillName;
                view.transform.Find("HighGradeSkillPanel/Summary").GetComponent<TMP_Text>().text = erpin.HighGradeSkillName;
                view.StatusText.text = FrontendSkillUpgradeView.SelectMessage;
                view.RetryButton.gameObject.SetActive(true);
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

        private static bool UsesSprite(Image image, string name) =>
            image != null && image.sprite != null && image.sprite.name == name;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
