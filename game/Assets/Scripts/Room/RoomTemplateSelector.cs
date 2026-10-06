using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public static class RoomTemplateSelector
    {
        public const string StartingRoomTemplateId = "small-standard";
        private const uint TemplateSalt = 0xB5297A4Du;
        private const float OverlapTolerance = 0.0001f;

        public static bool TryAssign(
            GeneratedFloorGraph graph,
            IReadOnlyList<RoomTemplateDefinition> templates,
            int contentVersion,
            Vector2 gridSpacing,
            out string error)
        {
            if (graph == null)
            {
                error = "Room template selection requires a generated floor graph.";
                return false;
            }

            if (contentVersion < 1)
            {
                error = "Room template selection requires a positive content version.";
                return false;
            }

            if (gridSpacing.x <= 0f || gridSpacing.y <= 0f)
            {
                error = "Room template selection requires positive grid spacing.";
                return false;
            }

            if (templates == null || templates.Count == 0)
            {
                error = "Room template selection requires at least one template.";
                return false;
            }

            RoomTemplateDefinition[] orderedTemplates = templates
                .OrderBy(template => template != null ? template.TemplateId : string.Empty, StringComparer.Ordinal)
                .ToArray();
            RoomProfile[] profiles = orderedTemplates
                .Where(template => template != null && template.Profile != null)
                .Select(template => template.Profile)
                .Distinct()
                .ToArray();
            if (!RoomContractCatalog.TryValidate(profiles, orderedTemplates, out error))
            {
                error = $"Room template selection has an invalid catalog. {error}";
                return false;
            }

            foreach (GeneratedFloor floor in graph.Floors.OrderBy(candidate => candidate.FloorNumber))
            {
                List<GeneratedRoomNode> assigned = new();
                foreach (GeneratedRoomNode node in floor.Nodes.OrderBy(candidate => candidate.RoomNumber))
                {
                    RoomTemplateDefinition[] candidates = orderedTemplates
                        .Where(template => template.SupportsRoomType(node.RoomType) &&
                                           template.SupportsFloor(node.FloorNumber) &&
                                           template.SupportsConnections(node.DirectionalConnections))
                        .ToArray();
                    if (node.Role == GeneratedRoomRole.Start)
                    {
                        candidates = candidates
                            .Where(template => string.Equals(
                                template.TemplateId,
                                StartingRoomTemplateId,
                                StringComparison.Ordinal))
                            .ToArray();
                    }
                    if (candidates.Length == 0)
                    {
                        if (node.Role == GeneratedRoomRole.Start)
                        {
                            error = $"Starting room {node.RoomId} requires compatible template " +
                                    $"'{StartingRoomTemplateId}'.";
                            return false;
                        }

                        error = $"Room {node.RoomId} has no template compatible with RoomType {node.RoomType} " +
                                "and its connection directions.";
                        return false;
                    }

                    int firstIndex = StableIndex(node.ContentSeed, contentVersion, candidates.Length);
                    RoomTemplateDefinition selected = null;
                    for (int offset = 0; offset < candidates.Length; offset++)
                    {
                        RoomTemplateDefinition candidate = candidates[(firstIndex + offset) % candidates.Length];
                        if (!OverlapsAnyAssigned(node, candidate, assigned, gridSpacing))
                        {
                            selected = candidate;
                            break;
                        }
                    }

                    if (selected == null)
                    {
                        error = $"Room {node.RoomId} has no size-compatible template at grid position " +
                                $"{node.GridPosition} with spacing {gridSpacing}.";
                        return false;
                    }

                    node.AssignTemplate(selected);
                    assigned.Add(node);
                }
            }

            error = null;
            return true;
        }

        public static bool LayoutsOverlap(
            RoomGridPosition firstPosition,
            Vector2 firstSize,
            RoomGridPosition secondPosition,
            Vector2 secondSize,
            Vector2 gridSpacing)
        {
            Vector2 firstCenter = new(firstPosition.X * gridSpacing.x, firstPosition.Y * gridSpacing.y);
            Vector2 secondCenter = new(secondPosition.X * gridSpacing.x, secondPosition.Y * gridSpacing.y);
            Vector2 separation = new(
                Mathf.Abs(firstCenter.x - secondCenter.x),
                Mathf.Abs(firstCenter.y - secondCenter.y));
            Vector2 requiredSeparation = (firstSize + secondSize) * 0.5f;
            return separation.x < requiredSeparation.x - OverlapTolerance &&
                   separation.y < requiredSeparation.y - OverlapTolerance;
        }

        private static bool OverlapsAnyAssigned(
            GeneratedRoomNode node,
            RoomTemplateDefinition candidate,
            IReadOnlyList<GeneratedRoomNode> assigned,
            Vector2 gridSpacing)
        {
            foreach (GeneratedRoomNode other in assigned)
            {
                if (LayoutsOverlap(
                        node.GridPosition,
                        candidate.Profile.InteriorSize,
                        other.GridPosition,
                        other.Template.Profile.InteriorSize,
                        gridSpacing))
                {
                    return true;
                }
            }

            return false;
        }

        private static int StableIndex(int contentSeed, int contentVersion, int count)
        {
            int seed = FloorGenerator.DeriveSeed(contentSeed, contentVersion, TemplateSalt);
            return (int)(unchecked((uint)seed) % (uint)count);
        }
    }
}
