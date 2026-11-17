using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static TrickalFanGame.Editor.MenuUiBuilder;

namespace TrickalFanGame.Editor
{
    public static class RewardUiArtworkSetup
    {
        public static readonly Vector2 PanelSize = new(1800, 900);
        public static readonly Vector2 CardSize = new(550, 560);
        private const string Folder = "Assets/Art/UI/Reward/";

        [MenuItem("Trickal Fan Game/UI/Apply Clover Reward UI")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before applying reward artwork.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
            var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ItemRewardSelectionView>(true)).Single();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Apply Clover Reward UI");
            Sprite cardSprite = Import("reward-card-background");
            Sprite confirmSprite = Import("reward-confirm-button");
            var panel = view.FocusScope;
            Place(panel, new Vector2(0, -12), PanelSize);
            Skin(panel.gameObject, MenuArtSetup.PanelMain, Color.white);
            DisableOutline(panel);
            view.GetComponent<Image>().color = new Color(0.04f, 0.08f, 0.03f, 0.78f);
            var titleArt = Ensure<Image>(Node(panel, "Title Artwork", new Vector2(0, 428), new Vector2(700, 232)).gameObject);
            titleArt.sprite = Import("reward-title-plaque-original-wide");
            titleArt.type = Image.Type.Simple;
            titleArt.color = Color.white;
            titleArt.raycastTarget = false;
            titleArt.preserveAspect = true;
            Style(view.TitleText, 0, 438, 540, 60, 40);
            view.TitleText.transform.SetAsLastSibling();
            panel.Find("Subtitle").gameObject.SetActive(false);
            for (int i = 0; i < view.Cards.Length; i++) ApplyCard(view.Cards[i], (i - 1) * 570, cardSprite);
            ApplyButton(view.ConfirmButton, -210, confirmSprite, false);
            ApplyButton(view.CancelButton, 210, null, true);
            var confirmClover = view.ConfirmButton.transform.Find("Clover");
            if (confirmClover != null) Undo.DestroyObjectImmediate(confirmClover.gameObject);
            view.InputHintText.gameObject.SetActive(false);
            view.HideNow();
            foreach (Component component in view.GetComponentsInChildren<Component>(true)) EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save reward UI.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Clover Reward UI applied.");
        }

        private static void ApplyCard(ItemRewardCardView card, float x, Sprite sprite)
        {
            var root = (RectTransform)card.transform;
            Place(root, new Vector2(x, 10), CardSize);
            var background = root.GetComponent<Image>();
            background.sprite = sprite;
            background.type = Image.Type.Simple;
            background.color = Color.white;
            DisableOutline(root);
            card.Button.colors = Colors();
            var highlightRect = Node(root, "Selection Highlight", Vector2.zero, CardSize);
            var highlightImage = Ensure<Image>(highlightRect.gameObject);
            highlightImage.sprite = Import("reward-card-selected");
            highlightImage.type = Image.Type.Simple;
            highlightImage.color = Color.white;
            highlightImage.raycastTarget = false;
            var highlight = highlightRect.gameObject;
            highlight.transform.SetAsFirstSibling();
            highlight.SetActive(false);
            Picture(root, "State Ribbon", MenuArtSetup.SectionRibbon, new Vector2(0, 258), new Vector2(330, 65));
            Picture(root, "State Clover", MenuArtSetup.Clover, new Vector2(-80, 258), new Vector2(40, 40));
            Style(card.StateText, 20, 258, 220, 46, 28, RibbonText);
            card.StateText.transform.SetAsLastSibling();
            Badge(root, "Kind Badge", 94, 192, 190, 42);
            Style(card.KindText, 94, 192, 178, 36, 24);
            var art = Ensure<Image>(Node(root, "Item Artwork", new Vector2(-125, 103), new Vector2(140, 140)).gameObject);
            art.raycastTarget = false;
            art.preserveAspect = true;
            Style(card.NameText, 94, 122, 275, 96, 35);
            Badge(root, "Rarity Badge", 94, 57, 160, 44);
            Style(card.RarityText, 94, 57, 145, 38, 25);
            var divider = Node(root, "Divider", new Vector2(0, -8), new Vector2(454, 2));
            divider.GetComponent<Image>().color = Divider;
            Picture(root, "Divider Clover", MenuArtSetup.Clover, new Vector2(0, -8), new Vector2(29, 29));
            Style(card.EffectText, 0, -115, 454, 160, 29);
            card.EffectText.alignment = TextAlignmentOptions.TopLeft;
            card.EffectText.lineSpacing = 0;
            Badge(root, "Stack Badge", 0, -222, 438, 46);
            Style(card.StackText, 0, -222, 410, 40, 25);
            if (root.Find("Action") != null) Undo.DestroyObjectImmediate(root.Find("Action").gameObject);
            highlight.transform.SetAsFirstSibling();
            card.ConfigureArtwork(art, highlight, root.Find("State Clover").gameObject, root.Find("State Ribbon").gameObject);

        }

        private static void Badge(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = Node(parent, name, new Vector2(x, y), new Vector2(w, h));
            Skin(rect.gameObject, MenuArtSetup.RoundedFill, new Color(0.94f, 0.87f, 0.91f));
            rect.SetAsFirstSibling();
        }

        private static void ApplyButton(Button button, float x, Sprite sprite, bool secondary)
        {
            var rect = (RectTransform)button.transform;
            Place(rect, new Vector2(x, -382), new Vector2(secondary ? 376 : 420, secondary ? 84 : 144));
            var image = button.GetComponent<Image>();
            if (secondary) Skin(button.gameObject, MenuArtSetup.ButtonSecondary, Color.white, true, 84);
            else { image.sprite = sprite; image.type = Image.Type.Simple; image.color = Color.white; }
            button.colors = Colors();
            Style(rect.Find("Label").GetComponent<TMP_Text>(), 0, 0, 300, 65, 37, secondary ? Ink : RibbonText);
        }

        private static void Style(TMP_Text text, float x, float y, float w, float h, float size, Color? color = null)
        {
            Place(text.rectTransform, new Vector2(x, y), new Vector2(w, h));
            StyleText(text, size, color ?? Ink);
            text.enableAutoSizing = true;
            text.fontSizeMin = 17;
            text.fontSizeMax = size;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void DisableOutline(Transform target)
        {
            if (target.TryGetComponent<Outline>(out var outline)) outline.enabled = false;
        }

        private static Sprite Import(string name)
        {
            string path = Folder + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = 100;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("Trickal Fan Game/UI/Capture Clover Reward UI")]
        public static void CapturePreview()
        {
            MenuScreenCapture.Capture("reward-clover", Week13FrontendSetup.GameScenePath,
                Week13Hud1Setup.HudRootName, frame =>
                {
                    MenuScreenCapture.ShowOnly(frame, Week16Reward2Setup.OverlayName);
                    var view = frame.GetComponentInChildren<ItemRewardSelectionView>(true);
                    string[] ids = { "artifact-greed-ring", "artifact-attatta-gun", "active-divine-punishment" };
                    for (int i = 0; i < view.Cards.Length; i++)
                    {
                        var definition = AssetDatabase.LoadAssetAtPath<TrickalFanGame.Item.ItemDefinition>("Assets/Items/" + ids[i] + ".asset");
                        view.Cards[i].Bind(TrickalFanGame.Item.ItemRewardCandidate.ForItem(definition, 0), null);
                        view.Cards[i].SetChosen(i == 0);
                    }
                    view.ConfirmButton.interactable = true;
                    view.ConfirmButton.targetGraphic.canvasRenderer.SetColor(Color.white);
                    view.Overlay.alpha = 1;
                });
        }

        public static void ApplyVerifyAndCaptureBatch()
        {
            SetupAndVerifyBatch();
            CapturePreview();
        }

        public static void SetupAndVerifyBatch()
        {
            Setup();
            Week16Reward2Verification.Verify();
        }
    }
}
