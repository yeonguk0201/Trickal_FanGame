using TMPro;
using TrickalFanGame.Character;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Shop
{
    // Shop-0: the shopkeeper (시스트) standing in the shop room. E opens the shop UI while the player stands next to
    // them. Shop-1: in a 골디 shop the same object shows 골디's name and artwork.
    // The keeper stands in the room like an NPC: drawn at the player's size, with a solid body the player walks
    // around, below the player's sprite. The trigger on this object is only the talking range: inside it the keeper
    // greets the player in a speech bubble.
    [RequireComponent(typeof(Collider2D))]
    public sealed class ShopKeeper : MonoBehaviour
    {
        private void OnEnable() => TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);

        public const string DisplayName = "시스트";
        public const string GoldiDisplayName = "골디";
        public const string ArtworkKey = "sist";
        public const string GoldiArtworkKey = "goldi";
        public const string GreetingLine = "뭐 하나 사시려구요?";
        public const string GoldiGreetingLine = "안녕하세요!";
        public const string BodyObjectName = "Body";
        public const string BodyLayerName = "Environment";
        // The player's body circle; the player's feet collide with the Environment layer.
        public const float BodyRadius = PlayerFeet.BodyRadius;
        // The longer side of the player's sprite in world units (Character_Erpin: 1254 px at 1000 px per unit).
        public const float PortraitWorldSize = 1.254f;
        public const string OpenPrompt = "[E] 상점 열기";

        [SerializeField] private SpriteRenderer portrait;
        [SerializeField] private TextMeshPro nameLabel;
        [SerializeField] private TextMeshPro prompt;
        [SerializeField] private NpcSpeechBubble speechBubble;

        private ShopRoom owner;
        private ShopSession session;
        private bool isPlayerInside;
        private PlayerInventory playerInventory;
        private Health playerHealth;
        private PlayerActionState playerActionState;

        public SpriteRenderer Portrait => portrait;
        public TextMeshPro NameLabel => nameLabel;
        public TextMeshPro Prompt => prompt;
        public NpcSpeechBubble SpeechBubble => speechBubble;
        public bool CanInteract => isPlayerInside && owner != null && owner.Stock != null && session != null &&
                                   !session.IsOpen && Time.timeScale > 0f &&
                                   (playerHealth == null || !playerHealth.IsDead) &&
                                   (playerActionState == null || playerActionState.CanTransition);

        public static string GetDisplayName(ShopKind kind) => kind == ShopKind.Goldi ? GoldiDisplayName : DisplayName;
        public static string GetArtworkKey(ShopKind kind) => kind == ShopKind.Goldi ? GoldiArtworkKey : ArtworkKey;
        public static string GetGreetingLine(ShopKind kind) =>
            kind == ShopKind.Goldi ? GoldiGreetingLine : GreetingLine;

        public void ConfigureVisuals(SpriteRenderer configuredPortrait, TextMeshPro configuredNameLabel,
            TextMeshPro configuredPrompt, NpcSpeechBubble configuredSpeechBubble = null)
        {
            portrait = configuredPortrait;
            nameLabel = configuredNameLabel;
            prompt = configuredPrompt;
            speechBubble = configuredSpeechBubble;
        }

        public void Bind(ShopRoom configuredOwner, ShopSession configuredSession)
        {
            owner = configuredOwner;
            session = configuredSession;
            ShopKind kind = owner != null ? owner.Kind : ShopKind.General;
            if (nameLabel != null) nameLabel.text = GetDisplayName(kind);
            TrickalFanGame.Frontend.UserArtwork.ApplyPickup(portrait, GetArtworkKey(kind));
            FitPortrait();
            RefreshPrompt();
        }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            TrickalFanGame.Frontend.UserArtwork.ApplyPickup(portrait, ArtworkKey);
            FitPortrait();
        }

        // Whatever the artwork's import size is, its longer side is drawn at the player's size.
        private void FitPortrait()
        {
            if (portrait == null || portrait.sprite == null) return;
            Vector2 size = portrait.sprite.bounds.size;
            float longerSide = Mathf.Max(size.x, size.y);
            if (longerSide > 0f) portrait.transform.localScale = Vector3.one * (PortraitWorldSize / longerSide);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player == null) return;
            SetPlayerPresence(true, player.GetComponent<PlayerInventory>(), player.GetComponent<Health>(),
                player.GetComponent<PlayerActionState>());
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerMovement>() != null) SetPlayerPresence(false, null, null);
        }

        private void Update()
        {
            if (!isPlayerInside) return;
            RefreshPrompt();
            if (Keyboard.current?.eKey.wasPressedThisFrame == true) TryOpen();
        }

        public bool TryOpen()
        {
            bool opened = CanInteract && session.TryOpen(owner, playerInventory);
            RefreshPrompt();
            return opened;
        }

        public void SetPlayerPresence(bool inside, PlayerInventory inventory, Health health,
            PlayerActionState actionState = null)
        {
            isPlayerInside = inside;
            playerInventory = inside ? inventory : null;
            playerHealth = inside ? health : null;
            playerActionState = inside ? actionState : null;
            RefreshPrompt();
        }

        private void RefreshPrompt()
        {
            if (prompt != null) prompt.gameObject.SetActive(CanInteract);
            if (speechBubble == null) return;
            // The greeting shows while the player is near and the shop screen is closed.
            if (isPlayerInside && (session == null || !session.IsOpen))
                speechBubble.Show(GetGreetingLine(owner != null ? owner.Kind : ShopKind.General));
            else
                speechBubble.Hide();
        }
    }
}
