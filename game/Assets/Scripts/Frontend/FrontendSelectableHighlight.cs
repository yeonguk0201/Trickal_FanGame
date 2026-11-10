using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // Hover and keyboard-focus outlines for sliders, toggles and dropdowns (docs/16 §4). Buttons use
    // FrontendStartButton, which does the same for its own borders.
    [RequireComponent(typeof(Selectable))]
    public sealed class FrontendSelectableHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private GameObject hoverBorder;
        [SerializeField] private GameObject focusBorder;
        private Selectable selectable;
        private bool hovered;
        private bool focused;

        public GameObject HoverBorder => hoverBorder;
        public GameObject FocusBorder => focusBorder;

        public void ConfigureBorders(GameObject hover, GameObject focus)
        {
            hoverBorder = hover;
            focusBorder = focus;
            Refresh();
        }

        private void Awake() => selectable = GetComponent<Selectable>();

        private void OnEnable()
        {
            focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            Refresh();
        }

        private void OnDisable()
        {
            hovered = focused = false;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; Refresh(); }
        public void OnSelect(BaseEventData eventData) { focused = true; Refresh(); }
        public void OnDeselect(BaseEventData eventData) { focused = false; Refresh(); }

        private void Refresh()
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            bool interactable = selectable != null && selectable.IsInteractable();
            if (hoverBorder != null) hoverBorder.SetActive(hovered && interactable);
            if (focusBorder != null) focusBorder.SetActive(focused && interactable);
        }
    }
}
