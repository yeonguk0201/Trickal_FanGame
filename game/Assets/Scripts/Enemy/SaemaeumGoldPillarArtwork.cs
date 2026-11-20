using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Six cells keep a common scale and authored ground anchors, including empty space above rubble.
    public static class SaemaeumGoldPillarArtwork
    {
        public const string IntactPath = "CombatEffects/saemaeum-gold-pillar";
        public const string BreakPath = "CombatEffects/saemaeum-gold-pillar-six-frame";
        public const int FrameCount = 6;
        private static Sprite intact;
        private static readonly Sprite[] frames = new Sprite[FrameCount];
        // Pixel anchors correct small shifts in the generated cells without cropping or enlarging falling chunks.
        private static readonly Vector2[] GroundPivots =
        {
            new(266f, 48f), new(254.5f, 48f), new(247f, 48f),
            new(264f, 39f), new(251.5f, 38f), new(255.5f, 42f)
        };

        public static Sprite Intact
        {
            get
            {
                if (intact != null) return intact;
                Texture2D texture = Resources.Load<Texture2D>(IntactPath);
                if (texture == null) return null;
                intact = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(619.5f / 1254f, 13f / 1254f), 863f, 0, SpriteMeshType.FullRect);
                intact.name = "Saemaeum gold pillar intact";
                return intact;
            }
        }

        public static Sprite LightlyDamaged => Frame(0);

        public static Sprite Frame(int index)
        {
            index = Mathf.Clamp(index, 0, FrameCount - 1);
            if (frames[index] != null) return frames[index];
            Texture2D texture = Resources.Load<Texture2D>(BreakPath);
            if (texture == null) return null;
            int cell = texture.width / 3;
            frames[index] = Sprite.Create(texture,
                new Rect(index % 3 * cell, (1 - index / 3) * cell, cell, cell),
                GroundPivots[index] / cell, 290f, 0, SpriteMeshType.FullRect);
            frames[index].name = "Saemaeum gold pillar damage " + (index + 1);
            return frames[index];
        }
    }
}
