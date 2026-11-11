namespace TrickalFanGame.Enemy
{
    // Keeps a ranged enemy committed to what it is doing near a threshold. Without it, an enemy standing on the edge of
    // its distance band, or of an obstacle's shadow, switches between two movement choices every physics step and
    // shuffles left and right in place.
    public sealed class RangedStanceKeeper
    {
        // How long a blocked line of fire is waited out, holding still, before the enemy walks to find a new one.
        public const float SightLossGrace = 0.3f;

        private int radialMode;
        private bool hadSight;
        private float sightLostAt = float.NegativeInfinity;

        // -1 back away, 0 stay, 1 close in. A move toward the band continues until the middle of the band, so a
        // moving player does not flip the enemy between backing away and holding on every step.
        public int RadialMode => radialMode;

        public int UpdateRadial(float distance, float preferredDistance, float tolerance)
        {
            if ((radialMode > 0 && distance <= preferredDistance) ||
                (radialMode < 0 && distance >= preferredDistance))
                radialMode = 0;
            if (radialMode == 0)
            {
                if (distance > preferredDistance + tolerance) radialMode = 1;
                else if (distance < preferredDistance - tolerance) radialMode = -1;
            }

            return radialMode;
        }

        // True when the enemy should walk to find a line of fire; false when it should act in sight. Sight returns
        // at once, but losing it is waited out briefly so the shadow edge cannot toggle the two every step.
        public bool ShouldSeekSight(bool hasSight, float currentTime)
        {
            if (hasSight)
            {
                hadSight = true;
                sightLostAt = float.NegativeInfinity;
                return false;
            }

            if (!hadSight) return true;
            if (float.IsNegativeInfinity(sightLostAt)) sightLostAt = currentTime;
            if (currentTime < sightLostAt + SightLossGrace) return false;
            hadSight = false;
            return true;
        }

        public void Reset()
        {
            radialMode = 0;
            hadSight = false;
            sightLostAt = float.NegativeInfinity;
        }
    }
}
