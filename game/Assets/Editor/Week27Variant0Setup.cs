using System;
using System.Linq;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Variant-0 (docs/28 §8·§9): the variant system and its first kind, the elite. Each combat room rolls one elite
    // from its content seed after its Encounter is chosen (floor 3: 20%, floors 1~2: none). The runtime rules live in
    // EnemyVariant and EnemyVariantRules; this setup only makes the elite kill drop table, binds it to the Game Scene
    // and raises the Encounter content version, because the same seed now spawns an elite on floor 3.
    // Re-running updates the same table asset and keeps its GUID.
    public static class Week27Variant0Setup
    {
        public const int EncounterContentVersion = 21;
        public const string EliteDropTablePath = Week17Resource3Setup.DropTableFolder + "/elite-kill-drop-table.asset";

        // Placeholder until Drop-0: the basic obstacle candidates without the secret pit, always one pickup.
        public static readonly (string DropId, int Weight)[] EliteDropWeights =
        {
            ("heart", 25),
            ("sp", 25),
            ("key", 10),
            ("bomb", 8),
            ("elif", 30),
        };

        [MenuItem("Trickal Fan Game/Week 27/Setup Variant-0 Elite Enemies")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Variant-0 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Week27Enemy13Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Variant-0 requires the completed Enemy-13 Game Scene.");
            }

            ResourceDropTable table = Week17Resource3Setup.EnsureTable(EliteDropTablePath, 1f,
                EliteDropWeights.Select(entry => (entry.DropId, entry.Weight)).ToArray(),
                "Configure Variant-0 elite drop table");
            if (!table.TryValidate(out string error))
                throw new InvalidOperationException($"Variant-0 elite drop table is invalid. {error}");

            Undo.RecordObjects(new Object[] { generator, assembler }, "Configure Variant-0 elites");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                generator.EncounterDefinitions.ToArray());
            assembler.ConfigureEliteKillDrop(table);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Variant-0 setup.");

            AssetDatabase.SaveAssets();
            Debug.Log("Variant-0 ready: one elite per combat room at " +
                      $"{EnemyVariantRules.ElitePercent(1)}/{EnemyVariantRules.ElitePercent(2)}/" +
                      $"{EnemyVariantRules.ElitePercent(3)}% on floors 1/2/3, elite kill drop table " +
                      $"{EliteDropTablePath}, Encounter content version {generator.EncounterContentVersion}.");
        }
    }
}
