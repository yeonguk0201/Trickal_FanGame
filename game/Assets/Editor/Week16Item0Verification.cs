using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week16Item0Verification
    {
        [MenuItem("Trickal Fan Game/Week 16/Verify Item-0 Common Item Kinds")]
        public static void Verify()
        {
            PhaseGArtifactContractVerification.Verify();
            VerifyStableIdContracts();
            VerifySharedInventoryHudPauseAndRunRecord();
            Debug.Log("Week 16 Item-0 verification passed: ARTIFACT and SPELL use stable IDs and share acquisition, " +
                      "stacking, HUD, pause-list, and Run-record paths.");
        }

        public static void SetupAndVerifyBatch()
        {
            string[] paths = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();

            Week16Item0Setup.Setup();
            Week16Item0Setup.Setup();

            Assert(paths.SequenceEqual(AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(path => path, StringComparer.Ordinal)),
                "Item-0 setup changed the ItemDefinition asset set when run twice.");
            Assert(guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID)),
                "Item-0 setup changed an existing ItemDefinition GUID.");
            Verify();
        }

        private static void VerifyStableIdContracts()
        {
            Assert(ItemDefinition.IsItemIdValidForKind("item-01", ItemKind.Artifact),
                "Legacy item-* IDs must remain valid ARTIFACT IDs.");
            Assert(ItemDefinition.IsItemIdValidForKind("artifact-life-gem", ItemKind.Artifact),
                "New artifact-* IDs must be valid ARTIFACT IDs.");
            Assert(ItemDefinition.IsItemIdValidForKind("spell-catch-that-one", ItemKind.Spell),
                "New spell-* IDs must be valid SPELL IDs.");
            Assert(!ItemDefinition.IsItemIdValidForKind("spell-catch-that-one", ItemKind.Artifact) &&
                   !ItemDefinition.IsItemIdValidForKind("artifact-life-gem", ItemKind.Spell) &&
                   !ItemDefinition.IsItemIdValidForKind("item-01", ItemKind.Spell),
                "Item kind and stable ID prefixes must not be interchangeable.");

            string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" });
            foreach (string guid in guids)
            {
                ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Assert(definition != null && definition.IsValid,
                    $"Invalid ItemDefinition asset: {AssetDatabase.GUIDToAssetPath(guid)}");
                if (definition.ItemId.StartsWith("item-", StringComparison.Ordinal))
                {
                    Assert(definition.Kind == ItemKind.Artifact,
                        $"Legacy stable ID changed kind: {definition.ItemId}");
                }
            }
        }

        private static void VerifySharedInventoryHudPauseAndRunRecord()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameArtifactHudView configuredHud = FindAll<GameArtifactHudView>(scene).Single();
            GamePauseArtifactView configuredPause = FindAll<GamePauseArtifactView>(scene).Single();
            GameObject player = new("Item-0 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory));
            GameObject hudClone = Object.Instantiate(configuredHud.gameObject);
            GameObject pauseClone = Object.Instantiate(configuredPause.gameObject);
            ItemDefinition artifact = CreateDefinition(
                "artifact-verification-life-gem", "검증용 아티팩트", ItemKind.Artifact);
            ItemDefinition spell = CreateDefinition(
                "spell-verification-catch-that-one", "검증용 스펠", ItemKind.Spell);

            try
            {
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                InvokeLifecycle(inventory, "Awake");

                GameArtifactHudView hud = hudClone.GetComponent<GameArtifactHudView>();
                hud.Configure(inventory, hud.SlotsRoot, hud.SlotTemplate, hud.OverflowText, 10);
                GamePauseArtifactView pause = pauseClone.GetComponent<GamePauseArtifactView>();
                pause.Configure(inventory, pause.Overlay, pause.EntriesRoot, pause.EntryTemplate, pause.EmptyText);

                Assert(inventory.TryAcquire(artifact) && inventory.TryAcquire(spell) && inventory.TryAcquire(spell),
                    "ARTIFACT and SPELL must both enter the common acquisition and stacking path.");
                Assert(inventory.AcquiredDefinitions.SequenceEqual(new[] { artifact, spell }) &&
                       inventory.GetStackCount(artifact.ItemId) == 1 && inventory.GetStackCount(spell.ItemId) == 2,
                    "Common inventory lost kind-independent acquisition order or stacks.");
                Assert(hud.VisibleSlotCount == 2 &&
                       hud.VisibleSlots[0].Definition.Kind == ItemKind.Artifact &&
                       hud.VisibleSlots[1].Definition.Kind == ItemKind.Spell &&
                       hud.VisibleSlots[1].StackCount == 2,
                    "The owned-item HUD did not display both Item kinds through the same path.");

                pause.RebuildEntries();
                Assert(pause.EntryCount == 2 &&
                       pause.Entries[0].Definition.Kind == ItemKind.Artifact &&
                       pause.Entries[1].Definition.Kind == ItemKind.Spell &&
                       pause.Entries[1].StackCount == 2,
                    "The pause owned-item list did not display both Item kinds through the same path.");
                Assert(inventory.AcquiredItems.Count == 3 &&
                       inventory.AcquiredItems[0].ItemId == artifact.ItemId &&
                       inventory.AcquiredItems[1].ItemId == spell.ItemId &&
                       inventory.AcquiredItems[2].ItemId == spell.ItemId &&
                       inventory.AcquiredItems.Select(item => item.Order).SequenceEqual(new[] { 1, 2, 3 }),
                    "The Run acquisition record must preserve selected Item IDs and order regardless of kind.");
            }
            finally
            {
                Object.DestroyImmediate(pauseClone);
                Object.DestroyImmediate(hudClone);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(artifact);
                Object.DestroyImmediate(spell);
            }
        }

        private static ItemDefinition CreateDefinition(string itemId, string displayName, ItemKind kind)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = itemId;
            definition.ConfigureContract(itemId, displayName, kind, ItemRarity.Common, true, 2,
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.01f));
            Assert(definition.IsValid, $"Verification definition is invalid: {itemId}");
            return definition;
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
