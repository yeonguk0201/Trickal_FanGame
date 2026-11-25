using System;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    /// <summary>
    /// Procedural idle loop for a 2D Animation standing rig: the chest bone rises and falls for breathing
    /// and each listed bone swings around its rest rotation. Chained bones with growing phase offsets read
    /// as hair or cloth trailing behind the motion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StandingIdleMotion : MonoBehaviour
    {
        [Serializable]
        public struct BoneSway
        {
            public Transform bone;
            [Tooltip("Peak rotation in degrees around the rest pose.")]
            public float amplitude;
            [Tooltip("Seconds per full swing.")]
            public float period;
            [Tooltip("Radians. Later bones in a chain use larger values so the tip trails the root.")]
            public float phase;

            public BoneSway(Transform bone, float amplitude, float period, float phase)
            {
                this.bone = bone;
                this.amplitude = amplitude;
                this.period = period;
                this.phase = phase;
            }
        }

        [SerializeField] private Transform breathBone;
        [Tooltip("World units the chest rises at the top of a breath.")]
        [SerializeField] private float breathHeight = 0.012f;
        [SerializeField] private float breathPeriod = 3.2f;
        [SerializeField] private BoneSway[] sways = Array.Empty<BoneSway>();
        [Tooltip("Scales every breath and sway amplitude. 0 freezes the rest pose.")]
        [SerializeField, Range(0f, 2f)] private float intensity = 1f;
        [Tooltip("Keeps moving while the game is paused, as menus and loading screens do.")]
        [SerializeField] private bool useUnscaledTime = true;

        private Vector3 breathRest;
        private Quaternion[] swayRest = Array.Empty<Quaternion>();
        private bool captured;

        public Transform BreathBone => breathBone;
        public float BreathHeight => breathHeight;
        public float BreathPeriod => breathPeriod;
        public float Intensity => intensity;
        public int SwayCount => sways.Length;
        public BoneSway GetSway(int index) => sways[index];

        public void Configure(Transform breath, float height, float period, BoneSway[] boneSways, float motionIntensity = 1f)
        {
            ReleaseRestPose();
            breathBone = breath;
            breathHeight = height;
            breathPeriod = period;
            sways = boneSways ?? Array.Empty<BoneSway>();
            intensity = motionIntensity;
        }

        private void OnEnable() => CaptureRestPose();

        private void OnDisable() => ReleaseRestPose();

        private void Update() => Apply(useUnscaledTime ? Time.unscaledTime : Time.time);

        /// <summary>Poses every bone for the given time. The rest pose is captured on first use.</summary>
        public void Apply(float time)
        {
            CaptureRestPose();
            if (breathBone != null && breathPeriod > 0f)
            {
                float breath = 0.5f - 0.5f * Mathf.Cos(time * Mathf.PI * 2f / breathPeriod);
                Vector3 rise = transform.up * (breathHeight * breath * intensity);
                Transform parent = breathBone.parent;
                breathBone.localPosition = breathRest + (parent != null ? parent.InverseTransformVector(rise) : rise);
            }

            for (int i = 0; i < sways.Length; i++)
            {
                BoneSway sway = sways[i];
                if (sway.bone == null || sway.period <= 0f) continue;
                float angle = sway.amplitude * intensity * Mathf.Sin(time * Mathf.PI * 2f / sway.period - sway.phase);
                sway.bone.localRotation = swayRest[i] * Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>Puts every bone back to the rest pose captured before motion started.</summary>
        public void ReleaseRestPose()
        {
            if (!captured) return;
            if (breathBone != null) breathBone.localPosition = breathRest;
            for (int i = 0; i < sways.Length && i < swayRest.Length; i++)
                if (sways[i].bone != null) sways[i].bone.localRotation = swayRest[i];
            captured = false;
        }

        private void CaptureRestPose()
        {
            if (captured) return;
            if (breathBone != null) breathRest = breathBone.localPosition;
            swayRest = new Quaternion[sways.Length];
            for (int i = 0; i < sways.Length; i++)
                swayRest[i] = sways[i].bone != null ? sways[i].bone.localRotation : Quaternion.identity;
            captured = true;
        }
    }
}
