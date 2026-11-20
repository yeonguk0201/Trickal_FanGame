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
        public static int PlayVoice(CombatSfxCue cue)
        {
            RequestCounts[(int)cue]++;
            GameAudioController controller = GameAudioController.Instance;
            return controller != null ? controller.PlayCombatVoice(cue) : 0;
        }

        public static void StopVoice(int handle, float fadeSeconds = 0.08f)
        {
            if (handle <= 0) return;
            GameAudioController.Instance?.StopCombatVoice(handle, fadeSeconds);
        }
    }
}
