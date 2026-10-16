using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Room;

namespace TrickalFanGame.Item
{
    // The alive enemies of the room the graph shows the player in, for effects aimed at "every enemy in the room"
    // (교주의 권능, 짱셈스펠). Enemies of other rooms are never included.
    public static class CurrentRoomEnemies
    {
        public static List<Health> Collect(RoomGraphController graph)
        {
            RoomController room = FindController(graph);
            return room == null || room.IsProgressionStopped ? new List<Health>() : Filter(room.AliveEnemies);
        }

        public static List<Health> Filter(IEnumerable<Health> source)
        {
            List<Health> enemies = new();
            if (source == null) return enemies;
            foreach (Health enemy in source)
            {
                if (enemy != null && !enemy.IsDead && enemy.isActiveAndEnabled) enemies.Add(enemy);
            }

            return enemies;
        }

        public static RoomController FindController(RoomGraphController graph)
        {
            RoomNode node = graph != null ? graph.CurrentNode : null;
            if (node == null) return null;
            RoomPrefab prefab = node.GetComponent<RoomPrefab>();
            if (prefab != null && prefab.Controller != null) return prefab.Controller;
            return node.ContentRoot != null ? node.ContentRoot.GetComponentInChildren<RoomController>(true) : null;
        }
    }
}
