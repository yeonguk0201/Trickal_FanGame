using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Menu-5 (docs/26 §3.5): the credits screen uses the menu parts, headings get the ribbon row, BGM rows show the
    // music icon and sound rows the starburst. Credit text and licences are covered by CreditsScreenVerification.
    public static class Menu5CreditsVerification
    {
        [MenuItem("Trickal Fan Game/Menu/Verify Menu-5 Credits")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendCreditsView view = MenuUiBuilder.FindSingle<FrontendCreditsView>(scene);
            Assert(UsesSprite(view.GetComponent<Image>(), MenuArtSetup.Background), "Credits need the library background.");
            Assert(UsesSprite(view.transform.Find("CreditsBoard")?.GetComponent<Image>(), MenuArtSetup.PanelMain), "Missing credits board.");
            Assert(view.transform.Find("TitlePlaque").GetSiblingIndex() < view.transform.Find("CreditsTitle").GetSiblingIndex(),
                "Title plaque must draw behind the title.");
            Assert(view.SectionRowTemplate != null && !view.SectionRowTemplate.activeSelf, "Missing hidden section row template.");
            Assert(UsesSprite(view.EntryTemplate.GetComponent<Image>(), MenuArtSetup.PanelMain), "Credit rows must use the card art.");
            Assert(UsesSprite(view.BackButton.GetComponent<Image>(), MenuArtSetup.ButtonPrimary), "Back must use the primary button art.");

            GameObject clone = Object.Instantiate(view.gameObject);
            try
            {
                clone.SetActive(true);
                FrontendCreditsView copy = clone.GetComponent<FrontendCreditsView>();
                copy.Build(CreditsScreenSetup.LoadSections());
                GameObject[] headings = copy.Rows.Where(row => row.name == "Credits Section").ToArray();
                Assert(headings.Length > 0 && headings.All(row => row.transform.Find("Ribbon") != null),
                    "Section headings must use the ribbon row.");
                foreach (FrontendCreditsEntryView entry in copy.Entries)
                {
                    Image icon = entry.transform.Find("KindIcon").GetComponent<Image>();
                    bool music = entry.Entry.usage != null && entry.Entry.usage.Contains("BGM", StringComparison.OrdinalIgnoreCase);
                    Assert(UsesSprite(icon, music ? MenuArtSetup.IconMusic : MenuArtSetup.IconStarburst),
                        "Wrong credit row icon for " + entry.Entry.title);
                    // Every credit line must show in full, not only the CC BY ones Credits-0 checks.
                    float titleWidth = entry.PrimaryText.GetPreferredValues(entry.PrimaryText.text).x;
                    Assert(titleWidth <= entry.PrimaryText.rectTransform.sizeDelta.x,
                        $"{entry.Entry.title}: title line is wider than its box ({titleWidth:0}).");
                    Vector2 box = entry.LicenseText.rectTransform.sizeDelta;
                    float licenceHeight = entry.LicenseText.GetPreferredValues(entry.LicenseText.text, box.x, 0f).y;
                    Assert(licenceHeight <= box.y + 0.5f,
                        $"{entry.Entry.title}: licence needs {licenceHeight:0}px, more than two lines ({box.y:0}px).");
                }
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
            Debug.Log("Menu-5 credits verification passed.");
        }

        public static void SetupAndVerifyBatch() => Batch(() =>
        {
            Menu5CreditsSetup.Setup();
            CreditsScreenVerification.SetupAndVerifyBatch(); // Credits-0 twice, no duplicates, data still on screen
            Verify();
            Capture();
        });

        public static void CaptureBatch() => Batch(Capture);

        [MenuItem("Trickal Fan Game/Menu/Capture Menu-5 Credits")]
        public static void Capture()
        {
            MenuScreenCapture.Capture("menu5-credits", frame =>
            {
                MenuScreenCapture.ShowOnly(frame, CreditsScreenSetup.PanelName);
                FrontendCreditsView view = frame.GetComponentInChildren<FrontendCreditsView>(true);
                view.gameObject.SetActive(true);
                view.Build(CreditsScreenSetup.LoadSections());
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
