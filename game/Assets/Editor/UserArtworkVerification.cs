using System;
using System.Linq;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Editor
{
    public static class UserArtworkVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify User Artwork")]
        public static void Verify()
        {
            string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/UserArtwork" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Require(paths.Length == 42, "Expected all 42 supplied PNGs.");
            foreach (string path in paths)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Require(sprite != null, "Missing Sprite import: " + path);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Require(!importer.mipmapEnabled && importer.alphaIsTransparency &&
                    importer.spriteImportMode == SpriteImportMode.Single,
                    "Artwork must use single sprites, transparent alpha, and no mipmaps: " + path);
            }
            Shader shader = Resources.Load<Shader>("UserArtwork/ItemArtwork");
            Require(shader != null && !ShaderUtil.ShaderHasError(shader), "Artwork shader failed compilation.");
            for (int i = 0; i < 4; i++)
            {
                Sprite icon = UserArtwork.HudIcon(i);
                Sprite sheet = UserArtwork.Load("hud-icons");
                Require(icon != null && icon.rect.width == sheet.texture.width / 2f &&
                    icon.rect.height == sheet.texture.height / 2f,
                    "HUD icon must be one of four equal quadrants of the supplied sheet.");
            }
            GameObject root = new("Artwork verification", typeof(RectTransform), typeof(Image));
            try
            {
                Image image = root.GetComponent<Image>();
                int linked = 0;
                foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" }))
                {
                    ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (UserArtwork.Load(item.ItemId) == null) continue;
                    Require(UserArtwork.Apply(image, item) && image.color == Color.white &&
                        image.material.GetFloat("_Card") == (item.Kind == ItemKind.Artifact ? 0f : 1f),
                        "Artwork must retain its colors and the correct frame style: " + item.ItemId);
                    linked++;
                }
                Require(linked == 30, "Expected 30 existing item definitions with supplied artwork; got " + linked);
                Require(!UserArtwork.Apply(image, (ItemDefinition)null) && image.sprite == null && image.material == Graphic.defaultGraphicMaterial,
                    "An empty slot must clear the previous artwork and material.");
                foreach (string name in new[] { "HealthPickup", "SPPickup" })
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
                    SpriteRenderer display = prefab.GetComponentInChildren<SpriteRenderer>();
                    string key = name == "HealthPickup" ? "hp-pickup" : "sp-pickup";
                    Require(display.sprite == UserArtwork.Load(key) && display.color == Color.white,
                        name + " must use the supplied capsule, including in the shop.");
                }
                Debug.Log("User artwork verification passed: 42 PNGs, 30 item illustrations, four HUD icons, capsule prefabs and frame shader.");
                CapturePreview();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void CapturePreview()
        {
            GameObject cameraObject = new("Artwork preview camera", typeof(Camera));
            GameObject canvasObject = new("Artwork preview canvas", typeof(RectTransform), typeof(Canvas));
            RenderTexture target = new(1600, 1000, 24);
            Texture2D capture = new(1600, 1000, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 500;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.08f, 0.14f);
                camera.targetTexture = target;
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(1600, 1000);
                Image background = PreviewImage(canvas.transform, "Home", Vector2.zero, new Vector2(1600,1000));
                background.sprite = UserArtwork.Load("home-background");
                background.color = new Color(0.6f,0.6f,0.6f,1);
                string[] keys = { "single-spell-random-coin", "single-spell-afterimage", "single-spell-decisive-strike",
                    "single-spell-aroma-therapy", "single-spell-membership-card", "single-spell-meditation-time",
                    "artifact-levi-dagger", "artifact-greed-ring", "artifact-sylla-wind-arrow",
                    "item-02", "item-13", "artifact-explosive-muffin" };
                for (int i = 0; i < keys.Length; i++)
                {
                    Image image = PreviewImage(canvas.transform, keys[i],
                        new Vector2(-610 + i % 6 * 244, i < 6 ? 335 : 85), new Vector2(204,204));
                    image.sprite = UserArtwork.Load(keys[i]);
                    image.preserveAspect = true;
                    image.material = UserArtwork.ItemMaterial(i < 6);
                }
                for (int i = 0; i < 4; i++)
                    UserArtwork.Apply(PreviewImage(canvas.transform,"HUD " + i,
                        new Vector2(-450 + i*220,-205),new Vector2(150,150)),UserArtwork.HudIcon(i));
                UserArtwork.Apply(PreviewImage(canvas.transform,"Sist",new Vector2(630,-235),new Vector2(240,320)),UserArtwork.Load("sist"));
                UserArtwork.Apply(PreviewImage(canvas.transform,"HP pickup",new Vector2(-310,-390),new Vector2(100,100)),UserArtwork.Load("hp-pickup"));
                UserArtwork.Apply(PreviewImage(canvas.transform,"SP pickup",new Vector2(-100,-390),new Vector2(100,100)),UserArtwork.Load("sp-pickup"));
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0,0,1600,1000),0,0);
                capture.Apply();
                System.IO.File.WriteAllBytes(Application.dataPath + "/../Logs/user-artwork-preview.png",capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(capture);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static Image PreviewImage(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = child.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
