using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Special-3: attached to the boundary seal of a door slot that hides a passage to the floor's secret room.
    // The seal is an ordinary Environment wall until a player bomb explodes within reach of it, during a fight or
    // after it. The opened passage is stored in the secret room's run state, so both sides reopen together and stay
    // open on revisit.
    [DisallowMultipleComponent]
    public sealed class SecretPassageWall : MonoBehaviour
    {
        private RoomRunState secretState;
        private string neighborRoomId;
        private Action applyOpen;
        private SpriteRenderer highlightedRenderer;
        private Color originalColor;

        public RoomRunState SecretState => secretState;
        public string NeighborRoomId => neighborRoomId;
        public bool IsOpen => secretState != null && secretState.IsSecretPassageOpen(neighborRoomId);

        public void Bind(RoomRunState configuredSecretState, string configuredNeighborRoomId, Action configuredApplyOpen)
        {
            Unsubscribe();
            secretState = configuredSecretState;
            neighborRoomId = configuredNeighborRoomId;
            applyOpen = configuredApplyOpen;
            if (secretState != null) secretState.Changed += Refresh;
        }

        public bool TryOpenByBomb()
        {
            // State.Changed rebinds this slot and the matching slot in the secret room.
            return secretState != null && isActiveAndEnabled && secretState.TryOpenSecretPassage(neighborRoomId);
        }

        public static int OpenByBombInCircle(Vector2 center, float radius)
        {
            int opened = 0;
            HashSet<SecretPassageWall> visited = new();
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(center, radius, DestructibleObstacle.ObstacleMask))
            {
                SecretPassageWall wall = collider != null ? collider.GetComponent<SecretPassageWall>() : null;
                if (wall != null && visited.Add(wall) && wall.TryOpenByBomb()) opened++;
            }

            return opened;
        }

        // Development panel only: tints the sealed wall so the tester can find it without the in-game clue.
        public void SetDevelopmentHighlight(bool highlighted)
        {
            if (highlighted && highlightedRenderer == null && TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                highlightedRenderer = spriteRenderer;
                originalColor = spriteRenderer.color;
                spriteRenderer.color = new Color(1f, 0.25f, 0.85f);
            }
            else if (!highlighted && highlightedRenderer != null)
            {
                highlightedRenderer.color = originalColor;
                highlightedRenderer = null;
            }
        }

        private void Refresh()
        {
            // A rebuilt floor destroys old seals without always running OnDestroy on never-activated objects.
            if (this == null)
            {
                Unsubscribe();
                return;
            }

            if (IsOpen) applyOpen?.Invoke();
        }

        private void Unsubscribe()
        {
            if (secretState != null) secretState.Changed -= Refresh;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
