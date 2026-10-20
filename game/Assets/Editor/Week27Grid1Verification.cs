using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week27Grid1Verification
    {
        private const float Tolerance = 0.001f;

        [MenuItem("Trickal Fan Game/Week 27/Setup and Verify Grid-1 Room Profile Walkable Bounds")]
        public static void SetupAndVerifyBatch()
        {
            Week27Grid1Setup.Setup();
            Verify();
        }

        // Every room template, the Layout importer, chests and boss arenas read the profile bounds.
        [MenuItem("Trickal Fan Game/Week 27/Verify Grid-1 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Action[] checks =
            {
                Verify, Week14Room0RegressionVerification.Verify, Week14Room1Verification.Verify,
                Week14Room3Verification.Verify, Week14Room5Verification.Verify, Week14Room6Verification.Verify,
                Week14Room7Verification.Verify, Week15Boss0Verification.Verify, Week15Boss1Verification.Verify,
                Week15Boss2Verification.Verify, Week15Boss3Verification.Verify, Week18Obstacle2Verification.Verify,
                Week25Trap1Verification.Verify,
            };
            foreach (Action check in checks)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                check();
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RoomLayoutImporterVerification.VerifyWithRegressionsBatch();
            Debug.Log("Grid-1 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Grid-1 Room Profile Walkable Bounds")]
        public static void Verify()
        {
            ValidateProfiles();
            ValidateLayoutGrid();
            ValidateTemplates();
            ValidatePassageRule();
            Debug.Log("Grid-1 verification passed: every Room Profile moves on the walkable floor, every room " +
                      "Prefab Encounter trigger matches it, and every template leaves no gap narrower than one cell.");
        }

        private static void ValidateProfiles()
        {
            List<RoomProfile> profiles = Week27Grid1Setup.Profiles().ToList();
            Assert(profiles.Count >= 8, $"Grid-1 expected at least 8 Room Profiles, found {profiles.Count}.");
            foreach (RoomProfile profile in profiles)
            {
                Rect floor = FairyVillageArtworkSetup.WalkableFloor(profile.InteriorSize);
                Assert(Same(profile.MovementBounds, floor),
                    $"Room Profile '{profile.ProfileId}' movement bounds {profile.MovementBounds} are not the walkable " +
                    $"floor {floor}. Run Grid-1 setup.");
                Assert(Same(profile.EncounterBounds, floor),
                    $"Room Profile '{profile.ProfileId}' Encounter bounds are not the walkable floor.");
                Assert(IsWhole(floor.width) && IsWhole(floor.height),
                    $"Room Profile '{profile.ProfileId}' walkable floor {floor.size} is not a whole number of cells.");
                Assert(profile.TryValidate(out string error), error);
            }
        }

        // The room text grid, the importer and the profile must describe the same floor.
        private static void ValidateLayoutGrid()
        {
            foreach (RoomLayoutProfile layout in RoomLayoutFormat.Profiles)
            {
                RoomProfile profile = AssetDatabase.LoadAssetAtPath<RoomProfile>(layout.ProfilePath);
                Assert(profile != null, $"Room Layout profile '{layout.ProfileId}' has no asset.");
                Assert(Same(layout.GridRect, profile.MovementBounds) && Same(layout.Floor, profile.MovementBounds),
                    $"Room Layout grid {layout.Columns}x{layout.Rows} of '{layout.ProfileId}' does not match its " +
                    $"profile movement bounds {profile.MovementBounds}.");
            }
        }

        // Reports every failing template at once so a Layout fix list can be made from one run.
        private static void ValidateTemplates()
        {
            List<RoomTemplateDefinition> templates = Week27Grid1Setup.Templates().ToList();
            Assert(templates.Count >= 30, $"Grid-1 expected every room template, found {templates.Count}.");
            List<string> failures = new();
            foreach (RoomTemplateDefinition template in templates)
            {
                if (!template.TryValidate(out string error)) failures.Add(error);
                else if (!template.TryValidateLayout(out error)) failures.Add(error);
            }

            foreach (string failure in failures) Debug.LogError($"Grid-1 template failure: {failure}");
            Assert(failures.Count == 0,
                $"Grid-1: {failures.Count} of {templates.Count} room templates fail on the walkable floor. " +
                "See the logged template failures.");
        }

        // Probes sit in the lower-left quarter of a Large floor, away from every door passage.
        private static void ValidatePassageRule()
        {
            Assert(Passes(Box("touching-wall", -10f, -4f)), "An obstacle against the wall must be allowed.");
            Assert(Passes(Box("one-cell-from-wall", -9f, -4f)),
                "A one-cell lane between the wall and an obstacle must be allowed.");
            Assert(!Passes(Box("half-cell-from-wall", -9.5f, -4f)),
                "A half-cell gap between the wall and an obstacle must fail.");
            Assert(!Passes(Box("half-cell-from-top", -6f, 4.5f)),
                "A half-cell gap between the top wall and an obstacle must fail.");
            Assert(Passes(Box("first", -7f, -4f), Box("side-by-side", -6f, -4f)),
                "Obstacles side by side must be allowed.");
            Assert(Passes(Box("first", -7f, -4f), Box("one-cell-apart", -5f, -4f)),
                "A one-cell lane between obstacles must be allowed.");
            Assert(Passes(Box("first", -7f, -4f), Box("corner-touching", -6f, -3f)),
                "Obstacles meeting at a corner must be allowed.");
            Assert(!Passes(Box("first", -7f, -4f), Box("half-cell-apart", -5.5f, -4f)),
                "A half-cell gap between obstacles must fail.");
            Assert(!Passes(Box("first", -7f, -4f), Box("half-cell-diagonal", -5.5f, -2.5f)),
                "A diagonal gap narrower than one cell must fail.");
            Assert(!Passes(Box("first", -7f, -4f),
                    new RoomObstacleFootprint("pit", Rect.MinMaxRect(-6f, -4.5f, -5f, -3.5f), false, true)),
                "A half-cell gap between an obstacle and a pit must fail.");
            Vector2 chest = Vector2.one * ChestPlacement.ChestWorldSize;
            Assert(Passes(Box("first", -7f, -4f), new RoomObstacleFootprint("chest-diagonal",
                    new Rect(new Vector2(-6f, -3f) - chest * 0.5f, chest), false)),
                "A chest smaller than its cell, diagonal to an obstacle, must not count as a narrow gap.");
        }

        private static bool Passes(params RoomObstacleFootprint[] footprints)
        {
            Vector2 size = Week14Room6Setup.LargeSize;
            Rect floor = Week14Room3Setup.MovementBounds(size);
            bool passed = RoomObstacleLayout.TryValidate("grid-1-probe", floor, Week14Room3Setup.EncounterBounds(size),
                Week14Room3Setup.BuildDoorContracts(size), Array.Empty<Vector2>(), footprints, out string error);
            Assert(passed || error.Contains("-unit gap"), $"Grid-1 probe failed for an unrelated reason: {error}");
            return passed;
        }

        private static RoomObstacleFootprint Box(string id, float x, float y) =>
            new(id, new Rect(x - 0.5f, y - 0.5f, 1f, 1f), true);

        private static bool IsWhole(float value) => Mathf.Abs(value - Mathf.Round(value)) < Tolerance;

        private static bool Same(Rect a, Rect b) =>
            Mathf.Abs(a.xMin - b.xMin) < Tolerance && Mathf.Abs(a.yMin - b.yMin) < Tolerance &&
            Mathf.Abs(a.xMax - b.xMax) < Tolerance && Mathf.Abs(a.yMax - b.yMax) < Tolerance;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
