using System;
using System.Collections.Generic;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Shop
{
    // Dresses a built shop room: the shop's own map replaces the village tiles, a door is drawn only on a side that is
    // connected right now, and the decorations stand on the floor as ordinary breakable obstacles. Walls, door
    // colliders and door rules stay with the room.
    [DisallowMultipleComponent]
    public sealed class ShopRoomDecor : MonoBehaviour
    {
        public const string RootName = "Shop Decor";
        public const string PieceVariantId = "shop-decor";
        public const int MapSortingOrder = -90;
        public const int DoorSortingOrder = -89;
        // Below the shopkeeper (-2) and the player (2).
        public const int PieceSortingOrder = -3;
        // A piece the player stands behind covers the walking player (2); a flying player (4) stays above it.
        public const int PieceFrontSortingOrder = 3;
        public const int KeeperFrontSortingOrder = 4;
        public const string HitAreaName = "Hit Area";
        private const string KeeperPortraitName = "Portrait";
        private const string StallArtSuffix = "-stall-1";
        // The same order as the village room's lower wall, which covers feet at the floor's bottom edge.
        public const int ForegroundSortingOrder = 20;
        // Pieces share one sorting order; this depth draws the one standing lower on screen in front.
        public const float DepthPerUnit = 0.001f;

        private static readonly string[] ReplacedArtPrefixes = { "Fairy Village", "Connected Patch", "Grass Floor" };

        private readonly List<(RoomDoorSlot Slot, SpriteRenderer Door)> doors = new();
        private readonly List<DestructibleObstacle> pieces = new();
        private RoomDoorSlot downSlot;
        private SpriteRenderer foregroundCenter;
        private readonly List<(DestructibleObstacle Piece, SpriteRenderer Visual, float Line)> sorted = new();
        private DestructibleObstacle stall;
        private float stallLine;
        private SpriteRenderer keeperPortrait;
        private int keeperSortingOrder;
        private PlayerFeet playerFeet;

        public IReadOnlyList<DestructibleObstacle> Pieces => pieces;

        // A room of another size keeps its own art, because the shop maps are painted for the 16x9 room.
        public static bool FitsRoom(Transform content)
        {
            Vector2 size = FairyVillageRoomGeometry.RoomSize(content);
            return Mathf.Approximately(size.x, RoomLayout.Width) && Mathf.Approximately(size.y, RoomLayout.Height);
        }

        public static ShopRoomDecor Build(ShopRoom shop, ShopDecorDefinition definition, RoomPrefab room,
            RoomRunState state, int contentSeed, RunProgress progress)
        {
            if (shop == null || definition == null || room == null) return null;
            ShopDecorDefinition.Theme theme = definition.Find(shop.Kind);
            Transform content = room.Node.ContentRoot.transform;
            if (theme == null || !FitsRoom(content)) return null;

            GameObject rootObject = new(RootName);
            rootObject.transform.SetParent(shop.transform, false);
            ShopRoomDecor decor = rootObject.AddComponent<ShopRoomDecor>();
            decor.HideRoomArt(content);
            decor.BuildBackdrop(theme, room);
            foreach (ShopDecorDefinition.Piece piece in theme.pieces)
                decor.pieces.Add(BuildPiece(piece, rootObject.transform, state, contentSeed,
                    progress, content, room.Node, room.Controller));
            if (shop.Keeper != null)
            {
                Vector3 keeper = shop.Keeper.transform.localPosition;
                shop.Keeper.transform.localPosition = new Vector3(theme.keeperPosition.x, theme.keeperPosition.y, keeper.z);
                foreach (SpriteRenderer renderer in shop.Keeper.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.gameObject.name != KeeperPortraitName) continue;
                    decor.keeperPortrait = renderer;
                    decor.keeperSortingOrder = renderer.sortingOrder;
                    break;
                }
            }

            decor.PrepareSorting(theme);
            decor.Refresh();
            return decor;
        }

        public static DestructibleObstacle BuildPiece(ShopDecorDefinition.Piece piece, Transform parent,
            RoomRunState state, int contentSeed, RunProgress progress,
            Transform dropParent, RoomNode node = null, RoomController controller = null)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            GameObject pieceObject = new(piece.pieceId);
            // Inactive until bound, so its first enable already knows its kind and broken state.
            pieceObject.SetActive(false);
            pieceObject.layer = LayerMask.NameToLayer("Environment");
            pieceObject.transform.SetParent(parent, false);
            float feet = piece.center.y - piece.size.y * 0.5f;
            pieceObject.transform.localPosition = new Vector3(piece.center.x, piece.center.y, feet * DepthPerUnit);

            BoxCollider2D footprint = pieceObject.AddComponent<BoxCollider2D>();
            footprint.size = piece.solidArea.size;
            footprint.offset = piece.solidArea.center - piece.center;

            // The footprint stops the player; this trigger takes attacks over all of the visible art.
            GameObject hitObject = new(HitAreaName) { layer = pieceObject.layer };
            hitObject.transform.SetParent(pieceObject.transform, false);
            BoxCollider2D hitArea = hitObject.AddComponent<BoxCollider2D>();
            Rect area = piece.hitArea;
            float areaCenterX = piece.flipX ? 1f - area.center.x : area.center.x;
            hitArea.isTrigger = true;
            hitArea.size = new Vector2(area.width * piece.size.x, area.height * piece.size.y);
            hitArea.offset = new Vector2((areaCenterX - 0.5f) * piece.size.x, (area.center.y - 0.5f) * piece.size.y);

            GameObject visualObject = new("Visual") { layer = pieceObject.layer };
            visualObject.transform.SetParent(pieceObject.transform, false);
            SpriteRenderer visual = visualObject.AddComponent<SpriteRenderer>();
            visual.sprite = piece.stages[0];
            visual.flipX = piece.flipX;
            visual.sortingOrder = PieceSortingOrder;
            Vector2 bounds = visual.sprite.bounds.size;
            visualObject.transform.localScale = new Vector3(piece.size.x / bounds.x, piece.size.y / bounds.y, 1f);

            DestructibleObstacle obstacle = pieceObject.AddComponent<DestructibleObstacle>();
            obstacle.Configure(piece.pieceId, ShopDecorDefinition.PieceRequiredHits, piece.dropTable, visual);
            obstacle.ConfigureFixedKind(PieceVariantId, Color.white, Color.white, false, 0, Color.white);
            obstacle.Bind(state, contentSeed, dropParent, progress, null, node, controller);
            pieceObject.AddComponent<ShopDecorPiece>().Configure(obstacle, visual, piece.stages);
            if (!obstacle.IsBroken) pieceObject.SetActive(true);
            return obstacle;
        }

        private void HideRoomArt(Transform content)
        {
            foreach (SpriteRenderer renderer in content.GetComponentsInChildren<SpriteRenderer>(true))
            {
                foreach (string prefix in ReplacedArtPrefixes)
                {
                    if (!renderer.gameObject.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    renderer.enabled = false;
                    break;
                }
            }
        }

        private void BuildBackdrop(ShopDecorDefinition.Theme theme, RoomPrefab room)
        {
            Draw("Shop Map", theme.map, Vector2.zero, MapSortingOrder);
            foreach (RoomDoorSlot slot in room.DoorSlots)
            {
                ShopDecorDefinition.DoorArt art = Array.Find(theme.doors, door => door.direction == slot.Direction);
                if (art == null) continue;
                doors.Add((slot, Draw($"Shop Door {slot.Direction}", art.art.sprite, art.art.position, DoorSortingOrder)));
                if (slot.Direction == RoomDoorDirection.Down) downSlot = slot;
            }

            Draw("Shop Foreground Left", theme.foregroundLeft.sprite, theme.foregroundLeft.position,
                ForegroundSortingOrder);
            Draw("Shop Foreground Right", theme.foregroundRight.sprite, theme.foregroundRight.position,
                ForegroundSortingOrder);
            foregroundCenter = Draw("Shop Foreground Center", theme.foregroundCenter.sprite,
                theme.foregroundCenter.position, ForegroundSortingOrder);
        }

        private SpriteRenderer Draw(string objectName, Sprite sprite, Vector2 position, int sortingOrder)
        {
            GameObject child = new(objectName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = position;
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void OnEnable() => Refresh();
        // A hidden passage can open while the player stands in this room.
        private void LateUpdate()
        {
            Refresh();
            if (playerFeet == null) playerFeet = FindFirstObjectByType<PlayerFeet>();
            if (playerFeet != null) ApplyPlayerDepth(playerFeet.WorldCenter.y - PlayerFeet.Radius);
        }

        private void PrepareSorting(ShopDecorDefinition.Theme theme)
        {
            for (int index = 0; index < pieces.Count; index++)
            {
                DestructibleObstacle piece = pieces[index];
                ShopDecorDefinition.Piece authored = theme.pieces[index];
                SpriteRenderer visual = piece.transform.Find("Visual").GetComponent<SpriteRenderer>();
                // The art's ground line, so a collider stretched to a wall does not change who stands in front.
                float line = piece.transform.position.y +
                             authored.size.y * (ShopDecorDefinition.FootprintHeight - 1f) * 0.5f;
                sorted.Add((piece, visual, line));
                if (authored.stages[0].name.EndsWith(StallArtSuffix, StringComparison.Ordinal))
                {
                    stall = piece;
                    stallLine = line;
                }
            }
        }

        // Each piece covers the player only while the player's feet are behind (above) the middle of its footprint.
        // The keeper stands in the stall, so it comes forward with it.
        public void ApplyPlayerDepth(float playerFeetY)
        {
            foreach ((DestructibleObstacle piece, SpriteRenderer visual, float line) in sorted)
                if (piece != null) visual.sortingOrder = playerFeetY > line ? PieceFrontSortingOrder : PieceSortingOrder;
            if (keeperPortrait == null) return;
            bool behindStall = stall != null && !stall.IsBroken && playerFeetY > stallLine;
            keeperPortrait.sortingOrder = behindStall ? KeeperFrontSortingOrder : keeperSortingOrder;
        }

        public void Refresh()
        {
            foreach ((RoomDoorSlot slot, SpriteRenderer door) in doors) door.enabled = slot != null && slot.IsConnected;
            // An open bottom door must not be covered by the solid wall strip.
            if (foregroundCenter != null) foregroundCenter.enabled = downSlot == null || !downSlot.IsConnected;
        }
    }
}
