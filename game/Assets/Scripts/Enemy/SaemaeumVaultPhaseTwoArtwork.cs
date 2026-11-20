using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // 새마음금고 2페이즈 예고 (2026-11-20 사용자 원화 "보물상자 슬라임 분노 4단계"): startled, annoyed, roaring, seething.
    // 2×2 sheet of 1024px cells read from the top left, drawn at the movement frames' 400 PPU with a centered pivot.
    // The chest sits on the same spot as Vault_Hop_0 so the switch from walking does not jump.
    public static class SaemaeumVaultPhaseTwoArtwork
    {
        public const string SheetPath = "CombatEffects/saemaeum-vault-phase-two";
        public const int FrameCount = 4;
        public const int CellSize = 1024;
        public const float PixelsPerUnit = 400f;
        // Share of the transition each pose starts at: a short start, the roar holds longest.
        private static readonly float[] starts = { 0f, 0.18f, 0.36f, 0.78f };
        private static readonly Sprite[] frames = new Sprite[FrameCount];

        public static Sprite Frame(int index)
        {
            index = Mathf.Clamp(index, 0, FrameCount - 1);
            if (frames[index] != null) return frames[index];
            Texture2D texture = Resources.Load<Texture2D>(SheetPath);
            if (texture == null) return null;
            frames[index] = Sprite.Create(texture,
                new Rect(index % 2 * CellSize, (1 - index / 2) * CellSize, CellSize, CellSize),
                Vector2.one * 0.5f, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            frames[index].name = "Saemaeum vault phase two " + index;
            return frames[index];
        }

        public static int FrameAt(float progress)
        {
            int index = 0;
            for (int i = 1; i < FrameCount; i++)
                if (progress >= starts[i]) index = i;
            return index;
        }

        // A small shudder while it roars, still at every other pose.
        public static float ShakeAt(float progress, float time) =>
            FrameAt(progress) == 2 ? Mathf.Sin(time * 70f) * 0.03f : 0f;
    }
}
