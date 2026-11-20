using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Audio;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
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
            VerifyWorldRouting();
            VerifyCancelButtonNames();
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
            foreach (CombatSfxCue cue in ActiveCues())
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
                int expected = CombatSfxSetup.IsRetired(cue) ? 0 : 1;
                Assert(count == expected, CombatSfxSetup.IsRetired(cue)
                    ? $"{cue} is retired and must not appear in the cue table, found {count}."
                    : $"{cue} must appear exactly once in the cue table, found {count}.");
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

            // Sound pass 2026-11-21: the slam's later layers land after the swing, in order.
            Assert(CrayonHeroBossPatternRuntime.SlamMagicDelay > 0f &&
                   CrayonHeroBossPatternRuntime.SlamClashDelay > CrayonHeroBossPatternRuntime.SlamMagicDelay,
                "The slam's magic layer must follow the swing and come before the clash.");
            Assert(CrayonHeroBossPatternRuntime.TransformFadeSeconds > 0f &&
                   CrayonHeroBossPatternRuntime.TransformFadeSeconds <= CrayonHeroBossPatternRuntime.TransformSoundSeconds,
                "The transformation must fade out within the time it plays.");

            // The lower-grade skill was raised deliberately; keep it above
            // the basic attack so it does not sink back under the music.
            Assert(VolumeOf(CombatSfxCue.SkillLowerCast) > VolumeOf(CombatSfxCue.BasicAttack) &&
                   VolumeOf(CombatSfxCue.SkillLowerBurst) > VolumeOf(CombatSfxCue.BasicAttack),
                "The lower-grade cast and burst must stay louder than the basic attack.");

            // A tick is background damage, so a whole pack ticking at once
            // must collapse into one sound. Its loudness against the hit is
            // tuned by ear in tools/sound-mixer (the files differ in level,
            // so the table numbers alone cannot order them).
            Assert(GuardOf(CombatSfxCue.EnemyTickDamage) > GuardOf(CombatSfxCue.HitEnemy),
                "The tick cue needs a longer guard than the blast: a whole pack ticks at once.");
        }

        private static CombatSfxCue[] ActiveCues()
        {
            return Enum.GetValues(typeof(CombatSfxCue)).Cast<CombatSfxCue>()
                .Where(cue => !CombatSfxSetup.IsRetired(cue)).ToArray();
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

            foreach (CombatSfxCue cue in CombatSfxSetup.RetiredCues)
                Assert(controller.GetCombatClip(cue) == null, $"{scenePath} still wires the retired cue {cue}.");
            foreach (CombatSfxCue cue in ActiveCues())
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

                // Sound pass 2026-11-21: a delayed layer waits, then plays once.
                int before = controller.GetCombatCuePlayCount(CombatSfxCue.BasicAttack);
                controller.PlayCombatCueAfter(CombatSfxCue.BasicAttack, 5f);
                Assert(controller.PendingCueCount == 1 &&
                       controller.GetCombatCuePlayCount(CombatSfxCue.BasicAttack) == before,
                    "A delayed cue must wait instead of playing at once.");
                Invoke(controller, "TickPendingCues", Time.unscaledTime + 1f);
                Assert(controller.PendingCueCount == 1, "A delayed cue must not play before its time.");
                Invoke(controller, "TickPendingCues", Time.unscaledTime + 6f);
                Assert(controller.PendingCueCount == 0 &&
                       controller.GetCombatCuePlayCount(CombatSfxCue.BasicAttack) == before + 1,
                    "A delayed cue must play once when its time comes.");
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

                    Raise(router, "OnSpellUsed", new object[] { null });
                    Assert(router.GetRequestCount(CombatSfxCue.SpellCast) == 1 &&
                           router.GetRequestCount(CombatSfxCue.SpellTeleport) == 0,
                        "An ordinary spell must play the spell cue, not the teleport.");
                    ItemDefinition teleport = ScriptableObject.CreateInstance<ItemDefinition>();
                    try
                    {
                        teleport.ConfigureContract("sfx-teleport-probe", "probe", ItemKind.SingleUseSpell,
                            ItemRarity.Common, true, 1, new ItemEffectEntry(ItemEffectType.TeleportToBossRoom));
                        Raise(router, "OnSpellUsed", teleport);
                        Assert(router.GetRequestCount(CombatSfxCue.SpellTeleport) == 1 &&
                               router.GetRequestCount(CombatSfxCue.SpellCast) == 1,
                            "A teleport spell must play the teleport instead of the spell cue.");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(teleport);
                    }

                    Raise(router, "OnPowerUsed", new object[] { null });
                    Assert(router.GetRequestCount(CombatSfxCue.PowerCast) == 1,
                        "Using the 권능 must ask for the power cue.");

                    VerifyDashRun(router);
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

        // Sound-1: the world cues that are not the player's own. An enemy
        // dying asks for the defeat cue and the player dying does not; an
        // ordinary enemy's attack asks for the attack cue and a boss's
        // does not, because the boss voices its own patterns.
        private static void VerifyWorldRouting()
        {
            GameObject enemy = new("Combat SFX defeat fixture");
            GameObject player = new("Combat SFX player death fixture");
            GameObject minion = new("Combat SFX minion fixture");
            GameObject boss = new("Combat SFX boss fixture");
            try
            {
                Health enemyHealth = enemy.AddComponent<Health>();
                Invoke(enemyHealth, "Awake");
                int defeats = CombatSfx.GetRequestCount(CombatSfxCue.EnemyDefeated);
                enemyHealth.TakeDamage(new DamageContext(player, DamageSourceType.Environment, 10000f));
                Assert(enemyHealth.IsDead, "The defeat fixture must die to a lethal hit.");
                Assert(CombatSfx.GetRequestCount(CombatSfxCue.EnemyDefeated) == defeats + 1,
                    "An enemy dying must ask for the defeat cue once.");

                Health playerHealth = player.AddComponent<Health>();
                Invoke(playerHealth, "Awake");
                player.AddComponent<PlayerCombatEvents>();
                playerHealth.TakeDamage(new DamageContext(enemy, DamageSourceType.Environment, 10000f));
                Assert(playerHealth.IsDead, "The player fixture must die to a lethal hit.");
                Assert(CombatSfx.GetRequestCount(CombatSfxCue.EnemyDefeated) == defeats + 1,
                    "The player dying must not play the enemy defeat cue.");

                int attacks = CombatSfx.GetRequestCount(CombatSfxCue.EnemyAttack);
                EnemyProjectile.PlayAttackSound(minion);
                Assert(CombatSfx.GetRequestCount(CombatSfxCue.EnemyAttack) == attacks + 1,
                    "An ordinary enemy attacking must ask for the attack cue.");
                boss.AddComponent<BossController>();
                EnemyProjectile.PlayAttackSound(boss);
                Assert(CombatSfx.GetRequestCount(CombatSfxCue.EnemyAttack) == attacks + 1,
                    "A boss attacking must not play the ordinary enemy attack cue.");

                // Sound-3: a 자폭형 (or crumb minion) death blast sounds like a bomb, once per blast.
                int blasts = CombatSfx.GetRequestCount(CombatSfxCue.BombExplosion);
                EnemyDeathBlast blast = EnemyDeathBlast.Spawn(Vector3.one * 5000f, minion.transform, minion, null,
                    0f, 1f, 2, 0f);
                Assert(blast.Explode() && !blast.Explode() &&
                       CombatSfx.GetRequestCount(CombatSfxCue.BombExplosion) == blasts + 1,
                    "An enemy death blast must ask for the bomb explosion cue once when it goes off.");

                // Sound-3: a spell or 권능 taken off the floor asks for the item pickup cue once.
                int pickups = CombatSfx.GetRequestCount(CombatSfxCue.ItemPickup);
                new GameObject("Combat SFX spell pickup fixture", typeof(BoxCollider2D))
                    .AddComponent<SingleUseItemPickup>().MarkCollected();
                new GameObject("Combat SFX power pickup fixture", typeof(BoxCollider2D))
                    .AddComponent<ActivePowerPickup>().MarkCollected();
                Assert(CombatSfx.GetRequestCount(CombatSfxCue.ItemPickup) == pickups + 2,
                    "Collecting a spell or a power pickup must ask for the item pickup cue once each.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boss);
                UnityEngine.Object.DestroyImmediate(minion);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(enemy);
            }
        }

        // Sound-2: the cancel sound goes to the buttons that back out of a
        // screen, by name, and every other button keeps the click.
        private static void VerifyCancelButtonNames()
        {
            foreach (string name in new[] { "BackButton", "SettingsBackButton", "CreditsBackButton", "Cancel",
                         "Cancel Button", "Cancel Selection", "Close", "Resume Button" })
                Assert(GameAudioController.IsCancelButtonName(name), $"{name} must sound as a cancel.");
            foreach (string name in new[] { "Background", "HomeQuitButton", "StartButton", "Buy Button", "" })
                Assert(!GameAudioController.IsCancelButtonName(name), $"{name} must keep the click.");
        }

        // Sound pass 2026-11-21: the running starts with the dash and stops with it, however
        // the dash ends, so a long dash never falls silent and a short one
        // never leaves the loop running.
        private static void VerifyDashRun(PlayerCombatAudio router)
        {
            Assert(PlayerCombatAudio.DashRunPitch >= 1f && PlayerCombatAudio.DashRunPitch <= 2.5f,
                "The dash run plays sped up (1x..2.5x) to keep up with the dash; a slower or chipmunk run is a typo.");
            foreach (UltimateEndReason reason in new[] { UltimateEndReason.Impact, UltimateEndReason.Timeout })
            {
                int runs = router.GetRequestCount(CombatSfxCue.SkillUpperRun);
                Raise(router, "OnDashStarted");
                Assert(router.GetRequestCount(CombatSfxCue.SkillUpperRun) == runs + 1 && router.IsDashRunActive,
                    "Starting the dash must start the running loop.");
                Raise(router, "OnDashEnded", reason);
                Assert(!router.IsDashRunActive, $"A dash that ends by {reason} must stop the running loop.");
            }
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

        private static void Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo found = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (found == null) throw new InvalidOperationException($"{target.GetType().Name}.{method} is gone.");
            found.Invoke(target, arguments);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Combat SFX verification failed: {message}");
        }
    }
}
