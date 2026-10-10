using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Review output only. Never saves changes to the gameplay scene, prefabs or item definitions.
    public static class GroundShadowPreviewExporter
    {
        [Serializable] public sealed class Entry
        {
            public int number;
            public string id, name, group, source;
            public float span, anchorX, anchorY, width, height;
            public float widthMultiplier, thickness, opacity, offsetX, offsetY;
        }
        [Serializable] private sealed class Manifest { public List<Entry> entries = new(); }
        private static Manifest manifest;
        private static string folder;
        private static Camera camera;
        private static GameObject floor;
        private static Scene review;

        public static void ExportBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run this exporter in batch mode.");
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/ground-shadow-review"));
            Directory.CreateDirectory(folder);
            manifest = new Manifest();
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerMovement player = Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).FirstOrDefault();
            if (player == null) throw new InvalidOperationException("Missing gameplay player.");
            foreach (Light2D existing in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                existing.enabled = false; // The isolated review uses one matching white/intensity-1 global light.
            review = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(review);
            try
            {
                camera = new GameObject("Shadow review camera").AddComponent<Camera>();
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.orthographic = true; camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                Light2D light = new GameObject("Gameplay global light").AddComponent<Light2D>();
                light.gameObject.layer = 30; light.lightType = Light2D.LightType.Global;
                light.color = Color.white; light.intensity = 1f;
                floor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Rooms/Prefabs/room-basic-tree-grove.prefab"));
                floor.transform.position = Vector3.zero;
                Layer(floor);
                foreach (MonoBehaviour behaviour in floor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (Collider2D collider in floor.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
                foreach (Renderer renderer in floor.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = renderer is SpriteRenderer && renderer.sortingOrder == -100;

                Capture(Object.Instantiate(player.gameObject), "player-erpin", "에르핀", "캐릭터", "SampleScene / Player");
                string[] enemies = { "ChargingEnemy", "RangedEnemy", "QuickRangedFairy", "HighBloodSugarFairy",
                    "JyubiEnemy", "SansamoEnemy", "BuseureogiCrumbMinion", "CrayonArcherMinion",
                    "CrayonAxeMinion", "CrayonMageMinion", "CrayonShieldMinion", "CrayonHeroBoss",
                    "SaemaeumVaultBoss", "TestBoss" };
                foreach (string name in enemies) Capture(Prefab(name), name, EnemyName(name), "적·보스", name + ".prefab");
                foreach (string color in new[] { "cream", "chocolate" })
                {
                    GameObject owner = Prefab("BuseureogiCrumbMinion");
                    owner.GetComponent<FairyKingdomArtworkView>().SetArtwork("crumb-minion-" + color);
                    // This mode selects the configured color rather than the prefab's default variant.
                    Capture(owner, "crumb-" + color, "부스러기 · " + color, "적·보스", "BuseureogiCrumbMinion.prefab");
                }
                GameObject grove = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Rooms/Prefabs/room-basic-tree-grove.prefab");
                DestructibleObstacle tree = grove.GetComponentsInChildren<DestructibleObstacle>(true)
                    .First(value => value.VariantId == "tree");
                Capture(Object.Instantiate(tree.gameObject), "tree", "나무 · 2칸", "장애물", "room-basic-tree-grove.prefab");
                Capture(Prefab("DestructibleObstacle"), "obstacle-rock", "돌", "장애물", "DestructibleObstacle.prefab");
                foreach (string guid in AssetDatabase.FindAssets("t:ObstacleVariantDefinition").OrderBy(value => value))
                {
                    var variant = AssetDatabase.LoadAssetAtPath<ObstacleVariantDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (FairyKingdomArtwork.Fit("obstacle-" + variant.VariantId, 1f) == null) continue;
                    GameObject owner = Prefab("DestructibleObstacle");
                    owner.GetComponent<DestructibleObstacle>().ApplyVariant(variant);
                    Capture(owner, "obstacle-" + variant.VariantId, variant.name, "장애물", AssetDatabase.GUIDToAssetPath(guid));
                }
                foreach (string name in new[] { "BuseureogiDoughObstacle", "BuseureogiCreamObstacle" })
                    Capture(Prefab(name), name, name == "BuseureogiDoughObstacle" ? "반죽 장애물" : "크림 장애물", "장애물", name + ".prefab");
                GameObject pillarRoom = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Rooms/Prefabs/room-large-central-pillar.prefab");
                var pillar = pillarRoom.GetComponentInChildren<RoomStaticObstacle>(true);
                if (pillar != null) Capture(Object.Instantiate(pillar.gameObject), "pillar", "중앙 기둥", "장애물", "room-large-central-pillar.prefab");
                foreach (ChestKind kind in Enum.GetValues(typeof(ChestKind)))
                foreach (bool open in new[] { false, true })
                {
                    GameObject owner = Prefab("TreasureChest");
                    TreasureChest chest = owner.GetComponent<TreasureChest>();
                    chest.Configure("shadow-review", kind);
                    RoomRunState state = new("shadow-review-room");
                    var record = state.RegisterChest(chest.ChestId, kind);
                    typeof(TreasureChest).GetField("record", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(chest, record);
                    if (open) state.TryOpenChest(chest.ChestId);
                    Capture(owner, "chest-" + kind + "-" + open, kind + (open ? " 상자 · 열림" : " 상자 · 닫힘"), "상자", "TreasureChest.prefab");
                }
                foreach (string name in new[] { "ElifPickup", "KeyPickup", "BombPickup", "HealthPickup", "SPPickup", "PlacedBomb" })
                {
                    GameObject owner = Prefab(name);
                    if (name == "HealthPickup" || name == "SPPickup")
                        UserArtwork.ApplyResourcePickup(owner.GetComponentInChildren<SpriteRenderer>(), name == "HealthPickup" ? "hp-pickup" : "sp-pickup");
                    Capture(owner, name, name, "픽업·폭탄", name + ".prefab");
                }
                foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition").OrderBy(value => value))
                {
                    var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (UserArtwork.Load(item.ItemId) == null) continue;
                    GameObject owner = Prefab(item.IsSingleUse ? "SingleUseItemPickup" : "ItemPickup");
                    if (item.IsSingleUse) owner.GetComponent<SingleUseItemPickup>().Configure(item, "shadow-review", false);
                    else owner.GetComponent<ItemPickup>().Configure(item);
                    Capture(owner, item.ItemId, item.DisplayName, "아이템", item.ItemId);
                }
                GameObject shop = Prefab("ShopRoom");
                ShopKeeper keeper = shop.GetComponentInChildren<ShopKeeper>(true);
                if (keeper == null) throw new InvalidOperationException("Missing shopkeeper.");
                foreach (string key in new[] { "sist", "goldi" })
                {
                    GameObject owner = Object.Instantiate(keeper.gameObject);
                    SpriteRenderer portrait = owner.GetComponent<ShopKeeper>().Portrait;
                    UserArtwork.ApplyPickup(portrait, key);
                    portrait.transform.localScale = Vector3.one * (ShopKeeper.PortraitWorldSize /
                        Mathf.Max(portrait.sprite.bounds.size.x, portrait.sprite.bounds.size.y));
                    foreach (Renderer extra in owner.GetComponentsInChildren<Renderer>(true)) if (extra != portrait) extra.enabled = false;
                    Capture(owner, "npc-" + key, key == "sist" ? "시스트" : "골디", "상점 NPC", "ShopRoom / Keeper");
                }
                Object.DestroyImmediate(shop);
                File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonUtility.ToJson(manifest, true),
                    new System.Text.UTF8Encoding(false));
                Debug.Log($"Ground shadow review exported: {manifest.entries.Count} actual gameplay appearances, " +
                    "Unity composite/body/shadow/floor layers; gameplay assets unchanged.");
            }
            finally { EditorSceneManager.CloseScene(review, true); }
        }

        private static GameObject Prefab(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + name);
            return Object.Instantiate(prefab);
        }
        private static void Layer(GameObject owner)
        {
            foreach (Transform node in owner.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 30;
        }
        private static string EnemyName(string name) => name switch
        {
            "ChargingEnemy" => "돌진 요정", "RangedEnemy" => "원거리 요정", "QuickRangedFairy" => "저혈당 요정",
            "HighBloodSugarFairy" => "고혈당 요정", "JyubiEnemy" => "쥬비", "SansamoEnemy" => "산사모",
            "BuseureogiCrumbMinion" => "부스러기 · 분홍", "CrayonHeroBoss" => "크레용사용",
            "SaemaeumVaultBoss" => "새마음금고", _ => name,
        };
        private static void Capture(GameObject owner, string id, string name, string group, string sourcePath)
        {
            try
            {
                owner.transform.SetParent(null); owner.transform.position = Vector3.zero; owner.SetActive(true); Layer(owner);
                foreach (MonoBehaviour behaviour in owner.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var art = owner.GetComponent<FairyKingdomArtworkView>();
                if (art != null) art.Refresh();
                GroundShadow shadow = GroundShadow.Ensure(owner); shadow.enabled = true;
                Physics2D.SyncTransforms(); shadow.Refresh();
                if (shadow.Visual == null || !shadow.Visual.enabled) throw new InvalidOperationException("Missing shadow: " + id);
                shadow.Visual.gameObject.layer = 30;
                Renderer[] bodies = owner.GetComponentsInChildren<Renderer>(true).Where(value => value != shadow.Visual && value.enabled).ToArray();
                Bounds bounds = bodies[0].bounds;
                foreach (Renderer body in bodies) bounds.Encapsulate(body.bounds);
                bounds.Encapsulate(shadow.Visual.bounds);
                float span = Mathf.Max(3.4f, Mathf.Max(bounds.size.x, bounds.size.y) + 0.9f);
                camera.orthographicSize = span * 0.5f;
                camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
                int number = manifest.entries.Count + 1;
                string prefix = number.ToString("D3");
                floor.SetActive(true); Render(prefix + "-game.png");
                floor.SetActive(false); shadow.Visual.enabled = false; Render(prefix + "-body.png");
                foreach (Renderer body in bodies) body.enabled = false;
                shadow.Visual.enabled = true; Render(prefix + "-shadow.png");
                foreach (Renderer body in bodies) body.enabled = true;
                owner.SetActive(false); floor.SetActive(true); Render(prefix + "-floor.png");
                Vector3 anchor = camera.WorldToViewportPoint(shadow.Visual.transform.position);
                SerializedObject settings = new(shadow);
                if (GroundShadowProfiles.TryGet(GroundShadowProfiles.ResolveId(owner, shadow.ProfileId), out var expected))
                {
                    foreach (string property in new[] { "widthMultiplier", "thickness", "opacity" })
                    {
                        float target = property == "widthMultiplier" ? expected.widthMultiplier :
                            property == "thickness" ? expected.thickness : expected.opacity;
                        if (Mathf.Abs(settings.FindProperty(property).floatValue - target) > 0.00001f)
                            throw new InvalidOperationException("Reviewed shadow setting mismatch: " + id + "/" + property);
                    }
                    if ((settings.FindProperty("offset").vector2Value - new Vector2(expected.offsetX, expected.offsetY)).sqrMagnitude > 0.00000001f)
                        throw new InvalidOperationException("Reviewed shadow offset mismatch: " + id);
                }
                manifest.entries.Add(new Entry
                {
                    number = number, id = id, name = name, group = group, source = sourcePath, span = span,
                    anchorX = anchor.x, anchorY = 1f - anchor.y,
                    width = shadow.Visual.bounds.size.x, height = shadow.Visual.bounds.size.y,
                    widthMultiplier = settings.FindProperty("widthMultiplier").floatValue,
                    thickness = settings.FindProperty("thickness").floatValue,
                    opacity = settings.FindProperty("opacity").floatValue,
                    offsetX = settings.FindProperty("offset").vector2Value.x,
                    offsetY = settings.FindProperty("offset").vector2Value.y,
                });
            }
            finally { Object.DestroyImmediate(owner); }
        }
        private static void Render(string filename)
        {
            const int size = 480;
            RenderTexture target = new(size, size, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new(size, size, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, size, size), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(folder, filename), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
