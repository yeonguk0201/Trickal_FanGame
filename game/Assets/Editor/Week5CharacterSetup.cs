using TrickalFanGame.Character;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week5CharacterSetup
    {
        private const string RootName = "Week5 Character Selection";
        private const string CharacterFolder = "Assets/Characters";
        private const string CharacterAssetPath = CharacterFolder + "/erpin.asset";

        [MenuItem("Trickal Fan Game/Setup Week 5 Character Selection")]
        public static void Setup()
        {
            if (GameObject.Find(RootName) != null)
            {
                Debug.LogWarning("Week 5 character selection already exists.");
                return;
            }

            RunSession session = Object.FindFirstObjectByType<RunSession>();
            PlayerMovement movement = Object.FindFirstObjectByType<PlayerMovement>();
            if (session == null || movement == null)
            {
                Debug.LogError("The character selection setup requires RunSession and PlayerMovement in the scene.");
                return;
            }

            CharacterDefinition character = LoadOrCreateCharacter();
            if (character == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Week 5 Character Selection");

            GameObject root = new(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create character selection");
            CharacterSelectionUI selection = Undo.AddComponent<CharacterSelectionUI>(root);
            selection.Configure(
                session,
                new[] { character },
                movement,
                movement.GetComponent<PlayerProjectileAttack>());

            Undo.RecordObject(session, "Wait for character selection");
            session.SetWaitForCharacterSelection(true);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("Week 5 character selection ready. Choose Erpin to begin the Run.", root);
        }

        private static CharacterDefinition LoadOrCreateCharacter()
        {
            if (!AssetDatabase.IsValidFolder(CharacterFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Characters");
            }

            CharacterDefinition character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(CharacterAssetPath);
            if (character == null)
            {
                character = ScriptableObject.CreateInstance<CharacterDefinition>();
                AssetDatabase.CreateAsset(character, CharacterAssetPath);
            }

            SerializedObject serialized = new(character);
            serialized.FindProperty("characterId").stringValue = "erpin";
            serialized.FindProperty("displayName").stringValue = "에르핀";
            serialized.FindProperty("description").stringValue = "MVP 기본 플레이 캐릭터";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(character);
            AssetDatabase.SaveAssetIfDirty(character);
            return character;
        }
    }
}
