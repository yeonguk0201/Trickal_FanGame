using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week27Ux1Verification
    {
        // Both 16:9 resolutions the plan names (26 §3.2). The HUD canvas scales the 1920 x 1080 frame.
        private static readonly Vector2[] Resolutions = { new(1920, 1080), new(1280, 720) };
        private const float MinimumScreenMargin = 8f;
        private const float MinimumTextPixels = 10f;

        // Every always-on HUD panel in the frame. Full-screen overlays and centre announcements are left out.
        private static readonly string[] HudPanels =
        {
            "Survival HUD", Week13Hud2Setup.SkillPanelName, Week13Hud3ASetup.ArtifactPanelName,
            Week13Hud4ASetup.MinimapPanelName, Week27Ux1Setup.ResourcePanelName, Week13Hud5Setup.PanelName,
            Week13Setting1CSetup.HudPanelName, Week21Slot0Setup.HudPanelName, Week27Power1Setup.HudPanelName,
        };

        [MenuItem("Trickal Fan Game/Week 27/Verify UX-1 Right HUD, Minimap and Pickup Description")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            GameHudView gameHud = Week27Ux1Setup.FindSingle<GameHudView>(scene, "GameHudView");
            RectTransform frame = gameHud.transform.Find("ReferenceFrame") as RectTransform;
            Assert(frame != null, "Game HUD ReferenceFrame is missing.");

            VerifyLayout(frame);
            VerifyResources(scene, frame);
            VerifyMinimap(scene);
            VerifyDescription(scene, frame);

            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            Debug.Log("Week 27 UX-1 verification passed: right HUD column without overlaps at 1920x1080 and " +
                      "1280x720, gold/key/bomb counts, current-room minimap viewport and nearest item description.");
        }

        public static void SetupAndVerifyBatch()
        {
            string gameGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week27Ux1Setup.Setup();
            int firstCount = CountTransforms(EditorSceneManager.GetActiveScene());
            Week27Ux1Setup.Setup();
            Assert(CountTransforms(EditorSceneManager.GetActiveScene()) == firstCount,
                "Running UX-1 setup twice duplicated objects.");
            Assert(gameGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "UX-1 setup changed the Game Scene GUID.");
            Verify();
        }

        // HUD-3A and HUD-4A re-run their setups and must keep the UX-1 layout; Slot-0 and Power-1 own the slots.
        public static void VerifyWithRegressionsBatch()
        {
            SetupAndVerifyBatch();
            Week13Hud3AVerification.SetupAndVerifyBatch();
            Week13Hud4AVerification.SetupAndVerifyBatch();
            Week13Hud4BVerification.SetupAndVerifyBatch();
            Week13Hud1Verification.Verify();
            Week13Hud2Verification.Verify();
            Week13Setting1CVerification.Verify();
            Week21Slot0Verification.Verify();
            Week27Power1Verification.Verify();
            Week26Spell6Verification.Verify();
            Verify();
            Debug.Log("Week 27 UX-1 regression verification passed.");
        }

        private static void VerifyLayout(RectTransform frame)
        {
            RectTransform minimap = Panel(frame, Week13Hud4ASetup.MinimapPanelName);
            RectTransform resources = Panel(frame, Week27Ux1Setup.ResourcePanelName);
            RectTransform artifacts = Panel(frame, Week13Hud3ASetup.ArtifactPanelName);
            RectTransform survival = Panel(frame, "Survival HUD");
            RectTransform power = Panel(frame, Week27Power1Setup.HudPanelName);
            RectTransform stats = Panel(frame, Week13Setting1CSetup.HudPanelName);
            Assert(minimap.anchoredPosition == Week27Ux1Setup.MinimapPosition &&
                   minimap.sizeDelta == Week27Ux1Setup.MinimapSize &&
                   artifacts.anchoredPosition == Week27Ux1Setup.ArtifactPosition &&
                   artifacts.sizeDelta == Week27Ux1Setup.ArtifactSize && Bottom(minimap) > Top(artifacts),
                "The right HUD column must be the minimap with the artifacts under it.");
            Assert(survival.anchoredPosition == Week27Ux1Setup.SurvivalPosition &&
                   survival.sizeDelta == Week27Ux1Setup.SurvivalSize &&
                   power.anchoredPosition == Week27Ux1Setup.PowerPosition &&
                   power.sizeDelta == Week27Ux1Setup.PowerSize &&
                   resources.anchoredPosition == Week27Ux1Setup.ResourcePosition &&
                   resources.sizeDelta == Week27Ux1Setup.ResourceSize &&
                   stats.anchoredPosition == Week27Ux1Setup.DetailedStatsPosition,
                "The top-left HUD must use the UX-1 positions.");
            Assert(Right(power) < Left(survival) && Mathf.Approximately(Top(power), Top(survival)) &&
                   Left(survival) - Right(power) <= 40f,
                "The 권능 must sit just left of the hearts and SP, aligned with their top.");
            Assert(Bottom(survival) > Top(resources) && Left(resources) == Left(survival) &&
                   Bottom(survival) - Top(resources) <= 24f,
                "Gold, keys and bombs must sit right under the hearts and SP.");
            VerifySurvivalSizes(frame);
            VerifyPowerGauge(power);
            GameArtifactHudView artifactView = artifacts.GetComponent<GameArtifactHudView>();
            Assert(artifactView != null && artifactView.MaximumVisible == 10 && artifactView.OverflowText != null,
                "The moved artifact HUD must keep its 10 visible slots and +N overflow.");

            CanvasScaler scaler = frame.GetComponentInParent<CanvasScaler>();
            Assert(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                   scaler.referenceResolution == new Vector2(1920, 1080),
                "The HUD canvas must scale the 1920x1080 reference frame.");
            RectTransform[] panels = HudPanels.Select(name => Panel(frame, name)).ToArray();
            foreach (Vector2 resolution in Resolutions)
            {
                float scale = CanvasScale(scaler, resolution);
                Rect screen = new(Vector2.zero, resolution);
                Rect[] rects = panels.Select(panel => ScreenRect(panel, scale, resolution)).ToArray();
                for (int i = 0; i < rects.Length; i++)
                {
                    Assert(rects[i].xMin >= screen.xMin + MinimumScreenMargin &&
                           rects[i].xMax <= screen.xMax - MinimumScreenMargin &&
                           rects[i].yMin >= screen.yMin + MinimumScreenMargin &&
                           rects[i].yMax <= screen.yMax - MinimumScreenMargin,
                        $"{panels[i].name} leaves the screen at {resolution.x}x{resolution.y}: {rects[i]}.");
                    for (int j = i + 1; j < rects.Length; j++)
                        Assert(!rects[i].Overlaps(rects[j]),
                            $"{panels[i].name} overlaps {panels[j].name} at {resolution.x}x{resolution.y}.");
                }

                foreach (RectTransform panel in new[] { minimap, resources, artifacts, survival, power })
                // Room marker symbols are map icons that scale with the marker, not labels.
                foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true)
                             .Where(text => text.GetComponentInParent<MinimapRoomMarkerView>(true) == null))
                    Assert(text.fontSize * scale >= MinimumTextPixels,
                        $"{panel.name}/{text.name} is too small to read at {resolution.x}x{resolution.y}.");
            }

            Transform pause = frame.Find(Week13Hud3CSetup.OverlayName);
            Transform description = frame.Find(Week27Ux1Setup.DescriptionPanelName);
            Assert(description != null && pause != null &&
                   pause.GetSiblingIndex() > description.GetSiblingIndex() &&
                   pause.GetSiblingIndex() > resources.GetSiblingIndex(),
                "The pause overlay must stay above the UX-1 panels.");
            Assert(!resources.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget) &&
                   !description.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget),
                "UX-1 HUD graphics must not intercept combat input.");
        }

        private static void VerifyResources(Scene scene, RectTransform frame)
        {
            RunProgress sceneProgress = Week27Ux1Setup.FindSingle<RunProgress>(scene, "RunProgress");
            GameResourceHudView configured = Week27Ux1Setup.FindSingle<GameResourceHudView>(scene, "resource HUD");
            Assert(configured.Progress == sceneProgress && configured.EntryCount == 3,
                "The resource HUD must show gold, keys and bombs of the Run's progress.");
            for (int i = 0; i < 3; i++)
            {
                Assert(configured.GetIcon(i) != null && configured.GetIcon(i).sprite != null &&
                       configured.GetCountText(i) != null,
                    $"Resource HUD entry {GameResourceHudView.DisplayOrder[i]} needs an icon and a count.");
                string path = new[] { Week27Ux1Setup.GoldSpritePath, Week27Ux1Setup.KeySpritePath,
                    Week27Ux1Setup.BombSpritePath }[i];
                Assert(configured.GetIcon(i).sprite == AssetDatabase.LoadAssetAtPath<Sprite>(path) &&
                       configured.GetIcon(i).color == Color.white &&
                       configured.GetIcon(i).transform.Find("Letter") == null,
                    $"Resource HUD entry {GameResourceHudView.DisplayOrder[i]} must show its pickup artwork.");
            }

            GameObject progressObject = new("UX-1 Resource Progress", typeof(RunProgress));
            GameObject clone = Object.Instantiate(configured.gameObject, frame);
            try
            {
                RunProgress progress = progressObject.GetComponent<RunProgress>();
                GameResourceHudView view = clone.GetComponent<GameResourceHudView>();
                view.Configure(progress, Enumerable.Range(0, 3).Select(view.GetIcon).ToArray(),
                    Enumerable.Range(0, 3).Select(view.GetCountText).ToArray());
                view.RefreshNow();
                Assert(view.GetShownText(RunResourceType.Gold) == "00" &&
                       view.GetShownText(RunResourceType.Key) == "00" &&
                       view.GetShownText(RunResourceType.Bomb) == "00",
                    "An empty Run must show 00 for every resource.");
                progress.TryAddResource(RunResourceType.Gold, 7);
                progress.TryAddResource(RunResourceType.Key, 12);
                progress.TryAddResource(RunResourceType.Bomb, 3);
                progress.TrySpendResource(RunResourceType.Bomb);
                view.RefreshNow();
                Assert(view.GetShownText(RunResourceType.Gold) == "07" &&
                       view.GetShownText(RunResourceType.Key) == "12" &&
                       view.GetShownText(RunResourceType.Bomb) == "02",
                    "The resource HUD must show the current counts as two digits.");
                progress.TryAddResource(RunResourceType.Gold, 500);
                view.RefreshNow();
                Assert(view.GetShownText(RunResourceType.Gold) == "99", "Gold must show the 99 cap.");
            }
            finally
            {
                Object.DestroyImmediate(clone);
                Object.DestroyImmediate(progressObject);
            }
        }

        private static void VerifyMinimap(Scene scene)
        {
            GameMinimapView configured = Week27Ux1Setup.FindSingle<GameMinimapView>(scene, "GameMinimapView");
            Assert(configured.CentersOnCurrentRoom && configured.Content.parent == configured.MapRoot,
                "The minimap must place rooms under a content root inside its viewport.");
            RectMask2D mask = configured.MapRoot.GetComponent<RectMask2D>();
            Assert(mask != null && mask.softness.x > 0 && mask.softness.y > 0,
                "The minimap viewport must cut off outer rooms with a soft mask.");
            Assert(configured.MapRoot.sizeDelta == Week27Ux1Setup.MinimapViewportSize,
                "The minimap viewport must use the UX-1 size.");

            FloorGenerator generator = Week27Ux1Setup.FindSingle<FloorGenerator>(scene, "FloorGenerator");
            Assert(generator.TryGenerateForSeed(20260910, out GeneratedFloorGraph graph, out string error),
                "UX-1 test graph generation failed: " + error);
            GameObject progressObject = new("UX-1 Minimap Progress", typeof(RunProgress));
            GameObject clone = Object.Instantiate(configured.gameObject, configured.transform.parent);
            try
            {
                RunProgress progress = progressObject.GetComponent<RunProgress>();
                Assert(progress.TrySetGeneratedGraph(graph, out error), "UX-1 progress rejected the graph: " + error);
                GeneratedFloor floor = graph.FindFloor(1);
                GeneratedRoomNode start = floor.Nodes.First(node => node.RoomId == floor.StartingRoomId);
                GameMinimapView view = clone.GetComponent<GameMinimapView>();
                view.Configure(progress, view.MapRoot, view.MarkerTemplate, view.ConnectionTemplate, 48f);
                progress.RecordRoomEntry(start.FloorNumber, start.RoomNumber);

                Assert(Mathf.Approximately(view.EffectiveSpacing, view.RoomSpacing),
                    "The centred minimap must keep its real spacing instead of shrinking the floor to fit.");
                Assert(view.VisibleMarkers.All(marker => marker.transform.parent == view.Content),
                    "Room markers must live under the moving content root.");
                Vector2 startPosition = CurrentMarkerPosition(view);
                Assert(view.ContentTargetPosition == -startPosition &&
                       view.Content.anchoredPosition == view.ContentTargetPosition,
                    "Entering a floor must put the current room at the viewport centre at once.");

                GeneratedRoomNode next = floor.Nodes.First(node => node.RoomId ==
                    start.DirectionalConnections.First(connection => !connection.IsSecret).DestinationRoomId);
                progress.RecordRoomEntry(next.FloorNumber, next.RoomNumber);
                Vector2 nextPosition = CurrentMarkerPosition(view);
                Assert(view.ContentTargetPosition == -nextPosition && view.ContentTargetPosition != -startPosition,
                    "Moving to another room must aim the viewport at the new current room.");
                Assert(view.Content.anchoredPosition == -startPosition,
                    "Changing rooms must glide from the previous view instead of jumping (Update moves it).");
                Vector2 roomShift = nextPosition - startPosition;
                Assert(Mathf.Approximately(roomShift.magnitude, view.RoomSpacing * GridDistance(start, next)),
                    "Room positions must stay fixed in content space while the view moves.");

                // With the whole floor revealed, some rooms lie past the viewport edge and are cut off.
                progress.TryRevealFloorMap(1);
                view.SnapToCurrentRoom();
                Rect viewport = view.MapRoot.rect;
                bool anyOutside = view.VisibleMarkers.Any(marker =>
                {
                    Vector2 shown = ((RectTransform)marker.transform).anchoredPosition + view.ContentTargetPosition;
                    return !viewport.Contains(shown);
                });
                int spanX = floor.Nodes.Max(node => node.GridPosition.X) - floor.Nodes.Min(node => node.GridPosition.X);
                int spanY = floor.Nodes.Max(node => node.GridPosition.Y) - floor.Nodes.Min(node => node.GridPosition.Y);
                if (spanX * view.RoomSpacing > viewport.width || spanY * view.RoomSpacing > viewport.height)
                    Assert(anyOutside, "A floor larger than the viewport must not be shrunk to fit.");

                MinimapRoomShape basic = MinimapRoomShape.From(null);
                Assert(basic.MarkerSize == Vector2.one * MinimapRoomShape.BasicMarkerSize,
                    "A room without a silhouette must use the Basic marker size.");
                foreach (MinimapRoomMarkerView marker in view.VisibleMarkers)
                {
                    GeneratedRoomNode node = floor.Nodes.First(candidate => candidate.RoomId == marker.RoomId);
                    Assert(marker.SilhouetteSize == MinimapRoomShape.From(node.Template?.Profile).MarkerSize,
                        "Room markers must take their size from the minimap shape contract.");
                }
            }
            finally
            {
                Object.DestroyImmediate(clone);
                Object.DestroyImmediate(progressObject);
            }
        }

        private static void VerifyDescription(Scene scene, RectTransform frame)
        {
            PickupDescriptionView configured =
                Week27Ux1Setup.FindSingle<PickupDescriptionView>(scene, "pickup description");
            Assert(configured.Player != null && configured.Frame == frame && configured.NameText != null &&
                   configured.KindText != null && configured.RarityText != null && configured.EffectText != null &&
                   configured.DetailText != null,
                "The pickup description must reference the player, the HUD frame and its texts.");
            Assert(configured.GetComponent<CanvasGroup>().alpha == 0f, "The description must start hidden.");
            foreach (Type type in new[] { typeof(ItemPickup), typeof(SingleUseItemPickup), typeof(ActivePowerPickup) })
                Assert(typeof(IDescribedPickup).IsAssignableFrom(type), type.Name + " must be describable.");

            ItemDefinition artifact = FindItem(item => item.Kind == ItemKind.Artifact && item.Rarity == ItemRarity.Rare)
                                      ?? FindItem(item => item.Kind == ItemKind.Artifact);
            ItemDefinition spell = FindItem(item => item.Kind == ItemKind.SingleUseSpell);
            ItemDefinition power = FindItem(item => item.Kind == ItemKind.Active);
            Assert(artifact != null && spell != null && power != null,
                "UX-1 description checks need an artifact, a spell and a 권능 asset.");

            List<GameObject> created = new();
            GameObject player = new("UX-1 Player");
            created.Add(player);
            GameObject panelClone = Object.Instantiate(configured.gameObject, frame);
            created.Add(panelClone);
            try
            {
                PickupDescriptionView view = panelClone.GetComponent<PickupDescriptionView>();
                view.Configure(player.transform, frame, view.KindText, view.RarityText, view.NameText, view.EffectText,
                    view.DetailText, view.DetectionRadius);
                CanvasGroup group = view.GetComponent<CanvasGroup>();
                Vector3 origin = player.transform.position;

                ItemPickup near = Pickup<ItemPickup>(created, origin + new Vector3(1.0f, 0f));
                near.Configure(artifact);
                SingleUseItemPickup farther = Pickup<SingleUseItemPickup>(created, origin + new Vector3(-1.6f, 0f));
                farther.Configure(spell, "ux1-spell", false);
                ActivePowerPickup outside = Pickup<ActivePowerPickup>(created,
                    origin + new Vector3(0f, view.DetectionRadius + 1f));
                outside.Configure(power, "ux1-power", ActivePowerPickup.NoStoredCharge, false);
                foreach (IDescribedPickup pickup in new IDescribedPickup[] { near, farther, outside })
                    DescribedPickupRegistry.Register(pickup);

                view.RefreshNow();
                Assert(ReferenceEquals(view.Shown, near) && group.alpha > 0.99f,
                    "Only the nearest item inside the range may be described.");
                Assert(view.NameText.text == artifact.DisplayName &&
                       view.KindText.text == ItemKindText.GetDisplayName(artifact.Kind) &&
                       view.RarityText.text == ItemRewardCardView.RarityLabel(artifact.Rarity) &&
                       view.EffectText.text == ArtifactEffectDescription.Build(artifact) &&
                       !view.DetailText.gameObject.activeSelf,
                    "The description must show the artifact's kind, name, rarity text and effect.");
                Assert(view.NameText.color == PickupDescriptionView.RarityTextColor(artifact.Rarity),
                    "The item name colour must follow its rarity (with the rarity text beside it).");
                AssertInsideFrame(view, frame);

                Object.DestroyImmediate(near.gameObject);
                view.RefreshNow();
                Assert(ReferenceEquals(view.Shown, farther) && view.NameText.text == spell.DisplayName,
                    "When the described item is collected the next nearest one in range takes its place.");

                farther.MarkCollected();
                view.RefreshNow();
                Assert(view.Shown == null && group.alpha == 0f,
                    "No item in range must hide the description, even with one just outside it.");

                player.transform.position = outside.transform.position + new Vector3(0.5f, 0f);
                view.RefreshNow();
                Assert(ReferenceEquals(view.Shown, outside) && view.DetailText.gameObject.activeSelf &&
                       view.DetailText.text == GamePowerHudView.BuildOfferLine(power),
                    "A 권능's description must add how it charges.");

                player.transform.position = origin;
                view.RefreshNow();
                Assert(view.Shown == null && group.alpha == 0f, "Walking away must hide the description.");

                player.transform.position = outside.transform.position;
                float timeScale = Time.timeScale;
                Time.timeScale = 0f;
                try
                {
                    view.RefreshNow();
                    Assert(view.Shown == null, "The description must hide while the game is paused.");
                }
                finally
                {
                    Time.timeScale = timeScale;
                }
            }
            finally
            {
                foreach (GameObject target in created)
                    if (target != null) Object.DestroyImmediate(target);
            }
        }

        private static void AssertInsideFrame(PickupDescriptionView view, RectTransform frame)
        {
            if (Camera.main == null) return;
            RectTransform rect = (RectTransform)view.transform;
            Rect bounds = frame.rect;
            Vector2 position = rect.anchoredPosition;
            Vector2 size = rect.sizeDelta;
            Assert(size.y > 60f && position.x - size.x * 0.5f >= bounds.xMin && position.x + size.x * 0.5f <= bounds.xMax &&
                   position.y <= bounds.yMax && position.y - size.y >= bounds.yMin,
                "The description panel must stay inside the HUD frame.");
        }

        private static T Pickup<T>(List<GameObject> created, Vector3 position) where T : Component
        {
            GameObject owner = new("UX-1 " + typeof(T).Name, typeof(CircleCollider2D));
            owner.transform.position = position;
            created.Add(owner);
            return owner.AddComponent<T>();
        }

        private static ItemDefinition FindItem(Func<ItemDefinition, bool> predicate) =>
            AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null && item.IsValid)
                .OrderBy(item => item.ItemId, StringComparer.Ordinal)
                .FirstOrDefault(predicate);

        private static Vector2 CurrentMarkerPosition(GameMinimapView view) =>
            ((RectTransform)view.VisibleMarkers.Single(marker => marker.IsCurrent).transform).anchoredPosition;

        private static float GridDistance(GeneratedRoomNode a, GeneratedRoomNode b) =>
            new Vector2(b.GridPosition.X - a.GridPosition.X, b.GridPosition.Y - a.GridPosition.Y).magnitude;

        // CanvasScaler.ScaleWithScreenSize with MatchWidthOrHeight: a log-space blend of the width and height ratios.
        private static float CanvasScale(CanvasScaler scaler, Vector2 resolution)
        {
            float width = Mathf.Log(resolution.x / scaler.referenceResolution.x, 2f);
            float height = Mathf.Log(resolution.y / scaler.referenceResolution.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(width, height, scaler.matchWidthOrHeight));
        }

        // Panels are centre-anchored in the centred 1920 x 1080 frame.
        private static Rect ScreenRect(RectTransform panel, float scale, Vector2 resolution)
        {
            Vector2 centre = resolution * 0.5f + panel.anchoredPosition * scale;
            Vector2 size = panel.sizeDelta * scale;
            return new Rect(centre - size * 0.5f, size);
        }

        private static void VerifySurvivalSizes(RectTransform frame)
        {
            GameHudView view = frame.GetComponentInParent<GameHudView>();
            Assert(Mathf.Approximately(view.MaximumHeartSize, Week27Ux1Setup.HeartMaximumSize) &&
                   view.HeartTemplate.sizeDelta == Vector2.one * Week27Ux1Setup.HeartMaximumSize &&
                   view.HeartRowRoot.sizeDelta == Week27Ux1Setup.HeartRowSize &&
                   view.SpSlotTemplate.rectTransform.sizeDelta == Vector2.one * Week27Ux1Setup.SpSlotSize &&
                   view.SpSlotsRoot.sizeDelta == Week27Ux1Setup.SpSlotsSize,
                "Hearts and SP slots must use the larger UX-1 sizes.");
            Assert(Week27Ux1Setup.HeartMaximumSize > 36f && Week27Ux1Setup.SpSlotSize > 40f,
                "UX-1 hearts and SP must be larger than the HUD-1 sizes (36 and 40).");
        }

        // Cell 1 is at the bottom and each next cell is straight above it.
        private static void VerifyPowerGauge(RectTransform power)
        {
            GamePowerHudView view = power.GetComponent<GamePowerHudView>();
            Assert(view != null && view.GaugeCells.Count == Week27Power1Setup.MaximumGaugeCells,
                "The 권능 HUD must keep its gauge cells.");
            for (int index = 1; index < view.GaugeCells.Count; index++)
            {
                Vector2 below = view.GaugeCells[index - 1].rectTransform.anchoredPosition;
                Vector2 cell = view.GaugeCells[index].rectTransform.anchoredPosition;
                Assert(Mathf.Approximately(cell.x, below.x) && cell.y > below.y,
                    "The 권능 gauge must be vertical and fill from the bottom.");
            }

            RectTransform icon = view.Icon.rectTransform;
            RectTransform gauge = (RectTransform)view.GaugeCells[0].transform.parent;
            Assert(gauge.anchoredPosition.x + gauge.sizeDelta.x * 0.5f < icon.anchoredPosition.x - icon.sizeDelta.x * 0.5f,
                "The vertical gauge must stand left of the 권능 icon.");
        }

        private static float Left(RectTransform rect) => rect.anchoredPosition.x - rect.sizeDelta.x * 0.5f;
        private static float Right(RectTransform rect) => rect.anchoredPosition.x + rect.sizeDelta.x * 0.5f;

        private static float Top(RectTransform rect) => rect.anchoredPosition.y + rect.sizeDelta.y * 0.5f;
        private static float Bottom(RectTransform rect) => rect.anchoredPosition.y - rect.sizeDelta.y * 0.5f;

        private static RectTransform Panel(RectTransform frame, string name)
        {
            RectTransform panel = frame.Find(name) as RectTransform;
            Assert(panel != null, "HUD panel is missing: " + name);
            Assert(panel.anchorMin == new Vector2(0.5f, 0.5f) && panel.anchorMax == panel.anchorMin &&
                   panel.pivot == panel.anchorMin, name + " must be centre-anchored like the rest of the HUD.");
            return panel;
        }

        private static int CountTransforms(Scene scene) =>
            scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("UX-1 verification failed: " + message);
        }
    }
}
