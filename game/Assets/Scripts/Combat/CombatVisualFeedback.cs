using TrickalFanGame.Player;
using TrickalFanGame.Enemy;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class CombatVisualFeedback : MonoBehaviour
    {
        private Health health;
        private PlayerSP sp;
        private SpriteRenderer source;
        private SpriteRenderer overlay;
        private MaterialPropertyBlock properties;
        private BossMovementAnimator bossArtwork;
        private readonly List<MeshRenderer> meshes = new();
        private float hitStartedAt;
        private float hitEndsAt;
        private float nextHealAt;

        private void Awake()
        {
            health = GetComponent<Health>();
            sp = GetComponent<PlayerSP>();
            source = GetComponentInChildren<SpriteRenderer>();
            bossArtwork = GetComponent<BossMovementAnimator>();
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (health == null) Awake();
            if (health != null) { health.Healed += HandleHeal; health.DamageApplied += HandleDamage; }
            if (sp != null) sp.Restored += HandleSP;
        }

        private void OnDisable()
        {
            if (health != null) { health.Healed -= HandleHeal; health.DamageApplied -= HandleDamage; }
            if (sp != null) sp.Restored -= HandleSP;
            hitEndsAt = 0f;
            if (overlay != null) overlay.enabled = false;
            SetMeshFlash(0f);
        }

        private void HandleHeal(float amount)
        {
            if (sp == null || source == null || Time.time < nextHealAt) return;
            nextHealAt = Time.time + 0.35f;
            Vector3 feet = new Vector3(source.bounds.center.x, source.bounds.min.y, transform.position.z);
            float width = Mathf.Clamp(source.bounds.size.x * 1.6f, 1.2f, 2.4f);
            CombatSpriteEffect.Play("hp-heal", feet, width, width, 0.8f, transform, source,
                true, pivot: new Vector2(0.5f, 0.12f));
        }

        private void HandleSP() => SPAbsorptionEffect.Play(transform, source);

        private void HandleDamage(DamageContext context, float damage, float remaining)
        {
            if (sp != null || damage <= 0f || remaining <= 0f) return;
            hitStartedAt = Time.time;
            hitEndsAt = Time.time + 0.28f;
        }

        private void LateUpdate()
        {
            if (sp != null || source == null) return;
            float strength = Time.time < hitEndsAt && Mathf.FloorToInt((Time.time - hitStartedAt) / 0.055f) % 2 == 0 ? 1f : 0f;
            SetMeshFlash(strength);
            // Animated bosses hide the original sprite and display their drawn body on a separate renderer.
            SpriteRenderer visible = bossArtwork != null && bossArtwork.Artwork != null
                ? bossArtwork.Artwork : source;
            if (strength > 0f && overlay == null)
            {
                Material material = CombatEffectArtwork.Material("EnemyHitOverlay");
                if (material == null) return;
                var owner = new GameObject("VFX enemy hit overlay");
                owner.transform.SetParent(visible.transform, false);
                overlay = CombatEffectArtwork.Renderer(owner, visible.sprite, visible, 1);
                overlay.sharedMaterial = material;
            }
            if (overlay == null) return;
            if (overlay.transform.parent != visible.transform) overlay.transform.SetParent(visible.transform, false);
            overlay.enabled = strength > 0f && visible.enabled && !visible.forceRenderingOff;
            overlay.sprite = visible.sprite;
            overlay.flipX = visible.flipX;
            overlay.flipY = visible.flipY;
            overlay.color = new Color(1f, 1f, 1f, visible.color.a);
            overlay.sortingLayerID = visible.sortingLayerID;
            overlay.sortingOrder = visible.sortingOrder + 1;
        }

        private void SetMeshFlash(float strength)
        {
            // Hop animation renders a deformed mesh. Its shader accepts the same outline/flash strength.
            GetComponentsInChildren(true, meshes);
            foreach (MeshRenderer mesh in meshes)
            {
                if (mesh.sharedMaterial == null || !mesh.sharedMaterial.HasProperty("_HitStrength")) continue;
                mesh.GetPropertyBlock(properties);
                properties.SetFloat("_HitStrength", strength);
                mesh.SetPropertyBlock(properties);
            }
        }
    }
}
