using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class PitTileArtworkVerification
    {
        public const string PreviewPath = "Assets/Scenes/PitTileArtworkPreview.unity";
        public const string FocusPath = "Assets/Scenes/PitContourFocusPreview.unity";
        private static readonly string[][] Shapes = {
            new[] { "#" }, new[] { "###", "..#", "..#" }, new[] { "###", "#..", "###" },
            new[] { "###", "#.#", "###" }, new[] { "###", "..#", "###", "#..", "###" },
            new[] { "#####" }, new[] { "#", "#", "#", "#", "#" }, new[] { "#..", "#..", "###", "#..", "#.." },
            new[] { "##...", ".#...", ".###.", "...#.", "..##." }, new[] { "###", "###", "###" }
        };
        private static readonly string[] Names = { "1칸", "ㄱ", "ㄷ", "ㅁ", "ㄹ", "ㅡ", "ㅣ", "ㅏ", "구불구불", "3×3" };
        private static readonly string[] FileNames = { "single", "corner", "c", "ring", "lieul", "horizontal", "vertical", "branch", "winding", "3x3" };
        private static readonly string ArtDirectory = Path.GetFullPath("../docs/art-prompts/pit-tiles-v2");

        [MenuItem("Trickal Fan Game/Artwork/Verify Connected Pit Tiles")]
        public static void Verify()
        {
            Require(PitTileArtwork.Ribbon != null && PitTileArtwork.Depth != null &&
                PitTileArtwork.Ribbon.isReadable && PitTileArtwork.Depth.isReadable, "Missing readable pit source artwork.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week22Terrain0Setup.PitPrefabPath);
            Require(prefab != null && prefab.GetComponent<PitTileArtwork>() != null, "Pit prefab lacks contour renderer.");
            for (int i = 0; i < Shapes.Length; i++)
            {
                GameObject group = BuildShape(Shapes[i], Vector2.zero);
                try
                {
                    PitTileArtwork.RebuildAllNow();
                    var arts = group.GetComponentsInChildren<PitTileArtwork>();
                    Require(arts.Length == Shapes[i].Sum(row => row.Count(c => c == '#')), "Wrong pit cell count.");
                    Sprite first = arts[0].GeneratedSprite;
                    Color32[] firstPixels = first.texture.GetPixels32();
                    foreach (PitTileArtwork art in arts)
                    {
                        Require(art.GeneratedSprite == first, "Cell seams: one footprint must share one continuous sprite.");
                        Require(art.GetComponent<BoxCollider2D>().size == Vector2.one && art.transform.localScale == Vector3.one,
                            "Artwork changed collision size or root scale.");
                        Require(art.GetComponent<RoomPit>().TryValidate(out string error), error);
                    }
                    Require(group.GetComponentsInChildren<SpriteRenderer>().Count(r => r.enabled) == 1,
                        "Legacy art or duplicate generated drawing remains visible.");
                    if (i == 3) Require(first.texture.GetPixel(first.texture.width / 2, first.texture.height / 2).a < 0.01f,
                        "The ring's walkable island must remain transparent.");
                    if (i == 9)
                    {
                        Color32[] middle = firstPixels.Where((p, index) => index % first.texture.width > first.texture.width / 3 &&
                            index % first.texture.width < first.texture.width * 2 / 3 && index / first.texture.width > first.texture.height / 3 &&
                            index / first.texture.width < first.texture.height * 2 / 3).ToArray();
                        Require(middle.Select(p => (p.r, p.g, p.b)).Distinct().Count() > 100,
                            "Large pit center must contain varied visible cave artwork, not a black tile.");
                        Require(middle.Average(p => (p.r + p.g + p.b) / 3f) > 25f, "Large pit center is too dark.");
                    }
                    PitTileArtwork.RebuildAllNow();
                    Require(arts[0].GeneratedSprite.texture.GetPixels32().SequenceEqual(firstPixels), "Rebuild changed the art.");
                    Require(group.GetComponentsInChildren<SpriteRenderer>().Count(r => r.enabled) == 1, "Rebuild duplicated the drawing.");
                }
                finally { Object.DestroyImmediate(group); }
            }
            var cells = new List<Rect>();
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) cells.Add(new Rect(x, y, 1, 1));
            CompareSubdivisions(cells, new[] { new Rect(0, 0, 3, 3) });
            CompareSubdivisions(new[] { new Rect(0, 0, 1, 1), new Rect(1, 0, 1, 1), new Rect(1, 1, 1, 1) },
                new[] { new Rect(0, 0, 2, 1), new Rect(1, 1, 1, 1) });
            Require(PitContourRasterizer.Contours(new[] { new Rect(0, 0, 1, 1), new Rect(1, 1, 1, 1) }).Count == 2,
                "Diagonal-only contact must preserve two independent contours.");
            Debug.Log("Continuous pit contour verification passed: 10 shapes including lieul and filled 3x3; " +
                "pixel-identical rectangle subdivisions; textured large center; transparent island; one sprite, stable rebuild and unchanged colliders.");
        }

        private static void CompareSubdivisions(IReadOnlyList<Rect> cells, IReadOnlyList<Rect> merged)
        {
            var a = PitContourRasterizer.Bake(cells, PitTileArtwork.Ribbon, PitTileArtwork.Depth);
            var b = PitContourRasterizer.Bake(merged, PitTileArtwork.Ribbon, PitTileArtwork.Depth);
            try
            {
                Require(a.Texture.width == b.Texture.width && a.Texture.height == b.Texture.height &&
                    a.Texture.GetPixels32().SequenceEqual(b.Texture.GetPixels32()), "Cell division introduced a visible seam.");
            }
            finally { Object.DestroyImmediate(a.Texture); Object.DestroyImmediate(b.Texture); }
        }

        private static GameObject BuildShape(string[] shape, Vector2 position)
        {
            GameObject group = new("Pit shape"); group.transform.position = position;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week22Terrain0Setup.PitPrefabPath);
            for (int y = 0; y < shape.Length; y++) for (int x = 0; x < shape[y].Length; x++)
            {
                if (shape[y][x] != '#') continue;
                GameObject cell = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
                cell.name = $"Pit {x},{y}";
                cell.transform.localPosition = new Vector3(x - (shape[0].Length - 1) * 0.5f, (shape.Length - 1) * 0.5f - y, 0);
                cell.GetComponent<BoxCollider2D>().size = Vector2.one;
                cell.GetComponent<RoomPit>().Configure($"pit-c{x}-r{y}");
            }
            return group;
        }
        private static void Label(string name, Vector2 position)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Require(font != null, "Missing project TMP font.");
            TextMeshPro text = new GameObject("Label " + name).AddComponent<TextMeshPro>();
            text.font = font; text.text = name; text.fontSize = 6; text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.18f, 0.14f, 0.08f); text.rectTransform.sizeDelta = new Vector2(6, 1);
            text.transform.position = new Vector3(position.x, position.y + 3.15f, -1);
        }
        private static Camera Camera(float size)
        {
            Camera camera = new GameObject("Pit preview camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = size;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.72f, 0.73f, 0.50f);
            return camera;
        }
        [MenuItem("Trickal Fan Game/Artwork/Open Connected Pit Tile Preview")]
        public static void OpenPreview()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera(7.6f);
            for (int i = 0; i < Shapes.Length; i++)
            {
                Vector2 position = new(-13.6f + i % 5 * 6.8f, i < 5 ? 3.5f : -3.5f);
                BuildShape(Shapes[i], position).name = "Pit " + Names[i]; Label(Names[i], position);
            }
            PitTileArtwork.RebuildAllNow(); EditorSceneManager.SaveScene(scene, PreviewPath);
            Debug.Log("Continuous pit preview saved: " + PreviewPath);
        }
        [MenuItem("Trickal Fan Game/Artwork/Open Pit Lieul and 3x3 Review")]
        public static void OpenFocus()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera(4f);
            BuildShape(Shapes[4], new Vector2(-3.4f, 0)).name = "Pit ㄹ"; Label("ㄹ", new Vector2(-3.4f, 0));
            BuildShape(Shapes[9], new Vector2(3.4f, 0)).name = "Pit 3×3"; Label("3×3", new Vector2(3.4f, 0));
            PitTileArtwork.RebuildAllNow(); EditorSceneManager.SaveScene(scene, FocusPath);
        }
        private static void Render(string file, int width, int height)
        {
            Camera camera = Object.FindFirstObjectByType<Camera>();
            RenderTexture target = new(width, height, 24); RenderTexture previous = RenderTexture.active;
            Texture2D image = new(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(ArtDirectory, file), image.EncodeToPNG());
                Debug.Log("Continuous pit Unity render saved: " + file);
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
        public static void ApplyAndVerifyBatch()
        {
            Directory.CreateDirectory(ArtDirectory);
            Verify(); OpenPreview(); Render("pit-unity-preview-v2.png", 2000, 900);
            for (int i = 0; i < Names.Length; i++)
            {
                var art = GameObject.Find("Pit " + Names[i]).GetComponentInChildren<PitTileArtwork>();
                File.WriteAllBytes(Path.Combine(ArtDirectory, "pit-" + FileNames[i] + "-v2.png"), art.GeneratedSprite.texture.EncodeToPNG());
            }
            OpenFocus(); Render("pit-lieul-3x3-v2.png", 1600, 900);
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
