using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [Serializable]
    public struct EncounterEnemyPrefabBinding
    {
        [SerializeField] private EncounterEnemyRole role;
        [SerializeField] private GameObject prefab;

        public EncounterEnemyPrefabBinding(EncounterEnemyRole configuredRole, GameObject configuredPrefab)
        {
            role = configuredRole;
            prefab = configuredPrefab;
        }

        public EncounterEnemyRole Role => role;
        public GameObject Prefab => prefab;
    }

    [CreateAssetMenu(fileName = "EnemyRoster", menuName = "Trickal Fan Game/Encounter Enemy Roster")]
    public sealed class EncounterEnemyRoster : ScriptableObject
    {
        [SerializeField] private EncounterEnemyPrefabBinding[] bindings =
            Array.Empty<EncounterEnemyPrefabBinding>();

        public IReadOnlyList<EncounterEnemyPrefabBinding> Bindings => bindings;

        public void Configure(EncounterEnemyPrefabBinding[] configuredBindings)
        {
            bindings = configuredBindings ?? Array.Empty<EncounterEnemyPrefabBinding>();
        }

        public bool TryResolve(EncounterEnemyRole role, out GameObject prefab, out string error)
        {
            prefab = null;
            if (!TryValidate(out error)) return false;
            foreach (EncounterEnemyPrefabBinding binding in bindings)
            {
                if (binding.Role != role) continue;
                prefab = binding.Prefab;
                error = null;
                return true;
            }

            error = $"Enemy roster has no Prefab for role '{role}'.";
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (bindings == null || bindings.Length == 0)
            { error = "Enemy roster requires at least one role binding."; return false; }
            HashSet<EncounterEnemyRole> roles = new();
            foreach (EncounterEnemyPrefabBinding binding in bindings)
            {
                if (!roles.Add(binding.Role))
                { error = $"Enemy roster duplicates role '{binding.Role}'."; return false; }
                if (binding.Prefab == null || binding.Prefab.GetComponent<Health>() == null)
                { error = $"Enemy roster role '{binding.Role}' requires a Prefab with Health."; return false; }
                bool matchesRole = binding.Role switch
                {
                    EncounterEnemyRole.Chaser => binding.Prefab.GetComponent<EnemyChase>() != null,
                    EncounterEnemyRole.Ranged => binding.Prefab.GetComponent<RangedEnemyController>() != null,
                    EncounterEnemyRole.Charging => binding.Prefab.GetComponent<ChargingEnemyController>() != null,
                    EncounterEnemyRole.Boss => true,
                    _ => false,
                };
                if (!matchesRole)
                { error = $"Enemy roster Prefab '{binding.Prefab.name}' does not implement role '{binding.Role}'."; return false; }
            }
            error = null;
            return true;
        }
    }
}
