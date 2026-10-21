using UnityEngine;

namespace TrickalFanGame.Item
{
    // A 교주의 권능 on the floor. Touching it puts it in the player's 권능 slot; a held 권능 is swapped out and dropped
    // here with the gauge it had, so picking it up again restores that gauge (contract 23 §9.4). A new 권능 has no
    // stored gauge and starts empty. A dropped 권능 ignores the player until they have stepped off it once.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class ActivePowerPickup : MonoBehaviour, IDescribedPickup
    {
        private void OnEnable()
        {
            TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);
            DescribedPickupRegistry.Register(this);
        }

        private void OnDisable() => DescribedPickupRegistry.Unregister(this);

        public static readonly Color PowerColor = new(1f, 0.78f, 0.28f, 1f);
        public const int NoStoredCharge = -1;

        // Physics steps without player contact before a dropped 권능 can be collected again.
        private const int ReleaseSteps = 2;

        [SerializeField] private ItemDefinition definition;
        [SerializeField] private string instanceId;
        [SerializeField] private int storedCharge = NoStoredCharge;
        [SerializeField] private SpriteRenderer display;

        private bool waitsForPlayerExit;
        private bool isCollected;
        private int fixedStep;
        private int lastPlayerContactStep;

        public ItemDefinition Definition => definition;
        public Vector3 Position => transform.position;
        public bool IsAvailable => !isCollected && definition != null;
        public string InstanceId => instanceId;
        public bool HasStoredCharge => storedCharge >= 0;
        public int StoredCharge => storedCharge;
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

        public void Configure(ItemDefinition configuredDefinition, string configuredInstanceId,
            int configuredStoredCharge, bool waitForPlayerExit)
        {
            definition = configuredDefinition;
            instanceId = configuredInstanceId;
            storedCharge = configuredStoredCharge < 0 ? NoStoredCharge : configuredStoredCharge;
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
            PlayerPowerSlot slot = other != null ? other.GetComponentInParent<PlayerPowerSlot>() : null;
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
            display.color = PowerColor;
            TrickalFanGame.Frontend.UserArtwork.Apply(display, definition);
        }
    }
}
