using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // UX-1: what a room looks like on the minimap, kept apart from how markers are laid out. Today it is the
    // bounding box of the profile's silhouette relative to a Basic room; an ㄱ/ㄴ-shaped Template can later add
    // its cell outline here without changing GameMinimapView.
    public readonly struct MinimapRoomShape
    {
        public const float BasicMarkerSize = 30f;

        public MinimapRoomShape(Vector2 normalizedSize)
        {
            NormalizedSize = normalizedSize;
        }

        // 1 x 1 is a Basic 16 x 9 room; Wide is wider than 1, Tall is taller.
        public Vector2 NormalizedSize { get; }
        public Vector2 MarkerSize => NormalizedSize * BasicMarkerSize;

        public static MinimapRoomShape From(RoomProfile profile)
        {
            if (profile == null || profile.MinimapSilhouette.Count < 3) return new MinimapRoomShape(Vector2.one);

            Vector2 minimum = profile.MinimapSilhouette[0];
            Vector2 maximum = minimum;
            for (int index = 1; index < profile.MinimapSilhouette.Count; index++)
            {
                minimum = Vector2.Min(minimum, profile.MinimapSilhouette[index]);
                maximum = Vector2.Max(maximum, profile.MinimapSilhouette[index]);
            }

            Vector2 silhouette = maximum - minimum;
            return new MinimapRoomShape(new Vector2(silhouette.x / RoomLayout.Width, silhouette.y / RoomLayout.Height));
        }
    }
}
