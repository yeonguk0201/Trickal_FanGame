using System;
using System.Linq;
using TrickalFanGame.Frontend;
using TrickalFanGame.Character;
using TrickalFanGame.Item;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class InGameUiPolishVerification
    {
        public static void SetupAndVerifyBatch()
        {
            InGameUiPolishSetup.Setup();
            Verify();
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) CaptureNpcBatch();
        }

        [MenuItem("Trickal Fan Game/Menu/Verify NPC and Item UI Polish")]
        public static void Verify()
        {
            var scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var pause = roots.SelectMany(root => root.GetComponentsInChildren<GamePauseArtifactView>(true)).Single();
            var overlay = (RectTransform)pause.LeaveConfirmation.transform;
            Assert(overlay.sizeDelta.x >= 1920 && overlay.sizeDelta.y >= 1080 &&
                   overlay.GetComponent<Image>().raycastTarget &&
                   overlay.GetComponent<Image>().color.a >= 0.6f &&
                   overlay.GetSiblingIndex() > pause.FocusScope.GetSiblingIndex(),
                "Confirmation must dim and block the full game behind its dialog.");
            Assert(pause.ConfirmationFocusScope.GetComponent<Image>().sprite == MenuArtSetup.Load(MenuArtSetup.PanelMain),
                "Confirmation must reuse the menu panel art.");
            foreach (Button button in new[] { pause.CancelLeaveButton, pause.ConfirmLeaveButton })
                Assert(button.image.sprite != null && button.targetGraphic == button.image &&
                       button.GetComponent<Outline>() == null,
                    "Confirmation buttons must use menu artwork without temporary rectangular outlines.");
            foreach (var image in roots.SelectMany(root => root.GetComponentsInChildren<Image>(true))
                         .Where(image => image.GetComponent<PickupDescriptionView>() != null ||
                                         image.GetComponent<GameArtifactAcquisitionToastView>() != null))
                Assert(image.sprite == MenuArtSetup.Load(MenuArtSetup.RoundedFill) &&
                       image.type == Image.Type.Sliced && !image.raycastTarget,
                    "Item description and acquisition toast must be rounded and pass through input.");
            Assert(ItemRewardCardView.RewardRarityLabel(ItemRarity.Uncommon) == "고급", "Use Korean rarity labels.");
            GameObject shop = AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special4Setup.ShopRoomPrefabPath);
            ShopKeeper keeper = shop.GetComponentInChildren<ShopKeeper>(true);
            Assert(!keeper.NameLabel.gameObject.activeSelf && keeper.Prompt.text == "[E]상점열기" &&
                   keeper.SpeechBubble.Fill.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(NpcSpeechBubbleSetup.SpritePath),
                "Shop NPC must hide its name and show a simple speech bubble and unprefixed shop prompt.");
            // Exercise layout at both short and long greeting lengths without changing the saved prefab.
            GameObject clone = UnityEngine.Object.Instantiate(keeper.SpeechBubble.gameObject);
            try
            {
                var bubble = clone.GetComponent<TrickalFanGame.Character.NpcSpeechBubble>();
                float shortHeight = 0f;
                foreach (string line in new[] { ShopKeeper.GoldiGreetingLine, ShopKeeper.GreetingLine,
                             ShopKeeper.GreetingLine + "\n" + ShopKeeper.GreetingLine + "\n" + ShopKeeper.GreetingLine })
                {
                    bubble.Show(line);
                    bubble.Text.ForceMeshUpdate(true);
                    if (line == ShopKeeper.GreetingLine) Assert(bubble.Text.textInfo.lineCount == 1,
                        "The shop greeting must fit on one line.");
                    Assert(bubble.Fill.size.x <= NpcSpeechBubble.MaxTextWidth + NpcSpeechBubble.Padding.x * 2f + 0.001f &&
                           bubble.Text.preferredHeight <= bubble.Text.rectTransform.sizeDelta.y + 0.01f &&
                           bubble.Outline.enabled && bubble.TailOutline.enabled && bubble.TailFill.enabled,
                        "Simple bubble must wrap text inside a bounded width and expand vertically without clipping.");
                    if (shortHeight == 0f) shortHeight = bubble.Fill.size.y;
                    if (line.Contains("\n")) Assert(bubble.Fill.size.y > shortHeight * 2f,
                        "Long dialogue must grow taller than the short greeting.");
                    bubble.Hide();
                    Assert(!bubble.IsShown, "NPC bubble must hide when interaction ends.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
            Debug.Log("NPC and item UI polish verification passed.");
        }

        public static void CaptureNpcBatch()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special4Setup.ShopRoomPrefabPath);
            ShopKeeper source = prefab.GetComponentInChildren<ShopKeeper>(true);
            ShopKeeper keeper = UnityEngine.Object.Instantiate(source.gameObject).GetComponent<ShopKeeper>();
            keeper.transform.position = Vector3.zero;
            keeper.Prompt.gameObject.SetActive(true);
            var camera = new GameObject("Review Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.7f;
            camera.transform.position = new Vector3(0, 0.6f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.38f, 0.55f, 0.32f);
            var target = new RenderTexture(720, 720, 24);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (var sample in new[] { ("short", ShopKeeper.GreetingLine),
                             ("long", ShopKeeper.GreetingLine + "\n" + ShopKeeper.GreetingLine + "\n" + ShopKeeper.GreetingLine) })
                {
                    keeper.SpeechBubble.Show(sample.Item2);
                    keeper.SpeechBubble.Text.ForceMeshUpdate(true);
                    keeper.Prompt.ForceMeshUpdate(true);
                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(720, 720, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 720, 720), 0, 0);
                    image.Apply();
                    System.IO.Directory.CreateDirectory(MenuScreenCapture.OutputFolder);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(MenuScreenCapture.OutputFolder,
                        "npc-simple-bubble-" + sample.Item1 + ".png"), image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            }
        }

        // Render-only review; sample text and visibility are discarded when the scene is reopened.
        public static void CaptureBatch()
        {
            MenuScreenCapture.Capture("ingame-restart-confirmation", Week13FrontendSetup.GameScenePath,
                Week13Hud1Setup.HudRootName, frame =>
                {
                    var pause = frame.GetComponentInChildren<GamePauseArtifactView>(true);
                    pause.Overlay.alpha = 1f;
                    pause.LeaveConfirmation.alpha = 1f;
                    pause.ConfirmationTitle.text = "처음부터 다시 시작할까요?";
                    pause.ConfirmationWarning.text = "현재 Run의 진행과 획득 아이템은 저장되지 않습니다.\n같은 캐릭터로 새 Run을 시작합니다.";
                    pause.ConfirmationActionLabel.text = "다시 시작";
                });
            MenuScreenCapture.Capture("ingame-rounded-item-description", Week13FrontendSetup.GameScenePath,
                Week13Hud1Setup.HudRootName, frame =>
                {
                    var description = frame.GetComponentInChildren<PickupDescriptionView>(true);
                    description.GetComponent<CanvasGroup>().alpha = 1f;
                    description.transform.Find("Kind").GetComponent<TMPro.TMP_Text>().text = "아티팩트";
                    description.transform.Find("Rarity").GetComponent<TMPro.TMP_Text>().text = "고급";
                    description.transform.Find("Name").GetComponent<TMPro.TMP_Text>().text = "광기의 가면";
                    description.transform.Find("Effect").GetComponent<TMPro.TMP_Text>().text = "반경 2.5m 적에게 1초마다 공격력의 20% 피해";
                    var toast = frame.GetComponentInChildren<GameArtifactAcquisitionToastView>(true);
                    toast.GetComponent<CanvasGroup>().alpha = 1f;
                    toast.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -200);
                    toast.MessageText.text = "<b>중량 조끼</b>\n<size=20>최대 HP +1칸 · 이동속도 -5%</size>";
                });
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
