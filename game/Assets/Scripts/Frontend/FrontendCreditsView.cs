using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // Builds its rows from CreditsCatalog, so a changed sound source needs no change here.
    public sealed class FrontendCreditsView : MonoBehaviour
    {
        public const string ScreenTitle = "크레딧";
        public const string EmptyMessage = "표시할 크레딧 항목이 없습니다.";

        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text sectionTemplate;
        [SerializeField] private FrontendCreditsEntryView entryTemplate;
        [SerializeField] private TMP_Text emptyMessage;
        [SerializeField] private Button backButton;

        private readonly List<GameObject> rows = new();
        private readonly List<FrontendCreditsEntryView> entries = new();

        public ScrollRect ScrollRect => scrollRect;
        public RectTransform Content => content;
        public TMP_Text SectionTemplate => sectionTemplate;
        public FrontendCreditsEntryView EntryTemplate => entryTemplate;
        public TMP_Text EmptyMessageText => emptyMessage;
        public Button BackButton => backButton;
        public IReadOnlyList<GameObject> Rows => rows;
        public IReadOnlyList<FrontendCreditsEntryView> Entries => entries;

        public event Action OnBackRequested;

        public void Configure(ScrollRect scroll, RectTransform contentRoot, TMP_Text section,
            FrontendCreditsEntryView entry, TMP_Text empty, Button back)
        {
            scrollRect = scroll;
            content = contentRoot;
            sectionTemplate = section;
            entryTemplate = entry;
            emptyMessage = empty;
            backButton = back;
        }

        private void OnEnable()
        {
            if (backButton != null) backButton.onClick.AddListener(RequestBack);
            Build();
        }

        private void OnDisable()
        {
            if (backButton != null) backButton.onClick.RemoveListener(RequestBack);
        }

        // Activating the panel runs OnEnable, which builds the list, so a reopened screen is always current.
        public void Show()
        {
            gameObject.SetActive(true);
            FocusBack();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void FocusBack()
        {
            if (backButton != null && backButton.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(backButton.gameObject);
        }

        // Rebuilds from scratch so a reopened screen never shows a stale or doubled list.
        public void Build(IReadOnlyList<CreditsCatalog.Section> sections = null)
        {
            if (content == null || sectionTemplate == null || entryTemplate == null) return;
            Clear();
            sections ??= CreditsCatalog.Sections;

            foreach (CreditsCatalog.Section section in sections)
            {
                if (section == null) continue;
                if (!string.IsNullOrWhiteSpace(section.heading)) AddSectionRow(section.heading.Trim());
                if (!string.IsNullOrWhiteSpace(section.note)) AddSectionRow(section.note.Trim(), note: true);
                foreach (CreditsCatalog.Entry entry in section.Entries)
                {
                    if (entry == null) continue;
                    FrontendCreditsEntryView row = Instantiate(entryTemplate, content);
                    row.name = "Credits Entry";
                    row.gameObject.SetActive(true);
                    row.Bind(entry);
                    rows.Add(row.gameObject);
                    entries.Add(row);
                }
            }

            sectionTemplate.gameObject.SetActive(false);
            entryTemplate.gameObject.SetActive(false);
            if (emptyMessage != null) emptyMessage.gameObject.SetActive(entries.Count == 0);
            // Only while playing: in the Editor the layout has not been rebuilt yet, so the scroll bounds
            // a verifier would hit here are still empty.
            if (scrollRect != null && Application.isPlaying) scrollRect.verticalNormalizedPosition = 1f;
        }

        private void AddSectionRow(string text, bool note = false)
        {
            TMP_Text row = Instantiate(sectionTemplate, content);
            row.name = note ? "Credits Section Note" : "Credits Section";
            row.gameObject.SetActive(true);
            row.text = text;
            if (note)
            {
                row.fontSize = sectionTemplate.fontSize * 0.68f;
                row.color = new Color(0.72f, 0.8f, 0.88f);
            }
            rows.Add(row.gameObject);
        }

        private void Clear()
        {
            foreach (GameObject row in rows)
            {
                if (row == null) continue;
                if (Application.isPlaying) Destroy(row); else DestroyImmediate(row);
            }
            rows.Clear();
            entries.Clear();
        }

        private void RequestBack()
        {
            OnBackRequested?.Invoke();
        }
    }
}
