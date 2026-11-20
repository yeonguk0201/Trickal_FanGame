using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class SaemaeumGoldPillarArtworkVerification
    {
        [MenuItem("Trickal Fan Game/Effects/Verify Saemaeum Gold Pillar Artwork")]
        public static void Verify()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SaemaeumVaultBoss.prefab");
            Assert(prefab != null && prefab.GetComponent<SaemaeumVaultBossPatternRuntime>().PillarRequiredHits == 2,
                "The boss prefab must use two-hit pillars.");
            Sprite intact = SaemaeumGoldPillarArtwork.Intact;
            Assert(intact != null && intact.texture.width == 1254, "Missing full-resolution pillar art.");
            for (int i = 0; i < 4; i++)
            {
                Sprite frame = SaemaeumGoldPillarArtwork.Frame(i);
                Assert(frame != null && frame.rect.size == new Vector2(627, 627), "Invalid breakup cell.");
                Assert(frame.pivot == SaemaeumGoldPillarArtwork.Frame(0).pivot &&
                       frame.pixelsPerUnit == SaemaeumGoldPillarArtwork.Frame(0).pixelsPerUnit,
                    "Breakup must retain a common scale and ground pivot.");
            }
            SaemaeumGoldPillar pillar = null;
            SaemaeumGoldPillarBreakEffect effect = null;
            try
            {
                pillar = SaemaeumGoldPillar.Create(Vector2.zero, 0, 0, 0.6f, EnemyDamageTier.Light,
                    7f, 0.2f, 2, null, null, 0);
                pillar.Tick(1f);
                pillar.RefreshArtwork();
                SpriteRenderer drawing = pillar.transform.Find("Visual").GetComponent<SpriteRenderer>();
                DestructibleObstacle obstacle = pillar.GetComponent<DestructibleObstacle>();
                Assert(drawing.sprite == intact && pillar.GetComponent<FairyKingdomArtworkView>() == null,
                    "Pillar must use its own intact art without the gold-rock view overwriting it.");
                Assert(obstacle.RegisterPlayerHit() && !obstacle.IsBroken, "First hit must leave the pillar standing.");
                pillar.RefreshArtwork();
                Assert(drawing.sprite == SaemaeumGoldPillarArtwork.Frame(0) && drawing.color == Color.white,
                    "First hit must show the authored cracked state.");
                effect = SaemaeumGoldPillarBreakEffect.Create(drawing, null);
                for (int i = 0; i < 3; i++)
                {
                    effect.RenderAt(i * SaemaeumGoldPillarBreakEffect.FrameDuration + 0.001f);
                    Assert(effect.Visual.sprite == SaemaeumGoldPillarArtwork.Frame(i + 1),
                        "Second-hit destruction must play cells 2, 3, 4 in order.");
                }
                effect.RenderAt(SaemaeumGoldPillarBreakEffect.Duration);
                Assert(effect.Visual.color.a == 0f && effect.GetComponent<Collider2D>() == null,
                    "Remnants must fully fade without blocking movement.");
                Assert(obstacle.RegisterPlayerHit() && obstacle.IsBroken && !pillar.gameObject.activeSelf &&
                       !obstacle.RegisterPlayerHit(), "Second hit must remove collision immediately and reject repeats.");
            }
            finally
            {
                if (pillar != null) UnityEngine.Object.DestroyImmediate(pillar.gameObject);
                if (effect != null) UnityEngine.Object.DestroyImmediate(effect.gameObject);
            }
            Debug.Log("Saemaeum gold pillar artwork verification passed: intact, cracked, three breakup cells, two-hit removal.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
