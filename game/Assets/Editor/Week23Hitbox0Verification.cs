using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week23Hitbox0Verification
    {
        private const float MoveSpeed = 5f;
        private const int SimulationSteps = 80;
        private const float Tolerance = 0.04f;
        private const float EnemyRadius = 0.5f;
        private static readonly Vector2 Origin = new(400f, 400f);

        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Hitbox-0 Player Feet Collider")]
        public static void SetupAndVerifyBatch()
        {
            Week23Hitbox0Setup.Setup();
            // A rerun must not add a second feet child or move the layer.
            int feetLayer = PlayerFeet.Layer;
            Week23Hitbox0Setup.Setup();
            Assert(feetLayer >= 0 && feetLayer == PlayerFeet.Layer, "Hitbox-0 setup rerun moved the feet layer.");
            Verify();
        }

        // Also re-runs the checks that share the movement layers, doorways, flight and Player layer fixtures. Each
        // starts from an empty scene except the two that read the Player of the Game Scene.
        [MenuItem("Trickal Fan Game/Week 23/Verify Hitbox-0 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Action[] emptySceneChecks =
            {
                Verify, Week20Move1Verification.Verify, Week19Door1Verification.Verify,
                Week22Terrain0Verification.Verify, Week22Chest0Verification.Verify, Week22Chest2Verification.Verify,
                Week22Flight0Verification.Verify, Week18Obstacle1Verification.Verify,
                Week23Obstacle5Verification.Verify, Week23Obstacle6Verification.Verify,
                Week23Enemy6Verification.Verify,
                Week23Passive1Verification.Verify, FairyVillageArtworkVerification.Verify,
                Week18Obstacle0Verification.Verify,
            };
            foreach (Action check in emptySceneChecks)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                check();
            }

            foreach (Action check in new Action[] { Week22Corner0Verification.Verify, Week7HighGradeSkillVerification.Verify })
            {
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath);
                check();
            }

            Debug.Log("Hitbox-0 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Hitbox-0 Player Feet Collider")]
        public static void Verify()
        {
            ValidateLayers();

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Hitbox-0 scene checks need the open scenes saved or discarded.");
            }

            SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string scenePath in Week23Hitbox0Setup.PlayerScenes)
                {
                    ValidatePlayerScene(scenePath);
                }

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ValidateTerrainUsesFeet();
                ValidateEnemiesUseBody();
                ValidateDoorwayUsesFeet();
            }
            finally
            {
                if (previousScenes.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                }
            }

            Debug.Log($"Hitbox-0 verification passed: the {PlayerFeet.LayerName} layer collides with terrain only, " +
                      $"every player scene has one body circle of radius {PlayerFeet.BodyRadius} and one feet circle " +
                      $"of radius {PlayerFeet.Radius}, walls, pits and a {PlayerFeet.Radius * 2f + 0.2f:0.#} gap act on " +
                      "the feet, enemies still meet the body, and a doorway waits for the feet.");
        }

        private static void ValidateLayers()
        {
            int feet = PlayerFeet.Layer;
            int player = LayerMask.NameToLayer(Week23Hitbox0Setup.PlayerLayerName);
            Assert(feet >= 0, $"Run Hitbox-0 setup to create the {PlayerFeet.LayerName} layer.");
            Assert(player >= 0 && player != feet, "Hitbox-0 needs separate Player and PlayerFeet layers.");

            HashSet<int> terrain = new(PlayerFeet.CollisionLayers.Select(LayerMask.NameToLayer));
            Assert(!terrain.Contains(-1), "Hitbox-0 needs the Environment and Pit layers.");
            for (int layer = 0; layer < 32; layer++)
            {
                Assert(Physics2D.GetIgnoreLayerCollision(feet, layer) != terrain.Contains(layer),
                    $"The feet layer must collide with terrain only, but layer '{LayerMask.LayerToName(layer)}' " +
                    "differs.");
            }

            foreach (int layer in terrain)
            {
                Assert(Physics2D.GetIgnoreLayerCollision(player, layer),
                    $"The body layer must not collide with '{LayerMask.LayerToName(layer)}'.");
            }

            // Default holds the doorway, room, item and enemy projectile triggers.
            foreach (string layerName in new[] { "Default", "Enemy", "Pickup" })
            {
                Assert(!Physics2D.GetIgnoreLayerCollision(player, LayerMask.NameToLayer(layerName)),
                    $"The body layer must still collide with the {layerName} layer.");
            }
        }

        private static void ValidatePlayerScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int players = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PlayerMovement movement in root.GetComponentsInChildren<PlayerMovement>(true))
                {
                    players++;
                    GameObject player = movement.gameObject;
                    Collider2D[] rootColliders = player.GetComponents<Collider2D>();
                    Assert(player.layer == LayerMask.NameToLayer(Week23Hitbox0Setup.PlayerLayerName) &&
                           rootColliders.Length == 1 && rootColliders[0] is CircleCollider2D body && !body.isTrigger &&
                           Mathf.Approximately(body.radius, PlayerFeet.BodyRadius) && body.offset == Vector2.zero,
                        $"{scenePath} player root must hold one solid body circle of radius {PlayerFeet.BodyRadius} " +
                        "on the Player layer.");

                    PlayerFeet[] feet = player.GetComponentsInChildren<PlayerFeet>(true);
                    Assert(feet.Length == 1 && feet[0].transform.parent == player.transform &&
                           feet[0].gameObject.activeSelf,
                        $"{scenePath} player must have exactly one active feet child, found {feet.Length}.");
                    Assert(!PlayerFeet.Ensure(player, out _),
                        $"{scenePath} player feet differ from the Hitbox-0 layer, place or size. Run the setup.");
                    Assert(feet[0].GetComponents<Collider2D>().Length == 1,
                        $"{scenePath} player feet must hold one collider.");
                    Assert(player.GetComponentsInChildren<Collider2D>(true).Count(found => !found.isTrigger) == 2,
                        $"{scenePath} player must have no solid collider besides the body and the feet.");
                }
            }

            Assert(players > 0, $"{scenePath} must contain a player.");
        }

        // Walls and pits stop the feet circle, so the body overlaps a wall above it and a gap narrower than the body
        // still lets the player through.
        private static void ValidateTerrainUsesFeet()
        {
            float feetTop = PlayerFeet.LocalPosition.y + PlayerFeet.Radius;
            float feetBottom = PlayerFeet.LocalPosition.y - PlayerFeet.Radius;
            foreach (string layerName in PlayerFeet.CollisionLayers)
            {
                Run(layerName + " above", root =>
                {
                    Box(root, layerName, Origin + new Vector2(0f, 2f), new Vector2(6f, 1f));
                    Rigidbody2D body = Player(root, Origin);
                    Drive(body, Vector2.up);
                    float expected = Origin.y + 1.5f - feetTop;
                    Assert(Mathf.Abs(body.position.y - expected) < Tolerance,
                        $"Walking up into {layerName} must stop at the feet (y {expected:F2}), but stopped at " +
                        $"{body.position.y:F2}.");
                });
                Run(layerName + " below", root =>
                {
                    Box(root, layerName, Origin + new Vector2(0f, -2f), new Vector2(6f, 1f));
                    Rigidbody2D body = Player(root, Origin);
                    Drive(body, Vector2.down);
                    float expected = Origin.y - 1.5f - feetBottom;
                    Assert(Mathf.Abs(body.position.y - expected) < Tolerance,
                        $"Walking down into {layerName} must stop at the feet (y {expected:F2}), but stopped at " +
                        $"{body.position.y:F2}.");
                });
                Run(layerName + " beside", root =>
                {
                    Box(root, layerName, Origin + new Vector2(2f, 0f), new Vector2(1f, 6f));
                    Rigidbody2D body = Player(root, Origin);
                    Drive(body, Vector2.right);
                    float expected = Origin.x + 1.5f - PlayerFeet.Radius;
                    Assert(Mathf.Abs(body.position.x - expected) < Tolerance,
                        $"Walking sideways into {layerName} must stop at the feet (x {expected:F2}), but stopped at " +
                        $"{body.position.x:F2}.");
                });
            }

            float passingGap = PlayerFeet.Radius * 2f + 0.2f;
            Assert(passingGap < PlayerFeet.BodyRadius * 2f, "The gap probe must be narrower than the body.");
            Assert(CrossesGap(passingGap), $"A {passingGap:F1} gap is wider than the feet and must let the player by.");
            float blockingGap = PlayerFeet.Radius * 2f - 0.2f;
            Assert(!CrossesGap(blockingGap), $"A {blockingGap:F1} gap is narrower than the feet and must block.");
        }

        private static bool CrossesGap(float gap)
        {
            bool crossed = false;
            Run($"gap {gap:F1}", root =>
            {
                float feetY = Origin.y + PlayerFeet.LocalPosition.y;
                float half = gap * 0.5f;
                Box(root, "Environment", new Vector2(Origin.x + 2f, feetY + half + 2f), new Vector2(1f, 4f));
                Box(root, "Environment", new Vector2(Origin.x + 2f, feetY - half - 2f), new Vector2(1f, 4f));
                Rigidbody2D body = Player(root, Origin);
                Drive(body, Vector2.right);
                crossed = body.position.x > Origin.x + 3f;
            });
            return crossed;
        }

        private static void ValidateEnemiesUseBody()
        {
            Run("enemy", root =>
            {
                GameObject enemy = new("Hitbox-0 Enemy", typeof(CircleCollider2D));
                enemy.transform.SetParent(root.transform);
                enemy.transform.position = Origin + new Vector2(3f, 0f);
                enemy.layer = LayerMask.NameToLayer("Enemy");
                enemy.GetComponent<CircleCollider2D>().radius = EnemyRadius;
                Rigidbody2D body = Player(root, Origin);
                Drive(body, Vector2.right);
                float expected = Origin.x + 3f - EnemyRadius - PlayerFeet.BodyRadius;
                Assert(Mathf.Abs(body.position.x - expected) < Tolerance,
                    $"An enemy body must stop the body circle (x {expected:F2}), but the player stopped at " +
                    $"{body.position.x:F2}.");
            });
        }

        private static void ValidateDoorwayUsesFeet()
        {
            Run("doorway", root =>
            {
                GameObject triggerObject = new("Hitbox-0 Doorway Trigger", typeof(BoxCollider2D));
                triggerObject.transform.SetParent(root.transform);
                triggerObject.transform.position = Origin + new Vector2(0f, 2f);
                BoxCollider2D trigger = triggerObject.GetComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(2f, 1f);
                float triggerBottom = Origin.y + 1.5f;

                GameObject player = new("Hitbox-0 Doorway Player", typeof(PlayerMovement), typeof(CircleCollider2D));
                player.transform.SetParent(root.transform);
                player.layer = LayerMask.NameToLayer(Week23Hitbox0Setup.PlayerLayerName);
                CircleCollider2D body = player.GetComponent<CircleCollider2D>();
                body.radius = PlayerFeet.BodyRadius;
                PlayerMovement movement = player.GetComponent<PlayerMovement>();
                Assert(movement.Feet == null && movement.FeetOverlap(trigger) &&
                       movement.FeetPosition == (Vector2)player.transform.position,
                    "A player fixture without feet must fall back to its root.");
                PlayerFeet.Ensure(player, out PlayerFeet feet);
                Assert(movement.Feet == feet, "The player must find its feet child.");

                // The body top is inside the trigger, the feet top is still below it.
                player.transform.position = new Vector3(Origin.x, triggerBottom - 0.3f, 0f);
                Physics2D.SyncTransforms();
                Assert(body.Distance(trigger).isOverlapped && !movement.FeetOverlap(trigger),
                    "A doorway the body reaches but the feet do not must not count as entered.");
                Assert(Vector2.Distance(movement.FeetPosition,
                        (Vector2)player.transform.position + PlayerFeet.LocalPosition) < 0.001f,
                    "The feet position must be the feet circle center.");

                player.transform.position = new Vector3(Origin.x, triggerBottom, 0f);
                Physics2D.SyncTransforms();
                Assert(movement.FeetOverlap(trigger), "Feet standing in a doorway trigger must count as entered.");
            });
        }

        private static Rigidbody2D Player(GameObject root, Vector2 position)
        {
            GameObject player = new("Hitbox-0 Player", typeof(Rigidbody2D), typeof(CircleCollider2D));
            player.transform.SetParent(root.transform);
            player.transform.position = position;
            player.layer = LayerMask.NameToLayer(Week23Hitbox0Setup.PlayerLayerName);
            player.GetComponent<CircleCollider2D>().radius = PlayerFeet.BodyRadius;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None;
            PlayerFeet.Ensure(player, out _);
            return body;
        }

        private static void Box(GameObject root, string layerName, Vector2 position, Vector2 size)
        {
            GameObject box = new($"Hitbox-0 {layerName}", typeof(BoxCollider2D));
            box.transform.SetParent(root.transform);
            box.transform.position = position;
            box.layer = LayerMask.NameToLayer(layerName);
            box.GetComponent<BoxCollider2D>().size = size;
        }

        private static void Drive(Rigidbody2D body, Vector2 direction)
        {
            Physics2D.SyncTransforms();
            for (int index = 0; index < SimulationSteps; index++)
            {
                body.linearVelocity = direction * MoveSpeed;
                Physics2D.Simulate(Time.fixedDeltaTime);
            }
        }

        private static void Run(string label, Action<GameObject> probe)
        {
            SimulationMode2D previousMode = Physics2D.simulationMode;
            GameObject root = new($"Hitbox-0 {label}");
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                probe(root);
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
