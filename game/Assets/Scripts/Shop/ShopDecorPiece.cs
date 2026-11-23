using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Shop
{
    // Shows a shop decoration's damage: intact, cracked after the first hit, wrecked after the second. The third hit
    // breaks the obstacle, which removes it.
    [DisallowMultipleComponent]
    public sealed class ShopDecorPiece : MonoBehaviour
    {
        private DestructibleObstacle obstacle;
        private SpriteRenderer visual;
        private Sprite[] stages;

        public void Configure(DestructibleObstacle configuredObstacle, SpriteRenderer configuredVisual,
            Sprite[] configuredStages)
        {
            if (obstacle != null) obstacle.Hit -= OnHit;
            obstacle = configuredObstacle;
            visual = configuredVisual;
            stages = configuredStages;
            obstacle.Hit += OnHit;
            Refresh();
        }

        private void OnDestroy()
        {
            if (obstacle != null) obstacle.Hit -= OnHit;
        }

        private void OnHit(DestructibleObstacle _) => Refresh();

        private void Refresh()
        {
            if (obstacle == null || visual == null || stages == null || stages.Length == 0) return;
            visual.sprite = stages[Mathf.Clamp(obstacle.HitsTaken, 0, stages.Length - 1)];
        }
    }
}
