using System;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // One lump of slime the Saemaeum Vault spits: it arcs from the boss to a locked spot, whose landing mark is
    // shown for the whole flight, and leaves a slime puddle there. The lump itself never hurts anyone.
    [DisallowMultipleComponent]
    public sealed class BossSlimeBlob : MonoBehaviour
    {
        public const string ObjectName = "Saemaeum Slime Blob";
        public const string MarkName = "Saemaeum Slime Landing Mark";

        private Vector2 start;
        private Vector2 destination;
        private float launchTime;
        private float flightDuration;
        private float arcHeight;
        private Transform body;
        private SpriteRenderer lump;
        private GameObject mark;
        private Action<Vector2> landed;
        private bool hasLanded;

        public Vector2 Destination => destination;
        public bool HasLanded => hasLanded;
        public GameObject Mark => mark;

        public static BossSlimeBlob Launch(Vector2 from, Vector2 to, float now, float duration, float height,
            float puddleRadius, Sprite sprite, int sortingLayerId, int sortingOrder, Action<Vector2> onLanded)
        {
            var host = new GameObject(ObjectName);
            host.transform.position = from;
            var blob = host.AddComponent<BossSlimeBlob>();
            blob.start = from;
            blob.destination = to;
            blob.launchTime = now;
            blob.flightDuration = Mathf.Max(0.05f, duration);
            blob.arcHeight = Mathf.Max(0f, height);
            blob.landed = onLanded;

            // The lump is drawn on a child so its height above the ground moves only the picture.
            var visual = new GameObject("Lump");
            visual.transform.SetParent(host.transform, false);
            // A small puddle's lump is smaller, so the two sizes read apart in the air.
            visual.transform.localScale = Vector3.one * (0.7f * Mathf.Clamp(puddleRadius / 1.2f, 0.55f, 1f));
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 0.86f, 0.32f, 0.95f);
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            blob.body = visual.transform;
            blob.lump = renderer;

            // Where the puddle will spread: the reviewed landing shadow at the puddle's width.
            blob.mark = new GameObject(MarkName);
            blob.mark.transform.position = to;
            SpriteRenderer markRenderer = blob.mark.AddComponent<SpriteRenderer>();
            Sprite[] shadow = CombatEffectArtwork.Frames("landing-shadow");
            markRenderer.sprite = shadow.Length > 0 ? shadow[0] : null;
            markRenderer.color = new Color(1f, 0.92f, 0.55f, 0.7f);
            markRenderer.sortingLayerID = sortingLayerId;
            markRenderer.sortingOrder = BossSlimePuddle.SortingOrder + 1;
            float width = puddleRadius * 2f;
            CombatEffectArtwork.Size(markRenderer, width, width / 3f);
            blob.Tick(now);
            return blob;
        }

        public void Tick(float now)
        {
            if (hasLanded) return;
            // A staggered lump waits hidden inside the boss until its own launch time.
            if (lump != null) lump.enabled = now >= launchTime;
            float flight = Mathf.Clamp01((now - launchTime) / flightDuration);
            // A lob: straight along the ground with a parabola above it.
            transform.position = Vector2.Lerp(start, destination, flight);
            if (body != null) body.localPosition = Vector3.up * (arcHeight * 4f * flight * (1f - flight));
            if (flight < 1f) return;
            hasLanded = true;
            landed?.Invoke(destination);
            Release();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        private void OnDestroy()
        {
            if (mark != null) CombatEffectArtwork.Release(mark);
        }

        private void Release()
        {
            if (mark != null) CombatEffectArtwork.Release(mark);
            mark = null;
            CombatEffectArtwork.Release(gameObject);
        }
    }
}
