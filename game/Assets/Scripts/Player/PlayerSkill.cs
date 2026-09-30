using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace TrickalFanGame.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(PlayerSP), typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerCombatEvents), typeof(PlayerActionState))]
    public sealed class PlayerSkill : MonoBehaviour
    {
        private const int BaseProjectileCount = 4;
        private static readonly float[] BaseProjectileSlotOffsets = { -1.5f, 0.5f, -0.5f, 1.5f };

        [SerializeField] private HomingSkillProjectile projectilePrefab;
        [SerializeField, Min(0f)] private float spawnOffset = 0.65f;
        [FormerlySerializedAs("untargetedSpreadAngle")]
        [SerializeField, Range(0f, 45f)] private float fanSpacingAngle = 12f;
        [SerializeField, Min(0f)] private float shotInterval = 0.08f;
        [SerializeField] private LayerMask targetLayers = 1 << 6;

        private readonly List<Health> targets = new();
        private Health health;
        private PlayerSP playerSP;
        private PlayerStats stats;
        private PlayerMovement movement;
        private PlayerActionState actionState;
        private Health[] salvoTargets = Array.Empty<Health>();
        private DamageContext salvoDamageContext;
        private Vector2 salvoDirection;
        private int nextProjectileIndex;
        private int salvoProjectileCount;
        private float salvoStartTime;
        private float nextShotTime;
        private bool isFiring;
        private int progressionLevel = SkillProgressionRules.MinimumLevel;

        public bool IsFiring => isFiring;
        public int ProgressionLevel => progressionLevel;
        public float ProgressionDamageMultiplier =>
            SkillProgressionRules.DamageMultiplier(progressionLevel);
        public int ProgressionProjectileBonus =>
            SkillProgressionRules.LowerGradeProjectileBonus(progressionLevel);
        public float ShotInterval => shotInterval;
        public float FanSpacingAngle => fanSpacingAngle;
        public bool CanCast => health != null && !health.IsDead && !IsFiring &&
            actionState != null && actionState.CanUseLowerGradeSkill && projectilePrefab != null &&
            playerSP != null && playerSP.CurrentSP > 0;
        public event Action<HomingSkillProjectile> ProjectileLaunched;
        public event Action SalvoStarted;

        private void Awake()
        {
            health = GetComponent<Health>();
            playerSP = GetComponent<PlayerSP>();
            stats = GetComponent<PlayerStats>();
            movement = GetComponent<PlayerMovement>();
            actionState = GetComponent<PlayerActionState>();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                TryCast();
            }
        }

        public void Configure(
            HomingSkillProjectile configuredProjectilePrefab,
            LayerMask configuredTargetLayers,
            float configuredShotInterval = 0.08f,
            float configuredFanSpacingAngle = 12f)
        {
            projectilePrefab = configuredProjectilePrefab;
            targetLayers = configuredTargetLayers;
            shotInterval = Mathf.Max(0f, configuredShotInterval);
            fanSpacingAngle = Mathf.Clamp(configuredFanSpacingAngle, 0f, 45f);
        }

        public bool TryCast()
        {
            Vector2 direction = movement != null && movement.FacingDirection.sqrMagnitude > 0.001f
                ? movement.FacingDirection
                : Vector2.down;
            return TryCast(direction, Time.time);
        }

        public bool TryCast(Vector2 initialDirection, float currentTime)
        {
            if (health == null)
            {
                Awake();
            }

            if (health.IsDead || IsFiring || !actionState.CanUseLowerGradeSkill || projectilePrefab == null)
            {
                return false;
            }

            int projectileBonus = stats.GetLowerGradeSkillProjectileBonus(playerSP.CurrentSP);
            if (!playerSP.TrySpend())
            {
                return false;
            }

            FindTargets();
            salvoTargets = targets.ToArray();
            salvoDirection = initialDirection.sqrMagnitude > 0.001f
                ? initialDirection.normalized
                : Vector2.down;
            salvoProjectileCount = BaseProjectileCount +
                SkillProgressionRules.LowerGradeProjectileBonus(progressionLevel) +
                projectileBonus;
            salvoDamageContext = stats.CreateDirectDamageContext(
                gameObject,
                DamageSourceType.PlayerSkillExplosion,
                SkillProgressionRules.DamageMultiplier(progressionLevel) * stats.SkillDamageMultiplier);
            nextProjectileIndex = 0;
            salvoStartTime = currentTime;
            nextShotTime = currentTime;
            isFiring = true;
            SalvoStarted?.Invoke();
            FireNextProjectile();

            Debug.Log(
                $"[PlayerSkill] Started {salvoProjectileCount}-shot homing salvo. SP {playerSP.CurrentSP}/{playerSP.MaxSP}",
                this);
            return true;
        }

        public void ApplyProgressionLevel(int level)
        {
            progressionLevel = SkillProgressionRules.ClampLevel(level);
        }

        public void Tick(float currentTime)
        {
            while (IsFiring && currentTime >= nextShotTime)
            {
                FireNextProjectile();
            }
        }

        private void FireNextProjectile()
        {
            int shotIndex = nextProjectileIndex;
            Health target = salvoTargets.Length == 0
                ? null
                : salvoTargets[shotIndex % salvoTargets.Length];
            float fanAngle = GetFanSlotOffset(shotIndex) * fanSpacingAngle;
            Vector2 direction = Rotate(salvoDirection, fanAngle);
            HomingSkillProjectile projectile = Instantiate(
                projectilePrefab,
                transform.position + (Vector3)(direction * spawnOffset),
                Quaternion.identity);
            projectile.Launch(direction, health, target, salvoDamageContext, targetLayers);
            ProjectileLaunched?.Invoke(projectile);

            nextProjectileIndex++;
            if (nextProjectileIndex >= salvoProjectileCount)
            {
                isFiring = false;
                salvoTargets = Array.Empty<Health>();
                Debug.Log($"[PlayerSkill] Fired {salvoProjectileCount} homing projectiles.", this);
            }
            else
            {
                nextShotTime = salvoStartTime + shotInterval * nextProjectileIndex;
            }
        }

        private static float GetFanSlotOffset(int shotIndex)
        {
            if (shotIndex < BaseProjectileSlotOffsets.Length)
            {
                return BaseProjectileSlotOffsets[shotIndex];
            }

            int outerIndex = shotIndex - BaseProjectileSlotOffsets.Length;
            float distanceFromCenter = 2.5f + outerIndex / 2;
            return outerIndex % 2 == 0 ? -distanceFromCenter : distanceFromCenter;
        }

        private void FindTargets()
        {
            targets.Clear();
            foreach (Health candidate in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (candidate != null && candidate != health && !candidate.IsDead &&
                    (targetLayers.value & (1 << candidate.gameObject.layer)) != 0 &&
                    candidate.GetComponent<PlayerCombatEvents>() == null)
                {
                    targets.Add(candidate);
                }
            }

            targets.Sort((left, right) =>
                ((Vector2)(left.transform.position - transform.position)).sqrMagnitude.CompareTo(
                    ((Vector2)(right.transform.position - transform.position)).sqrMagnitude));
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine).normalized;
        }
    }
}
