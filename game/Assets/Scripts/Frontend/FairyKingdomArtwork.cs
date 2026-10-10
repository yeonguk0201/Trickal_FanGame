using UnityEngine;

namespace TrickalFanGame.Frontend
{
    public static class FairyKingdomArtwork
    {
        public const string ResourcePath = "FairyKingdomArtwork/Catalog";
        private static FairyKingdomArtworkCatalog catalog;
        public static FairyKingdomArtworkCatalog Catalog => catalog != null
            ? catalog : catalog = Resources.Load<FairyKingdomArtworkCatalog>(ResourcePath);
        public static Sprite Fit(string id, float width) => Catalog != null ? Catalog.Fit(id, width) : null;
    }
}
