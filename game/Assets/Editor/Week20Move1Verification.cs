using System;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week20Move1Verification
    {
        private const float MoveSpeed = 5f;
        private const int SimulationSteps = 50;
        // Unity's implicit default friction when Physics2D has no default material.
        private const float PreviousFriction = 0.4f;
        // The narrowest obstacle gap seen in play (wall to single obstacle, diagonal obstacle pair).
        private const float NarrowGap = 1.4f;

        [MenuItem("Trickal Fan Game/Week 20/Setup and Verify Move-1 Frictionless Actors and Player Collider")]
        public static void SetupAndVerifyBatch()
        {
            Week20Move1Setup.Setup();
            string materialGuid = AssetDatabase.AssetPathToGUID(Week20Move1Setup.MaterialPath);
            Week20Move1Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(materialGuid) &&
                   materialGuid == AssetDatabase.AssetPathToGUID(Week20Move1Setup.MaterialPath),
                "Move-1 setup rerun changed or lost the frictionless material GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 20/Verify Move-1 Frictionless Actors and Player Collider")]
        public static void Verify()
        {
            PhysicsMaterial2D material = ValidateDefaultMaterial();
            ValidateActorPrefabs();

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Move-1 scene checks need the open scenes saved or discarded.");
            }

            SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
            float frictionlessRatio;
            float previousRatio;
            try
            {
                foreach (string scenePath in Week20Move1Setup.PlayerScenes)
                {
                    ValidatePlayerScene(scenePath);
                }

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                frictionlessRatio = SimulateWallSlide(null);
                PhysicsMaterial2D rough = new("Move-1 Previous Friction") { friction = PreviousFriction };
                try
                {
                    previousRatio = SimulateWallSlide(rough);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(rough);
                }
            }
            finally
            {
                if (previousScenes.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                }
            }

            Assert(previousRatio < 0.85f,
                $"The friction {PreviousFriction} control slide kept {previousRatio:P0} of its speed; " +
                "the wall-slide probe no longer measures friction.");
            Assert(frictionlessRatio >= 0.95f,
                $"Sliding diagonally along a wall kept only {frictionlessRatio:P0} of the tangential speed.");
            Debug.Log($"Move-1 verification passed: Physics2D default material '{material.name}' has friction 0, " +
                      "no actor Prefab overrides it with friction, all player scenes use collider radius " +
                      $"{Week20Move1Setup.PlayerColliderRadius} (clearance {NarrowGap - Week20Move1Setup.PlayerColliderRadius * 2f:0.##} " +
                      $"in a {NarrowGap} gap), and a diagonal wall slide keeps {frictionlessRatio:P0} of its speed " +
                      $"(was {previousRatio:P0} at friction {PreviousFriction}).");
        }

        private static PhysicsMaterial2D ValidateDefaultMaterial()
        {
            PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(Week20Move1Setup.MaterialPath);
            Assert(material != null, "Run Move-1 setup to create the frictionless material.");
            Assert(material.friction == 0f && material.bounciness == 0f,
                "The Move-1 material must have friction 0 and bounciness 0.");

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(Week20Move1Setup.Physics2DSettingsPath);
            Assert(assets.Length > 0 && assets[0] != null, "Move-1 could not load the Physics2D settings.");
            SerializedProperty defaultMaterial = new SerializedObject(assets[0]).FindProperty("m_DefaultMaterial");
            Assert(defaultMaterial != null && defaultMaterial.objectReferenceValue == material,
                "Physics2D Default Material must be the Move-1 frictionless material.");
            return material;
        }

        // A collider or body material with friction would reintroduce the drag against walls and obstacles.
        private static void ValidateActorPrefabs()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Rooms" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                foreach (Rigidbody2D body in prefab.GetComponentsInChildren<Rigidbody2D>(true))
                {
                    AssertFrictionless(body.sharedMaterial, $"{path} Rigidbody2D '{body.name}'");
                }

                foreach (Collider2D collider in prefab.GetComponentsInChildren<Collider2D>(true))
                {
                    if (collider.isTrigger) continue;
                    AssertFrictionless(collider.sharedMaterial, $"{path} {collider.GetType().Name} '{collider.name}'");
                }
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
                    CircleCollider2D collider = movement.GetComponent<CircleCollider2D>();
                    Assert(collider != null && !collider.isTrigger &&
                           Mathf.Approximately(collider.radius, Week20Move1Setup.PlayerColliderRadius) &&
                           collider.offset == Vector2.zero,
                        $"{scenePath} player collider must be a solid circle of radius " +
                        $"{Week20Move1Setup.PlayerColliderRadius} centered on the player.");
                    AssertFrictionless(collider.sharedMaterial, $"{scenePath} player collider");
                    AssertFrictionless(movement.GetComponent<Rigidbody2D>().sharedMaterial, $"{scenePath} player body");
                }
            }

            Assert(players > 0, $"{scenePath} must contain a player.");
        }

        // Pushes a player-sized body diagonally into a wall like PlayerMovement does, returning the share of
        // the tangential speed it keeps while sliding.
        private static float SimulateWallSlide(PhysicsMaterial2D material)
        {
            SimulationMode2D previousMode = Physics2D.simulationMode;
            GameObject wall = new("Move-1 Wall");
            GameObject actor = new("Move-1 Actor");
            try
            {
                BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
                wallCollider.size = new Vector2(40f, 1f);
                wallCollider.sharedMaterial = material;

                float radius = Week20Move1Setup.PlayerColliderRadius;
                actor.transform.position = new Vector3(-10f, 0.5f + radius + 0.01f, 0f);
                Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.interpolation = RigidbodyInterpolation2D.None;
                CircleCollider2D actorCollider = actor.AddComponent<CircleCollider2D>();
                actorCollider.radius = radius;
                actorCollider.sharedMaterial = material;
                Physics2D.SyncTransforms();

                Physics2D.simulationMode = SimulationMode2D.Script;
                Vector2 velocity = new Vector2(1f, -1f).normalized * MoveSpeed;
                float step = Time.fixedDeltaTime;
                float startX = body.position.x;
                for (int index = 0; index < SimulationSteps; index++)
                {
                    body.linearVelocity = velocity;
                    Physics2D.Simulate(step);
                }

                Assert(body.position.y > 0.5f, "The wall-slide probe passed through the wall.");
                float expected = velocity.x * step * SimulationSteps;
                return (body.position.x - startX) / expected;
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(wall);
            }
        }

        private static void AssertFrictionless(PhysicsMaterial2D material, string owner)
        {
            Assert(material == null || material.friction == 0f,
                $"{owner} overrides the frictionless default with friction {material?.friction}.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
