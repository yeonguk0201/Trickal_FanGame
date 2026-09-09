using TMPro;
using TrickalFanGame.Room;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public sealed class MinimapRoomMarkerView : MonoBehaviour
    {
        [SerializeField] private Image roomFill;
        [SerializeField] private Image currentOutline;
        [SerializeField] private TMP_Text symbolText;
        [SerializeField] private TMP_Text clearedBadgeText;

        public string RoomId { get; private set; }
        public RoomGridPosition GridPosition { get; private set; }
        public bool IsCurrent { get; private set; }
        public Image RoomFill => roomFill;
        public Image CurrentOutline => currentOutline;
        public TMP_Text SymbolText => symbolText;
        public TMP_Text ClearedBadgeText => clearedBadgeText;
        public bool HasVisited { get; private set; }
        public bool IsCleared { get; private set; }
        public bool IsSpecialRoomRevealed { get; private set; }

        public void ConfigureVisuals(Image configuredRoomFill, Image configuredCurrentOutline,
            TMP_Text configuredSymbolText)
        {
            ConfigureVisuals(configuredRoomFill, configuredCurrentOutline, configuredSymbolText, null);
        }

        public void ConfigureVisuals(Image configuredRoomFill, Image configuredCurrentOutline,
            TMP_Text configuredSymbolText, TMP_Text configuredClearedBadgeText)
        {
            roomFill = configuredRoomFill;
            currentOutline = configuredCurrentOutline;
            symbolText = configuredSymbolText;
            clearedBadgeText = configuredClearedBadgeText;
        }

        public void Bind(GeneratedRoomNode node, RoomRunState state, bool isCurrent)
        {
            RoomId = node?.RoomId;
            GridPosition = node != null ? node.GridPosition : default;
            IsCurrent = isCurrent;
            HasVisited = isCurrent || state?.HasVisited == true;
            IsCleared = state?.IsCleared == true;
            IsSpecialRoomRevealed = node != null && node.Role != GeneratedRoomRole.Intermediate;
            bool showClearedBadge = HasVisited && IsCleared;

            if (roomFill != null)
                roomFill.color = isCurrent ? new Color(0.24f, 0.67f, 0.95f, 1f)
                    : showClearedBadge ? new Color(0.2f, 0.42f, 0.34f, 1f)
                    : HasVisited ? new Color(0.31f, 0.38f, 0.47f, 1f)
                    : new Color(0.1f, 0.14f, 0.2f, 0.82f);
            if (currentOutline != null) currentOutline.gameObject.SetActive(isCurrent);
            if (symbolText != null) symbolText.text = Symbol(node, isCurrent, HasVisited);
            if (clearedBadgeText != null)
            {
                clearedBadgeText.text = showClearedBadge ? "V" : string.Empty;
                clearedBadgeText.gameObject.SetActive(showClearedBadge);
            }
            gameObject.name = isCurrent ? "Current Room" : HasVisited
                ? $"Visited Room {RoomId}"
                : $"Unknown Room {RoomId}";
        }

        private static string Symbol(GeneratedRoomNode node, bool isCurrent, bool hasVisited)
        {
            if (isCurrent) return "P";
            if (!hasVisited && node?.Role == GeneratedRoomRole.Intermediate) return "?";
            return node?.Role switch
            {
                GeneratedRoomRole.Start => "S",
                GeneratedRoomRole.Treasure => "T",
                GeneratedRoomRole.Boss => "B",
                _ => string.Empty,
            };
        }
    }
}
