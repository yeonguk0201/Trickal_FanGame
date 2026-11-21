using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Room
{
    public enum RoomTransitionWipeStage
    {
        Idle,
        Covering,
        Holding,
        Revealing
    }

    // Floor-Wipe: the screen wipe for leaving a floor. Dark diagonal bands sweep in from the left one after another
    // until the screen is black, the move happens behind them, and they sweep on out to the right to open the new
    // floor. Game time stops while the screen closes and holds, so nothing moves or is hit in the dark; the wipe runs
    // on unscaled time. It builds its own overlay canvas the first time it is used, so no Scene setup is needed.
    [DisallowMultipleComponent]
    public sealed class RoomTransitionWipe : MonoBehaviour
    {
        public const int BandCount = 9;
        public const float BandSeconds = 0.24f;
        public const float BandStagger = 0.03f;
        public const float CoverSeconds = BandSeconds + (BandCount - 1) * BandStagger;
        public const float HoldSeconds = 0.32f;
        public const float RevealBandSeconds = 0.3f;
        public const float RevealStagger = 0.035f;
        public const float RevealSeconds = RevealBandSeconds + (BandCount - 1) * RevealStagger;
        // Seconds from the start until the screen begins to open again.
        public const float DarkUntil = CoverSeconds + HoldSeconds;
        public const float TotalSeconds = DarkUntil + RevealSeconds;
        public const float BandAngle = -18f;
        public const int SortingOrder = 32000;
        // A frame hitch must not skip the moment the screen is fully covered.
        private const float MaxStep = 0.05f;
        private static readonly Color BandColor = new(0.035f, 0.03f, 0.07f, 1f);

        private static RoomTransitionWipe instance;

        private RectTransform bandRoot;
        private RectTransform[] bands;
        private Action onCovered;
        private bool coveredInvoked;
        private float previousTimeScale = 1f;
        private float elapsed;

        public static bool IsBusy => instance != null && instance.Stage != RoomTransitionWipeStage.Idle;
        public static RoomTransitionWipe Current => instance;
        public RoomTransitionWipeStage Stage { get; private set; } = RoomTransitionWipeStage.Idle;
        public float Elapsed => elapsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        // Starts the wipe and calls `covered` once the screen is fully dark. Returns false while another wipe runs.
        public static bool Play(Action covered)
        {
            if (IsBusy) return false;
            if (instance == null)
            {
                GameObject root = new("Room Transition Wipe");
                instance = root.AddComponent<RoomTransitionWipe>();
            }

            instance.Begin(covered);
            return true;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            BuildOverlay();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            if (Stage is RoomTransitionWipeStage.Covering or RoomTransitionWipeStage.Holding && Time.timeScale == 0f)
                Time.timeScale = previousTimeScale;
            instance = null;
        }

        private void Begin(Action covered)
        {
            onCovered = covered;
            coveredInvoked = false;
            elapsed = 0f;
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            Stage = RoomTransitionWipeStage.Covering;
            SetVisible(true);
            Apply();
        }

        private void Update()
        {
            if (Stage == RoomTransitionWipeStage.Idle) return;
            elapsed += Mathf.Clamp(Time.unscaledDeltaTime, 0f, MaxStep);
            Apply();
        }

        private void Apply()
        {
            LayoutBands();
            if (elapsed >= TotalSeconds)
            {
                Stage = RoomTransitionWipeStage.Idle;
                SetVisible(false);
                return;
            }

            float travel = bandRoot.rect.width;
            if (elapsed < CoverSeconds)
            {
                Stage = RoomTransitionWipeStage.Covering;
                for (int i = 0; i < bands.Length; i++)
                {
                    float t = Mathf.Clamp01((elapsed - i * BandStagger) / BandSeconds);
                    SetBand(i, -travel * (1f - EaseOutCubic(t)));
                }

                return;
            }

            if (elapsed < DarkUntil)
            {
                Stage = RoomTransitionWipeStage.Holding;
                for (int i = 0; i < bands.Length; i++) SetBand(i, 0f);
                if (!coveredInvoked)
                {
                    coveredInvoked = true;
                    Action covered = onCovered;
                    onCovered = null;
                    try
                    {
                        covered?.Invoke();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                    }
                }

                return;
            }

            if (Stage != RoomTransitionWipeStage.Revealing)
            {
                Stage = RoomTransitionWipeStage.Revealing;
                // The new floor is in place: let the player move again while the bands clear.
                if (Time.timeScale == 0f) Time.timeScale = previousTimeScale;
            }

            float sinceReveal = elapsed - DarkUntil;
            for (int i = 0; i < bands.Length; i++)
            {
                float t = Mathf.Clamp01((sinceReveal - i * RevealStagger) / RevealBandSeconds);
                SetBand(i, travel * EaseInCubic(t));
            }
        }

        private void SetBand(int index, float x)
        {
            bands[index].anchoredPosition = new Vector2(x, bands[index].anchoredPosition.y);
        }

        private void BuildOverlay()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            gameObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = new("Bands", typeof(RectTransform));
            bandRoot = rootObject.GetComponent<RectTransform>();
            bandRoot.SetParent(transform, false);
            bandRoot.anchorMin = bandRoot.anchorMax = new Vector2(0.5f, 0.5f);
            bandRoot.pivot = new Vector2(0.5f, 0.5f);
            bandRoot.localRotation = Quaternion.Euler(0f, 0f, BandAngle);

            bands = new RectTransform[BandCount];
            for (int i = 0; i < BandCount; i++)
            {
                GameObject band = new($"Band {i + 1}", typeof(RectTransform), typeof(Image));
                RectTransform rect = band.GetComponent<RectTransform>();
                rect.SetParent(bandRoot, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                Image image = band.GetComponent<Image>();
                // Every other band a shade lighter, so the sweep reads as separate strips going by.
                image.color = i % 2 == 0 ? BandColor : Color.Lerp(BandColor, new Color(0.12f, 0.1f, 0.2f, 1f), 0.35f);
                image.raycastTarget = true;
                bands[i] = rect;
            }

            LayoutBands();
        }

        // The rotated band field must cover the screen at any aspect, so it is sized from the screen diagonal.
        private void LayoutBands()
        {
            float diagonal = Mathf.Sqrt(Screen.width * (float)Screen.width + Screen.height * (float)Screen.height);
            float size = diagonal * 1.08f;
            if (Mathf.Approximately(bandRoot.rect.width, size)) return;
            bandRoot.sizeDelta = new Vector2(size, size);
            float height = size / BandCount;
            for (int i = 0; i < BandCount; i++)
            {
                // A pixel of overlap so no seam shows between neighbouring bands.
                bands[i].sizeDelta = new Vector2(size, height + 2f);
                bands[i].anchoredPosition = new Vector2(bands[i].anchoredPosition.x, -i * height);
            }
        }

        private void SetVisible(bool visible)
        {
            bandRoot.gameObject.SetActive(visible);
            if (visible) return;
            foreach (RectTransform band in bands)
                band.anchoredPosition = new Vector2(-bandRoot.rect.width, band.anchoredPosition.y);
        }

        private static float EaseOutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        private static float EaseInCubic(float t) => t * t * t;
    }
}
