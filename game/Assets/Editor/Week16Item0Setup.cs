using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week16Item0Setup
    {
        [MenuItem("Trickal Fan Game/Week 16/Setup Item-0 Common Item Kinds")]
        public static void Setup()
        {
            PhaseGArtifactContractSetup.Setup();

            string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (definition == null || definition.Kind != ItemKind.Artifact)
                {
                    continue;
                }

                EditorUtility.SetDirty(definition);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Week 16 Item-0 setup complete: existing item-* assets retain the ARTIFACT kind contract.");
        }
    }
}
