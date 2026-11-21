using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class RunLoadingScreenVerification
    {
        [MenuItem("Trickal Fan Game/Loading Screen/Verify")]
        public static void Verify()
        {
            RunLoadingScreenStyle style = Resources.Load<RunLoadingScreenStyle>(RunLoadingScreenStyle.ResourcePath);
            Require(style != null, "The loading style resource is missing.");
            Require(style.font == AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath),
                "Loading must use the existing ONE Mobile POP TMP font.");
            Require(style.portrait == AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/Characters/Player/Character_Erpin.png"), "Loading must preserve the existing portrait reference.");
            RunLoadingLayout layout = style.ReadLayout();
            RunLoadingLayer portrait = layout.layers.Single(l => l.id == "portrait");
            Require(portrait.portraitZoom == 121f && portrait.portraitX == -14f && portrait.portraitY == 60f &&
                portrait.w == 660f && portrait.h == 660f, "Approved portrait geometry changed.");
            Require(layout.layers.Single(l => l.id == "bubble").text == "간식 더 없어?\n더 꺼내 봐!" &&
                layout.layers.Single(l => l.id == "epithet").text == "요정여왕" &&
                layout.layers.Single(l => l.id == "name").text == "에르핀", "Approved character copy changed.");
            Require(SceneUtility.GetBuildIndexByScenePath(Week13FrontendSetup.ScenePath) >= 0 &&
                SceneUtility.GetBuildIndexByScenePath(Week13FrontendSetup.GameScenePath) >= 0,
                "Frontend and Game must be enabled in Build Settings.");
            Require(!RunSceneLoader.CanLoad(-1) && !RunSceneLoader.CanLoad(SceneManager.sceneCountInBuildSettings),
                "Invalid scene indices must be rejected without opening an overlay.");

            GameObject root = new("Loading Screen Verification") { hideFlags = HideFlags.HideAndDontSave };
            string uiError = null;
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (type is LogType.Exception or LogType.Error or LogType.Assert) uiError = message;
            };
            Application.logMessageReceived += capture;
            try
            {
                RunLoadingScreenView view = root.AddComponent<RunLoadingScreenView>();
                view.Build(style);
                int count = root.GetComponentsInChildren<RectTransform>(true).Length;
                view.Build(style);
                Require(count == root.GetComponentsInChildren<RectTransform>(true).Length,
                    "Repeated view construction duplicated UI objects.");
                Require(!view.IsVisible, "The loading screen must start hidden.");
                Canvas canvas = root.GetComponentInChildren<Canvas>(true);
                Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                    canvas.sortingOrder == RunLoadingScreenView.SortingOrder &&
                    canvas.GetComponent<Image>().raycastTarget && canvas.GetComponent<GraphicRaycaster>() != null,
                    "Loading must cover all game HUD and block pointer input.");
                foreach (RunLoadingLayer layer in layout.layers)
                {
                    RectTransform rect = (RectTransform)view.Frame.Find(layer.id);
                    Require(rect != null && rect.sizeDelta == new Vector2(layer.w, layer.h),
                        $"Loading layer size changed: {layer.id}.");
                    Vector2 expected = new(layer.x + layer.w / 2f, -layer.y - layer.h / 2f);
                    Require(Vector2.Distance(rect.anchoredPosition, expected) < 0.001f &&
                        rect.gameObject.activeSelf == layer.visible, $"Loading layer placement changed: {layer.id}.");
                }
                RectTransform clip = (RectTransform)view.Frame.Find("portrait/Portrait Clip");
                RectTransform art = (RectTransform)clip.Find("Character");
                Require(clip.GetComponent<Mask>() != null &&
                    clip.GetComponent<RunLoadingScreenGraphic>().Kind == RunLoadingScreenGraphic.Shape.Circle,
                    "The portrait must clip to a circle independently of its image zoom.");
                Require(Mathf.Approximately(art.localScale.x, 1.21f) && art.anchoredPosition.x == -14f &&
                    Mathf.Abs(art.anchoredPosition.y - (-clip.sizeDelta.y * 1.16f * 0.06f - 60f)) < 0.001f,
                    "Portrait zoom/translation does not match the exported HTML transform.");
                Require(root.GetComponentsInChildren<TMP_Text>(true).All(t => t.font == style.font),
                    "A loading label uses a different font.");
                foreach (float p in new[] { 0f, 0.72f, 1f, -1f, 2f })
                {
                    view.SetProgress(p);
                    RectTransform fill = (RectTransform)view.Frame.Find("bar/Track/Fill");
                    RectTransform track = (RectTransform)fill.parent;
                    TMP_Text percent = view.Frame.Find("percent/Text").GetComponent<TMP_Text>();
                    Require(Mathf.Abs(fill.sizeDelta.x - track.sizeDelta.x * Mathf.Clamp01(p)) < 0.01f &&
                        percent.text == $"{Mathf.RoundToInt(Mathf.Clamp01(p) * 100f)}%" &&
                        fill.gameObject.activeSelf == (p > 0f), "Bar and percentage diverged.");
                }
                view.SetVisible(true);
                Canvas.ForceUpdateCanvases();
                Require(view.IsVisible, "The loading overlay did not become visible.");
                view.SetVisible(false);
                Require(!view.IsVisible && !canvas.gameObject.activeInHierarchy,
                    "The closed loading overlay must not keep an invisible input blocker.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Application.logMessageReceived -= capture;
            }
            Require(uiError == null, $"Loading UI logged an error: {uiError}");

            VerifyInvalidLayout(style, "\"portraitZoom\": 121", "\"portraitZoom\": 0");
            VerifyInvalidLayout(style, "\"id\": \"bubble\"", "\"id\": \"portrait\"");
            Debug.Log("Run loading screen verification passed: approved layout, original portrait/font, circle mask, " +
                "idempotent view construction, synchronized/clamped progress, input backdrop and invalid layout rejection. " +
                "Scene transition timing and visual quality still require Play verification.");
        }

        private static void VerifyInvalidLayout(RunLoadingScreenStyle original, string from, string to)
        {
            RunLoadingScreenStyle copy = Object.Instantiate(original);
            TextAsset invalid = new(original.layout.text.Replace(from, to));
            copy.layout = invalid;
            bool rejected = false;
            try { copy.ReadLayout(); }
            catch (InvalidOperationException) { rejected = true; }
            finally { Object.DestroyImmediate(copy); Object.DestroyImmediate(invalid); }
            Require(rejected, "Invalid or duplicate loading layers were accepted.");
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
