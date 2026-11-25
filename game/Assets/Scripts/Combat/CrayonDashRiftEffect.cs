using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Boss-Pattern-1: the crayon hero's dash slash. The red warning and the silver strike are separate renderers so
    // each keeps its own sorting, timing and fade. Damage stays in CrayonHeroBossPatternRuntime.
    public sealed class CrayonDashRiftEffect : MonoBehaviour
    {
        public const string WarningArtwork = "crayon-rift-warning";
        public const string StrikeArtwork = "crayon-rift-strike";
        public const int StrikeColumns = 7;
        public const int StrikeRows = 4;
        public const int StrikeFrameCount = 26;
        public const float StrikeFramesPerSecond = 40f;
        // One texture cell covers this much of the world, for both textures: the drawn slash is 7.4 long, the warning 4.5.
        public const float CellWidth = 8f;
        public const float CellHeight = CellWidth / 3f;
        private const float WarningFadeInSeconds = 0.06f;

        private SpriteRenderer warning;
        private SpriteRenderer strike;
        private Sprite[] strikeFrames;
        private float warningAge;
        private float strikeAge = -1f;

        public static CrayonDashRiftEffect Show(Vector2 center, float angle, Renderer sortingSource, int warningOrderOffset)
        {
            if (!Application.isPlaying) return null;
            Sprite[] warningFrames = CombatEffectArtwork.Frames(WarningArtwork);
            Sprite[] strikeSheet = CombatEffectArtwork.Frames(StrikeArtwork, StrikeColumns, StrikeRows);
            if (warningFrames.Length == 0 || strikeSheet.Length == 0) return null;

            var owner = new GameObject("VFX crayon dash rift");
            owner.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
            var effect = owner.AddComponent<CrayonDashRiftEffect>();
            effect.strikeFrames = strikeSheet;
            effect.warning = effect.Layer("Warning", warningFrames[0], sortingSource, warningOrderOffset);
            effect.warning.color = new Color(1f, 1f, 1f, 0f);
            effect.strike = effect.Layer("Strike", strikeSheet[0], sortingSource, 2);
            effect.strike.enabled = false;
            return effect;
        }

        public void Strike()
        {
            warning.enabled = false;
            strike.enabled = true;
            strike.sprite = strikeFrames[0];
            strikeAge = 0f;
        }

        private SpriteRenderer Layer(string layerName, Sprite sprite, Renderer sortingSource, int orderOffset)
        {
            var child = new GameObject(layerName);
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = CombatEffectArtwork.Renderer(child, sprite, sortingSource, orderOffset);
            CombatEffectArtwork.Size(renderer, CellWidth, CellHeight);
            return renderer;
        }

        private void Update()
        {
            if (strikeAge < 0f)
            {
                warningAge += Time.deltaTime;
                warning.color = new Color(1f, 1f, 1f, Mathf.Clamp01(warningAge / WarningFadeInSeconds));
                return;
            }
            strikeAge += Time.deltaTime;
            int frame = (int)(strikeAge * StrikeFramesPerSecond);
            if (frame >= StrikeFrameCount) { Destroy(gameObject); return; }
            strike.sprite = strikeFrames[frame];
        }
    }
}
