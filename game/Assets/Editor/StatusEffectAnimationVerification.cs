using System;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class StatusEffectAnimationVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Apply Status Preview To Selected Characters (Play)")]
        public static void PreviewSelected()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Select live characters during Play.");
            foreach (GameObject owner in Selection.gameObjects)
            {
                if (!owner.scene.IsValid()) continue;
                Health target = owner.GetComponentInParent<Health>();
                if (target == null) continue;
                EnemyStatusEffects.TryApplyPoison(target,null,1f,new PoisonSettings(1f,.0001f,10f,1f,4),0f);
                EnemyStatusEffects.TryApplyBurn(target,null,1f,new BurnSettings(1f,.0001f,10f,1f),0f);
                EnemyStatusEffects.TryApplyShock(target,new ShockSettings(1f,.1f,10f,4),0f);
            }
        }
        [MenuItem("Trickal Fan Game/Artwork/Verify Shared Status Animations")]
        public static void Verify()
        {
            var effects = StatusEffectAnimation.Effects;
            Require(effects.Length == 3, "Three status sheets must be included in Resources.");
            var anchors = effects.SelectMany(e => e.pieces.Select(p => new Vector2(p.anchorX, p.anchorY))).ToArray();
            Require(anchors.Distinct().Count() == 9, "Simultaneous effects must use nine distinct anchors.");
            foreach (var effect in effects)
            {
                Require(effect.frames.Length == 8 && effect.pieces.Length == 3, "Eight frames and three pieces required.");
                for (int f = 0; f < 8; f++) for (int p = 0; p < 3; p++)
                    Require(effect.sprites[f,p] != null && effect.sprites[f,p].rect.width > 0,
                        "Missing frame or piece: " + effect.id);
            }
            var shock = effects.Single(e => e.id == "shock");
            var poison = effects.Single(e => e.id == "poison");
            for (int p = 0; p < 3; p++)
            {
                Require(StatusEffectAnimation.Sample(shock,p,.02f+p*.055f).alpha == 1f, "Shock must flash.");
                for (int ms = 330; ms < 1100; ms += 10)
                    Require(StatusEffectAnimation.Sample(shock,p,ms/1000f).alpha == 0f, "Shock quiet period missing.");
            }
            Require(StatusEffectAnimation.Sample(poison,0,.7f).y > StatusEffectAnimation.Sample(poison,0,.2f).y,
                "Poison bubbles must rise.");
            Sprite sprite = FairyKingdomArtwork.Fit("low-blood-sugar-fairy",1f);
            Require(sprite != null,"Reference character artwork missing.");
            VerifyOwner("Player",sprite,Vector3.one,false);
            VerifyOwner("Enemy",sprite,new Vector3(.7f,.7f,1f),false);
            VerifyOwner("Boss",sprite,new Vector3(3f,3f,1f),false);
            VerifyOwner("Scaled child character",sprite,new Vector3(2f,.8f,1f),true);
            VerifyAttackSizeIsolation(sprite);
            foreach (string name in new[]{"TestBoss","CrayonHeroBoss","SaemaeumVaultBoss"})
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+name+".prefab");
                Require(prefab != null,"Missing boss prefab: "+name);
                GameObject owner = Object.Instantiate(prefab);
                try { VerifyTarget(owner); } finally { Object.DestroyImmediate(owner); }
            }
            Debug.Log("Shared status animation verification passed: 24 frames/72 pieces, nine independent anchors, " +
                "rising poison, intermittent shock, player/enemy/boss and child/nonuniform scaling, attack frame/pose size isolation, clear/disable cleanup; source color preserved.");
        }
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week23Passive1Verification.Verify();
            Week23Artifact2Verification.Verify();
            Debug.Log("Shared status animation combat regressions passed.");
        }
        private static void VerifyOwner(string name,Sprite sprite,Vector3 scale,bool child)
        {
            GameObject owner = new(name);
            try
            {
                owner.transform.localScale = scale;
                GameObject visual = child ? new GameObject("Body") : owner;
                if (child) { visual.transform.SetParent(owner.transform,false); visual.transform.localScale = new Vector3(.6f,1.3f,1f); }
                visual.AddComponent<SpriteRenderer>().sprite = sprite;
                owner.AddComponent<Health>();
                VerifyTarget(owner);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        private static void VerifyTarget(GameObject owner)
        {
            Health health = owner.GetComponent<Health>();
            Require(health != null,"Health missing on "+owner.name);
            health.ResetHealth();
            var source = owner.GetComponentInChildren<SpriteRenderer>();
            Require(source != null && source.sprite != null,"Character sprite missing on "+owner.name);
            Color color = source.color;
            Vector3 scale = owner.transform.localScale;
            EnemyStatusEffects.TryApplyPoison(health,null,1f,new PoisonSettings(1f,.01f,100f,1f,4),0f);
            EnemyStatusEffects.TryApplyBurn(health,null,1f,new BurnSettings(1f,.01f,100f,1f),0f);
            EnemyStatusEffects.TryApplyShock(health,new ShockSettings(1f,.1f,100f,4),0f);
            var status = owner.GetComponent<EnemyStatusEffects>();
            var view = owner.GetComponent<FairyKingdomStatusArtwork>();
            Require(view != null,"Shared renderer was not attached to "+owner.name);
            float time = Time.time;
            view.RefreshAt(time);
            view.RefreshAt(time+.2f);
            var pieces = owner.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.StartsWith("Status ")).ToArray();
            Require(pieces.Length == 9,"Three pieces per status required on "+owner.name);
            var fire = pieces.First(r=>r.name=="Status burn 0");
            Require(view.BodyRenderer != null && Mathf.Abs(fire.bounds.size.x/view.StatusBounds.size.y-.27f)<.003f,
                "Effect must scale with visible body height on "+owner.name);
            Require(source.color == color && owner.transform.localScale == scale,"Artwork changed character color/scale.");
            Require(!owner.GetComponentsInChildren<Transform>().Any(t=>t.name=="Status Tint"),"Legacy tint still created.");
            view.RefreshAt(time+.5f);
            Require(pieces.Where(r=>r.name.StartsWith("Status shock")).All(r=>!r.enabled),"Shock must fully disappear.");
            status.Clear();view.RefreshAt(time+.6f);
            Require(pieces.All(r=>!r.enabled),"Cleared status left visible pieces.");
            owner.SetActive(false);
            Require(pieces.All(r=>!r.enabled),"Disabled owner left visible pieces.");
        }
        private static void VerifyAttackSizeIsolation(Sprite neutral)
        {
            GameObject owner = new("Attack size regression");
            Sprite attackSprite = Sprite.Create(neutral.texture,neutral.rect,Vector2.one*.5f,
                neutral.pixelsPerUnit/2.5f,0,SpriteMeshType.FullRect);
            try
            {
                owner.transform.localScale = Vector3.one * 1.7f;
                var body = owner.AddComponent<SpriteRenderer>();body.sprite = neutral;
                var health = owner.AddComponent<Health>();health.ResetHealth();
                var attack = owner.AddComponent<TrickalFanGame.Enemy.EnemyAttackArtwork>();
                attack.Configure(neutral,new[]{attackSprite,attackSprite,attackSprite,attackSprite});
                var presentation = owner.GetComponent<TrickalFanGame.Enemy.EnemyAttackPresentation>();
                var serialized = new SerializedObject(presentation);
                serialized.FindProperty("activeScale").vector2Value = new Vector2(1.6f,2.1f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                presentation.SetPhase(TrickalFanGame.Enemy.EnemyAttackPhase.Idle);
                // First status is applied DURING the larger attack pose, not just before it.
                presentation.SetPhase(TrickalFanGame.Enemy.EnemyAttackPhase.Active);
                body.sprite = attackSprite;
                EnemyStatusEffects.TryApplyBurn(health,null,1f,new BurnSettings(1f,.01f,100f,1f),0f);
                var view = owner.GetComponent<FairyKingdomStatusArtwork>();view.RefreshAt(Time.time+.2f);
                var fire = owner.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.name=="Status burn 0");
                float attackingWidth = fire.bounds.size.x;
                Require(Mathf.Abs(view.StatusBounds.size.y-neutral.bounds.size.y*1.7f)<.003f,
                    "Attack artwork/pose changed neutral status size on first application.");
                presentation.SetPhase(TrickalFanGame.Enemy.EnemyAttackPhase.Idle);body.sprite = neutral;
                view.RefreshAt(Time.time+.2f);
                Require(Mathf.Abs(fire.bounds.size.x-attackingWidth)<.003f,
                    "Status size changed when attack ended.");
                owner.transform.localScale *= 2f;view.RefreshAt(Time.time+.2f);
                Require(Mathf.Abs(fire.bounds.size.x-attackingWidth*2f)<.003f,
                    "Real character growth stopped resizing status effects.");
            }
            finally { Object.DestroyImmediate(owner);Object.DestroyImmediate(attackSprite); }
        }
        private static void Require(bool condition,string message)
        { if(!condition) throw new InvalidOperationException(message); }
    }
}
