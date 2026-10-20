using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class ReviewedCombatEffectsVerification
    {
        [MenuItem("Trickal Fan Game/Effects/Verify Reviewed Combat Effects")]
        public static void Verify()
        {
            foreach (string name in new[] { "hp-heal", "circular-explosion", "sword-slash", "sword-slam", "slam-light", "slam-ground" })
            {
                Sprite[] frames = CombatEffectArtwork.Frames(name, 4, 2);
                Assert(frames.Length == 8, name + " must contain eight reviewed frames.");
                foreach (Sprite frame in frames)
                    Assert(frame != null && frame.rect.width > 0 && frame.rect.height > 0,
                        name + " has an empty frame.");
                TextureImporter importer = AssetImporter.GetAtPath(
                    "Assets/Resources/CombatEffects/" + name + ".png") as TextureImporter;
                Assert(importer != null && !importer.mipmapEnabled && importer.npotScale == TextureImporterNPOTScale.None &&
                       importer.textureCompression == TextureImporterCompression.Uncompressed,
                    name + " must preserve its alpha, NPOT size and frame boundaries.");
            }
            foreach (string name in new[] { "sp-mote", "warning-chevron", "landing-shadow" })
                Assert(CombatEffectArtwork.Frames(name).Length == 1, name + " is missing.");
            foreach (string name in new[] { "EnemyHitOverlay", "ChargeWarning", "MeleeWarning", "GroundSlam", "SlamLight", "LayeredSlam", LayeredSlamEffect.FlashShader })
            {
                Material material = CombatEffectArtwork.Material(name);
                string detail = material == null ? "(not loaded from Resources)" : string.Join(" | ", Array.ConvertAll(
                    ShaderUtil.GetShaderMessages(material.shader), m => m.message + " (line " + m.line + ")"));
                Assert(material != null && !ShaderUtil.ShaderHasError(material.shader),
                    name + " shader failed to import. " + detail);
            }
            Shader movement = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Art/Enemies/FairyKingdom/EnemyMovement.shader");
            Assert(movement != null && !ShaderUtil.ShaderHasError(movement), "Enemy movement hit shader failed.");
            ValidateRecoveryEvents();
            ValidateVaultShadowAndBossHit();
            ValidateSlamLayout();
            ValidateWarningPathAlignment();
            ValidateIndependentSlamHeight();
            ValidateSlamLightTiming();
            ValidateLayeredSlam();
            Debug.Log("Reviewed combat effects verification passed: legacy alpha sheets and five layered slam textures, " +
                      "eight shaders, falling slam flash, bounded slam layout, independent light with sustained peak, actual HP/SP recovery events, and no recovery VFX for resets, overheal or spending.");
        }

        private static void ValidateSlamLightTiming()
        {
            Assert(SlamLightEffect.StrengthAt(-0.1f) == 0f && SlamLightEffect.StrengthAt(0f) == 0f,
                "Slam light must begin without a premature flash.");
            Assert(Mathf.Approximately(SlamLightEffect.StrengthAt(SlamLightEffect.FallSeconds*0.5f), 0.5f),
                "The descending light must build up before reaching the ground.");
            foreach (float time in new[] { SlamLightEffect.FallSeconds, 0.45f,
                SlamLightEffect.FallSeconds+SlamLightEffect.PeakHoldSeconds-0.001f })
                Assert(SlamLightEffect.StrengthAt(time) == 1f,
                    "The light must hold its largest form for 0.7 seconds, including after the ground impact.");
            Assert(Mathf.Abs(SlamLightEffect.StrengthAt(SlamLightEffect.Duration-SlamLightEffect.FadeSeconds*0.5f) - 0.5f) < 0.001f &&
                   SlamLightEffect.StrengthAt(SlamLightEffect.Duration) == 0f,
                "The sustained light must fade and finish without leaving a residual glow.");
        }

        private static void ValidateLayeredSlam()
        {
            foreach(string name in new[] {LayeredSlamEffect.MainArtwork,LayeredSlamEffect.MiddleArtwork,
                LayeredSlamEffect.EndArtwork,LayeredSlamEffect.GroundArtwork,LayeredSlamEffect.DebrisArtwork})
            {
                Texture2D texture=Resources.Load<Texture2D>("CombatEffects/"+name);
                var importer=AssetImporter.GetAtPath("Assets/Resources/CombatEffects/"+name+".png") as TextureImporter;
                Assert(texture!=null&&importer!=null&&!importer.mipmapEnabled&&importer.alphaIsTransparency&&
                    importer.npotScale==TextureImporterNPOTScale.None&&
                    importer.textureCompression==TextureImporterCompression.Uncompressed&&importer.maxTextureSize>=4096,
                    name+" must preserve reviewed single-image/particle artwork without sheet slicing or resizing.");
            }
            Assert(LayeredSlamEffect.GroundOpacityAt(-0.01f)==0&&
                LayeredSlamEffect.GroundOpacityAt(LayeredSlamEffect.GroundSeconds)==0&&
                LayeredSlamEffect.GroundScaleAt(0.3f)>LayeredSlamEffect.GroundScaleAt(0),
                "The floor shock must expand and then clear.");
            foreach(float length in new[] {14f,9.2f,2f})
            foreach(float sign in new[] {1f,-1f})
            {
                var owner=new GameObject("Layered slam verification");
                try
                {
                    var effect=owner.AddComponent<LayeredSlamEffect>();
                    Type type=typeof(LayeredSlamEffect);
                    const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
                    type.GetField("length",flags).SetValue(effect,length);
                    type.GetField("sign",flags).SetValue(effect,sign);
                    type.GetField("room",flags).SetValue(effect,Rect.MinMaxRect(-50,-50,50,50));
                    type.GetField("material",flags).SetValue(effect,CombatEffectArtwork.Material("LayeredSlam"));
                    type.GetMethod("Build",flags).Invoke(effect,new object[] {9f});
                    effect.RenderAt(0.7f);
                    MeshRenderer main=owner.transform.Find("Slam Main").GetComponent<MeshRenderer>();
                    MeshRenderer ground=owner.transform.Find("Slam Ground").GetComponent<MeshRenderer>();
                    MeshRenderer debris=owner.transform.Find("Slam Debris").GetComponent<MeshRenderer>();
                    Assert(Mathf.Approximately(main.bounds.size.y,9f)&&main.bounds.size.x<=length+0.001f,
                        "Long, short and cropped narrow paths must preserve main height without horizontal stretching.");
                    var block=new MaterialPropertyBlock();main.GetPropertyBlock(block);
                    Vector4 uv=block.GetVector("_UVRect");
                    Assert(Mathf.Abs(main.bounds.size.x/uv.z-9f)<0.001f,
                        "A narrow main must crop original texels instead of compressing the whole artwork.");
                    Assert(ground.sortingOrder<main.sortingOrder&&debris.sortingOrder>main.sortingOrder,
                        "Floor shock, blade and airborne debris must have independent sorting.");
                    Assert(Mathf.Abs(ground.bounds.size.x-5.184f)<0.001f||length<5f,
                        "Ground size must be independent of the main light and path length.");
                    MeshRenderer flash=owner.transform.Find("Slam Flash").GetComponent<MeshRenderer>();
                    Assert(flash.sortingOrder>main.sortingOrder&&flash.sortingOrder<debris.sortingOrder,
                        "The falling flash must sit in front of the blade and behind airborne debris.");
                    effect.RenderAt(0.05f);
                    Assert(flash.enabled,"The falling flash must be visible while it strikes down.");
                    effect.RenderAt(LayeredSlamEffect.FlashSeconds);
                    Assert(!flash.enabled&&main.enabled,
                        "The falling flash must clear before the painted blade's sustained peak ends.");
                    effect.RenderAt(LayeredSlamEffect.Duration);
                    foreach(MeshRenderer renderer in owner.GetComponentsInChildren<MeshRenderer>())
                        Assert(!renderer.enabled,"All independent layers must clear after their lifetime.");
                }
                finally {Object.DestroyImmediate(owner);}
            }
            ValidateFallingFlash();
            Debug.Log("Layered slam v21 passed: five artwork imports, independent height/ground/debris, mirrored UV windows, lifetime and sorting.");
        }

        private static void ValidateFallingFlash()
        {
            Assert(LayeredSlamEffect.FlashSweepSeconds+LayeredSlamEffect.FlashJitterSeconds+
                LayeredSlamEffect.FlashFallSeconds<=SlamLightEffect.FallSeconds+0.0001f,
                "Every falling beam must reach the floor before the painted blade lands.");
            Assert(LayeredSlamEffect.FlashSeconds<SlamLightEffect.FallSeconds+SlamLightEffect.PeakHoldSeconds,
                "The falling flash must not outlast the blade's sustained peak.");
            const float length=10f,height=7f;
            foreach(float angle in new[] {0f,45f,-60f,90f})
            {
                Rect room=Rect.MinMaxRect(-50,-50,50,50);
                LayeredSlamEffect effect=LayeredSlamEffect.CreatePreview(Vector2.zero,length,height,angle,room,angle<0);
                Assert(effect!=null,"The slam preview entry must build without a source.");
                try
                {
                    Transform flash=effect.transform.Find("Slam Flash");
                    Assert(Quaternion.Angle(flash.rotation,Quaternion.identity)<0.01f,
                        "Falling beams must stay world-vertical for every path angle.");
                    Mesh mesh=flash.GetComponent<MeshFilter>().sharedMesh;
                    Assert(effect.FlashBeamCount>=3&&mesh.vertexCount==effect.FlashBeamCount*4,
                        "The flash must place a beam along the whole path.");
                    Vector3[] vertices=mesh.vertices;
                    var data=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(1,data);
                    Vector2 direction=effect.transform.right;
                    float minDelay=float.MaxValue,maxDelay=0;
                    for(int beam=0;beam<effect.FlashBeamCount;beam++)
                    {
                        // Corner 0 is the beam's lower-left; the foot is FlashBelow above it and FlashHalfWidth to the right.
                        Vector2 foot=(Vector2)vertices[beam*4]+new Vector2(LayeredSlamEffect.FlashHalfWidth,LayeredSlamEffect.FlashBelow);
                        Vector2 offset=foot-(Vector2)effect.transform.TransformPoint(new Vector3(0,-0.04f,0));
                        Assert(Mathf.Abs(Vector2.Dot(offset,new Vector2(-direction.y,direction.x)))<0.001f&&
                            Mathf.Abs(Vector2.Dot(offset,direction))<=length*0.5f+0.001f,
                            "Each beam must land on the slam's ground line inside the path.");
                        Assert(vertices[beam*4+2].y-foot.y>=height*LayeredSlamEffect.FlashHeightScale*0.6f-0.001f,
                            "Falling beams must start well above the blade so they read as coming from above.");
                        minDelay=Mathf.Min(minDelay,data[beam*4].x);maxDelay=Mathf.Max(maxDelay,data[beam*4].x);
                    }
                    Assert(minDelay>=0&&maxDelay-minDelay>=LayeredSlamEffect.FlashSweepSeconds*0.8f,
                        "The flash must sweep along the path rather than land everywhere at once.");
                    effect.SetPreviewLayers(true,false,true,true);
                    effect.RenderAt(0.05f);
                    Assert(!flash.GetComponent<MeshRenderer>().enabled,
                        "The preview layer toggle must hide the flash.");
                }
                finally {Object.DestroyImmediate(effect.gameObject);}
            }
            Debug.Log("Slam falling flash v22 passed: world-vertical beams on the ground line, sword-to-far sweep, landing before the blade, lifetime and preview toggles.");
        }

        private static void ValidateSlamLayout()
        {
            for (int degrees = -180; degrees <= 180; degrees += 15)
            {
                Vector2 aim = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                float angle = CrayonHeroBossPatternRuntime.ResolveSlamVisualAngle(aim, out bool flip);
                Quaternion rotation = Quaternion.Euler(0, 0, angle);
                Vector2 crestDirection = rotation * (flip ? Vector3.right : Vector3.left);
                Vector2 heightDirection = rotation * Vector3.up;
                Assert(Vector2.Dot(crestDirection, aim) > 0.999f && heightDirection.y >= -0.001f,
                    "Every slam direction must preserve the forward crest without inverting ground artwork.");
            }
            Rect arena = Rect.MinMaxRect(-10, -6, 10, 6);
            foreach (Vector2 direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down,
                new Vector2(1, 1).normalized })
            {
                Assert(CrayonHeroBossPatternRuntime.TryGetSlamVisualLayout(arena, Vector2.zero, direction,
                    4f, 1f, out Vector2 center, out float size), "Slam layout must support all attack directions.");
                Vector2 start = center - direction * size * 0.5f;
                Vector2 end = center + direction * size * 0.5f;
                float closestEdge = Mathf.Min(end.x-arena.xMin, arena.xMax-end.x,
                    end.y-arena.yMin, arena.yMax-end.y);
                Assert(Mathf.Abs(Vector2.Dot(start, direction) - 0.8f) < 0.001f && arena.Contains(end) && closestEdge < 0.16f,
                    "Slam must start near the sword side and reach the room edge in every direction.");
            }
            Assert(CrayonHeroBossPatternRuntime.TryGetSlamVisualLayout(arena, new Vector2(6f, 0), Vector2.right,
                2f, 0.5f, out Vector2 edgeCenter, out float edgeSize) &&
                edgeCenter.x + edgeSize * 0.5f < arena.xMax, "Slam must shorten near a wall.");
            Assert(!CrayonHeroBossPatternRuntime.TryGetSlamVisualLayout(arena, new Vector2(9.9f, 0), Vector2.right,
                4f, 1f, out _, out _), "No forward room must produce no oversized slam.");
            Assert(CrayonHeroBossPatternRuntime.TryGetSlamVisualLayout(Rect.MinMaxRect(-30,-20,30,20),
                Vector2.zero, Vector2.right, 4f, 1f, out Vector2 largeCenter, out float largeSize) &&
                largeSize > 18f && Mathf.Abs(largeCenter.x + largeSize * 0.5f - 29.85f) < 0.001f,
                "Slam in a large room must reach its boundary without a fixed size limit.");
        }

        private static void ValidateWarningPathAlignment()
        {
            Vector2 start = new Vector2(3.7f, -2.4f);
            for (int degrees = -180; degrees <= 180; degrees += 15)
            {
                Vector2 end = start + new Vector2(Mathf.Cos(degrees*Mathf.Deg2Rad),
                    Mathf.Sin(degrees*Mathf.Deg2Rad)) * 18f;
                Assert(SlamLightEffect.TryGetPathLayout(start, end, out Vector2 center,
                    out float length, out float angle, out bool reverse), "A warning line must produce a blade.");
                Vector2 axis = Quaternion.Euler(0, 0, angle) * Vector3.right;
                Vector2 actualStart = center + axis * length * (reverse ? -0.5f : 0.5f);
                Vector2 actualEnd = center + axis * length * (reverse ? 0.5f : -0.5f);
                Assert(Vector2.Distance(start, actualStart) < 0.001f &&
                    Vector2.Distance(end, actualEnd) < 0.001f,
                    "Blade baseline endpoints must exactly match the warning in every direction, without body offsets.");
            }
            Assert(!SlamLightEffect.TryGetPathLayout(start,start,out _,out _,out _,out _),
                "An empty warning must not produce an oversized blade.");
        }

        private static void ValidateIndependentSlamHeight()
        {
            Rect room = Rect.MinMaxRect(-10,-6,10,6);
            foreach (Vector2 start in new[] { Vector2.zero, new Vector2(8f,4f), new Vector2(-8f,-4f) })
                for (int degrees = -180; degrees <= 180; degrees += 15)
                {
                    Vector2 direction = new Vector2(Mathf.Cos(degrees*Mathf.Deg2Rad), Mathf.Sin(degrees*Mathf.Deg2Rad));
                    Assert(SlamLightEffect.TryGetFittedPathLayout(start,start+direction*18f,9f,room,
                        out Vector2 center,out float length,out float height,out float angle,out bool reverse),
                        "Room fitting must support centered and wall-adjacent attacks in every direction.");
                    Quaternion rotation = Quaternion.Euler(0,0,angle);
                    Vector2 axis = rotation * Vector3.right;
                    Vector2 anchor = center + axis * length * (reverse ? -0.5f : 0.5f);
                    Assert(Vector2.Distance(anchor,start+Vector2.up*SlamLightEffect.VerticalOffset) < 0.001f &&
                        Mathf.Approximately(height,9f),
                        "A wall-shortened path must preserve the lowered warning origin and full blade height.");
                    Vector2 end = center + axis * length * (reverse ? 0.5f : -0.5f);
                    Assert(room.Contains(anchor) && room.Contains(end) && length <= 18.001f,
                        "Blade baseline must finish inside the room along the warning direction.");
                }
            Assert(SlamLightEffect.TryGetFittedPathLayout(Vector2.zero,Vector2.right*18f,9f,
                Rect.MinMaxRect(-50,-50,50,50),out _,out float fullLength,out float fullHeight,out _,out _) &&
                Mathf.Approximately(fullLength,18f) && Mathf.Approximately(fullHeight,9f),
                "An ample room must preserve the full ultimate size.");
            Assert(SlamLightEffect.TryGetFittedPathLayout(new Vector2(9f,0),new Vector2(27f,0),9f,room,
                out _,out float shortLength,out float tallHeight,out _,out _) && shortLength < 1f &&
                Mathf.Approximately(tallHeight,9f), "A short path must not turn the ultimate into a tiny effect.");
        }

        [MenuItem("Trickal Fan Game/Effects/Verify Reviewed Effects With Combat Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week7LowerGradeSkillVerification.Verify();
            CrayonHeroAttackAnimationVerification.Verify();
            EnemyMovementAnimationVerification.Verify();
            Debug.Log("Reviewed effects combat regressions passed.");
        }

        private static void ValidateVaultShadowAndBossHit()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss2Setup.BossPrefabPath);
            Assert(prefab != null, "Vault boss prefab is missing.");
            GameObject owner = Object.Instantiate(prefab);
            var target = new GameObject("Reviewed landing verification target");
            try
            {
                target.transform.position = new Vector3(100f, 100f, 0f);
                target.AddComponent<Health>();
                BossMovementAnimator animator = owner.GetComponent<BossMovementAnimator>();
                Invoke(animator, "Awake");
                animator.RenderPose(0f, false);
                Health health = owner.GetComponent<Health>();
                health.ResetHealth();
                Color original = animator.Artwork.color;
                var feedback = owner.AddComponent<CombatVisualFeedback>();
                Invoke(feedback, "Awake");
                Invoke(feedback, "OnEnable");
                health.TakeDamage(1f);
                Invoke(feedback, "LateUpdate");
                var overlay = (SpriteRenderer)typeof(CombatVisualFeedback).GetField("overlay",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(feedback);
                Assert(overlay != null && overlay.enabled && overlay.transform.parent == animator.Artwork.transform &&
                       overlay.sprite == animator.Artwork.sprite && animator.Artwork.color == original,
                    "Hit feedback must follow visible boss artwork and preserve its original tint.");
                Invoke(feedback, "OnDisable");
                Assert(!overlay.enabled, "Disabling the enemy must clear its hit overlay.");

                var runtime = owner.GetComponent<SaemaeumVaultBossPatternRuntime>();
                Invoke(runtime, "Awake");
                runtime.BeginCombat(target.transform, 42, 0f);
                runtime.OnPatternStateChanged(BossActionState.Active, BossPatternExecution.SaemaeumJumpSequence, 5f);
                Assert(runtime.TryExecute(BossPatternExecution.SaemaeumJumpSequence), "Vault jump did not start.");
                var shadow = (GameObject)typeof(SaemaeumVaultBossPatternRuntime).GetField("landingTelegraph",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime);
                SpriteRenderer renderer = shadow != null ? shadow.GetComponent<SpriteRenderer>() : null;
                Assert(renderer != null && renderer.sprite == CombatEffectArtwork.Frames("landing-shadow")[0] &&
                       renderer.bounds.size.x > renderer.bounds.size.y * 2.9f &&
                       renderer.sortingOrder < animator.Source.sortingOrder,
                    "Jump destination must use the reviewed flat shadow below the boss.");
                Invoke(runtime, "OnDisable");
                Assert(shadow == null, "Cancelling the jump must remove its landing shadow.");
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(target); }
        }

        private static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

        private static void ValidateRecoveryEvents()
        {
            var owner = new GameObject("Recovery event verification");
            owner.transform.position = new Vector3(-8000, -8000);
            try
            {
                Health health = owner.AddComponent<Health>();
                health.ResetHealth();
                int heals = 0;
                float recovered = 0;
                health.Healed += amount => { heals++; recovered += amount; };
                health.Heal(2);
                health.ResetHealth();
                Assert(heals == 0, "Full health and reset must not play a recovery effect.");
                health.TakeDamage(2);
                health.Heal(1);
                Assert(heals == 1 && Mathf.Approximately(recovered, 1), "Actual healing must emit one event.");
                PlayerSP sp = owner.AddComponent<PlayerSP>();
                int gains = 0;
                sp.Restored += () => gains++;
                Assert(sp.TryAddHalf() && gains == 1, "Half-SP pickup must play absorption.");
                Assert(sp.TryAddHalf() && gains == 2, "Completed SP slot must play absorption.");
                sp.TrySpend();
                Assert(gains == 2, "Spending SP must not play absorption.");
                sp.AddMaxSP();
                Assert(gains == 2, "Increasing max SP must not play absorption.");
                sp.TryFillWithOvercharge(1);
                Assert(gains == 3, "Overcharge recovery must play absorption.");
                Assert(!sp.TryAdd() && !sp.TryFillWithOvercharge(1) && gains == 3,
                    "Rejected/full SP gains must not play absorption.");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[ReviewedCombatEffects] " + message);
        }
    }
}
