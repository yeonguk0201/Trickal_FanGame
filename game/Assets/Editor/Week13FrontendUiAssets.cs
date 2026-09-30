using System;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week13FrontendUiAssets
    {
        public const string PlaceholderFillSpritePath = "UI/Skin/UISprite.psd";

        public static Sprite LoadPlaceholderFillSprite()
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(PlaceholderFillSpritePath);
            if (sprite == null)
                throw new InvalidOperationException("Unity built-in placeholder fill Sprite is missing: " +
                    PlaceholderFillSpritePath);
            return sprite;
        }
    }
}
