using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [CreateAssetMenu(fileName = "RoomDefinition", menuName = "Trickal Fan Game/Room Definition")]
    public sealed class RoomDefinition : ScriptableObject
    {
        [SerializeField] private string roomDefinitionId;
        [SerializeField] private RoomType roomType;
        [SerializeField] private GameObject[] encounterPrefabs = Array.Empty<GameObject>();

        public string RoomDefinitionId => roomDefinitionId;
        public RoomType RoomType => roomType;
        public IReadOnlyList<GameObject> EncounterPrefabs => encounterPrefabs;

        public void Configure(
            string configuredId,
            RoomType configuredType,
            GameObject[] configuredEncounterPrefabs)
        {
            roomDefinitionId = configuredId;
            roomType = configuredType;
            encounterPrefabs = configuredEncounterPrefabs ?? Array.Empty<GameObject>();
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(roomDefinitionId))
            {
                error = "Room definition ID is required.";
                return false;
            }

            for (int index = 0; index < roomDefinitionId.Length; index++)
            {
                char character = roomDefinitionId[index];
                if ((character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') &&
                    character != '-')
                {
                    error = $"Room definition ID '{roomDefinitionId}' must use lowercase ASCII letters, digits, or hyphens.";
                    return false;
                }
            }

            if (encounterPrefabs == null || encounterPrefabs.Length == 0)
            {
                error = $"Room definition '{roomDefinitionId}' needs at least one encounter prefab.";
                return false;
            }

            foreach (GameObject encounterPrefab in encounterPrefabs)
            {
                if (encounterPrefab == null || encounterPrefab.GetComponent<Health>() == null)
                {
                    error = $"Room definition '{roomDefinitionId}' has a missing encounter prefab or one without Health.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
