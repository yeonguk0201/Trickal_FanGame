using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Boss-Pattern-0 review output only (2026-11-19): renders each boss's own boss room, the boss's real drawn poses
    // and ground shadow, and the player, with the numbers a browser tuner needs to place them exactly where the feet
    // rule (EnemyFeet.StandingRootArea) would stop them. Never saves scenes, Prefabs or other assets.
    public static class BossFeetLayoutExporter
    {
        public const float PixelsPerUnit = 48f;
        private const float ActorCanvas = 8f;
        private const int ForegroundSortingOrder = 20;

        [Serializable] public sealed class Pose { public string name, file; }

        [Serializable] public sealed class BossEntry
        {
            public string id, name, roomFile, roomForegroundFile, shadowFile;
            public float roomWidth, roomHeight, standXMin, standYMin, standXMax, standYMax;
            public float bodyRadius, feetRadius, feetHalfWidth, feetHalfHeight, feetOffsetX, feetDrop, shadowOffsetY;
            public List<Pose> poses = new();
        }

        [Serializable] public sealed class Manifest
        {
            public int version = 1;
            public float pixelsPerUnit = PixelsPerUnit, actorCanvas = ActorCanvas;
            public string playerFile, playerShadowFile;
            public float playerFeetOffsetY, playerFeetRadius;
            public List<BossEntry> bosses = new();
        }

        private static readonly (string Id, string Name, string Boss, string Room, string Profile)[] Bosses =
        {
            ("buseureogi", "부스러기", Week15Boss0Setup.BossPrefabPath, "Assets/Rooms/Prefabs/room-boss-floor-1-wide.prefab",
                Week14Room6Setup.BossFloor1ProfilePath),
            ("saemaeum", "새마음금고", Week15Boss2Setup.BossPrefabPath, "Assets/Rooms/Prefabs/room-boss-floor-2-tall.prefab",
                Week14Room6Setup.BossFloor2ProfilePath),
            ("crayon", "크레용 용사", Week15Boss3Setup.BossPrefabPath, "Assets/Rooms/Prefabs/room-boss-floor-3-large.prefab",
                Week14Room6Setup.BossFloor3ProfilePath),
        };

        private static Camera camera;
        private static string folder;

        public static void ExportBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run this exporter in batch mode.");
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/boss-feet-review"));
            Directory.CreateDirectory(folder);
            var manifest = new Manifest();
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerMovement scenePlayer = Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).FirstOrDefault();
            if (scenePlayer == null) throw new InvalidOperationException("Missing gameplay player.");
            foreach (Light2D existing in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                existing.enabled = false;
            Scene review = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(review);
            try
            {
                camera = new GameObject("Boss feet review camera").AddComponent<Camera>();
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.orthographic = true;
                camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                Light2D light = new GameObject("Review global light").AddComponent<Light2D>();
                light.gameObject.layer = 30;
                light.lightType = Light2D.LightType.Global;
                light.color = Color.white;
                light.intensity = 1f;

                GameObject player = Object.Instantiate(scenePlayer.gameObject);
                Prepare(player);
                PlayerFeet feet = player.GetComponentInChildren<PlayerFeet>(true);
                manifest.playerFeetRadius = feet != null ? feet.Collider.radius *
                    Mathf.Abs(feet.transform.lossyScale.y) : PlayerFeet.Radius;
                manifest.playerFeetOffsetY = feet != null ? feet.WorldCenter.y - player.transform.position.y
                    : PlayerFeet.LocalPosition.y;
                RenderActor(player, "player.png", "player-shadow.png");
                manifest.playerFile = "player.png";
                manifest.playerShadowFile = "player-shadow.png";
                Object.DestroyImmediate(player);

                foreach (var boss in Bosses) manifest.bosses.Add(ExportBoss(boss));

                File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonUtility.ToJson(manifest, true),
                    new System.Text.UTF8Encoding(false));
                Debug.Log($"Boss feet review exported to {folder}: {manifest.bosses.Count} bosses with their rooms.");
            }
            finally
            {
                EditorSceneManager.CloseScene(review, true);
            }
        }

        private static BossEntry ExportBoss((string Id, string Name, string Boss, string Room, string Profile) spec)
        {
            RoomProfile profile = AssetDatabase.LoadAssetAtPath<RoomProfile>(spec.Profile);
            if (profile == null) throw new InvalidOperationException("Missing room profile: " + spec.Profile);
            var entry = new BossEntry
            {
                id = spec.Id, name = spec.Name, roomWidth = profile.InteriorSize.x + 1f,
                roomHeight = profile.InteriorSize.y + 1f,
                standXMin = profile.MovementBounds.xMin, standYMin = profile.MovementBounds.yMin,
                standXMax = profile.MovementBounds.xMax, standYMax = profile.MovementBounds.yMax,
            };

            GameObject room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(spec.Room));
            try
            {
                room.transform.position = Vector3.zero;
                room.GetComponent<RoomNode>()?.SetVisible(true);
                Layer(room);
                foreach (MonoBehaviour behaviour in room.GetComponentsInChildren<MonoBehaviour>(true))
                    behaviour.enabled = false;
                // Wall and door blockers carry tinted placeholder squares that the game hides; only artwork is drawn.
                Renderer[] renderers = room.GetComponentsInChildren<Renderer>(false)
                    .Where(renderer => !IsPlaceholder(renderer)).ToArray();
                foreach (Renderer hidden in room.GetComponentsInChildren<Renderer>(false).Where(IsPlaceholder))
                    hidden.enabled = false;
                camera.orthographicSize = entry.roomHeight * 0.5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                int width = Mathf.RoundToInt(entry.roomWidth * PixelsPerUnit);
                int height = Mathf.RoundToInt(entry.roomHeight * PixelsPerUnit);
                foreach (Renderer renderer in renderers) renderer.enabled = renderer.sortingOrder < ForegroundSortingOrder;
                entry.roomFile = spec.Id + "-room.png";
                Render(entry.roomFile, width, height);
                foreach (Renderer renderer in renderers) renderer.enabled = renderer.sortingOrder >= ForegroundSortingOrder;
                entry.roomForegroundFile = spec.Id + "-room-front.png";
                Render(entry.roomForegroundFile, width, height);
            }
            finally
            {
                Object.DestroyImmediate(room);
            }

            GameObject owner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(spec.Boss));
            try
            {
                Prepare(owner);
                BossController controller = owner.GetComponent<BossController>();
                EnemyFeet.TryGetBody(owner, out CircleCollider2D body);
                entry.bodyRadius = EnemyFeet.WorldRadiusOf(body);
                entry.feetRadius = controller.FeetHalfHeight;
                entry.feetHalfWidth = controller.FeetHalfWidth;
                entry.feetHalfHeight = controller.FeetHalfHeight;
                entry.feetOffsetX = controller.FeetOffsetX;
                entry.feetDrop = controller.FeetDrop;
                BossMovementAnimator animator = owner.GetComponent<BossMovementAnimator>();
                animator.RenderPose(0f, false);
                GroundShadow shadow = GroundShadow.Ensure(owner);
                shadow.enabled = true;
                Physics2D.SyncTransforms();
                shadow.Refresh();
                entry.shadowOffsetY = shadow.Visual != null ? shadow.Visual.transform.position.y : -entry.bodyRadius;
                entry.shadowFile = spec.Id + "-shadow.png";
                RenderShadowOnly(owner, shadow, entry.shadowFile);
                entry.poses.Add(new Pose { name = "서 있기", file = RenderBody(owner, shadow, spec.Id + "-idle.png") });
                // Walking frames that stand on the floor (the hop frames that lift off are left out).
                int frames = animator.WalkFrameCount > 0 ? animator.WalkFrameCount : 0;
                for (int index = 0; index < frames; index++)
                {
                    animator.RenderPose((index + 0.5f) / frames, true);
                    entry.poses.Add(new Pose { name = $"걷기 {index + 1}",
                        file = RenderBody(owner, shadow, $"{spec.Id}-walk-{index}.png") });
                }
                for (int index = 0; index < animator.HopFrameCount; index++)
                {
                    if (index == 2) continue; // airborne
                    Sprite frame = animator.GetHopFrame(index);
                    animator.Artwork.sprite = frame;
                    animator.Artwork.transform.localPosition = Vector3.zero;
                    entry.poses.Add(new Pose { name = $"뛰기 {index + 1}",
                        file = RenderBody(owner, shadow, $"{spec.Id}-hop-{index}.png") });
                }
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
            return entry;
        }

        private static bool IsPlaceholder(Renderer renderer) =>
            renderer is SpriteRenderer sprite && renderer.GetComponent<Collider2D>() != null &&
            (sprite.color.r < 0.99f || sprite.color.g < 0.99f || sprite.color.b < 0.99f);

        private static void Prepare(GameObject owner)
        {
            owner.transform.SetParent(null);
            owner.transform.position = Vector3.zero;
            owner.SetActive(true);
            Layer(owner);
            foreach (MonoBehaviour behaviour in owner.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
        }

        private static void RenderActor(GameObject owner, string bodyFile, string shadowFile)
        {
            GroundShadow shadow = GroundShadow.Ensure(owner);
            shadow.enabled = true;
            Physics2D.SyncTransforms();
            shadow.Refresh();
            RenderShadowOnly(owner, shadow, shadowFile);
            RenderBody(owner, shadow, bodyFile);
        }

        // The actor's drawn body on a transparent canvas centered on its root.
        private static string RenderBody(GameObject owner, GroundShadow shadow, string file)
        {
            if (shadow != null && shadow.Visual != null) shadow.Visual.enabled = false;
            foreach (Transform node in owner.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 30;
            if (shadow != null && shadow.Visual != null) shadow.Visual.gameObject.layer = 31;
            CenterOnRoot(owner);
            int size = Mathf.RoundToInt(ActorCanvas * PixelsPerUnit);
            Render(file, size, size);
            if (shadow != null && shadow.Visual != null)
            {
                shadow.Visual.gameObject.layer = 30;
                shadow.Visual.enabled = true;
            }
            return file;
        }

        private static void RenderShadowOnly(GameObject owner, GroundShadow shadow, string file)
        {
            if (shadow == null || shadow.Visual == null) return;
            foreach (Transform node in owner.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
            shadow.Visual.gameObject.layer = 30;
            shadow.Visual.enabled = true;
            CenterOnRoot(owner);
            int size = Mathf.RoundToInt(ActorCanvas * PixelsPerUnit);
            Render(file, size, size);
            foreach (Transform node in owner.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 30;
        }

        private static void CenterOnRoot(GameObject owner)
        {
            camera.orthographicSize = ActorCanvas * 0.5f;
            camera.transform.position = new Vector3(owner.transform.position.x, owner.transform.position.y, -10f);
        }

        private static void Layer(GameObject owner)
        {
            foreach (Transform node in owner.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 30;
        }

        private static void Render(string file, int width, int height)
        {
            RenderTexture target = new(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.aspect = width / (float)height;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(folder, file), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
