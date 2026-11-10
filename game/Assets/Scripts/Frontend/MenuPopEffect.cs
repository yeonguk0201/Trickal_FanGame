using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // Menu (docs/16 §4): hover and keyboard focus make a control grow a little and press makes it dip, so the state
    // shows as a change in size and not only colour. Uses unscaled time so it also works in the pause menu.
    [RequireComponent(typeof(Selectable))]
    public sealed class MenuPopEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler,
        IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.07f;
        [SerializeField] private float pressScale = 0.95f;
        [SerializeField] private float speed = 18f;
        [SerializeField] private Transform target; // empty: the control itself

        private Selectable selectable;
        private bool hovered;
        private bool focused;
        private bool pressed;

        public float HoverScale => hoverScale;
        public Transform Target => target != null ? target : transform;

        public void Configure(Transform scaled, float scale, float press = 0.95f)
        {
            target = scaled;
            hoverScale = scale;
            pressScale = press;
        }

        private void Awake() => selectable = GetComponent<Selectable>();

        private void OnDisable()
        {
            hovered = focused = pressed = false;
            Target.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnSelect(BaseEventData eventData) => focused = true;
        public void OnDeselect(BaseEventData eventData) => focused = false;
        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        private void Update()
        {
            bool active = selectable == null || selectable.IsInteractable();
            float goal = !active ? 1f : pressed ? pressScale : hovered || focused ? hoverScale : 1f;
            float current = Target.localScale.x;
            float next = Mathf.Lerp(current, goal, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            if (Mathf.Abs(next - goal) < 0.001f) next = goal;
            Target.localScale = new Vector3(next, next, 1f);
        }
    }
}
