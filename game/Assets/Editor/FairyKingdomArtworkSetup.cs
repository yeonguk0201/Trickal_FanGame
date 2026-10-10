using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class FairyKingdomArtworkSetup
    {
        public const string ArtRoot = "Assets/Art/Drafts/FairyKingdom";
        public const string CatalogPath = "Assets/Resources/FairyKingdomArtwork/Catalog.asset";
        public const string DisplayRoot = "Assets/Prefabs/FairyKingdomArtwork";
        public const string PreviewScenePath = "Assets/Scenes/FairyKingdomArtworkPreview.unity";
        // Charging and ranged/sniper fairies keep their existing idle, walk and attack art. Variants inherit a
        // binding placed on RangedEnemy, so the base must stay unbound too.
        private static readonly Dictionary<string, string> EnemyIds = new()
        {
            ["JyubiEnemy"] = "jyubi",
            ["BuseureogiCrumbMinion"] = "crumb-minion-pink",
        };

        [MenuItem("Trickal Fan Game/Artwork/Apply Fairy Kingdom 45 Sprites")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before artwork setup.");
            if (!Application.isBatchMode && SceneManager.GetActiveScene().path.Length == 0 &&
                SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the untitled scene before artwork setup.");
            AssetDatabase.Refresh();
            FairyKingdomArtworkCatalog catalog = BuildCatalog();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Rooms" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(DisplayRoot + "/", StringComparison.Ordinal)) continue;
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (ApplyHierarchy(contents, catalog))
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            // Preserve the active game's authored state; only add/update presentation components and renderers.
            Scene game = SceneManager.GetSceneByPath(Week13FrontendSetup.GameScenePath);
            bool opened = !game.IsValid() || !game.isLoaded;
            if (opened) game = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Additive);
            try
            {
                bool changed = false;
                foreach (GameObject root in game.GetRootGameObjects()) changed |= ApplyHierarchy(root, catalog);
                if (changed) EditorSceneManager.SaveScene(game);
            }
            finally { if (opened) EditorSceneManager.CloseScene(game, true); }
            BuildDisplayPrefabs(catalog);
            BuildPreview(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Fairy Kingdom artwork applied: 45 catalog entries and display prefabs; existing gameplay " +
                "prefabs/rooms connected. Traps and Nururing are artwork previews only; no new gameplay rules.");
        }

        public static void ApplyAndVerifyBatch()
        {
            Apply();
            FairyKingdomArtworkVerification.Verify();
        }

        private static FairyKingdomArtworkCatalog BuildCatalog()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            var entries = new List<FairyKingdomArtworkCatalog.Entry>();
            foreach (string path in Directory.GetFiles(ArtRoot, "*.png", SearchOption.AllDirectories)
                         .Select(value => value.Replace('\\', '/')).OrderBy(value => value, StringComparer.Ordinal))
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing importer: " + path);
                if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                    importer.mipmapEnabled || !importer.alphaIsTransparency)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new InvalidOperationException("Missing sprite: " + path);
                entries.Add(new FairyKingdomArtworkCatalog.Entry
                {
                    id = Path.GetFileNameWithoutExtension(path), sprite = sprite, visibleRect = VisibleRect(path, sprite),
                });
            }
            if (entries.Count != 45) throw new InvalidOperationException("Expected exactly 45 selected PNGs.");
            FairyKingdomArtworkCatalog catalog = AssetDatabase.LoadAssetAtPath<FairyKingdomArtworkCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<FairyKingdomArtworkCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            Undo.RecordObject(catalog, "Configure fairy kingdom artwork");
            catalog.Configure(entries.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static Rect VisibleRect(string path, Sprite imported)
        {
            Texture2D texture = new(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException(path);
                Color32[] pixels = texture.GetPixels32();
                int left = texture.width, right = -1, bottom = texture.height, top = -1;
                for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    // Ignore near-transparent padding/soft halos when fitting artwork, without editing source PNGs.
                    if (pixels[y * texture.width + x].a < 96) continue;
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                }
                if (right < left) throw new InvalidOperationException("Empty artwork: " + path);
                left = Math.Max(0, left - 2); bottom = Math.Max(0, bottom - 2);
                right = Math.Min(texture.width - 1, right + 2); top = Math.Min(texture.height - 1, top + 2);
                float sx = imported.texture.width / (float)texture.width;
                float sy = imported.texture.height / (float)texture.height;
                return Rect.MinMaxRect(left * sx, bottom * sy, (right + 1) * sx, (top + 1) * sy);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static bool ApplyHierarchy(GameObject root, FairyKingdomArtworkCatalog catalog)
        {
            bool changed = false;
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject owner = node.gameObject;
                string id = null;
                FairyKingdomArtworkMode mode = FairyKingdomArtworkMode.Fixed;
                SpriteRenderer renderer = owner.GetComponent<SpriteRenderer>();
                string baseName = owner.name.Replace("(Clone)", "").Trim();
                if (owner.GetComponent<DestructibleObstacle>() is DestructibleObstacle obstacle)
                {
                    id = "obstacle-" + obstacle.VariantId; mode = FairyKingdomArtworkMode.Obstacle;
                    renderer = owner.GetComponentInChildren<SpriteRenderer>(true);
                }
                else if (owner.GetComponent<TreasureChest>() != null)
                { id = "chest-normal-closed"; mode = FairyKingdomArtworkMode.Chest; }
                else if (owner.GetComponent<PlacedBomb>() != null)
                { id = "pickup-bomb"; mode = FairyKingdomArtworkMode.Bomb; }
                else if (owner.GetComponent<SecretPit>() != null || owner.GetComponent<RoomPit>() != null)
                {
                    if (owner.GetComponent<RoomPit>() != null && owner.GetComponent<PitTileArtwork>() == null)
                        Undo.AddComponent<PitTileArtwork>(owner);
                    id = owner.GetComponent<SecretPit>() != null ? "terrain-secret-pit" : "terrain-pit";
                    renderer = owner.GetComponentInChildren<SpriteRenderer>(true);
                    foreach (SpriteRenderer extra in owner.GetComponentsInChildren<SpriteRenderer>(true))
                        if (extra != renderer) { Undo.RecordObject(extra, "Hide placeholder pit layer"); extra.enabled = false; }
                }
                else if (owner.GetComponent<RoomStaticObstacle>() != null) id = "terrain-pillar";
                else if (EnemyIds.TryGetValue(baseName, out string enemyId))
                {
                    id = enemyId;
                    if (id == "crumb-minion-pink") mode = FairyKingdomArtworkMode.Crumb;
                    EnemyMovementAnimator movement = owner.GetComponent<EnemyMovementAnimator>();
                    EnemyAttackArtwork attacks = owner.GetComponent<EnemyAttackArtwork>();
                    if (movement != null) { Undo.RecordObject(movement, "Defer drawn movement frames"); movement.enabled = false; }
                    if (attacks != null) { Undo.RecordObject(attacks, "Defer drawn attack frames"); attacks.enabled = false; }
                }
                else if (baseName == "KeyPickup") id = "pickup-key";
                else if (baseName == "BombPickup") id = "pickup-bomb";
                if (id == null || renderer == null) continue;
                FairyKingdomArtworkCatalog.Entry entry = catalog.Entries.FirstOrDefault(value => value.id == id);
                if (entry == null) continue; // Boss-owned dough/cream obstacles have their own existing art.
                FairyKingdomArtworkView view = owner.GetComponent<FairyKingdomArtworkView>();
                float width = view != null ? view.LocalWidth : renderer.sprite != null ? renderer.sprite.bounds.size.x : 1f;
                if (id == "obstacle-tree") width = 1f;
                if (view == null) view = Undo.AddComponent<FairyKingdomArtworkView>(owner);
                Undo.RecordObject(view, "Bind fairy kingdom artwork");
                Undo.RecordObject(renderer, "Apply fairy kingdom sprite");
                view.Configure(id, mode, renderer, width);
                renderer.sprite = entry.sprite;
                renderer.color = Color.white;
                if (id == "obstacle-tree") FairyKingdomArtworkView.ConfigureTreeVisual(renderer);
                if (owner.GetComponent<PitTileArtwork>() is PitTileArtwork pitTiles) pitTiles.Rebuild();
                EditorUtility.SetDirty(view);
                changed = true;
            }
            return changed;
        }

        private static void BuildDisplayPrefabs(FairyKingdomArtworkCatalog catalog)
        {
            Directory.CreateDirectory(DisplayRoot);
            const string materialPath = "Assets/Resources/FairyKingdomArtwork/PreviewUnlit.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException("Missing unlit sprite shader.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            foreach (FairyKingdomArtworkCatalog.Entry entry in catalog.Entries)
            {
                string path = DisplayRoot + "/" + entry.id + ".prefab";
                GameObject owner = File.Exists(path) ? PrefabUtility.LoadPrefabContents(path) : new GameObject(entry.id);
                bool loaded = File.Exists(path);
                try
                {
                    SpriteRenderer renderer = owner.GetComponent<SpriteRenderer>();
                    if (renderer == null) renderer = owner.AddComponent<SpriteRenderer>();
                    renderer.sprite = entry.sprite;
                    renderer.color = Color.white;
                    renderer.sharedMaterial = material;
                    renderer.sortingOrder = entry.id.StartsWith("terrain-") ? -2 : 2;
                    FairyKingdomArtworkView view = owner.GetComponent<FairyKingdomArtworkView>();
                    if (view == null) view = owner.AddComponent<FairyKingdomArtworkView>();
                    view.Configure(entry.id, FairyKingdomArtworkMode.Fixed, renderer, 1f);
                    // Authored display prefabs have no colliders, damage or enemy AI.
                    PrefabUtility.SaveAsPrefabAsset(owner, path);
                }
                finally { if (loaded) PrefabUtility.UnloadPrefabContents(owner); else Object.DestroyImmediate(owner); }
            }
        }

        private static void BuildPreview(FairyKingdomArtworkCatalog catalog)
        {
            Scene previous = SceneManager.GetActiveScene();
            // A batch Editor starts with an untitled scene. Unity forbids NewScene(Additive) in that state.
            if (previous.path.Length == 0)
            {
                previous = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            }
            Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(preview);
                GameObject cameraObject = new("Artwork Preview Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.cullingMask = 1 << 31;
                camera.orthographic = true;
                camera.orthographicSize = 7.7f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.43f, 0.58f, 0.36f);
                camera.transform.position = new Vector3(10.4f, -5.2f, -10f);
                for (int index = 0; index < catalog.Entries.Count; index++)
                {
                    var entry = catalog.Entries[index];
                    GameObject owner = (GameObject)PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(DisplayRoot + "/" + entry.id + ".prefab"), preview);
                    owner.transform.position = new Vector3(index % 9 * 2.6f, -(index / 9) * 2.6f, 0f);
                    owner.layer = 31; // Isolate this additive art review from objects in the user's game scene.
                    SpriteRenderer renderer = owner.GetComponent<SpriteRenderer>();
                    float size = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
                    owner.transform.localScale = Vector3.one * (2f / size);
                }
                EditorSceneManager.SaveScene(preview, PreviewScenePath);
                RenderPreview(camera);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(preview, true);
            }
        }

        private static void RenderPreview(Camera camera)
        {
            RenderTexture target = new(1440, 900, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new(1440, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
                image.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/asset-integration"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "fairy-kingdom-unity-preview.png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
