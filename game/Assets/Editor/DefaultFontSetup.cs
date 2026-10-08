using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace TrickalFanGame.Editor
{
    // The default font (2026-10-08): ONE Mobile POP. Every Scene, Prefab and Setup already uses the one TMP font
    // asset at Week13FrontendSetup.FontPath, so this swaps that asset's source font in place. Its GUID and its
    // material stay, so no text reference changes; the glyphs it held are rendered again from the new font.
    // Re-running changes nothing once the source font is set.
    public static class DefaultFontSetup
    {
        public const string SourceFontPath = "Assets/Fonts/ONE Mobile POP.ttf";
        public const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        public static void SetupAndVerifyBatch()
        {
            Setup();
            string guid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.FontPath);
            Setup();
            Require(guid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.FontPath),
                "Default font setup changed the TMP font asset GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Fonts/Setup Default Font (ONE Mobile POP)")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before the default font setup.");
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (source == null || font == null || settings == null)
                throw new InvalidOperationException(
                    $"The default font setup requires {SourceFontPath}, the Frontend TMP font asset (run the " +
                    "Frontend setup first), and the TMP Settings.");

            if (font.sourceFontFile != source) ReplaceSourceFont(font, source);

            // New TMP texts made without a Setup also start with the default font.
            SerializedObject serializedSettings = new(settings);
            SerializedProperty defaultFont = serializedSettings.FindProperty("m_defaultFontAsset");
            if (defaultFont.objectReferenceValue != font)
            {
                defaultFont.objectReferenceValue = font;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Default font setup complete: {Week13FrontendSetup.FontPath} renders {font.characterTable.Count} " +
                      $"characters from {SourceFontPath} and is the TMP default font.");
        }

        private static void ReplaceSourceFont(TMP_FontAsset font, Font source)
        {
            string characters = string.Concat(font.characterTable
                .Select(character => char.ConvertFromUtf32((int)character.unicode)));
            // The face metrics (line height, ascender, ...) come from a font asset made the same way as the first one.
            int pointSize = Mathf.RoundToInt(font.faceInfo.pointSize);
            TMP_FontAsset probe = TMP_FontAsset.CreateFontAsset(source, pointSize, font.atlasPadding,
                GlyphRenderMode.SDFAA, font.atlasWidth, font.atlasHeight);
            if (probe == null) throw new InvalidOperationException($"Unity could not read {SourceFontPath}.");
            try
            {
                FaceInfo faceInfo = probe.faceInfo;
                SerializedObject serialized = new(font);
                serialized.FindProperty("m_SourceFontFile").objectReferenceValue = source;
                serialized.FindProperty("m_SourceFontFileGUID").stringValue =
                    AssetDatabase.AssetPathToGUID(SourceFontPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                font.faceInfo = faceInfo;
                font.ClearFontAssetData(true);
                if (!font.TryAddCharacters(characters, out string missing))
                    throw new InvalidOperationException("The default font is missing characters the game uses: " +
                                                        missing);
            }
            finally
            {
                if (probe.material != null) UnityEngine.Object.DestroyImmediate(probe.material);
                foreach (Texture2D atlas in probe.atlasTextures)
                    if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas);
                UnityEngine.Object.DestroyImmediate(probe);
            }

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures)
                if (atlas != null) EditorUtility.SetDirty(atlas);
        }

        [MenuItem("Trickal Fan Game/Fonts/Verify Default Font")]
        public static void Verify()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Require(source != null && font != null && font.sourceFontFile == source,
                "Run the default font setup first: the Frontend TMP font asset must render from ONE Mobile POP.");
            Require(TMP_Settings.defaultFontAsset == font, "The TMP default font must be the Frontend TMP font asset.");
            Require(font.atlasPopulationMode == AtlasPopulationMode.Dynamic && font.material != null &&
                    AssetDatabase.GetAssetPath(font.material) == Week13FrontendSetup.FontPath &&
                    font.atlasTextures.Length > 0 && font.atlasTextures.All(atlas =>
                        atlas != null && AssetDatabase.GetAssetPath(atlas) == Week13FrontendSetup.FontPath),
                "The font asset must stay dynamic with its material and atlas textures saved inside it.");
            Require(font.characterTable.Count > 0 && font.characterTable.All(character => character.glyph != null) &&
                    font.HasCharacters("TRICKAL FAN GAME트릭컬 팬게임0123456789"),
                "The font asset must hold glyphs for its characters, Korean included.");
            Debug.Log($"Default font verification passed: {font.characterTable.Count} characters render from " +
                      "ONE Mobile POP in the Frontend TMP font asset, which is the TMP default font.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
