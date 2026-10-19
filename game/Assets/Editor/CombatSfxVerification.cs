using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Audio;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Sound-0: the invariants that make the combat sounds audible and correct.
    // Each check fails loudly, because a silent sound is easy to miss by eye
    // and the whole point of this piece is that it is heard.
    public static class CombatSfxVerification
    {
        [MenuItem("Trickal Fan Game/Audio/Verify Combat SFX")]
        public static void Verify()
        {
            VerifyClips();
            VerifyCueTable();
            VerifyScene(Week13FrontendSetup.ScenePath, false);
            VerifyScene(Week13FrontendSetup.GameScenePath, true);
            VerifyRetriggerGuard();
            VerifyRouting();
            Debug.Log("Combat SFX verification passed.");
        }

        [MenuItem("Trickal Fan Game/Audio/Setup and Verify Combat SFX")]
        public static void SetupAndVerifyBatch()
        {
            CombatSfxSetup.Setup();
            Verify();
        }

        // Sound-0 added an event to the basic attack, the dash and the homing
        // shot, so the three systems that raise them are re-checked here.
        public static void VerifyWithRegressionsBatch()
        {
            CombatSfxSetup.Setup();
            Verify();
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            Week7HighGradeSkillVerification.Verify();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Week7LowerGradeSkillVerification.Verify();
            Week21Range0Verification.Verify();
            Debug.Log("Combat SFX regression verification passed.");
        }

        // Every cue has a clip, and every clip is a short, uncompressed,
        // preloaded one-shot. A streamed hit sound arrives late.
        private static void VerifyClips()
        {
            foreach (CombatSfxCue cue in Enum.GetValues(typeof(CombatSfxCue)).Cast<CombatSfxCue>())
            {
                string path = CombatSfxSetup.PathFor(cue);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert(clip != null, $"{cue} has no imported clip at {path}.");
                float cap = MaxSecondsOf(cue);
                Assert(clip.length > 0f && clip.length <= cap,
                    $"{path} is {clip.length:0.###}s; {cue} must stay within {cap:0.###}s.");
                Assert(AssetImporter.GetAtPath(path) is AudioImporter importer &&
                       importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad &&
                       importer.defaultSampleSettings.preloadAudioData,
                    $"{path} must import as DecompressOnLoad with preloaded data.");
            }
        }

        // Each cue appears exactly once, audibly, and its guard is shorter
        // than the gap the game actually plays it at.
        private static void VerifyCueTable()
        {
            CombatSfxCue[] all = Enum.GetValues(typeof(CombatSfxCue)).Cast<CombatSfxCue>().ToArray();
            foreach (CombatSfxCue cue in all)
            {
                int count = CombatSfxSetup.Cues.Count(entry => entry.Cue == cue);
                Assert(count == 1, $"{cue} must appear exactly once in the cue table, found {count}.");
            }

            foreach ((CombatSfxCue cue, string file, float volume, float interval, float cap)
                     in CombatSfxSetup.Cues)
            {
                Assert(volume > 0f && volume <= 1f, $"{cue} needs an audible volume, found {volume}.");
                Assert(interval >= 0f && interval < 0.2f,
                    $"{cue} guard of {interval}s is too long; it would swallow real repeats.");
                Assert(!string.IsNullOrEmpty(file), $"{cue} has no clip file.");
                Assert(cap > 0f, $"{cue} needs a length cap.");
            }

            // The salvo fires every 0.08s and its blasts land about 0.07s
            // apart, so these two guards must stay below that.
            Assert(GuardOf(CombatSfxCue.SkillLowerShot) < 0.08f,
                "The lower-grade shot guard must stay under the 0.08s firing interval.");
            Assert(GuardOf(CombatSfxCue.SkillLowerBurst) < 0.07f,
                "The lower-grade burst guard must stay under the gap between two blasts.");

            // Each foot repeats every other cadence, so its guard has to fit
            // inside that, or the run would lose steps.
            float footInterval = CombatSfxSetup.DashStepInterval * 2f;
            Assert(GuardOf(CombatSfxCue.SkillUpperStepLeft) < footInterval &&
                   GuardOf(CombatSfxCue.SkillUpperStepRight) < footInterval,
                $"A step guard must stay under {footInterval:0.###}s, the gap between two falls of the same foot.");

            // The lower-grade skill was raised deliberately; keep it above
            // the basic attack so it does not sink back under the music.
            Assert(VolumeOf(CombatSfxCue.SkillLowerCast) > VolumeOf(CombatSfxCue.BasicAttack) &&
                   VolumeOf(CombatSfxCue.SkillLowerBurst) > VolumeOf(CombatSfxCue.BasicAttack),
                "The lower-grade cast and burst must stay louder than the basic attack.");

            // A tick is background damage; it must not compete with the hit
            // the player actually aimed, and its guard is the longest.
            Assert(VolumeOf(CombatSfxCue.EnemyTickDamage) < VolumeOf(CombatSfxCue.HitEnemy),
                "The tick cue must stay quieter than the basic attack blast.");
            Assert(GuardOf(CombatSfxCue.EnemyTickDamage) > GuardOf(CombatSfxCue.HitEnemy),
                "The tick cue needs a longer guard than the blast: a whole pack ticks at once.");
        }

        private static float GuardOf(CombatSfxCue cue)
        {
            return CombatSfxSetup.Cues.First(entry => entry.Cue == cue).Interval;
        }

        private static float VolumeOf(CombatSfxCue cue)
        {
            return CombatSfxSetup.Cues.First(entry => entry.Cue == cue).Volume;
        }

        private static float MaxSecondsOf(CombatSfxCue cue)
        {
            return CombatSfxSetup.Cues.First(entry => entry.Cue == cue).MaxSeconds;
        }

        private static void VerifyScene(string scenePath, bool expectPlayerRouter)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameAudioController[] controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameAudioController>(true)).ToArray();
            Assert(controllers.Length == 1, $"{scenePath} must contain exactly one GameAudioController.");
            GameAudioController controller = controllers[0];

            foreach (CombatSfxCue cue in Enum.GetValues(typeof(CombatSfxCue)).Cast<CombatSfxCue>())
            {
                AudioClip clip = controller.GetCombatClip(cue);
                Assert(clip != null, $"{scenePath} has no clip wired for {cue}.");
                Assert(AssetDatabase.GetAssetPath(clip) == CombatSfxSetup.PathFor(cue),
                    $"{scenePath} wires {cue} to {AssetDatabase.GetAssetPath(clip)}, " +
                    $"not {CombatSfxSetup.PathFor(cue)}.");
            }

            PlayerCombatAudio[] routers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerCombatAudio>(true)).ToArray();
            if (!expectPlayerRouter)
            {
                Assert(routers.Length == 0, $"{scenePath} has no player, so it must carry no PlayerCombatAudio.");
                return;
            }

            Assert(routers.Length == 1, $"{scenePath} must contain exactly one PlayerCombatAudio.");
            Assert(routers[0].GetComponent<PlayerCombatEvents>() != null,
                "PlayerCombatAudio must sit on the player, beside PlayerCombatEvents.");
        }

        // Two plays in the same frame collapse into one; after the guard
        // expires the cue plays again.
        private static void VerifyRetriggerGuard()
        {
            GameObject fixture = new("Combat SFX fixture");
            try
            {
                GameAudioController controller = fixture.AddComponent<GameAudioController>();
                AudioClip clip = AudioClip.Create("probe", 1024, 1, 44100, false);
                controller.ConfigureCombatSfx(new[]
                {
                    new GameAudioController.CombatSfxEntry
                    {
                        cue = CombatSfxCue.HitEnemy, clip = clip, volume = 0.6f, minimumInterval = 10f,
                    },
                    new GameAudioController.CombatSfxEntry
                    {
                        cue = CombatSfxCue.BasicAttack, clip = clip, volume = 0.5f, minimumInterval = 0f,
                    },
                });

                Assert(controller.PlayCombatCue(CombatSfxCue.HitEnemy), "The first hit must play.");
                Assert(!controller.PlayCombatCue(CombatSfxCue.HitEnemy),
                    "A second hit inside the guard must be dropped.");
                Assert(controller.GetCombatCuePlayCount(CombatSfxCue.HitEnemy) == 1,
                    "A dropped repeat must not count as a play.");
                Assert(controller.PlayCombatCue(CombatSfxCue.BasicAttack) &&
                       controller.PlayCombatCue(CombatSfxCue.BasicAttack),
                    "A cue with no guard must play every time it is asked for.");
                Assert(!controller.PlayCombatCue(CombatSfxCue.PlayerHurt),
                    "A cue with no entry must report that nothing played.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        // The player's events reach the cues they are meant to, and the two
        // that must stay quiet do: a blocked hit, and a dash that ends
        // without touching an enemy.
        private static void VerifyRouting()
        {
            GameObject player = new("Combat SFX player fixture");
            try
            {
                Health health = player.AddComponent<Health>();
                Invoke(health, "Awake");
                PlayerCombatEvents events = player.AddComponent<PlayerCombatEvents>();
                PlayerCombatAudio router = player.AddComponent<PlayerCombatAudio>();
                Invoke(router, "Awake");
                Invoke(router, "OnEnable");

                GameObject enemy = new("Combat SFX enemy fixture");
                try
                {
                    Health enemyHealth = enemy.AddComponent<Health>();
                    Invoke(enemyHealth, "Awake");
                    DamageContext basic = new(player, DamageSourceType.PlayerProjectile, 1f);

                    Report(events, enemyHealth, basic, 1f);
                    Assert(router.GetRequestCount(CombatSfxCue.HitEnemy) == 1,
                        "A basic attack hit must ask for the hit cue.");

                    Report(events, enemyHealth, basic, 0f);
                    Assert(router.GetRequestCount(CombatSfxCue.HitEnemy) == 1,
                        "A hit that applied no damage must stay silent.");

                    health.TakeDamage(new DamageContext(enemy, DamageSourceType.EnemyContact, 1f));
                    Assert(router.GetRequestCount(CombatSfxCue.PlayerHurt) == 1,
                        "Damage to the player must ask for the hurt cue.");

                    // A critical replaces the ordinary hit instead of adding
                    // a second sound on the same frame.
                    DamageContext crit = new(player, DamageSourceType.PlayerProjectile, 1f,
                        criticalChance: 1f, criticalDamageMultiplier: 2f, criticalRollOverride: 0f);
                    router.OnAnyDamageResolved(enemyHealth, crit, new DamageResult(2f, true));
                    Report(events, enemyHealth, crit, 2f);
                    Assert(router.GetRequestCount(CombatSfxCue.HitCritical) == 1,
                        "A critical basic attack must play the critical cue.");
                    Assert(router.GetRequestCount(CombatSfxCue.HitEnemy) == 1,
                        "A critical must not also play the ordinary hit cue.");

                    // The critical flag must not carry into the next hit.
                    Report(events, enemyHealth, basic, 1f);
                    Assert(router.GetRequestCount(CombatSfxCue.HitEnemy) == 2 &&
                           router.GetRequestCount(CombatSfxCue.HitCritical) == 1,
                        "The hit after a critical must be an ordinary hit again.");

                    // A tick is told apart from a hit by its delivery type.
                    enemyHealth.TakeDamage(new DamageContext(player, DamageSourceType.PlayerStatusEffect, 1f,
                        deliveryType: DamageDeliveryType.Periodic));
                    Assert(router.GetRequestCount(CombatSfxCue.EnemyTickDamage) == 1,
                        "A damage-over-time tick on an enemy must ask for the tick cue.");
                    Assert(router.GetRequestCount(CombatSfxCue.HitEnemy) == 2,
                        "A tick must not also play the basic attack blast.");

                    enemyHealth.TakeDamage(new DamageContext(player, DamageSourceType.PlayerProjectile, 1f));
                    Assert(router.GetRequestCount(CombatSfxCue.EnemyTickDamage) == 1,
                        "Direct damage must not play the tick cue.");

                    // The player's own damage over time stays the hurt sound.
                    health.TakeDamage(new DamageContext(enemy, DamageSourceType.Environment, 1f,
                        deliveryType: DamageDeliveryType.Periodic));
                    Assert(router.GetRequestCount(CombatSfxCue.EnemyTickDamage) == 1,
                        "A tick on the player is the hurt cue, never the enemy tick cue.");

                    Raise(router, "OnDashEnded", UltimateEndReason.Timeout);
                    Raise(router, "OnDashEnded", UltimateEndReason.Cancelled);
                    Assert(router.GetRequestCount(CombatSfxCue.SkillUpperImpact) == 0,
                        "A dash that ran out or was cancelled must not play the impact.");
                    Raise(router, "OnDashEnded", UltimateEndReason.Impact);
                    Assert(router.GetRequestCount(CombatSfxCue.SkillUpperImpact) == 1,
                        "A dash that lands on an enemy must play the impact.");

                    VerifyDashSteps(router);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(enemy);
                }
            }
            finally
            {
                // The router holds a static subscription, and a fixture that
                // was enabled by reflection is not disabled by destroying it.
                Invoke(player.GetComponent<PlayerCombatAudio>(), "OnDisable");
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        // The run has to last as long as the dash does, and the two feet have
        // to alternate, or a long dash falls silent or repeats one sample.
        private static void VerifyDashSteps(PlayerCombatAudio router)
        {
            router.ConfigureDashSteps(CombatSfxSetup.DashStepInterval);
            Assert(Mathf.Approximately(router.DashStepInterval, CombatSfxSetup.DashStepInterval),
                "The router must take the cadence the setup configures.");

            router.BeginDashSteps(0f);
            // Six seconds of dash, pumped a frame at a time.
            for (float t = 0f; t <= 6f; t += 1f / 60f) router.PumpDashSteps(t);

            int left = router.GetRequestCount(CombatSfxCue.SkillUpperStepLeft);
            int right = router.GetRequestCount(CombatSfxCue.SkillUpperStepRight);
            int expected = (int)(6f / CombatSfxSetup.DashStepInterval);
            Assert(left + right >= expected - 1,
                $"A six-second dash must keep stepping: expected about {expected} falls, got {left + right}.");
            Assert(Mathf.Abs(left - right) <= 1,
                $"The feet must alternate, got {left} left and {right} right.");
        }

        private static void Report(PlayerCombatEvents events, Health target, DamageContext damage, float applied)
        {
            MethodInfo method = typeof(PlayerCombatEvents).GetMethod(
                "TryReportBasicAttackHit", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("PlayerCombatEvents.TryReportBasicAttackHit is gone.");
            method.Invoke(events, new object[] { target, damage, applied });
        }

        private static void Raise(PlayerCombatAudio router, string handler, params object[] arguments)
        {
            MethodInfo method = typeof(PlayerCombatAudio).GetMethod(
                handler, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException($"PlayerCombatAudio.{handler} is gone.");
            method.Invoke(router, arguments);
        }

        private static void Invoke(object target, string method)
        {
            if (target == null) return;
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, Array.Empty<object>());
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Combat SFX verification failed: {message}");
        }
    }
}
