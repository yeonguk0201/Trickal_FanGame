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
    // Hitbox-0: splits the player's terrain collision (feet) from the hurtbox (body). Adds the PlayerFeet layer,
    // moves the Environment and Pit collisions from the Player layer to it, and gives every scene player a feet child.
    public static class Week23Hitbox0Setup
    {
        public const string PlayerLayerName = "Player";
        public const string TagManagerPath = "ProjectSettings/TagManager.asset";
        public const string Physics2DSettingsPath = "ProjectSettings/Physics2DSettings.asset";

        public static string[] PlayerScenes => Week20Move1Setup.PlayerScenes;

        [MenuItem("Trickal Fan Game/Week 23/Setup Hitbox-0 Player Feet Collider")]
        public static void Setup()
        {
            int feetLayer = EnsureFeetLayer();
            ConfigureCollisions(feetLayer);
            int added = ConfigurePlayerScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"Hitbox-0 ready: the {PlayerFeet.LayerName} layer collides with " +
                      $"{string.Join(" and ", PlayerFeet.CollisionLayers)} only, the {PlayerLayerName} layer no longer " +
                      $"does, and {PlayerScenes.Length} scenes have a feet collider of radius {PlayerFeet.Radius} " +
                      $"({added} changed).");
        }

        public static int EnsureFeetLayer()
        {
            int existing = PlayerFeet.Layer;
            if (existing >= 0) return existing;

            SerializedObject tagManager = new(AssetDatabase.LoadAllAssetsAtPath(TagManagerPath)[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            // Layers 0~7 are reserved by Unity.
            for (int index = 8; index < layers.arraySize; index++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = PlayerFeet.LayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return index;
            }

            throw new InvalidOperationException($"Hitbox-0 found no free user layer for {PlayerFeet.LayerName}.");
        }

        // The feet row collides with terrain only. The Player row keeps everything else it had: enemies, pickups and
        // the Default triggers (doorways, rooms, enemy projectiles) still meet the body.
        public static void ConfigureCollisions(int feetLayer)
        {
            int playerLayer = LayerMask.NameToLayer(PlayerLayerName);
            HashSet<int> terrain = new(PlayerFeet.CollisionLayers.Select(LayerMask.NameToLayer));
            if (playerLayer < 0 || terrain.Contains(-1))
                throw new InvalidOperationException(
                    $"Hitbox-0 needs the {PlayerLayerName}, {string.Join(" and ", PlayerFeet.CollisionLayers)} layers.");

            SerializedObject settings = new(AssetDatabase.LoadAllAssetsAtPath(Physics2DSettingsPath)[0]);
            SerializedProperty matrix = settings.FindProperty("m_LayerCollisionMatrix");
            if (matrix == null || !matrix.isArray || matrix.arraySize < 32)
                throw new InvalidOperationException("Hitbox-0 could not read the 2D layer collision matrix.");
            for (int layer = 0; layer < 32; layer++)
            {
                bool collide = terrain.Contains(layer);
                SetBit(matrix.GetArrayElementAtIndex(layer), feetLayer, collide);
                SetBit(matrix.GetArrayElementAtIndex(feetLayer), layer, collide);
            }

            foreach (int layer in terrain)
            {
                SetBit(matrix.GetArrayElementAtIndex(layer), playerLayer, false);
                SetBit(matrix.GetArrayElementAtIndex(playerLayer), layer, false);
            }

            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            for (int layer = 0; layer < 32; layer++)
            {
                if (Physics2D.GetIgnoreLayerCollision(feetLayer, layer) == terrain.Contains(layer))
                    throw new InvalidOperationException(
                        $"Hitbox-0 could not set the feet collision with layer {LayerMask.LayerToName(layer)}.");
            }

            foreach (int layer in terrain)
            {
                if (!Physics2D.GetIgnoreLayerCollision(playerLayer, layer))
                    throw new InvalidOperationException(
                        $"Hitbox-0 could not turn off the body collision with layer {LayerMask.LayerToName(layer)}.");
            }
        }

        private static void SetBit(SerializedProperty row, int bit, bool value)
        {
            long mask = 1L << bit;
            long current = row.longValue & 0xFFFFFFFFL;
            row.longValue = value ? current | mask : current & ~mask;
        }

        private static int ConfigurePlayerScenes()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Hitbox-0 scene setup needs the open scenes saved or discarded.");
            }

            SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
            int changedPlayers = 0;
            try
            {
                foreach (string scenePath in PlayerScenes)
                {
                    changedPlayers += ConfigurePlayerScene(scenePath);
                }
            }
            finally
            {
                if (previousScenes.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                }
            }

            return changedPlayers;
        }

        private static int ConfigurePlayerScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int players = 0;
            int changedPlayers = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PlayerMovement movement in root.GetComponentsInChildren<PlayerMovement>(true))
                {
                    players++;
                    bool existed = movement.GetComponentInChildren<PlayerFeet>(true) != null;
                    if (!PlayerFeet.Ensure(movement.gameObject, out PlayerFeet feet)) continue;
                    if (!existed) Undo.RegisterCreatedObjectUndo(feet.gameObject, "Hitbox-0 Player Feet");
                    EditorUtility.SetDirty(feet.gameObject);
                    changedPlayers++;
                }
            }

            if (players == 0)
            {
                throw new InvalidOperationException($"{scenePath} must contain a player.");
            }

            if (changedPlayers > 0 &&
                (!EditorSceneManager.MarkSceneDirty(scene) || !EditorSceneManager.SaveScene(scene)))
            {
                throw new InvalidOperationException($"Hitbox-0 could not save '{scenePath}'.");
            }

            return changedPlayers;
        }
    }
}
