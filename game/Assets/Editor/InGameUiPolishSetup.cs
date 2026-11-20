using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static TrickalFanGame.Editor.MenuUiBuilder;

namespace TrickalFanGame.Editor
{
    // Focused presentation migration: preserve the existing HUD, bindings and other scene edits.
    public static class InGameUiPolishSetup
    {
        [MenuItem("Trickal Fan Game/Menu/Setup NPC and Item UI Polish")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before UI setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            foreach (var pause in roots.SelectMany(root => root.GetComponentsInChildren<GamePauseArtifactView>(true)))
                StyleConfirmation(pause);
            foreach (var view in roots.SelectMany(root => root.GetComponentsInChildren<PickupDescriptionView>(true)))
                RoundDescription(view.GetComponent<Image>());
            foreach (var view in roots.SelectMany(root => root.GetComponentsInChildren<GameArtifactAcquisitionToastView>(true)))
                RoundDescription(view.GetComponent<Image>());
            foreach (var keeper in roots.SelectMany(root => root.GetComponentsInChildren<ShopKeeper>(true)))
                StyleKeeper(keeper);

            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Rooms" })
                         .Select(AssetDatabase.GUIDToAssetPath))
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset.GetComponentInChildren<ShopKeeper>(true) == null) continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var keeper in root.GetComponentsInChildren<ShopKeeper>(true)) StyleKeeper(keeper);
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new InvalidOperationException("Could not save " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Game Scene save failed.");
            AssetDatabase.SaveAssets();
            Debug.Log("NPC and item UI polish setup complete.");
        }

        private static void StyleKeeper(ShopKeeper keeper)
        {
            if (keeper.NameLabel != null)
            {
                Undo.RecordObject(keeper.NameLabel.gameObject, "Hide NPC name");
                keeper.NameLabel.gameObject.SetActive(false);
            }
            if (keeper.Prompt != null)
            {
                Undo.RecordObject(keeper.Prompt, "Update shop prompt");
                keeper.Prompt.text = ShopKeeper.OpenPrompt;
            }
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            var bubble = NpcSpeechBubbleSetup.Ensure(keeper.transform, font, new Vector2(0, 0.8f), ShopKeeper.GreetingLine);
            Undo.RecordObject(keeper, "Style NPC greeting");
            keeper.ConfigureVisuals(keeper.Portrait, keeper.NameLabel, keeper.Prompt, bubble);
            EditorUtility.SetDirty(keeper);
        }

        public static void RoundDescription(Image image)
        {
            if (image == null) throw new InvalidOperationException("Missing item description background.");
            Skin(image.gameObject, MenuArtSetup.RoundedFill, image.color);
        }

        public static void StyleConfirmation(GamePauseArtifactView view)
        {
            if (view.LeaveConfirmation == null || view.ConfirmationFocusScope == null) return;
            RectTransform overlay = (RectTransform)view.LeaveConfirmation.transform;
            Place(overlay, Vector2.zero, new Vector2(1920, 1080));
            // Keep this layer after the pause panel; runtime also fits letterboxed canvas margins.
            overlay.SetAsLastSibling();
            Image dim = Ensure<Image>(overlay.gameObject);
            dim.sprite = null;
            dim.color = new Color(0.025f, 0.02f, 0.04f, 0.68f);
            dim.raycastTarget = true;
            RectTransform dialog = view.ConfirmationFocusScope;
            Place(dialog, Vector2.zero, new Vector2(760, 392));
            Skin(dialog.gameObject, MenuArtSetup.PanelMain, Color.white, raycast: true);
            Outline old = dialog.GetComponent<Outline>();
            if (old != null) Undo.DestroyObjectImmediate(old);
            TMP_Text title = dialog.Find("Title").GetComponent<TMP_Text>();
            Place(title.rectTransform, new Vector2(0, 104), new Vector2(640, 56));
            StyleText(title, 32, Ink);
            TMP_Text warning = dialog.Find("Warning").GetComponent<TMP_Text>();
            Place(warning.rectTransform, new Vector2(0, 18), new Vector2(620, 104));
            StyleText(warning, 22, InkSoft);
            warning.textWrappingMode = TextWrappingModes.Normal;
            StyleButton(view.CancelLeaveButton, new Vector2(-152, -112), false);
            StyleButton(view.ConfirmLeaveButton, new Vector2(152, -112), true);
        }

        private static void StyleButton(Button button, Vector2 position, bool primary)
        {
            RectTransform rect = (RectTransform)button.transform;
            Place(rect, position, new Vector2(272, 64));
            Image image = Skin(button.gameObject,
                primary ? MenuArtSetup.ButtonPrimary : MenuArtSetup.ButtonSecondary, Color.white,
                raycast: true, fitHeight: 64);
            button.targetGraphic = image;
            Outline old = button.GetComponent<Outline>();
            if (old != null) Undo.DestroyObjectImmediate(old);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            Place(label.rectTransform, Vector2.zero, new Vector2(240, 50));
            StyleText(label, 24, primary ? ButtonText : Ink);
            label.fontStyle = FontStyles.Normal;
            Highlight(button);
        }
    }
}
