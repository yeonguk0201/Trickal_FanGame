using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Obstacle-3 Play Mode fixture: seeded floors from the Game Scene catalog pick obstacle rooms and their Encounters,
    // then each room runs on an empty scene with its real Room Prefab, enemy prefabs, RoomController waves and physics.
    // The player holds still at a door entry or behind an obstacle; every enemy must reach an attack (melee swing or
    // contact, shot, charge telegraph) without entering an obstacle, and the room must clear wave by wave.
    [InitializeOnLoad]
    public static class Week18Obstacle3PlayVerification
    {
        private const string PendingKey = "Week18Obstacle3PlayVerification.Pending";
        private const string CasesKey = "Week18Obstacle3PlayVerification.Cases";
        private const string ExitKey = "Week18Obstacle3PlayVerification.ExitOnFinish";
        private const string RosterPath = "Assets/Encounters/enemy-roster.asset";
        private const int SeedLimit = 256;
        private const int CasesPerTemplate = 5;
        private const double RealTimeLimitSeconds = 900;

        private static double deadline;
        private static int runIndex;
        private static CaseList cases;
        private static Week18Obstacle3RoomProbe probe;
        private static readonly List<string> Reports = new();

        static Week18Obstacle3PlayVerification() => EditorApplication.update += Tick;

        public static string[] ObstacleTemplateIds => Week18Obstacle2Setup.Layouts
            .Select(layout => layout.TemplateId)
            .Append(Week14Room7Setup.TemplateId)
            .ToArray();

        public static void RunBatch() => Start(true);

        [MenuItem("Trickal Fan Game/Week 18/Play Verify Obstacle-3 Enemies Never Stall In Obstacle Rooms")]
        public static void RunFromMenu() => Start(false);

        private static void Start(bool exitOnFinish)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ValidateRangedRetreat();
            CaseList collected = CollectCases();
            SessionState.SetString(CasesKey, JsonUtility.ToJson(collected));
            SessionState.SetBool(ExitKey, exitOnFinish);
            SessionState.SetBool(PendingKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        // The stall the Play Mode runs first exposed: a ranged enemy too close to the player backed straight into the
        // pillar behind it and neither moved nor fired. It must now turn 45 degrees or hold position and fire.
        private static void ValidateRangedRetreat()
        {
            Vector2 origin = Week18Obstacle0Verification.Origin;
            GameObject root = new("Obstacle-3 Ranged Retreat Verification");
            List<EnemyProjectile> fired = new();
            try
            {
                GameObject player = Week18Obstacle0Verification.CreatePlayer(root.transform, origin + Vector2.down * 1.5f);
                Week18Obstacle0Verification.CreateEnemy<RangedEnemyController>(root.transform, out Rigidbody2D body,
                    out RangedEnemyController ranged);
                ranged.SetTarget(player.transform);
                ranged.ProjectileFired += fired.Add;
                Physics2D.SyncTransforms();
                ranged.TickBehavior(10f);
                Assert(fired.Count == 0 && Vector2.Dot(body.linearVelocity.normalized, Vector2.up) > 0.999f,
                    "A ranged enemy too close to the player with open space behind it must back straight away.");

                GameObject behind = Week18Obstacle0Verification.CreateObstacle(root.transform, origin + Vector2.up * 1.6f);
                Physics2D.SyncTransforms();
                ranged.TickBehavior(10f);
                Assert(fired.Count == 0 && body.linearVelocity.sqrMagnitude > 0.5f &&
                       Mathf.Abs(Vector2.Dot(body.linearVelocity.normalized, Vector2.up) - Mathf.Cos(45f * Mathf.Deg2Rad)) <
                       0.01f,
                    "A ranged enemy whose straight retreat is blocked must back away at 45 degrees.");

                foreach (Vector2 cell in new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f) })
                    Week18Obstacle0Verification.CreateObstacle(root.transform, origin + cell);
                Physics2D.SyncTransforms();
                ranged.TickBehavior(10f);
                Assert(fired.Count == 1 && body.linearVelocity == Vector2.zero,
                    "A ranged enemy cornered with a clear line of fire must hold position and fire.");
                Assert(behind != null, "The blocking obstacle must remain for the cornered check.");
            }
            finally
            {
                foreach (EnemyProjectile projectile in fired.Where(projectile => projectile != null))
                    Object.DestroyImmediate(projectile.gameObject);
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        // Edit Mode: the Game Scene generator decides which obstacle rooms, Encounters and SpawnPoints each seed uses.
        private static CaseList CollectCases()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null && generator.RoomContentVersion >= Week18Obstacle2Setup.RoomContentVersion,
                "Run Obstacle-2 setup before the Obstacle-3 Play Mode verification.");

            Dictionary<string, int> counts = ObstacleTemplateIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
            List<CaseData> result = new();
            for (int seed = 1; seed <= SeedLimit && counts.Values.Any(count => count < CasesPerTemplate); seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                foreach (GeneratedRoomNode node in graph.Nodes)
                {
                    if (node.Template == null || node.Encounter == null ||
                        !counts.TryGetValue(node.TemplateId, out int count) || count >= CasesPerTemplate)
                        continue;
                    List<WaveData> waves = new();
                    for (int waveIndex = 0; waveIndex < node.Encounter.Waves.Count; waveIndex++)
                    {
                        Assert(node.Encounter.TryResolveWave(node.Template, node.FloorNumber,
                                node.DirectionalConnections, waveIndex, out ResolvedEncounterSpawn[] resolved,
                                out error),
                            $"Seed {seed} room {node.RoomId} wave {waveIndex + 1} did not resolve. {error}");
                        waves.Add(new WaveData
                        {
                            roles = resolved.Select(spawn => (int)spawn.Role).ToArray(),
                            spawnIndices = resolved.Select(spawn => spawn.SpawnPointIndex).ToArray(),
                        });
                    }

                    result.Add(new CaseData
                    {
                        templatePath = AssetDatabase.GetAssetPath(node.Template),
                        seed = seed,
                        roomId = node.RoomId,
                        floor = node.FloorNumber,
                        encounterId = node.EncounterId,
                        doors = node.DirectionalConnections.Select(connection => (int)connection.Direction).ToArray(),
                        waves = waves.ToArray(),
                    });
                    counts[node.TemplateId] = count + 1;
                }
            }

            Assert(counts.Values.All(count => count >= CasesPerTemplate),
                $"{SeedLimit} seeds must place every obstacle Layout with an Encounter {CasesPerTemplate} times. " +
                string.Join(", ", counts.Select(entry => $"{entry.Key}={entry.Value}")));
            return new CaseList { cases = result.ToArray() };
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + RealTimeLimitSeconds;
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Finish(false, "Obstacle-3 Play Mode timed out.");
                return;
            }

            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 5) return;
            try
            {
                cases ??= JsonUtility.FromJson<CaseList>(SessionState.GetString(CasesKey, "{}"));
                Assert(cases.cases != null && cases.cases.Length > 0, "Obstacle-3 has no collected cases.");
                if (probe == null)
                {
                    probe = CreateRun(cases.cases[runIndex / 2], runIndex / 2, runIndex % 2 == 1);
                }
                else if (probe.Failure != null)
                {
                    Finish(false, probe.Failure);
                }
                else if (probe.Done)
                {
                    Reports.Add(probe.Summary);
                    Object.DestroyImmediate(probe.gameObject);
                    probe = null;
                    if (++runIndex == cases.cases.Length * 2)
                    {
                        Finish(true,
                            $"Obstacle-3 Play Mode passed: {runIndex} runs over {cases.cases.Length} seeded obstacle " +
                            $"rooms ({string.Join(", ", ObstacleTemplateIds)}); with the player holding a door entry " +
                            "or cover behind an obstacle, every enemy reached an attack without entering an " +
                            "obstacle and every room cleared all of its waves.\n" + string.Join("\n", Reports));
                    }
                }
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static Week18Obstacle3RoomProbe CreateRun(CaseData data, int caseIndex, bool useCover)
        {
            RoomTemplateDefinition template = AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(data.templatePath);
            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(RosterPath);
            Assert(template != null && roster != null, $"Obstacle-3 could not load {data.templatePath} or the roster.");

            GameObject root = new($"Obstacle-3 seed {data.seed} {data.roomId} {template.TemplateId} " +
                                  (useCover ? "cover" : "entry"));
            RoomPrefab room = Object.Instantiate(template.RoomPrefabAsset, root.transform).GetComponent<RoomPrefab>();
            room.transform.localPosition = Vector3.zero;
            if (room.RewardRoom != null) room.RewardRoom.gameObject.SetActive(false);
            if (room.Node.ContentRoot != null) room.Node.ContentRoot.SetActive(true);
            Physics2D.SyncTransforms();

            RoomDoorDirection entryDirection = (RoomDoorDirection)data.doors[caseIndex % data.doors.Length];
            Vector2 entry = template.DoorSlots.Single(door => door.Direction == entryDirection).SafeEntryPosition;
            Vector2 anchor = useCover ? FindCoverAnchor(template, room.transform, caseIndex) : entry;

            GameObject playerObject = new("Obstacle-3 Player");
            playerObject.transform.SetParent(root.transform);
            playerObject.transform.position = room.transform.TransformPoint(anchor);
            playerObject.tag = "Player";
            playerObject.layer = LayerMask.NameToLayer("Player");
            Rigidbody2D playerBody = playerObject.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerObject.AddComponent<CircleCollider2D>().radius = 0.4f;
            Health playerHealth = playerObject.AddComponent<Health>();
            playerObject.AddComponent<PlayerStats>();
            playerObject.AddComponent<PlayerActionState>();
            PlayerMovement movement = playerObject.AddComponent<PlayerMovement>();
            playerObject.AddComponent<PlayerCombatEvents>();
            movement.enabled = false;
            playerBody.bodyType = RigidbodyType2D.Kinematic;
            playerHealth.SetInvulnerable(true);

            RoomController controller = room.Controller;
            Collider2D trigger = controller.GetComponent<Collider2D>();
            if (trigger != null) trigger.enabled = false;
            DoorController[] blockers = room.DoorSlots.Select(slot => slot.Blocker).ToArray();
            controller.Configure(data.floor, 2, null, null, controller.SpawnPoints.ToArray(), blockers);
            controller.ConfigurePreplacedEnemies(Array.Empty<Health>());
            EncounterRuntimeWave[] waves = data.waves.Select(wave =>
            {
                GameObject[] prefabs = wave.roles.Select(role =>
                {
                    Assert(roster.TryResolve((EncounterEnemyRole)role, out GameObject prefab, out string error), error);
                    return prefab;
                }).ToArray();
                Transform[] points = wave.spawnIndices.Select(index => controller.SpawnPoints[index]).ToArray();
                return new EncounterRuntimeWave(prefabs, points);
            }).ToArray();
            controller.ConfigureEncounterWaves(waves);

            Week18Obstacle3RoomProbe created = root.AddComponent<Week18Obstacle3RoomProbe>();
            created.Begin(controller, playerBody, playerHealth,
                $"seed {data.seed} floor {data.floor} {data.roomId} '{template.TemplateId}' Encounter " +
                $"'{data.encounterId}' player at {(useCover ? "cover" : $"{entryDirection} entry")} {anchor}",
                data.waves.Length);
            return created;
        }

        // Picks a standing point hugging an obstacle on the side that hides it from the most SpawnPoints.
        private static Vector2 FindCoverAnchor(RoomTemplateDefinition template, Transform room, int caseIndex)
        {
            Assert(RoomObstacleLayout.TryCollectFootprints(template.RoomPrefabAsset,
                    out List<RoomObstacleFootprint> footprints, out string error) && footprints.Count > 0,
                $"Layout '{template.TemplateId}' needs obstacles for a cover anchor. {error}");
            Rect movement = template.Profile.MovementBounds;
            const float radius = RoomObstacleLayout.ActorRadius;
            Vector2[] normals = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
            List<(Vector2 point, int hidden)> candidates = new();
            foreach (RoomObstacleFootprint footprint in footprints)
            foreach (Vector2 normal in normals)
            {
                Vector2 extent = footprint.Bounds.size * 0.5f;
                Vector2 point = footprint.Bounds.center + Vector2.Scale(normal, extent) + normal * (radius + 0.15f);
                if (point.x < movement.xMin + radius || point.x > movement.xMax - radius ||
                    point.y < movement.yMin + radius || point.y > movement.yMax - radius)
                    continue;
                Vector2 world = room.TransformPoint(point);
                if (Physics2D.OverlapCircle(world, radius, EnemyObstacleNavigator.ObstacleMask) != null) continue;
                int hidden = template.SpawnPoints.Count(spawn =>
                    !EnemyObstacleNavigator.HasLineOfFire(room.TransformPoint(spawn), world));
                candidates.Add((point, hidden));
            }

            Assert(candidates.Count > 0, $"Layout '{template.TemplateId}' has no free cover point beside an obstacle.");
            (Vector2 point, int hidden)[] best = candidates
                .OrderByDescending(candidate => candidate.hidden)
                .Take(3)
                .ToArray();
            return best[caseIndex % best.Length].point;
        }

        private static void Finish(bool passed, string message)
        {
            bool exit = SessionState.GetBool(ExitKey, true);
            SessionState.SetBool(PendingKey, false);
            SessionState.EraseString(CasesKey);
            deadline = 0;
            runIndex = 0;
            cases = null;
            probe = null;
            Reports.Clear();
            Time.timeScale = 1f;
            if (passed) Debug.Log(message);
            else Debug.LogError(message);
            if (exit) EditorApplication.Exit(passed ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [Serializable]
        private sealed class CaseList
        {
            public CaseData[] cases;
        }

        [Serializable]
        private sealed class CaseData
        {
            public string templatePath;
            public int seed;
            public string roomId;
            public int floor;
            public string encounterId;
            public int[] doors;
            public WaveData[] waves;
        }

        [Serializable]
        private sealed class WaveData
        {
            public int[] roles;
            public int[] spawnIndices;
        }
    }

    [DefaultExecutionOrder(30000)]
    public sealed class Week18Obstacle3RoomProbe : MonoBehaviour
    {
        // Game seconds an enemy may take from spawning to its first attack before the run counts as stalled.
        public const float EngageTimeout = 20f;
        private const float KillDelay = 0.3f;
        private const float ContactDistance = 1.1f;
        private const float SimulationTimeScale = 4f;

        private readonly List<Tracked> tracked = new();
        private RoomController controller;
        private Rigidbody2D playerBody;
        private Vector2 anchor;
        private string label;
        private float startedAt;
        private float runTimeout;
        private float slowestEngage;
        private int spawned;

        public string Failure { get; private set; }
        public bool Done { get; private set; }
        public string Summary { get; private set; }

        public void Begin(RoomController configuredController, Rigidbody2D configuredPlayerBody, Health playerHealth,
            string configuredLabel, int waveCount)
        {
            controller = configuredController;
            playerBody = configuredPlayerBody;
            anchor = playerBody.position;
            label = configuredLabel;
            startedAt = Time.time;
            runTimeout = waveCount * (EngageTimeout + 5f);
            Time.timeScale = SimulationTimeScale;
            controller.EnemySpawned += OnEnemySpawned;
            controller.BeginCombat(playerHealth);
            if (controller.State != RoomState.Combat && controller.State != RoomState.Cleared)
                Failure = $"Obstacle-3 {label}: combat did not start ({controller.State}).";
        }

        private void OnDestroy()
        {
            if (controller != null) controller.EnemySpawned -= OnEnemySpawned;
        }

        private void OnEnemySpawned(GameObject enemy)
        {
            Tracked entry = new()
            {
                Enemy = enemy,
                Health = enemy.GetComponent<Health>(),
                Body = enemy.GetComponent<Rigidbody2D>(),
                Chase = enemy.GetComponent<EnemyChase>(),
                Melee = enemy.GetComponent<MeleeEnemyAttack>(),
                Charging = enemy.GetComponent<ChargingEnemyController>(),
                SpawnedAt = Time.time,
                SpawnPosition = enemy.transform.position,
            };
            RangedEnemyController ranged = enemy.GetComponent<RangedEnemyController>();
            if (ranged != null) ranged.ProjectileFired += _ => entry.Attacked = true;
            LongRangeSniperController sniper = enemy.GetComponent<LongRangeSniperController>();
            if (sniper != null) sniper.ProjectileFired += _ => entry.Attacked = true;
            if (entry.Melee != null)
                entry.Melee.StateChanged += state => entry.Attacked |= state != MeleeEnemyAttackState.Idle;
            tracked.Add(entry);
            spawned++;
        }

        private void FixedUpdate()
        {
            if (Failure != null || Done || controller == null) return;
            playerBody.position = anchor;
            playerBody.linearVelocity = Vector2.zero;

            int mask = EnemyObstacleNavigator.ObstacleMask;
            // Killing the last enemy of a wave spawns the next one into the list, so iterate by index.
            for (int index = 0; index < tracked.Count; index++)
            {
                Tracked entry = tracked[index];
                if (entry.Enemy == null || entry.Health == null || entry.Health.IsDead) continue;
                Vector2 position = entry.Body != null ? entry.Body.position : (Vector2)entry.Enemy.transform.position;
                if (Physics2D.OverlapPoint(position, mask) != null)
                {
                    Failure = $"Obstacle-3 {label}: {Describe(entry)} entered an obstacle at {position}.";
                    return;
                }

                if (entry.EngagedAt < 0f && HasEngaged(entry, position))
                {
                    entry.EngagedAt = Time.time;
                    slowestEngage = Mathf.Max(slowestEngage, Time.time - entry.SpawnedAt);
                }

                if (entry.EngagedAt >= 0f && Time.time >= entry.EngagedAt + KillDelay)
                {
                    entry.Health.SetInvulnerable(false);
                    entry.Health.TakeDamage(entry.Health.MaxHealth * 100f + 1000f);
                }
                else if (entry.EngagedAt < 0f && Time.time - entry.SpawnedAt > EngageTimeout)
                {
                    Failure = $"Obstacle-3 {label}: {Describe(entry)} spawned at {entry.SpawnPosition} never attacked " +
                              $"within {EngageTimeout}s and stalled at {position} " +
                              $"(distance {Vector2.Distance(position, anchor):0.00}, velocity " +
                              $"{(entry.Body != null ? entry.Body.linearVelocity : Vector2.zero)}).";
                    return;
                }
            }

            if (controller.State == RoomState.Cleared)
            {
                Done = true;
                Summary = $"  {label}: {spawned} enemies over {controller.WaveCount} waves, slowest first attack " +
                          $"{slowestEngage:0.0}s, cleared at {Time.time - startedAt:0.0}s.";
                Time.timeScale = 1f;
                return;
            }

            if (Time.time - startedAt > runTimeout)
            {
                Failure = $"Obstacle-3 {label}: the room did not clear within {runTimeout}s (state {controller.State}, " +
                          $"wave {controller.CurrentWaveNumber}/{controller.WaveCount}, alive {controller.AliveEnemyCount}).";
            }
        }

        private bool HasEngaged(Tracked entry, Vector2 position)
        {
            if (entry.Attacked) return true;
            if (entry.Charging != null && (entry.Charging.State == ChargingEnemyState.Windup ||
                                           entry.Charging.State == ChargingEnemyState.Dashing))
                return true;
            float distance = Vector2.Distance(position, anchor);
            if (entry.Chase != null && distance <= entry.Chase.StopDistance + 0.3f) return true;
            return distance <= ContactDistance;
        }

        private static string Describe(Tracked entry)
        {
            string state = entry.Charging != null ? $" [{entry.Charging.State}]" : string.Empty;
            LongRangeSniperController sniper = entry.Enemy.GetComponent<LongRangeSniperController>();
            if (sniper != null) state = $" [{sniper.State}]";
            return $"'{entry.Enemy.name}'{state}";
        }

        private sealed class Tracked
        {
            public GameObject Enemy;
            public Health Health;
            public Rigidbody2D Body;
            public EnemyChase Chase;
            public MeleeEnemyAttack Melee;
            public ChargingEnemyController Charging;
            public float SpawnedAt;
            public Vector2 SpawnPosition;
            public float EngagedAt = -1f;
            public bool Attacked;
        }
    }
}
