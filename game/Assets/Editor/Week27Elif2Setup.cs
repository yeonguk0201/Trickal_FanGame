using System;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Elif-2 (contract 27 §9): the Unity side of the permanent elif. Runtime rules live in PermanentElif and
    // RunProgress; this only configures assets. It builds PermanentElifPickup.prefab (Resource-1 spec), gives
    // 아이시아의 지갑 its one-time elif roll (Artifact-3 setup) and adds the elif cell to the resource HUD (UX-1 setup).
    // Each re-runs in place without duplicating objects. Floor elif comes from converting a spawned gold pickup.
    public static class Week27Elif2Setup
    {
        public const string Piece = "Elif-2";

        [MenuItem("Trickal Fan Game/Week 27/Setup Elif-2 Permanent Elif")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Elif-2 setup.");
            if (Week17Resource1Setup.EnsurePrefab(TrickalFanGame.Resource.RunResourceType.Elif, out string error) == null)
                throw new InvalidOperationException("Elif-2 could not build the permanent elif pickup. " + error);
            Week23Artifact3Setup.Setup();
            Week27Ux1Setup.Setup();
            Debug.Log("Elif-2 setup complete: 아이시아의 지갑 rolls permanent elif and the resource HUD shows elif.");
        }
    }
}
