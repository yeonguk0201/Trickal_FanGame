using System;
using UnityEngine;

namespace TrickalFanGame.Audio
{
    // Sound-0: how world objects ask for a sound. The player's own cues go
    // through PlayerCombatAudio, but a bomb, a door, a portal or a dropped
    // item has no player to hang a subscription on, so those call this
    // directly — the same shape as CombatSpriteEffect.Play for visuals.
    //
    // It is safe before the controller exists (fixtures, verifiers, a scene
    // loaded without audio), where it simply plays nothing.
    public static class CombatSfx
    {
        private static readonly int[] RequestCounts = new int[Enum.GetValues(typeof(CombatSfxCue)).Length];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetRequestCounts() => Array.Clear(RequestCounts, 0, RequestCounts.Length);

        // Sound-1: how many times a world cue was asked for, whether or not a
        // controller was there to play it, so the verifier can check routing
        // without audio hardware.
        public static int GetRequestCount(CombatSfxCue cue) => RequestCounts[(int)cue];

        public static bool Play(CombatSfxCue cue)
        {
            RequestCounts[(int)cue]++;
            GameAudioController controller = GameAudioController.Instance;
            return controller != null && controller.PlayCombatCue(cue);
        }

        // Sound-2: a cue that must stop when its moment ends. Keep the handle
        // and pass it to StopVoice; 0 means nothing played.
        public static int PlayVoice(CombatSfxCue cue, bool loop = false)
        {
            RequestCounts[(int)cue]++;
            GameAudioController controller = GameAudioController.Instance;
            return controller != null ? controller.PlayCombatVoice(cue, loop) : 0;
        }

        // Sound pass 2026-11-21: a later layer of the same moment, `delay` seconds from now.
        // Counted when asked for, so routing checks do not have to wait.
        public static void PlayAfter(CombatSfxCue cue, float delay)
        {
            RequestCounts[(int)cue]++;
            GameAudioController.Instance?.PlayCombatCueAfter(cue, delay);
        }

        // Sound pass 2026-11-21: plays a voice for `seconds` in all, its last `fadeSeconds`
        // fading out; for a long clip of which only the start is wanted.
        public static int PlayVoiceFor(CombatSfxCue cue, float seconds, float fadeSeconds)
        {
            int handle = PlayVoice(cue);
            if (handle > 0)
                GameAudioController.Instance?.StopCombatVoiceAfter(handle, Mathf.Max(0f, seconds - fadeSeconds),
                    fadeSeconds);
            return handle;
        }

        public static void StopVoice(int handle, float fadeSeconds = 0.08f)
        {
            if (handle <= 0) return;
            GameAudioController.Instance?.StopCombatVoice(handle, fadeSeconds);
        }
    }
}
