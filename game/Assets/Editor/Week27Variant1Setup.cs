using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Variant-1 (docs/28 §8·§9): champions. Each eligible enemy that is not the room's elite rolls a champion from
    // its room content seed (floor 2: 10%, floor 3: 15%) and takes one death property with its outline colour. The
    // runtime rules live in EnemyVariant, EnemyVariantRules and the EnemyChampion* components, and the outline
    // shader is loaded from Resources; this setup only raises the Encounter content version, because the same seed
    // now spawns champions on floors 2~3. Re-running keeps the version.
    public static class Week27Variant1Setup
    {
        public const int EncounterContentVersion = 22;
        public const string OutlineShaderPath = "Assets/Resources/CombatEffects/ChampionOutline.shader";

        [MenuItem("Trickal Fan Game/Week 27/Setup Variant-1 Champion Enemies")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Variant-1 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null || generator.DifficultyTable == null ||
                assembler.EliteKillDropTable == null ||
                generator.EncounterContentVersion < Week27Variant0Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Variant-1 requires the completed Variant-0 Game Scene.");
            }

            if (AssetDatabase.LoadAssetAtPath<Shader>(OutlineShaderPath) == null)
                throw new InvalidOperationException($"Variant-1 needs the champion outline shader at {OutlineShaderPath}.");

            if (generator.EncounterContentVersion < EncounterContentVersion)
            {
                Undo.RecordObject(generator, "Configure Variant-1 champions");
                generator.ConfigureEncounters(EncounterContentVersion, generator.EncounterDefinitions.ToArray());
                EditorUtility.SetDirty(generator);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                    throw new InvalidOperationException("Game Scene save failed during Variant-1 setup.");
            }

            Debug.Log("Variant-1 ready: champions at " +
                      $"{EnemyVariantRules.ChampionPercent(1)}/{EnemyVariantRules.ChampionPercent(2)}/" +
                      $"{EnemyVariantRules.ChampionPercent(3)}% of eligible enemies on floors 1/2/3, Encounter " +
                      $"content version {generator.EncounterContentVersion}.");
        }
    }
}
