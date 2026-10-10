using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    [InitializeOnLoad]
    public static class GroundShadowPlayVerification
    {
        private const string Pending = "GroundShadowPlayVerification.Pending";
        private static readonly List<GameObject> probes = new();
        private static int startedFrame;
        private static bool started;
        static GroundShadowPlayVerification() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the batch entry point only.");
            GroundShadowVerification.Verify();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            SessionState.SetString(Pending + ".Started", DateTime.UtcNow.Ticks.ToString());
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (long.TryParse(SessionState.GetString(Pending + ".Started", "0"), out long ticks) &&
                DateTime.UtcNow.Ticks - ticks > TimeSpan.FromSeconds(90).Ticks)
            {
                Finish(new TimeoutException("Ground shadow Play verification timed out."));
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 2) return;
            try
            {
                if (!started)
                {
                    started = true;
                    startedFrame = Time.frameCount;
                    foreach (string name in new[] { "ChargingEnemy", "RangedEnemy", "JyubiEnemy",
                                 "BuseureogiCrumbMinion", "TestBoss", "DestructibleObstacle", "TreasureChest",
                                 "KeyPickup", "BombPickup", "ElifPickup", "HealthPickup", "SPPickup",
                                 "ItemPickup", "SingleUseItemPickup", "PlacedBomb", "RoomPit", "SecretPit" })
                    {
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
                        Require(prefab != null, "Missing Play probe prefab: " + name);
                        GameObject owner = Object.Instantiate(prefab,
                            new Vector3(600 + probes.Count * 4, 600, 0), Quaternion.identity);
                        // Exercise real Awake/OnEnable registration, then isolate gameplay while presentation runs.
                        foreach (Behaviour component in owner.GetComponents<Behaviour>())
                            if (component is not GroundShadow && component is not Health &&
                                component is not FairyKingdomArtworkView) component.enabled = false;
                        Rigidbody2D body = owner.GetComponent<Rigidbody2D>();
                        if (body != null) body.constraints = RigidbodyConstraints2D.FreezeAll;
                        probes.Add(owner);
                    }
                    return;
                }
                if (Time.frameCount - startedFrame < 4) return;
                int attached = 0;
                foreach (GameObject owner in probes)
                {
                    bool floor = owner.GetComponent<TrickalFanGame.Room.RoomPit>() != null ||
                        owner.GetComponent<TrickalFanGame.Room.SecretPit>() != null;
                    GroundShadow shadow = owner.GetComponent<GroundShadow>();
                    if (floor)
                    {
                        Require(shadow == null, "Pits must not acquire ground shadows.");
                        continue;
                    }
                    Require(shadow != null && shadow.Visual != null && shadow.Visual.enabled &&
                        shadow.Visual.sprite == GroundShadow.SharedSprite,
                        "Spawned entity did not acquire its shared ground shadow: " + owner.name);
                    attached++;
                    owner.SetActive(false);
                    Require(!shadow.Visual.gameObject.activeInHierarchy, "Disabled body left a live shadow.");
                    owner.SetActive(true);
                    GroundShadow.AttachDuringPlay(owner);
                    shadow.Refresh();
                    int count = 0;
                    foreach (Transform child in owner.transform)
                        if (child.name == GroundShadow.ObjectName) count++;
                    Require(count == 1 && shadow.Visual.enabled, "Re-enable duplicated/lost a shadow: " + owner.name);
                }
                Debug.Log($"Common ground shadow Play verification passed: {attached} spawned gameplay prefabs, " +
                    "shared texture, room/body re-enable without duplicates, 2 pit exclusions. Art review pending.");
                Finish(null);
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void Finish(Exception exception)
        {
            SessionState.SetBool(Pending, false);
            if (exception != null) Debug.LogException(exception);
            EditorApplication.Exit(exception == null ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
