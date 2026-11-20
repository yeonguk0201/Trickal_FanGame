using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>The truck scene left by <see cref="CrayonHeroTruckDefeat"/>. It has no collider.</summary>
    [DisallowMultipleComponent]
    public sealed class CrayonHeroTruckDefeatView : MonoBehaviour
    {
        public const string ObjectName = "Crayon Hero Truck Defeat";
        public const string StandingObjectName = "Standing Boss";
        public const string TruckObjectName = "Truck";
        public const string ShadowObjectName = "Standing Boss Shadow";

        public enum Stage { Waiting, Approaching, Impact, Leaving, Done }

        // A copy of the owner's settings: the boss is removed in the same frame it dies.
        private Sprite[] driveFrames, impactFrames;
        private Rect[] driveRects, impactRects;
        private float[] impactSeconds;
        private float wait, approach, approachDistance, impactTravel, exit, exitDistance, acceleration, wheelFps;
        private float shakePeak, shakeSeconds, impactDuration;

        private SpriteRenderer standing, truck, shadow;
        private Vector3 feet;
        private float bossHeight;
        private float age;
        private bool shaken;

        public float Age => age;
        public Stage CurrentStage { get; private set; }
        public int ImpactFrame { get; private set; } = -1;
        public SpriteRenderer Standing => standing;
        public SpriteRenderer Truck => truck;
        public Vector3 Feet => feet;
        public float BossHeight => bossHeight;
        public float Lifetime => wait + approach + impactDuration + exit;

        public static CrayonHeroTruckDefeatView Spawn(CrayonHeroTruckDefeat owner, SpriteRenderer body)
        {
            GameObject root = new(ObjectName, typeof(CrayonHeroTruckDefeatView));
            // Stay with the room, so leaving it also clears the scene.
            Transform room = owner.transform.parent;
            root.transform.SetParent(room, false);
            root.transform.localScale = Vector3.one;
            Bounds bounds = body.bounds;
            var view = root.GetComponent<CrayonHeroTruckDefeatView>();
            view.feet = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * owner.BodyBottomShare,
                owner.transform.position.z);
            root.transform.position = view.feet;
            view.bossHeight = Mathf.Max(0.01f, bounds.size.y * owner.BodyVisibleShare);
            view.driveFrames = owner.DriveFrames;
            view.driveRects = owner.DriveRects;
            view.impactFrames = owner.ImpactFrames;
            view.impactRects = owner.ImpactRects;
            view.impactSeconds = owner.ImpactSeconds;
            view.wait = owner.WaitSeconds;
            view.approach = owner.ApproachSeconds;
            view.approachDistance = owner.ApproachDistance;
            view.impactTravel = owner.ImpactTravel * view.bossHeight;
            view.exit = owner.ExitSeconds;
            view.exitDistance = owner.ExitDistance;
            view.acceleration = owner.ExitAcceleration;
            view.wheelFps = owner.WheelFramesPerSecond;
            view.shakePeak = owner.ShakePeak;
            view.shakeSeconds = owner.ShakeSeconds;
            view.impactDuration = owner.ImpactDuration;
            view.anchor = view.feet + (Vector3)(owner.AnchorFromFeet * view.bossHeight);

            // The boss stays standing, as it last looked, until the truck reaches it.
            view.standing = view.CreateRenderer(StandingObjectName, body);
            view.standing.sprite = body.sprite;
            view.standing.flipX = body.flipX;
            view.standing.transform.position = body.transform.position;
            view.SetWorldScale(view.standing.transform, body.transform.lossyScale);
            view.standing.color = new Color(1f, 1f, 1f, body.color.a);
            view.CopyShadow(owner);

            view.truck = view.CreateRenderer(TruckObjectName, body);
            view.truck.sortingOrder = body.sortingOrder + 1;
            view.truck.enabled = false;
            view.Advance(0f);
            return view;
        }

        private Vector3 anchor;

        private SpriteRenderer CreateRenderer(string objectName, SpriteRenderer like)
        {
            GameObject child = new(objectName, typeof(SpriteRenderer));
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            var renderer = child.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = like.sortingLayerID;
            renderer.sortingOrder = like.sortingOrder;
            return renderer;
        }

        private void SetWorldScale(Transform target, Vector3 world)
        {
            Vector3 parent = target.parent != null ? target.parent.lossyScale : Vector3.one;
            target.localScale = new Vector3(world.x / NonZero(parent.x), world.y / NonZero(parent.y), 1f);
        }

        private static float NonZero(float value) => Mathf.Abs(value) < 0.0001f ? 0.0001f : value;

        // The boss's last ground shadow stays under the standing copy until the hit.
        private void CopyShadow(CrayonHeroTruckDefeat owner)
        {
            GroundShadow source = owner.GetComponent<GroundShadow>();
            SpriteRenderer last = source != null ? source.Visual : null;
            if (last == null || last.sprite == null) return;
            shadow = CreateRenderer(ShadowObjectName, last);
            shadow.sprite = last.sprite;
            shadow.color = last.color;
            shadow.transform.position = last.transform.position;
            SetWorldScale(shadow.transform, last.transform.lossyScale);
        }

        private void Update() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            age += Mathf.Max(0f, deltaTime);
            float impactStart = wait + approach;
            bool standingVisible = age < impactStart;
            standing.enabled = standingVisible;
            if (shadow != null) shadow.enabled = standingVisible;

            if (age < wait)
            {
                CurrentStage = Stage.Waiting;
                truck.enabled = false;
            }
            else if (age < impactStart)
            {
                CurrentStage = Stage.Approaching;
                float q = (age - wait) / approach;
                Place(Wheel(), -approachDistance * (1f - q), false);
            }
            else if (age < impactStart + impactDuration)
            {
                CurrentStage = Stage.Impact;
                if (!shaken)
                {
                    shaken = true;
                    if (shakePeak > 0f && shakeSeconds > 0f) CameraShake.Play(shakePeak, shakeSeconds);
                }
                float elapsed = age - impactStart, acc = 0f;
                int index = impactFrames.Length - 1;
                for (int i = 0; i < impactFrames.Length; i++)
                {
                    acc += Mathf.Max(0.001f, impactSeconds[i]);
                    if (elapsed < acc) { index = i; break; }
                }
                ImpactFrame = index;
                Place(index, impactTravel * Mathf.Clamp01(elapsed / impactDuration), true);
            }
            else if (age < Lifetime)
            {
                CurrentStage = Stage.Leaving;
                ImpactFrame = -1;
                float q = (age - impactStart - impactDuration) / exit;
                Place(Wheel(), impactTravel + exitDistance * (q * (1f - acceleration) + q * q * acceleration), false);
            }
            else
            {
                CurrentStage = Stage.Done;
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
        }

        private int Wheel() => Mathf.FloorToInt(age * wheelFps) % driveFrames.Length;

        // Puts picture index (a wheel or crash frame) at its rect, travel units forward of the hit point.
        private void Place(int index, float travel, bool impact)
        {
            Sprite sprite = impact ? impactFrames[index] : driveFrames[index];
            Rect rect = impact ? impactRects[index] : driveRects[index];
            truck.enabled = true;
            truck.sprite = sprite;
            float width = rect.width * bossHeight, height = rect.height * bossHeight;
            Vector3 centre = anchor + new Vector3(travel + (rect.x * bossHeight) + width / 2f,
                rect.y * bossHeight + height / 2f, 0f);
            truck.transform.position = centre;
            float spriteHeight = Mathf.Max(0.0001f, sprite.bounds.size.y);
            float scale = height / spriteHeight;
            SetWorldScale(truck.transform, new Vector3(scale, scale, 1f));
        }
    }
}
