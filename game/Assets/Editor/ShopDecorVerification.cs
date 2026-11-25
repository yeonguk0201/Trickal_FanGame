using System;
using System.Linq;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // The shop rooms' map, door art and layout are connected; no piece blocks a door; a piece breaks on its third hit
    // through its three art stages; and its drops are a little better than a basic obstacle's, without a secret pit.
    public static class ShopDecorVerification
    {
        private const float DoorWidth = 2.4f;
        private const float DoorDepth = 1.7f;
        private const float FloorHalfWidth = 6.5f;
        private const float FloorHalfHeight = 3.5f;

        public static void SetupAndVerifyBatch()
        {
            ShopDecorSetup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(ShopDecorSetup.DefinitionPath);
            ShopDecorSetup.Setup();
            Assert(!string.IsNullOrEmpty(guid) && guid == AssetDatabase.AssetPathToGUID(ShopDecorSetup.DefinitionPath),
                "Re-running the shop decor setup changed the definition GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Shop/Verify Shop Decor")]
        public static void Verify()
        {
            ShopDecorDefinition definition = AssetDatabase.LoadAssetAtPath<ShopDecorDefinition>(ShopDecorSetup.DefinitionPath);
            Assert(definition != null, "Missing shop decor definition. Run Trickal Fan Game/Shop/Apply Shop Decor.");
            Assert(definition.TryValidate(out string error), error);
            ShopRoom prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special4Setup.ShopRoomPrefabPath)
                ?.GetComponent<ShopRoom>();
            Assert(prefab != null && prefab.Decor == definition, "The shop Prefab does not use the shop decor definition.");

            ShopDecorDefinition.Theme sist = definition.Find(ShopKind.General), goldi = definition.Find(ShopKind.Goldi);
            Assert(sist.pieces.Length == ShopDecorSetup.SistPieceCount && sist.pieces.Length == 19,
                "The 시스트 room must have its 19 authored pieces.");
            Assert(goldi.pieces.Length == ShopDecorSetup.GoldiPieceCount && goldi.pieces.Length == 20,
                "The 골디 room must have its 20 authored pieces.");
            Assert(sist.map.name == "sist-room-walled" && goldi.map.name == "goldi-room-walled",
                "Each shop kind must use its own map.");
            Assert(sist.pieces.All(piece => piece.stages[0].name.StartsWith("sist-", StringComparison.Ordinal)),
                "The 시스트 room may only use 시스트 art.");
            Assert(goldi.pieces.All(piece => piece.stages[0].name.StartsWith("goldi-", StringComparison.Ordinal)),
                "The 골디 room may only use 골디 art.");
            foreach (ShopDecorDefinition.Theme theme in definition.Themes)
            {
                ValidateDoorsStayClear(theme);
                Assert(theme.pieces.All(piece => piece.hitArea.width > 0f && piece.hitArea.height > 0f),
                    "Every piece needs a hit area over its art.");
            }

            ValidateGoldiWallPieces(goldi);
            ValidateDrops(definition);
            ValidateDropAmounts(sist, goldi);
            // The sign never drops, so the break test leaves no pickup behind.
            ValidateThreeHitBreak(sist.pieces.First(piece => piece.stages[0].name == "sist-sign-1"));
            Debug.Log("Shop decor verification passed.");
        }

        private static void ValidateDoorsStayClear(ShopDecorDefinition.Theme theme)
        {
            Rect[] zones =
            {
                Rect.MinMaxRect(-DoorWidth / 2, FloorHalfHeight - DoorDepth, DoorWidth / 2, FloorHalfHeight),
                Rect.MinMaxRect(-DoorWidth / 2, -FloorHalfHeight, DoorWidth / 2, -FloorHalfHeight + DoorDepth),
                Rect.MinMaxRect(-FloorHalfWidth, -DoorWidth / 2, -FloorHalfWidth + DoorDepth, DoorWidth / 2),
                Rect.MinMaxRect(FloorHalfWidth - DoorDepth, -DoorWidth / 2, FloorHalfWidth, DoorWidth / 2),
            };
            foreach (ShopDecorDefinition.Piece piece in theme.pieces)
            {
                Assert(!zones.Any(zone => zone.Overlaps(piece.solidArea)), $"{piece.pieceId} blocks a door.");
            }
        }

        private static void ValidateGoldiWallPieces(ShopDecorDefinition.Theme goldi)
        {
            foreach (ShopDecorDefinition.Piece piece in goldi.pieces)
            {
                Rect solid = piece.solidArea;
                if (piece.center.y >= ShopDecorSetup.WallRowY)
                    Assert(solid.yMax >= FloorHalfHeight, $"The player can pass behind {piece.pieceId}.");
                if (piece.center.y <= -ShopDecorSetup.WallRowY)
                    Assert(solid.yMin <= -FloorHalfHeight, $"The player can pass behind {piece.pieceId}.");
                if (piece.center.x >= ShopDecorSetup.WallColumnX)
                    Assert(solid.xMax >= FloorHalfWidth, $"The player can pass behind {piece.pieceId}.");
                if (piece.center.x <= -ShopDecorSetup.WallColumnX)
                    Assert(solid.xMin <= -FloorHalfWidth, $"The player can pass behind {piece.pieceId}.");
            }
        }

        // Balance-1: a dropping piece leaves 2~3 pickups; a whole shop gives about 6 (시스트) or 10 (골디).
        private static void ValidateDropAmounts(ShopDecorDefinition.Theme sist, ShopDecorDefinition.Theme goldi)
        {
            int[] counts = Enumerable.Range(0, 256).Select(seed => DestructibleObstacle.ResolveDropCount(seed,
                ShopDecorDefinition.PieceMinimumDrops, ShopDecorDefinition.PieceMaximumDrops)).ToArray();
            Assert(ShopDecorDefinition.PieceMinimumDrops == 2 && ShopDecorDefinition.PieceMaximumDrops == 3 &&
                   counts.All(count => count is 2 or 3) && counts.Contains(2) && counts.Contains(3),
                "A shop piece that drops must leave 2 or 3 pickups, both possible.");
            float perDrop = (ShopDecorDefinition.PieceMinimumDrops + ShopDecorDefinition.PieceMaximumDrops) * 0.5f;
            float Expected(ShopDecorDefinition.Theme theme) =>
                theme.pieces.Sum(piece => piece.dropTable.DropChance) * perDrop;
            float sistTotal = Expected(sist), goldiTotal = Expected(goldi);
            Assert(sistTotal >= 5f && sistTotal <= 7f && goldiTotal >= 9f && goldiTotal <= 11f,
                $"A broken shop must give about 6 (시스트) and 10 (골디) pickups, found {sistTotal:0.0} and " +
                $"{goldiTotal:0.0}.");
        }

        private static void ValidateDrops(ShopDecorDefinition definition)
        {
            ResourceDropTable basic = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(ShopDecorSetup.BasicDropTablePath);
            Assert(basic != null, "Missing basic obstacle drop table.");
            foreach (ShopDecorDefinition.Theme theme in definition.Themes)
            {
                foreach (ShopDecorDefinition.Piece piece in theme.pieces)
                {
                    string stageName = piece.stages[0].name;
                    string art = stageName.Substring(0, stageName.Length - "-1".Length);
                    Assert(ShopDecorSetup.DropChances.TryGetValue(art, out float chance) &&
                           Mathf.Approximately(piece.dropTable.DropChance, chance),
                        $"{piece.pieceId} has the wrong drop chance.");
                    Assert(piece.dropTable.Entries.Count == basic.Entries.Count - 1 &&
                           piece.dropTable.Entries.All(entry => entry.DropId != ShopDecorSetup.ExcludedDropId),
                        "Shop decor must keep the basic drops except the secret pit.");
                }
            }
        }

        private static void ValidateThreeHitBreak(ShopDecorDefinition.Piece piece)
        {
            GameObject parent = new("Shop Decor Verification");
            try
            {
                DestructibleObstacle obstacle = ShopRoomDecor.BuildPiece(piece, parent.transform, null, 1, null,
                    parent.transform);
                SpriteRenderer visual = obstacle.GetComponentInChildren<SpriteRenderer>();
                Assert(obstacle.TryValidate(out string error), error);
                BoxCollider2D hitArea = obstacle.transform.Find(ShopRoomDecor.HitAreaName).GetComponent<BoxCollider2D>();
                BoxCollider2D footprint = obstacle.GetComponent<BoxCollider2D>();
                Assert(hitArea.isTrigger && !footprint.isTrigger, "Only the footprint may block the player.");
                Assert(hitArea.size.y > footprint.size.y * 2f && hitArea.size.y >= piece.size.y * 0.5f,
                    "Attacks must hit the piece's visible art, not only its footprint.");
                Assert(visual.sprite == piece.stages[0], "A new piece must show its intact art.");
                Assert(obstacle.RegisterPlayerHit() && visual.sprite == piece.stages[1], "The first hit must show the cracked art.");
                Assert(obstacle.RegisterPlayerHit() && visual.sprite == piece.stages[2], "The second hit must show the wrecked art.");
                Assert(!obstacle.IsBroken, "A piece must survive its second hit.");
                Assert(obstacle.RegisterPlayerHit() && obstacle.IsBroken, "The third hit must break the piece.");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
