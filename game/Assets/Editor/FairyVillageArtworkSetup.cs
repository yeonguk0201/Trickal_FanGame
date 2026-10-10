using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class FairyVillageArtworkSetup
    {
        public const string Folder = "Assets/Rooms/Artwork/FairyVillage/";
        public const string RootName = "Fairy Village Tiles";
        public const string BoundaryName = "Fairy Village Wall Boundaries";
        public const string SealBoundaryName = "Fairy Village Seal Boundary";
        public const string ForegroundName = "Fairy Village Foreground";
        public const string ForegroundMaskPath = Folder + "wall-foreground-cutout-v1.png";
        public const string OpenDoorMaskPath = Folder + "door-open-foreground-cutout-v1.png";
        public const string ClosedDoorMaskPath = Folder + "door-closed-foreground-cutout-v1.png";
        public const string ClosedBoundaryName = "Fairy Village Closed Door Boundary";
        public const float MasterScale = 16f / 1572f;
        // The lower corner cuts also hold the foot of the side walls. Only the lower hedge, up to its top edge
        // (source Y=783 in the 221 px tall bottom row), may cover actors; the side wall above it must not.
        public const float CornerForegroundCutoff = (941f - 783f) / 221f;
        public static readonly string[] DoorFamilies = { "shop", "treasure", "boss", "secret" };
        public static string FamilyPath(string family, string state, int version = 2) => Folder + $"room-{family}-{state}-v{version}.png";
        public static int FamilyVersion(string family, RoomDoorDirection direction) => family switch {
            "shop" => 2, "secret" => direction == RoomDoorDirection.Down ? 2 : 1,
            _ => direction == RoomDoorDirection.Left || direction == RoomDoorDirection.Right ? 3 : 1 };
        public static string FamilyMaskPath(string family, string state) =>
            Folder + $"door-{family}-{state}-cutout-v{(family == "shop" || family == "secret" ? 2 : 1)}.png";
        // Common registered cuts preserve corner and door scale; long wall/grass spans resize together.
        private static readonly int[] SourceX = { 50, 220, 690, 982, 1452, 1622 };
        private static readonly int[] SourceY = { 0, 190, 300, 560, 720, 941 };
        private static readonly string[] Walls = { "Top Left Wall", "Top Right Wall", "Bottom Left Wall",
            "Bottom Right Wall", "Left Upper Wall", "Left Lower Wall", "Right Upper Wall", "Right Lower Wall" };

        [MenuItem("Trickal Fan Game/Artwork/Apply Fairy Village Tiles and Walls")]
        public static void Setup()
        {
            ImportForegroundMask(ForegroundMaskPath);
            ImportForegroundMask(OpenDoorMaskPath);
            ImportForegroundMask(ClosedDoorMaskPath);
            Slice("open"); Slice("closed"); Slice("locked"); Slice("sealed");
            foreach (string family in DoorFamilies)
            {
                SliceFamily(family, "open");
                if (family != "shop") SliceFamily(family, "open", 1);
                ImportForegroundMask(FamilyMaskPath(family, "open"));
                if (family == "secret") continue;
                SliceFamily(family, "closed");
                if (family != "shop") SliceFamily(family, "closed", 1);
                ImportForegroundMask(FamilyMaskPath(family, "closed"));
            }
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "ConnectedRoom.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Invalid connected room shader.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "connected-room.mat");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "connected-room.mat"); }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            foreach (string path in PrefabPaths()) Apply(path, material);
            AssetDatabase.SaveAssets();
            Debug.Log("Connected Fairy Village modules applied to all templates. Start a new run to refresh rooms.");
        }

        // Re-applies the connected artwork to one room Prefab whose walls a Layout setup has just rebuilt.
        public static void ApplyToPrefab(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "connected-room.mat");
            if (material == null)
                throw new InvalidOperationException("Run Apply Fairy Village Tiles and Walls before a Layout setup.");
            Apply(path, material);
        }

        public static string[] PrefabPaths() => AssetDatabase.FindAssets("t:Prefab", new[] { Week8GridFloorSetup.PrefabFolder })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<RoomPrefab>() != null)
            .OrderBy(p => p).ToArray();

        public static Vector2 RoomSize(Transform content) => new Vector2(
            2f * Mathf.Abs(content.Find("Right Upper Wall").localPosition.x) + RoomLayout.WallThickness,
            2f * Mathf.Abs(content.Find("Top Left Wall").localPosition.y) + RoomLayout.WallThickness);

        public static float NativeScale(Vector2 size) => MasterScale * Mathf.Min(1f, size.x / 16f, size.y / 9f);

        private static void Apply(string path, Material material)
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RoomPrefab room = prefab.GetComponent<RoomPrefab>();
                if (!room.TryValidate(out string error)) throw new InvalidOperationException(path + ": " + error);
                Transform content = room.Node.ContentRoot.transform;
                Remove(content, RootName);
                Transform temporary = content.Find(Week14BasicRoomArtworkSetup.BackgroundObjectName);
                if (temporary != null) temporary.gameObject.SetActive(false);
                Transform root = new GameObject(RootName).transform;
                root.SetParent(content, false);
                Vector2 size = RoomSize(content);
                float nativeScale = NativeScale(size);
                BuildWallBoundaries(content, size);
                float edge = 170 * nativeScale, dw = 146 * nativeScale, dh = 130 * nativeScale;
                float[] x = { -size.x / 2, -size.x / 2 + edge, -dw, dw, size.x / 2 - edge, size.x / 2 };
                // Preserve the master painting's vertical scale; its emblem/stone margins may extend
                // slightly past the collision rectangle, like other decorative wall overhangs.
                float margin = (941 * MasterScale - 9) * 0.5f * (nativeScale / MasterScale);
                float[] y = { size.y / 2 + margin, size.y / 2 + margin - 190 * nativeScale,
                    170.5f * nativeScale, -89.5f * nativeScale,
                    -size.y / 2 - margin + 221 * nativeScale, -size.y / 2 - margin };
                if (Enumerable.Range(0, 5).Any(i => x[i + 1] <= x[i] || y[i] <= y[i + 1]))
                    throw new InvalidOperationException(path + " is too small for the connected artwork bands.");
                for (int row = 0; row < 5; row++)
                for (int col = 0; col < 5; col++)
                {
                    if (IsDoorCell(col, row)) continue;
                    Draw(root, content, col == 2 && row == 2 ? "Grass Floor" : $"Connected Patch {col} {row}",
                        Load("open", $"patch-{col}-{row}"), CellRect(x, y, col, row), material, nativeScale);
                    if (row == 4) DrawForeground(root, content, ForegroundName + " " + col,
                        Load("open", $"patch-{col}-{row}"), CellRect(x, y, col, row), material, nativeScale,
                        cutoff: IsLowerCornerCell(col, row) ? CornerForegroundCutoff : 1f);
                }
                foreach (string wallName in Walls)
                {
                    Transform wall = content.Find(wallName);
                    if (wall == null || wall.GetComponent<BoxCollider2D>() == null) throw new InvalidOperationException(path + " missing " + wallName);
                    wall.GetComponent<SpriteRenderer>().enabled = false;
                }
                foreach (RoomDoorSlot slot in room.DoorSlots)
                {
                    (int col, int row) = DoorCell(slot.Direction);
                    Rect rect = CellRect(x, y, col, row);
                    Transform blocker = slot.Blocker.transform;
                    Remove(blocker, "Fairy Village Door");
                    blocker.GetComponent<SpriteRenderer>().enabled = false;
                    SpriteRenderer visual = Draw(blocker, content, "Fairy Village Door",
                        Load("open", DoorName(slot.Direction, "open")), rect, material, nativeScale);
                    FairyVillageDoorArtwork binding = blocker.GetComponent<FairyVillageDoorArtwork>();
                    if (binding == null) binding = blocker.gameObject.AddComponent<FairyVillageDoorArtwork>();
                    binding.Configure(slot.Blocker, visual, visual.sprite, Load("closed", DoorName(slot.Direction, "closed")),
                        Load("locked", DoorName(slot.Direction, "locked")));
                    binding.ConfigureVariants(DoorFamilies.Select(family => new FairyVillageDoorArtwork.Variant {
                        kind = FamilyKind(family),
                        open = LoadFamily(family, "open", slot.Direction),
                        closed = family == "secret" ? Load("sealed", DoorName(slot.Direction, "sealed")) : LoadFamily(family, "closed", slot.Direction),
                        locked = family == "secret" ? Load("sealed", DoorName(slot.Direction, "sealed")) : LoadFamily(family, "closed", slot.Direction),
                        openMask = AssetDatabase.LoadAssetAtPath<Texture2D>(FamilyMaskPath(family, "open")),
                        closedMask = AssetDatabase.LoadAssetAtPath<Texture2D>(family == "secret" ? ForegroundMaskPath : FamilyMaskPath(family, "closed"))
                    }).ToArray());
                    BoxCollider2D innerBlocker = BuildInnerBoundary(blocker, ClosedBoundaryName, slot.Direction, content, size);
                    innerBlocker.gameObject.AddComponent<ClosedDoorBoundary>().Configure(slot.Doorway);
                    slot.Blocker.ConfigureInteriorBlocker(innerBlocker);
                    Remove(blocker, ForegroundName);
                    if (slot.Direction == RoomDoorDirection.Down) {
                        SpriteRenderer front = DrawForeground(blocker, content, ForegroundName, visual.sprite, rect, material,
                            nativeScale, AssetDatabase.LoadAssetAtPath<Texture2D>(OpenDoorMaskPath));
                        binding.ConfigureForeground(front, AssetDatabase.LoadAssetAtPath<Texture2D>(OpenDoorMaskPath),
                            AssetDatabase.LoadAssetAtPath<Texture2D>(ClosedDoorMaskPath));
                    }
                    Remove(slot.Seal.transform, "Fairy Village Seal");
                    slot.Seal.GetComponent<SpriteRenderer>().enabled = false;
                    Draw(slot.Seal.transform, content, "Fairy Village Seal", Load("sealed", DoorName(slot.Direction, "sealed")), rect, material, nativeScale);
                    BuildSealBoundary(slot, content, size);
                    Remove(slot.Seal.transform, ForegroundName);
                    if (slot.Direction == RoomDoorDirection.Down)
                        DrawForeground(slot.Seal.transform, content, ForegroundName,
                            Load("sealed", DoorName(slot.Direction, "sealed")), rect, material, nativeScale);
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        private static Rect CellRect(float[] x, float[] y, int col, int row) => new Rect(x[col], y[row + 1], x[col + 1] - x[col], y[row] - y[row + 1]);
        // The floor actors can stand on: the largest whole-unit rectangle, centered on the room, that fits inside the
        // painted floor. Its edges are on the 0.5 Layout grid, so 1x1 obstacles and pits meet the walls with no gap,
        // and the room Layout grid (RoomLayoutFormat) is exactly this rectangle.
        public static Rect WalkableFloor(Vector2 size) {
            Rect painted = PaintedFloor(size);
            float width = Mathf.Floor(painted.width), height = Mathf.Floor(painted.height);
            return new Rect(-width / 2, -height / 2, width, height);
        }

        // Where the painting itself would let actors stand, from the registered wall and hedge edges.
        public static Rect PaintedFloor(Vector2 size) {
            float scale = NativeScale(size);
            // Actors do not shrink with Small's artwork. Keep their penetration below the
            // painted hedge edge constant in world units, using Small's approved 80% setting.
            Vector2 reference = Week14Room3Setup.SmallSize;
            float referenceBoundary = -reference.y / 2 + 110 * NativeScale(reference) * 0.84f;
            float penetration = LowerHedgeEdge(reference) - referenceBoundary;
            return Rect.MinMaxRect(-size.x / 2 + 125 * scale * 0.9f, LowerHedgeEdge(size) - penetration,
                size.x / 2 - 125 * scale * 0.9f, size.y / 2 - 125 * scale * 0.5f);
        }

        public static float LowerHedgeEdge(Vector2 size)
        {
            float scale = NativeScale(size);
            float margin = (941 * MasterScale - 9) * 0.5f * (scale / MasterScale);
            // Registered foreground silhouette varies slightly around source Y=783.
            return -size.y / 2 - margin + (941 - 783) * scale;
        }

        private static void BuildSealBoundary(RoomDoorSlot slot, Transform content, Vector2 size)
        {
            BuildInnerBoundary(slot.Seal.transform, SealBoundaryName, slot.Direction, content, size);
        }

        private static BoxCollider2D BuildInnerBoundary(Transform parent, string name, RoomDoorDirection direction,
            Transform content, Vector2 size)
        {
            Remove(parent, name);
            Rect floor = WalkableFloor(size);
            float gap = RoomLayout.DoorOpeningLength / 2;
            Rect rect = direction switch {
                RoomDoorDirection.Up => Rect.MinMaxRect(-gap, floor.yMax, gap, size.y / 2),
                RoomDoorDirection.Down => Rect.MinMaxRect(-gap, -size.y / 2, gap, floor.yMin),
                RoomDoorDirection.Left => Rect.MinMaxRect(-size.x / 2, -gap, floor.xMin, gap),
                _ => Rect.MinMaxRect(floor.xMax, -gap, size.x / 2, gap) };
            Transform wall = new GameObject(name).transform;
            wall.SetParent(parent, false);
            wall.position = content.TransformPoint(rect.center);
            Vector3 parentScale = parent.lossyScale;
            Vector3 contentScale = content.lossyScale;
            wall.localScale = new Vector3(contentScale.x / parentScale.x, contentScale.y / parentScale.y, 1);
            wall.gameObject.layer = content.Find("Top Left Wall").gameObject.layer;
            BoxCollider2D collider = wall.gameObject.AddComponent<BoxCollider2D>();
            collider.size = rect.size;
            return collider;
        }

        private static SpriteRenderer DrawForeground(Transform parent, Transform content, string name, Sprite sprite,
            Rect rect, Material material, float nativeScale, Texture2D mask = null, float cutoff = 1f)
        {
            SpriteRenderer renderer = Draw(parent, content, name, sprite, rect, material, nativeScale);
            renderer.sortingOrder = 20;
            renderer.GetComponent<ConnectedRoomPatch>().Configure(renderer,
                new Vector2(rect.width / (sprite.rect.width * nativeScale), rect.height / (sprite.rect.height * nativeScale)),
                cutoff, mask != null ? mask : AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundMaskPath));
            return renderer;
        }

        private static void ImportForegroundMask(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing transparent wall cutout.");
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void BuildWallBoundaries(Transform content, Vector2 size)
        {
            Remove(content, BoundaryName);
            Transform root = new GameObject(BoundaryName).transform;
            root.SetParent(content, false);
            Rect floor = WalkableFloor(size);
            float gap = RoomLayout.DoorOpeningLength / 2;
            int layer = content.Find("Top Left Wall").gameObject.layer;
            void Wall(string name, float left, float bottom, float right, float top) {
                Transform wall = new GameObject(name).transform;
                wall.SetParent(root, false);
                wall.gameObject.layer = layer;
                wall.localPosition = new Vector3((left + right) / 2, (bottom + top) / 2, 0);
                wall.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(right - left, top - bottom);
            }
            foreach (bool upper in new[] { false, true }) {
                float bottom = upper ? floor.yMax : -size.y / 2;
                float top = upper ? size.y / 2 : floor.yMin;
                Wall(upper ? "Upper Left" : "Lower Left", -size.x / 2, bottom, -gap, top);
                Wall(upper ? "Upper Right" : "Lower Right", gap, bottom, size.x / 2, top);
            }
            foreach (bool right in new[] { false, true }) {
                float left = right ? floor.xMax : -size.x / 2;
                float outer = right ? size.x / 2 : floor.xMin;
                Wall(right ? "Right Lower" : "Left Lower", left, floor.yMin, outer, -gap);
                Wall(right ? "Right Upper" : "Left Upper", left, gap, outer, floor.yMax);
            }
        }
        private static bool IsDoorCell(int col, int row) => col == 2 && (row == 0 || row == 4) || row == 2 && (col == 0 || col == 4);
        public static bool IsLowerCornerCell(int col, int row) => row == 4 && (col == 0 || col == 4);
        private static (int, int) DoorCell(RoomDoorDirection direction) => direction switch {
            RoomDoorDirection.Up => (2, 0), RoomDoorDirection.Down => (2, 4), RoomDoorDirection.Left => (0, 2), _ => (4, 2) };
        private static string DoorName(RoomDoorDirection direction, string state) => $"{direction.ToString().ToLowerInvariant()}-{state}";
        private static void Remove(Transform parent, string name) {
            Transform old = parent.Find(name); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject); }

        private static SpriteRenderer Draw(Transform parent, Transform content, string name, Sprite sprite, Rect rect, Material material, float nativeScale)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = content.TransformPoint(rect.center);
            Vector3 scale = parent.lossyScale;
            t.localScale = new Vector3(rect.width / sprite.bounds.size.x / scale.x, rect.height / sprite.bounds.size.y / scale.y, 1);
            SpriteRenderer renderer = t.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingOrder = -100;
            t.gameObject.AddComponent<ConnectedRoomPatch>().Configure(renderer,
                new Vector2(rect.width / (sprite.rect.width * nativeScale), rect.height / (sprite.rect.height * nativeScale)));
            return renderer;
        }

        private static Sprite Load(string state, string name) => AssetDatabase.LoadAllAssetsAtPath(Folder + $"room-{state}-v3.png")
            .OfType<Sprite>().First(s => s.name == name);

        public static DoorVisualKind FamilyKind(string family) => family switch {
            "shop" => DoorVisualKind.Shop, "treasure" => DoorVisualKind.KeyLockedTreasure,
            "boss" => DoorVisualKind.Boss, "secret" => DoorVisualKind.SecretPassage,
            _ => throw new ArgumentException("Unknown door family: " + family) };

        private static Sprite LoadFamily(string family, string state, RoomDoorDirection direction) =>
            AssetDatabase.LoadAllAssetsAtPath(FamilyPath(family, state, FamilyVersion(family, direction))).OfType<Sprite>()
                .First(s => s.name == DoorName(direction, family + "-" + (family == "treasure" && state == "closed" ? "locked" : state)));

        private static void SliceFamily(string family, string state, int version = 0)
        {
            if (version == 0) version = family == "treasure" || family == "boss" ? 3 : 2;
            string path = FamilyPath(family, state, version);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing special door atlas: " + path);
#pragma warning disable CS0618
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple &&
                importer.spritePixelsPerUnit == 100 && importer.spritesheet.Length == 4) return;
#pragma warning restore CS0618
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
#pragma warning disable CS0618
            importer.spritesheet = Enum.GetValues(typeof(RoomDoorDirection)).Cast<RoomDoorDirection>().Select(direction => {
                (int col, int row) = DoorCell(direction);
                SpriteMetaData metadata = Metadata(DoorName(direction, family + "-" +
                    (family == "treasure" && state == "closed" ? "locked" : state)), col, row);
                // Generated canvases can differ by one pixel. Register cuts in normalized source coordinates.
                Rect rect = metadata.rect;
                metadata.rect = new Rect(rect.x * sourceWidth / 1672f, rect.y * sourceHeight / 941f,
                    rect.width * sourceWidth / 1672f, rect.height * sourceHeight / 941f);
                return metadata;
            }).ToArray();
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        private static void Slice(string state)
        {
            string path = Folder + $"room-{state}-v3.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing room master: " + path);
#pragma warning disable CS0618
            // Existing registered masters already carry stable Sprite IDs. Do not rebuild their metadata.
            SpriteMetaData[] existing = importer.spritesheet;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple &&
                importer.spritePixelsPerUnit == 100 && existing.Length == 29 &&
                Enum.GetValues(typeof(RoomDoorDirection)).Cast<RoomDoorDirection>().All(direction =>
                    existing.Any(sprite => sprite.name == DoorName(direction, state)))) return;
#pragma warning restore CS0618
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            List<SpriteMetaData> sprites = new List<SpriteMetaData>();
            for (int row = 0; row < 5; row++) for (int col = 0; col < 5; col++) sprites.Add(Metadata($"patch-{col}-{row}", col, row));
            foreach (RoomDoorDirection direction in Enum.GetValues(typeof(RoomDoorDirection))) {
                (int col, int row) = DoorCell(direction); sprites.Add(Metadata(DoorName(direction, state), col, row)); }
#pragma warning disable CS0618
            importer.spritesheet = sprites.ToArray();
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        private static SpriteMetaData Metadata(string name, int col, int row) => new SpriteMetaData {
            name = name, rect = new Rect(SourceX[col], 941 - SourceY[row + 1], SourceX[col + 1] - SourceX[col], SourceY[row + 1] - SourceY[row]),
            alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
    }
}
