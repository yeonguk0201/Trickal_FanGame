using System;
using System.Collections.Generic;
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
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week13Hud3AVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-3A Artifact List")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            VerifyRuntimeState(scene);
            Debug.Log("Week 13 HUD-3A verification passed: acquisition order, live stacks, ten-slot limit and overflow count.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Hud2Setup.Setup();
            Week13Hud3ASetup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud3ASetup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-3A setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-3A setup changed the Game Scene GUID.");
            Verify();
            Week13Flow4Verification.Verify();
            Debug.Log("Week 13 HUD-3A batch verification passed: setup twice, stable Scene GUID and HUD-2/Flow-4 regression.");
        }

        public static void ValidateScene(Scene scene)
        {
            GameArtifactHudView[] views = FindAll<GameArtifactHudView>(scene);
            Assert(views.Length == 1, "Game Scene requires exactly one GameArtifactHudView.");
            GameArtifactHudView view = views[0];
            PlayerInventory inventory = FindAll<PlayerInventory>(scene).Single();
            Assert(view.Inventory == inventory, "HUD-3A must reference the actual player inventory.");
            Assert(view.SlotsRoot != null && view.SlotTemplate != null && view.OverflowText != null,
                "HUD-3A visual references are missing.");
            Assert(view.MaximumVisible == 10 && !view.SlotTemplate.gameObject.activeSelf,
                "HUD-3A requires one inactive template and a ten-artifact visible limit.");
            Assert(view.SlotTemplate.GetComponent<RectTransform>().sizeDelta == new Vector2(48, 48) &&
                view.SlotTemplate.Icon != null && view.SlotTemplate.IconText != null &&
                view.SlotTemplate.StackBadge != null && view.SlotTemplate.StackText != null,
                "HUD-3A slot template must be a fully configured 48x48 icon with a stack badge.");

            GridLayoutGroup grid = view.SlotsRoot.GetComponent<GridLayoutGroup>();
            Assert(grid != null && grid.cellSize == new Vector2(48, 48) && grid.spacing == new Vector2(8, 8) &&
                grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount == 5,
                "HUD-3A slots must use the specified five-column, two-row layout.");
            RectTransform panel = view.GetComponent<RectTransform>();
            Assert(panel.sizeDelta == new Vector2(288, 112) && panel.anchoredPosition == new Vector2(-752, -430),
                "Artifact HUD must occupy the specified bottom-left 288x112 region.");
            Assert(!view.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget),
                "HUD-3A graphics must not intercept combat input.");
            Assert(FindAll<ArtifactHudSlotView>(scene).Length == 1,
                "The saved Game Scene must contain only the inactive artifact slot template.");
            Week13Hud2Verification.ValidateScene(scene);
        }

        private static void VerifyRuntimeState(Scene scene)
        {
            GameObject player = new("HUD-3A Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory));
            GameObject hudClone = Object.Instantiate(FindAll<GameArtifactHudView>(scene).Single().gameObject);
            List<ItemDefinition> definitions = new();
            try
            {
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                InvokeLifecycle(inventory, "Awake");

                GameArtifactHudView view = hudClone.GetComponent<GameArtifactHudView>();
                view.Configure(inventory, view.SlotsRoot, view.SlotTemplate, view.OverflowText, 10);
                Assert(view.VisibleSlotCount == 0 && view.OverflowCount == 0 && !view.OverflowText.gameObject.activeSelf,
                    "An empty inventory must produce an empty artifact HUD.");

                for (int i = 1; i <= 11; i++) definitions.Add(CreateDefinition(i));
                Assert(inventory.TryAcquire(definitions[0]), "First artifact acquisition failed.");
                Assert(inventory.TryAcquire(definitions[0]), "Artifact stack acquisition failed.");
                Assert(view.VisibleSlotCount == 1 && view.VisibleSlots[0].Definition == definitions[0] &&
                    view.VisibleSlots[0].StackCount == 2 && view.VisibleSlots[0].StackText.text == "2",
                    "A repeated acquisition must update the existing first slot and stack badge.");

                for (int i = 1; i < definitions.Count; i++)
                    Assert(inventory.TryAcquire(definitions[i]), "Artifact acquisition failed for " + definitions[i].ItemId);

                Assert(inventory.AcquiredDefinitions.Count == 11 && inventory.AcquiredItems.Count == 12,
                    "Inventory must keep unique display order separately from the complete acquisition record.");
                Assert(view.VisibleSlotCount == 10 && view.OverflowCount == 1 &&
                    view.OverflowText.gameObject.activeSelf && view.OverflowText.text == "+1",
                    "Eleven artifact types must keep ten visible slots and show +1 overflow.");
                for (int i = 0; i < 10; i++)
                {
                    Assert(view.VisibleSlots[i].Definition == definitions[i],
                        "Artifact HUD did not preserve first-acquisition order at index " + i + ".");
                    Assert(view.VisibleSlots[i].IconText.text == (i + 1).ToString("00"),
                        "Temporary artifact icon does not expose its stable ID suffix.");
                }

                view.RefreshNow();
                Assert(view.VisibleSlotCount == 10 && view.OverflowCount == 1 &&
                    view.VisibleSlots[0].StackCount == 2 && view.VisibleSlots[9].Definition == definitions[9],
                    "HUD rebuild changed acquisition order, stack state, or overflow count.");
            }
            finally
            {
                Object.DestroyImmediate(hudClone);
                Object.DestroyImmediate(player);
                foreach (ItemDefinition definition in definitions) Object.DestroyImmediate(definition);
            }
        }

        private static ItemDefinition CreateDefinition(int index)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = "HUD-3A Test Item " + index;
            definition.ConfigureContract(
                "item-" + index.ToString("00"),
                "HUD Test Artifact " + index,
                (ItemRarity)((index - 1) % 4),
                true,
                0,
                new ItemEffectEntry(ItemEffectType.AttackDamage, 0.01f));
            return definition;
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static int CountTransforms(Scene scene) => scene.GetRootGameObjects()
            .Sum(root => root.GetComponentsInChildren<Transform>(true).Length);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
