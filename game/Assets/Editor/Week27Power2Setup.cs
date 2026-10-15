using System;
using System.Linq;
using TMPro;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Power-2: gives the Game Scene room assembler the six 교주의 권능 as its 권능 offer pool (start-floor treasure
    // room, secret rooms and shops) and raises the Room content version, since floor 1's layout and the reward and
    // shop offers changed. Re-running only reassigns the same references.
    public static class Week27Power2Setup
    {
        public const int RoomContentVersion = 14;

        [MenuItem("Trickal Fan Game/Week 27/Setup Power-2 Power Offers")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Power-2 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ItemDefinition[] powers = LoadPowers();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            if (font == null) throw new InvalidOperationException("Power-2 requires the Frontend TMP font.");
            Week27Power1Setup.EnsureGlyphs(font, GamePowerHudView.OfferFixedText);
            AssetDatabase.SaveAssets();

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Power-2 Power Offers");

            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler[] assemblers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).ToArray();
            if (assemblers.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one RoomGraphAssembler; found {assemblers.Length}.");
            RoomGraphAssembler assembler = assemblers[0];
            FloorGenerator generator = assembler.Generator;
            if (generator == null) throw new InvalidOperationException("The room assembler needs its FloorGenerator.");

            Undo.RecordObject(assembler, "Configure Power-2 offers");
            assembler.ConfigurePowerRewards(powers);
            EditorUtility.SetDirty(assembler);
            Undo.RecordObject(generator, "Raise Power-2 Room content version");
            generator.ConfigureTemplates(Math.Max(RoomContentVersion, generator.RoomContentVersion),
                generator.RoomTemplates.ToArray());
            EditorUtility.SetDirty(generator);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Game Scene save failed during Power-2 setup.");
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Power-2 setup complete: {powers.Length} 교주의 권능 offered by the floor-1 treasure room, " +
                      $"secret rooms and shops; Room content version {generator.RoomContentVersion}.");
        }

        public static ItemDefinition[] LoadPowers() => Week27Power1Setup.Specs.Select(spec =>
        {
            ItemDefinition power = AssetDatabase.LoadAssetAtPath<ItemDefinition>(spec.AssetPath);
            if (power == null || !power.IsValid || !power.IsActivePower)
                throw new InvalidOperationException($"Run Power-1 setup first: {spec.AssetPath} is missing or invalid.");
            return power;
        }).ToArray();
    }
}
