using System;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week20Move1Setup
    {
        public const string MaterialPath = "Assets/Settings/FrictionlessActor.physicsMaterial2D";
        public const string Physics2DSettingsPath = "ProjectSettings/Physics2DSettings.asset";
        // Kept at 0.5 while testing whether the frictionless material alone fixes the wall drag (0.38 was tried).
        public const float PlayerColliderRadius = 0.5f;
        public const float PreviousPlayerColliderRadius = 0.38f;

        public static readonly string[] PlayerScenes =
        {
            "Assets/Scenes/SampleScene.unity", "Assets/Scenes/ItemTestScene.unity",
            "Assets/Scenes/BossTestScene.unity", "Assets/Scenes/Boss2TestScene.unity",
        };

        [MenuItem("Trickal Fan Game/Week 20/Setup Move-1 Frictionless Actors and Player Collider")]
        public static void Setup()
        {
            PhysicsMaterial2D material = EnsureMaterial();
            AssignDefaultMaterial(material);
            ConfigurePlayerScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"Move-1 ready: Physics2D default material '{MaterialPath}' (friction 0, bounciness 0) " +
                      $"and player collider radius {PreviousPlayerColliderRadius} -> {PlayerColliderRadius} " +
                      $"in {PlayerScenes.Length} scenes.");
        }

        private static PhysicsMaterial2D EnsureMaterial()
        {
            PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
            if (material == null)
            {
                material = new PhysicsMaterial2D("FrictionlessActor");
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            if (material.friction != 0f || material.bounciness != 0f)
            {
                Undo.RecordObject(material, "Move-1 Frictionless Material");
                material.friction = 0f;
                material.bounciness = 0f;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static void AssignDefaultMaterial(PhysicsMaterial2D material)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(Physics2DSettingsPath);
            if (assets.Length == 0 || assets[0] == null)
            {
                throw new InvalidOperationException($"Move-1 could not load '{Physics2DSettingsPath}'.");
            }

            SerializedObject settings = new(assets[0]);
            SerializedProperty defaultMaterial = settings.FindProperty("m_DefaultMaterial");
            if (defaultMaterial == null)
            {
                throw new InvalidOperationException("Move-1 could not find Physics2D m_DefaultMaterial.");
            }

            if (defaultMaterial.objectReferenceValue == material) return;
            defaultMaterial.objectReferenceValue = material;
            settings.ApplyModifiedProperties();
            EditorUtility.SetDirty(assets[0]);
        }

        private static void ConfigurePlayerScenes()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Move-1 scene setup needs the open scenes saved or discarded.");
            }

            SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string scenePath in PlayerScenes)
                {
                    ConfigurePlayerScene(scenePath);
                }
            }
            finally
            {
                if (previousScenes.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                }
            }
        }

        private static void ConfigurePlayerScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int players = 0;
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PlayerMovement movement in root.GetComponentsInChildren<PlayerMovement>(true))
                {
                    players++;
                    CircleCollider2D collider = movement.GetComponent<CircleCollider2D>();
                    if (collider == null)
                    {
                        throw new InvalidOperationException($"{scenePath} player needs a CircleCollider2D.");
                    }

                    if (Mathf.Approximately(collider.radius, PlayerColliderRadius)) continue;
                    Undo.RecordObject(collider, "Move-1 Player Collider Radius");
                    collider.radius = PlayerColliderRadius;
                    EditorUtility.SetDirty(collider);
                    changed = true;
                }
            }

            if (players == 0)
            {
                throw new InvalidOperationException($"{scenePath} must contain a player.");
            }

            if (changed && !EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Move-1 could not save '{scenePath}'.");
            }
        }
    }
}
