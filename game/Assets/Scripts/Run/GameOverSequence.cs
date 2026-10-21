using System;
using TrickalFanGame.Enemy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Run
{
    // UX-0: player death → fall → hold → circular fade (iris) → GAME OVER → the player confirms → result.
    // Runs on unscaled time so a stopped game clock cannot stall it. The result screen opens only after the
    // confirm input; nothing advances to it by time alone.
    [DisallowMultipleComponent]
    public sealed class GameOverSequence : MonoBehaviour
    {
        public enum Phase
        {
            Idle,
            Falling,
            Holding,
            Closing,
            Title,
            AwaitingConfirm,
            Confirmed
        }

        [Header("UI")]
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private RectTransform irisHole;
        [SerializeField] private RectTransform irisLeft;
        [SerializeField] private RectTransform irisRight;
        [SerializeField] private RectTransform irisTop;
        [SerializeField] private RectTransform irisBottom;
        [SerializeField] private CanvasGroup title;
        [SerializeField] private Button confirmButton;
        [SerializeField] private GraphicRaycaster graphicRaycaster;

        [Header("Timing (seconds, unscaled)")]
        // Placeholder fall until the character fall artwork exists; then this becomes that clip's length.
        [SerializeField, Min(0f)] private float fallDuration = 0.6f;
        [SerializeField, Min(0f)] private float holdAfterFall = 5f;
        [SerializeField, Min(0.01f)] private float irisDuration = 1.2f;
        [SerializeField, Min(0.01f)] private float titleFadeDuration = 0.6f;
        [SerializeField, Min(0f)] private float confirmDelay = 0.4f;

        private Transform focusTarget;
        private Camera focusCamera;
        private Quaternion fallStartRotation;
        private float fallAngle;
        private float phaseElapsed;
        private Action confirmed;
        private Texture2D holeTexture;

        public Phase CurrentPhase { get; private set; } = Phase.Idle;
        public bool IsActive => CurrentPhase != Phase.Idle;
        public bool IsAwaitingConfirm => CurrentPhase == Phase.AwaitingConfirm;
        public float FallDuration => fallDuration;
        public float HoldAfterFall => holdAfterFall;
        public float IrisDuration => irisDuration;
        public float IrisRadius { get; private set; }
        public CanvasGroup Overlay => overlay;
        public RectTransform IrisHole => irisHole;
        public CanvasGroup Title => title;
        public Button ConfirmButton => confirmButton;
        public GraphicRaycaster GraphicRaycaster => graphicRaycaster;

        public void Configure(CanvasGroup configuredOverlay, RectTransform configuredHole, RectTransform left,
            RectTransform right, RectTransform top, RectTransform bottom, CanvasGroup configuredTitle,
            Button configuredConfirmButton, GraphicRaycaster configuredRaycaster)
        {
            UnbindConfirm();
            overlay = configuredOverlay;
            irisHole = configuredHole;
            irisLeft = left;
            irisRight = right;
            irisTop = top;
            irisBottom = bottom;
            title = configuredTitle;
            confirmButton = configuredConfirmButton;
            graphicRaycaster = configuredRaycaster;
            BindConfirm();
            ApplyHidden();
        }

        private void Awake()
        {
            BindConfirm();
            ApplyHidden();
            Image holeImage = irisHole != null ? irisHole.GetComponent<Image>() : null;
            if (holeImage != null && Application.isPlaying)
            {
                holeTexture = CreateHoleTexture(256);
                holeImage.sprite = Sprite.Create(holeTexture, new Rect(0, 0, holeTexture.width, holeTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }
        }

        private void OnDestroy()
        {
            UnbindConfirm();
            if (holeTexture != null) Destroy(holeTexture);
        }

        public bool TryBegin(Transform target, Action onConfirmed)
        {
            if (IsActive || overlay == null || confirmButton == null) return false;

            focusTarget = target;
            focusCamera = Camera.main;
            confirmed = onConfirmed;
            StopBosses();
            if (focusTarget != null)
            {
                fallStartRotation = focusTarget.rotation;
                SpriteRenderer sprite = focusTarget.GetComponent<SpriteRenderer>();
                // Falls backwards from the facing direction (flipX = facing right in the player artwork).
                fallAngle = sprite != null && sprite.flipX ? 90f : -90f;
            }
            SetPhase(Phase.Falling);
            return true;
        }

        // Verification and quick skip: jumps straight to the confirm prompt.
        public bool SkipToConfirm()
        {
            if (!IsActive || CurrentPhase >= Phase.AwaitingConfirm) return false;
            ApplyFall(1f);
            ShowOverlay();
            ApplyIris(0f);
            title.alpha = 1f;
            EnterAwaitingConfirm();
            return true;
        }

        public bool Confirm()
        {
            if (CurrentPhase != Phase.AwaitingConfirm) return false;
            SetPhase(Phase.Confirmed);
            confirmButton.interactable = false;
            Action callback = confirmed;
            confirmed = null;
            callback?.Invoke();
            return true;
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        // Steps the sequence by unscaled seconds. Update drives it; verification steps it directly.
        public void Advance(float seconds)
        {
            if (!IsActive) return;
            phaseElapsed += Mathf.Max(0f, seconds);
            switch (CurrentPhase)
            {
                case Phase.Falling:
                    ApplyFall(fallDuration <= 0f ? 1f : Mathf.Clamp01(phaseElapsed / fallDuration));
                    if (phaseElapsed >= fallDuration) SetPhase(Phase.Holding);
                    break;
                case Phase.Holding:
                    if (phaseElapsed >= holdAfterFall)
                    {
                        ShowOverlay();
                        SetPhase(Phase.Closing);
                        ApplyIris(1f);
                    }
                    break;
                case Phase.Closing:
                    float t = Mathf.Clamp01(phaseElapsed / irisDuration);
                    ApplyIris(1f - t * t * t);
                    if (t >= 1f) SetPhase(Phase.Title);
                    break;
                case Phase.Title:
                    title.alpha = Mathf.Clamp01(phaseElapsed / titleFadeDuration);
                    if (phaseElapsed >= titleFadeDuration + confirmDelay) EnterAwaitingConfirm();
                    break;
                case Phase.AwaitingConfirm:
                    if (EventSystem.current != null &&
                        EventSystem.current.currentSelectedGameObject != confirmButton.gameObject)
                        EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
                    break;
            }
        }

        private void SetPhase(Phase phase)
        {
            CurrentPhase = phase;
            phaseElapsed = 0f;
        }

        private void EnterAwaitingConfirm()
        {
            SetPhase(Phase.AwaitingConfirm);
            confirmButton.gameObject.SetActive(true);
            confirmButton.interactable = true;
            overlay.interactable = true;
            overlay.blocksRaycasts = true;
            if (graphicRaycaster != null) graphicRaycaster.enabled = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
        }

        // Placeholder fall: the character tips over. Replace with the fall artwork when it exists.
        private void ApplyFall(float t)
        {
            if (focusTarget == null) return;
            float eased = 1f - (1f - t) * (1f - t);
            focusTarget.rotation = fallStartRotation * Quaternion.Euler(0f, 0f, fallAngle * eased);
            // Keep the body in step so physics interpolation does not write the old pose back.
            Rigidbody2D body = focusTarget.GetComponent<Rigidbody2D>();
            if (body != null) body.rotation = focusTarget.eulerAngles.z;
        }

        private void ShowOverlay()
        {
            overlay.alpha = 1f;
            overlay.blocksRaycasts = true;
            title.alpha = 0f;
        }

        // openness 1 = the hole reaches past every screen corner, 0 = fully closed.
        private void ApplyIris(float openness)
        {
            RectTransform root = overlay.transform as RectTransform;
            if (root == null || irisHole == null) return;
            Rect area = root.rect;
            Vector2 center = Vector2.zero;
            if (focusTarget != null && focusCamera != null)
            {
                Vector2 screen = focusCamera.WorldToScreenPoint(focusTarget.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out center);
            }

            float maxRadius = 0f;
            foreach (Vector2 corner in new[]
                     {
                         new Vector2(area.xMin, area.yMin), new Vector2(area.xMin, area.yMax),
                         new Vector2(area.xMax, area.yMin), new Vector2(area.xMax, area.yMax)
                     })
                maxRadius = Mathf.Max(maxRadius, Vector2.Distance(center, corner));
            float radius = (maxRadius + 8f) * Mathf.Clamp01(openness);
            IrisRadius = radius;

            const float pad = 4f;
            const float seam = 1f;
            SetRect(irisHole, center.x - radius, center.y - radius, center.x + radius, center.y + radius);
            SetRect(irisLeft, area.xMin - pad, area.yMin - pad, center.x - radius + seam, area.yMax + pad);
            SetRect(irisRight, center.x + radius - seam, area.yMin - pad, area.xMax + pad, area.yMax + pad);
            SetRect(irisBottom, center.x - radius, area.yMin - pad, center.x + radius, center.y - radius + seam);
            SetRect(irisTop, center.x - radius, center.y + radius - seam, center.x + radius, area.yMax + pad);
        }

        private static void SetRect(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
        }

        private void ApplyHidden()
        {
            if (overlay != null)
            {
                overlay.alpha = 0f;
                overlay.interactable = false;
                overlay.blocksRaycasts = false;
            }
            if (title != null) title.alpha = 0f;
            if (confirmButton != null) confirmButton.gameObject.SetActive(false);
            if (graphicRaycaster != null) graphicRaycaster.enabled = false;
        }

        // Bosses keep running patterns at a dead player; stop them so the fall reads clearly.
        // Regular enemies already disengage when their target dies.
        private static void StopBosses()
        {
            foreach (BossController boss in FindObjectsByType<BossController>(FindObjectsSortMode.None))
            {
                if (boss == null) continue;
                boss.CancelCombat();
                boss.enabled = false;
            }
        }

        private void BindConfirm()
        {
            if (confirmButton == null) return;
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        private void UnbindConfirm()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }

        private void OnConfirmClicked() => Confirm();

        // White with a transparent anti-aliased circle; the Image colour tints the opaque part.
        private static Texture2D CreateHoleTexture(int size)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Game Over Iris Hole",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half));
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(distance - (half - 1.5f)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
