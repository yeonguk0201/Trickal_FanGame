using System;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week23Hitbox1Verification
    {
        private const float MoveSpeed = 5f;
        private const int SimulationSteps = 80;
        private const float Tolerance = 0.04f;
        private const float EnemyRadius = 0.5f;
        private static readonly Vector2 Origin = new(400f, 440f);
        private static readonly float[] Sizes = { 0.5f, 1.5f, 2f, 3f };

        // Also re-runs the Hitbox-0 checks and its regressions: they share the feet, doorways and placement.
        [MenuItem("Trickal Fan Game/Week 23/Verify Hitbox-1 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Verify();
            Week23Hitbox0Verification.VerifyWithRegressionsBatch();
            // Bomb placement and the development panel read the standing position and the size.
            foreach (Action check in new Action[]
                     {
                         Week20Special2Verification.Verify, Week20Special3Verification.Verify,
                         Week20DevPanelVerification.Verify,
                     })
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                check();
            }

            Debug.Log("Hitbox-1 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Hitbox-1 Player Size")]
        public static void Verify()
        {
            ValidateShape();
            ValidateTerrainIgnoresSize();
            ValidateEnemiesMeetScaledBody();
            Debug.Log("Hitbox-1 verification passed: the look scales without a limit, the hurtbox stops at " +
                      $"x{PlayerBodySize.HurtboxSizeLimit:0.#}, the feet keep radius {PlayerFeet.Radius} and stay " +
                      "where they stand while the body grows upward, size 1 restores the original shape, walls and " +
                      "gaps act on the same feet at every size, and enemies meet the scaled body.");
        }

        private static void ValidateShape()
        {
            Run("shape", root =>
            {
                PlayerMovement movement = Player(root, Origin, out PlayerBodySize size, out CircleCollider2D body);
                PlayerFeet feet = movement.Feet;
                Physics2D.SyncTransforms();
                Vector2 feetStart = feet.WorldCenter;
                Assert(Mathf.Approximately(size.SizeMultiplier, 1f) && !size.SetSizeMultiplier(1f) &&
                       Vector2.Distance(movement.StandingPosition, Origin) < 0.001f &&
                       Vector2.Distance(movement.RootPositionForStanding(Origin), Origin) < 0.001f,
                    "At size 1 the player must stand at its root and setting size 1 must change nothing.");

                foreach (float target in Sizes)
                {
                    Assert(size.SetSizeMultiplier(target), $"Setting size {target} must report a change.");
                    Physics2D.SyncTransforms();
                    float hurtbox = PlayerFeet.BodyRadius * Mathf.Min(target, PlayerBodySize.HurtboxSizeLimit);
                    Assert(Mathf.Abs(movement.transform.localScale.x - target) < 0.001f &&
                           Mathf.Abs(movement.transform.localScale.y - target) < 0.001f,
                        $"The look must scale to x{target}.");
                    Assert(Mathf.Abs(body.bounds.extents.x - hurtbox) < 0.001f &&
                           Mathf.Abs(size.HurtboxRadius - hurtbox) < 0.001f,
                        $"At size {target} the hurtbox radius must be {hurtbox:F2}, but is {body.bounds.extents.x:F2}.");
                    Assert(Mathf.Abs(feet.Collider.bounds.extents.x - PlayerFeet.Radius) < 0.001f &&
                           Mathf.Abs(feet.Collider.bounds.extents.y - PlayerFeet.Radius) < 0.001f,
                        $"At size {target} the feet must keep radius {PlayerFeet.Radius}.");
                    Assert(Vector2.Distance(feet.WorldCenter, feetStart) < 0.001f &&
                           Vector2.Distance(movement.StandingPosition, Origin) < 0.001f,
                        $"At size {target} the feet must stay where they stood.");
                    float lift = PlayerFeet.BodyRadius * (target - 1f);
                    Assert(Vector2.Distance(movement.transform.position, Origin + Vector2.up * lift) < 0.001f,
                        $"At size {target} the root must sit {lift:F2} above where the player stands.");
                    // The bottom of the scaled look stays at the bottom of the feet.
                    Assert(Mathf.Abs(movement.transform.position.y - PlayerFeet.BodyRadius * target -
                                     feet.Collider.bounds.min.y) < 0.001f,
                        $"At size {target} the body must grow upward from the bottom of the feet.");
                    Vector2 destination = Origin + new Vector2(7f, -3f);
                    Assert(Vector2.Distance(movement.RootPositionForStanding(destination),
                            destination + Vector2.up * lift) < 0.001f,
                        $"At size {target} a placement must put the feet, not the root, on the entry point.");
                }

                Assert(size.SetSizeMultiplier(0.1f) &&
                       Mathf.Approximately(size.SizeMultiplier, PlayerBodySize.MinimumSize),
                    $"The size must not go below x{PlayerBodySize.MinimumSize}.");

                Assert(size.SetSizeMultiplier(1f), "Returning to size 1 must report a change.");
                Physics2D.SyncTransforms();
                Assert(movement.transform.localScale == Vector3.one &&
                       Vector2.Distance(movement.transform.position, Origin) < 0.001f &&
                       Mathf.Abs(body.radius - PlayerFeet.BodyRadius) < 0.0001f &&
                       !PlayerFeet.Ensure(movement.gameObject, out _),
                    "Returning to size 1 must restore the root, the body circle and the feet.");
            });
        }

        private static void ValidateTerrainIgnoresSize()
        {
            foreach (float target in new[] { 1f, 3f })
            {
                Run($"wall x{target}", root =>
                {
                    Box(root, "Environment", Origin + new Vector2(0f, 3f), new Vector2(6f, 1f));
                    PlayerMovement movement = Player(root, Origin, out PlayerBodySize size, out _);
                    size.SetSizeMultiplier(target);
                    Drive(movement, Vector2.up);
                    float feetTop = movement.Feet.Collider.bounds.max.y;
                    Assert(Mathf.Abs(feetTop - (Origin.y + 2.5f)) < Tolerance,
                        $"At size {target} walking up into a wall must stop at the feet, but the feet top is " +
                        $"{feetTop - Origin.y:F2} above the start.");
                });
                Run($"gap x{target}", root =>
                {
                    float gap = PlayerFeet.Radius * 2f + 0.2f;
                    float feetY = Origin.y + PlayerFeet.LocalPosition.y;
                    Box(root, "Environment", new Vector2(Origin.x + 2f, feetY + gap * 0.5f + 2f), new Vector2(1f, 4f));
                    Box(root, "Environment", new Vector2(Origin.x + 2f, feetY - gap * 0.5f - 2f), new Vector2(1f, 4f));
                    PlayerMovement movement = Player(root, Origin, out PlayerBodySize size, out _);
                    size.SetSizeMultiplier(target);
                    Drive(movement, Vector2.right);
                    Assert(movement.transform.position.x > Origin.x + 3f,
                        $"At size {target} a {gap:F1} gap must still let the player through.");
                });
            }
        }

        private static void ValidateEnemiesMeetScaledBody()
        {
            foreach (float target in new[] { 1.5f, 3f })
            {
                Run($"enemy x{target}", root =>
                {
                    PlayerMovement movement = Player(root, Origin, out PlayerBodySize size, out _);
                    size.SetSizeMultiplier(target);
                    GameObject enemy = new("Hitbox-1 Enemy", typeof(CircleCollider2D));
                    enemy.transform.SetParent(root.transform);
                    enemy.transform.position = new Vector3(Origin.x + 4f, movement.transform.position.y, 0f);
                    enemy.layer = LayerMask.NameToLayer("Enemy");
                    enemy.GetComponent<CircleCollider2D>().radius = EnemyRadius;
                    Drive(movement, Vector2.right);
                    float expected = Origin.x + 4f - EnemyRadius - size.HurtboxRadius;
                    Assert(Mathf.Abs(movement.transform.position.x - expected) < Tolerance,
                        $"At size {target} an enemy must stop the body at radius {size.HurtboxRadius:F2} " +
                        $"(x {expected - Origin.x:F2}), but the player stopped at " +
                        $"{movement.transform.position.x - Origin.x:F2}.");
                });
            }
        }

        private static PlayerMovement Player(GameObject root, Vector2 position, out PlayerBodySize size,
            out CircleCollider2D body)
        {
            GameObject player = new("Hitbox-1 Player", typeof(PlayerMovement), typeof(CircleCollider2D),
                typeof(PlayerBodySize));
            player.transform.SetParent(root.transform);
            player.transform.position = position;
            player.layer = LayerMask.NameToLayer(Week23Hitbox0Setup.PlayerLayerName);
            body = player.GetComponent<CircleCollider2D>();
            body.radius = PlayerFeet.BodyRadius;
            Rigidbody2D rigidbody = player.GetComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
            rigidbody.interpolation = RigidbodyInterpolation2D.None;
            PlayerFeet.Ensure(player, out _);
            size = player.GetComponent<PlayerBodySize>();
            return player.GetComponent<PlayerMovement>();
        }

        private static void Box(GameObject root, string layerName, Vector2 position, Vector2 size)
        {
            GameObject box = new($"Hitbox-1 {layerName}", typeof(BoxCollider2D));
            box.transform.SetParent(root.transform);
            box.transform.position = position;
            box.layer = LayerMask.NameToLayer(layerName);
            box.GetComponent<BoxCollider2D>().size = size;
        }

        private static void Drive(PlayerMovement movement, Vector2 direction)
        {
            Rigidbody2D body = movement.GetComponent<Rigidbody2D>();
            Physics2D.SyncTransforms();
            for (int index = 0; index < SimulationSteps; index++)
            {
                body.linearVelocity = direction * MoveSpeed;
                Physics2D.Simulate(Time.fixedDeltaTime);
            }

            Physics2D.SyncTransforms();
        }

        private static void Run(string label, Action<GameObject> probe)
        {
            SimulationMode2D previousMode = Physics2D.simulationMode;
            GameObject root = new($"Hitbox-1 {label}");
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
