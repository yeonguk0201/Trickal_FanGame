using UnityEngine;

namespace TrickalFanGame.Room
{
    // Fairy village room measurements shared by the artwork setup and runtime visuals. All rects are in the room
    // content's local space; size is the full room size including walls.
    public static class FairyVillageRoomGeometry
    {
        public const float MasterScale = 16f / 1572f;
        public static readonly Vector2 SmallSize = new(12f, 6.75f);

        public static float NativeScale(Vector2 size) => MasterScale * Mathf.Min(1f, size.x / 16f, size.y / 9f);

        public static Vector2 RoomSize(Transform content)
        {
            Transform right = content.Find("Right Upper Wall");
            Transform top = content.Find("Top Left Wall");
            if (right == null || top == null) return Vector2.zero;
            return new Vector2(2f * Mathf.Abs(right.localPosition.x) + RoomLayout.WallThickness,
                2f * Mathf.Abs(top.localPosition.y) + RoomLayout.WallThickness);
        }

        // The floor actors can stand on: the largest whole-unit rectangle, centered on the room, that fits inside the
        // painted floor. Its edges are on the 0.5 Layout grid, so 1x1 obstacles and pits meet the walls with no gap,
        // and the room Layout grid (RoomLayoutFormat) is exactly this rectangle.
        public static Rect WalkableFloor(Vector2 size)
        {
            Rect painted = PaintedFloor(size);
            float width = Mathf.Floor(painted.width), height = Mathf.Floor(painted.height);
            return new Rect(-width / 2, -height / 2, width, height);
        }

        // Where the painting itself would let actors stand, from the registered wall and hedge edges.
        public static Rect PaintedFloor(Vector2 size)
        {
            float scale = NativeScale(size);
            // Actors do not shrink with Small's artwork. Keep their penetration below the
            // painted hedge edge constant in world units, using Small's approved 80% setting.
            Vector2 reference = SmallSize;
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
    }
}
