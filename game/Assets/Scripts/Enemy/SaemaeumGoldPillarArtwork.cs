using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // All breakup cells keep the first cell's scale and ground anchor, including empty space above rubble.
    public static class SaemaeumGoldPillarArtwork
    {
        public const string IntactPath = "CombatEffects/saemaeum-gold-pillar";
        public const string BreakPath = "CombatEffects/saemaeum-gold-pillar-break";
        private static Sprite intact;
        private static readonly Sprite[] frames = new Sprite[4];

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

        public static Sprite Frame(int index)
        {
            index = Mathf.Clamp(index, 0, 3);
            if (frames[index] != null) return frames[index];
            Texture2D texture = Resources.Load<Texture2D>(BreakPath);
            if (texture == null) return null;
            int cell = texture.width / 2;
            frames[index] = Sprite.Create(texture,
                new Rect(index % 2 * cell, (1 - index / 2) * cell, cell, cell),
                new Vector2(317f / 627f, 38f / 627f), 398f, 0, SpriteMeshType.FullRect);
            frames[index].name = "Saemaeum gold pillar damage " + (index + 1);
            return frames[index];
        }
    }
}
