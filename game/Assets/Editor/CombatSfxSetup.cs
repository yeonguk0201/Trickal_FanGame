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

        // Sound pass 2026-11-21: cues that stay in the enum only so the numbers after them
        // do not move. They carry no clip and nothing plays them.
        public static readonly CombatSfxCue[] RetiredCues =
        {
            CombatSfxCue.SkillUpperStepLeft,
            CombatSfxCue.SkillUpperStepRight,
        };

        public static bool IsRetired(CombatSfxCue cue) => Array.IndexOf(RetiredCues, cue) >= 0;

        // cue, clip file, volume, minimum interval between two plays, and the
        // longest the clip may be. The length cap is per cue because it comes
        // from how often the game plays it: a sound that fires every 0.35s
        // may not run for seconds, while a one-off cast may.
        public static readonly (CombatSfxCue Cue, string File, float Volume, float Interval, float MaxSeconds)[] Cues =
        {
            (CombatSfxCue.BasicAttack, "SFX_Attack_Basic.wav", 0.25f, 0.03f, 1.5f),
            (CombatSfxCue.HitEnemy, "SFX_Hit_Punch.mp3", 0.76f, 0.05f, 1.2f),
            (CombatSfxCue.HitCritical, "SFX_Hit_Critical_Slap.mp3", 0.37f, 0.05f, 1.2f),
            // A poisoned pack ticks on the same frame, so this guard is the
            // longest of the set: one sound per tick wave, not one per enemy.
            (CombatSfxCue.EnemyTickDamage, "SFX_Enemy_Tick_Hitmarker.mp3", 0.49f, 0.12f, 1f),
            (CombatSfxCue.PlayerHurt, "SFX_Player_Hurt.wav", 0.65f, 0.1f, 1.5f),
            // The lower-grade skill read as too quiet next to the rest, so
            // its three cues sit well above the basic attack now.
            (CombatSfxCue.SkillLowerCast, "SFX_Skill_Lower_Cast.wav", 0.61f, 0.05f, 3f),
            // The four shots are 0.08s apart and the explosions land close
            // together, so these guards only drop same-frame repeats.
            (CombatSfxCue.SkillLowerShot, "SFX_Skill_Lower_Shot_Heavy.wav", 0.71f, 0.02f, 1f),
            (CombatSfxCue.SkillLowerBurst, "SFX_Skill_Lower_Burst_8Bit.mp3", 0.79f, 0.03f, 1f),
            (CombatSfxCue.SkillUpperDash, "SFX_Skill_Upper_Dash.wav", 0.49f, 0.1f, 1.5f),
            // Sound pass 2026-11-21: the running loops on a voice while the dash lasts (up
            // to ten seconds), so its length cap is only a sanity bound.
            (CombatSfxCue.SkillUpperRun, "SFX_Skill_Upper_Run.mp3", 0.7f, 0.1f, 6f),
            (CombatSfxCue.SkillUpperImpact, "SFX_Skill_Upper_Impact_Thud.mp3", 0.47f, 0.1f, 2.5f),
            // World events. Each happens once at a time, so they carry the
            // loosest guards and may run longer.
            (CombatSfxCue.BossPortal, "SFX_Boss_Portal.wav", 0.56f, 0.15f, 3f),
            (CombatSfxCue.BombExplosion, "SFX_Bomb_Explosion.wav", 0.79f, 0.06f, 3f),
            (CombatSfxCue.MuffinExplosion, "SFX_Muffin_Explosion.wav", 0.49f, 0.06f, 2f),
            // Sound pass 2026-11-21: spells, 짱셈 spells and the 권능 each have an impact of
            // their own from one family (lentikula's healing spell impacts).
            (CombatSfxCue.SpellCast, "SFX_Spell_Impact.wav", 0.83f, 0.15f, 3f),
            (CombatSfxCue.JjangsemSpellCast, "SFX_Jjangsem_Spell_Impact.wav", 0.61f, 0.15f, 3f),
            (CombatSfxCue.PowerCast, "SFX_Power_Impact.wav", 0.74f, 0.15f, 3f),
            (CombatSfxCue.SpGain, "SFX_SP_Gain.wav", 0.46f, 0.12f, 2f),
            (CombatSfxCue.Heal, "SFX_Heal.wav", 0.59f, 0.15f, 2f),
            (CombatSfxCue.DoorOpen, "SFX_Door_Open.wav", 0.7f, 0.15f, 2f),
            (CombatSfxCue.ItemDrop, "SFX_Item_Drop_Cork.mp3", 0.75f, 0.06f, 1.2f),
            // Sound-1: the 크레용사's sword. Swings and dashes come at least a
            // retarget apart, so the guard only drops same-frame repeats.
            (CombatSfxCue.BossSwordSwing, "SFX_Boss_Sword_Swing.mp3", 0.47f, 0.08f, 2f),
            // Sound pass 2026-11-21: the charged map slash is three layers: this swing, then
            // the magic burst and the clash at CrayonHeroBossPatternRuntime's
            // SlamMagicDelay and SlamClashDelay.
            (CombatSfxCue.BossSwordSlam, "SFX_Boss_Sword_Slam_Swing.mp3", 0.8f, 0.1f, 2f),
            (CombatSfxCue.BossSwordSlamMagic, "SFX_Boss_Sword_Slam_Magic.mp3", 0.6f, 0.1f, 3.5f),
            (CombatSfxCue.BossSwordSlamClash, "SFX_Boss_Sword_Slam_Clash.mp3", 0.6f, 0.1f, 2.5f),
            // The charge plays on a voice cut by the slam; the transformation
            // plays its first three seconds and fades over the last 1.5.
            (CombatSfxCue.BossCrayonCharge, "SFX_Boss_Crayon_Charge.mp3", 0.6f, 0.1f, 7.5f),
            (CombatSfxCue.BossCrayonTransform, "SFX_Boss_Crayon_Transform.mp3", 0.6f, 0.15f, 10f),
            (CombatSfxCue.BossCrayonSummon, "SFX_Boss_Crayon_Summon.mp3", 0.7f, 0.15f, 2.5f),
            (CombatSfxCue.ObstacleHit, "SFX_Obstacle_Hit.mp3", 1f, 0.05f, 1.2f),
            // A room full of enemies attacks often; keep it under the hits
            // the player lands, and fold a volley into one sound.
            (CombatSfxCue.EnemyAttack, "SFX_Enemy_Attack.mp3", 0.59f, 0.08f, 1f),
            (CombatSfxCue.EnemyDefeated, "SFX_Enemy_Defeated.mp3", 0.75f, 0.05f, 1.5f),
            // Plays once per boss, over the intro; its dark tail runs on into
            // the fight, so the cap is the clip's own length.
            (CombatSfxCue.BossIntroVs, "SFX_Boss_Intro_VS.mp3", 0.69f, 0.15f, 9f),
            // Sound-2: the 새마음금고. Pillars emerge 0.1s or more apart and
            // each is meant to be heard, so the guard stays short.
            (CombatSfxCue.VaultPillarEmerge, "SFX_Vault_Pillar_Earth.wav", 0.77f, 0.05f, 3f),
            // The whistle and the meteor wind play on voices that are cut at
            // the impact, so their length is only an upper bound.
            (CombatSfxCue.VaultJumpWhistle, "SFX_Vault_Jump_Whistle.mp3", 0.5f, 0.1f, 2.5f),
            (CombatSfxCue.VaultJumpLanding, "SFX_Vault_Jump_Landing.mp3", 0.9f, 0.1f, 2.5f),
            // Meteors land every 0.35-0.45s and overlap, so both sit lower.
            (CombatSfxCue.VaultMeteorFall, "SFX_Vault_Meteor_Fall.mp3", 0.45f, 0.1f, 2.5f),
            (CombatSfxCue.VaultMeteorImpact, "SFX_Vault_Meteor_Impact.mp3", 0.6f, 0.1f, 3.5f),
            (CombatSfxCue.BossObstacleLanding, "SFX_Boss_Obstacle_Landing.mp3", 0.85f, 0.1f, 2f),
            (CombatSfxCue.PortalAppear, "SFX_Portal_Appear.mp3", 0.9f, 0.15f, 3f),
            (CombatSfxCue.LevelUp, "SFX_Level_Up.mp3", 0.57f, 0.15f, 3f),
            (CombatSfxCue.SpellTeleport, "SFX_Spell_Teleport.wav", 0.56f, 0.15f, 2.5f),
            (CombatSfxCue.UiCancel, "SFX_UI_Cancel.wav", 1f, 0.05f, 0.5f),
            (CombatSfxCue.EnemyAppear, "SFX_Enemy_Appear.wav", 0.6f, 0.1f, 0.5f),
            // Sound-3: a held arrow key repeats every 0.09s, so the guard
            // stays below that and every step is heard.
            (CombatSfxCue.UiFocusMove, "SFX_UI_Focus_Move.wav", 0.6f, 0.03f, 0.5f),
            // A chest spills several keys and bombs that can be swept up in
            // one step; the guard folds a same-frame sweep into one sound.
            (CombatSfxCue.ItemPickup, "SFX_Item_Pickup.mp3", 0.7f, 0.05f, 2f),
            // Gold spills several coins at once; one sound per sweep.
            (CombatSfxCue.GoldPickup, "SFX_Gold_Pickup.wav", 0.7f, 0.08f, 1.5f),
            (CombatSfxCue.RoomTransition, "SFX_Room_Transition.wav", 0.7f, 0.15f, 3f),
            (CombatSfxCue.ChestOpen, "SFX_Chest_Open.mp3", 1f, 0.1f, 2f),
            // Two footfalls every 0.5s of walking: quiet, and the guard sits
            // under the 0.25s step so none is dropped.
            (CombatSfxCue.PlayerStep, "SFX_Player_Step.wav", 0.07f, 0.1f, 1f),
            // Sound pass 2026-11-21: flying beats the wings once per 0.5s walk cycle instead.
            (CombatSfxCue.PlayerWingFlap, "SFX_Player_Wing_Flap.mp3", 0.57f, 0.1f, 1f),
            // Sound pass 2026-11-21: a purchase or investment that went through in the shop.
            (CombatSfxCue.ShopPurchase, "SFX_Shop_Purchase.mp3", 0.57f, 0.1f, 2.5f),
            // The chewing plays on a voice cut when the heal ends, so its
            // length is only an upper bound.
            (CombatSfxCue.VaultEat, "SFX_Vault_Eat.mp3", 0.75f, 0.15f, 11f),
            // One per lump; lumps leave 0.08s apart, so the guard stays below that.
            (CombatSfxCue.VaultSlimeThrow, "SFX_Vault_Slime_Throw.wav", 0.8f, 0.03f, 1f),
            // One per drawn hop takeoff of the walk.
            (CombatSfxCue.VaultMove, "SFX_Vault_Move.wav", 0.73f, 0.15f, 1f),
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
