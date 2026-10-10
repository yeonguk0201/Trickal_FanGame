using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Preserve serialized component/GUID. Quarter tiles are superseded by a continuous contour drawing.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RoomPit))]
    [DefaultExecutionOrder(200)]
    public sealed class PitTileArtwork : MonoBehaviour
    {
        public const string RibbonPath = "PitTiles/BoundaryRibbon";
        public const string DepthPath = "PitTiles/CavernDepth";
        private static Texture2D ribbon, depth;
        private static bool dirty = true, rebuilding;
        private Transform drawing;
        private Sprite generatedSprite;
        private Texture2D generatedTexture;
        private PitTileArtwork owner;
        private Rect generatedBounds;
        private int contourCount;
        private Vector3 lastPosition, lastScale;
        private Quaternion lastRotation;
        private Vector2 lastSize, lastOffset;
        private Transform lastParent;
        private bool lastEnabled;
        private readonly Dictionary<SpriteRenderer, bool> hidden = new();
        public static Texture2D Ribbon => ribbon != null ? ribbon : ribbon = Resources.Load<Texture2D>(RibbonPath);
        public static Texture2D Depth => depth != null ? depth : depth = Resources.Load<Texture2D>(DepthPath);
        public bool HasArtwork => Ribbon != null && Depth != null;
        public Sprite GeneratedSprite => owner != null ? owner.generatedSprite : null;
        public Rect GeneratedBounds => owner != null ? owner.generatedBounds : default;
        public int ContourCount => owner != null ? owner.contourCount : 0;

        private void OnEnable() => dirty = true;
        private void OnDisable()
        {
            Clear(true);
            foreach (var pair in hidden) if (pair.Key != null) pair.Key.enabled = pair.Value;
            hidden.Clear(); dirty = true;
        }
        private void LateUpdate()
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box == null || !gameObject.scene.IsValid()) return;
            if (lastPosition != transform.localPosition || lastScale != transform.localScale ||
                lastRotation != transform.localRotation || lastSize != box.size || lastOffset != box.offset ||
                lastParent != transform.parent || lastEnabled != box.enabled)
            {
                lastPosition = transform.localPosition; lastScale = transform.localScale; lastRotation = transform.localRotation;
                lastSize = box.size; lastOffset = box.offset; lastParent = transform.parent; lastEnabled = box.enabled;
                dirty = true;
            }
            if (dirty) RebuildAllNow();
        }

        // Setup may request many rebuilds. Bake after the hierarchy is complete.
        public void Rebuild() => dirty = true;

        public static void RebuildAllNow()
        {
            if (rebuilding || Ribbon == null || Depth == null) return;
            rebuilding = true;
            try
            {
                PitTileArtwork[] all = FindObjectsByType<PitTileArtwork>(FindObjectsSortMode.None)
                    .Where(a => a.isActiveAndEnabled && a.gameObject.scene.IsValid()).ToArray();
                foreach (PitTileArtwork art in all) art.Clear();
                var groups = all.Where(a => a.GetComponent<BoxCollider2D>().enabled && a.GetComponent<RoomPit>().isActiveAndEnabled)
                    .GroupBy(a => a.transform.parent != null ? a.transform.parent : a.transform);
                foreach (var group in groups)
                {
                    PitTileArtwork[] members = group.OrderBy(a => a.GetInstanceID()).ToArray();
                    PitTileArtwork leader = members[0];
                    Transform space = group.Key;
                    List<Rect> rectangles = members.Select(a => Footprint(a.GetComponent<BoxCollider2D>(), space)).ToList();
                    PitContourRasterizer.Result result = PitContourRasterizer.Bake(rectangles, Ribbon, Depth);
                    leader.generatedTexture = result.Texture; leader.generatedBounds = result.Bounds;
                    leader.contourCount = result.ContourCount;
                    leader.generatedSprite = Sprite.Create(result.Texture, new Rect(0, 0, result.Texture.width,
                        result.Texture.height), new Vector2(0.5f, 0.5f), result.Ppu, 0, SpriteMeshType.FullRect);
                    leader.generatedSprite.name = "Connected pit contour";
                    leader.generatedSprite.hideFlags = HideFlags.HideAndDontSave;
                    Material material = null;
                    int sortingLayer = 0, sortingOrder = -10;
                    foreach (PitTileArtwork art in members)
                    {
                        art.owner = leader;
                        foreach (SpriteRenderer legacy in art.GetComponentsInChildren<SpriteRenderer>(true))
                        {
                            if (legacy.gameObject.hideFlags.HasFlag(HideFlags.DontSaveInEditor)) continue;
                            if (!art.hidden.ContainsKey(legacy)) art.hidden.Add(legacy, legacy.enabled);
                            if (material == null) { material = legacy.sharedMaterial; sortingLayer = legacy.sortingLayerID; }
                            sortingOrder = Mathf.Min(sortingOrder, legacy.sortingOrder); legacy.enabled = false;
                        }
                    }
                    GameObject child = new("Connected pit contour") { hideFlags = HideFlags.HideAndDontSave };
                    leader.drawing = child.transform; leader.drawing.SetParent(space, false);
                    leader.drawing.localPosition = result.Bounds.min + new Vector2(result.Texture.width, result.Texture.height) / (2f * result.Ppu);
                    SpriteRenderer renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = leader.generatedSprite;
                    if (material != null) renderer.sharedMaterial = material;
                    renderer.sortingLayerID = sortingLayer; renderer.sortingOrder = sortingOrder;
                }
                foreach (PitTileArtwork art in all)
                {
                    BoxCollider2D box = art.GetComponent<BoxCollider2D>();
                    art.lastPosition = art.transform.localPosition; art.lastScale = art.transform.localScale;
                    art.lastRotation = art.transform.localRotation; art.lastParent = art.transform.parent;
                    art.lastSize = box.size; art.lastOffset = box.offset; art.lastEnabled = box.enabled;
                }
                dirty = false;
            }
            finally { rebuilding = false; }
        }

        public static Rect Footprint(BoxCollider2D box, Transform space)
        {
            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector2 sign in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) })
            {
                Vector2 point = space.InverseTransformPoint(box.transform.TransformPoint(box.offset + box.size * sign * 0.5f));
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static void Release(Object value, bool defer = false)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else
            {
#if UNITY_EDITOR
                if (defer && value is GameObject)
                {
                    UnityEditor.EditorApplication.delayCall += () => { if (value != null) DestroyImmediate(value); };
                    return;
                }
#endif
                DestroyImmediate(value);
            }
        }
        private void Clear(bool defer = false)
        {
            if (drawing != null) drawing.gameObject.SetActive(false);
            Release(drawing != null ? drawing.gameObject : null, defer); Release(generatedSprite); Release(generatedTexture);
            drawing = null; generatedSprite = null; generatedTexture = null; owner = null;
        }
    }
}
