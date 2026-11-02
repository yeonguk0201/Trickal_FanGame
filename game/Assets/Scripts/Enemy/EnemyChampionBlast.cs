using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Effect-0 (docs/28, 2026-11-01 user decisions): the blast a 죽으면 자폭 champion leaves. Where it fell, the area
    // nine bombs on a 3x3 grid of 1x1 cells will reach flashes for one second while the stopped body turns red. Then
    // all nine real bomb explosions go off at once, each the unowned explosion an exploding box makes: the bomb radius, the bomb damage to every player and enemy inside,
    // and it breaks obstacles, webs, secret walls and chests. Every bomb hits on its own, so an enemy can take all
    // nine; a player is held to what their post-hit invulnerability lets through. The bombs carry no kill credit.
    // It has no Health and no collider, so room progress ignores it. Room transitions remove what has not gone off.
    [DisallowMultipleComponent]
    public sealed class EnemyChampionBlast : MonoBehaviour
    {
        public const string ObjectName = "Champion Death Blast";
        public const string BombName = "Champion Blast Bomb";
        public const string BodyBurstArtwork = EnemyDeathBlast.BodyBurstArtwork;
        public const float BodyBurstDuration = EnemyDeathBlast.BodyBurstDuration;
        public const float CellSize = 1f;
        public const int CellsPerSide = 3;
        public const int BombCount = CellsPerSide * CellsPerSide;
        public const float TelegraphDuration = 1f;
        // How long the object stays after the bombs go off.
        public const float LingerAfterBombs = 0.3f;
        // Under actors, over the floor (as the other death telegraphs).
        private const int AreaSortingOrder = 1;
        private const int AreaPixelsPerUnit = 24;

        private static readonly Color AreaColor = new(1f, 0.32f, 0.12f, 1f);
        private static readonly List<EnemyChampionBlast> live = new();
        private static Sprite areaSprite;
        private static float areaSpriteRadius;

        private readonly List<PlacedBomb> bombs = new();
        private PlacedBomb bombPrefab;
        private float startTime;
        private float destroyAt;

        public static IReadOnlyList<EnemyChampionBlast> Live
        {
            get
            {
                live.RemoveAll(item => item == null);
                return live;
            }
        }

        public float ExplodeAt { get; private set; }
        public bool HasStarted { get; private set; }
        public bool HasEnded { get; private set; }
        public float BombRadius { get; private set; }
        public IReadOnlyList<PlacedBomb> Bombs => bombs;
        public SpriteRenderer Area { get; private set; }
        public SpriteRenderer Body { get; private set; }
        public float TelegraphOpacity { get; private set; }

        public static Vector2[] BombPositions(Vector2 center)
        {
            Vector2Int[] order =
            {
                new(0, 0), new(0, 1), new(1, 1), new(1, 0), new(1, -1),
                new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1),
            };
            return order.Select(cell => center + (Vector2)cell * CellSize).ToArray();
        }

        // The area the nine bombs reach together: within the bomb radius of the 3x3 square of bomb positions.
        public static bool Reaches(Vector2 center, Vector2 point, float bombRadius) =>
            DistanceToSpots(point - center) <= bombRadius;

        private static float DistanceToSpots(Vector2 offset)
        {
            float half = CellSize * (CellsPerSide - 1) * 0.5f;
            float dx = Mathf.Max(0f, Mathf.Abs(offset.x) - half);
            float dy = Mathf.Max(0f, Mathf.Abs(offset.y) - half);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public static PlacedBomb ResolveBombPrefab()
        {
            PlayerBombController controller = FindAnyObjectByType<PlayerBombController>();
            return controller != null ? controller.BombPrefab : null;
        }

        public static EnemyChampionBlast Spawn(Vector3 position, Transform parent, SpriteRenderer sourceVisual,
            float now)
        {
            PlacedBomb prefab = ResolveBombPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("No placed bomb prefab in the scene, so the blast champion cannot explode.");
                return null;
            }

            GameObject host = new(ObjectName);
            host.transform.SetParent(parent, false);
            host.transform.position = position;
            EnemyChampionBlast blast = host.AddComponent<EnemyChampionBlast>();
            blast.bombPrefab = prefab;
            blast.BombRadius = prefab.ExplosionRadius;
            blast.startTime = now;
            blast.ExplodeAt = now + TelegraphDuration;

            GameObject area = new("Telegraph Area");
            area.transform.SetParent(host.transform, false);
            blast.Area = area.AddComponent<SpriteRenderer>();
            blast.Area.sprite = AreaSprite(blast.BombRadius);
            blast.Area.sortingOrder = AreaSortingOrder;

            if (sourceVisual != null && sourceVisual.sprite != null)
            {
                GameObject body = new("Body");
                body.transform.SetParent(host.transform, false);
                body.transform.SetPositionAndRotation(sourceVisual.transform.position, sourceVisual.transform.rotation);
                body.transform.localScale = sourceVisual.transform.lossyScale;
                blast.Body = body.AddComponent<SpriteRenderer>();
                blast.Body.sprite = sourceVisual.sprite;
                blast.Body.flipX = sourceVisual.flipX;
                blast.Body.flipY = sourceVisual.flipY;
                blast.Body.sharedMaterial = sourceVisual.sharedMaterial;
                blast.Body.sortingLayerID = sourceVisual.sortingLayerID;
                blast.Body.sortingOrder = sourceVisual.sortingOrder;
            }

            blast.ApplyTelegraph(now);
            if (!live.Contains(blast)) live.Add(blast);
            return blast;
        }

        public static void ClearAll()
        {
            foreach (EnemyChampionBlast blast in Live.ToArray()) blast.End();
        }

        // The reached area as a rounded square with a brighter rim, made once per bomb radius.
        private static Sprite AreaSprite(float bombRadius)
        {
            if (areaSprite != null && Mathf.Approximately(areaSpriteRadius, bombRadius)) return areaSprite;
            float half = CellSize * (CellsPerSide - 1) * 0.5f + bombRadius;
            int size = Mathf.CeilToInt(half * 2f * AreaPixelsPerUnit);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Champion blast area", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            float rim = 2.5f / AreaPixelsPerUnit;
            float feather = 1.2f / AreaPixelsPerUnit;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new((x + 0.5f) / AreaPixelsPerUnit - half, (y + 0.5f) / AreaPixelsPerUnit - half);
                float edge = bombRadius - DistanceToSpots(offset);
                float alpha = Mathf.Clamp01(edge / feather) * (edge < rim ? 1f : 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            areaSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                AreaPixelsPerUnit);
            areaSprite.name = "Champion blast area";
            areaSpriteRadius = bombRadius;
            return areaSprite;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        public void Tick(float now)
        {
            if (HasEnded) return;
            if (!HasStarted)
            {
                if (now < ExplodeAt)
                {
                    ApplyTelegraph(now);
                    return;
                }

                ExplodeAll();
                destroyAt = now + LingerAfterBombs;
            }

            if (now >= destroyAt) End();
        }

        // Fills in over the second and blinks faster toward the end, on the bomb fuse rhythm.
        private void ApplyTelegraph(float now)
        {
            float elapsed = Mathf.Max(0f, now - startTime);
            float progress = Mathf.Clamp01(elapsed / TelegraphDuration);
            float blink = TrickalFanGame.Combat.BombFuseFlash.Intensity(elapsed, TelegraphDuration);
            TelegraphOpacity = Mathf.Clamp01(Mathf.Lerp(0.16f, 0.34f, progress) + 0.3f * blink);
            if (Area != null)
            {
                Color area = AreaColor;
                area.a = TelegraphOpacity;
                Area.color = area;
            }

            if (Body != null) Body.color = EnemyDeathBlast.BodyColor(progress);
        }

        // All nine go off together, in this call. The bombs are the room's, like an exploding box's.
        private void ExplodeAll()
        {
            HasStarted = true;
            if (Area != null) Area.enabled = false;
            Vector2[] positions = BombPositions(transform.position);
            for (int index = 0; index < positions.Length; index++)
            {
                PlacedBomb bomb = Instantiate(bombPrefab, positions[index], Quaternion.identity, transform.parent);
                bomb.name = $"{BombName} {index + 1}";
                bombs.Add(bomb);
                bomb.ConfigureUnownedBlast(Time.time, 0f);
            }

            if (Body != null)
            {
                TrickalFanGame.Frontend.FairyKingdomSpriteEffect.Play(BodyBurstArtwork, Body.transform.position,
                    Mathf.Max(0.3f, Body.bounds.size.x * 1.2f), BodyBurstDuration, Body);
                Body.enabled = false;
            }
        }

        public void End()
        {
            if (HasEnded) return;
            HasEnded = true;
            live.Remove(this);
            Release(gameObject);
        }

        private static void Release(Object item)
        {
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }

        private void OnDestroy() => live.Remove(this);
    }
}
