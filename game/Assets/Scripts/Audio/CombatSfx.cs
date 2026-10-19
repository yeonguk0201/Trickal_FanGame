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
        public static bool Play(CombatSfxCue cue)
        {
            GameAudioController controller = GameAudioController.Instance;
            return controller != null && controller.PlayCombatCue(cue);
        }
    }
}
