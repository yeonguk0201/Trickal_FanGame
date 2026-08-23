using System;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week6ItemSetup
    {
        private const string ItemFolder = "Assets/Items";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Trickal Fan Game/Setup Week 6 Items and Synergy")]
        public static void Setup()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            ItemDefinition[] itemPool =
            {
                LoadRequiredDefinition("item-01"),
                LoadRequiredDefinition("item-02"),
                LoadRequiredDefinition("item-03"),
                LoadOrCreateDefinition("item-06", "Multi Shot", ItemEffectType.MultiShot, 1f, 2),
                LoadOrCreateDefinition("item-11", "Piercing Projectile", ItemEffectType.Pierce, 1f, 2),
                LoadOrCreateDefinition("item-08", "Heal On Kill", ItemEffectType.HealOnKill, 1f, 3)
            };

            if (Array.Exists(itemPool, definition => definition == null))
            {
                Debug.LogError("Run the Week 5 item setup first; item-01 through item-03 are required.");
                return;
            }

            ItemDropSource[] dropSources = UnityEngine.Object.FindObjectsByType<ItemDropSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (ItemDropSource source in dropSources)
            {
                SerializedObject serializedSource = new(source);
                SerializedProperty poolProperty = serializedSource.FindProperty("itemPool");
                poolProperty.arraySize = itemPool.Length;
                for (int index = 0; index < itemPool.Length; index++)
                {
                    poolProperty.GetArrayElementAtIndex(index).objectReferenceValue = itemPool[index];
                }

                serializedSource.ApplyModifiedProperties();
                EditorUtility.SetDirty(source);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Week 6 item setup ready: {itemPool.Length} items are available from {dropSources.Length} reward sources. " +
                "Collect Multi Shot and Piercing Projectile in either order to activate the synergy.");
        }

        private static ItemDefinition LoadRequiredDefinition(string itemId)
        {
            return AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{itemId}.asset");
        }

        private static ItemDefinition LoadOrCreateDefinition(
            string itemId,
            string displayName,
            ItemEffectType effectType,
            float effectValue,
            int maxStacks)
        {
            string path = $"{ItemFolder}/{itemId}.asset";
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            SerializedObject serializedDefinition = new(definition);
            serializedDefinition.FindProperty("itemId").stringValue = itemId;
            serializedDefinition.FindProperty("displayName").stringValue = displayName;
            serializedDefinition.FindProperty("effectType").enumValueIndex = (int)effectType;
            serializedDefinition.FindProperty("effectValue").floatValue = effectValue;
            serializedDefinition.FindProperty("stackMode").enumValueIndex = (int)ItemStackMode.Additive;
            serializedDefinition.FindProperty("maxStacks").intValue = maxStacks;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }
    }
}
