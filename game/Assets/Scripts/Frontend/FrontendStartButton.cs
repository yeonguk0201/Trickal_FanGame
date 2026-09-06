using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public sealed class FrontendStartButton : Button
    {
        [SerializeField] private GameObject hoverBorder;
        [SerializeField] private GameObject focusBorder;
        private bool hovered;
        private bool focused;

        public void ConfigureBorders(GameObject hover, GameObject focus)
        {
            hoverBorder = hover;
            focusBorder = focus;
            RefreshBorders();
        }

        public override void OnPointerEnter(PointerEventData data)
        { base.OnPointerEnter(data); hovered = true; RefreshBorders(); }
        public override void OnPointerExit(PointerEventData data)
        { base.OnPointerExit(data); hovered = false; RefreshBorders(); }
        public override void OnSelect(BaseEventData data)
        { base.OnSelect(data); focused = true; RefreshBorders(); }
        public override void OnDeselect(BaseEventData data)
        { base.OnDeselect(data); focused = false; RefreshBorders(); }
        protected override void OnDisable()
        { base.OnDisable(); hovered = focused = false; RefreshBorders(); }

        private void RefreshBorders()
        {
            if (hoverBorder != null) hoverBorder.SetActive(hovered && IsInteractable());
            if (focusBorder != null) focusBorder.SetActive(focused && IsInteractable());
        }
    }
}
