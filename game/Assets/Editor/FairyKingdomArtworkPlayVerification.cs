using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    [InitializeOnLoad]
    public static class FairyKingdomArtworkPlayVerification
    {
        private const string Pending = "FairyKingdomArtworkPlayVerification.Pending";
        private static readonly List<GameObject> probes = new();
        private static int startedFrame;
        private static bool started;
        private static PlayerWaterStream water;
        private static GameObject statusProbe;
        static FairyKingdomArtworkPlayVerification() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            FairyKingdomArtworkVerification.Verify();
            EditorSceneManager.OpenScene(FairyKingdomArtworkSetup.PreviewScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            SessionState.SetString(Pending + ".Started", DateTime.UtcNow.Ticks.ToString());
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (SessionState.GetBool(Pending, false) && long.TryParse(
                    SessionState.GetString(Pending + ".Started", "0"), out long startedTicks) &&
                DateTime.UtcNow.Ticks - startedTicks > TimeSpan.FromSeconds(90).Ticks)
            {
                Debug.LogError("Fairy kingdom artwork Play verification timed out.");
                SessionState.SetBool(Pending, false);
                EditorApplication.Exit(1);
                return;
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
                EditorApplication.isCompiling || Time.frameCount < 2) return;
            try
            {
                if (!started)
                {
                    started = true;
                    startedFrame = Time.frameCount;
                    foreach (string name in new[] { "JyubiEnemy", "BuseureogiCrumbMinion", "DestructibleObstacle",
                                 "TreasureChest", "KeyPickup", "BombPickup", "PlacedBomb", "RoomPit", "SecretPit" })
                    {
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
                        GameObject owner = Object.Instantiate(prefab, new Vector3(500 + probes.Count * 3, 500, 0), Quaternion.identity);
                        foreach (Behaviour component in owner.GetComponents<Behaviour>())
                            if (component is not FairyKingdomArtworkView && component is not Health) component.enabled = false;
                        Rigidbody2D body = owner.GetComponent<Rigidbody2D>();
                        if (body != null) body.simulated = false;
                        probes.Add(owner);
                    }
                    GameObject waterOwner = new("Water artwork probe");
                    waterOwner.transform.position = new Vector3(500, 510, 0);
                    waterOwner.transform.localScale = Vector3.one * 1.7f;
                    water = waterOwner.AddComponent<PlayerWaterStream>();
                    water.enabled = false;
                    typeof(PlayerWaterStream).GetMethod("ShowBeam", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(water, new object[] { 0, new Vector2(500, 510), new Vector2(503, 510), 0.3f });
                    statusProbe = new GameObject("Independent status artwork probe");
                    statusProbe.transform.position = new Vector3(500, 515, 0);
                    statusProbe.AddComponent<SpriteRenderer>().sprite = FairyKingdomArtwork.Fit("low-blood-sugar-fairy", 1f);
                    Health statusHealth = statusProbe.AddComponent<Health>();
                    EnemyStatusEffects.TryApplyPoison(statusHealth, waterOwner, 1f,
                        new PoisonSettings(1f, 0.1f, 10f, 1f, 4), 0f);
                    EnemyStatusEffects.TryApplyBurn(statusHealth, waterOwner, 1f,
                        new BurnSettings(1f, 0.1f, 10f, 1f), 0f);
                    EnemyStatusEffects.TryApplyShock(statusHealth, new ShockSettings(1f, 0.1f, 10f, 4), 0f);
                    return;
                }
                if (Time.frameCount - startedFrame < 4) return;
                var preview = Object.FindObjectsByType<FairyKingdomArtworkView>(FindObjectsSortMode.None)
                    .Where(view => view.gameObject.layer == 31).ToArray();
                Require(preview.Length == 45, "Play preview must render all 45 assets.");
                foreach (FairyKingdomArtworkView view in preview)
                    Require(view.Visual != null && view.Visual.sprite != null && view.Visual.sprite.name == view.ArtworkId,
                        "Missing live preview artwork: " + view.ArtworkId);
                foreach (GameObject owner in probes)
                {
                    FairyKingdomArtworkView view = owner.GetComponent<FairyKingdomArtworkView>();
                    Require(view != null && view.Visual.sprite != null &&
                        view.Visual.sprite.texture != null && view.Visual.sprite.name != "",
                        "Missing live prefab artwork: " + owner.name);
                }
                SpriteRenderer beam = water.GetComponentInChildren<SpriteRenderer>();
                statusProbe.GetComponent<FairyKingdomStatusArtwork>().RefreshAt(Time.time + .2f);
                foreach (string id in new[] { "effect-poison", "effect-burn", "effect-shock" })
                    Require(statusProbe.GetComponentsInChildren<SpriteRenderer>().Any(renderer =>
                            renderer.enabled && renderer.sprite != null && renderer.sprite.name == id),
                        "Simultaneous status artwork missing: " + id);
                Require(beam != null && beam.sprite.name == "effect-water-stream" &&
                    Mathf.Abs(beam.bounds.size.x - 3f) < 0.01f && Mathf.Abs(beam.bounds.size.y - 0.3f) < 0.01f,
                    "Water artwork must retain world path length and width under player scaling.");
                foreach (string id in new[] { "effect-lightning", "effect-crumb-death" })
                {
                    var effect = FairyKingdomSpriteEffect.Play(id, new Vector3(500, 512, 0), 1f, 0.2f);
                    Require(effect != null && effect.GetComponent<SpriteRenderer>().sprite.name == id,
                        "Single-frame effect did not spawn: " + id);
                }
                Debug.Log("Fairy Kingdom Play artwork verification passed: 45 live preview sprites, 9 gameplay " +
                    "prefabs, simultaneous poison/burn/shock, scaled water-stream dimensions and transient " +
                    "single-frame effects. Visual art approval pending.");
                SessionState.SetBool(Pending, false);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool(Pending, false);
                EditorApplication.Exit(1);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
