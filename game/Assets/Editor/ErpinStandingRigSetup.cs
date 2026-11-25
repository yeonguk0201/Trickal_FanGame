using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Character;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    /// <summary>
    /// Builds the Erpin standing 2D Animation test rig from the exported PSD parts:
    /// sprite import settings, a grid mesh with distance weights per part, one shared skeleton,
    /// the prefab and a preview scene. Re-running rewrites the same assets in place.
    /// </summary>
    public static class ErpinStandingRigSetup
    {
        public const string ArtRoot = "Assets/Art/Characters/Player/Standing";
        public const string LayersPath = ArtRoot + "/Erpin_Standing_Layers.json";
        public const string RigPath = ArtRoot + "/Erpin_Standing_Rig.json";
        public const string PrefabPath = "Assets/Prefabs/Standing/ErpinStanding.prefab";
        public const string PreviewScenePath = "Assets/Scenes/ErpinStandingPreview.unity";
        public const int MeshCellSize = 24;
        private const byte MeshAlphaThreshold = 8;
        private const float WeightFalloffPixels = 24f;
        private const float WeightPower = 4f;

        [Serializable] public sealed class LayerManifest { public string source; public int[] canvas; public LayerEntry[] layers; }
        [Serializable] public sealed class LayerEntry { public string name; public string file; public int order; public int[] bbox; }
        [Serializable] public sealed class RigManifest { public float pixelsPerUnit; public float[] origin; public BoneEntry[] bones; public RigLayer[] layers; }
        [Serializable] public sealed class BoneEntry { public string name; public string parent; public float[] start; public float[] end; }
        [Serializable] public sealed class RigLayer { public string name; public string[] bones; }

        [MenuItem("Trickal Fan Game/Artwork/Setup Erpin Standing Rig")]
        public static void Setup()
        {
            LayerManifest layers = LoadLayers();
            RigManifest rig = LoadRig();
            foreach (LayerEntry layer in layers.layers.OrderBy(l => l.order))
                ImportPart(layer, rig);
            AssetDatabase.SaveAssets();
            BuildPrefab(layers, rig);
            BuildPreviewScene();
            LinkCharacter();
            Debug.Log($"ErpinStandingRigSetup: rigged {layers.layers.Length} parts into {PrefabPath} and {PreviewScenePath}, linked to {Menu3CharacterSetup.ErpinPath}.");
        }

        public const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        /// <summary>Points Erpin's character definition at the rig so menu portraits use it.</summary>
        private static void LinkCharacter()
        {
            var erpin = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Menu3CharacterSetup.ErpinPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (erpin == null || prefab == null) throw new InvalidOperationException("Erpin definition or standing prefab is missing.");
            var data = new SerializedObject(erpin);
            SerializedProperty property = data.FindProperty("standingRig");
            if (property.objectReferenceValue == prefab) return;
            Undo.RecordObject(erpin, "Link Erpin standing rig");
            property.objectReferenceValue = prefab;
            data.ApplyModifiedProperties();
            EditorUtility.SetDirty(erpin);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Motion values already tuned on the saved prefab, keyed by bone name, so a rebuild keeps them.</summary>
        private sealed class TunedMotion
        {
            public float breathHeight = 0.012f, breathPeriod = 3.2f, intensity = 1f;
            public readonly Dictionary<string, StandingIdleMotion.BoneSway> sways = new();
        }

        private static TunedMotion ReadTunedMotion()
        {
            var tuned = new TunedMotion();
            var motion = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<StandingIdleMotion>();
            if (motion == null) return tuned;
            tuned.breathHeight = motion.BreathHeight;
            tuned.breathPeriod = motion.BreathPeriod;
            tuned.intensity = motion.Intensity;
            for (int i = 0; i < motion.SwayCount; i++)
            {
                StandingIdleMotion.BoneSway sway = motion.GetSway(i);
                if (sway.bone != null) tuned.sways[sway.bone.name] = sway;
            }
            return tuned;
        }

        public static void SetupBatch()
        {
            try
            {
                Setup();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static LayerManifest LoadLayers()
        {
            var manifest = JsonUtility.FromJson<LayerManifest>(File.ReadAllText(LayersPath, System.Text.Encoding.UTF8));
            if (manifest?.layers == null || manifest.layers.Length == 0) throw new InvalidOperationException($"{LayersPath} lists no layers.");
            return manifest;
        }

        public static RigManifest LoadRig()
        {
            var rig = JsonUtility.FromJson<RigManifest>(File.ReadAllText(RigPath, System.Text.Encoding.UTF8));
            if (rig?.bones == null || rig.bones.Length == 0 || rig.layers == null) throw new InvalidOperationException($"{RigPath} has no skeleton.");
            if (rig.pixelsPerUnit <= 0f || rig.origin == null || rig.origin.Length != 2) throw new InvalidOperationException($"{RigPath} needs pixelsPerUnit and origin.");
            return rig;
        }

        public static string PartPath(LayerEntry layer) => ArtRoot + "/" + layer.file;

        public static RigLayer RigFor(RigManifest rig, LayerEntry layer) =>
            rig.layers.FirstOrDefault(l => l.name == layer.name)
            ?? throw new InvalidOperationException($"{RigPath} assigns no bones to layer '{layer.name}'.");

        public static BoneEntry Bone(RigManifest rig, string name) =>
            rig.bones.FirstOrDefault(b => b.name == name)
            ?? throw new InvalidOperationException($"{RigPath} has no bone named '{name}'.");

        /// <summary>PSD pixel (y down) to prefab-local units with the rig origin at (0, 0).</summary>
        public static Vector3 ToUnits(RigManifest rig, float x, float y) =>
            new Vector3((x - rig.origin[0]) / rig.pixelsPerUnit, (rig.origin[1] - y) / rig.pixelsPerUnit, 0f);

        private static float BoneAngle(BoneEntry bone) =>
            Mathf.Atan2(-(bone.end[1] - bone.start[1]), bone.end[0] - bone.start[0]) * Mathf.Rad2Deg;

        private static void ImportPart(LayerEntry layer, RigManifest rig)
        {
            string path = PartPath(layer);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException($"Missing part texture {path}. Run tools/artwork/export_erpin_standing_parts.py first.");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = rig.pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
            settings.spritePivot = Vector2.zero;
            settings.spriteMeshType = SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect rect = provider.GetSpriteRects().Single();
            GUID id = rect.spriteID;

            int x0 = layer.bbox[0], y1 = layer.bbox[3];
            Vector2 ToPart(float x, float y) => new Vector2(x - x0, y1 - y);
            string[] boneNames = RigFor(rig, layer).bones;

            var spriteBones = new List<SpriteBone>(boneNames.Length);
            var segments = new List<(Vector2 a, Vector2 b)>(boneNames.Length);
            foreach (string name in boneNames)
            {
                BoneEntry bone = Bone(rig, name);
                Vector2 start = ToPart(bone.start[0], bone.start[1]);
                Vector2 end = ToPart(bone.end[0], bone.end[1]);
                segments.Add((start, end));
                // Every bone is stored as a root in sprite space; the shared skeleton in the prefab carries the hierarchy.
                spriteBones.Add(new SpriteBone
                {
                    name = name,
                    guid = name,
                    position = start,
                    rotation = Quaternion.Euler(0f, 0f, BoneAngle(bone)),
                    length = Vector2.Distance(start, end),
                    parentId = -1,
                    color = Color.white
                });
            }

            BuildGridMesh(path, segments, out Vertex2DMetaData[] vertices, out int[] indices, out Vector2Int[] edges);
            provider.GetDataProvider<ISpriteBoneDataProvider>().SetBones(id, spriteBones);
            ISpriteMeshDataProvider meshProvider = provider.GetDataProvider<ISpriteMeshDataProvider>();
            meshProvider.SetVertices(id, vertices);
            meshProvider.SetIndices(id, indices);
            meshProvider.SetEdges(id, edges);
            provider.Apply();
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Covers every opaque pixel with grid quads and weights each vertex by inverse distance
        /// to the part's bone segments, keeping the four strongest bones.
        /// </summary>
        private static void BuildGridMesh(string texturePath, List<(Vector2 a, Vector2 b)> bones,
            out Vertex2DMetaData[] vertices, out int[] indices, out Vector2Int[] edges)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(texturePath))) throw new InvalidOperationException($"Cannot read {texturePath}.");
                int width = texture.width, height = texture.height;
                Color32[] pixels = texture.GetPixels32();
                int cols = Mathf.CeilToInt(width / (float)MeshCellSize);
                int rows = Mathf.CeilToInt(height / (float)MeshCellSize);
                var occupied = new bool[cols, rows];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (pixels[y * width + x].a > MeshAlphaThreshold)
                        occupied[x / MeshCellSize, y / MeshCellSize] = true;

                var vertexIndex = new Dictionary<Vector2Int, int>();
                var positions = new List<Vector2>();
                int Corner(int cx, int cy)
                {
                    var key = new Vector2Int(cx, cy);
                    if (vertexIndex.TryGetValue(key, out int index)) return index;
                    index = positions.Count;
                    positions.Add(new Vector2(Mathf.Min(cx * MeshCellSize, width), Mathf.Min(cy * MeshCellSize, height)));
                    vertexIndex.Add(key, index);
                    return index;
                }

                var triangles = new List<int>();
                var edgeCount = new Dictionary<Vector2Int, int>();
                void CountEdge(int a, int b)
                {
                    var key = a < b ? new Vector2Int(a, b) : new Vector2Int(b, a);
                    edgeCount[key] = edgeCount.TryGetValue(key, out int count) ? count + 1 : 1;
                }

                for (int cy = 0; cy < rows; cy++)
                for (int cx = 0; cx < cols; cx++)
                {
                    if (!occupied[cx, cy]) continue;
                    int bl = Corner(cx, cy), br = Corner(cx + 1, cy), tr = Corner(cx + 1, cy + 1), tl = Corner(cx, cy + 1);
                    triangles.AddRange(new[] { bl, tl, tr, bl, tr, br });
                    CountEdge(bl, br); CountEdge(br, tr); CountEdge(tr, tl); CountEdge(tl, bl);
                }

                if (triangles.Count == 0) throw new InvalidOperationException($"{texturePath} has no opaque pixels.");
                indices = triangles.ToArray();
                edges = edgeCount.Where(pair => pair.Value == 1).Select(pair => pair.Key).ToArray();
                vertices = positions.Select(p => new Vertex2DMetaData { position = p, boneWeight = Weigh(p, bones) }).ToArray();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static BoneWeight Weigh(Vector2 point, List<(Vector2 a, Vector2 b)> bones)
        {
            var ranked = bones
                .Select((bone, index) => (index, weight: 1f / Mathf.Pow(DistanceToSegment(point, bone.a, bone.b) + WeightFalloffPixels, WeightPower)))
                .OrderByDescending(entry => entry.weight)
                .Take(4)
                .ToArray();
            float total = ranked.Sum(entry => entry.weight);
            var result = new BoneWeight();
            for (int i = 0; i < ranked.Length; i++)
            {
                float weight = ranked[i].weight / total;
                switch (i)
                {
                    case 0: result.boneIndex0 = ranked[i].index; result.weight0 = weight; break;
                    case 1: result.boneIndex1 = ranked[i].index; result.weight1 = weight; break;
                    case 2: result.boneIndex2 = ranked[i].index; result.weight2 = weight; break;
                    case 3: result.boneIndex3 = ranked[i].index; result.weight3 = weight; break;
                }
            }
            return result;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(point, a + ab * t);
        }

        private static void BuildPrefab(LayerManifest layers, RigManifest rig)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath)!);
            TunedMotion tuned = ReadTunedMotion();
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            if (unlit == null) throw new InvalidOperationException($"Missing {UnlitMaterialPath}; menu lights must not tint the rig.");
            var root = new GameObject("ErpinStanding");
            try
            {
                root.AddComponent<SortingGroup>();
                var bones = new Dictionary<string, Transform>();
                foreach (BoneEntry bone in rig.bones)
                {
                    Transform parent = string.IsNullOrEmpty(bone.parent) ? root.transform
                        : bones.TryGetValue(bone.parent, out Transform found) ? found
                        : throw new InvalidOperationException($"Bone '{bone.name}' is listed before its parent '{bone.parent}'.");
                    var go = new GameObject(bone.name);
                    go.transform.SetParent(parent, false);
                    go.transform.position = root.transform.TransformPoint(ToUnits(rig, bone.start[0], bone.start[1]));
                    go.transform.rotation = root.transform.rotation * Quaternion.Euler(0f, 0f, BoneAngle(bone));
                    bones.Add(bone.name, go.transform);
                }

                var parts = new GameObject("Parts").transform;
                parts.SetParent(root.transform, false);
                foreach (LayerEntry layer in layers.layers.OrderBy(l => l.order))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PartPath(layer));
                    if (sprite == null) throw new InvalidOperationException($"No sprite at {PartPath(layer)}.");
                    var go = new GameObject(layer.name);
                    go.transform.SetParent(parts, false);
                    go.transform.localPosition = ToUnits(rig, layer.bbox[0], layer.bbox[3]);
                    var renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sharedMaterial = unlit;
                    renderer.sortingOrder = layer.order;
                    var skin = go.AddComponent<SpriteSkin>();
                    skin.SetRootBone(bones[rig.bones[0].name]);
                    skin.SetBoneTransforms(RigFor(rig, layer).bones.Select(name => bones[name]).ToArray());
                }

                StandingIdleMotion.BoneSway[] sways = DefaultSways(bones)
                    .Select(sway => tuned.sways.TryGetValue(sway.bone.name, out StandingIdleMotion.BoneSway kept)
                        ? new StandingIdleMotion.BoneSway(sway.bone, kept.amplitude, kept.period, kept.phase) : sway)
                    .ToArray();
                root.AddComponent<StandingIdleMotion>().Configure(bones["chest"], tuned.breathHeight, tuned.breathPeriod, sways, tuned.intensity);

                // The still portrait is the whole PSD canvas, so the frame is that canvas in rig units.
                Vector3 canvasMin = ToUnits(rig, 0f, layers.canvas[1]);
                Vector3 canvasMax = ToUnits(rig, layers.canvas[0], 0f);
                root.AddComponent<StandingRigFrame>().Configure(Rect.MinMaxRect(canvasMin.x, canvasMin.y, canvasMax.x, canvasMax.y));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved) throw new InvalidOperationException($"Could not save {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Starting idle values: hair and cape chains trail their roots, the wing flaps faster.</summary>
        private static StandingIdleMotion.BoneSway[] DefaultSways(Dictionary<string, Transform> bones)
        {
            StandingIdleMotion.BoneSway Sway(string name, float amplitude, float period, float phase) =>
                new StandingIdleMotion.BoneSway(bones[name], amplitude, period, phase);
            return new[]
            {
                Sway("head", 0.6f, 6.4f, 0f),
                Sway("hairL1", 0.8f, 3.6f, 0.0f), Sway("hairL2", 1.4f, 3.6f, 0.6f), Sway("hairL3", 2.2f, 3.6f, 1.2f),
                Sway("hairR1", 0.8f, 3.6f, 0.4f), Sway("hairR2", 1.4f, 3.6f, 1.0f), Sway("hairR3", 2.2f, 3.6f, 1.6f),
                Sway("sideLockL", 2.5f, 2.8f, 0.3f), Sway("sideLockR", 1.8f, 2.8f, 0.9f),
                Sway("capeL", 1.0f, 3.2f, 0.2f), Sway("capeL2", 1.6f, 3.2f, 0.8f),
                Sway("capeR", 1.0f, 3.2f, 0.5f), Sway("capeR2", 1.6f, 3.2f, 1.1f),
                Sway("wing", 6f, 1.8f, 0f),
                Sway("armStaff", 0.8f, 3.2f, 0.4f), Sway("armFree", 1.5f, 3.2f, 0.6f)
            };
        }

        private static void BuildPreviewScene()
        {
            Scene previous = SceneManager.GetActiveScene();
            // Unity cannot add a scene beside an unsaved untitled one, so that case replaces it instead.
            bool untitledOpen = Enumerable.Range(0, SceneManager.sceneCount).Any(i => string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path));
            if (untitledOpen && !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Preview scene not rebuilt: the open untitled scene was kept.");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, untitledOpen ? NewSceneMode.Single : NewSceneMode.Additive);
            if (untitledOpen) previous = default;
            try
            {
                var cameraObject = new GameObject("Preview Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.transform.position = new Vector3(0f, 2.5f, -10f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 2.9f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.93f, 0.9f, 0.86f);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.position = Vector3.zero;

                EditorSceneManager.SaveScene(scene, PreviewScenePath);
            }
            finally
            {
                if (previous.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, true);
                    SceneManager.SetActiveScene(previous);
                }
            }
        }
    }
}
