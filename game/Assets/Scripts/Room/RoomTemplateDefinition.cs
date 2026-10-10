using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [Flags]
    public enum SpawnPointPlacementRole
    {
        None = 0,
        MeleePressure = 1 << 0,
        RearFiring = 1 << 1,
        ChargeLane = 1 << 2,
        AllCombat = MeleePressure | RearFiring | ChargeLane,
    }

    [Serializable]
    public struct RoomTemplateDoor
    {
        [SerializeField] private RoomDoorDirection direction;
        [SerializeField] private Vector2 slotPosition;
        [SerializeField] private Vector2 safeEntryPosition;

        public RoomTemplateDoor(
            RoomDoorDirection configuredDirection,
            Vector2 configuredSlotPosition,
            Vector2 configuredSafeEntryPosition)
        {
            direction = configuredDirection;
            slotPosition = configuredSlotPosition;
            safeEntryPosition = configuredSafeEntryPosition;
        }

        public RoomDoorDirection Direction => direction;
        public Vector2 SlotPosition => slotPosition;
        public Vector2 SafeEntryPosition => safeEntryPosition;
    }

    // A chest authored into a Layout (T7). It stands in the room from the first build instead of rolling on clear.
    [Serializable]
    public struct RoomTemplateChest
    {
        [SerializeField] private string chestId;
        [SerializeField] private ChestKind kind;
        [SerializeField] private Vector2 localPosition;

        public RoomTemplateChest(string configuredChestId, ChestKind configuredKind, Vector2 configuredLocalPosition)
        {
            chestId = configuredChestId;
            kind = configuredKind;
            localPosition = configuredLocalPosition;
        }

        public string ChestId => chestId;
        public ChestKind Kind => kind;
        public Vector2 LocalPosition => localPosition;
    }

    [CreateAssetMenu(fileName = "RoomTemplate", menuName = "Trickal Fan Game/Room Template")]
    public sealed class RoomTemplateDefinition : ScriptableObject
    {
        [SerializeField] private string templateId;
        [SerializeField] private RoomProfile profile;
        [SerializeField] private GameObject roomPrefab;
        [SerializeField] private RoomType[] allowedRoomTypes = Array.Empty<RoomType>();
        [SerializeField, Min(1)] private int minimumFloor = 1;
        [SerializeField, Min(1)] private int maximumFloor = 99;
        [SerializeField] private RoomTemplateDoor[] doorSlots = Array.Empty<RoomTemplateDoor>();
        [SerializeField] private Vector2[] spawnPoints = Array.Empty<Vector2>();
        [SerializeField] private SpawnPointPlacementRole[] spawnPointRoles =
            Array.Empty<SpawnPointPlacementRole>();
        // Added to the resolved Encounter threat to form the room difficulty score (obstacles, later traps).
        [SerializeField] private int layoutDifficultyModifier;
        // Relative chance among the compatible candidates of a room. Rooms that give more than an ordinary room
        // (authored chests, many resource obstacles) use a lower weight.
        [SerializeField, Min(1)] private int selectionWeight = DefaultSelectionWeight;
        [SerializeField] private RoomTemplateChest[] authoredChests = Array.Empty<RoomTemplateChest>();

        public const int DefaultSelectionWeight = 100;

        public string TemplateId => templateId;
        public RoomProfile Profile => profile;
        public GameObject RoomPrefabAsset => roomPrefab;
        public IReadOnlyList<RoomType> AllowedRoomTypes => allowedRoomTypes;
        public int MinimumFloor => minimumFloor;
        public int MaximumFloor => maximumFloor;
        public IReadOnlyList<RoomTemplateDoor> DoorSlots => doorSlots;
        public IReadOnlyList<Vector2> SpawnPoints => spawnPoints;
        public IReadOnlyList<SpawnPointPlacementRole> SpawnPointRoles => spawnPointRoles;
        public int LayoutDifficultyModifier => layoutDifficultyModifier;
        public int SelectionWeight => Mathf.Max(1, selectionWeight);
        public IReadOnlyList<RoomTemplateChest> AuthoredChests => authoredChests ?? Array.Empty<RoomTemplateChest>();

        public void ConfigureLayoutDifficultyModifier(int modifier)
        {
            layoutDifficultyModifier = modifier;
        }

        public void ConfigureSelectionWeight(int weight)
        {
            selectionWeight = Mathf.Max(1, weight);
        }

        public void ConfigureAuthoredChests(RoomTemplateChest[] chests)
        {
            authoredChests = chests != null ? (RoomTemplateChest[])chests.Clone() : Array.Empty<RoomTemplateChest>();
        }

        public static string SpawnPointId(int index) => $"spawn-{index + 1:00}";

        public bool SupportsEnemyRole(int spawnPointIndex, EncounterEnemyRole enemyRole)
        {
            if (spawnPointRoles == null || spawnPointIndex < 0 || spawnPointIndex >= spawnPointRoles.Length)
            {
                return false;
            }

            SpawnPointPlacementRole required = RequiredPlacementRole(enemyRole);
            return required == SpawnPointPlacementRole.None ||
                   (spawnPointRoles[spawnPointIndex] & required) == required;
        }

        public static SpawnPointPlacementRole RequiredPlacementRole(EncounterEnemyRole enemyRole)
        {
            return enemyRole switch
            {
                EncounterEnemyRole.Chaser => SpawnPointPlacementRole.MeleePressure,
                EncounterEnemyRole.FastChaser => SpawnPointPlacementRole.MeleePressure,
                EncounterEnemyRole.Ranged => SpawnPointPlacementRole.RearFiring,
                EncounterEnemyRole.Sniper => SpawnPointPlacementRole.RearFiring,
                EncounterEnemyRole.Charging => SpawnPointPlacementRole.ChargeLane,
                // Bosses use dedicated room templates and are not selected through the normal Spawn-1 contract.
                EncounterEnemyRole.Boss => SpawnPointPlacementRole.None,
                _ => SpawnPointPlacementRole.None,
            };
        }

        public bool TryResolveSpawnReference(
            string spawnPointId,
            string spawnGroupId,
            int count,
            out int[] indices,
            out string error)
        {
            indices = Array.Empty<int>();
            if (count < 1)
            {
                error = "A spawn reference requires a positive count.";
                return false;
            }

            bool usesPoint = !string.IsNullOrWhiteSpace(spawnPointId);
            bool usesGroup = !string.IsNullOrWhiteSpace(spawnGroupId);
            if (usesPoint == usesGroup)
            {
                error = "A spawn reference must use exactly one SpawnPoint ID or SpawnGroup ID.";
                return false;
            }

            if (usesPoint)
            {
                if (count != 1)
                {
                    error = $"SpawnPoint '{spawnPointId}' can host exactly one enemy per wave.";
                    return false;
                }

                for (int index = 0; index < spawnPoints.Length; index++)
                {
                    if (string.Equals(spawnPointId, SpawnPointId(index), StringComparison.Ordinal))
                    {
                        indices = new[] { index };
                        error = null;
                        return true;
                    }
                }

                error = $"Room template '{templateId}' has no SpawnPoint ID '{spawnPointId}'.";
                return false;
            }

            if (!string.Equals(spawnGroupId, "all", StringComparison.Ordinal))
            {
                error = $"Room template '{templateId}' has no SpawnGroup ID '{spawnGroupId}'.";
                return false;
            }

            if (count > spawnPoints.Length)
            {
                error = $"SpawnGroup 'all' has {spawnPoints.Length} points but requires {count}.";
                return false;
            }

            indices = new int[count];
            for (int index = 0; index < count; index++) indices[index] = index;
            error = null;
            return true;
        }

        public bool SupportsRoomType(RoomType roomType)
        {
            if (allowedRoomTypes == null)
            {
                return false;
            }

            foreach (RoomType allowedRoomType in allowedRoomTypes)
            {
                if (allowedRoomType == roomType)
                {
                    return true;
                }
            }

            return false;
        }

        public bool SupportsFloor(int floorNumber) =>
            floorNumber >= minimumFloor && floorNumber <= maximumFloor;

        public bool SupportsConnections(IReadOnlyList<GeneratedRoomConnection> connections)
        {
            if (connections == null || doorSlots == null)
            {
                return false;
            }

            foreach (GeneratedRoomConnection connection in connections)
            {
                bool found = false;
                foreach (RoomTemplateDoor door in doorSlots)
                {
                    if (door.Direction == connection.Direction)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        public void Configure(
            string configuredTemplateId,
            RoomProfile configuredProfile,
            GameObject configuredRoomPrefab,
            RoomType[] configuredAllowedRoomTypes,
            RoomTemplateDoor[] configuredDoorSlots,
            Vector2[] configuredSpawnPoints,
            int configuredMinimumFloor = 1,
            int configuredMaximumFloor = 99,
            SpawnPointPlacementRole[] configuredSpawnPointRoles = null)
        {
            templateId = configuredTemplateId;
            profile = configuredProfile;
            roomPrefab = configuredRoomPrefab;
            allowedRoomTypes = configuredAllowedRoomTypes ?? Array.Empty<RoomType>();
            minimumFloor = configuredMinimumFloor;
            maximumFloor = configuredMaximumFloor;
            doorSlots = configuredDoorSlots ?? Array.Empty<RoomTemplateDoor>();
            spawnPoints = configuredSpawnPoints ?? Array.Empty<Vector2>();
            spawnPointRoles = configuredSpawnPointRoles != null
                ? (SpawnPointPlacementRole[])configuredSpawnPointRoles.Clone()
                : BuildDefaultSpawnPointRoles(spawnPoints);
        }

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(templateId, "Room template", out error))
            {
                return false;
            }

            if (profile == null)
            {
                error = $"Room template '{templateId}' has a missing profile.";
                return false;
            }

            if (!profile.TryValidate(out error))
            {
                error = $"Room template '{templateId}' has an invalid profile. {error}";
                return false;
            }

            RoomPrefab prefab = roomPrefab != null ? roomPrefab.GetComponent<RoomPrefab>() : null;
            if (prefab == null)
            {
                error = $"Room template '{templateId}' has a missing RoomPrefab.";
                return false;
            }

            if (!prefab.TryValidate(out error))
            {
                error = $"Room template '{templateId}' has an invalid RoomPrefab. {error}";
                return false;
            }

            if (allowedRoomTypes == null || allowedRoomTypes.Length == 0)
            {
                error = $"Room template '{templateId}' needs at least one allowed RoomType.";
                return false;
            }

            if (minimumFloor < 1 || maximumFloor < minimumFloor)
            {
                error = $"Room template '{templateId}' has an invalid floor range.";
                return false;
            }

            HashSet<RoomType> uniqueTypes = new();
            foreach (RoomType roomType in allowedRoomTypes)
            {
                if (!uniqueTypes.Add(roomType))
                {
                    error = $"Room template '{templateId}' duplicates allowed RoomType {roomType}.";
                    return false;
                }
            }

            if (doorSlots == null || doorSlots.Length != 4)
            {
                error = $"Room template '{templateId}' requires exactly four directional door slots.";
                return false;
            }

            HashSet<RoomDoorDirection> uniqueDirections = new();
            foreach (RoomTemplateDoor door in doorSlots)
            {
                if (!uniqueDirections.Add(door.Direction))
                {
                    error = $"Room template '{templateId}' duplicates its {door.Direction} door contract.";
                    return false;
                }

                RoomDoorSlot prefabSlot = prefab.FindSlot(door.Direction);
                if (prefabSlot == null ||
                    !Approximately(ToLocal(prefab.transform, prefabSlot.transform), door.SlotPosition) ||
                    !Approximately(ToLocal(prefab.transform, prefabSlot.EntryPoint), door.SafeEntryPosition) ||
                    !profile.MovementBounds.Contains(door.SafeEntryPosition))
                {
                    error = $"Room template '{templateId}' {door.Direction} door or safe entry does not match its Prefab/profile.";
                    return false;
                }
            }

            if (spawnPoints == null || spawnPoints.Length == 0 ||
                spawnPoints.Length != prefab.Controller.SpawnPoints.Count)
            {
                error = $"Room template '{templateId}' SpawnPoint count does not match its Prefab.";
                return false;
            }

            HashSet<Vector2> uniqueSpawns = new();
            if (spawnPointRoles == null || spawnPointRoles.Length != spawnPoints.Length)
            {
                error = $"Room template '{templateId}' SpawnPoint role count does not match its SpawnPoints.";
                return false;
            }

            SpawnPointPlacementRole declaredRoles = SpawnPointPlacementRole.None;
            for (int index = 0; index < spawnPoints.Length; index++)
            {
                Vector2 point = spawnPoints[index];
                SpawnPointPlacementRole placementRole = spawnPointRoles[index];
                if (placementRole == SpawnPointPlacementRole.None ||
                    (placementRole & ~SpawnPointPlacementRole.AllCombat) != 0)
                {
                    error = $"Room template '{templateId}' SpawnPoint {index + 1} has an invalid placement role.";
                    return false;
                }

                declaredRoles |= placementRole;
                if (!uniqueSpawns.Add(point) || !profile.EncounterBounds.Contains(point) ||
                    !Approximately(ToLocal(prefab.transform, prefab.Controller.SpawnPoints[index]), point))
                {
                    error = $"Room template '{templateId}' has an invalid or mismatched SpawnPoint {index + 1}.";
                    return false;
                }

                foreach (RoomTemplateDoor door in doorSlots)
                {
                    if (RoomTemplateGeometry.IsInsideRequiredDoorPassage(door, point))
                    {
                        error = $"Room template '{templateId}' SpawnPoint {index + 1} overlaps the " +
                                $"{door.Direction} required door passage.";
                        return false;
                    }
                }
            }

            if (SupportsRoomType(RoomType.Normal) &&
                (declaredRoles & SpawnPointPlacementRole.AllCombat) != SpawnPointPlacementRole.AllCombat)
            {
                error = $"Room template '{templateId}' needs melee-pressure, rear-firing, and charge-lane SpawnPoints.";
                return false;
            }

            if (selectionWeight < 1)
            {
                error = $"Room template '{templateId}' needs a positive selection weight.";
                return false;
            }

            HashSet<string> chestIds = new(StringComparer.Ordinal);
            foreach (RoomTemplateChest chest in AuthoredChests)
            {
                if (!StableRoomId.TryValidate(chest.ChestId, "Authored chest", out error))
                {
                    error = $"Room template '{templateId}' has an invalid authored chest. {error}";
                    return false;
                }

                if (!chestIds.Add(chest.ChestId) || !Enum.IsDefined(typeof(ChestKind), chest.Kind) ||
                    !profile.MovementBounds.Contains(chest.LocalPosition))
                {
                    error = $"Room template '{templateId}' authored chest '{chest.ChestId}' is duplicated, has an " +
                            "undefined kind, or sits outside the movement bounds.";
                    return false;
                }
            }

            BoxCollider2D encounter = prefab.Controller.GetComponent<BoxCollider2D>();
            Vector2 encounterCenter = encounter != null
                ? ToLocal(prefab.transform, encounter.transform.TransformPoint(encounter.offset))
                : Vector2.zero;
            Vector2 encounterSize = encounter != null
                ? Vector2.Scale(encounter.size, encounter.transform.lossyScale)
                : Vector2.zero;
            if (encounter == null || !Approximately(encounterCenter, profile.EncounterBounds.center) ||
                !Approximately(encounterSize, profile.EncounterBounds.size) ||
                !Approximately(ToLocal(prefab.transform, prefab.Node.CameraAnchor), profile.CameraBounds.center))
            {
                error = $"Room template '{templateId}' Encounter or camera anchor does not match its profile.";
                return false;
            }

            error = null;
            return true;
        }

        // Obstacle Layout checks run once per catalog validation, not per Encounter resolution, because they
        // flood-fill the room and sample sightlines.
        public bool TryValidateLayout(out string error)
        {
            if (profile == null || roomPrefab == null)
            {
                error = $"Room template '{templateId}' needs a profile and Prefab before its Layout can be validated.";
                return false;
            }

            if (!RoomObstacleLayout.TryCollectFootprints(roomPrefab, out List<RoomObstacleFootprint> footprints,
                    out error) ||
                !RoomObstacleLayout.TryValidate(templateId, profile.MovementBounds, profile.EncounterBounds,
                    doorSlots, spawnPoints, footprints, out error))
            {
                error = $"Room template '{templateId}' has an invalid obstacle Layout. {error}";
                return false;
            }

            return true;
        }

        private static Vector2 ToLocal(Transform root, Transform child) => root.InverseTransformPoint(child.position);

        private static Vector2 ToLocal(Transform root, Vector3 worldPosition) =>
            root.InverseTransformPoint(worldPosition);

        private static bool Approximately(Vector2 first, Vector2 second) =>
            (first - second).sqrMagnitude < 0.0001f;

        public static SpawnPointPlacementRole[] BuildDefaultSpawnPointRoles(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
            {
                return Array.Empty<SpawnPointPlacementRole>();
            }

            SpawnPointPlacementRole[] roles = new SpawnPointPlacementRole[points.Count];
            for (int index = 0; index < points.Count; index++)
            {
                roles[index] = SpawnPointPlacementRole.RearFiring;
            }

            // Existing authored templates use three ordered points. The first two form the pressure pair used by
            // two-melee waves, while the last point is the charge lane. Every point remains a valid ranged anchor,
            // preserving the established three-enemy crossfire Encounter.
            int pressureCount = Math.Min(2, points.Count);
            for (int index = 0; index < pressureCount; index++)
            {
                roles[index] |= SpawnPointPlacementRole.MeleePressure;
            }

            roles[points.Count - 1] |= SpawnPointPlacementRole.ChargeLane;
            return roles;
        }
    }

    public static class RoomTemplateGeometry
    {
        public static Rect RequiredDoorPassageBounds(RoomTemplateDoor door)
        {
            float halfWidth = RoomLayout.DoorOpeningLength * 0.5f;
            Vector2 minimum = Vector2.Min(door.SlotPosition, door.SafeEntryPosition);
            Vector2 maximum = Vector2.Max(door.SlotPosition, door.SafeEntryPosition);
            if (door.Direction == RoomDoorDirection.Left || door.Direction == RoomDoorDirection.Right)
            {
                minimum.y -= halfWidth;
                maximum.y += halfWidth;
            }
            else
            {
                minimum.x -= halfWidth;
                maximum.x += halfWidth;
            }

            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        public static bool IsInsideRequiredDoorPassage(RoomTemplateDoor door, Vector2 point)
        {
            const float tolerance = 0.0001f;
            float halfWidth = RoomLayout.DoorOpeningLength * 0.5f;
            Vector2 start = door.SlotPosition;
            Vector2 end = door.SafeEntryPosition;
            if (door.Direction == RoomDoorDirection.Left || door.Direction == RoomDoorDirection.Right)
            {
                return point.x >= Mathf.Min(start.x, end.x) - tolerance &&
                       point.x <= Mathf.Max(start.x, end.x) + tolerance &&
                       Mathf.Abs(point.y - start.y) <= halfWidth + tolerance;
            }

            return point.y >= Mathf.Min(start.y, end.y) - tolerance &&
                   point.y <= Mathf.Max(start.y, end.y) + tolerance &&
                   Mathf.Abs(point.x - start.x) <= halfWidth + tolerance;
        }
    }

    public static class RoomContractCatalog
    {
        public static bool TryValidate(
            IReadOnlyList<RoomProfile> profiles,
            IReadOnlyList<RoomTemplateDefinition> templates,
            out string error)
        {
            if (profiles == null || profiles.Count == 0 || templates == null || templates.Count == 0)
            {
                error = "Room contract catalog requires at least one profile and template.";
                return false;
            }

            HashSet<string> profileIds = new(StringComparer.Ordinal);
            HashSet<RoomProfile> registeredProfiles = new();
            foreach (RoomProfile profile in profiles)
            {
                if (profile == null)
                {
                    error = "Room contract catalog contains a missing profile.";
                    return false;
                }

                if (!profile.TryValidate(out error))
                {
                    error = $"Room contract catalog contains an invalid profile. {error}";
                    return false;
                }

                if (!profileIds.Add(profile.ProfileId))
                {
                    error = $"Room contract catalog duplicates profile ID '{profile.ProfileId}'.";
                    return false;
                }

                registeredProfiles.Add(profile);
            }

            HashSet<string> templateIds = new(StringComparer.Ordinal);
            foreach (RoomTemplateDefinition template in templates)
            {
                if (template == null)
                {
                    error = "Room contract catalog contains a missing template.";
                    return false;
                }

                if (!template.TryValidate(out error))
                {
                    error = $"Room contract catalog contains an invalid template. {error}";
                    return false;
                }

                if (!template.TryValidateLayout(out error))
                {
                    error = $"Room contract catalog contains an invalid Layout. {error}";
                    return false;
                }

                if (!registeredProfiles.Contains(template.Profile))
                {
                    error = $"Room template '{template.TemplateId}' references an unregistered profile.";
                    return false;
                }

                if (!templateIds.Add(template.TemplateId))
                {
                    error = $"Room contract catalog duplicates template ID '{template.TemplateId}'.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
