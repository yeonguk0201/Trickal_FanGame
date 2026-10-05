using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Flight-0: creates 시스트의 가짜 날개 and puts it in the chest content table's golden exclusive pool, the only place
    // that may hold it. The selection reward pool (and the shop stock that shares it) is cleaned of exclusive artifacts.
    // Re-running updates the same asset and table, keeping their GUIDs, and leaves a clean pool unchanged.
    public static class Week22Flight0Setup
    {
        public const string FakeWingsId = "artifact-sist-fake-wings";
        public const string FakeWingsName = "시스트의 가짜 날개";
        public const string FakeWingsPath = "Assets/Items/" + FakeWingsId + ".asset";
        public const string ArtifactPickupPrefabPath = "Assets/Prefabs/ItemPickup.prefab";

        [MenuItem("Trickal Fan Game/Week 22/Setup Flight-0 Fake Wings")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Flight-0 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Flight-0 Fake Wings");

            ItemDefinition wings = ConfigureItem();
            ConfigureGoldenPool(wings);
            RemoveExclusivesFromSelectionPool();
            EnsureGlyphs(wings);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Flight-0 setup complete: {FakeWingsName} ({FakeWingsId}) is the golden chest exclusive " +
                      "artifact and stays out of the selection reward pool and shop stock.");
        }

        private static ItemDefinition ConfigureItem()
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(FakeWingsPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = FakeWingsId;
                AssetDatabase.CreateAsset(definition, FakeWingsPath);
                Undo.RegisterCreatedObjectUndo(definition, "Create " + FakeWingsId);
            }
            else
            {
                Undo.RecordObject(definition, "Configure " + FakeWingsId);
            }

            definition.ConfigureContract(FakeWingsId, FakeWingsName, ItemKind.Artifact, ItemRarity.Epic, true, 1,
                new ItemEffectEntry(ItemEffectType.Flight));
            if (!definition.IsValid)
                throw new InvalidOperationException($"{FakeWingsId} is not a valid item contract.");
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ConfigureGoldenPool(ItemDefinition wings)
        {
            ChestContentTable table =
                AssetDatabase.LoadAssetAtPath<ChestContentTable>(Week22Chest1Setup.ChestContentTablePath);
            if (table == null)
                throw new InvalidOperationException($"Run Chest-1 setup first: {Week22Chest1Setup.ChestContentTablePath}.");
            ItemPickup pickup = AssetDatabase.LoadAssetAtPath<GameObject>(ArtifactPickupPrefabPath)
                ?.GetComponent<ItemPickup>();
            if (pickup == null)
                throw new InvalidOperationException($"The artifact pickup Prefab is missing: {ArtifactPickupPrefabPath}.");

            Undo.RecordObject(table, "Configure golden exclusive pool");
            table.ConfigureGoldenExclusivePool(new[] { wings }, pickup);
            if (!table.TryValidate(out string error))
                throw new InvalidOperationException($"Flight-0 built an invalid chest content table. {error}");
            EditorUtility.SetDirty(table);
        }

        private static void RemoveExclusivesFromSelectionPool()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            ItemDefinition[] pool = assembler.SelectionRewardPool.ToArray();
            ItemDefinition[] retained = pool.Where(item => !GoldenChestExclusivePool.IsExclusive(item)).ToArray();
            if (retained.Length == pool.Length) return;

            Undo.RecordObject(assembler, "Remove golden exclusive artifacts from the reward pool");
            assembler.ConfigureSelectionRewards(assembler.RewardSelectionSession, retained);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Flight-0 setup.");
        }

        // The acquisition toast, reward card and pause list show the name and effect description.
        private static void EnsureGlyphs(ItemDefinition wings)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Flight-0 requires the Frontend TMP font.");
            Week13Hud3BSetup.EnsureDescriptionGlyphs(font);
            string characters = wings.DisplayName + ArtifactEffectDescription.Build(wings);
            if (!font.HasCharacters(characters) && !font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing Flight-0 glyphs: " + missing);

            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        }
    }
}
