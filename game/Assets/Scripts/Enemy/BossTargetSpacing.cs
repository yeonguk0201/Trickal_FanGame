using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Corner-0: a boss body is far heavier than the player or kinematic, so the player cannot push it away. A boss
    // therefore stops its approach once the bodies touch instead of walking into its target.
    public static class BossTargetSpacing
    {
        // The approach ends slightly inside touching distance so contact damage still applies.
        public const float ContactOverlap = 0.05f;

        // Center distance at which an approaching boss stops.
        public static float ResolveStopDistance(GameObject boss, Transform target)
        {
            if (boss == null || target == null) return 0f;
            return Mathf.Max(0f, EnemyObstacleNavigator.ResolveBodyRadius(boss) +
                                 EnemyObstacleNavigator.ResolveBodyRadius(target.gameObject) - ContactOverlap);
        }
    }
}
