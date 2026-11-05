using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Contact-0: every ranged enemy hurts on touch for a fixed half heart, so pushing into one is not free.
    public static class RangedContactDamageSetup
    {
        public const float Cooldown = 1f;

        // Base first: HighBloodSugarFairy and QuickRangedFairy are variants of RangedEnemy and inherit its component.
        public static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/RangedEnemy.prefab",
            "Assets/Prefabs/HighBloodSugarFairy.prefab",
            "Assets/Prefabs/QuickRangedFairy.prefab",
            "Assets/Prefabs/LivelyLowBloodSugarFairy.prefab",
            "Assets/Prefabs/LobbingFairy.prefab",
            "Assets/Prefabs/CrayonArcherMinion.prefab",
            "Assets/Prefabs/CrayonMageMinion.prefab",
        };

        [MenuItem("Trickal Fan Game/Week 28/Setup Contact-0 Ranged Contact Damage")]
        public static void Setup()
        {
            foreach (string path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    throw new InvalidOperationException($"Contact-0 needs {path}.");
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Ensure(contents);
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                        throw new InvalidOperationException($"Could not save {path}.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Contact-0 ranged contact damage ready: half a heart, 1 s cooldown.");
        }

        public static ContactDamage Ensure(GameObject contents)
        {
            ContactDamage contact = contents.GetComponent<ContactDamage>();
            if (contact == null) contact = contents.AddComponent<ContactDamage>();
            contact.Configure(EnemyDamageTier.Light, Cooldown);
            contact.ConfigureHalfHeart(true);
            EditorUtility.SetDirty(contact);
            return contact;
        }
    }
}
