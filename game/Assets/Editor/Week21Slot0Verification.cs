using System;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Slot-0: single-use spells and jjangsem spells share one slot. Verifies the kind contract, single acquisition
    // records per instance, swap without loss or duplication, one consumption per use, use blocking while paused,
    // dead or after the Run ends, and the Game Scene wiring.
    public static class Week21Slot0Verification
    {
        public static void SetupAndVerifyBatch()
        {
            Week21Slot0Setup.Setup();
            string[] paths = { Week21Slot0Setup.PickupPrefabPath, Week13FrontendSetup.GameScenePath };
            string[] guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Week21Slot0Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID)),
                "Slot-0 setup changed the pickup Prefab or Game Scene GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 21/Verify Slot-0 Spell Slot")]
        public static void Verify()
        {
            ValidateKindContract();
            ValidateSlotRuntime();
            ValidateHudRuntime();
            ValidatePrefab();
            ValidateGameScene();
            Debug.Log("Slot-0 verification passed: SingleUseSpell = 2 / JjangsemSpell = 3 with their own prefixes, " +
                      "one shared slot, one Run record per instance, swaps that drop the held item without loss or " +
                      "duplication, one consumption per use, no use while paused, dead, stopped or without an " +
                      "effect, and the Game Scene player and HUD wiring.");
        }

        private static void ValidateKindContract()
        {
            Assert((int)ItemKind.Artifact == 0 && (int)ItemKind.Spell == 1 &&
                   (int)ItemKind.SingleUseSpell == 2 && (int)ItemKind.JjangsemSpell == 3,
                "ItemKind values are serialized by number and must stay Artifact 0, Spell 1, SingleUseSpell 2, " +
                "JjangsemSpell 3.");
            Assert(Enum.GetValues(typeof(ItemKind)).Length == 4,
                "Reserved kinds (Trinket 4, Active 5) must not be added before their first item.");
            Assert(ItemDefinition.IsItemIdValidForKind("single-spell-catch-that-one", ItemKind.SingleUseSpell) &&
                   ItemDefinition.IsItemIdValidForKind("jjangsem-melune-card", ItemKind.JjangsemSpell),
                "New single-use prefixes must be valid for their kinds.");
            Assert(!ItemDefinition.IsItemIdValidForKind("spell-catch-that-one", ItemKind.SingleUseSpell) &&
                   !ItemDefinition.IsItemIdValidForKind("single-spell-catch-that-one", ItemKind.Spell) &&
                   !ItemDefinition.IsItemIdValidForKind("single-spell-catch-that-one", ItemKind.Artifact) &&
                   !ItemDefinition.IsItemIdValidForKind("single-spell-catch-that-one", ItemKind.JjangsemSpell) &&
                   !ItemDefinition.IsItemIdValidForKind("jjangsem-melune-card", ItemKind.SingleUseSpell) &&
                   !ItemDefinition.IsItemIdValidForKind("artifact-life-gem", ItemKind.JjangsemSpell),
                "Kind prefixes must not overlap.");
            Assert(ItemKindText.GetDisplayName(ItemKind.SingleUseSpell) == "스펠" &&
                   ItemKindText.GetDisplayName(ItemKind.JjangsemSpell) == "짱셈스펠" &&
                   ItemKindText.GetDisplayName(ItemKind.Artifact) == "아티팩트",
                "Kind labels must read 스펠 / 짱셈스펠 / 아티팩트.");
            Assert(!ItemKindText.IsSingleUse(ItemKind.Artifact) && !ItemKindText.IsSingleUse(ItemKind.Spell),
                "The legacy always-on Spell must not be treated as single-use.");

            ItemDefinition stacked = CreateDefinition("single-spell-slot0-stacked", "검증용 중첩", ItemKind.SingleUseSpell, 2);
            try
            {
                Assert(!stacked.IsValid, "A single-use item must have maxStacks = 1.");
            }
            finally
            {
                Object.DestroyImmediate(stacked);
            }
        }

        private static void ValidateSlotRuntime()
        {
            SingleUseItemPickup prefab = AssetDatabase.LoadAssetAtPath<SingleUseItemPickup>(
                Week21Slot0Setup.PickupPrefabPath);
            Assert(prefab != null, "Run Slot-0 setup first: the single-use pickup Prefab is missing.");

            GameObject progressObject = new("Slot-0 Verification Progress", typeof(RunProgress));
            GameObject floor = new("Slot-0 Verification Floor");
            GameObject player = new("Slot-0 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory), typeof(PlayerSpellSlot));
            ItemDefinition spell = CreateDefinition("single-spell-slot0-a", "검증용 스펠", ItemKind.SingleUseSpell);
            ItemDefinition jjangsem = CreateDefinition("jjangsem-slot0-b", "검증용 짱셈스펠", ItemKind.JjangsemSpell);
            float previousTimeScale = Time.timeScale;
            try
            {
                RunProgress progress = progressObject.GetComponent<RunProgress>();
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                InvokeLifecycle(inventory, "Awake");
                PlayerSpellSlot slot = player.GetComponent<PlayerSpellSlot>();
                Assert(player.GetComponent<PlayerSingleUseEffects>() != null,
                    "PlayerSpellSlot must bring its default executor.");
                slot.Configure(progress, prefab);
                InvokeLifecycle(slot, "Awake");
                Time.timeScale = 1f;

                // The default executor has no connected effects yet, so nothing can be consumed without an effect.
                SingleUseItemPickup first = Spawn(prefab, floor, spell, "slot0-instance-a");
                Assert(slot.TryCollect(first) && slot.HeldDefinition == spell &&
                       slot.HeldInstanceId == "slot0-instance-a" && first.IsCollected,
                    "An empty slot must take a touched single-use item.");
                Assert(slot.TryUse() == SpellSlotUseResult.ConditionNotMet && slot.HeldDefinition == spell,
                    "An item without a connected effect must not be consumed.");
                Assert(inventory.AcquiredItems.Count == 1 && inventory.AcquiredItems[0].ItemId == spell.ItemId &&
                       inventory.AcquiredItems[0].Order == 1 && inventory.AcquiredDefinitions.Count == 0,
                    "A single-use item is recorded for the Run once and never joins the artifact list.");

                // Swap: the held item drops where the new one was, keeps its instance, and waits for the player.
                SingleUseItemPickup second = Spawn(prefab, floor, jjangsem, "slot0-instance-b");
                second.transform.position = new Vector3(3f, 2f, 0f);
                Assert(slot.TryCollect(second) && slot.HeldDefinition == jjangsem, "A full slot must swap.");
                SingleUseItemPickup[] onFloor = FloorPickups(floor);
                Assert(onFloor.Length == 1 && onFloor[0].Definition == spell &&
                       onFloor[0].InstanceId == "slot0-instance-a" && onFloor[0].WaitsForPlayerExit &&
                       onFloor[0].transform.position == new Vector3(3f, 2f, 0f) &&
                       onFloor[0].transform.parent == floor.transform,
                    "A swap must drop exactly the held instance at the pickup position, waiting for the player to leave.");
                Assert(inventory.AcquiredItems.Count == 2 && inventory.AcquiredItems[1].ItemId == jjangsem.ItemId &&
                       inventory.AcquiredItems[1].Order == 2,
                    "Each new instance is recorded once when it first enters the slot.");

                // Swapping back must not create, lose, or re-record anything.
                Assert(slot.TryCollect(onFloor[0]) && slot.HeldInstanceId == "slot0-instance-a",
                    "A dropped item must be collectable again.");
                onFloor = FloorPickups(floor);
                Assert(onFloor.Length == 1 && onFloor[0].InstanceId == "slot0-instance-b" &&
                       inventory.AcquiredItems.Count == 2,
                    "Swapping back must keep exactly two instances and add no Run record.");

                // A full slot without a pickup Prefab cannot drop the held item, so it must refuse the swap.
                slot.Configure(progress, null);
                Assert(!slot.TryCollect(onFloor[0]) && !onFloor[0].IsCollected &&
                       slot.HeldInstanceId == "slot0-instance-a",
                    "A swap that cannot drop the held item must not happen.");
                slot.Configure(progress, prefab);

                // Inventory must refuse direct single-use acquisition (it would apply effects while held).
                Assert(!inventory.TryAcquire(spell) && inventory.AcquiredItems.Count == 2,
                    "PlayerInventory.TryAcquire must reject single-use items.");
                Assert(!inventory.TryRecordSingleUseAcquisition(spell, "slot0-instance-a"),
                    "A recorded instance must not be recorded twice.");

                // One consumption per use, blocked states never consume.
                CountingExecutor executor = new();
                slot.ConfigureExecutor(executor);
                executor.Allow = false;
                Assert(slot.TryUse() == SpellSlotUseResult.ConditionNotMet && slot.HasItem && executor.Executions == 0,
                    "A failed use condition must keep the item and skip its effect.");
                executor.Allow = true;
                Time.timeScale = 0f;
                Assert(slot.TryUse() == SpellSlotUseResult.Paused && slot.HasItem && executor.Executions == 0,
                    "Use must be blocked while paused.");
                Time.timeScale = 1f;
                Assert(slot.TryUse() == SpellSlotUseResult.Used && !slot.HasItem && executor.Executions == 1 &&
                       executor.LastDefinition == spell,
                    "A successful use must run the effect once and empty the slot.");
                Assert(slot.TryUse() == SpellSlotUseResult.Empty && executor.Executions == 1,
                    "A second press must not use anything.");
                Assert(inventory.AcquiredItems.Count == 2, "Using an item must not change the Run record.");

                // Re-entrant use from inside the effect cannot use a second item.
                SingleUseItemPickup third = FloorPickups(floor)[0];
                Assert(slot.TryCollect(third) && FloorPickups(floor).Length == 0, "The empty slot must take the floor item.");
                executor.ReenterSlot = slot;
                Assert(slot.TryUse() == SpellSlotUseResult.Used && executor.Executions == 2 &&
                       executor.ReentrantResult == SpellSlotUseResult.AlreadyUsing,
                    "A re-entrant use must not consume or execute again.");
                executor.ReenterSlot = null;

                // Run end and death block both use and collection.
                SingleUseItemPickup fourth = Spawn(prefab, floor, spell, "slot0-instance-c");
                Assert(slot.TryCollect(fourth), "The slot must accept a new instance.");
                progress.StopProgression();
                SingleUseItemPickup fifth = Spawn(prefab, floor, jjangsem, "slot0-instance-d");
                Assert(slot.TryUse() == SpellSlotUseResult.RunStopped && slot.HasItem &&
                       !slot.TryCollect(fifth) && !fifth.IsCollected && executor.Executions == 2,
                    "After the Run ends nothing may be used or collected.");
                progress.ResetProgress();
                Health health = player.GetComponent<Health>();
                health.TakeDamage(health.MaxHealth * 10f);
                Assert(health.IsDead, "Verification player must die.");
                Assert(slot.TryUse() == SpellSlotUseResult.PlayerDead && slot.HasItem &&
                       !slot.TryCollect(fifth) && executor.Executions == 2,
                    "A dead player cannot use or collect.");
                Assert(inventory.AcquiredItems.Count == 3,
                    "Blocked collections must not record anything.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(progressObject);
                Object.DestroyImmediate(spell);
                Object.DestroyImmediate(jjangsem);
            }
        }

        private static void ValidateHudRuntime()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameSpellSlotHudView configured = FindAll<GameSpellSlotHudView>(scene).Single();
            GameObject progressObject = new("Slot-0 HUD Progress", typeof(RunProgress));
            GameObject player = new("Slot-0 HUD Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory), typeof(PlayerSpellSlot));
            GameObject hudClone = Object.Instantiate(configured.gameObject);
            GameObject floor = new("Slot-0 HUD Floor");
            ItemDefinition jjangsem = CreateDefinition("jjangsem-slot0-hud", "검증용 짱셈스펠", ItemKind.JjangsemSpell);
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerInventory>(), "Awake");
                PlayerSpellSlot slot = player.GetComponent<PlayerSpellSlot>();
                slot.Configure(progressObject.GetComponent<RunProgress>(),
                    AssetDatabase.LoadAssetAtPath<SingleUseItemPickup>(Week21Slot0Setup.PickupPrefabPath));
                InvokeLifecycle(slot, "Awake");
                CountingExecutor executor = new() { Allow = false };
                slot.ConfigureExecutor(executor);

                GameSpellSlotHudView hud = hudClone.GetComponent<GameSpellSlotHudView>();
                hud.Configure(slot, hud.Icon, hud.NameText, hud.KindText, hud.KeyText);
                Assert(hud.NameText.text == GameSpellSlotHudView.EmptyText && hud.KindText.text == string.Empty &&
                       !hud.KeyText.gameObject.activeSelf,
                    "An empty slot must read 비어 있음 without a use key.");

                Assert(slot.TryCollect(Spawn(AssetDatabase.LoadAssetAtPath<SingleUseItemPickup>(
                        Week21Slot0Setup.PickupPrefabPath), floor, jjangsem, "slot0-hud")),
                    "HUD verification slot must collect.");
                hud.RefreshNow();
                Assert(hud.NameText.text == jjangsem.DisplayName && hud.KindText.text == "짱셈스펠" &&
                       hud.KeyText.gameObject.activeSelf && hud.KeyText.text == GameSpellSlotHudView.UnusableText &&
                       !hud.IsShowingUsable && hud.Icon.color == SingleUseItemPickup.JjangsemSpellColor,
                    "The HUD must show the held name, kind and that it cannot be used now.");
                executor.Allow = true;
                hud.RefreshNow();
                Assert(hud.KeyText.text == GameSpellSlotHudView.UsableText && hud.IsShowingUsable,
                    "The HUD must show the item is usable with Shift.");
                Time.timeScale = 0f;
                hud.RefreshNow();
                Assert(!hud.IsShowingUsable, "The HUD must show the item is not usable while paused.");
                Time.timeScale = 1f;
                slot.TryUse();
                hud.RefreshNow();
                Assert(hud.NameText.text == GameSpellSlotHudView.EmptyText, "The HUD must clear after a use.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                Object.DestroyImmediate(hudClone);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(progressObject);
                Object.DestroyImmediate(jjangsem);
            }
        }

        private static void ValidatePrefab()
        {
            SingleUseItemPickup prefab = AssetDatabase.LoadAssetAtPath<SingleUseItemPickup>(
                Week21Slot0Setup.PickupPrefabPath);
            Assert(prefab != null && prefab.Display != null && prefab.Definition == null &&
                   string.IsNullOrEmpty(prefab.InstanceId),
                "The pickup Prefab must be an empty template with a display renderer.");
            Collider2D collider = prefab.GetComponent<Collider2D>();
            Assert(collider != null && collider.isTrigger, "The pickup Prefab needs a trigger collider.");
        }

        private static void ValidateGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerMovement player = FindAll<PlayerMovement>(scene).Single();
            PlayerSpellSlot[] slots = FindAll<PlayerSpellSlot>(scene);
            Assert(slots.Length == 1 && slots[0].gameObject == player.gameObject,
                "The Game Scene must have exactly one spell slot, on the player.");
            PlayerSpellSlot slot = slots[0];
            RunProgress progress = FindAll<RunProgress>(scene).Single();
            Assert(slot.Progress == progress && slot.PickupPrefab != null &&
                   AssetDatabase.GetAssetPath(slot.PickupPrefab) == Week21Slot0Setup.PickupPrefabPath,
                "The player slot must reference the scene RunProgress and the pickup Prefab.");
            Assert(player.GetComponent<PlayerSingleUseEffects>() != null && player.GetComponent<PlayerInventory>() != null,
                "The player needs the default single-use executor and the inventory.");

            GameSpellSlotHudView[] huds = FindAll<GameSpellSlotHudView>(scene);
            Assert(huds.Length == 1 && huds[0].Slot == slot && huds[0].Icon != null && huds[0].NameText != null &&
                   huds[0].KindText != null && huds[0].KeyText != null,
                "The Game HUD must have exactly one spell slot view bound to the player slot.");
            Assert(huds[0].name == Week21Slot0Setup.HudPanelName &&
                   huds[0].transform.parent != null && huds[0].transform.parent.name == "ReferenceFrame",
                "The spell slot HUD must live in the game HUD reference frame.");
            TMP_FontAsset font = huds[0].NameText.font;
            Assert(font != null && font.HasCharacters(GameSpellSlotHudView.AllFixedText),
                "The HUD font must contain every fixed slot label glyph.");
            Assert(huds[0].GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget),
                "The spell slot HUD must not block input.");
        }

        private static SingleUseItemPickup Spawn(SingleUseItemPickup prefab, GameObject floor, ItemDefinition definition,
            string instanceId)
        {
            SingleUseItemPickup pickup = Object.Instantiate(prefab, floor.transform);
            pickup.Configure(definition, instanceId, false);
            return pickup;
        }

        private static SingleUseItemPickup[] FloorPickups(GameObject floor) => floor
            .GetComponentsInChildren<SingleUseItemPickup>(true).Where(pickup => !pickup.IsCollected).ToArray();

        private static ItemDefinition CreateDefinition(string itemId, string displayName, ItemKind kind,
            int maxStacks = 1)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = itemId;
            definition.ConfigureContract(itemId, displayName, kind, ItemRarity.Common, true, maxStacks,
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.01f));
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

        private sealed class CountingExecutor : ISingleUseItemExecutor
        {
            public bool Allow { get; set; }
            public int Executions { get; private set; }
            public ItemDefinition LastDefinition { get; private set; }
            public PlayerSpellSlot ReenterSlot { get; set; }
            public SpellSlotUseResult? ReentrantResult { get; private set; }

            public bool CanExecute(ItemDefinition definition, out string reason)
            {
                reason = Allow ? string.Empty : "verification condition not met";
                return Allow;
            }

            public void Execute(ItemDefinition definition)
            {
                Executions++;
                LastDefinition = definition;
                if (ReenterSlot != null) ReentrantResult = ReenterSlot.TryUse();
            }
        }
    }
}
