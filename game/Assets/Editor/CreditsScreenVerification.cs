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
    // Credits-0. The invariant this piece exists for: the CC BY 4.0 sources must reach the screen with every
    // mandatory part of their credit. Shipping without them is a licence violation, and a missing line is
    // invisible by eye, so each part is checked here and the check fails loudly.
    public static class CreditsScreenVerification
    {
        // Mirrors the confirmed credit block in docs/audio-sources.md ("크레딧 표기 의무"). Kept in the verifier
        // rather than in the data, so deleting an entry from Credits.json fails instead of passing quietly.
        private static readonly (string Source, string Author)[] RequiredAttribution =
        {
            ("https://freesound.org/s/541478/", "EminYILDIRIM"),
            ("https://freesound.org/s/396500/", "alonsotm"),
        };

        private const string RequiredLicenseName = "Attribution 4.0";
        private const string RequiredLicenseUrl = "https://creativecommons.org/licenses/by/4.0/";

        // CC BY 4.0 forbids wording that suggests the author endorses or sponsors this game.
        private static readonly string[] ForbiddenEndorsementWords = { "후원", "보증", "제공받", "endors", "sponsor" };

        [MenuItem("Trickal Fan Game/Credits/Verify Credits Screen")]
        public static void Verify()
        {
            CreditsCatalog.Section[] sections = CreditsScreenSetup.LoadSections();
            VerifyData(sections);
            VerifyRuntimeLoad(sections);
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendCreditsView view = VerifyScene(scene);
            VerifyRenderedRows(view, sections);
            VerifyRoundTrip();
            Debug.Log($"Credits-0 verification passed: {RequiredAttribution.Length} CC BY 4.0 credits complete on " +
                      $"screen, {CreditsCatalog.AllEntries(sections).Count()} entries from Resources/Credits.json, " +
                      "both routes in and the way back.");
        }

        [MenuItem("Trickal Fan Game/Credits/Setup and Verify Credits Screen")]
        public static void SetupAndVerifyBatch()
        {
            string guid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath);
            CreditsScreenSetup.Setup();
            int transforms = CountTransforms();
            CreditsScreenSetup.Setup();
            Assert(transforms == CountTransforms(), "Repeated Credits-0 setup duplicated scene objects.");
            Assert(guid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath),
                "Credits-0 setup changed the FrontendScene GUID.");
            Verify();
            Debug.Log("Credits-0 batch verification passed: setup twice without duplicates and a stable Scene GUID.");
        }

        // Credits-0 adds fields to the title and home views and a panel to the Frontend reference frame, so the
        // Frontend flow itself is re-checked before a commit.
        public static void VerifyWithRegressionsBatch()
        {
            CreditsScreenSetup.Setup();
            Verify();
            EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            Week13FrontendVerification.Verify();
            Week13Setting1AVerification.Verify();
            Debug.Log("Credits-0 regression verification passed: Frontend flow and Setting-1A audio still hold.");
        }

        private static int CountTransforms() => Object
            .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        private static void VerifyData(CreditsCatalog.Section[] sections)
        {
            CreditsCatalog.Entry[] entries = CreditsCatalog.AllEntries(sections).ToArray();
            Assert(entries.Length > 0, "The credits data holds no entries.");
            Assert(sections.All(section => !string.IsNullOrWhiteSpace(section.heading)),
                "Every credits section needs a heading.");
            Assert(entries.All(entry => !string.IsNullOrWhiteSpace(entry.title)),
                "Every credits entry needs a title.");
            Assert(entries.All(entry => !string.IsNullOrWhiteSpace(entry.source)),
                "Every credits entry needs a source, so a swapped sound stays traceable.");

            foreach (CreditsCatalog.Entry entry in entries.Where(entry => entry.attributionRequired))
                VerifyAttributionComplete(entry);

            foreach ((string source, string author) in RequiredAttribution)
            {
                CreditsCatalog.Entry[] matches = entries
                    .Where(entry => string.Equals(entry.source?.Trim(), source, StringComparison.Ordinal)).ToArray();
                Assert(matches.Length == 1,
                    $"The credits data must hold exactly one entry for the CC BY 4.0 source {source}; found {matches.Length}.");
                CreditsCatalog.Entry entry = matches[0];
                Assert(entry.attributionRequired,
                    $"{source} is CC BY 4.0, so its entry must be marked attributionRequired.");
                Assert(string.Equals(entry.author?.Trim(), author, StringComparison.Ordinal),
                    $"{source} must credit {author}; found \"{entry.author}\".");
                Assert(entry.licenseName != null && entry.licenseName.Contains(RequiredLicenseName, StringComparison.Ordinal),
                    $"{source} must name the licence \"{RequiredLicenseName}\"; found \"{entry.licenseName}\".");
                Assert(string.Equals(entry.licenseUrl?.Trim(), RequiredLicenseUrl, StringComparison.Ordinal),
                    $"{source} must link the licence at {RequiredLicenseUrl}; found \"{entry.licenseUrl}\".");
                Assert(entry.modified,
                    $"{source} was resampled, trimmed, levelled and layered, so its entry must carry the modification notice.");
            }

            foreach (CreditsCatalog.Section section in sections)
            {
                VerifyNoEndorsement(section.heading);
                VerifyNoEndorsement(section.note);
                foreach (CreditsCatalog.Entry entry in section.Entries)
                {
                    VerifyNoEndorsement(entry.PrimaryLine);
                    VerifyNoEndorsement(entry.LicenseLine);
                    VerifyNoEndorsement(entry.usage);
                }
            }
        }

        private static void VerifyAttributionComplete(CreditsCatalog.Entry entry)
        {
            string label = entry.title ?? "(untitled)";
            Assert(!string.IsNullOrWhiteSpace(entry.author), $"{label} requires attribution but names no author.");
            Assert(entry.source != null && entry.source.StartsWith("http", StringComparison.OrdinalIgnoreCase),
                $"{label} requires attribution, so its source must be a link; found \"{entry.source}\".");
            Assert(!string.IsNullOrWhiteSpace(entry.licenseName), $"{label} requires attribution but names no licence.");
            Assert(entry.licenseUrl != null && entry.licenseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase),
                $"{label} requires attribution, so its licence must be linked; found \"{entry.licenseUrl}\".");

            string primary = entry.PrimaryLine;
            string license = entry.LicenseLine;
            Assert(primary.Contains(entry.title.Trim(), StringComparison.Ordinal) &&
                   primary.Contains(entry.author.Trim(), StringComparison.Ordinal),
                $"{label}: the displayed line drops the title or the author.");
            Assert(license.Contains(entry.licenseName.Trim(), StringComparison.Ordinal) &&
                   license.Contains(entry.source.Trim(), StringComparison.Ordinal) &&
                   license.Contains(entry.licenseUrl.Trim(), StringComparison.Ordinal),
                $"{label}: the displayed licence line drops the licence name, the source or the licence link.");
            Assert(!entry.modified || license.Contains(CreditsCatalog.ModifiedNotice, StringComparison.Ordinal),
                $"{label}: the modification notice is missing from the displayed licence line.");
        }

        private static void VerifyNoEndorsement(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (string word in ForbiddenEndorsementWords)
                Assert(!text.Contains(word, StringComparison.OrdinalIgnoreCase),
                    $"Credits copy must not read as an endorsement by the author (CC BY 4.0): \"{text}\".");
        }

        // The screen reads the data through Resources at runtime, not through AssetDatabase.
        private static void VerifyRuntimeLoad(CreditsCatalog.Section[] expected)
        {
            CreditsCatalog.Section[] loaded = CreditsCatalog.Load(reload: true);
            Assert(loaded.Length == expected.Length,
                $"Resources.Load returned {loaded.Length} credits sections; the asset holds {expected.Length}.");
            Assert(CreditsCatalog.AllEntries(loaded).Count() == CreditsCatalog.AllEntries(expected).Count(),
                "Resources.Load returned a different number of credit entries than the asset holds.");
        }

        private static FrontendCreditsView VerifyScene(Scene scene)
        {
            Component[] components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            FrontendCreditsView[] views = components.OfType<FrontendCreditsView>().ToArray();
            Assert(views.Length == 1, $"FrontendScene requires exactly one credits screen; found {views.Length}.");
            FrontendCreditsView view = views[0];
            Assert(view.name == CreditsScreenSetup.PanelName,
                $"The credits screen must keep the stable name {CreditsScreenSetup.PanelName}.");
            Assert(!view.gameObject.activeSelf, "The credits screen must start hidden.");
            Assert(view.ScrollRect != null && view.Content != null && view.SectionTemplate != null &&
                   view.EntryTemplate != null && view.EmptyMessageText != null && view.BackButton != null,
                "Missing credits screen references.");
            Assert(!view.SectionTemplate.gameObject.activeSelf && !view.EntryTemplate.gameObject.activeSelf,
                "The credits row templates must stay inactive in the saved Scene.");
            Assert(view.EntryTemplate.PrimaryText != null && view.EntryTemplate.LicenseText != null &&
                   view.EntryTemplate.UsageText != null, "Missing credits row references.");
            Assert(view.Rows.Count == 0, "The saved Scene must not hold pre-built credit rows.");
            Assert(view.BackButton.onClick.GetPersistentEventCount() == 0,
                "The credits actions must be wired once at runtime, not as persistent Scene calls.");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null, "The Frontend TMP font is missing.");
            foreach (TMP_Text text in view.GetComponentsInChildren<TMP_Text>(true))
                Assert(text.font == font, $"The credits screen must use the Frontend TMP font: {text.name}.");

            FrontendTitleView title = components.OfType<FrontendTitleView>().Single();
            FrontendHomeView home = components.OfType<FrontendHomeView>().Single();
            Assert(title.CreditsView == view && title.CreditsPanel == view.gameObject,
                "The title view must hold the credits screen.");
            Assert(title.CreditsButton != null && title.CreditsButton.gameObject.scene == scene &&
                   title.CreditsButton.name == CreditsScreenSetup.TitleButtonName,
                "The title screen needs the credits button, so the credit stays reachable without a profile.");
            Assert(title.CreditsButton.transform.parent == title.TitlePanel.transform,
                "The title credits button must sit on the title panel.");
            Assert(home.CreditsButton != null && home.CreditsButton.gameObject.scene == scene &&
                   home.CreditsButton.name == CreditsScreenSetup.HomeButtonName,
                "The home menu needs the credits button, so the credit stays reachable after a run.");
            Assert(title.CreditsButton.onClick.GetPersistentEventCount() == 0 &&
                   home.CreditsButton.onClick.GetPersistentEventCount() == 0,
                "The credits routes must be wired once at runtime, not as persistent Scene calls.");

            Assert(title.StartButton.navigation.mode == Navigation.Mode.Explicit &&
                   title.StartButton.navigation.selectOnDown == title.CreditsButton &&
                   title.CreditsButton.navigation.selectOnUp == title.StartButton,
                "The title credits button must be reachable with the keyboard. Re-run Setup Credits Screen " +
                "if Setup Frontend Flow has been run since.");
            Assert(home.QuitButton.navigation.selectOnDown == home.CreditsButton &&
                   home.CreditsButton.navigation.selectOnUp == home.QuitButton,
                "The home credits button must be reachable with the keyboard. Re-run Setup Credits Screen " +
                "if Setup Frontend Flow has been run since.");
            return view;
        }

        // Builds the rows the player would see, on a throwaway copy, and reads the required credit back out of
        // the TMP text. Data alone is not evidence that the line reaches the screen.
        private static void VerifyRenderedRows(FrontendCreditsView view, CreditsCatalog.Section[] sections)
        {
            GameObject clone = Object.Instantiate(view.gameObject);
            try
            {
                clone.SetActive(true);
                FrontendCreditsView copy = clone.GetComponent<FrontendCreditsView>();
                copy.Build(sections);
                CreditsCatalog.Entry[] expected = CreditsCatalog.AllEntries(sections).ToArray();
                Assert(copy.Entries.Count == expected.Length,
                    $"The credits screen built {copy.Entries.Count} rows for {expected.Length} entries.");
                Assert(copy.EmptyMessageText == null || !copy.EmptyMessageText.gameObject.activeSelf,
                    "The empty-credits message must stay hidden while entries exist.");

                // Rebuilding must not stack rows: reopening the screen calls Build again.
                copy.Build(sections);
                Assert(copy.Entries.Count == expected.Length, "Rebuilding the credits screen duplicated rows.");

                foreach ((string source, string author) in RequiredAttribution)
                {
                    FrontendCreditsEntryView[] rows = copy.Entries
                        .Where(row => row.Entry != null &&
                                      string.Equals(row.Entry.source?.Trim(), source, StringComparison.Ordinal))
                        .ToArray();
                    Assert(rows.Length == 1, $"{source} must appear on the credits screen exactly once.");
                    FrontendCreditsEntryView row = rows[0];
                    Assert(row.gameObject.activeSelf, $"{source}: its credit row is hidden.");
                    string primary = row.PrimaryText.text;
                    string license = row.LicenseText.text;
                    Assert(primary.Contains(author, StringComparison.Ordinal),
                        $"{source}: the author is missing from the row on screen.");
                    Assert(license.Contains(source, StringComparison.Ordinal),
                        $"{source}: the original link is missing from the row on screen.");
                    Assert(license.Contains(RequiredLicenseName, StringComparison.Ordinal),
                        $"{source}: the licence name is missing from the row on screen.");
                    Assert(license.Contains(RequiredLicenseUrl, StringComparison.Ordinal),
                        $"{source}: the licence link is missing from the row on screen.");
                    Assert(license.Contains(CreditsCatalog.ModifiedNotice, StringComparison.Ordinal),
                        $"{source}: the modification notice is missing from the row on screen.");
                    AssertFits(row.PrimaryText, source + " author line");
                    AssertFits(row.LicenseText, source + " licence line");
                }

                foreach (string heading in sections.Select(section => section.heading))
                    Assert(copy.Rows.Any(child => child != null &&
                            child.GetComponent<TMP_Text>() is { } text && text.text == heading.Trim()),
                        $"The credits screen dropped the section heading \"{heading}\".");
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        // A required line that runs past its box would be unreadable on screen, which is the same failure as
        // leaving it out. Checked only for the lines the licence demands.
        private static void AssertFits(TMP_Text text, string label)
        {
            text.ForceMeshUpdate(true);
            Assert(text.preferredWidth <= text.rectTransform.rect.width + 1f,
                $"{label} is wider than its box ({text.preferredWidth:F0} > {text.rectTransform.rect.width:F0}).");
        }

        // Entering the credits screen and coming back, without touching the saved Scene.
        private static void VerifyRoundTrip()
        {
            var host = new GameObject("Credits-0 Route Verification");
            try
            {
                var titlePanel = new GameObject("TitlePanel");
                var creditsPanel = new GameObject("CreditsPanel");
                titlePanel.transform.SetParent(host.transform, false);
                creditsPanel.transform.SetParent(host.transform, false);
                FrontendTitleView title = host.AddComponent<FrontendTitleView>();
                title.ConfigureTitlePanel(titlePanel);
                title.ConfigureCredits(creditsPanel, null, null);

                title.ShowCredits(false);
                Assert(creditsPanel.activeSelf && !titlePanel.activeSelf,
                    "Opening the credits screen must replace the title screen.");
                title.CloseCredits();
                Assert(!creditsPanel.activeSelf && titlePanel.activeSelf,
                    "Leaving the credits screen must return to the title screen.");

                title.ShowCredits(true);
                Assert(creditsPanel.activeSelf && title.CreditsReturnToHome,
                    "Opening the credits screen from Home must remember where to return.");
                title.CloseCredits();
                Assert(!creditsPanel.activeSelf && !title.CreditsReturnToHome,
                    "Leaving the credits screen must clear the remembered return route.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
