using TMPro;
using TrickalFanGame.Character;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public enum BossIntroStage
    {
        Idle,
        Panels,
        Fill
    }

    // Boss-Intro-0: entering a boss room stops game time, slides the boss and player panels in from the sides, slams
    // "VS" in the middle, then fills the boss HP bar. Combat resumes when the bar is full. Game time stays at 0 the
    // whole time, so nothing moves or deals damage; the intro itself runs on unscaled time.
    // Runs before GameBossHudView so the bar never shows full for a frame before the intro hides it.
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class GameBossIntroView : MonoBehaviour
    {
        public const float SlideInSeconds = 0.35f;
        public const float VsSlamSeconds = 0.22f;
        public const float VsImpactAt = SlideInSeconds + VsSlamSeconds;
        public const float SlideOutAt = 1.6f;
        public const float SlideOutSeconds = 0.3f;
        public const float FillStartsAt = SlideOutAt + SlideOutSeconds;
        public const float FillSeconds = 1f;
        public const float TotalSeconds = FillStartsAt + FillSeconds;
        // A key pressed while walking through the door must not skip the intro by accident.
        public const float SkipGuardSeconds = 0.25f;
        public const float FlashSeconds = 0.25f;
        public const float ShakeSeconds = 0.2f;
        public const float ShakeAmplitude = 16f;
        public const float SlideDistance = 1700f;
        public const float VsStartScale = 2.8f;
        // A frame hitch must not jump past the slam.
        private const float MaxStep = 0.05f;

        [SerializeField] private RoomGraphController roomGraph;
        [SerializeField] private GameBossHudView bossHud;
        [SerializeField] private RunSession runSession;
        [SerializeField] private CharacterDefinition[] characters = System.Array.Empty<CharacterDefinition>();
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform shakeRoot;
        [SerializeField] private RectTransform bossPanel;
        [SerializeField] private Image bossPortrait;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private RectTransform playerPanel;
        [SerializeField] private Image playerPortrait;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text vsText;
        [SerializeField] private Image flash;

        private BossController introducedBoss;
        private Vector2 bossRestPosition;
        private Vector2 playerRestPosition;
        private bool restPositionsCaptured;
        private float previousTimeScale = 1f;
        private float elapsed;

        public RoomGraphController RoomGraph => roomGraph;
        public GameBossHudView BossHud => bossHud;
        public RunSession RunSession => runSession;
        public CharacterDefinition[] Characters => characters;
        public CanvasGroup CanvasGroup => canvasGroup;
        public RectTransform ShakeRoot => shakeRoot;
        public RectTransform BossPanel => bossPanel;
        public Image BossPortrait => bossPortrait;
        public TMP_Text BossNameText => bossNameText;
        public RectTransform PlayerPanel => playerPanel;
        public Image PlayerPortrait => playerPortrait;
        public TMP_Text PlayerNameText => playerNameText;
        public TMP_Text VsText => vsText;
        public Image Flash => flash;
        public BossIntroStage Stage { get; private set; } = BossIntroStage.Idle;
        public bool IsPlaying => Stage != BossIntroStage.Idle;
        public float Elapsed => elapsed;
        public BossController IntroducedBoss => introducedBoss;
        public Vector2 BossRestPosition => bossRestPosition;
        public Vector2 PlayerRestPosition => playerRestPosition;

        public void Configure(RoomGraphController configuredRoomGraph, GameBossHudView configuredBossHud,
            RunSession configuredRunSession, CharacterDefinition[] configuredCharacters,
            CanvasGroup configuredCanvasGroup, RectTransform configuredShakeRoot,
            RectTransform configuredBossPanel, Image configuredBossPortrait, TMP_Text configuredBossNameText,
            RectTransform configuredPlayerPanel, Image configuredPlayerPortrait, TMP_Text configuredPlayerNameText,
            TMP_Text configuredVsText, Image configuredFlash)
        {
            roomGraph = configuredRoomGraph;
            bossHud = configuredBossHud;
            runSession = configuredRunSession;
            characters = configuredCharacters ?? System.Array.Empty<CharacterDefinition>();
            canvasGroup = configuredCanvasGroup;
            shakeRoot = configuredShakeRoot;
            bossPanel = configuredBossPanel;
            bossPortrait = configuredBossPortrait;
            bossNameText = configuredBossNameText;
            playerPanel = configuredPlayerPanel;
            playerPortrait = configuredPlayerPortrait;
            playerNameText = configuredPlayerNameText;
            vsText = configuredVsText;
            flash = configuredFlash;
            restPositionsCaptured = false;
        }

        private void Awake()
        {
            CaptureRestPositions();
            SetOverlayVisible(false);
        }

        private void OnDisable()
        {
            if (IsPlaying) Finish();
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                TryBegin();
                return;
            }

            if (SkipPressed()) Skip();
            Advance(Time.unscaledDeltaTime);
        }

        private void TryBegin()
        {
            BossController boss = GameBossHudView.FindActiveBoss(roomGraph != null ? roomGraph.CurrentNode : null);
            if (boss == null || boss == introducedBoss) return;
            // Pause, hit stop or the result screen own the time scale right now: keep the bar hidden and start after.
            if (Time.timeScale != 1f || (runSession != null && runSession.HasEnded))
            {
                if (bossHud != null) bossHud.SetIntroPresentation(true, 0f);
                return;
            }

            Begin(boss);
        }

        public void Begin(BossController boss)
        {
            if (boss == null || IsPlaying) return;
            CaptureRestPositions();
            introducedBoss = boss;
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            if (bossNameText != null) bossNameText.text = boss.DisplayName;
            SetPortrait(bossPortrait, ResolveBossSprite(boss));
            CharacterDefinition character = ResolveCharacter();
            if (playerNameText != null)
                playerNameText.text = character != null ? character.DisplayName : string.Empty;
            SetPortrait(playerPortrait, character != null && character.Portrait != null
                ? character.Portrait
                : FindFirstObjectByType<PlayerMovement>()?.GetComponentInChildren<SpriteRenderer>()?.sprite);

            elapsed = 0f;
            Stage = BossIntroStage.Panels;
            Apply();
        }

        // During the panels: jump to the bar fill. During the fill: finish now.
        public void Skip()
        {
            if (!IsPlaying || elapsed < SkipGuardSeconds) return;
            elapsed = Stage == BossIntroStage.Panels ? FillStartsAt : TotalSeconds;
            Apply();
        }

        public void Advance(float unscaledDeltaTime)
        {
            if (!IsPlaying) return;
            elapsed += Mathf.Clamp(unscaledDeltaTime, 0f, MaxStep);
            Apply();
        }

        private void Apply()
        {
            if (elapsed >= TotalSeconds)
            {
                Finish();
                return;
            }

            if (elapsed >= FillStartsAt)
            {
                Stage = BossIntroStage.Fill;
                SetOverlayVisible(false);
                float fill = (elapsed - FillStartsAt) / FillSeconds;
                if (bossHud != null) bossHud.SetIntroPresentation(false, EaseOutCubic(fill));
                return;
            }

            Stage = BossIntroStage.Panels;
            SetOverlayVisible(true);
            if (bossHud != null) bossHud.SetIntroPresentation(true, 0f);

            float slideIn = EaseOutBack(Mathf.Clamp01(elapsed / SlideInSeconds));
            float slideOut = EaseInCubic(Mathf.Clamp01((elapsed - SlideOutAt) / SlideOutSeconds));
            float offset = SlideDistance * (1f - slideIn) + SlideDistance * slideOut;
            if (bossPanel != null) bossPanel.anchoredPosition = bossRestPosition + Vector2.left * offset;
            if (playerPanel != null) playerPanel.anchoredPosition = playerRestPosition + Vector2.right * offset;

            if (vsText != null)
            {
                float slam = Mathf.Clamp01((elapsed - SlideInSeconds) / VsSlamSeconds);
                bool shown = elapsed >= SlideInSeconds;
                vsText.gameObject.SetActive(shown);
                vsText.transform.localScale = Vector3.one * Mathf.Lerp(VsStartScale, 1f, slam * slam);
                vsText.alpha = Mathf.Clamp01(slam * 3f) * (1f - slideOut);
            }

            float sinceImpact = elapsed - VsImpactAt;
            if (flash != null)
            {
                float flashAlpha = sinceImpact >= 0f ? 0.7f * (1f - Mathf.Clamp01(sinceImpact / FlashSeconds)) : 0f;
                flash.color = new Color(1f, 1f, 1f, flashAlpha);
            }

            if (shakeRoot != null)
            {
                float shake = sinceImpact >= 0f && sinceImpact < ShakeSeconds
                    ? ShakeAmplitude * (1f - sinceImpact / ShakeSeconds)
                    : 0f;
                shakeRoot.anchoredPosition = new Vector2(Mathf.Sin(sinceImpact * 113f), Mathf.Cos(sinceImpact * 97f)) * shake;
            }

            if (canvasGroup != null) canvasGroup.alpha = 1f - slideOut;
        }

        private void Finish()
        {
            Stage = BossIntroStage.Idle;
            elapsed = 0f;
            SetOverlayVisible(false);
            if (bossHud != null) bossHud.ClearIntroPresentation();
            if (Time.timeScale == 0f) Time.timeScale = previousTimeScale;
        }

        private void SetOverlayVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (!visible && shakeRoot != null) shakeRoot.anchoredPosition = Vector2.zero;
        }

        private void CaptureRestPositions()
        {
            if (restPositionsCaptured || IsPlaying) return;
            if (bossPanel != null) bossRestPosition = bossPanel.anchoredPosition;
            if (playerPanel != null) playerRestPosition = playerPanel.anchoredPosition;
            restPositionsCaptured = true;
        }

        private CharacterDefinition ResolveCharacter()
        {
            string id = runSession != null ? runSession.CharacterId : null;
            CharacterDefinition fallback = null;
            foreach (CharacterDefinition character in characters)
            {
                if (character == null) continue;
                if (character.CharacterId == id) return character;
                if (fallback == null) fallback = character;
            }

            return fallback;
        }

        // The boss's own still body; the intro art (§7) replaces this later.
        private static Sprite ResolveBossSprite(BossController boss)
        {
            BossMovementAnimator animator = boss.GetComponent<BossMovementAnimator>();
            if (animator != null && animator.BodySprite != null) return animator.BodySprite;
            if (animator != null && animator.Source != null && animator.Source.sprite != null) return animator.Source.sprite;
            Sprite largest = null;
            float largestArea = 0f;
            foreach (SpriteRenderer renderer in boss.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null || renderer.GetComponent<GroundShadow>() != null) continue;
                float area = renderer.bounds.size.x * renderer.bounds.size.y;
                if (area <= largestArea) continue;
                largestArea = area;
                largest = renderer.sprite;
            }

            return largest;
        }

        private static void SetPortrait(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
        }

        // Not Escape: a skip that finishes the intro would restore time in the same frame the pause menu reads it.
        private static bool SkipPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                return true;
            if (Mouse.current?.leftButton.wasPressedThisFrame == true) return true;
            Gamepad gamepad = Gamepad.current;
            return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private static float EaseOutCubic(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u;
        }

        private static float EaseInCubic(float t) => t * t * t;
    }
}
