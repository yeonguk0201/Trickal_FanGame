using System;
using System.IO;
using System.Linq;
using TrickalFanGame.Frontend;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Trap-0: one Prefab per trap kind, wired to the user's trap drawings. Floor kinds have no collider; obstacle
    // kinds are a fixed-kind DestructibleObstacle on the Environment layer. Layouts (Trap-1) and the development
    // panel place copies and give each its own ID. Re-running updates the same Prefabs and keeps their GUIDs.
    public static class Week25Trap0Setup
    {
        public const string PrefabFolder = "Assets/Prefabs/Traps";
        public const string TrapArtFolder = FairyKingdomArtworkSetup.ArtRoot + "/Traps";
        public const string VisualName = "Visual";
        public const string DefaultTrapId = "trap-01";
        // Above the pit drawing (-9) and under obstacles, enemies and the player.
        public const int FloorSortingOrder = -8;
        public static readonly Color HitColor = new(0.7f, 0.7f, 0.7f);

        public static readonly RoomTrapKind[] Kinds = (RoomTrapKind[])Enum.GetValues(typeof(RoomTrapKind));

        public static string PrefabPath(RoomTrapKind kind) => $"{PrefabFolder}/{RoomTrap.ArtworkIdOf(kind)}.prefab";

        public static string[] CreatedAssetPaths() => Kinds.Select(PrefabPath).ToArray();

        [MenuItem("Trickal Fan Game/Week 25/Setup Trap-0 Trap Prefabs")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Trap-0 setup.");
            int environment = LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName);
            if (environment < 0) throw new InvalidOperationException("The Environment layer is missing.");
            // 가시 돌 is a 짱돌 with spikes, so it shares the basic obstacle drop table. The table is only read:
            // rebuilding it would rewrite the pickup Prefabs and drop their connected artwork.
            if (AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week18Obstacle1Setup.DropTablePath) == null)
                throw new InvalidOperationException(
                    $"Trap-0 needs the obstacle drop table {Week18Obstacle1Setup.DropTablePath}. Run Obstacle-1 setup.");
            Directory.CreateDirectory(PrefabFolder);
            foreach (RoomTrapKind kind in Kinds)
            {
                string path = PrefabPath(kind);
                bool exists = File.Exists(path);
                GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject();
                try
                {
                    root.name = RoomTrap.ArtworkIdOf(kind);
                    ConfigureTrap(root, DefaultTrapId, kind, false, environment);
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new InvalidOperationException($"Trap-0 could not save {path}.");
                }
                finally
                {
                    if (exists) PrefabUtility.UnloadPrefabContents(root);
                    else Object.DestroyImmediate(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Trap-0 ready: seven trap Prefabs in " + PrefabFolder + " (floor spikes, periodic spikes, " +
                      "damage puddle and spiderweb without colliders; fire, spiked rock and poison mushroom as " +
                      "fixed-kind destructible obstacles), each bound to its trap drawing.");
        }

        // One trap on a cell. The room Layout importer places its traps through the same path.
        public static void ConfigureTrap(GameObject trap, string id, RoomTrapKind kind, bool secondPhase,
            int environment)
        {
            string artworkId = RoomTrap.ArtworkIdOf(kind);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TrapArtFolder}/{artworkId}.png");
            if (sprite == null)
                throw new InvalidOperationException($"Trap-0 is missing the drawing {TrapArtFolder}/{artworkId}.png.");

            bool isFloor = RoomTrap.IsFloorKind(kind);
            trap.transform.localRotation = Quaternion.identity;
            trap.transform.localScale = Vector3.one;
            trap.layer = isFloor ? 0 : environment;

            Transform visualTransform = trap.transform.Find(VisualName);
            GameObject visual = visualTransform != null ? visualTransform.gameObject : new GameObject(VisualName);
            visual.transform.SetParent(trap.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            // The artwork view swaps in a sprite fitted to one cell at play time, so the renderer is not scaled.
            visual.transform.localScale = Vector3.one;
            visual.layer = trap.layer;
            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(visual);
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = isFloor ? FloorSortingOrder : 0;

            if (isFloor)
            {
                if (trap.TryGetComponent(out DestructibleObstacle staleObstacle)) Object.DestroyImmediate(staleObstacle);
                foreach (Collider2D stale in trap.GetComponentsInChildren<Collider2D>(true))
                    Object.DestroyImmediate(stale);
            }
            else
            {
                BoxCollider2D collider = GetOrAdd<BoxCollider2D>(trap);
                collider.isTrigger = false;
                collider.offset = Vector2.zero;
                collider.size = Vector2.one;
                ResourceDropTable table = kind == RoomTrapKind.SpikedRock
                    ? AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week18Obstacle1Setup.DropTablePath)
                    : null;
                DestructibleObstacle obstacle = GetOrAdd<DestructibleObstacle>(trap);
                obstacle.Configure(id, RoomTrap.RequiredHitsOf(kind), table, renderer);
                obstacle.ConfigureFixedKind(RoomTrap.ObstacleVariantIdOf(kind), Color.white, HitColor,
                    RoomTrap.BlocksFlightOf(kind), 0, Color.white);
            }

            FairyKingdomArtworkView artwork = GetOrAdd<FairyKingdomArtworkView>(trap);
            artwork.Configure(artworkId,
                isFloor ? FairyKingdomArtworkMode.Fixed : FairyKingdomArtworkMode.Obstacle, renderer, RoomTrap.CellSize);
            RoomTrap roomTrap = GetOrAdd<RoomTrap>(trap);
            roomTrap.Configure(id, kind, secondPhase, renderer);
            roomTrap.ConfigureHitSize(isFloor ? DrawnSize(artworkId) : Vector2.one);
            if (!roomTrap.TryValidate(out string error)) throw new InvalidOperationException(error);
        }

        // The drawing is fitted to one cell wide, so its visible rectangle gives the size it covers on the cell.
        public static Vector2 DrawnSize(string artworkId)
        {
            FairyKingdomArtworkCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FairyKingdomArtworkCatalog>(FairyKingdomArtworkSetup.CatalogPath);
            FairyKingdomArtworkCatalog.Entry entry = catalog != null
                ? catalog.Entries.FirstOrDefault(candidate => candidate.id == artworkId)
                : null;
            if (entry == null || entry.visibleRect.width <= 0f || entry.visibleRect.height <= 0f)
                throw new InvalidOperationException(
                    $"Trap-0 needs the catalog entry '{artworkId}'. Run the fairy kingdom artwork setup.");
            float height = RoomTrap.CellSize * entry.visibleRect.height / entry.visibleRect.width;
            return new Vector2(RoomTrap.CellSize, Mathf.Min(RoomTrap.CellSize, height));
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component =>
            target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
    }
}
