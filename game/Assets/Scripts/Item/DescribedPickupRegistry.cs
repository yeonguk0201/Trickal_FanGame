using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // UX-1: a floor item whose name and effect can be shown before it is collected.
    public interface IDescribedPickup
    {
        ItemDefinition Definition { get; }
        Vector3 Position { get; }
        bool IsAvailable { get; }
    }

    // Floor artifacts, spells and 권능 register here while they are enabled, so the description panel finds the
    // nearest one without searching the scene every frame.
    public static class DescribedPickupRegistry
    {
        private static readonly List<IDescribedPickup> pickups = new();

        public static IReadOnlyList<IDescribedPickup> All => pickups;

        public static void Register(IDescribedPickup pickup)
        {
            if (pickup != null && !pickups.Contains(pickup)) pickups.Add(pickup);
        }

        public static void Unregister(IDescribedPickup pickup) => pickups.Remove(pickup);

        // The closest available pickup within radius of the point, or null. Ties keep the one registered first.
        public static IDescribedPickup FindNearest(Vector2 point, float radius)
        {
            IDescribedPickup nearest = null;
            float best = radius * radius;
            for (int index = pickups.Count - 1; index >= 0; index--)
            {
                IDescribedPickup pickup = pickups[index];
                if (pickup == null || pickup is Object unityObject && unityObject == null)
                {
                    pickups.RemoveAt(index);
                    continue;
                }

                if (!pickup.IsAvailable || pickup.Definition == null) continue;
                float distance = ((Vector2)pickup.Position - point).sqrMagnitude;
                if (distance <= best)
                {
                    best = distance;
                    nearest = pickup;
                }
            }

            return nearest;
        }
    }
}
