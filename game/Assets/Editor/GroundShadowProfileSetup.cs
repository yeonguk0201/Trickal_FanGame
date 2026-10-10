using System;
using System.Reflection;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class GroundShadowProfileSetup
    {
        private static readonly string[] Prefabs = { "ChargingEnemy", "HighBloodSugarFairy", "JyubiEnemy",
            "SansamoEnemy", "BuseureogiCrumbMinion", "CrayonArcherMinion", "CrayonAxeMinion",
            "CrayonMageMinion", "CrayonShieldMinion", "CrayonHeroBoss", "SaemaeumVaultBoss", "TestBoss",
            "BuseureogiDoughObstacle", "BuseureogiCreamObstacle" };

        [MenuItem("Trickal Fan Game/Artwork/Apply Reviewed Ground Shadow Profiles")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (string id in Prefabs)
            {
                string path = "Assets/Prefabs/" + id + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // Do not render or create procedural children in saved prefabs.
                    var shadow = root.GetComponent<GroundShadow>();
                    if (shadow == null) shadow = root.AddComponent<GroundShadow>();
                    shadow.SetProfileId(id);
                    EditorUtility.SetDirty(shadow);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        public static void ApplyAndVerifyBatch()
        {
            Apply();
            GroundShadowVerification.Verify();
            GroundShadowPreviewExporter.ExportBatch();
        }

        [MenuItem("Trickal Fan Game/Artwork/Verify Reviewed Ground Shadow Profiles")]
        public static void Verify()
        {
            VerifySpellProfiles();
            GameObject crumb = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/BuseureogiCrumbMinion.prefab"));
            GameObject chestObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/TreasureChest.prefab"));
            try
            {
                crumb.name = "Renamed spawned minion";
                var art = crumb.GetComponent<FairyKingdomArtworkView>();
                GroundShadow shadow = GroundShadow.Ensure(crumb);
                foreach (string color in new[] { "cream", "chocolate", "pink" })
                {
                    art.SetArtwork("crumb-minion-" + color);
                    AssertSettings(shadow, color == "pink" ? "BuseureogiCrumbMinion" : "crumb-" + color);
                }
                var manual = new SerializedObject(shadow);
                manual.FindProperty("useSharedProfile").boolValue = false;
                manual.FindProperty("opacity").floatValue = 0.12f;
                manual.ApplyModifiedPropertiesWithoutUndo();
                art.SetArtwork("crumb-minion-cream"); shadow.Refresh(); manual.Update();
                if (Mathf.Abs(manual.FindProperty("opacity").floatValue - 0.12f) > 0.00001f)
                    throw new InvalidOperationException("Manual shadow override was overwritten.");

                TreasureChest chest = chestObject.GetComponent<TreasureChest>();
                shadow = GroundShadow.Ensure(chestObject);
                foreach (ChestKind kind in Enum.GetValues(typeof(ChestKind)))
                {
                    // A bound gameplay chest cannot change kind. Reset only this isolated test fixture.
                    typeof(TreasureChest).GetField("record", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(chest, null);
                    chest.Configure("profile-probe", kind);
                    RoomRunState state = new("profile-probe-room");
                    var record = state.RegisterChest(chest.ChestId, kind);
                    typeof(TreasureChest).GetField("record", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(chest, record);
                    AssertSettings(shadow, "chest-" + kind + "-False");
                    state.TryOpenChest(chest.ChestId);
                    AssertSettings(shadow, "chest-" + kind + "-True");
                }
                foreach (string id in Prefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + id + ".prefab");
                    if (prefab.GetComponent<GroundShadow>()?.ProfileId != id)
                        throw new InvalidOperationException("Missing stable prefab shadow binding: " + id);
                    if (prefab.transform.Find(GroundShadow.ObjectName) != null)
                        throw new InvalidOperationException("Procedural shadow child was saved in prefab: " + id);
                }
                Debug.Log("Reviewed ground shadow profiles passed: stable prefab bindings, three live crumb colors, six live chest states and manual override.");
            }
            finally { UnityEngine.Object.DestroyImmediate(crumb); UnityEngine.Object.DestroyImmediate(chestObject); }
        }

        private static void VerifySpellProfiles()
        {
            GameObject regular = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemPickup.prefab"));
            GameObject singleUse = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SingleUseItemPickup.prefab"));
            int spells = 0;
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
                {
                    var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (definition.Kind == ItemKind.Artifact)
                    {
                        if (GroundShadowProfiles.ResolveItemId(definition) != definition.ItemId)
                            throw new InvalidOperationException("Artifact shadow was aliased to spell profile.");
                        continue;
                    }
                    GameObject owner = definition.IsSingleUse ? singleUse : regular;
                    if (definition.IsSingleUse) owner.GetComponent<SingleUseItemPickup>().Configure(definition, "shadow-spell-probe", false);
                    else owner.GetComponent<ItemPickup>().Configure(definition);
                    AssertSettings(GroundShadow.Ensure(owner), GroundShadowProfiles.SpellProfileId);
                    spells++;
                }
                if (spells == 0) throw new InvalidOperationException("No spell definitions checked.");
                Debug.Log($"Shared spell shadow verification passed: {spells} definitions, including reusable pickup reconfiguration; artifact IDs preserved.");
            }
            finally { UnityEngine.Object.DestroyImmediate(regular); UnityEngine.Object.DestroyImmediate(singleUse); }
        }

        public static void VerifyAndExportBatch()
        {
            Verify();
            GroundShadowPreviewExporter.ExportBatch();
        }

        private static void AssertSettings(GroundShadow shadow, string id)
        {
            shadow.Refresh();
            if (!GroundShadowProfiles.TryGet(id, out var expected)) throw new InvalidOperationException("Missing profile: " + id);
            var actual = new SerializedObject(shadow);
            if (Mathf.Abs(actual.FindProperty("widthMultiplier").floatValue - expected.widthMultiplier) > 0.00001f ||
                Mathf.Abs(actual.FindProperty("thickness").floatValue - expected.thickness) > 0.00001f ||
                Mathf.Abs(actual.FindProperty("opacity").floatValue - expected.opacity) > 0.00001f ||
                (actual.FindProperty("offset").vector2Value - new Vector2(expected.offsetX, expected.offsetY)).sqrMagnitude > 0.00000001f)
                throw new InvalidOperationException("Live state did not select its reviewed shadow: " + id);
        }
    }
}
