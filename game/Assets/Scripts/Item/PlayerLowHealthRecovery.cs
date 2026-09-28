using TrickalFanGame.Combat;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // Life Gem: once per floor, when HP falls to the threshold, heal a max-HP ratio in half-heart pulses.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerLowHealthRecovery : MonoBehaviour
    {
        private const float ThresholdTolerance = 0.0001f;

        private Health health;
        private RunProgress runProgress;
        private bool subscribed;
        private float healthThreshold;
        private float healRatio;
        private float durationSeconds;
        private float startedAt;
        private int totalPulses;
        private int deliveredPulses;
        private float pulseAmount;
        private int currentFloor = 1;
        private int consumedFloor;

        public bool IsConfigured { get; private set; }
        public bool IsConsumed => consumedFloor == currentFloor;
        public bool IsActive => IsConsumed && deliveredPulses < totalPulses;
        public float HealthThreshold => healthThreshold;
        public float HealRatio => healRatio;
        public float DurationSeconds => durationSeconds;
        public float StartedAt => startedAt;
        public int TotalPulses => totalPulses;
        public int DeliveredPulses => deliveredPulses;
        public float PulseAmount => pulseAmount;

        private void Update()
        {
            Tick(Time.time);
        }

        private void OnDestroy()
        {
            if (subscribed && health != null)
            {
                health.Changed -= OnHealthChanged;
            }

            if (runProgress != null)
            {
                runProgress.RoomChanged -= OnRoomChanged;
            }
        }

        public void BindRunProgress(RunProgress configuredRunProgress)
        {
            if (runProgress == configuredRunProgress)
            {
                return;
            }

            if (runProgress != null)
            {
                runProgress.RoomChanged -= OnRoomChanged;
            }

            runProgress = configuredRunProgress;
            if (runProgress != null)
            {
                runProgress.RoomChanged += OnRoomChanged;
                if (runProgress.CurrentFloor > 0)
                {
                    currentFloor = runProgress.CurrentFloor;
                }
            }
        }

        public bool Configure(float configuredThreshold, float configuredHealRatio, float configuredDuration)
        {
            if (IsConfigured || configuredThreshold <= 0f || configuredThreshold > 1f ||
                configuredHealRatio <= 0f || configuredDuration <= 0f)
            {
                return false;
            }

            EnsureReferences();
            healthThreshold = configuredThreshold;
            healRatio = configuredHealRatio;
            durationSeconds = configuredDuration;
            IsConfigured = true;
            // Acquiring the gem while already below the threshold starts it immediately.
            TryTrigger(Time.time);
            return true;
        }

        public bool TryTrigger(float currentTime)
        {
            if (!IsConfigured || IsConsumed || health == null || health.IsDead || health.CurrentHealth <= 0f ||
                health.CurrentHealth > health.MaxHealth * healthThreshold + ThresholdTolerance)
            {
                return false;
            }

            float totalHeal = health.GetMaxHealthRatioAmount(healRatio);
            if (totalHeal <= 0f)
            {
                return false;
            }

            consumedFloor = currentFloor;
            startedAt = currentTime;
            deliveredPulses = 0;
            // Unit-mode totals are whole units, so each pulse restores exactly half a heart.
            totalPulses = Mathf.Max(1, Mathf.CeilToInt(totalHeal - ThresholdTolerance));
            pulseAmount = totalHeal / totalPulses;
            return true;
        }

        public int Tick(float currentTime)
        {
            if (!IsActive)
            {
                return 0;
            }

            if (health == null || health.IsDead)
            {
                deliveredPulses = totalPulses;
                return 0;
            }

            int duePulses = Mathf.Min(
                totalPulses,
                Mathf.FloorToInt((currentTime - startedAt) / durationSeconds * totalPulses + ThresholdTolerance));
            int delivered = 0;
            while (deliveredPulses < duePulses)
            {
                deliveredPulses++;
                health.Heal(pulseAmount);
                delivered++;
            }

            return delivered;
        }

        private void OnHealthChanged(float current, float maximum)
        {
            TryTrigger(Time.time);
        }

        private void OnRoomChanged(int floor, int room)
        {
            int nextFloor = Mathf.Max(1, floor);
            if (nextFloor == currentFloor)
            {
                return;
            }

            currentFloor = nextFloor;
            deliveredPulses = totalPulses;
            TryTrigger(Time.time);
        }

        private void EnsureReferences()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (!subscribed && health != null)
            {
                health.Changed += OnHealthChanged;
                subscribed = true;
            }
        }
    }
}
