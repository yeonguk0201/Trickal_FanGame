using System;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public enum ItemRewardCardState
    {
        Available,
        Focused,
        Acquired,
        Unavailable,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ItemRewardCardView : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text kindText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text stackText;

        private bool buttonBound;
        [SerializeField] private Image artwork;
        [SerializeField] private GameObject selectionHighlight;
        [SerializeField] private GameObject selectionClover;
        [SerializeField] private GameObject selectionRibbon;
        private bool chosen;

        public void ConfigureArtwork(Image image, GameObject highlight, GameObject clover = null, GameObject ribbon = null)
        {
            artwork = image;
            selectionHighlight = highlight;
            selectionClover = clover;
            selectionRibbon = ribbon;
            RefreshHighlight();
        }

        public void SetChosen(bool value)
        {
            chosen = value;
            RefreshHighlight();
            if (stateText != null && (State == ItemRewardCardState.Available || State == ItemRewardCardState.Focused))
                stateText.text = chosen || State == ItemRewardCardState.Focused ? "[선택 중]" : "[선택 가능]";
        }

        private void RefreshHighlight()
        {
            bool selected = chosen || State == ItemRewardCardState.Focused || State == ItemRewardCardState.Acquired;
            if (selectionHighlight != null) selectionHighlight.SetActive(selected);
            if (selectionRibbon != null) selectionRibbon.SetActive(selected);
            if (selectionClover != null)
            {
                selectionClover.SetActive(selected);
                if (stateText != null) stateText.gameObject.SetActive(selected);
            }
        }

        public ItemRewardCandidate Candidate { get; private set; }
        public ItemRewardCardState State { get; private set; } = ItemRewardCardState.Unavailable;
        public Button Button => button;
        public TMP_Text StateText => stateText;
        public TMP_Text KindText => kindText;
        public TMP_Text NameText => nameText;
        public TMP_Text RarityText => rarityText;
        public TMP_Text EffectText => effectText;
        public TMP_Text StackText => stackText;
        public event Action<ItemRewardCardView> Clicked;
        public event Action<ItemRewardCardView> Focused;

        public void ConfigureVisuals(Button configuredButton, TMP_Text configuredStateText,
            TMP_Text configuredKindText, TMP_Text configuredNameText, TMP_Text configuredRarityText,
            TMP_Text configuredEffectText, TMP_Text configuredStackText)
        {
            UnbindButton();
            button = configuredButton;
            stateText = configuredStateText;
            kindText = configuredKindText;
            nameText = configuredNameText;
            rarityText = configuredRarityText;
            effectText = configuredEffectText;
            stackText = configuredStackText;
            BindButton();
        }

        private void Awake() => BindButton();
        private void OnEnable() => BindButton();
        private void OnDestroy() => UnbindButton();

        public void Bind(ItemRewardCandidate candidate, Health playerHealth)
        {
            Candidate = candidate;
            chosen = false;
            RefreshArtwork(candidate != null && candidate.IsItem ? candidate.Definition : null);
            if (candidate == null)
            {
                ApplyState(ItemRewardCardState.Unavailable);
                return;
            }

            if (candidate.IsItem)
            {
                ItemDefinition definition = candidate.Definition;
                SetText(kindText, ItemKindText.GetDisplayName(definition.Kind));
                SetText(nameText, definition.DisplayName);
                SetText(rarityText, RewardRarityLabel(definition.Rarity));
                SetText(effectText, ArtifactEffectDescription.Build(definition));
                string maximum = definition.MaxStacks > 0 ? definition.MaxStacks.ToString() : "제한 없음";
                SetText(stackText, definition.IsActivePower
                    ? BuildPowerChargeLabel(definition)
                    : $"현재 스택 {candidate.CurrentStacks} / {maximum}");
            }
            else
            {
                float currentHealth = playerHealth != null ? playerHealth.CurrentHealth : 0f;
                float maximumHealth = playerHealth != null ? playerHealth.MaxHealth : 0f;
                SetText(kindText, "회복");
                SetText(nameText, "응급 회복");
                SetText(rarityText, "대체 보상");
                SetText(effectText,
                    $"최대 HP의 {Mathf.RoundToInt(candidate.HealMaxHealthRatio * 100f)}%를 즉시 회복합니다.");
                SetText(stackText, playerHealth != null && playerHealth.UsesHealthUnits
                    ? $"현재 HP {HealthUnits.FormatHearts(currentHealth)}칸 / 최대 HP {HealthUnits.FormatHearts(maximumHealth)}칸"
                    : $"현재 HP {Number(currentHealth)} / 최대 HP {Number(maximumHealth)}");
            }

            ApplyState(ItemRewardCardState.Available);
        }

        public void ShowOutcome(bool acquired)
        {
            chosen = acquired;
            ApplyState(acquired ? ItemRewardCardState.Acquired : ItemRewardCardState.Unavailable);
        }

        private void RefreshArtwork(ItemDefinition definition)
        {
            if (artwork == null)
            {
                GameObject child = new("Item Artwork", typeof(RectTransform), typeof(Image));
                RectTransform rect = (RectTransform)child.transform;
                rect.SetParent(transform, false);
                rect.sizeDelta = new Vector2(76f, 76f);
                rect.anchoredPosition = new Vector2(-142f, 124f);
                artwork = child.GetComponent<Image>();
                artwork.raycastTarget = false;
            }
            bool hasArtwork = UserArtwork.Apply(artwork, definition);
            artwork.gameObject.SetActive(hasArtwork);
            if (nameText != null && selectionHighlight == null)
            {
                nameText.rectTransform.anchoredPosition = new Vector2(hasArtwork ? 42f : 0f, 126f);
                nameText.rectTransform.sizeDelta = new Vector2(hasArtwork ? 264f : 360f, 70f);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (State == ItemRewardCardState.Available) ApplyState(ItemRewardCardState.Focused);
            if (State == ItemRewardCardState.Focused) Focused?.Invoke(this);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (State == ItemRewardCardState.Focused) ApplyState(ItemRewardCardState.Available);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && button.interactable && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private void ApplyState(ItemRewardCardState nextState)
        {
            State = nextState;
            RefreshHighlight();
            if (button != null)
                button.interactable = nextState == ItemRewardCardState.Available ||
                                      nextState == ItemRewardCardState.Focused;
            if (stateText == null) return;
            stateText.text = nextState switch
            {
                ItemRewardCardState.Available => chosen ? "[선택 중]" : "[선택 가능]",
                ItemRewardCardState.Focused => "[선택 중]",
                ItemRewardCardState.Acquired => "[획득 완료]",
                _ => "[선택 종료]",
            };
        }

        private void BindButton()
        {
            if (buttonBound || button == null) return;
            button.onClick.AddListener(NotifyClicked);
            buttonBound = true;
        }

        private void UnbindButton()
        {
            if (!buttonBound || button == null) return;
            button.onClick.RemoveListener(NotifyClicked);
            buttonBound = false;
        }

        private void NotifyClicked() => Clicked?.Invoke(this);
        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }

        public static string BuildPowerChargeLabel(ItemDefinition definition) =>
            $"방 {definition.ActiveChargeRooms}개 클리어로 충전";

        public static string RewardRarityLabel(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => "고급",
            ItemRarity.Rare => "희귀",
            ItemRarity.Epic => "전설",
            _ => "일반",
        };

        // Other consumers retain their existing bilingual labels.
        public static string RarityLabel(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => "고급 / UNCOMMON",
            ItemRarity.Rare => "희귀 / RARE",
            ItemRarity.Epic => "전설 / EPIC",
            _ => "일반 / COMMON",
        };

        private static string Number(float value) => Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.#");
    }
}
