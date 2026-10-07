using UnityEngine;

namespace TrickalFanGame.Combat
{
    public sealed class SPAbsorptionEffect : MonoBehaviour
    {
        private readonly SpriteRenderer[] motes = new SpriteRenderer[6];
        private Transform player;
        private SpriteRenderer playerVisual;
        private Vector3 lastPlayerPosition;
        private float elapsed;
        private float radius;

        public static void Play(Transform player, SpriteRenderer visual)
        {
            if (!Application.isPlaying || visual == null) return;
            Sprite[] sprites = CombatEffectArtwork.Frames("sp-mote", pivot: new Vector2(0.53f, 0.46f));
            if (sprites.Length == 0) return;
            // Repeated half-SP gains refresh one effect instead of obscuring the player with stacked swirls.
            var effect = player.GetComponentInChildren<SPAbsorptionEffect>();
            if (effect != null) { effect.elapsed = 0f; return; }
            var owner = new GameObject("VFX SP absorption");
            owner.transform.SetParent(player, false);
            effect = owner.AddComponent<SPAbsorptionEffect>();
            effect.player = player;
            effect.playerVisual = visual;
            effect.lastPlayerPosition = player.position;
            effect.radius = Mathf.Clamp(visual.bounds.size.x * 0.85f, 0.6f, 1.25f);
            for (int i = 0; i < effect.motes.Length; i++)
            {
                var child = new GameObject("SP mote " + i);
                child.transform.SetParent(owner.transform, false);
                effect.motes[i] = CombatEffectArtwork.Renderer(child, sprites[0], visual, 2);
                CombatEffectArtwork.Size(effect.motes[i], 1.1f, 0.73f);
                effect.motes[i].color = new Color(1f, 1f, 1f, 0f);
            }
        }

        private void Update()
        {
            if (player == null || playerVisual == null ||
                (player.position - lastPlayerPosition).sqrMagnitude > 16f)
            { Destroy(gameObject); return; }
            lastPlayerPosition = player.position;
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / 2.8f);
            if (progress >= 1f) { Destroy(gameObject); return; }
            float r = radius * Mathf.Pow(1f - progress, 1.15f);
            Vector3 center = playerVisual.bounds.center;
            float alpha = Mathf.Min(1f, progress * 12f) * Mathf.Pow(1f - progress, 1.9f) * 0.85f;
            for (int i = 0; i < motes.Length; i++)
            {
                float angle = i * Mathf.PI / 3f + progress * Mathf.PI * 2.8f;
                motes[i].transform.position = center + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * 0.68f, 0f);
                motes[i].transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
                motes[i].color = new Color(1f, 1f, 1f, alpha);
            }
        }
    }
}
