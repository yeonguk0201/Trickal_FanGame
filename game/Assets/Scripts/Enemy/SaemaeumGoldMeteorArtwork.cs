using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public static class SaemaeumGoldMeteorArtwork
    {
        public const string FallPath = "CombatEffects/saemaeum-gold-meteor-fall";
        public const string ImpactPath = "CombatEffects/saemaeum-gold-meteor-impact";
        public const float FrameDuration = 0.07f;
        public const float FallRotation = 45f;
        private static readonly Sprite[] fall = new Sprite[4];
        private static readonly Sprite[] impact = new Sprite[4];

        // Pixel anchors were measured on the 627px cells, from their top-left corners.
        // Anchor the rock, not the much larger flame canvas; the impact stays on the ground.
        private static readonly Vector2[] head =
        {
            new(224f, 404f), new(206f, 404f), new(224f, 404f), new(206f, 404f)
        };
        private static readonly Vector2[] ground =
        {
            new(312f, 535f), new(312f, 535f), new(310f, 490f), new(312f, 490f)
        };

        public static Sprite FallFrame(int index) => Frame(FallPath, fall, head, index, 150f / SaemaeumGoldMeteor.LumpWidth);
        public static Sprite ImpactFrame(int index) => Frame(ImpactPath, impact, ground, index, 500f / 2.4f);

        private static Sprite Frame(string path, Sprite[] cache, Vector2[] anchors, int index, float ppu)
        {
            index = Mathf.Clamp(index, 0, 3);
            if (cache[index] != null) return cache[index];
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            int cell = texture.width / 2;
            Vector2 pivot = new(anchors[index].x / 627f, 1f - anchors[index].y / 627f);
            cache[index] = Sprite.Create(texture,
                new Rect(index % 2 * cell, (1 - index / 2) * cell, cell, cell),
                pivot, ppu, 0, SpriteMeshType.FullRect);
            cache[index].name = path + " frame " + index;
            return cache[index];
        }
    }
}
