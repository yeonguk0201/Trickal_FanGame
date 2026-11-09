using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Number-0: outlined materials for damage numbers, their shadow and the blocked label, wired into the HUD-6
    // pool. The TMP SDF shader draws an outline only with OUTLINE_ON, and a build keeps that variant only when a saved
    // material uses it, so the outline cannot be switched on from runtime code alone.
    public static class Number0Setup
    {
        public const string NumberMaterialPath = "Assets/Fonts/Combat Number.mat";
        public const string ShadowMaterialPath = "Assets/Fonts/Combat Number Shadow.mat";
        public const string BlockedLabelMaterialPath = "Assets/Fonts/Combat Number Blocked Label.mat";
        public const float OutlineWidth = 0.4f;
        public static readonly Color OutlineColor = new(0.76f, 0.28f, 0.02f, 1f);
        public static readonly Color BlockedLabelOutlineColor = new(194f / 255f, 143f / 255f, 38f / 255f, 1f);
        // Opaque: the shadow's vertex colour (WorldCombatNumberPool) sets its transparency and fade.
        public static readonly Color ShadowOutlineColor = new(0.36f, 0.13f, 0.02f, 1f);

        [MenuItem("Trickal Fan Game/Beta/Setup Number-0 Damage Numbers")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Number-0 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null || font.material == null) throw new InvalidOperationException("Frontend TMP font is missing.");
            Material number = OutlinedMaterial(NumberMaterialPath, font.material, OutlineColor);
            Material shadow = OutlinedMaterial(ShadowMaterialPath, font.material, ShadowOutlineColor);
            Material blockedLabel = OutlinedMaterial(BlockedLabelMaterialPath, font.material, BlockedLabelOutlineColor);

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            WorldCombatNumberPool[] pools = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldCombatNumberPool>(true)).ToArray();
            if (pools.Length != 1) throw new InvalidOperationException("Run HUD-6 setup first: one combat number pool.");
            Undo.RecordObject(pools[0], "Setup Number-0 Damage Numbers");
            pools[0].ConfigureMaterials(number, shadow, blockedLabel);
            EditorUtility.SetDirty(pools[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Number-0 setup.");
            AssetDatabase.SaveAssets();
            Debug.Log("Number-0 setup complete. Damage numbers, their shadow and the blocked label use outlined font materials.");
        }

        // Rerunning updates the same asset (same GUID) from the font's current material.
        private static Material OutlinedMaterial(string path, Material source, Color outlineColor)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(source);
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
            ShaderUtilities.UpdateShaderRatios(material);
            if (created) AssetDatabase.CreateAsset(material, path);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
