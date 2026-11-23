using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Connects the shop room art and the layout authored in docs/art-prompts/shop-decor-layout-v1. The 시스트 room uses
    // the 상점문 map and the 골디 room the 비밀문 map.
    public static class ShopDecorSetup
    {
        public const string ArtFolder = "Assets/Art/Shop/Decor/";
        public const string DefinitionPath = "Assets/Shop/shop-decor-definition.asset";
        public const string BasicDropTablePath = "Assets/Items/Drops/obstacle-basic-drop-table.asset";
        // Each kind of piece has its own table with the basic obstacle's candidates, never a secret pit.
        public static readonly IReadOnlyDictionary<string, float> DropChances = new Dictionary<string, float>
        {
            ["sist-sign"] = 0f,
            ["sist-crate-potion"] = 0.14f,
            ["sist-crates"] = 0.2f,
            ["sist-stall"] = 1f,
            ["goldi-barrel"] = 0.3f,
            ["goldi-crate-gold"] = 0.3f,
            ["goldi-treasure"] = 0.8f,
            ["goldi-pedestal"] = 0.5f,
            ["goldi-stall"] = 1f,
        };
        public const string ExcludedDropId = "pit";

        public static string DropTablePath(string art) => $"Assets/Items/Drops/obstacle-shop-decor-{art}-drop-table.asset";

        // The room art's registration: 1572 source pixels are the 16-unit room, and the door axes cross at this pixel.
        public const float SourcePixelsPerUnit = 1572f / 16f;
        private const float SourceCenterX = 836f;
        private const float SourceCenterY = 470.5f;

        private static readonly (RoomDoorDirection Direction, string Name, RectInt Rect)[] Doors =
        {
            (RoomDoorDirection.Up, "up", new RectInt(700, 0, 272, 190)),
            (RoomDoorDirection.Down, "down", new RectInt(700, 722, 272, 219)),
            (RoomDoorDirection.Left, "left", new RectInt(55, 330, 201, 265)),
            (RoomDoorDirection.Right, "right", new RectInt(1416, 330, 201, 265)),
        };
        private const int ForegroundTop = 760;
        // Pieces centered this far out were placed against a wall; their solid area runs slightly into it.
        public const float WallColumnX = 5.9f;
        public const float WallRowY = 2.8f;
        public const float WallReachX = 6.6f;
        public const float WallReachY = 3.6f;
        private const int ForegroundDoorLeft = 700;
        private const int ForegroundDoorRight = 972;

        private readonly struct Placement
        {
            public readonly string Art;
            public readonly float X, Y, Width, Height;
            public readonly bool Flip;

            public Placement(string art, float x, float y, float width, float height, bool flip = false)
            {
                Art = art; X = x; Y = y; Width = width; Height = height; Flip = flip;
            }
        }

        public static readonly Vector2 SistKeeperPosition = new(-0.025f, -0.25f);
        public static readonly Vector2 GoldiKeeperPosition = new(-0.065f, -0.23f);

        private static readonly Placement[] SistPieces =
        {
            new("sist-stall", -0.145f, 0.53f, 2.918f, 2.7f),
            new("sist-sign", -1.435f, 0.06f, 1.687f, 1.9f),
            new("sist-crates", 5.815f, 3.21f, 1.863f, 1.65f),
            new("sist-crates", -5.825f, 3.14f, 1.863f, 1.65f, true),
            new("sist-crates", -1.365f, 0.91f, 1.863f, 1.65f, true),
            new("sist-crates", 1.075f, 0.86f, 1.863f, 1.65f),
            new("sist-crate-potion", 1.105f, -0.39f, 1.174f, 1.061f),
            new("sist-crate-potion", -1.675f, 3.46f, 1.174f, 1.061f),
            new("sist-crate-potion", 1.715f, 3.35f, 1.174f, 1.061f),
            new("sist-crates", -5.845f, 2.38f, 1.863f, 1.65f, true),
            new("sist-crates", 5.835f, 2.42f, 1.863f, 1.65f),
            new("sist-crate-potion", 5.155f, -3.08f, 1.174f, 1.061f),
            new("sist-crate-potion", 6.175f, -2.27f, 1.174f, 1.061f),
            new("sist-crate-potion", 6.175f, -3.1f, 1.174f, 1.061f),
            new("sist-crate-potion", -6.255f, -3.17f, 1.174f, 1.061f, true),
            new("sist-crate-potion", -5.335f, -3.21f, 1.174f, 1.061f, true),
            new("sist-crate-potion", -6.255f, -2.27f, 1.174f, 1.061f, true),
            new("sist-crate-potion", -1.785f, -3.23f, 1.174f, 1.061f),
            new("sist-crate-potion", 1.765f, -3.05f, 1.174f, 1.061f),
        };

        private static readonly Placement[] GoldiPieces =
        {
            new("goldi-stall", 0.005f, 0.52f, 2.585f, 2.62f),
            new("goldi-treasure", 1.815f, -0.19f, 1.258f, 1.228f),
            new("goldi-pedestal", -1.625f, -0.28f, 0.852f, 1.085f),
            new("goldi-barrel", -4.995f, 2.94f, 1.037f, 0.9f),
            new("goldi-barrel", -6.065f, 2.94f, 0.973f, 0.845f),
            new("goldi-barrel", -6.235f, -2.89f, 1.037f, 0.9f),
            new("goldi-barrel", -5.265f, -2.86f, 1.037f, 0.9f),
            new("goldi-barrel", -5.985f, 1.97f, 1.037f, 0.9f),
            new("goldi-barrel", -6.185f, -1.96f, 1.037f, 0.9f),
            new("goldi-treasure", 1.785f, 0.46f, 1.258f, 1.228f),
            new("goldi-crate-gold", 6.153f, -2.963f, 1.03f, 0.894f),
            new("goldi-crate-gold", 5.195f, -2.97f, 1.033f, 0.897f),
            new("goldi-crate-gold", 6.065f, -1.93f, 1.033f, 0.897f),
            new("goldi-crate-gold", 6.185f, 2.02f, 1.033f, 0.897f),
            new("goldi-crate-gold", 5.265f, 2.99f, 1.033f, 0.897f),
            new("goldi-crate-gold", 6.225f, 3.01f, 1.033f, 0.897f),
            new("goldi-treasure", 1.805f, -3.12f, 1.258f, 1.228f),
            new("goldi-treasure", -1.855f, -3.12f, 1.258f, 1.228f),
            new("goldi-treasure", 1.805f, 3.02f, 1.258f, 1.228f),
            new("goldi-treasure", -1.825f, 3.11f, 1.258f, 1.228f),
        };

        public static int SistPieceCount => SistPieces.Length;
        public static int GoldiPieceCount => GoldiPieces.Length;

        [MenuItem("Trickal Fan Game/Shop/Apply Shop Decor")]
        public static void Setup()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ImportArt();
            Dictionary<string, ResourceDropTable> tables = EnsureDropTables();
            ShopDecorDefinition definition = EnsureDefinition(tables);
            if (!definition.TryValidate(out string error)) throw new InvalidOperationException(error);
            ConnectPrefab(definition);
            AssetDatabase.SaveAssets();
            Debug.Log("Shop decor setup finished.");
        }

        private static void ImportArt()
        {
            foreach (string path in Directory.GetFiles(ArtFolder, "*.png").Select(p => p.Replace('\\', '/')))
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing texture importer: " + path);
                bool isMap = path.EndsWith("-room-walled.png", StringComparison.Ordinal);
                float pixelsPerUnit = IsRoomArt(path) ? SourcePixelsPerUnit : 100f;
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                Vector2 pivot = isMap ? new Vector2(SourceCenterX / width, 1f - SourceCenterY / height) : new Vector2(0.5f, 0.5f);

                TextureImporterSettings settings = new();
                importer.ReadTextureSettings(settings);
                bool matches = importer.textureType == TextureImporterType.Sprite &&
                               importer.spriteImportMode == SpriteImportMode.Single &&
                               Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) &&
                               !importer.mipmapEnabled && importer.alphaIsTransparency &&
                               importer.filterMode == FilterMode.Bilinear && importer.maxTextureSize == 2048 &&
                               importer.textureCompression == TextureImporterCompression.Uncompressed &&
                               settings.spriteAlignment == (int)SpriteAlignment.Custom &&
                               settings.spritePivot == pivot && settings.spriteMeshType == SpriteMeshType.FullRect;
                if (matches) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private static bool IsRoomArt(string path)
        {
            string name = Path.GetFileName(path);
            return name.Contains("-room-walled") || name.Contains("-door-") || name.Contains("-foreground-");
        }

        private static Dictionary<string, ResourceDropTable> EnsureDropTables()
        {
            ResourceDropTable basic = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(BasicDropTablePath);
            if (basic == null) throw new InvalidOperationException("Missing basic obstacle drop table.");
            Dictionary<string, ResourceDropTable> tables = new(StringComparer.Ordinal);
            foreach ((string art, float chance) in DropChances)
            {
                string path = DropTablePath(art);
                ResourceDropTable table = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(path);
                if (table == null)
                {
                    table = ScriptableObject.CreateInstance<ResourceDropTable>();
                    AssetDatabase.CreateAsset(table, path);
                }

                Undo.RecordObject(table, "Configure shop decor drops");
                table.Configure(chance, basic.Entries.Where(entry => entry.DropId != ExcludedDropId)
                    .Select(entry => new ResourceDropEntry(entry.DropId, entry.Prefab, entry.Weight)).ToArray());
                EditorUtility.SetDirty(table);
                tables[art] = table;
            }

            return tables;
        }

        private static ShopDecorDefinition EnsureDefinition(IReadOnlyDictionary<string, ResourceDropTable> tables)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DefinitionPath));
            AssetDatabase.Refresh();
            ShopDecorDefinition definition = AssetDatabase.LoadAssetAtPath<ShopDecorDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ShopDecorDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            Undo.RecordObject(definition, "Configure shop decor");
            definition.Configure(new[]
            {
                BuildTheme(ShopKind.General, "sist", SistKeeperPosition, SistPieces, false, tables),
                BuildTheme(ShopKind.Goldi, "goldi", GoldiKeeperPosition, GoldiPieces, true, tables),
            });
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static ShopDecorDefinition.Theme BuildTheme(ShopKind kind, string prefix, Vector2 keeper,
            IReadOnlyList<Placement> placements, bool attachToWalls,
            IReadOnlyDictionary<string, ResourceDropTable> tables)
        {
            Sprite map = LoadSprite($"{prefix}-room-walled");
            int width = Mathf.RoundToInt(map.rect.width), height = Mathf.RoundToInt(map.rect.height);
            ShopDecorDefinition.Piece[] pieces = new ShopDecorDefinition.Piece[placements.Count];
            for (int index = 0; index < pieces.Length; index++)
            {
                Placement placement = placements[index];
                Sprite[] stages = Enumerable.Range(1, ShopDecorDefinition.StageCount)
                    .Select(stage => LoadSprite($"{placement.Art}-{stage}")).ToArray();
                pieces[index] = new ShopDecorDefinition.Piece
                {
                    pieceId = $"shop-decor-{prefix}-{index + 1:00}",
                    stages = stages,
                    hitArea = OpaqueArea(stages[0]),
                    solidArea = SolidArea(placement, attachToWalls),
                    dropTable = tables[placement.Art],
                    center = new Vector2(placement.X, placement.Y),
                    size = new Vector2(placement.Width, placement.Height),
                    flipX = placement.Flip,
                };
            }

            return new ShopDecorDefinition.Theme
            {
                kind = kind,
                map = map,
                doors = Doors.Select(door => new ShopDecorDefinition.DoorArt
                {
                    direction = door.Direction,
                    art = Placed($"{prefix}-door-{door.Name}", door.Rect),
                }).ToArray(),
                foregroundLeft = Placed($"{prefix}-foreground-left",
                    new RectInt(0, ForegroundTop, ForegroundDoorLeft, height - ForegroundTop)),
                foregroundCenter = Placed($"{prefix}-foreground-center", new RectInt(ForegroundDoorLeft, ForegroundTop,
                    ForegroundDoorRight - ForegroundDoorLeft, height - ForegroundTop)),
                foregroundRight = Placed($"{prefix}-foreground-right", new RectInt(ForegroundDoorRight, ForegroundTop,
                    width - ForegroundDoorRight, height - ForegroundTop)),
                keeperPosition = keeper,
                pieces = pieces,
            };
        }

        // The ground footprint. A piece in a wall row or column reaches that wall, and a column piece also covers its
        // art's height so the stack along the side wall leaves no slot for the player.
        private static Rect SolidArea(Placement placement, bool attachToWalls)
        {
            float halfWidth = placement.Width * ShopDecorDefinition.FootprintWidth * 0.5f;
            float feet = placement.Y - placement.Height * 0.5f;
            float xMin = placement.X - halfWidth, xMax = placement.X + halfWidth;
            float yMin = feet, yMax = feet + placement.Height * ShopDecorDefinition.FootprintHeight;
            if (attachToWalls)
            {
                if (Mathf.Abs(placement.X) >= WallColumnX)
                {
                    yMax = Mathf.Max(yMax, placement.Y + placement.Height * 0.5f);
                    if (placement.X > 0f) xMax = Mathf.Max(xMax, WallReachX);
                    else xMin = Mathf.Min(xMin, -WallReachX);
                }

                if (placement.Y >= WallRowY) yMax = Mathf.Max(yMax, WallReachY);
                else if (placement.Y <= -WallRowY) yMin = Mathf.Min(yMin, -WallReachY);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        // The visible part of the art as a 0-1 rect from its bottom-left corner.
        private static Rect OpaqueArea(Sprite sprite)
        {
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))))
                    throw new InvalidOperationException("Could not read shop decor art: " + sprite.name);
                Color32[] pixels = texture.GetPixels32();
                int width = texture.width, height = texture.height, minX = width, minY = height, maxX = -1, maxY = -1;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (pixels[y * width + x].a <= 128) continue;
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
                }

                if (maxX < 0) throw new InvalidOperationException("Shop decor art is empty: " + sprite.name);
                return Rect.MinMaxRect(minX / (float)width, minY / (float)height, (maxX + 1) / (float)width,
                    (maxY + 1) / (float)height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static ShopDecorDefinition.PlacedSprite Placed(string spriteName, RectInt sourceRect) => new()
        {
            sprite = LoadSprite(spriteName),
            position = new Vector2((sourceRect.x + sourceRect.width * 0.5f - SourceCenterX) / SourcePixelsPerUnit,
                (SourceCenterY - sourceRect.y - sourceRect.height * 0.5f) / SourcePixelsPerUnit),
        };

        private static Sprite LoadSprite(string spriteName)
        {
            string path = ArtFolder + spriteName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite != null ? sprite : throw new InvalidOperationException("Missing shop decor sprite: " + path);
        }

        private static void ConnectPrefab(ShopDecorDefinition definition)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Week20Special4Setup.ShopRoomPrefabPath);
            try
            {
                ShopRoom shop = root.GetComponent<ShopRoom>();
                if (shop == null) throw new InvalidOperationException("The shop Prefab has no ShopRoom.");
                if (shop.Decor == definition) return;
                shop.ConfigureDecor(definition);
                if (PrefabUtility.SaveAsPrefabAsset(root, Week20Special4Setup.ShopRoomPrefabPath) == null)
                    throw new InvalidOperationException("Could not save the shop Prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
