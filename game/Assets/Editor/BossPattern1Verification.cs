using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class BossPattern1Verification
    {
        private const BossPatternExecution Dash = BossPatternExecution.CrayonHeroDashChain;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [MenuItem("Trickal Fan Game/Beta/Verify Boss-Pattern-1 Crayon Dash Rift")]
        public static void Verify()
        {
            ValidateAssets();
            ValidateRules();
            ValidatePerPhaseTiming();
            ValidateFixedWarningAndStrike();
            ValidateOneHitPerDash();
            ValidateCollisionPass();
            ValidateLeavesTouchingWall();
            Debug.Log("Boss-Pattern-1 verification passed: fixed rift warning, leap without push, delayed strike, one hit " +
                      "per dash, per-phase warning and recovery, swing recognition 1.15.");
        }

        public static void SetupAndVerifyBatch()
        {
            BossPattern1Setup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(Week15Boss3Setup.BossPrefabPath);
            BossPattern1Setup.Setup();
            Require(!string.IsNullOrEmpty(guid) && guid == AssetDatabase.AssetPathToGUID(Week15Boss3Setup.BossPrefabPath),
                "Setup changed the prefab GUID.");
            Verify();
        }

        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            BossMovementAnimationVerification.Verify();
            Week27Hitbox2Verification.Verify();
            // Boss-4 runs Boss-3 first.
            Week15Boss4Verification.Verify();
            CrayonHeroAttackAnimationVerification.Verify();
            BossIntro0Verification.VerifyBossEntryBatch();
            Debug.Log("Boss-Pattern-1 regression batch passed.");
        }

        private static void ValidateAssets()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss3Setup.BossPrefabPath);
            var runtime = prefab.GetComponent<CrayonHeroBossPatternRuntime>();
            BossPatternDefinition pattern = prefab.GetComponent<BossController>().Patterns
                .Single(item => item.PatternId == BossPattern1Setup.DashPatternId);
            Require(Mathf.Approximately(runtime.SwingRecognitionMultiplier, 1.15f), "Swing recognition must be 1.15.");
            Require(Mathf.Approximately(runtime.SwingWindup, 0.3f), "Swing windup must stay 0.3.");
            Require(Mathf.Approximately(pattern.TelegraphDuration, 0.35f) &&
                    Mathf.Approximately(pattern.RecoveryDuration, 0.8f), "Dash pattern timing drifted.");
            Require(Mathf.Approximately(runtime.DashRestDuration, 0.7f) &&
                    Mathf.Approximately(runtime.DashStrikeDelay, 0.1f) &&
                    Mathf.Approximately(runtime.DashDuration, 0.05f),
                "Dash chain timing drifted.");
            Require(runtime.RiftHitLength < 7.4f && runtime.RiftHitWidth < 1.5f,
                "The rift's hit area must stay smaller than its drawing.");
            foreach (string path in BossPattern1Setup.TexturePaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer != null && !importer.mipmapEnabled && importer.alphaIsTransparency &&
                        importer.npotScale == TextureImporterNPOTScale.None &&
                        importer.textureCompression == TextureImporterCompression.Uncompressed,
                    $"{path} has the wrong import settings.");
            }
            Texture2D strike = Resources.Load<Texture2D>("CombatEffects/" + CrayonDashRiftEffect.StrikeArtwork);
            Require(strike != null && strike.width == 3360 && strike.height == 640, "Strike sheet must be 7 x 4 cells.");
            Require(Resources.Load<Texture2D>("CombatEffects/" + CrayonDashRiftEffect.WarningArtwork) != null,
                "Warning artwork is missing.");
        }

        private static void ValidateRules()
        {
            Require(Enumerable.Range(0, 100).All(roll =>
                    CrayonHeroBossPatternRuntime.ResolveDashCountForRoll(roll, true) == 3) &&
                Enumerable.Range(0, 100).All(roll =>
                    CrayonHeroBossPatternRuntime.ResolveDashCountForRoll(roll, false) is 2 or 3),
                "Dash count must be 2-3, and 3 when golden.");
            const float downRight = -36f, downLeft = 216f;
            Require(Mathf.Approximately(CrayonHeroBossPatternRuntime.ResolveRiftAngle(Vector2.left, 0), downLeft) &&
                    Mathf.Approximately(CrayonHeroBossPatternRuntime.ResolveRiftAngle(Vector2.left, 1), downRight) &&
                    Mathf.Approximately(CrayonHeroBossPatternRuntime.ResolveRiftAngle(Vector2.right, 0), downRight) &&
                    Mathf.Approximately(CrayonHeroBossPatternRuntime.ResolveRiftAngle(Vector2.right, 1), downLeft) &&
                    Mathf.Approximately(CrayonHeroBossPatternRuntime.ResolveRiftAngle(Vector2.right, 2), downRight),
                "Rift direction must follow the dash direction table and alternate.");
            for (int degrees = 0; degrees < 360; degrees += 3)
            {
                Vector2 direction = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                for (int dash = 0; dash < 3; dash++)
                {
                    float angle = CrayonHeroBossPatternRuntime.ResolveRiftAngle(direction, dash);
                    float stop = CrayonHeroBossPatternRuntime.ResolveRiftStopDistance(direction, angle, 1.2f);
                    Vector2 line = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    Vector2 standing = -direction * stop;
                    Require(Mathf.Abs(standing.x * line.y - standing.y * line.x) >= 1.199f && stop <= 2.401f,
                        $"At dash angle {degrees} the boss body must stop clear of the rift, close enough to swing.");
                }
            }
            Require(CrayonHeroBossPatternRuntime.IsInsideRift(new Vector2(2f, 0f), Vector2.zero, 0f, 4.2f, 0.9f) &&
                    !CrayonHeroBossPatternRuntime.IsInsideRift(new Vector2(2.2f, 0f), Vector2.zero, 0f, 4.2f, 0.9f) &&
                    !CrayonHeroBossPatternRuntime.IsInsideRift(new Vector2(0f, 0.5f), Vector2.zero, 0f, 4.2f, 0.9f),
                "Rift hit box is wrong.");
        }

        private static void ValidatePerPhaseTiming()
        {
            GameObject root = Create(out var runtime, out GameObject target);
            try
            {
                BossController boss = root.GetComponent<BossController>();
                runtime.BeginCombat(target.transform, 7, 0f);
                int index = boss.Patterns.ToList().FindIndex(item => item.Execution == Dash);
                Set(boss, "currentPatternIndex", index);
                foreach ((int phase, float warning) in new[] { (1, 0.35f), (2, 0.3f) })
                {
                    Set(boss, "currentPhase", phase);
                    float tempo = boss.CurrentTempoMultiplier;
                    Call(boss, "SetState", BossActionState.Telegraph, 10f + 0.35f * tempo);
                    Require(Mathf.Abs(boss.StateEndsAt - (10f + warning)) < 0.001f, $"Phase {phase} warning must be {warning}s.");
                    Call(boss, "SetState", BossActionState.Recovery, 20f + 0.8f * tempo);
                    Require(Mathf.Abs(boss.StateEndsAt - 20.8f) < 0.001f, $"Phase {phase} recovery must be 0.8s.");
                }
            }
            finally { Destroy(root, target); }
        }

        private static void ValidateFixedWarningAndStrike()
        {
            GameObject root = Create(out var runtime, out GameObject target);
            try
            {
                Health health = target.GetComponent<Health>();
                float full = health.CurrentHealth;
                target.transform.position = new Vector2(6f, 0f);
                runtime.BeginCombat(target.transform, 7, 0f);
                runtime.OnPatternStateChanged(BossActionState.Telegraph, Dash, 0.35f);
                Vector2 placed = runtime.RiftCenter;
                target.transform.position = new Vector2(6f, 3f);
                runtime.TickPattern(BossActionState.Telegraph, Dash, 0.2f);
                Require(runtime.HasPlacedRift && placed == new Vector2(6f, 0f) && runtime.RiftCenter == placed,
                    "The warning must stay where the player stood when it appeared.");
                target.transform.position = placed;
                runtime.TickPattern(BossActionState.Telegraph, Dash, 0.35f);
                runtime.TryExecute(Dash);
                Set(runtime, "dashCountTarget", 2);
                Require(runtime.IsDashInMotion, "The leap must start when the warning ends.");
                float gap = Vector2.Distance(runtime.LeapEnd, placed);
                float stop = CrayonHeroBossPatternRuntime.ResolveRiftStopDistance(Vector2.right, runtime.RiftAngle, 1.2f);
                Require(Mathf.Abs(gap - stop) < 0.01f, $"The leap must land {stop} before the rift, landed {gap} away.");
                float arrival = 0.35f + runtime.DashDuration;
                runtime.TickPattern(BossActionState.Active, Dash, arrival + 0.001f);
                Require(!runtime.IsDashInMotion && runtime.CompletedDashes == 0, "Arrival must wait before the strike.");
                Require(Mathf.Approximately(health.CurrentHealth, full), "A player on the warning must be hit by the strike, not the arrival.");
                runtime.TickPattern(BossActionState.Active, Dash, arrival + 0.102f);
                Require(runtime.CompletedDashes == 1 && health.CurrentHealth < full, "The strike must land 0.1s after arrival.");
                target.transform.position = new Vector2(-3f, 2f);
                float rest = arrival + 0.102f + runtime.DashRestDuration + 0.001f;
                runtime.TickPattern(BossActionState.Active, Dash, rest);
                Require(runtime.HasPlacedRift && runtime.RiftCenter == new Vector2(-3f, 2f),
                    "The next warning must appear where the player moved to.");
            }
            finally { Destroy(root, target); }
        }

        private static void ValidateOneHitPerDash()
        {
            GameObject root = Create(out var runtime, out GameObject target);
            try
            {
                Health health = target.GetComponent<Health>();
                float full = health.CurrentHealth;
                target.transform.position = new Vector2(4f, 0f);
                runtime.ConfigureDash(0.05f, 0.7f, 20f, EnemyDamageTier.Light);
                runtime.BeginCombat(target.transform, 7, 0f);
                runtime.OnPatternStateChanged(BossActionState.Telegraph, Dash, 0.35f);
                runtime.TickPattern(BossActionState.Telegraph, Dash, 0.35f);
                runtime.TryExecute(Dash);
                Set(runtime, "dashCountTarget", 2);
                runtime.TickPattern(BossActionState.Active, Dash, 0.45f);
                float afterLeap = health.CurrentHealth;
                Require(afterLeap < full && runtime.DashHitApplied, "The leap path must hit.");
                runtime.TickPattern(BossActionState.Active, Dash, 0.521f);
                runtime.TickPattern(BossActionState.Active, Dash, 0.63f);
                Require(runtime.CompletedDashes == 1 && Mathf.Approximately(health.CurrentHealth, afterLeap),
                    "Being on both the leap path and the rift must cost one hit.");
            }
            finally { Destroy(root, target); }
        }

        private static void ValidateCollisionPass()
        {
            GameObject root = Create(out var runtime, out GameObject target);
            try
            {
                Collider2D own = root.GetComponent<Collider2D>();
                Collider2D player = target.AddComponent<CircleCollider2D>();
                target.transform.position = new Vector2(4f, 0f);
                runtime.BeginCombat(target.transform, 7, 0f);
                runtime.OnPatternStateChanged(BossActionState.Telegraph, Dash, 0.35f);
                runtime.TryExecute(Dash);
                Require(runtime.IsPassingThroughPlayer && Physics2D.GetIgnoreCollision(own, player),
                    "The leap must pass through the player instead of pushing them.");
                runtime.OnPatternStateChanged(BossActionState.Defeated, Dash, 1f);
                Require(!runtime.IsPassingThroughPlayer && !Physics2D.GetIgnoreCollision(own, player),
                    "Collision with the player must come back when the pattern ends.");
            }
            finally { Destroy(root, target); }
        }

        // Each wall is placed against the feet: side -1 left, 1 right, 2 above, -2 below. A positive overlap sinks it in.
        private static void ValidateLeavesTouchingWall()
        {
            CheckWall("behind, touching", -1, 0.005f, new Vector2(5f, 0f), end => Mathf.Abs(end.x - 2.96f) < 0.02f);
            CheckWall("behind, sunk in", -1, 0.12f, new Vector2(5f, 0f), end => Mathf.Abs(end.x - 2.96f) < 0.02f);
            CheckWall("along the wall above", 2, 0.005f, new Vector2(5f, 0.5f), end => end.x > 3f && end.y < 0.03f);
            CheckWall("along the wall below", -2, 0.005f, new Vector2(5f, -0.5f), end => end.x > 2.6f && end.y > -0.03f);
            CheckWall("along a sunk wall above", 2, 0.12f, new Vector2(5f, 0.5f), end => end.x > 3f && end.y < 0.03f);
            CheckWall("wall ahead", 1, -2f, new Vector2(6f, 0f), end => end.x > 1.8f && end.x < 2.01f);
            // Nothing in the way: a player right next to the boss makes it hop back to keep the cut in front.
            CheckWall("too close", -1, -6f, new Vector2(0.8f, 0f), end => Mathf.Abs(end.x + 1.24f) < 0.02f);
        }

        private static void CheckWall(string label, int side, float overlap, Vector2 player,
            Func<Vector2, bool> expected)
        {
            GameObject root = Create(out var runtime, out GameObject target);
            var wall = new GameObject("Boss-Pattern-1 wall") { layer = LayerMask.NameToLayer("Environment") };
            try
            {
                Physics2D.SyncTransforms();
                Bounds feet = root.GetComponentInChildren<EnemyFeet>().Capsule.bounds;
                BoxCollider2D box = wall.AddComponent<BoxCollider2D>();
                box.size = Mathf.Abs(side) == 1 ? new Vector2(1f, 20f) : new Vector2(30f, 1f);
                wall.transform.position = side switch
                {
                    -1 => new Vector2(feet.min.x - 0.5f + overlap, feet.center.y),
                    1 => new Vector2(feet.max.x + 0.5f - overlap, feet.center.y),
                    2 => new Vector2(0f, feet.max.y + 0.5f - overlap),
                    _ => new Vector2(0f, feet.min.y - 0.5f + overlap),
                };
                Physics2D.SyncTransforms();
                target.transform.position = player;
                runtime.BeginCombat(target.transform, 7, 0f);
                runtime.OnPatternStateChanged(BossActionState.Telegraph, Dash, 0.35f);
                runtime.TryExecute(Dash);
                Require(expected(runtime.LeapEnd), $"Wall case '{label}' ended the leap at {runtime.LeapEnd:F3}.");
            }
            finally { UnityEngine.Object.DestroyImmediate(wall); Destroy(root, target); }
        }

        private static GameObject Create(out CrayonHeroBossPatternRuntime runtime, out GameObject target)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Boss3Setup.BossPrefabPath);
            GameObject root = UnityEngine.Object.Instantiate(prefab);
            root.transform.position = Vector3.zero;
            runtime = root.GetComponent<CrayonHeroBossPatternRuntime>();
            Call(root.GetComponent<Health>(), "Awake");
            Call(root.GetComponent<BossController>(), "Awake");
            Call(runtime, "Awake");
            target = new GameObject("Boss-Pattern-1 target");
            target.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            Health health = target.AddComponent<Health>();
            Set(health, "maxHealth", 100f);
            target.AddComponent<PlayerMovement>();
            Call(health, "Awake");
            return root;
        }

        private static void Destroy(GameObject root, GameObject target)
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static void Call(object instance, string method, params object[] arguments) =>
            instance.GetType().GetMethod(method, Private).Invoke(instance, arguments);

        private static void Set(object instance, string field, object value) =>
            instance.GetType().GetField(field, Private).SetValue(instance, value);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Boss-Pattern-1: " + message);
        }
    }
}
