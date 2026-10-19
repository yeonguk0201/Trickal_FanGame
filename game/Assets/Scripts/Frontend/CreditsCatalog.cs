using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // The credit lines live in Resources/Credits.json, not in screen code: swapping a sound means editing
    // that file (next to tools/sfx-synth/import_sources.py's MANIFEST), never touching the credits screen.
    public static class CreditsCatalog
    {
        public const string ResourceName = "Credits";
        public const string AssetPath = "Assets/Resources/Credits.json";

        // CC BY 4.0 requires the modification notice, which is the easiest part of a credit line to miss.
        public const string ModifiedNotice = "modified";
        public const string Separator = "  ·  ";
        // Plain hyphen, not an em dash: the UI font (ONE Mobile POP) has no glyph for one.
        public const string AuthorSeparator = " - ";

        [Serializable]
        public sealed class Entry
        {
            public string title;
            public string author;
            public string source;
            public string licenseName;
            public string licenseUrl;
            public bool modified;
            public string usage;
            // True where the license itself demands the credit (CC BY). The verifier requires those entries
            // to carry every mandatory part; the rest are listed only so a source stays traceable.
            public bool attributionRequired;

            // Deliberately free of any wording that could read as the author endorsing this game (CC BY 4.0 forbids it).
            public string PrimaryLine =>
                string.IsNullOrWhiteSpace(author) ? Text(title) : Text(title) + AuthorSeparator + Text(author);

            public string LicenseLine
            {
                get
                {
                    var line = new StringBuilder();
                    if (!string.IsNullOrWhiteSpace(licenseName)) line.Append(licenseName.Trim());
                    if (modified) line.Append(line.Length > 0 ? " (" + ModifiedNotice + ")" : ModifiedNotice);
                    Append(line, source);
                    Append(line, licenseUrl);
                    return line.ToString();
                }
            }

            private static void Append(StringBuilder line, string part)
            {
                if (string.IsNullOrWhiteSpace(part)) return;
                if (line.Length > 0) line.Append(Separator);
                line.Append(part.Trim());
            }

            private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        [Serializable]
        public sealed class Section
        {
            public string heading;
            public string note;
            public Entry[] entries = Array.Empty<Entry>();

            public IReadOnlyList<Entry> Entries => entries ?? Array.Empty<Entry>();
        }

        [Serializable]
        private sealed class Document
        {
            public Section[] sections;
        }

        private static Section[] cached;

        public static IReadOnlyList<Section> Sections => Load();

        public static Section[] Load(bool reload = false)
        {
            if (cached != null && !reload) return cached;
            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null)
            {
                Debug.LogError($"[CreditsCatalog] {AssetPath} is missing; the credits screen would hide required attribution.");
                cached = Array.Empty<Section>();
                return cached;
            }
            cached = Parse(asset.text);
            return cached;
        }

        public static Section[] Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<Section>();
            Document document = JsonUtility.FromJson<Document>(json);
            return document?.sections ?? Array.Empty<Section>();
        }

        public static IEnumerable<Entry> AllEntries(IReadOnlyList<Section> sections)
        {
            if (sections == null) yield break;
            foreach (Section section in sections)
            {
                if (section == null) continue;
                foreach (Entry entry in section.Entries)
                    if (entry != null) yield return entry;
            }
        }

        // Every piece of fixed and data-driven copy the screen can show, so the Editor setup can preload
        // the glyphs the dynamic TMP font needs for it.
        public static string CollectGlyphSource(IReadOnlyList<Section> sections)
        {
            var text = new StringBuilder();
            text.Append(ModifiedNotice).Append(Separator);
            if (sections == null) return text.ToString();
            foreach (Section section in sections)
            {
                if (section == null) continue;
                text.Append(section.heading).Append(section.note);
                foreach (Entry entry in section.Entries)
                {
                    if (entry == null) continue;
                    text.Append(entry.PrimaryLine).Append(entry.LicenseLine).Append(entry.usage);
                }
            }
            return text.ToString();
        }
    }
}
