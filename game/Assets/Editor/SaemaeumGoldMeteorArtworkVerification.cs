using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class SaemaeumGoldMeteorArtworkVerification
    {
        [MenuItem("Trickal Fan Game/Effects/Verify Saemaeum Gold Meteor Artwork")]
        public static void Verify()
        {
            foreach (string path in new[] { SaemaeumGoldMeteorArtwork.FallPath, SaemaeumGoldMeteorArtwork.ImpactPath })
            {
                Texture2D texture = Resources.Load<Texture2D>(path);
                Assert(texture != null && texture.width == 1254 && texture.height == 1254,
                    "Meteor sheets must load at full 1254px resolution: " + path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                Assert(importer.alphaIsTransparency && !importer.mipmapEnabled && importer.npotScale == TextureImporterNPOTScale.None,
                    "Preserve sheet alpha and non-power-of-two cell dimensions.");
            }
            for (int i = 0; i < 4; i++)
            {
                Sprite fall = SaemaeumGoldMeteorArtwork.FallFrame(i);
                Sprite impact = SaemaeumGoldMeteorArtwork.ImpactFrame(i);
                Assert(fall != null && impact != null && fall.rect.size == new Vector2(627f, 627f) &&
                       impact.rect.size == new Vector2(627f, 627f), "Missing meteor animation cell.");
                Assert(fall.rect.x == i % 2 * 627 && fall.rect.y == (1 - i / 2) * 627,
                    "Meteor cells must play in top-left reading order.");
            }
            SaemaeumGoldMeteor meteor = null;
            SaemaeumGoldMeteorImpact burst = null;
            try
            {
                meteor = SaemaeumGoldMeteor.Create(new Vector2(2f, 3f), false, 0f, 1f, 0.9f,
                    EnemyDamageTier.Light, 7f, 0.2f, null, null, 0, 4);
                Assert(Mathf.Approximately(SaemaeumGoldMeteor.FallDuration, 0.6f) &&
                       Mathf.Approximately(SaemaeumGoldMeteor.FallAngle, 35f), "The fall must last 0.6s at 35 degrees.");
                float fallStart = 1f - SaemaeumGoldMeteor.FallDuration;
                meteor.Tick(fallStart - 0.01f);
                Assert(!meteor.Lump.enabled && meteor.Mark.enabled, "Show only the warning before the fall.");
                float previousHeight = float.PositiveInfinity;
                float slope = Mathf.Tan(SaemaeumGoldMeteor.FallAngle * Mathf.Deg2Rad);
                for (int i = 0; i < 4; i++)
                {
                    meteor.Tick(fallStart + 0.001f + i * SaemaeumGoldMeteorArtwork.FrameDuration);
                    Assert(meteor.Lump.enabled && meteor.Lump.sprite == SaemaeumGoldMeteorArtwork.FallFrame(i),
                        "Falling meteor must loop the authored four cells.");
                    Vector3 offset = meteor.Lump.transform.localPosition;
                    Assert(offset.x < 0f && Mathf.Abs(-offset.x - offset.y * slope) < 0.001f && offset.y < previousHeight,
                        "The rock anchor must come down from the upper left along the 35 degree path to the mark.");
                    previousHeight = offset.y;
                }
                Vector3 direction = meteor.Lump.transform.TransformDirection(new Vector3(1f, 1f, 0f).normalized);
                Assert(Vector2.Dot(direction, SaemaeumGoldMeteor.FallPathUp) > 0.999f,
                    "The tail must point back up along the slanted fall path.");
                burst = SaemaeumGoldMeteorImpact.Create(meteor.Landing, meteor.Lump, null);
                float[] times = { 0f, 0.05f, 0.13f, 0.23f };
                for (int i = 0; i < 4; i++)
                {
                    burst.RenderAt(times[i]);
                    Assert(burst.Visual.sprite == SaemaeumGoldMeteorArtwork.ImpactFrame(i),
                        "Impact must play flash, peak, fragments and fading dust in order.");
                }
                burst.RenderAt(SaemaeumGoldMeteorImpact.FadeStartsAt - 0.01f);
                Assert(burst.Visual.sprite == SaemaeumGoldMeteorArtwork.ImpactFrame(3) && burst.Visual.color.a == 1f,
                    "The last impact cell must rest fully visible before fading.");
                burst.RenderAt(SaemaeumGoldMeteorImpact.FadeStartsAt + SaemaeumGoldMeteorImpact.FadeDuration * 0.5f);
                Assert(burst.Visual.color.a > 0.2f && burst.Visual.color.a < 0.8f, "The impact must fade gradually.");
                Assert(SaemaeumGoldMeteorImpact.Duration - SaemaeumGoldMeteorImpact.LastFrameAt <
                       SaemaeumGoldPillarBreakEffect.Duration - SaemaeumGoldPillarBreakEffect.FrameDuration * 2f,
                    "The meteor dust must vanish sooner than the pillar remnants.");
                burst.RenderAt(SaemaeumGoldMeteorImpact.Duration);
                Assert(burst.Visual.color.a == 0f && burst.GetComponent<Collider2D>() == null &&
                       burst.transform.position == new Vector3(2f, 3f, 0f),
                    "Impact must fade completely at the landing point without gameplay collision.");
                meteor.Tick(1f);
                Assert(meteor == null, "The landed meteor must disappear immediately.");
            }
            finally
            {
                if (meteor != null) UnityEngine.Object.DestroyImmediate(meteor.gameObject);
                if (burst != null) UnityEngine.Object.DestroyImmediate(burst.gameObject);
            }
            Debug.Log("Saemaeum gold meteor artwork verification passed: four falling cells, 35 degree 0.6s fall, four impact cells, hold and fade.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
