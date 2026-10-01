using System;
using System.Collections.Generic;
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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week16Reward2Verification
    {
        [MenuItem("Trickal Fan Game/Week 16/Verify Reward-2 Placeholder Cards")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ValidateSceneContract(scene);
            ValidateContentFits(scene);
            ValidateMouseKeyboardSelectionAndLiveHud(scene);
            Debug.Log("Week 16 Reward-2 verification passed: the replaceable 1920x1080 placeholder UI has " +
                      "three cards, complete Item/healing information, explicit horizontal navigation, confined " +
                      "focus, explicit confirm/cancel actions, candidate-preserving reopen, non-color state labels, " +
                      "long-text fit, and immediate " +
                      "inventory HUD/stat refresh after selection.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week16Reward1Verification.SetupAndVerifyBatch();
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week16Reward2Setup.Setup();
            Week16Reward2Setup.Setup();
            Assert(AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath) == sceneGuid,
                "Reward-2 setup must preserve the Game Scene GUID.");
            Verify();
        }

        private static void ValidateSceneContract(Scene scene)
        {
            ItemRewardSelectionView view = FindSingle<ItemRewardSelectionView>(scene, "Reward-2 view");
            ItemRewardSelectionSession session = FindSingle<ItemRewardSelectionSession>(scene, "Reward-2 session");
            EventSystem eventSystem = FindSingle<EventSystem>(scene, "EventSystem");
            Assert(view.Session == session && view.Cards.Length == ArtifactRewardSelector.MaximumCandidateCount &&
                   view.Cards.All(card => card != null && card.Button != null) &&
                   view.ConfirmButton != null && view.CancelButton != null,
                "Reward-2 must connect one session to three cards plus explicit confirm and cancel buttons.");
            Assert(view.GetComponents<ItemRewardSelectionView>().Length == 1 &&
                   view.GetComponents<ItemRewardSelectionSession>().Length == 1 &&
                   FindAll<ItemRewardSelectionView>(scene).Length == 1,
                "Repeated setup must not duplicate the selection view or session.");
            Assert(view.Overlay != null && !view.IsVisible && Mathf.Approximately(view.Overlay.alpha, 0f) &&
                   !view.Overlay.interactable && !view.Overlay.blocksRaycasts,
                "The configured overlay must start hidden and non-interactive.");
            RectTransform overlayRect = view.transform as RectTransform;
            Assert(overlayRect != null && Approximately(overlayRect.sizeDelta, Week16Reward2Setup.ReferenceResolution) &&
                   view.FocusScope != null && Approximately(view.FocusScope.sizeDelta, Week16Reward2Setup.PanelSize),
                "Reward-2 must use the 1920x1080 reference frame and the fixed placeholder panel size.");
            Assert(view.Cards.All(card => Approximately(card.GetComponent<RectTransform>().sizeDelta,
                       Week16Reward2Setup.CardSize)),
                "Every placeholder reward card must use the documented replaceable card size.");
            Assert(eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null &&
                   view.GraphicRaycaster != null && view.UiEventSystem == eventSystem,
                "Reward-2 requires the shared Input System EventSystem and Game HUD raycaster.");
        }

        private static void ValidateContentFits(Scene scene)
        {
            ItemRewardSelectionView view = FindSingle<ItemRewardSelectionView>(scene, "Reward-2 view");
            Health playerHealth = FindSingle<PlayerInventory>(scene, "player inventory").GetComponent<Health>();
            ItemDefinition[] definitions = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<ItemDefinition>(path))
                .Where(definition => definition != null && definition.IsActive && !definition.IsSingleUse)
                .ToArray();
            Assert(definitions.Length >= 6, "Reward-2 fit verification requires the configured active Item pool.");
            ItemRewardCardView card = view.Cards[0];
            foreach (ItemDefinition definition in definitions)
            {
                card.Bind(ItemRewardCandidate.ForItem(definition, Math.Max(0, definition.MaxStacks - 1)), playerHealth);
                Assert(card.KindText.text == (definition.Kind == ItemKind.Spell ? "스펠" : "아티팩트") &&
                       card.NameText.text == definition.DisplayName &&
                       card.RarityText.text.Contains(definition.Rarity.ToString().ToUpperInvariant(),
                           StringComparison.Ordinal) &&
                       !string.IsNullOrWhiteSpace(card.EffectText.text) &&
                       card.StackText.text.Contains("현재 스택", StringComparison.Ordinal),
                    $"Reward card fields are incomplete for {definition.ItemId}.");
                AssertFits(card.NameText, definition.ItemId + " name");
                AssertFits(card.EffectText, definition.ItemId + " effect");
                AssertFits(card.StackText, definition.ItemId + " stack");
            }

            card.Bind(ItemRewardCandidate.ForHealing(ArtifactRewardSelector.FallbackHealMaxHealthRatio), playerHealth);
            Assert(card.KindText.text == "회복" && card.NameText.text == "응급 회복" &&
                   card.EffectText.text.Contains("25%", StringComparison.Ordinal) &&
                   card.StackText.text.Contains("현재 HP", StringComparison.Ordinal) &&
                   card.StackText.text.Contains("최대 HP", StringComparison.Ordinal),
                "Healing cards must show the recovery ratio and current/maximum HP.");
            AssertFits(card.EffectText, "healing effect");
            AssertFits(card.StackText, "healing health values");
        }

        private static void ValidateMouseKeyboardSelectionAndLiveHud(Scene scene)
        {
            ItemRewardSelectionView view = FindSingle<ItemRewardSelectionView>(scene, "Reward-2 view");
            ItemRewardSelectionSession session = view.Session;
            EventSystem eventSystem = FindSingle<EventSystem>(scene, "EventSystem");
            EventSystem.current = eventSystem;
            GameObject player = CreatePlayer(out Health health, out PlayerStats stats,
                out PlayerInventory inventory, out PlayerActionState actionState);
            GameObject progressObject = new("Reward-2 Verification Progress", typeof(RunProgress));
            RunProgress progress = progressObject.GetComponent<RunProgress>();
            GameObject hudObject = CreateHud(inventory, out GameArtifactHudView hud);
            GameObject outsideFocus = new("Reward-2 Outside Focus", typeof(RectTransform), typeof(Button));
            List<ItemDefinition> definitions = CreateDefinitions("keyboard", 3);
            List<ItemDefinition> mouseDefinitions = CreateDefinitions("mouse", 3);
            try
            {
                Assert(progress.TryInitializeRunSeed(1603, out string seedError), seedError);
                session.Configure(progress, inventory, health, actionState);
                view.Configure(session, health, view.Overlay, view.FocusScope, view.GraphicRaycaster,
                    eventSystem, view.TitleText, view.InputHintText, view.Cards,
                    view.ConfirmButton, view.CancelButton);
                foreach (ItemRewardCardView card in view.Cards) InvokeLifecycle(card, "Awake");
                Assert(session.TryOpen(definitions, "reward-2:keyboard", out string openError), openError);
                Assert(view.IsVisible && Mathf.Approximately(view.Overlay.alpha, 1f) &&
                       view.Overlay.interactable && view.Overlay.blocksRaycasts &&
                       eventSystem.currentSelectedGameObject == view.Cards[0].gameObject &&
                       view.Cards[0].State == ItemRewardCardState.Focused,
                    "Opening Reward-2 must show the overlay and focus the first card with a text marker. " +
                    $"visible={view.IsVisible}, alpha={view.Overlay.alpha}, " +
                    $"interactable={view.Overlay.interactable}, raycasts={view.Overlay.blocksRaycasts}, " +
                    $"selected={eventSystem.currentSelectedGameObject?.name ?? "null"}, " +
                    $"firstState={view.Cards[0].State}.");
                for (int index = 0; index < view.Cards.Length; index++)
                {
                    Button current = view.Cards[index].Button;
                    Assert(current.navigation.mode == Navigation.Mode.Explicit &&
                           current.navigation.selectOnRight == view.Cards[(index + 1) % view.Cards.Length].Button &&
                           current.navigation.selectOnLeft ==
                           view.Cards[(index + view.Cards.Length - 1) % view.Cards.Length].Button &&
                           current.navigation.selectOnDown == view.ConfirmButton,
                        "Three reward cards must use circular explicit left/right navigation.");
                }

                eventSystem.SetSelectedGameObject(outsideFocus);
                view.EnsureFocusNow();
                Assert(eventSystem.currentSelectedGameObject == view.Cards[0].gameObject,
                    "Reward selection focus must remain inside the card scope.");

                AxisEventData moveRight = new(eventSystem)
                {
                    moveDir = MoveDirection.Right,
                    moveVector = Vector2.right,
                };
                ExecuteEvents.Execute(view.Cards[0].gameObject, moveRight, ExecuteEvents.moveHandler);
                Assert(eventSystem.currentSelectedGameObject == view.Cards[1].gameObject &&
                       view.Cards[1].State == ItemRewardCardState.Focused,
                    "Right navigation must move focus to the next card and update its non-color marker.");
                ItemRewardCandidate keyboardChoice = view.Cards[1].Candidate;
                float attackBefore = stats.AttackDamage;
                ExecuteEvents.Execute(view.Cards[1].gameObject, new BaseEventData(eventSystem),
                    ExecuteEvents.submitHandler);
                Assert(!session.State.IsCompleted &&
                       eventSystem.currentSelectedGameObject == view.ConfirmButton.gameObject,
                    "Activating a card must stage the candidate and move focus to the confirm button.");
                ExecuteEvents.Execute(view.ConfirmButton.gameObject, new BaseEventData(eventSystem),
                    ExecuteEvents.submitHandler);
                Assert(session.State.IsCompleted && session.State.SelectedCandidateId == keyboardChoice.StableId &&
                       view.Cards[1].State == ItemRewardCardState.Acquired &&
                       view.Cards.Where((_, index) => index != 1)
                           .All(card => card.State == ItemRewardCardState.Unavailable) &&
                       view.Cards[1].StateText.text == "[획득 완료]" &&
                       view.Cards[0].StateText.text == "[선택 종료]",
                    "Keyboard confirmation must distinguish the acquired and discarded cards without color alone. " +
                    $"completed={session.State.IsCompleted}, selected={session.State.SelectedCandidateId ?? "null"}, " +
                    $"expected={keyboardChoice.StableId}, states={string.Join(",", view.Cards.Select(card => card.State))}, " +
                    $"labels={string.Join(",", view.Cards.Select(card => card.StateText.text))}.");
                Assert(hud.VisibleSlotCount == 1 && hud.VisibleSlots[0].Definition == keyboardChoice.Definition &&
                       stats.AttackDamage > attackBefore,
                    "Reward confirmation must refresh the Item HUD and affected player stat immediately.");

                progress.ResetProgress();
                Assert(session.TryOpen(mouseDefinitions, "reward-2:mouse", out string mouseError), mouseError);
                string cancelledSignature = string.Join("|", session.Candidates.Select(candidate => candidate.StableId));
                PointerEventData pointer = new(eventSystem) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(view.CancelButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert(!session.IsOpen && !progress.IsRewardSelectionPending && !view.IsVisible &&
                       session.State != null && !session.State.IsCompleted,
                    "Cancel must close the overlay without completing or discarding the reward.");
                Assert(session.TryOpen(mouseDefinitions, "reward-2:mouse", out mouseError) &&
                       string.Join("|", session.Candidates.Select(candidate => candidate.StableId)) ==
                       cancelledSignature,
                    "A cancelled reward must reopen with the same candidates.");
                ItemRewardCandidate mouseChoice = view.Cards[2].Candidate;
                ExecuteEvents.Execute(view.Cards[2].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(view.ConfirmButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert(session.State.IsCompleted && session.State.SelectedCandidateId == mouseChoice.StableId &&
                       inventory.GetStackCount(mouseChoice.StableId) == 1 && hud.VisibleSlotCount == 2,
                    "Mouse click must use the same single-selection path and update the HUD.");
                view.HideNow();
                Assert(!view.IsVisible && !view.Overlay.blocksRaycasts,
                    "The completion display must be dismissible without leaving an input blocker.");
            }
            finally
            {
                view.HideNow();
                foreach (ItemDefinition definition in definitions) Object.DestroyImmediate(definition);
                foreach (ItemDefinition definition in mouseDefinitions) Object.DestroyImmediate(definition);
                Object.DestroyImmediate(outsideFocus);
                Object.DestroyImmediate(hudObject);
                Object.DestroyImmediate(progressObject);
                Object.DestroyImmediate(player);
            }
        }

        private static GameObject CreatePlayer(out Health health, out PlayerStats stats,
            out PlayerInventory inventory, out PlayerActionState actionState)
        {
            GameObject player = new("Reward-2 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory), typeof(PlayerActionState));
            health = player.GetComponent<Health>();
            stats = player.GetComponent<PlayerStats>();
            inventory = player.GetComponent<PlayerInventory>();
            actionState = player.GetComponent<PlayerActionState>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
            InvokeLifecycle(stats, "Awake");
            InvokeLifecycle(inventory, "Awake");
            return player;
        }

        private static GameObject CreateHud(PlayerInventory inventory, out GameArtifactHudView hud)
        {
            GameObject root = new("Reward-2 Verification HUD", typeof(RectTransform));
            RectTransform slots = new GameObject("Slots", typeof(RectTransform)).GetComponent<RectTransform>();
            slots.SetParent(root.transform, false);
            GameObject templateObject = new("Template", typeof(RectTransform), typeof(ArtifactHudSlotView));
            templateObject.transform.SetParent(slots, false);
            ArtifactHudSlotView template = templateObject.GetComponent<ArtifactHudSlotView>();
            template.ConfigureVisuals(null, null, null, null);
            templateObject.SetActive(false);
            GameObject overflowObject = new("Overflow", typeof(RectTransform), typeof(TextMeshProUGUI));
            overflowObject.transform.SetParent(root.transform, false);
            hud = root.AddComponent<GameArtifactHudView>();
            hud.Configure(inventory, slots, template, overflowObject.GetComponent<TMP_Text>(), 10);
            return root;
        }

        private static List<ItemDefinition> CreateDefinitions(string suffix, int count)
        {
            List<ItemDefinition> definitions = new();
            for (int index = 0; index < count; index++)
            {
                string prefix = index % 2 == 0 ? "artifact" : "spell";
                ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.name = $"{prefix}-reward2-{suffix}-{index}";
                definition.ConfigureContract(definition.name,
                    index == 0 ? "아주 긴 임시 보상 이름도 두 줄 안에서 안전하게 표시" : $"임시 보상 {index + 1}",
                    prefix == "spell" ? ItemKind.Spell : ItemKind.Artifact,
                    (ItemRarity)(index % 4), true, 2,
                    new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.1f),
                    new ItemEffectEntry(ItemEffectType.MoveSpeedPercent, 0.05f));
                definitions.Add(definition);
            }
            return definitions;
        }

        private static void AssertFits(TMP_Text text, string label)
        {
            text.ForceMeshUpdate(true, true);
            float preferredHeight = text.GetPreferredValues(text.text, text.rectTransform.rect.width, 0f).y;
            Assert(preferredHeight <= text.rectTransform.rect.height + 0.5f,
                $"Reward-2 {label} text needs {preferredHeight:0.#} px but only " +
                $"{text.rectTransform.rect.height:0.#} px is available at 1920x1080.");
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = FindAll<T>(scene);
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static bool Approximately(Vector2 left, Vector2 right) =>
            Mathf.Approximately(left.x, right.x) && Mathf.Approximately(left.y, right.y);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
