using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Audio
{
    // Sound pass 2026-11-22: sounds that follow what is in the showing room rather than a single moment. Only the
    // current room's content is active (RoomNode.SetVisible), so whatever this finds is in the player's room.
    //
    // - Hazard loops (fire trap, 독버섯 gas, damage puddle): one looping voice per kind. Each source is heard at the
    //   cue's full volume within Near units of the player's feet and at FarRatio of it from Far units on, linear in
    //   between; the loudest source sets the loop. A fire trap also steps down with each hit it takes (StageGains)
    //   over StageFadeSeconds and dies away over OffFadeSeconds when put out. Values tuned in tools/sound-mixer.
    // - Movement loops: every 쥬비 alive and every 잠복형 moving underground has a loop of its own (up to
    //   MaxEnemyLoops of each kind, the nearest first), each starting at a different point of the clip.
    // - Walking and hopping enemies: each steps every EnemyStepInterval on an offset of its own while it moves.
    // Everything goes quiet while the game is paused (time scale 0).
    [DisallowMultipleComponent]
    public sealed class CombatAmbienceAudio : MonoBehaviour
    {
        public const float NearDistance = 1f;
        public const float FarDistance = 6f;
        public const float FireFarRatio = 0.1f;
        public const float GasFarRatio = 0.1f;
        public const float SwampFarRatio = 0.24f;
        // Fire trap gain by hits taken (0, 1, 2); a third hit puts it out.
        public static readonly float[] FireStageGains = { 1f, 0.65f, 0.2f };
        public const float StageFadeSeconds = 0.25f;
        public const float OffFadeSeconds = 0.8f;
        // A gas cloud ending, a room being left or an enemy dying fades its loop over this.
        public const float LeaveFadeSeconds = 0.3f;
        public const float EnemyStepInterval = 0.35f;
        public const int MaxEnemyLoops = 4;
        private const float ScanInterval = 0.2f;

        private sealed class Source
        {
            public Transform Transform;
            public Vector2 Position;
            public float Gain;
            public float Target;
            public float RampPerSecond;
            public bool Seen;
            public int Handle;
        }

        private sealed class Loop
        {
            public CombatSfxCue Cue;
            public float FarRatio;
            public bool ByDistance;
            // One voice per source (enemies) instead of one per kind (hazards).
            public bool PerSource;
            public readonly Dictionary<Object, Source> Sources = new();
            public int Handle;
        }

        private readonly Loop fire = new() { Cue = CombatSfxCue.FireTrapLoop, FarRatio = FireFarRatio, ByDistance = true };
        private readonly Loop gas = new() { Cue = CombatSfxCue.PoisonGasLoop, FarRatio = GasFarRatio, ByDistance = true };
        private readonly Loop swamp = new() { Cue = CombatSfxCue.PoisonSwampLoop, FarRatio = SwampFarRatio, ByDistance = true };
        private readonly Loop jyubi = new() { Cue = CombatSfxCue.JyubiMoveLoop, PerSource = true };
        private readonly Loop ambush = new() { Cue = CombatSfxCue.AmbushMoveLoop, PerSource = true };
        private Loop[] loops;
        // Each moving enemy's next footfall (unscaled time); an enemy that stops is dropped and starts afresh.
        private readonly Dictionary<EnemyMovementAnimator, float> walkers = new();
        private readonly List<EnemyMovementAnimator> stepScratch = new();
        private readonly List<Source> rankScratch = new();
        private PlayerMovement player;
        private float nextScanTime;

        public int ActiveLoopCount { get; private set; }

        private void Awake() => loops = new[] { fire, gas, swamp, jyubi, ambush };

        private void Update()
        {
            GameAudioController controller = GameAudioController.Instance;
            if (controller == null) return;
            float now = Time.unscaledTime;
            if (now >= nextScanTime)
            {
                nextScanTime = now + ScanInterval;
                Scan();
            }

            bool paused = Time.timeScale <= 0f;
            Vector2 listener = player != null ? player.FeetPosition : Vector2.zero;
            ActiveLoopCount = 0;
            foreach (Loop loop in loops)
            {
                if (loop.PerSource) TickPerSource(controller, loop, listener, paused, Time.unscaledDeltaTime);
                else TickShared(controller, loop, listener, paused, Time.unscaledDeltaTime);
            }

            TickSteps(paused, now);
        }

        private void Scan()
        {
            if (player == null || !player.isActiveAndEnabled)
                player = FindAnyObjectByType<PlayerMovement>();
            foreach (Loop loop in loops)
                foreach (Source source in loop.Sources.Values) source.Seen = false;

            foreach (RoomTrap trap in FindObjectsByType<RoomTrap>(FindObjectsSortMode.None))
            {
                if (trap.Kind == RoomTrapKind.Fire)
                {
                    DestructibleObstacle obstacle = trap.Obstacle;
                    if (obstacle == null || obstacle.IsBroken) continue;
                    int hits = Mathf.Clamp(obstacle.HitsTaken, 0, FireStageGains.Length - 1);
                    See(fire, trap, trap.transform, FireStageGains[hits], StageFadeSeconds);
                }
                else if (trap.Kind == RoomTrapKind.DamagePuddle && !trap.IsRemoved)
                    See(swamp, trap, trap.transform, 1f, LeaveFadeSeconds);
            }

            foreach (PoisonGasCloud cloud in FindObjectsByType<PoisonGasCloud>(FindObjectsSortMode.None))
                if (!cloud.HasEnded) See(gas, cloud, cloud.transform, 1f, LeaveFadeSeconds);

            foreach (EnemyFlight flight in FindObjectsByType<EnemyFlight>(FindObjectsSortMode.None))
                if (IsAlive(flight)) See(jyubi, flight, flight.transform, 1f, LeaveFadeSeconds);

            foreach (AmbushEnemyController burrower in FindObjectsByType<AmbushEnemyController>(FindObjectsSortMode.None))
                if (IsAlive(burrower) && burrower.State == AmbushEnemyController.AmbushState.Burrowed)
                    See(ambush, burrower, burrower.transform, 1f, LeaveFadeSeconds);

            foreach (Loop loop in loops)
            {
                foreach (KeyValuePair<Object, Source> pair in loop.Sources)
                {
                    if (pair.Value.Seen) continue;
                    // A fire put out by its third hit dies away; anything else that left fades quickly.
                    bool putOut = loop == fire && pair.Key is RoomTrap trap && trap != null &&
                                  trap.Obstacle != null && trap.Obstacle.IsBroken;
                    SetTarget(pair.Value, 0f, putOut ? OffFadeSeconds : LeaveFadeSeconds);
                }
            }

            stepScratch.Clear();
            foreach (EnemyMovementAnimator walker in FindObjectsByType<EnemyMovementAnimator>(FindObjectsSortMode.None))
            {
                if (!IsAlive(walker)) continue;
                if (walker.GetComponent<EnemyFlight>() != null || walker.GetComponent<AmbushEnemyController>() != null)
                    continue;
                stepScratch.Add(walker);
            }

            // Keep the timers of enemies still here; forget the rest.
            List<EnemyMovementAnimator> gone = null;
            foreach (EnemyMovementAnimator walker in walkers.Keys)
                if (!stepScratch.Contains(walker)) (gone ??= new List<EnemyMovementAnimator>()).Add(walker);
            if (gone != null) foreach (EnemyMovementAnimator walker in gone) walkers.Remove(walker);
            foreach (EnemyMovementAnimator walker in stepScratch) walkers.TryAdd(walker, -1f);
        }

        private static bool IsAlive(Component enemy)
        {
            Health health = enemy.GetComponent<Health>();
            return health == null || !health.IsDead;
        }

        private static void See(Loop loop, Object key, Transform transform, float target, float fadeSeconds)
        {
            if (!loop.Sources.TryGetValue(key, out Source source))
            {
                // A source the room shows on entry starts at its level; only later changes ramp.
                source = new Source { Gain = target, Target = target };
                loop.Sources.Add(key, source);
            }

            source.Seen = true;
            source.Transform = transform;
            source.Position = transform.position;
            SetTarget(source, target, fadeSeconds);
        }

        private static void SetTarget(Source source, float target, float fadeSeconds)
        {
            if (Mathf.Approximately(source.Target, target) && source.RampPerSecond > 0f) return;
            source.Target = target;
            source.RampPerSecond = fadeSeconds > 0f ? Mathf.Max(0.01f, Mathf.Abs(source.Gain - target) / fadeSeconds)
                : float.PositiveInfinity;
        }

        public static float DistanceGain(float distance, float farRatio)
        {
            float t = Mathf.Clamp01((distance - NearDistance) / (FarDistance - NearDistance));
            return Mathf.Lerp(1f, farRatio, t);
        }

        // Ramps every source and drops the ones that have faded out, stopping a voice a dropped source held.
        private static void Ramp(GameAudioController controller, Loop loop, float deltaTime)
        {
            List<Object> gone = null;
            foreach (KeyValuePair<Object, Source> pair in loop.Sources)
            {
                Source source = pair.Value;
                source.Gain = Mathf.MoveTowards(source.Gain, source.Target, source.RampPerSecond * deltaTime);
                if (source.Seen && source.Transform != null) source.Position = source.Transform.position;
                if (source.Gain > 0f || source.Target > 0f) continue;
                if (source.Handle > 0) controller.StopCombatVoice(source.Handle, 0f);
                (gone ??= new List<Object>()).Add(pair.Key);
            }

            if (gone != null) foreach (Object key in gone) loop.Sources.Remove(key);
        }

        private void TickShared(GameAudioController controller, Loop loop, Vector2 listener, bool paused, float deltaTime)
        {
            Ramp(controller, loop, deltaTime);
            float level = 0f;
            foreach (Source source in loop.Sources.Values)
            {
                float distanceGain = loop.ByDistance
                    ? player != null ? DistanceGain(Vector2.Distance(listener, source.Position), loop.FarRatio) : 0f
                    : 1f;
                level = Mathf.Max(level, source.Gain * distanceGain);
            }

            if (paused) level = 0f;
            bool playing = controller.IsCombatVoicePlaying(loop.Handle);
            if (loop.Sources.Count == 0)
            {
                if (playing) controller.StopCombatVoice(loop.Handle, 0f);
                loop.Handle = 0;
                return;
            }

            ActiveLoopCount++;
            if (!playing)
            {
                loop.Handle = controller.PlayCombatVoice(loop.Cue, true, 1f, level);
                if (loop.Handle > 0) CombatSfx.CountRequest(loop.Cue);
            }
            else controller.SetCombatVoiceGain(loop.Handle, level);
        }

        private void TickPerSource(GameAudioController controller, Loop loop, Vector2 listener, bool paused,
            float deltaTime)
        {
            Ramp(controller, loop, deltaTime);
            // The nearest MaxEnemyLoops are heard; the rest give up their voices until they come closer.
            rankScratch.Clear();
            rankScratch.AddRange(loop.Sources.Values);
            rankScratch.Sort((a, b) => (a.Position - listener).sqrMagnitude.CompareTo((b.Position - listener).sqrMagnitude));
            for (int i = 0; i < rankScratch.Count; i++)
            {
                Source source = rankScratch[i];
                bool heard = i < MaxEnemyLoops;
                bool playing = controller.IsCombatVoicePlaying(source.Handle);
                if (!heard)
                {
                    if (playing) controller.StopCombatVoice(source.Handle);
                    source.Handle = 0;
                    continue;
                }

                ActiveLoopCount++;
                float level = paused ? 0f : source.Gain;
                if (!playing)
                {
                    // The retrigger guard lets one copy start per 0.1s, so a pack's loops come in one after another.
                    source.Handle = controller.PlayCombatVoice(loop.Cue, true, 1f, level, Random.Range(0f, 30f));
                    if (source.Handle > 0) CombatSfx.CountRequest(loop.Cue);
                }
                else controller.SetCombatVoiceGain(source.Handle, level);
            }
        }

        private void TickSteps(bool paused, float now)
        {
            if (paused) return;
            stepScratch.Clear();
            stepScratch.AddRange(walkers.Keys);
            foreach (EnemyMovementAnimator walker in stepScratch)
            {
                if (walker == null || !walker.isActiveAndEnabled || !walker.IsAnimating)
                {
                    walkers[walker] = -1f;
                    continue;
                }

                float next = walkers[walker];
                if (next < 0f)
                {
                    // Starting to move: the first footfall lands somewhere in the first interval, so a pack that sets
                    // off together does not step in unison.
                    walkers[walker] = now + Random.Range(0f, EnemyStepInterval);
                    continue;
                }

                if (now < next) continue;
                walkers[walker] = next + EnemyStepInterval > now ? next + EnemyStepInterval : now + EnemyStepInterval;
                CombatSfx.Play(CombatSfxCue.EnemyStep);
            }
        }

        private void OnDisable()
        {
            GameAudioController controller = GameAudioController.Instance;
            foreach (Loop loop in loops ?? System.Array.Empty<Loop>())
            {
                if (controller != null)
                {
                    controller.StopCombatVoice(loop.Handle, 0f);
                    foreach (Source source in loop.Sources.Values) controller.StopCombatVoice(source.Handle, 0f);
                }

                loop.Handle = 0;
                loop.Sources.Clear();
            }

            walkers.Clear();
        }
    }
}
