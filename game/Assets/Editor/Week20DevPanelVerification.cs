using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Checks the development-only hooks behind the Game Scene DEV panel (F1).
    public static class Week20DevPanelVerification
    {
        [MenuItem("Trickal Fan Game/Week 20/Verify Development Game Panel Hooks")]
        public static void Verify()
        {
            ValidateSeedOverride();
            ValidateForcedPit();
            ValidateWallHighlight();
            ValidateKillCurrentWave();
            Assert(!GameMinimapView.DevelopmentRevealSecrets && !DestructibleObstacle.DevelopmentForceNextSecretPit &&
                   !TrickalFanGame.Player.PlayerStats.DevelopmentForceBurnSource,
                "Development panel flags must default to off.");
            Debug.Log("Development panel verification passed: the seed override starts exactly one Run and clears, " +
                      "a forced pit needs a secret-room floor and is consumed once, wall highlight restores the " +
                      "original color, and killing the wave uses the room's death path.");
        }

        // The boss jump needs the generated floors of the Game Scene, so it is kept out of the scene-free Verify.
        public static void VerifyBossJumpBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
            VerifyBossJump();
        }

        [MenuItem("Trickal Fan Game/Week 20/Verify Development Boss Jump (Game Scene open)")]
        public static void VerifyBossJump()
        {
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>(
                FindObjectsInactive.Include);
            Assert(assembler != null && assembler.Progress != null,
                "The boss jump verification requires the Game Scene assembler.");
            int seed = assembler.Progress.HasRunSeed ? assembler.Progress.RunSeed : 3303;
            assembler.Progress.ResetProgress();
            Assert(assembler.Progress.TryInitializeRunSeed(seed, out string error), error);
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out error), error);

            // Last floor first, so the jump both skips floors and returns to an earlier one.
            foreach (int floorNumber in new[] { 3, 1, 2 })
            {
                GeneratedFloor floor = assembler.GeneratedGraph.FindFloor(floorNumber);
                Assert(floor != null, $"Generated floor {floorNumber} is missing.");
                Assert(DevelopmentGamePanel.TryGoToBossRoom(assembler, floorNumber, out string message), message);
                RoomNode current = assembler.Graph.CurrentNode;
                Assert(current.RoomId == floor.BossRoomId && current.Definition.RoomType == RoomType.Boss &&
                       assembler.Progress.CurrentFloor == floorNumber,
                    $"The boss jump must end in the floor {floorNumber} boss room.");
                Assert(current.GetComponent<RoomPrefab>().Controller.State != RoomState.Cleared,
                    $"The floor {floorNumber} boss room must not be cleared by the jump.");
                Assert(!DevelopmentGamePanel.TryGoToBossRoom(assembler, floorNumber, out _),
                    "Jumping to the boss room the player is already in must be refused.");
                SkipTransitionCooldown(assembler.Graph);
            }

            RoomController bossRoom = assembler.Graph.CurrentNode.GetComponent<RoomPrefab>().Controller;
            TrickalFanGame.Enemy.BossController spawned = null;
            bossRoom.EnemySpawned += enemy =>
                spawned = enemy != null ? enemy.GetComponent<TrickalFanGame.Enemy.BossController>() : spawned;
            bossRoom.BeginCombat(assembler.Graph.Player.GetComponent<Health>());
            Assert(spawned != null, "The boss room reached by the jump must spawn its boss when combat begins.");
            Assert(!DevelopmentGamePanel.TryGoToBossRoom(assembler, 3, out _) &&
                   assembler.Progress.CurrentFloor == 2,
                "A boss jump must be refused while the current room is in combat.");

            Debug.Log("Development boss jump verification passed: each floor's boss room is reached through the " +
                      "floor load and room teleport, forwards and backwards, with its boss still present.");
        }

        private static void SkipTransitionCooldown(RoomGraphController graph)
        {
            typeof(RoomGraphController)
                .GetField("nextTransitionTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(graph, 0f);
        }

        private static void ValidateSeedOverride()
        {
            GameObject root = new("DevPanel seed verification");
            try
            {
                RunProgress progress = root.AddComponent<RunProgress>();
                RunSession session = root.AddComponent<RunSession>();
                session.Configure(null, progress, null);
                RunSession.DevelopmentSeedOverride = 424242;
                Invoke(session, "EnsureRunSeed");
                Assert(progress.HasRunSeed && progress.RunSeed == 424242 && RunSession.DevelopmentSeedOverride == null,
                    "The development seed override must start the Run with that seed and then clear.");
            }
            finally
            {
                RunSession.DevelopmentSeedOverride = null;
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateForcedPit()
        {
            GameObject root = new("DevPanel pit verification");
            ResourceDropTable table = ScriptableObject.CreateInstance<ResourceDropTable>();
            try
            {
                SecretPit pitPrefab = Week20Special3Setup.EnsureSecretPitPrefab();
                table.Configure(0f, new[] { new ResourceDropEntry("pit", pitPrefab.gameObject, 1) });
                DestructibleObstacle.DevelopmentForceNextSecretPit = true;

                DestructibleObstacle noSecret = CreateObstacle(root.transform, "dev-01", table);
                noSecret.Bind(new RoomRunState("floor-01-room-02"), 1, root.transform, null);
                Assert(noSecret.TryDestroyByBomb() && noSecret.LastDrop == null &&
                       DestructibleObstacle.DevelopmentForceNextSecretPit,
                    "A forced pit must not appear on a floor without a secret room and must stay armed.");

                SecretRoomLink link = new(null);
                DestructibleObstacle forced = CreateObstacle(root.transform, "dev-02", table);
                forced.Bind(new RoomRunState("floor-01-room-03"), 1, root.transform, null, link);
                Assert(forced.TryDestroyByBomb() && forced.LastDrop != null &&
                       forced.LastDrop.GetComponent<SecretPit>() != null &&
                       !DestructibleObstacle.DevelopmentForceNextSecretPit,
                    "A forced pit must drop once on a secret-room floor and then disarm.");

                DestructibleObstacle next = CreateObstacle(root.transform, "dev-03", table);
                next.Bind(new RoomRunState("floor-01-room-04"), 1, root.transform, null, link);
                Assert(next.TryDestroyByBomb() && next.LastDrop == null,
                    "After the forced pit, obstacles must use their normal 0% drop roll.");
            }
            finally
            {
                DestructibleObstacle.DevelopmentForceNextSecretPit = false;
                Object.DestroyImmediate(table);
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateWallHighlight()
        {
            GameObject seal = new("DevPanel seal", typeof(SpriteRenderer), typeof(SecretPassageWall));
            try
            {
                SpriteRenderer renderer = seal.GetComponent<SpriteRenderer>();
                Color original = new(0.3f, 0.3f, 0.3f);
                renderer.color = original;
                SecretPassageWall wall = seal.GetComponent<SecretPassageWall>();
                wall.SetDevelopmentHighlight(true);
                wall.SetDevelopmentHighlight(true);
                Assert(renderer.color != original, "Highlight must tint the sealed wall.");
                wall.SetDevelopmentHighlight(false);
                Assert(renderer.color == original, "Turning highlight off must restore the original wall color.");
            }
            finally
            {
                Object.DestroyImmediate(seal);
            }
        }

        private static void ValidateKillCurrentWave()
        {
            GameObject root = new("DevPanel kill verification", typeof(BoxCollider2D));
            try
            {
                RoomController controller = root.AddComponent<RoomController>();
                Health first = CreateEnemy(root.transform);
                Health second = CreateEnemy(root.transform);
                controller.RegisterEnemy(first);
                controller.RegisterEnemy(second);
                Assert(controller.AliveEnemyCount == 2, "Fixture enemies must register with the room.");
                Assert(controller.KillAliveEnemiesForDevelopment() == 2 && first.IsDead && second.IsDead &&
                       controller.AliveEnemyCount == 0,
                    "Killing the wave must kill every registered enemy through the room death handlers.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Health CreateEnemy(Transform parent)
        {
            GameObject enemy = new("Enemy", typeof(Health));
            enemy.transform.SetParent(parent);
            Health health = enemy.GetComponent<Health>();
            Invoke(health, "Awake");
            return health;
        }

        private static DestructibleObstacle CreateObstacle(Transform parent, string obstacleId, ResourceDropTable table)
        {
            GameObject obstacleObject = new(obstacleId, typeof(BoxCollider2D), typeof(SpriteRenderer),
                typeof(DestructibleObstacle));
            obstacleObject.transform.SetParent(parent);
            obstacleObject.layer = LayerMask.NameToLayer("Environment");
            DestructibleObstacle obstacle = obstacleObject.GetComponent<DestructibleObstacle>();
            obstacle.Configure(obstacleId, DestructibleObstacle.DefaultRequiredHits, table,
                obstacleObject.GetComponent<SpriteRenderer>());
            return obstacle;
        }

        private static void Invoke(object target, string method)
        {
            try
            {
                target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.Invoke(target, null);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
