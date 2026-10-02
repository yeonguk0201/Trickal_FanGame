using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TrickalFanGame.Editor
{
    public static class FairyVillageArtworkVerification
    {
        [MenuItem("Trickal Fan Game/Artwork/Verify Fairy Village Tiles and Walls")]
        public static void Verify()
        {
            VerifyForegroundTransparency();
            VerifySpecialDoorMasks();
            VerifyInnerBoundaryKeyContact();
            string[] paths = FairyVillageArtworkSetup.PrefabPaths();
            float? referenceClearance = null;
            Assert(paths.Length > 0, "No room prefabs found.");
            foreach (string path in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                RoomPrefab room = prefab.GetComponent<RoomPrefab>();
                Assert(room.TryValidate(out string error), path + ": " + error);
                Transform tiles = room.Node.ContentRoot.transform.Find(FairyVillageArtworkSetup.RootName);
                Assert(tiles != null && tiles.Find("Grass Floor") != null && tiles.Find("Stone Path Horizontal") == null &&
                    tiles.GetComponentsInChildren<SpriteRenderer>(true).Length >= 11,
                    path + " missing tiled floor/walls.");
                Assert(tiles.GetComponentsInChildren<Collider2D>(true).Length == 0,
                    "Artwork must not add collision geometry.");
                Transform boundaries = room.Node.ContentRoot.transform.Find(FairyVillageArtworkSetup.BoundaryName);
                Assert(boundaries != null && boundaries.GetComponentsInChildren<BoxCollider2D>().Length == 8,
                    path + " must have eight inner wall boundaries with four door corridors.");
                Rect floor = FairyVillageArtworkSetup.WalkableFloor(FairyVillageArtworkSetup.RoomSize(room.Node.ContentRoot.transform));
                foreach (BoxCollider2D wall in boundaries.GetComponentsInChildren<BoxCollider2D>()) {
                    Vector2 center = wall.transform.localPosition;
                    Assert(!wall.isTrigger && wall.enabled && !floor.Contains(center), "Invalid inner wall collision.");
                    Assert(wall.gameObject.layer == room.Node.ContentRoot.transform.Find("Top Left Wall").gameObject.layer,
                        "Artwork boundaries must use the existing wall collision layer.");
                }
                Assert(prefab.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(r => r.transform.IsChildOf(tiles)).All(r => r.sprite != null &&
                        (r.name.StartsWith(FairyVillageArtworkSetup.ForegroundName) ? r.sortingOrder == 20 &&
                            r.GetComponent<ConnectedRoomPatch>().ForegroundCutoff == 1f &&
                            r.GetComponent<ConnectedRoomPatch>().ForegroundMask != null : r.sortingOrder < 0)),
                    path + " artwork has missing sprites or covers gameplay objects.");
                Assert(tiles.GetComponentsInChildren<SpriteRenderer>().Count(r =>
                    r.name.StartsWith(FairyVillageArtworkSetup.ForegroundName)) == 4, "Missing lower wall foreground spans.");
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    instance.GetComponent<RoomPrefab>().Node.ContentRoot.SetActive(true);
                    Transform content = instance.GetComponent<RoomPrefab>().Node.ContentRoot.transform;
                    BoxCollider2D lowerWall = content.Find(FairyVillageArtworkSetup.BoundaryName + "/Lower Left").GetComponent<BoxCollider2D>();
                    SpriteRenderer lowerForeground = content.Find(FairyVillageArtworkSetup.RootName + "/" +
                        FairyVillageArtworkSetup.ForegroundName + " 1").GetComponent<SpriteRenderer>();
                    float drawnEdge = lowerForeground.bounds.min.y + lowerForeground.bounds.size.y * (158f / 221f);
                    float clearance = lowerWall.bounds.max.y + 0.5f - drawnEdge;
                    if (!referenceClearance.HasValue) referenceClearance = clearance;
                    Assert(Mathf.Abs(clearance - referenceClearance.Value) < 0.01f,
                        "Small, Basic and large rooms must hide the same actor depth at the lower hedge.");
                    foreach (RoomDoorSlot slot in instance.GetComponent<RoomPrefab>().DoorSlots)
                    {
                        BoxCollider2D sealedWall = slot.Seal.transform.Find(FairyVillageArtworkSetup.SealBoundaryName)?.GetComponent<BoxCollider2D>();
                        Assert(sealedWall != null && !sealedWall.isTrigger, "Missing central wall boundary on sealed face.");
                        RoomNode node = instance.GetComponent<RoomPrefab>().Node;
                        slot.Bind(null, node, null, null, null);
                        Assert(slot.Seal.activeSelf && sealedWall.gameObject.activeInHierarchy,
                            "An absent doorway must fill the central gap.");
                        slot.Bind(null, node, node, slot.EntryPoint, null, isSealed: true);
                        Assert(slot.Seal.activeSelf && sealedWall.gameObject.activeInHierarchy,
                            "A hidden sealed doorway must fill the central gap.");
                        slot.Bind(null, node, node, slot.EntryPoint, null);
                        Assert(!slot.Seal.activeSelf && !sealedWall.gameObject.activeInHierarchy,
                            "A connected doorway must release its central boundary.");
                        FairyVillageDoorArtwork binding = slot.Blocker.GetComponent<FairyVillageDoorArtwork>();
                        slot.Doorway.Configure(null, instance.GetComponent<RoomPrefab>().Node, null, null);
                        Vector2 outward = slot.Direction switch {
                            RoomDoorDirection.Up => Vector2.up, RoomDoorDirection.Down => Vector2.down,
                            RoomDoorDirection.Left => Vector2.left, _ => Vector2.right };
                        Assert(slot.Doorway.IsMovingIntoPassage(outward) &&
                            slot.Doorway.IsMovingIntoPassage((outward + new Vector2(-outward.y, outward.x)).normalized) &&
                            !slot.Doorway.IsMovingIntoPassage(new Vector2(-outward.y, outward.x)) &&
                            !slot.Doorway.IsMovingIntoPassage(-outward) &&
                            !slot.Doorway.IsMovingIntoPassage(Vector2.zero),
                            "Every door must require movement toward the exit, including diagonal approach.");
                        SpriteRenderer visual = slot.Blocker.transform.Find("Fairy Village Door")?.GetComponent<SpriteRenderer>();
                        BoxCollider2D inner = slot.Blocker.transform.Find(FairyVillageArtworkSetup.ClosedBoundaryName)?.GetComponent<BoxCollider2D>();
                        Assert(inner != null && inner.GetComponent<ClosedDoorBoundary>() != null, "Missing closed door inner boundary.");
                        SpriteRenderer front = slot.Blocker.transform.Find(FairyVillageArtworkSetup.ForegroundName)?.GetComponent<SpriteRenderer>();
                        Assert(slot.Direction == RoomDoorDirection.Down ? front != null && front.sortingOrder == 20 : front == null,
                            "Only lower doors must render in front of actors.");
                        Assert(binding != null && visual != null, "Missing door artwork.");
                        Assert(!visual.flipX && visual.sprite.name.StartsWith(slot.Direction.ToString().ToLowerInvariant()),
                            "Each direction needs its own registered artwork, without mirrored doorway patches.");
                        Vector3 scale = visual.transform.lossyScale;
                        float expectedScale = FairyVillageArtworkSetup.NativeScale(FairyVillageArtworkSetup.RoomSize(
                            instance.GetComponent<RoomPrefab>().Node.ContentRoot.transform)) * 100;
                        Assert(Mathf.Abs(scale.x - scale.y) < 0.001f && Mathf.Abs(scale.x - expectedScale) < 0.001f,
                            "Door artwork must preserve its source aspect ratio.");
                        slot.Blocker.ConfigureVisualKind(DoorVisualKind.Normal);
                        slot.Blocker.SetKeyLocked(false);
                        slot.Blocker.SetLocked(false);
                        binding.Refresh();
                        Sprite open = visual.sprite;
                        Assert(!inner.enabled, "Open door must release its inner boundary.");
                        if (front != null) Assert(front.sprite == open && front.GetComponent<ConnectedRoomPatch>().ForegroundMask.name.StartsWith("door-open"),
                            "Lower open door must preserve a transparent passage.");
                        Assert(open != null && open.name.EndsWith("open") && slot.Blocker.IsPortalBarrierActive,
                            "Open door must show an open sprite and preserve the portal barrier.");
                        slot.Blocker.SetLocked(true);
                        binding.Refresh();
                        Assert(visual.sprite != open && visual.sprite.name.EndsWith("closed"), "Closed sprite missing.");
                        Assert(inner.enabled, "Combat-closed door must fill its central gap.");
                        if (front != null) Assert(front.sprite == visual.sprite && front.GetComponent<ConnectedRoomPatch>().ForegroundMask.name.StartsWith("door-closed"),
                            "Lower closed door must render its wood in front of actors.");
                        slot.Blocker.ConfigureVisualKind(DoorVisualKind.KeyLockedTreasure);
                        binding.Refresh();
                        Assert(visual.sprite.name.EndsWith("locked"), "Key lock sprite missing.");
                        slot.Blocker.SetLocked(false);
                        slot.Blocker.SetKeyLocked(true);
                        binding.Refresh();
                        Assert(visual.sprite.name.EndsWith("locked") && slot.Blocker.IsPortalBarrierActive,
                            "Key lock must keep both the artwork and barrier active.");
                        Assert(inner.enabled, "Key-locked door must fill its central gap.");
                        slot.Blocker.SetKeyLocked(false);
                        slot.Blocker.ConfigureVisualKind(DoorVisualKind.Normal);
                        binding.Refresh();
                        Assert(visual.sprite == open, "Unlock must restore open artwork.");
                        Assert(!inner.enabled, "Unlock must release the inner boundary immediately.");
                        Vector2 registeredSize = visual.bounds.size;
                        foreach (string family in FairyVillageArtworkSetup.DoorFamilies)
                        {
                            slot.Blocker.ConfigureVisualKind(FairyVillageArtworkSetup.FamilyKind(family));
                            binding.Refresh();
                            string prefix = slot.Direction.ToString().ToLowerInvariant() + "-" + family;
                            Assert(visual.sprite.name.StartsWith(prefix) && visual.sprite.name.EndsWith("open"),
                                "Every special doorway needs its own direction and open art: " + prefix);
                            Assert(((Vector2)visual.bounds.size - registeredSize).sqrMagnitude < 0.000001f,
                                "Generated canvas size differences must not move the doorway joins.");
                            Assert(AssetDatabase.GetAssetPath(visual.sprite) == FairyVillageArtworkSetup.FamilyPath(family, "open",
                                FairyVillageArtworkSetup.FamilyVersion(family, slot.Direction)),
                                "Only approved directions should switch to revised special door artwork.");
                            if (front != null) Assert(front.sprite == visual.sprite &&
                                AssetDatabase.GetAssetPath(front.GetComponent<ConnectedRoomPatch>().ForegroundMask) ==
                                    FairyVillageArtworkSetup.FamilyMaskPath(family, "open"),
                                "Special lower door must use its own silhouette and transparent aperture.");
                            slot.Blocker.SetKeyLocked(true);
                            binding.Refresh();
                            Assert(inner.enabled && (family == "secret" ? visual.sprite.name.EndsWith("sealed") :
                                visual.sprite.name.StartsWith(prefix) && !visual.sprite.name.EndsWith("open")),
                                "Locked special door must close its gap and preserve its room identity.");
                            if (family != "secret") Assert(AssetDatabase.GetAssetPath(visual.sprite) ==
                                FairyVillageArtworkSetup.FamilyPath(family, "closed", FairyVillageArtworkSetup.FamilyVersion(family, slot.Direction)),
                                "Closed special door must use the same revision as its open frame.");
                            if (front != null) Assert(front.sprite == visual.sprite &&
                                front.GetComponent<ConnectedRoomPatch>().ForegroundMask != null,
                                "Special closed lower door must retain a foreground mask.");
                            slot.Blocker.SetKeyLocked(false);
                            binding.Refresh();
                            Assert(!inner.enabled && visual.sprite.name.StartsWith(prefix) && visual.sprite.name.EndsWith("open"),
                                "Unlocking must preserve the special doorway's identity.");
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            Debug.Log($"Fairy Village artwork verification passed: {paths.Length} templates; floor, walls, door states and portal barriers.");
        }

        private static void VerifySpecialDoorMasks()
        {
            foreach (string family in FairyVillageArtworkSetup.DoorFamilies)
            foreach (string state in family == "secret" ? new[] { "open" } : new[] { "open", "closed" })
            {
                Texture2D mask = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert(mask.LoadImage(System.IO.File.ReadAllBytes(FairyVillageArtworkSetup.FamilyMaskPath(family, state))),
                        "Missing special doorway mask.");
                    Assert(Mathf.Abs(mask.width - 1672) <= 1 && mask.height == 941 && mask.GetPixel(836, 470).a < 0.01f,
                        "Special doorway masks must preserve registration and remove the floor.");
                    Assert(state == "open" ? mask.GetPixel(836, 61).a < 0.05f : mask.GetPixel(836, 61).a > 0.9f,
                        "Open passages must be transparent; closed panels must remain opaque: " + family);
                    if (family == "secret") Assert(mask.GetPixel(836, 941 - 770 - 1).a > 0.8f,
                        "The lower secret opening must retain its connected upper hedge cap in the foreground.");
                }
                finally { UnityEngine.Object.DestroyImmediate(mask); }
            }
            foreach (GeneratedRoomRole role in new[] { GeneratedRoomRole.Shop, GeneratedRoomRole.Treasure,
                GeneratedRoomRole.Boss, GeneratedRoomRole.Secret })
            {
                GeneratedRoomNode normal = new GeneratedRoomNode("art-normal", 1, 1, GeneratedRoomRole.Intermediate, null);
                GeneratedRoomNode special = new GeneratedRoomNode("art-special", 1, 2, role, null);
                DoorVisualKind expected = role switch { GeneratedRoomRole.Shop => DoorVisualKind.Shop,
                    GeneratedRoomRole.Treasure => DoorVisualKind.KeyLockedTreasure,
                    GeneratedRoomRole.Boss => DoorVisualKind.Boss, _ => DoorVisualKind.SecretPassage };
                Assert(RoomGraphAssembler.ConnectionVisualKind(normal, special) == expected &&
                    RoomGraphAssembler.ConnectionVisualKind(special, normal) == expected,
                    "Special door identity must persist in both directions without depending on a key lock.");
            }
        }

        private static void VerifyForegroundTransparency()
        {
            Texture2D mask = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try {
                Assert(mask.LoadImage(System.IO.File.ReadAllBytes(FairyVillageArtworkSetup.ForegroundMaskPath)),
                    "Unable to read transparent foreground cutout.");
                Assert(mask.width == 1672 && mask.height == 941, "Foreground canvas must match the room master.");
                Assert(mask.GetPixel(836, 470).a < 0.01f && mask.GetPixel(836, 110).a > 0.9f,
                    "The courtyard must be transparent and the lower wall opaque.");
                int minimum = 860, maximum = 720;
                for (int x = 220; x < 1452; x += 11) {
                    Assert(mask.GetPixel(x, 941 - 750 - 1).a < 0.05f,
                        "Foreground must not hide actors on the grassy floor.");
                    int edge = 860;
                    for (int y = 720; y < 860; y++) {
                        if (mask.GetPixel(x, 941 - y - 1).a > 0.5f) { edge = y; break; }
                    }
                    minimum = Mathf.Min(minimum, edge); maximum = Mathf.Max(maximum, edge);
                }
                Assert(maximum - minimum >= 3 && maximum < 810,
                    "Foreground must follow a leafy silhouette instead of a horizontal crop.");
            }
            finally { UnityEngine.Object.DestroyImmediate(mask); }
        }

        private static void VerifyInnerBoundaryKeyContact()
        {
            GameObject roomObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Week8GridFloorSetup.PrefabPath));
            GameObject playerObject = new GameObject("Inner door contact verification player");
            GameObject graphObject = new GameObject("Inner door contact verification graph");
            try {
                playerObject.AddComponent<Rigidbody2D>().gravityScale = 0;
                playerObject.AddComponent<CircleCollider2D>().radius = 0.5f;
                Health health = playerObject.AddComponent<Health>();
                playerObject.AddComponent<PlayerStats>();
                PlayerMovement player = playerObject.AddComponent<PlayerMovement>();
                typeof(Health).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, null);
                typeof(PlayerMovement).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
                FieldInfo movement = typeof(PlayerMovement).GetField("movement", BindingFlags.Instance | BindingFlags.NonPublic);
                RunProgress progress = graphObject.AddComponent<RunProgress>();
                RoomGraphController graph = graphObject.AddComponent<RoomGraphController>();
                RoomPrefab room = roomObject.GetComponent<RoomPrefab>();
                room.Node.ContentRoot.SetActive(true);
                graph.Configure(new[] { room.Node }, room.Node, player, null, progress);
                foreach (RoomDoorSlot slot in room.DoorSlots) {
                    RoomRunState state = new RoomRunState("inner-key-" + slot.Direction);
                    slot.Bind(graph, room.Node, room.Node, slot.EntryPoint, null, requiresKey: true, keyLockState: state);
                    BoxCollider2D inner = slot.Blocker.transform.Find(FairyVillageArtworkSetup.ClosedBoundaryName).GetComponent<BoxCollider2D>();
                    ClosedDoorBoundary contact = inner.GetComponent<ClosedDoorBoundary>();
                    Vector2 outward = slot.Direction switch {
                        RoomDoorDirection.Up => Vector2.up, RoomDoorDirection.Down => Vector2.down,
                        RoomDoorDirection.Left => Vector2.left, _ => Vector2.right };
                    Rect floor = FairyVillageArtworkSetup.WalkableFloor(FairyVillageArtworkSetup.RoomSize(room.Node.ContentRoot.transform));
                    Vector2 point = slot.Direction switch {
                        RoomDoorDirection.Up => new Vector2(0, floor.yMax - 0.5f),
                        RoomDoorDirection.Down => new Vector2(0, floor.yMin + 0.5f),
                        RoomDoorDirection.Left => new Vector2(floor.xMin + 0.5f, 0),
                        _ => new Vector2(floor.xMax - 0.5f, 0) };
                    player.transform.position = room.Node.ContentRoot.transform.TransformPoint(point);
                    movement.SetValue(player, outward);
                    Assert(!contact.TryUnlockFromContact(player) && inner.enabled, "No key must leave the inner edge blocked.");
                    progress.TryAddResource(RunResourceType.Key, 1);
                    movement.SetValue(player, new Vector2(-outward.y, outward.x));
                    Assert(!contact.TryUnlockFromContact(player) && progress.GetResourceCount(RunResourceType.Key) == 1,
                        "Sliding beside a locked door must not consume a key.");
                    movement.SetValue(player, Vector2.zero);
                    Assert(!contact.TryUnlockFromContact(player), "Idle contact must not unlock a door.");
                    movement.SetValue(player, outward);
                    Assert(contact.TryUnlockFromContact(player) && !inner.enabled && state.IsKeyLockOpen &&
                        progress.GetResourceCount(RunResourceType.Key) == 0, "Intentional inner-edge contact must unlock and release the path.");
                    Assert(!contact.TryUnlockFromContact(player), "Repeated contact must not spend another key.");
                }
            }
            finally {
                UnityEngine.Object.DestroyImmediate(roomObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(graphObject);
            }
        }

        public static void SetupAndVerifyBatch()
        {
            string before = CollisionSnapshot();
            FairyVillageArtworkSetup.Setup();
            string first = ArtworkSnapshot();
            FairyVillageArtworkSetup.Setup();
            Assert(before == CollisionSnapshot(), "Artwork setup changed collider geometry or prefab GUIDs.");
            Assert(first == ArtworkSnapshot(), "Repeated setup changed sprite references or created duplicate artwork.");
            Verify();
            Week19Door1Verification.Verify();
        }

        public static void RenderPreviewBatch()
        {
            Verify();
            RenderRoom(Week8GridFloorSetup.PrefabPath, "fairy-village-room-preview.png", false);
            RenderRoom(Week8GridFloorSetup.PrefabPath, "fairy-village-door-states.png", true);
            RenderRoom(Week14Room3Setup.SmallPrefabPath, "fairy-village-small-preview.png", false);
            RenderRoom(Week14Room3Setup.WidePrefabPath, "fairy-village-wide-preview.png", false);
            RenderRoom(Week14Room6Setup.TallPrefabPath, "fairy-village-tall-preview.png", false);
            RenderRoom(Week14Room6Setup.LargePrefabPath, "fairy-village-large-preview.png", false);
            RenderRoom(Week8GridFloorSetup.PrefabPath, "fairy-village-wall-depth-preview.png", false, true);
            RenderRoom(Week14Room3Setup.SmallPrefabPath, "fairy-village-small-wall-depth-preview.png", false, true);
            RenderRoom(Week14Room6Setup.LargePrefabPath, "fairy-village-large-wall-depth-preview.png", false, true);
            RenderRoom(Week8GridFloorSetup.PrefabPath, "fairy-village-lower-door-open-preview.png", false, true, 0);
            RenderRoom(Week8GridFloorSetup.PrefabPath, "fairy-village-lower-door-closed-preview.png", false, true, 1);
            RenderRoom(Week8GridFloorSetup.PrefabPath, "fairy-village-lower-door-locked-preview.png", false, true, 2);
            Debug.Log("Connected Fairy Village previews saved: Basic, door states, Wide, Tall, Large.");
        }

        private static void RenderRoom(string path, string filename, bool showStates, bool depthPreview = false, int doorDepth = -1,
            DoorVisualKind? family = null, bool mixedDoors = false, RoomDoorDirection? focus = null)
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            GameObject room = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(path));
            room.GetComponent<RoomPrefab>().Node.ContentRoot.SetActive(true);
            foreach (RoomDoorSlot slot in room.GetComponent<RoomPrefab>().DoorSlots)
            {
                slot.Seal.SetActive(showStates && slot.Direction == RoomDoorDirection.Left ||
                    depthPreview && doorDepth < 0 && slot.Direction == RoomDoorDirection.Down);
                slot.Blocker.gameObject.SetActive(!slot.Seal.activeSelf);
                slot.Blocker.SetLocked(showStates && slot.Direction == RoomDoorDirection.Up ||
                    doorDepth > 0 && slot.Direction == RoomDoorDirection.Down);
                slot.Blocker.ConfigureVisualKind((showStates && slot.Direction == RoomDoorDirection.Right ||
                    doorDepth == 2 && slot.Direction == RoomDoorDirection.Down)
                    ? DoorVisualKind.KeyLockedTreasure : DoorVisualKind.Normal);
                slot.Blocker.SetKeyLocked(showStates && slot.Direction == RoomDoorDirection.Right ||
                    doorDepth == 2 && slot.Direction == RoomDoorDirection.Down);
                if (family.HasValue) slot.Blocker.ConfigureVisualKind(family.Value);
                if (focus.HasValue) slot.Blocker.SetLocked(doorDepth > 0);
                if (mixedDoors) slot.Blocker.ConfigureVisualKind(slot.Direction switch {
                    RoomDoorDirection.Up => DoorVisualKind.Boss, RoomDoorDirection.Down => DoorVisualKind.KeyLockedTreasure,
                    RoomDoorDirection.Left => DoorVisualKind.Shop, _ => DoorVisualKind.SecretPassage });
                slot.Blocker.GetComponent<FairyVillageDoorArtwork>().Refresh();
            }
            Camera camera = new GameObject("Artwork Preview Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            Vector2 size = FairyVillageArtworkSetup.RoomSize(room.GetComponent<RoomPrefab>().Node.ContentRoot.transform);
            if (depthPreview) {
                Sprite player = Resources.LoadAll<Sprite>("Characters/Erpin_Walking").First(s => s.name == "Erpin_Walk_Down_0");
                Sprite monster = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Enemies/FairyKingdom/Enemy_HighBloodSugarFairy_Idle.png")
                    .OfType<Sprite>().First();
                Rect floor = FairyVillageArtworkSetup.WalkableFloor(size);
                void Actor(Sprite sprite, float x) {
                    SpriteRenderer renderer = new GameObject("Wall depth preview actor").AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.transform.localScale = Vector3.one * (1.2f / sprite.bounds.size.y);
                    renderer.transform.position = new Vector3(x, floor.yMin + 0.5f, 0);
                    renderer.sortingOrder = 0;
                }
                Actor(player, doorDepth >= 0 ? -0.65f : -3); Actor(monster, doorDepth >= 0 ? 0.65f : 3);
            }
            camera.orthographicSize = Mathf.Max(size.y / 2 + 0.7f, (size.x + 1.4f) / (2f * 1600f / 900f));
            if (family.HasValue && depthPreview)
            {
                camera.transform.position = new Vector3(0, -size.y / 2 + 0.6f, -10);
                camera.orthographicSize = 1.9f;
            }
            if (focus.HasValue)
            {
                camera.transform.position = new Vector3(focus == RoomDoorDirection.Left ? -size.x / 2 + 0.5f : size.x / 2 - 0.5f, 0, -10);
                camera.orthographicSize = 1.9f;
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.06f);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            RenderTexture target = new RenderTexture(1600, 900, 24);
            target.Create();
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            else
            {
                camera.targetTexture = target;
                camera.Render();
            }
            RenderTexture.active = target;
            Texture2D output = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            output.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            output.Apply();
            System.IO.Directory.CreateDirectory("../output");
            System.IO.File.WriteAllBytes("../output/" + filename, output.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(output);
            UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("Fairy Village rendered preview saved to output/" + filename);
        }

        public static void ApplyVerifyAndRenderBatch()
        {
            SetupAndVerifyBatch();
            Week14Room0RegressionVerification.Verify();
            OpenFreshGameScene();
            Week20Special1Verification.Verify();
            OpenFreshGameScene();
            Week20Special3Verification.Verify();
            VerifyShopAndRenderBatch();
        }

        private static void OpenFreshGameScene() => UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            Week13FrontendSetup.GameScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        public static void VerifyShopAndRenderBatch()
        {
            // Edit-mode time does not advance between synchronous verifiers. Reload the scene
            // so the previous secret-room transition cannot leave a cooldown in the shop test.
            OpenFreshGameScene();
            Week20Special4Verification.Verify();
            RenderSpecialDoorsBatch();
        }

        public static void RenderSpecialDoorsBatch()
        {
            RenderPreviewBatch();
            foreach (string family in FairyVillageArtworkSetup.DoorFamilies)
            {
                RenderRoom(Week8GridFloorSetup.PrefabPath, "fairy-village-" + family + "-preview.png", false,
                    family: FairyVillageArtworkSetup.FamilyKind(family));
                RenderRoom(Week8GridFloorSetup.PrefabPath, "fairy-village-" + family + "-depth-preview.png", false, true, 0,
                    FairyVillageArtworkSetup.FamilyKind(family));
                if (family != "secret") RenderRoom(Week8GridFloorSetup.PrefabPath, "fairy-village-" + family + "-closed-preview.png",
                    false, true, 1, FairyVillageArtworkSetup.FamilyKind(family));
                if (family != "secret") foreach (RoomDoorDirection direction in new[] { RoomDoorDirection.Left, RoomDoorDirection.Right })
                    foreach (bool closed in new[] { false, true })
                        RenderRoom(Week8GridFloorSetup.PrefabPath,
                            $"fairy-village-{family}-{direction.ToString().ToLowerInvariant()}-{(closed ? "closed" : "open")}-v{FairyVillageArtworkSetup.FamilyVersion(family, direction)}-preview.png",
                            false, doorDepth: closed ? 1 : 0, family: FairyVillageArtworkSetup.FamilyKind(family), focus: direction);
            }
            RenderRoom(Week8GridFloorSetup.PrefabPath, "fairy-village-special-doors-gallery.png", false, mixedDoors: true);
        }

        public static void ApplyRevisionAndRenderBatch()
        {
            SetupAndVerifyBatch();
            Week14Room0RegressionVerification.Verify();
            RenderSpecialDoorsBatch();
        }

        private static string CollisionSnapshot() => string.Join("\n", FairyVillageArtworkSetup.PrefabPaths().Select(path =>
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return AssetDatabase.AssetPathToGUID(path) + string.Join("|", root.GetComponentsInChildren<BoxCollider2D>(true)
                .Where(c => c.transform.parent == null || c.transform.parent.name != FairyVillageArtworkSetup.BoundaryName)
                .Where(c => c.name != FairyVillageArtworkSetup.SealBoundaryName && c.name != FairyVillageArtworkSetup.ClosedBoundaryName)
                .Select(c => c.name + c.transform.position.ToString("F5") + c.transform.lossyScale.ToString("F5") +
                    c.size.ToString("F5") + c.offset.ToString("F5") + c.enabled + c.isTrigger));
        }));

        private static string ArtworkSnapshot() => string.Join("\n", FairyVillageArtworkSetup.PrefabPaths().Select(path =>
            string.Join("|", AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r.sprite != null).Select(r =>
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(r.sprite, out string guid, out long id);
                    return r.name + guid + id + r.transform.localScale.ToString("F5");
                }))));

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
