using TMPro;
using TrickalFanGame.Item;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // UX-1: when the player comes near an artifact, spell or 권능 on the floor, a panel above it shows the kind,
    // name, rarity and effect. Only the nearest one is shown; it hides when the player walks away, the item is
    // collected or the game is paused. Collecting still happens on touch.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class PickupDescriptionView : MonoBehaviour
    {
        public const float DefaultDetectionRadius = 2.25f;
        // Above the item's centre in world units, so the panel does not cover the item.
        public const float WorldOffsetY = 0.6f;
        public const float EdgeMargin = 16f;
        public const float VerticalPadding = 14f;
        public const string AllFixedText = "아티팩트스펠짱셈스펠교주의권능일반고급희귀전설/COMMONUNCOMMONRAREEPIC·";

        [SerializeField] private Transform player;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform frame;
        [SerializeField] private TMP_Text kindText;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField, Min(0.1f)] private float detectionRadius = DefaultDetectionRadius;

        private CanvasGroup canvasGroup;
        private RectTransform rect;

        public Transform Player => player;
        public RectTransform Frame => frame;
        public TMP_Text KindText => kindText;
        public TMP_Text RarityText => rarityText;
        public TMP_Text NameText => nameText;
        public TMP_Text EffectText => effectText;
        public TMP_Text DetailText => detailText;
        public float DetectionRadius => detectionRadius;
        public IDescribedPickup Shown { get; private set; }
        public bool IsVisible => Shown != null;

        public void Configure(Transform configuredPlayer, RectTransform configuredFrame, TMP_Text configuredKind,
            TMP_Text configuredRarity, TMP_Text configuredName, TMP_Text configuredEffect, TMP_Text configuredDetail,
            float configuredRadius = DefaultDetectionRadius)
        {
            player = configuredPlayer;
            frame = configuredFrame;
            kindText = configuredKind;
            rarityText = configuredRarity;
            nameText = configuredName;
            effectText = configuredEffect;
            detailText = configuredDetail;
            detectionRadius = Mathf.Max(0.1f, configuredRadius);
            Hide();
        }

        public static Color RarityTextColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => new Color(0.47f, 0.9f, 0.56f, 1f),
            ItemRarity.Rare => new Color(0.47f, 0.7f, 1f, 1f),
            ItemRarity.Epic => new Color(0.86f, 0.56f, 1f, 1f),
            _ => new Color(0.88f, 0.9f, 0.94f, 1f),
        };

        // The one line under the effect: how a 권능 charges, nothing for the others.
        public static string DetailLine(ItemDefinition definition) =>
            definition != null && definition.IsActivePower ? GamePowerHudView.BuildOfferLine(definition) : string.Empty;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            Hide();
        }

        private void LateUpdate() => RefreshNow();

        // Editor checks call this directly; at runtime it runs every frame after movement.
        public void RefreshNow()
        {
            IDescribedPickup nearest = Time.timeScale <= 0f || player == null || !player.gameObject.activeInHierarchy
                ? null
                : DescribedPickupRegistry.FindNearest(player.position, detectionRadius);
            if (nearest == null)
            {
                Hide();
                return;
            }

            if (!ReferenceEquals(nearest, Shown)) Bind(nearest.Definition);
            Shown = nearest;
            SetVisible(true);
            Place(nearest.Position);
        }

        private void Bind(ItemDefinition definition)
        {
            Color rarityColor = RarityTextColor(definition.Rarity);
            SetText(kindText, ItemKindText.GetDisplayName(definition.Kind));
            SetText(rarityText, ItemRewardCardView.RarityLabel(definition.Rarity));
            if (rarityText != null) rarityText.color = rarityColor;
            SetText(nameText, definition.DisplayName);
            if (nameText != null) nameText.color = rarityColor;
            SetText(effectText, ArtifactEffectDescription.Build(definition));
            string detail = DetailLine(definition);
            SetText(detailText, detail);
            if (detailText != null) detailText.gameObject.SetActive(!string.IsNullOrEmpty(detail));
            FitHeight();
        }

        // The effect text wraps, so the panel grows to fit it and the detail line follows below.
        private void FitHeight()
        {
            if (rect == null) rect = (RectTransform)transform;
            if (effectText == null) return;
            effectText.ForceMeshUpdate();
            float effectHeight = Mathf.Max(24f, effectText.preferredHeight);
            RectTransform effectRect = effectText.rectTransform;
            effectRect.sizeDelta = new Vector2(effectRect.sizeDelta.x, effectHeight);
            float top = -effectRect.anchoredPosition.y;
            float height = top + effectHeight + VerticalPadding;
            if (detailText != null && detailText.gameObject.activeSelf)
            {
                RectTransform detailRect = detailText.rectTransform;
                detailRect.anchoredPosition = new Vector2(detailRect.anchoredPosition.x, -(top + effectHeight + 6f));
                height += detailRect.sizeDelta.y + 6f;
            }

            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private void Place(Vector3 worldPosition)
        {
            if (frame == null) return;
            Camera view = worldCamera != null ? worldCamera : Camera.main;
            if (view == null) return;
            Vector3 screen = view.WorldToScreenPoint(worldPosition + Vector3.up * WorldOffsetY);
            Canvas canvas = frame.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screen, uiCamera, out Vector2 local))
                return;

            // The panel's pivot is its top centre, so it is lifted by its height to sit above the item.
            if (rect == null) rect = (RectTransform)transform;
            Vector2 size = rect.sizeDelta;
            Rect bounds = frame.rect;
            float x = Mathf.Clamp(local.x, bounds.xMin + EdgeMargin + size.x * 0.5f,
                bounds.xMax - EdgeMargin - size.x * 0.5f);
            float y = Mathf.Clamp(local.y + size.y, bounds.yMin + EdgeMargin + size.y, bounds.yMax - EdgeMargin);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private void Hide()
        {
            Shown = null;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = visible ? 1f : 0f;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }
    }
}
