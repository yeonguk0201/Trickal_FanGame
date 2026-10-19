using System;
using System.IO;
using System.Linq;
using TrickalFanGame.Audio;
using TrickalFanGame.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Sound-0: wires the combat sounds. The clip paths are stable keys, so
    // replacing a sound means overwriting the file at the same path and
    // running this again; nothing here names a candidate.
    public static class CombatSfxSetup
    {
        public const string ClipRoot = "Assets/Audio/SFX";

        // Seconds between two footfalls while the upper-grade dash runs.
        public const float DashStepInterval = 0.17f;

        // cue, clip file, volume, minimum interval between two plays, and the
        // longest the clip may be. The length cap is per cue because it comes
        // from how often the game plays it: a sound that fires every 0.35s
        // may not run for seconds, while a one-off cast may.
        public static readonly (CombatSfxCue Cue, string File, float Volume, float Interval, float MaxSeconds)[] Cues =
        {
            (CombatSfxCue.BasicAttack, "SFX_Attack_Basic.wav", 0.5f, 0.03f, 1.5f),
            (CombatSfxCue.HitEnemy, "SFX_Hit_Enemy.wav", 0.7f, 0.05f, 1f),
            (CombatSfxCue.HitCritical, "SFX_Hit_Critical.wav", 0.9f, 0.05f, 1.2f),
            // A poisoned pack ticks on the same frame, so this guard is the
            // longest of the set: one sound per tick wave, not one per enemy.
            (CombatSfxCue.EnemyTickDamage, "SFX_Enemy_Tick.wav", 0.5f, 0.12f, 1f),
            (CombatSfxCue.PlayerHurt, "SFX_Player_Hurt.wav", 0.85f, 0.1f, 1.5f),
            // The lower-grade skill read as too quiet next to the rest, so
            // its three cues sit well above the basic attack now.
            (CombatSfxCue.SkillLowerCast, "SFX_Skill_Lower_Cast.wav", 0.95f, 0.05f, 3f),
            // The four shots are 0.08s apart and the explosions land close
            // together, so these guards only drop same-frame repeats.
            (CombatSfxCue.SkillLowerShot, "SFX_Skill_Lower_Shot.wav", 0.62f, 0.02f, 0.5f),
            (CombatSfxCue.SkillLowerBurst, "SFX_Skill_Lower_Burst.wav", 0.85f, 0.03f, 1f),
            (CombatSfxCue.SkillUpperDash, "SFX_Skill_Upper_Dash.wav", 0.6f, 0.1f, 1.5f),
            // One footfall each, alternating every PlayerCombatAudio
            // cadence (0.17s), so each foot repeats every 0.34s.
            (CombatSfxCue.SkillUpperStepLeft, "SFX_Skill_Upper_Step_Left.wav", 0.7f, 0.1f, 0.5f),
            (CombatSfxCue.SkillUpperStepRight, "SFX_Skill_Upper_Step_Right.wav", 0.7f, 0.1f, 0.5f),
            (CombatSfxCue.SkillUpperImpact, "SFX_Skill_Upper_Impact.wav", 0.9f, 0.1f, 1.5f),
            // World events. Each happens once at a time, so they carry the
            // loosest guards and may run longer.
            (CombatSfxCue.BossPortal, "SFX_Boss_Portal.wav", 0.8f, 0.15f, 3f),
            (CombatSfxCue.BombExplosion, "SFX_Bomb_Explosion.wav", 0.85f, 0.06f, 3f),
            (CombatSfxCue.MuffinExplosion, "SFX_Muffin_Explosion.wav", 0.7f, 0.06f, 2f),
            (CombatSfxCue.SpellCast, "SFX_Spell_Cast.wav", 0.9f, 0.15f, 3f),
            (CombatSfxCue.SpGain, "SFX_SP_Gain.wav", 0.7f, 0.12f, 2f),
            (CombatSfxCue.Heal, "SFX_Heal.wav", 0.7f, 0.15f, 2f),
            (CombatSfxCue.DoorOpen, "SFX_Door_Open.wav", 0.7f, 0.15f, 2f),
            (CombatSfxCue.ItemDrop, "SFX_Item_Drop.wav", 0.75f, 0.06f, 1f),
        };

        public static string PathFor(CombatSfxCue cue)
        {
            for (int i = 0; i < Cues.Length; i++)
                if (Cues[i].Cue == cue) return $"{ClipRoot}/{Cues[i].File}";
            throw new InvalidOperationException($"No clip path is configured for {cue}.");
        }

        [MenuItem("Trickal Fan Game/Audio/Setup Combat SFX")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Combat SFX setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            GameAudioController.CombatSfxEntry[] entries = BuildEntries();
            ConfigureScene(Week13FrontendSetup.ScenePath, entries, false);
            ConfigureScene(Week13FrontendSetup.GameScenePath, entries, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"Combat SFX setup completed for {entries.Length} cues in both scenes.");
        }

        public static GameAudioController.CombatSfxEntry[] BuildEntries()
        {
            return Cues.Select(cue =>
            {
                string path = $"{ClipRoot}/{cue.File}";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                    throw new InvalidOperationException($"Combat SFX clip is missing or failed to import: {path}");
                ConfigureImporter(path);
                return new GameAudioController.CombatSfxEntry
                {
                    cue = cue.Cue,
                    clip = clip,
                    volume = cue.Volume,
                    minimumInterval = cue.Interval,
                };
            }).ToArray();
        }

        private static void ConfigureScene(string scenePath, GameAudioController.CombatSfxEntry[] entries,
            bool wirePlayer)
        {
            if (!File.Exists(scenePath)) throw new InvalidOperationException($"Scene is missing: {scenePath}");
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameAudioController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameAudioController>(true))
                .FirstOrDefault();
            if (controller == null)
            {
                throw new InvalidOperationException(
                    $"{scenePath} has no GameAudioController. Run Setup Setting-1A Audio first.");
            }

            controller.ConfigureCombatSfx(entries);
            EditorUtility.SetDirty(controller);

            if (wirePlayer)
            {
                PlayerCombatEvents player = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PlayerCombatEvents>(true))
                    .FirstOrDefault();
                if (player == null)
                    throw new InvalidOperationException($"{scenePath} has no player to attach PlayerCombatAudio to.");
                // Re-running must not stack a second router on the player.
                PlayerCombatAudio router = player.GetComponent<PlayerCombatAudio>();
                if (router == null) router = Undo.AddComponent<PlayerCombatAudio>(player.gameObject);
                router.ConfigureDashSteps(DashStepInterval);
                EditorUtility.SetDirty(router);
                EditorUtility.SetDirty(player.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void ConfigureImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                throw new InvalidOperationException($"Audio importer is unavailable: {path}");
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            // Short one-shots: keep them uncompressed and resident, so a hit
            // sound never arrives a frame late.
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            AudioImporterSampleSettings current = importer.defaultSampleSettings;
            if (current.loadType == settings.loadType &&
                current.compressionFormat == settings.compressionFormat &&
                current.preloadAudioData == settings.preloadAudioData &&
                !importer.forceToMono)
            {
                return;
            }

            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.SaveAndReimport();
        }
    }
}
