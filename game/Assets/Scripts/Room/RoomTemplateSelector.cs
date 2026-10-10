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
        private const uint DrawSalt = 0x68E31DA4u;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development panel only: every room that can use this template gets it, so a new Layout is reachable
        // without searching seeds. Rooms it does not fit keep their seeded template.
        public static string DevelopmentForcedTemplateId { get; set; }
#endif
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

                    // A weighted draw without replacement: a candidate that would overlap an assigned room leaves
                    // the draw and the rest keep their relative weights.
                    List<RoomTemplateDefinition> remaining = new(candidates);
                    RoomTemplateDefinition selected = TakeDevelopmentForced(node, remaining);
                    if (selected != null && OverlapsAnyAssigned(node, selected, assigned, gridSpacing)) selected = null;
                    for (int draw = 0; selected == null && remaining.Count > 0; draw++)
                    {
                        int index = PickWeighted(remaining, StableSeed(node.ContentSeed, contentVersion, draw));
                        RoomTemplateDefinition candidate = remaining[index];
                        remaining.RemoveAt(index);
                        if (!OverlapsAnyAssigned(node, candidate, assigned, gridSpacing)) selected = candidate;
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

        // The index a seed picks among candidates, each with a chance proportional to its selection weight.
        public static int PickWeighted(IReadOnlyList<RoomTemplateDefinition> candidates, int seed)
        {
            long total = 0;
            foreach (RoomTemplateDefinition candidate in candidates) total += candidate.SelectionWeight;
            long pick = unchecked((uint)seed) % total;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (pick < candidates[index].SelectionWeight) return index;
                pick -= candidates[index].SelectionWeight;
            }

            return candidates.Count - 1;
        }

        private static int StableSeed(int contentSeed, int contentVersion, int draw) =>
            FloorGenerator.DeriveSeed(FloorGenerator.DeriveSeed(contentSeed, contentVersion, TemplateSalt), draw,
                DrawSalt);

        private static RoomTemplateDefinition TakeDevelopmentForced(GeneratedRoomNode node,
            List<RoomTemplateDefinition> remaining)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (string.IsNullOrEmpty(DevelopmentForcedTemplateId) || node.Role == GeneratedRoomRole.Start) return null;
            for (int index = 0; index < remaining.Count; index++)
            {
                if (!string.Equals(remaining[index].TemplateId, DevelopmentForcedTemplateId, StringComparison.Ordinal))
                    continue;
                RoomTemplateDefinition forced = remaining[index];
                remaining.RemoveAt(index);
                return forced;
            }
#endif
            return null;
        }
    }
}
