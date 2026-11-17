using System;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Trap-2: 진흙케이크. Adds the user's cake drawing to the artwork catalog with its display Prefab and builds only
    // the 진흙케이크 trap Prefab through the Trap-0 path, so the other trap Prefabs, Scenes and rooms are untouched.
    // The development panel's trap list collects the new Prefab by itself. Re-running keeps every GUID.
    public static class Trap2MudCakeSetup
    {
        public const string ArtworkId = "trap-mud-cake";

        public static string PrefabPath => Week25Trap0Setup.PrefabPath(RoomTrapKind.MudCake);

        [MenuItem("Trickal Fan Game/Traps/Setup Trap-2 Mud Cake")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Trap-2 setup.");
            int environment = LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName);
            if (environment < 0) throw new InvalidOperationException("The Environment layer is missing.");
            AssetDatabase.Refresh();
            FairyKingdomArtworkCatalog catalog = FairyKingdomArtworkSetup.RefreshCatalog();
            FairyKingdomArtworkSetup.BuildDisplayPrefab(catalog, ArtworkId);
            Week25Trap0Setup.BuildPrefab(RoomTrapKind.MudCake, environment);
            AssetDatabase.SaveAssets();
            Debug.Log("Trap-2 ready: 진흙케이크 drawing in the artwork catalog and " + PrefabPath +
                      " (floor trap, no collider, hit size from the drawing). Place it from the development panel " +
                      "(F1) → — Trap — → Place MudCake.");
        }
    }
}
