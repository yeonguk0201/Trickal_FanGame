using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Character
{
    public sealed class CharacterSelectionUI : MonoBehaviour
    {
        [SerializeField] private RunSession runSession;
        [SerializeField] private CharacterDefinition[] characters = System.Array.Empty<CharacterDefinition>();
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerProjectileAttack playerAttack;

        private float previousTimeScale = 1f;
        private bool isSelecting = true;
        private bool selectionPending;

        public void Configure(
            RunSession configuredRunSession,
            CharacterDefinition[] configuredCharacters,
            PlayerMovement configuredMovement,
            PlayerProjectileAttack configuredAttack)
        {
            runSession = configuredRunSession;
            characters = configuredCharacters ?? System.Array.Empty<CharacterDefinition>();
            playerMovement = configuredMovement;
            playerAttack = configuredAttack;
        }

        private void Awake()
        {
            if (runSession == null)
            {
                runSession = FindFirstObjectByType<RunSession>();
            }

            if (playerMovement == null)
            {
                playerMovement = FindFirstObjectByType<PlayerMovement>();
            }

            if (playerMovement != null && playerAttack == null)
            {
                playerAttack = playerMovement.GetComponent<PlayerProjectileAttack>();
            }

            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            SetPlayerInputEnabled(false);
        }

        private void OnDestroy()
        {
            if (isSelecting)
            {
                Time.timeScale = previousTimeScale;
                SetPlayerInputEnabled(true);
            }
        }

        private void OnGUI()
        {
            if (!isSelecting)
            {
                return;
            }

            const float width = 420f;
            float height = 145f + characters.Length * 72f;
            Rect area = new((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label("Select Character", GUI.skin.box);
            GUILayout.Space(8f);

            foreach (CharacterDefinition character in characters)
            {
                if (character == null || !character.IsValid)
                {
                    continue;
                }

                GUI.enabled = !selectionPending;
                if (GUILayout.Button($"{character.DisplayName}\n{character.Description}", GUILayout.Height(62f)))
                {
                    Select(character);
                }
                GUI.enabled = true;
            }

            GUILayout.EndArea();
        }

        private void Select(CharacterDefinition character)
        {
            if (runSession == null || selectionPending)
            {
                Debug.LogError("[CharacterSelectionUI] Failed to start the Run with the selected character.", this);
                return;
            }

            selectionPending = true;
            if (!runSession.PrepareAndBeginRun(
                character.CharacterId,
                started => CompleteSelection(character, started)))
            {
                selectionPending = false;
            }
        }

        private void CompleteSelection(CharacterDefinition character, bool started)
        {
            selectionPending = false;
            if (!started)
            {
                Debug.LogError("[CharacterSelectionUI] Failed to start the Run with the selected character.", this);
                return;
            }

            isSelecting = false;
            Time.timeScale = previousTimeScale;
            SetPlayerInputEnabled(true);
            Debug.Log($"[CharacterSelectionUI] Selected {character.DisplayName} ({character.CharacterId}).", this);
        }

        private void SetPlayerInputEnabled(bool enabled)
        {
            if (playerMovement != null)
            {
                playerMovement.enabled = enabled;
            }

            if (playerAttack != null)
            {
                playerAttack.enabled = enabled;
            }
        }
    }
}
