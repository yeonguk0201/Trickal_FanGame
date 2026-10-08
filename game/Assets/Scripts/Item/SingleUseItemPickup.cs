using UnityEngine;

namespace TrickalFanGame.Item
{
    // A single-use spell or jjangsem spell on the floor. Touching it puts it in the player's spell slot; a full slot
    // swaps, dropping the held item here. A dropped item ignores the player until they have stepped off it once.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class SingleUseItemPickup : MonoBehaviour
    {
        public static readonly Color SpellColor = new(0.42f, 0.78f, 1f, 1f);
        public static readonly Color JjangsemSpellColor = new(0.86f, 0.5f, 1f, 1f);

        // Physics steps without player contact before a dropped item can be collected again.
        private const int ReleaseSteps = 2;

        [SerializeField] private ItemDefinition definition;
        [SerializeField] private string instanceId;
        [SerializeField] private SpriteRenderer display;

        private bool waitsForPlayerExit;
        private bool isCollected;
        private int fixedStep;
        private int lastPlayerContactStep;

        public ItemDefinition Definition => definition;
        public string InstanceId => instanceId;
        public SpriteRenderer Display => display;
        public bool WaitsForPlayerExit => waitsForPlayerExit;
        public bool IsCollected => isCollected;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
            display = GetComponentInChildren<SpriteRenderer>();
        }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            ApplyVisuals();
        }

        public void Configure(ItemDefinition configuredDefinition, string configuredInstanceId, bool waitForPlayerExit)
        {
            definition = configuredDefinition;
            instanceId = configuredInstanceId;
            waitsForPlayerExit = waitForPlayerExit;
            lastPlayerContactStep = fixedStep;
            ApplyVisuals();
        }

        public void ConfigureDisplay(SpriteRenderer configuredDisplay)
        {
            display = configuredDisplay;
            ApplyVisuals();
        }

        // The slot gives an unnamed pickup a Run-unique ID the first time it is collected.
        public void AssignInstanceId(string configuredInstanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) instanceId = configuredInstanceId;
        }

        public void MarkCollected()
        {
            isCollected = true;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void FixedUpdate()
        {
            fixedStep++;
            if (waitsForPlayerExit && fixedStep - lastPlayerContactStep > ReleaseSteps) waitsForPlayerExit = false;
        }

        private void OnTriggerEnter2D(Collider2D other) => HandleContact(other);

        private void OnTriggerStay2D(Collider2D other)
        {
            if (waitsForPlayerExit) HandleContact(other);
        }

        private void HandleContact(Collider2D other)
        {
            PlayerSpellSlot slot = other != null ? other.GetComponentInParent<PlayerSpellSlot>() : null;
            if (slot == null || isCollected) return;
            if (waitsForPlayerExit)
            {
                lastPlayerContactStep = fixedStep;
                return;
            }

            slot.TryCollect(this);
        }

        private void ApplyVisuals()
        {
            if (display == null || definition == null) return;
            display.color = definition.Kind == ItemKind.JjangsemSpell ? JjangsemSpellColor : SpellColor;
            TrickalFanGame.Frontend.UserArtwork.Apply(display, definition);
        }
    }
}
