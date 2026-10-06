using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Shop
{
    // Shop-0: the shopkeeper (시스트) standing in the shop room. E opens the shop UI while the player stands next to
    // them. The sprite is a placeholder until the character asset exists.
    [RequireComponent(typeof(Collider2D))]
    public sealed class ShopKeeper : MonoBehaviour
    {
        public const string DisplayName = "시스트";
        public const string OpenPrompt = "[E] 상점 열기";

        [SerializeField] private SpriteRenderer portrait;
        [SerializeField] private TextMeshPro nameLabel;
        [SerializeField] private TextMeshPro prompt;

        private ShopRoom owner;
        private ShopSession session;
        private bool isPlayerInside;
        private PlayerInventory playerInventory;
        private Health playerHealth;
        private PlayerActionState playerActionState;

        public SpriteRenderer Portrait => portrait;
        public TextMeshPro NameLabel => nameLabel;
        public TextMeshPro Prompt => prompt;
        public bool CanInteract => isPlayerInside && owner != null && owner.Stock != null && session != null &&
                                   !session.IsOpen && Time.timeScale > 0f &&
                                   (playerHealth == null || !playerHealth.IsDead) &&
                                   (playerActionState == null || playerActionState.CanTransition);

        public void ConfigureVisuals(SpriteRenderer configuredPortrait, TextMeshPro configuredNameLabel,
            TextMeshPro configuredPrompt)
        {
            portrait = configuredPortrait;
            nameLabel = configuredNameLabel;
            prompt = configuredPrompt;
        }

        public void Bind(ShopRoom configuredOwner, ShopSession configuredSession)
        {
            owner = configuredOwner;
            session = configuredSession;
            RefreshPrompt();
        }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
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
        }
    }
}
