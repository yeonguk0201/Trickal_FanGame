using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
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
    public static class Week13Hud3CVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify HUD-3C Pause Artifact List")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            VerifyRuntimeState(scene);
            Debug.Log("Week 13 HUD-3C verification passed: Escape pause foundation and complete artifact details including overflow.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week13Hud2Setup.Setup();
            Week13Hud3ASetup.Setup();
            Week13Hud3BSetup.Setup();
            Week13Hud3CSetup.Setup();
            int transformCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week13Hud3CSetup.Setup();
            Assert(transformCount == CountTransforms(EditorSceneManager.GetActiveScene()),
                "HUD-3C setup created duplicate hierarchy objects when run twice.");
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "HUD-3C setup changed the Game Scene GUID.");
            Verify();
            Week13Flow4Verification.Verify();
            Debug.Log("Week 13 HUD-3C batch verification passed: setup twice, stable Scene GUID and HUD-3B/HUD-3A/HUD-2/Flow-4 regression.");
        }

        public static void ValidateScene(Scene scene)
        {
            GamePauseArtifactView[] views = FindAll<GamePauseArtifactView>(scene);
            Assert(views.Length == 1, "Game Scene requires exactly one pause artifact view.");
            GamePauseArtifactView view = views[0];
            PlayerInventory inventory = FindAll<PlayerInventory>(scene).Single();
            Assert(view.Inventory == inventory, "HUD-3C must reference the actual player inventory.");
            Assert(view.Overlay != null && view.EntriesRoot != null && view.EntryTemplate != null && view.EmptyText != null,
                "HUD-3C visual references are missing.");
            Assert(!view.IsPaused && Mathf.Approximately(view.Overlay.alpha, 0f) &&
                !view.Overlay.interactable && !view.Overlay.blocksRaycasts,
                "HUD-3C overlay must begin hidden and non-blocking.");
            Assert(!view.EntryTemplate.gameObject.activeSelf && view.EntryTemplate.Icon != null &&
                view.EntryTemplate.StableIdText != null && view.EntryTemplate.NameText != null &&
                view.EntryTemplate.StackText != null && view.EntryTemplate.DescriptionText != null,
                "HUD-3C requires one inactive, fully configured detail-row template.");
            Assert(view.GetComponent<RectTransform>().sizeDelta == new Vector2(1920, 1080),
                "HUD-3C overlay must cover the full reference canvas.");
            RectTransform panel = view.transform.Find("Pause Panel") as RectTransform;
            Assert(panel != null && panel.sizeDelta == new Vector2(1120, 760),
                "HUD-3C pause panel must fit the specified central popup range.");
            ScrollRect scroll = view.EntriesRoot.parent.GetComponent<ScrollRect>();
            Assert(scroll != null && scroll.vertical && !scroll.horizontal && scroll.content == view.EntriesRoot,
                "HUD-3C complete list must be vertically scrollable.");
            Week13Hud3BVerification.ValidateScene(scene);
        }

        private static void VerifyRuntimeState(Scene scene)
        {
            GameObject player = new("HUD-3C Verification Player", typeof(Rigidbody2D), typeof(Health),
                typeof(PlayerStats), typeof(PlayerActionState), typeof(PlayerMovement), typeof(PlayerAttack),
                typeof(PlayerSP), typeof(PlayerSkill), typeof(PlayerUltimate), typeof(PlayerInventory));
            GameObject overlayClone = Object.Instantiate(FindAll<GamePauseArtifactView>(scene).Single().gameObject);
            List<ItemDefinition> definitions = new();
            float originalTimeScale = Time.timeScale;
            try
            {
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerActionState>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerMovement>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerAttack>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSkill>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerUltimate>(), "Awake");
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                InvokeLifecycle(inventory, "Awake");

                GamePauseArtifactView view = overlayClone.GetComponent<GamePauseArtifactView>();
                view.Configure(inventory, view.Overlay, view.EntriesRoot, view.EntryTemplate, view.EmptyText);

                Time.timeScale = 1f;
                Assert(view.TryPause(), "HUD-3C failed to enter pause from active game time.");
                Assert(view.IsPaused && Mathf.Approximately(Time.timeScale, 0f) &&
                    Mathf.Approximately(view.Overlay.alpha, 1f) && view.Overlay.blocksRaycasts,
                    "Pause must stop scaled game time and show a blocking overlay.");
                Assert(player.GetComponent<PlayerMovement>().enabled && player.GetComponent<PlayerAttack>().enabled &&
                    player.GetComponent<PlayerSkill>().enabled && player.GetComponent<PlayerUltimate>().enabled,
                    "Pause must preserve component state so active attacks and skills can resume exactly.");
                Assert(view.EntryCount == 0 && view.EmptyText.gameObject.activeSelf,
                    "An empty inventory must show the explicit empty state.");
                Assert(view.Resume() && Mathf.Approximately(Time.timeScale, 1f) && !view.Overlay.blocksRaycasts,
                    "Resume must restore game time and release the overlay input block.");

                for (int i = 1; i <= 12; i++)
                {
                    ItemDefinition definition = CreateDefinition(i);
                    definitions.Add(definition);
                    Assert(inventory.TryAcquire(definition), "HUD-3C artifact acquisition failed at " + i + ".");
                }
                Assert(inventory.TryAcquire(definitions[10]), "HUD-3C stack acquisition failed.");
                Assert(view.TryPause() && view.EntryCount == 12 && !view.EmptyText.gameObject.activeSelf,
                    "Pause list must include every artifact, including HUD overflow beyond ten.");
                for (int i = 0; i < definitions.Count; i++)
                {
                    ArtifactPauseListEntryView entry = view.Entries[i];
                    int expectedStack = i == 10 ? 2 : 1;
                    Assert(entry.Definition == definitions[i] && entry.StackCount == expectedStack &&
                        entry.NameText.text == definitions[i].DisplayName && entry.StackText.text == $"×{expectedStack}" &&
                        entry.DescriptionText.text == ArtifactEffectDescription.Build(definitions[i]),
                        "HUD-3C entry lost acquisition order, stack, name, or description at index " + i + ".");
                }
                Assert(view.Resume(), "HUD-3C failed to resume after populated-list verification.");

                Time.timeScale = 0f;
                Assert(!view.TryPause() && !view.IsPaused && Mathf.Approximately(view.Overlay.alpha, 0f),
                    "HUD-3C must not stack over another time-stopping popup.");
            }
            finally
            {
                if (overlayClone != null)
                {
                    GamePauseArtifactView view = overlayClone.GetComponent<GamePauseArtifactView>();
                    if (view != null && view.IsPaused) view.Resume();
                    Object.DestroyImmediate(overlayClone);
                }
                Object.DestroyImmediate(player);
                foreach (ItemDefinition definition in definitions) Object.DestroyImmediate(definition);
                Time.timeScale = originalTimeScale;
            }
        }

        private static ItemDefinition CreateDefinition(int index)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = "HUD-3C Test Item " + index;
            definition.ConfigureContract("item-pause-" + index.ToString("00"), "테스트 아티팩트 " + index,
                (ItemRarity)((index - 1) % 4), true, 2,
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.01f * index));
            return definition;
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
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
