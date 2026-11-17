using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TrickalFanGame.Frontend
{
    // Shop UI artwork: reports the mouse entering and leaving a shop cell, an invest row, or the item picture in the
    // purchase popup. It needs a raycast target Graphic on the same object.
    [DisallowMultipleComponent]
    public sealed class ShopHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public bool IsHovered { get; private set; }
        public event Action<ShopHoverTarget> Entered;
        public event Action<ShopHoverTarget> Exited;

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsHovered = true;
            Entered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
            Exited?.Invoke(this);
        }

        private void OnDisable() => IsHovered = false;
    }
}
