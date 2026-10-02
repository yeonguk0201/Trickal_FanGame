using System;
using System.Linq;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Spell-2: creates the single-use 그건 내 잔상 asset, points the Game Scene player's single-use executor at the
    // room graph it escapes through, and retires the legacy spell-afterimage from the selection reward pool.
    // Re-running updates the same asset (keeping its GUID) and the same scene references.
    public static class Week21Spell2Setup
    {
        public const string AfterimageId = "single-spell-afterimage";
        public const string AfterimagePath = "Assets/Items/" + AfterimageId + ".asset";
        public const string LegacyAfterimageId = "spell-afterimage";
        public const string LegacyAfterimagePath = "Assets/Items/" + LegacyAfterimageId + ".asset";

        [MenuItem("Trickal Fan Game/Week 21/Setup Spell-2 Afterimage")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Spell-2 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Spell-2 Afterimage");

            ConfigureItem();
            ConfigurePlayerRoomGraph();
            LegacySpellRetirement.RetireFromGameSceneRewardPool("Retire legacy spells from the reward pool");

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Spell-2 setup complete: 그건 내 잔상 single-use spell configured, the player escapes through " +
                      "the Game Scene room graph and the legacy spell-afterimage left the selection reward pool.");
        }

        private static void ConfigureItem()
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AfterimagePath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = AfterimageId;
                AssetDatabase.CreateAsset(definition, AfterimagePath);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + AfterimageId);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + AfterimageId);
            }

            definition.ConfigureContract(AfterimageId, "그건 내 잔상", ItemKind.SingleUseSpell, ItemRarity.Uncommon,
                true, 1, new ItemEffectEntry(ItemEffectType.EscapeToFloorStartRoom));
            if (!definition.IsValid)
                throw new InvalidOperationException($"{AfterimageId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
        }

        private static void ConfigurePlayerRoomGraph()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphController graph = FindSingle<RoomGraphController>(scene, "RoomGraphController");
            PlayerMovement player = graph.Player;
            PlayerSingleUseEffects effects = player != null ? player.GetComponent<PlayerSingleUseEffects>() : null;
            if (effects == null)
                throw new InvalidOperationException("Run Slot-0 setup first: the room graph player has no " +
                                                    "PlayerSingleUseEffects.");
            if (effects.RoomGraph == graph) return;

            Undo.RecordObject(effects, "Configure Spell-2 room graph");
            effects.Configure(graph);
            EditorUtility.SetDirty(effects);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Spell-2 setup.");
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }
    }
}
