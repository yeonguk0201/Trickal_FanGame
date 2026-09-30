using UnityEngine;

namespace TrickalFanGame.Run
{
    public sealed class PlayerDeathReason : MonoBehaviour
    {
        public string CurrentReason { get; private set; } = "UNKNOWN";

        public void SetReason(string reason)
        {
            CurrentReason = string.IsNullOrWhiteSpace(reason) ? "UNKNOWN" : reason;
        }
    }
}
