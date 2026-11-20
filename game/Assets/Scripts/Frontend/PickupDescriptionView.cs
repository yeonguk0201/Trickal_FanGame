using System.Collections.Generic;
using TMPro;
using TrickalFanGame.Item;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Frontend
{
    // UX-1: when the player comes near an artifact, spell or 권능 on the floor, a panel above it shows the kind,
    // name, rarity and effect. Only the nearest one is shown; it hides when the player walks away, the item is
    // collected or the game is paused. Collecting still happens on touch.
    // The same panel describes a held 권능, spell or artifact while the mouse rests on its HUD icon (2026-11-20 user
    // request, not in the ESC menu); the hover wins over a nearby pickup and the panel sits beside the icon.
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
        public IHudDescribed HoveredHud { get; private set; }
        public bool IsVisible => Shown != null || HoveredHud != null;
        public const float HudGap = 10f;

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
            Vector2? pointer = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2?)null;
            if (RefreshHudHover(pointer)) return;

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

        // pointer is a screen position (null without a mouse). True while a HUD icon under it is described.
        public bool RefreshHudHover(Vector2? pointer)
        {
            // Nothing describes on hover while paused (the ESC menu lists the items itself).
            IHudDescribed hovered = Time.timeScale <= 0f || !pointer.HasValue
                ? null
                : HudDescribedRegistry.FindUnder(pointer.Value);
            if (hovered == null)
            {
                HoveredHud = null;
                return false;
            }

            if (!ReferenceEquals(hovered, HoveredHud) || Shown != null) Bind(hovered.Definition);
            // The pickup must bind again once the hover ends, since the panel now holds the HUD item's text.
            Shown = null;
            HoveredHud = hovered;
            SetVisible(true);
            PlaceBeside(hovered.HoverArea);
            return true;
        }

        private void Bind(ItemDefinition definition)
        {
            Color rarityColor = RarityTextColor(definition.Rarity);
            SetText(kindText, ItemKindText.GetDisplayName(definition.Kind));
            SetText(rarityText, ItemRewardCardView.RewardRarityLabel(definition.Rarity));
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

        // Below an icon in the upper half of the screen, above one in the lower half, centred on it and kept on screen.
        private void PlaceBeside(RectTransform target)
        {
            if (frame == null || target == null) return;
            if (rect == null) rect = (RectTransform)transform;
            Rect screen = HudDescribedRegistry.ScreenRect(target);
            Canvas canvas = frame.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screen.min, uiCamera, out Vector2 min) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screen.max, uiCamera, out Vector2 max))
                return;

            Vector2 size = rect.sizeDelta;
            Rect bounds = frame.rect;
            float centreY = (min.y + max.y) * 0.5f;
            // The pivot is the panel's top centre.
            float y = centreY > bounds.center.y ? min.y - HudGap : max.y + HudGap + size.y;
            float x = Mathf.Clamp((min.x + max.x) * 0.5f, bounds.xMin + EdgeMargin + size.x * 0.5f,
                bounds.xMax - EdgeMargin - size.x * 0.5f);
            y = Mathf.Clamp(y, bounds.yMin + EdgeMargin + size.y, bounds.yMax - EdgeMargin);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private void Hide()
        {
            HoveredHud = null;
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

namespace TrickalFanGame.Frontend
{
    // A HUD icon that describes the item it shows when the mouse rests on it (권능, spell slot, artifact row).
    public interface IHudDescribed
    {
        RectTransform HoverArea { get; }
        // Null while the icon is empty: nothing is described.
        ItemDefinition Definition { get; }
    }

    // HUD icons register while enabled. The HUD canvas does not raycast during play (the mouse aims attacks), so
    // the hover is a plain rectangle test against the pointer.
    public static class HudDescribedRegistry
    {
        private static readonly List<IHudDescribed> Entries = new();

        public static int Count => Entries.Count;

        public static void Register(IHudDescribed entry)
        {
            if (entry != null && !Entries.Contains(entry)) Entries.Add(entry);
        }

        public static void Unregister(IHudDescribed entry) => Entries.Remove(entry);

        public static IHudDescribed FindUnder(Vector2 screenPoint)
        {
            // Later registrations draw on top (rebuilt artifact rows), so search from the end.
            for (int index = Entries.Count - 1; index >= 0; index--)
            {
                IHudDescribed entry = Entries[index];
                if (entry == null || entry is Object unityObject && unityObject == null)
                {
                    Entries.RemoveAt(index);
                    continue;
                }
                RectTransform area = entry.HoverArea;
                if (entry.Definition == null || area == null || !IsShown(area)) continue;
                if (ScreenRect(area).Contains(screenPoint)) return entry;
            }
            return null;
        }

        // Active and not under a faded-out CanvasGroup.
        private static bool IsShown(RectTransform area)
        {
            if (!area.gameObject.activeInHierarchy) return false;
            for (Transform node = area; node != null; node = node.parent)
            {
                if (node.TryGetComponent(out CanvasGroup group))
                {
                    if (group.alpha <= 0.01f) return false;
                    if (group.ignoreParentGroups) break;
                }
                if (node.TryGetComponent(out Canvas canvas) && canvas.isRootCanvas) break;
            }
            return true;
        }

        public static Rect ScreenRect(RectTransform area)
        {
            Canvas canvas = area.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector3[] corners = new Vector3[4];
            area.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x),
                Mathf.Max(min.y, max.y));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Entries.Clear();
    }
}
