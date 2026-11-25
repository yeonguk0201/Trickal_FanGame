using System;
using System.Collections.Generic;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Shop
{
    // The authored look of each shop room: its map, the door drawn on each connected side, the keeper's spot, and the
    // breakable decorations. All positions are in the room content's local space.
    public sealed class ShopDecorDefinition : ScriptableObject
    {
        public const int PieceRequiredHits = 3;
        // Balance-1: a piece that drops leaves this many pickups (seeded per piece), unlike a plain rock's one.
        public const int PieceMinimumDrops = 2;
        public const int PieceMaximumDrops = 3;
        public const int StageCount = 3;
        // The share of a piece's art that is its solid ground footprint.
        public const float FootprintWidth = 0.8f;
        public const float FootprintHeight = 0.35f;

        [Serializable]
        public sealed class Piece
        {
            public string pieceId;
            public Sprite[] stages;
            // The visible art as a 0-1 rect from its bottom-left corner: attacks hit all of it.
            public Rect hitArea;
            // What stops the player, in room content space. A piece set against a wall reaches that wall.
            public Rect solidArea;
            public ResourceDropTable dropTable;
            public Vector2 center;
            public Vector2 size;
            public bool flipX;
        }

        [Serializable]
        public sealed class PlacedSprite
        {
            public Sprite sprite;
            public Vector2 position;
        }

        [Serializable]
        public sealed class DoorArt
        {
            public RoomDoorDirection direction;
            public PlacedSprite art;
        }

        [Serializable]
        public sealed class Theme
        {
            public ShopKind kind;
            public Sprite map;
            public DoorArt[] doors;
            public PlacedSprite foregroundLeft;
            public PlacedSprite foregroundCenter;
            public PlacedSprite foregroundRight;
            public Vector2 keeperPosition;
            public Piece[] pieces;
        }

        [SerializeField] private Theme[] themes = Array.Empty<Theme>();

        public IReadOnlyList<Theme> Themes => themes;

        public void Configure(Theme[] configuredThemes) => themes = configuredThemes ?? Array.Empty<Theme>();

        public Theme Find(ShopKind kind)
        {
            foreach (Theme theme in themes)
                if (theme != null && theme.kind == kind) return theme;
            return null;
        }

        public bool TryValidate(out string error)
        {
            foreach (ShopKind kind in Enum.GetValues(typeof(ShopKind)))
            {
                Theme theme = Find(kind);
                if (theme == null || theme.map == null || theme.doors == null || theme.pieces == null ||
                    theme.foregroundLeft?.sprite == null || theme.foregroundCenter?.sprite == null ||
                    theme.foregroundRight?.sprite == null)
                {
                    error = $"Shop decor theme {kind} is incomplete.";
                    return false;
                }

                foreach (RoomDoorDirection direction in Enum.GetValues(typeof(RoomDoorDirection)))
                {
                    if (Array.Find(theme.doors, door => door != null && door.direction == direction &&
                                                        door.art?.sprite != null) != null) continue;
                    error = $"Shop decor theme {kind} has no {direction} door art.";
                    return false;
                }

                HashSet<string> ids = new(StringComparer.Ordinal);
                foreach (Piece piece in theme.pieces)
                {
                    if (piece == null || !ids.Add(piece.pieceId ?? string.Empty) || piece.stages == null ||
                        piece.stages.Length != StageCount || Array.IndexOf(piece.stages, null) >= 0 ||
                        piece.size.x <= 0f || piece.size.y <= 0f || piece.hitArea.width <= 0f ||
                        piece.hitArea.height <= 0f || piece.solidArea.width <= 0f || piece.solidArea.height <= 0f ||
                        piece.dropTable == null || !piece.dropTable.TryValidate(out _))
                    {
                        error = $"Shop decor theme {kind} has an invalid piece '{piece?.pieceId}'.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }
    }
}
