using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week16Reward3Setup
    {
        [MenuItem("Trickal Fan Game/Week 16/Setup Reward-3 Room Integration")]
        public static void Setup()
        {
            Week16Reward2Setup.Setup();
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = FindSingle<RoomGraphAssembler>(scene, "RoomGraphAssembler");
            ItemRewardSelectionSession session = FindSingle<ItemRewardSelectionSession>(scene,
                "ItemRewardSelectionSession");
            ItemDefinition[] pool = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                // Single-use spells, retired legacy spells and golden chest exclusive artifacts never join the
                // selection reward pool (Contract-0 §2.1, §2.2, §3).
                .Where(definition => definition != null && definition.IsActive && definition.IsValid &&
                                     !definition.IsSingleUse && !LegacySpellRetirement.IsRetired(definition) &&
                                     !GoldenChestExclusivePool.IsExclusive(definition))
                .OrderBy(definition => definition.ItemId, StringComparer.Ordinal)
                .ToArray();

            if (pool.Length < ArtifactRewardSelector.MaximumCandidateCount ||
                pool.Select(definition => definition.ItemId).Distinct(StringComparer.Ordinal).Count() != pool.Length ||
                !pool.Any(definition => definition.Kind == ItemKind.Artifact) ||
                pool.Any(definition => definition.Kind == ItemKind.Spell))
            {
                // Spell-3 retired the last legacy spell, so the pool now holds Artifacts only (Contract-0 §3).
                throw new InvalidOperationException(
                    "Reward-3 requires at least three unique active Artifacts and no legacy Spell.");
            }

            Undo.RecordObject(assembler, "Configure Reward-3 Room Integration");
            assembler.ConfigureSelectionRewards(session, pool);
            ConfigureTreasureInteractionPrefabs();
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Reward-3 setup.");
            AssetDatabase.SaveAssets();
            Debug.Log($"Week 16 Reward-3 setup complete: {pool.Length} unified Item definitions are bound to " +
                      "treasure rooms and floor 1/2 boss rewards.");
        }

        private static void ConfigureTreasureInteractionPrefabs()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            GameObject pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemPickup.prefab");
            Sprite markerSprite = pickupPrefab != null ? pickupPrefab.GetComponent<SpriteRenderer>()?.sprite : null;
            if (font == null || markerSprite == null)
                throw new InvalidOperationException("Reward-3 interaction visuals require the HUD font and pickup sprite.");

            string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Rooms/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    RewardRoom reward = root.GetComponentInChildren<RewardRoom>(true);
                    if (reward == null) continue;
                    Transform markerTransform = reward.transform.Find("Interaction Marker");
                    if (markerTransform == null)
                    {
                        markerTransform = new GameObject("Interaction Marker", typeof(SpriteRenderer)).transform;
                        markerTransform.SetParent(reward.transform, false);
                    }
                    SpriteRenderer marker = markerTransform.GetComponent<SpriteRenderer>();
                    marker.sprite = markerSprite;
                    marker.color = new Color(0.28f, 0.92f, 1f, 0.9f);
                    marker.sortingOrder = 3;
                    markerTransform.localPosition = Vector3.zero;
                    markerTransform.localScale = Vector3.one * 0.9f;

                    Transform promptTransform = reward.transform.Find("Interaction Prompt");
                    if (promptTransform == null)
                    {
                        promptTransform = new GameObject("Interaction Prompt", typeof(RectTransform),
                            typeof(TextMeshPro)).transform;
                        promptTransform.SetParent(reward.transform, false);
                    }
                    TextMeshPro prompt = promptTransform.GetComponent<TextMeshPro>();
                    prompt.font = font;
                    prompt.text = "[E] 보상 선택";
                    prompt.fontSize = 3.5f;
                    prompt.alignment = TextAlignmentOptions.Center;
                    prompt.color = new Color(0.9f, 0.98f, 1f, 1f);
                    prompt.sortingOrder = 4;
                    RectTransform promptRect = (RectTransform)promptTransform;
                    promptRect.localPosition = new Vector3(0f, 1.1f, 0f);
                    promptRect.sizeDelta = new Vector2(5f, 1f);
                    promptRect.localScale = Vector3.one;
                    reward.ConfigureInteractionVisuals(marker.gameObject, prompt.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }
    }
}
