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
            Assert(prefab != null && prefab.GetComponent<SaemaeumVaultBossPatternRuntime>().PillarRequiredHits == 3,
                "The boss prefab must use three-hit pillars.");
            Sprite intact = SaemaeumGoldPillarArtwork.Intact;
            Assert(intact != null && intact.texture.width == 1254, "Missing full-resolution pillar art.");
            Sprite light = SaemaeumGoldPillarArtwork.LightlyDamaged;
            Assert(light != null && light != intact && light.texture.width == 1536 &&
                   light.texture.height == 1024, "Missing six-cell damage sheet.");
            for (int i = 0; i < SaemaeumGoldPillarArtwork.FrameCount; i++)
            {
                Sprite frame = SaemaeumGoldPillarArtwork.Frame(i);
                Assert(frame != null && frame.rect == new Rect(i % 3 * 512, (1 - i / 3) * 512, 512, 512),
                    "Invalid six-cell row-major order.");
                Assert(frame.pixelsPerUnit == light.pixelsPerUnit,
                    "Breakup must retain a common scale rather than enlarging smaller rubble.");
            }
            SaemaeumGoldPillar pillar = null;
            SaemaeumGoldPillarBreakEffect effect = null;
            try
            {
                pillar = SaemaeumGoldPillar.Create(Vector2.zero, 0, 0, 0.6f, EnemyDamageTier.Light,
                    7f, 0.2f, 3, null, null, 0);
                pillar.Tick(1f);
                pillar.RefreshArtwork();
                Assert(TrickalFanGame.Frontend.GroundShadowProfiles.ResolveId(pillar.gameObject, null) ==
                       SaemaeumGoldPillar.ShadowProfileId &&
                       TrickalFanGame.Frontend.GroundShadowProfiles.TryGet(SaemaeumGoldPillar.ShadowProfileId, out _),
                    "The pillar must use its own reviewed shadow profile, not the room gold rock's.");
                SpriteRenderer drawing = pillar.transform.Find("Visual").GetComponent<SpriteRenderer>();
                DestructibleObstacle obstacle = pillar.GetComponent<DestructibleObstacle>();
                Assert(drawing.sprite == intact && pillar.GetComponent<FairyKingdomArtworkView>() == null,
                    "Pillar must use its own intact art without the gold-rock view overwriting it.");
                Assert(obstacle.RegisterPlayerHit() && !obstacle.IsBroken, "First hit must leave the pillar standing.");
                pillar.RefreshArtwork();
                Assert(drawing.sprite == light && drawing.color == Color.white,
                    "First hit must show only the new minor damage state.");
                Assert(obstacle.RegisterPlayerHit() && !obstacle.IsBroken && pillar.gameObject.activeSelf,
                    "Second hit must leave the pillar solid and standing.");
                pillar.RefreshArtwork();
                Assert(drawing.sprite == SaemaeumGoldPillarArtwork.Frame(1) && drawing.color == Color.white,
                    "Second hit must show the existing heavily cracked state.");
                effect = SaemaeumGoldPillarBreakEffect.Create(drawing, null);
                for (int i = 0; i < SaemaeumGoldPillarBreakEffect.BreakFrameCount; i++)
                {
                    effect.RenderAt(i * SaemaeumGoldPillarBreakEffect.FrameDuration + 0.001f);
                    Assert(effect.Visual.sprite == SaemaeumGoldPillarArtwork.Frame(i + 2),
                        "Third-hit destruction must play cells 3, 4, 5, 6 in order.");
                }
                effect.RenderAt(SaemaeumGoldPillarBreakEffect.FadeStartsAt - 0.01f);
                Assert(effect.Visual.sprite == SaemaeumGoldPillarArtwork.Frame(5) && effect.Visual.color.a == 1f,
                    "The last breakup cell must rest fully visible before fading.");
                effect.RenderAt(SaemaeumGoldPillarBreakEffect.FadeStartsAt + SaemaeumGoldPillarBreakEffect.FadeDuration * 0.5f);
                Assert(effect.Visual.color.a > 0.2f && effect.Visual.color.a < 0.8f, "The remnants must fade gradually.");
                effect.RenderAt(SaemaeumGoldPillarBreakEffect.Duration);
                Assert(effect.Visual.color.a == 0f && effect.GetComponent<Collider2D>() == null,
                    "Remnants must fully fade without blocking movement.");
                Assert(obstacle.RegisterPlayerHit() && obstacle.IsBroken && !pillar.gameObject.activeSelf &&
                       !obstacle.RegisterPlayerHit(), "Third hit must remove collision immediately and reject repeats.");
            }
            finally
            {
                if (pillar != null) UnityEngine.Object.DestroyImmediate(pillar.gameObject);
                if (effect != null) UnityEngine.Object.DestroyImmediate(effect.gameObject);
            }
            Debug.Log("Saemaeum gold pillar artwork verification passed: six cells, two damage states, four breakup cells, hold and fade, three-hit removal.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
