using System;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week7HighGradeSkillVerification
    {
        [MenuItem("Trickal Fan Game/Verify Phase D High Grade Skill")]
        public static void Verify()
        {
            ValidateConfiguredAssets();

            GameObject root = new("Phase D Verification Root");
            GameObject player = CreatePlayer(
                root.transform,
                out Health playerHealth,
                out PlayerStats stats,
                out PlayerMovement movement,
                out PlayerActionState actionState,
                out PlayerAttack meleeAttack,
                out PlayerProjectileAttack projectileAttack,
                out PlayerSP playerSP,
                out PlayerSkill lowerGradeSkill,
                out PlayerUltimate ultimate);
            GameObject projectileTemplateObject = CreateSkillProjectile(root.transform, out HomingSkillProjectile projectileTemplate);
            GameObject normalEnemy = CreateEnemy(root.transform, "Phase D Normal Enemy", new Vector2(0.5f, 0f), false,
                out Health normalHealth, out KnockbackReceiver normalKnockback);
            GameObject bossEnemy = CreateEnemy(root.transform, "Phase D Boss Enemy", new Vector2(-0.5f, 0f), true,
                out Health bossHealth, out KnockbackReceiver bossKnockback);
            BossController bossController = bossEnemy.GetComponent<BossController>();
            DamageCalculator.SetCriticalRollProviderForTesting(() => 1f);

            try
            {
                stats.AddAttackDamage(40);
                lowerGradeSkill.Configure(projectileTemplate, LayerMask.GetMask("Enemy"), 0.08f);
                Assert(playerSP.TryAdd(), "The Space blocking check needs one SP.");
                int launchedCount = 0;
                lowerGradeSkill.ProjectileLaunched += _ => launchedCount++;

                SimpleGraph graph = CreateGraph(root.transform, movement);
                FloorExit floorExit = CreateFloorExit(root.transform, out Transform floorDestination);

                const float firstStart = 100f;
                Assert(ultimate.TryActivate(firstStart), "Q-equivalent activation must start when ready.");
                Assert(!ultimate.TryActivate(firstStart), "Repeated Q input must not re-enter an active dash.");
                Assert(actionState.IsDashing && playerHealth.IsInvulnerable,
                    "The active ultimate must enter dash state and grant invulnerability.");
                Assert(Mathf.Approximately(actionState.DashSpeedMultiplier, 2f),
                    "The active dash must use the tuned 2x movement-speed multiplier.");
                Assert(!actionState.CanBasicAttack && !actionState.CanUseLowerGradeSkill && !actionState.CanTransition,
                    "Dash state must block basic attack, Space skill, and room/floor transitions.");
                Assert(!meleeAttack.CanAttack && !projectileAttack.CanAttack,
                    "Both basic attack implementations must consume the shared action gate.");

                int spBeforeBlockedCast = playerSP.CurrentSP;
                Assert(!lowerGradeSkill.TryCast() && playerSP.CurrentSP == spBeforeBlockedCast && launchedCount == 0,
                    "Space must be blocked during dash without spending SP or launching projectiles.");
                float healthBeforeInvulnerableHit = playerHealth.CurrentHealth;
                playerHealth.TakeDamage(new DamageContext(normalEnemy, DamageSourceType.EnemyContact, 5));
                Assert(playerHealth.CurrentHealth == healthBeforeInvulnerableHit,
                    "Enemy damage must not reduce player HP during the invulnerable dash.");
                Assert(!graph.Graph.TryTransition(graph.First, graph.Second, graph.SecondEntry, movement),
                    "RoomGraphController must reject transitions during dash.");
                Vector2 floorPositionBefore = movement.transform.position;
                Assert(!floorExit.TryEnter(movement) && (Vector2)movement.transform.position == floorPositionBefore,
                    "FloorExit must reject transitions during dash.");

                Assert(actionState.TryUpdateDashDirection(Vector2.right) && actionState.DashDirection == Vector2.right,
                    "WASD direction input must update the active dash direction.");
                Assert(!ultimate.TryImpact(null, firstStart),
                    "A wall or non-enemy collision must not end the dash.");
                Assert(actionState.IsDashing,
                    "Wall collision handling must leave the dash active so the player can steer away.");

                Physics2D.SyncTransforms();
                Assert(ultimate.TryImpact(normalHealth, firstStart + 1f),
                    "First enemy contact must apply impact and end the dash.");
                Assert(!ultimate.TryImpact(normalHealth, firstStart + 1f),
                    "The same dash must reject repeated impact processing.");
                Assert(normalHealth.CurrentHealth == 200 && bossHealth.CurrentHealth == 200,
                    "The area impact must deal current attack damage x200% once to each target.");
                Assert(normalKnockback.IsActive && bossKnockback.IsActive,
                    "Both normal and boss targets must receive knockback.");
                Assert(Mathf.Approximately(normalKnockback.CurrentVelocity.magnitude, 8f) &&
                       Mathf.Approximately(bossKnockback.CurrentVelocity.magnitude, 2f),
                    "Boss knockback must use the configured reduced coefficient.");
                Assert(actionState.IsImpactRecovering && playerHealth.IsInvulnerable && !actionState.CanMove,
                    "Enemy contact must enter an immobile, invulnerable impact recovery.");
                Assert(!actionState.CanTransition && !actionState.CanBasicAttack &&
                       !actionState.CanUseLowerGradeSkill && movement.CurrentVelocity == Vector2.zero,
                    "Impact recovery must block movement, attacks, Space, and transitions at the collision point.");
                float healthBeforeImpactRecoveryHit = playerHealth.CurrentHealth;
                playerHealth.TakeDamage(new DamageContext(normalEnemy, DamageSourceType.EnemyContact, 5));
                Assert(playerHealth.CurrentHealth == healthBeforeImpactRecoveryHit,
                    "Impact recovery invulnerability must reject incoming damage.");
                Assert(Mathf.Approximately(ultimate.NextReadyTime, firstStart + 1f + 30f),
                    "Cooldown must begin at dash end, not at activation.");
                Assert(!ultimate.IsReadyAt(firstStart + 30f),
                    "The ultimate must remain unavailable until the end-based cooldown completes.");

                Assert(!graph.Graph.TryTransition(graph.First, graph.Second, graph.SecondEntry, movement),
                    "Room transition must stay blocked during impact recovery.");
                Assert(!floorExit.TryEnter(movement),
                    "Floor transition must stay blocked during impact recovery.");

                float knockbackEndCheck = Time.time + 0.21f;
                normalKnockback.Tick(knockbackEndCheck);
                bossKnockback.Tick(knockbackEndCheck);
                Assert(!normalKnockback.IsKnockedBack && normalKnockback.IsStunned &&
                       !bossKnockback.IsKnockedBack && bossKnockback.IsStunned,
                    "Enemies must enter stun after knockback ends.");
                Assert(bossController.IsActionSuppressed,
                    "Boss attacks must stay suppressed during post-knockback stun.");
                normalKnockback.Tick(knockbackEndCheck + 0.16f);
                bossKnockback.Tick(knockbackEndCheck + 0.16f);
                Assert(normalKnockback.IsStunned && !bossKnockback.IsActive,
                    "Boss stun must end sooner than the normal-enemy stun.");
                Assert(!bossController.IsActionSuppressed,
                    "Boss attacks must resume after its shorter stun ends.");

                ultimate.Tick(firstStart + 1f + 0.39f);
                Assert(actionState.IsImpactRecovering && playerHealth.IsInvulnerable,
                    "Impact recovery must remain active just before its boundary.");
                ultimate.Tick(firstStart + 1f + 0.4f);
                Assert(actionState.Phase == PlayerActionPhase.Normal && meleeAttack.CanAttack && projectileAttack.CanAttack,
                    "Recovery completion must restore both basic attack implementations.");
                Assert(!playerHealth.IsInvulnerable && actionState.CanTransition,
                    "Impact recovery completion must remove invulnerability and restore transitions.");
                Assert(graph.Graph.TryTransition(graph.First, graph.Second, graph.SecondEntry, movement),
                    "Room transition must resume after impact recovery ends.");
                Assert(floorExit.TryEnter(movement) &&
                       movement.GetComponent<Rigidbody2D>().position == (Vector2)floorDestination.position,
                    "Floor transition must resume after impact recovery ends.");
                const float lowerGradeCastTime = 200f;
                Assert(lowerGradeSkill.TryCast(Vector2.down, lowerGradeCastTime) &&
                       playerSP.CurrentSP == spBeforeBlockedCast - 1 && launchedCount == 1,
                    "Space must work again after recovery, consume exactly one SP, and start its salvo.");
                lowerGradeSkill.Tick(lowerGradeCastTime + 0.24f);
                Assert(launchedCount == 4,
                    "The restored Space action must finish its four-shot interval salvo.");
                RemoveSpawnedSkillProjectiles(projectileTemplate);

                float cancelStart = ultimate.NextReadyTime;
                Assert(ultimate.TryActivate(cancelStart), "The cancellation check needs an active ultimate.");
                Assert(ultimate.TryCancel(cancelStart + 2f) &&
                       ultimate.LastEndReason == UltimateEndReason.Cancelled &&
                       actionState.IsCoastRecovering && !playerHealth.IsInvulnerable &&
                       Mathf.Approximately(ultimate.NextReadyTime, cancelStart + 2f + 24f),
                    "Pressing Q during the dash must cancel it and apply 80% of the 30-second cooldown.");
                Assert(!ultimate.TryCancel(cancelStart + 2.01f),
                    "Cancellation must not be accepted again during recovery.");
                ultimate.Tick(cancelStart + 2f + ultimate.CoastRecoveryDuration);
                Assert(actionState.Phase == PlayerActionPhase.Normal,
                    "Cancelled ultimate coast recovery must return to normal state.");

                float secondStart = ultimate.NextReadyTime;
                Assert(ultimate.TryActivate(secondStart), "The ultimate must reactivate after the reduced cooldown.");
                ultimate.Tick(secondStart + 9.99f);
                Assert(actionState.IsDashing, "The dash must remain active just before maximum duration.");
                ultimate.Tick(secondStart + 10f);
                Assert(actionState.IsCoastRecovering && !playerHealth.IsInvulnerable && !actionState.CanTransition &&
                       Mathf.Approximately(ultimate.NextReadyTime, secondStart + 10f + 30f),
                    "Maximum duration must enter non-invulnerable coast recovery and start cooldown.");
                ultimate.Tick(secondStart + 10.125f);
                Assert(actionState.IsCoastRecovering &&
                       Mathf.Approximately(actionState.DashSpeedMultiplier, 1f),
                    "Timeout coast recovery must decelerate toward zero speed.");
                ultimate.Tick(secondStart + 10.25f);
                Assert(actionState.Phase == PlayerActionPhase.Normal,
                    "Timeout coast recovery must return to normal state at its boundary.");

                float deathStart = ultimate.NextReadyTime;
                Assert(ultimate.TryActivate(deathStart), "The death cleanup check needs an active dash.");
                playerHealth.SetInvulnerable(false);
                playerHealth.TakeDamage(new DamageContext(normalEnemy, DamageSourceType.EnemyContact, 100));
                Assert(playerHealth.IsDead && !playerHealth.IsInvulnerable &&
                       actionState.Phase == PlayerActionPhase.Normal,
                    "Player death must clean up dash state and invulnerability.");

                Debug.Log(
                    "Phase D verification passed: state re-entry, Q cooldown, steerable invulnerable dash, " +
                    "basic/Space/transition gates, 200% area impact, normal/boss knockback, wall handling, " +
                    "post-knockback stun, invulnerable impact recovery, Q cancellation with 80% cooldown, " +
                    "timeout deceleration, cooldown origin, " +
                    "and death cleanup are valid.");
            }
            finally
            {
                DamageCalculator.ResetCriticalRollProvider();
                RemoveSpawnedSkillProjectiles(projectileTemplate);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateConfiguredAssets()
        {
            PlayerMovement scenePlayer = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            Assert(scenePlayer != null && scenePlayer.GetComponent<PlayerActionState>() != null &&
                   scenePlayer.GetComponent<PlayerUltimate>() != null,
                "Run Setup Phase D High Grade Skill before verification; Player configuration is incomplete.");

            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TestEnemy.prefab");
            Assert(enemyPrefab != null && enemyPrefab.GetComponent<KnockbackReceiver>() != null,
                "Run Setup Phase D High Grade Skill before verification; TestEnemy lacks KnockbackReceiver.");
        }

        private static GameObject CreatePlayer(
            Transform parent,
            out Health health,
            out PlayerStats stats,
            out PlayerMovement movement,
            out PlayerActionState actionState,
            out PlayerAttack meleeAttack,
            out PlayerProjectileAttack projectileAttack,
            out PlayerSP playerSP,
            out PlayerSkill lowerGradeSkill,
            out PlayerUltimate ultimate)
        {
            GameObject player = new("Phase D Verification Player");
            player.transform.SetParent(parent);
            player.layer = LayerMask.NameToLayer("Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<CircleCollider2D>();
            health = player.AddComponent<Health>();
            actionState = player.AddComponent<PlayerActionState>();
            stats = player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerCombatEvents>();
            movement = player.AddComponent<PlayerMovement>();
            playerSP = player.AddComponent<PlayerSP>();
            lowerGradeSkill = player.AddComponent<PlayerSkill>();
            meleeAttack = player.AddComponent<PlayerAttack>();
            projectileAttack = player.AddComponent<PlayerProjectileAttack>();
            ultimate = player.AddComponent<PlayerUltimate>();

            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(stats, "Awake");
            InvokeLifecycle(movement, "Awake");
            InvokeLifecycle(playerSP, "Awake");
            InvokeLifecycle(lowerGradeSkill, "Awake");
            InvokeLifecycle(meleeAttack, "Awake");
            InvokeLifecycle(projectileAttack, "Awake");
            InvokeLifecycle(ultimate, "Awake");
            ultimate.Configure(30f, 10f, 2f, 0.4f, 0.25f, 1.5f, 8f, 0.25f, 0.4f, 0.15f,
                LayerMask.GetMask("Enemy"));
            return player;
        }

        private static GameObject CreateEnemy(
            Transform parent,
            string name,
            Vector2 position,
            bool isBoss,
            out Health health,
            out KnockbackReceiver knockback)
        {
            GameObject enemy = new(name);
            enemy.transform.SetParent(parent);
            enemy.transform.position = position;
            enemy.layer = LayerMask.NameToLayer("Enemy");
            Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            enemy.AddComponent<CircleCollider2D>();
            if (!isBoss)
            {
                enemy.AddComponent<BoxCollider2D>();
            }

            health = enemy.AddComponent<Health>();
            SerializedObject serializedHealth = new(health);
            serializedHealth.FindProperty("maxHealth").floatValue = 300f;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            knockback = enemy.AddComponent<KnockbackReceiver>();
            BossController bossController = null;
            if (isBoss)
            {
                bossController = enemy.AddComponent<BossController>();
            }

            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(knockback, "Awake");
            if (bossController != null)
            {
                InvokeLifecycle(bossController, "Awake");
            }
            return enemy;
        }

        private static GameObject CreateSkillProjectile(Transform parent, out HomingSkillProjectile projectile)
        {
            GameObject projectileObject = new("Phase D Skill Projectile Template");
            projectileObject.transform.SetParent(parent);
            projectileObject.transform.position = Vector3.one * 1000f;
            projectileObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            projectile = projectileObject.AddComponent<HomingSkillProjectile>();
            return projectileObject;
        }

        private static SimpleGraph CreateGraph(Transform parent, PlayerMovement player)
        {
            GameObject graphObject = new("Phase D Verification Graph");
            graphObject.transform.SetParent(parent);
            RoomGraphController graph = graphObject.AddComponent<RoomGraphController>();
            RoomNode first = CreateNode(graphObject.transform, "phase-d-room-01", Vector2.zero, out Transform firstEntry);
            RoomNode second = CreateNode(graphObject.transform, "phase-d-room-02", new Vector2(10f, 0f), out Transform secondEntry);
            RoomDoorway forward = CreateDoorway(first, second, secondEntry, graph);
            RoomDoorway backward = CreateDoorway(second, first, firstEntry, graph);
            first.SetDoorways(new[] { forward });
            second.SetDoorways(new[] { backward });
            graph.Configure(new[] { first, second }, first, player, null, null);
            InvokeLifecycle(graph, "Start");
            return new SimpleGraph(graph, first, second, secondEntry);
        }

        private static RoomNode CreateNode(Transform parent, string roomId, Vector2 center, out Transform entry)
        {
            GameObject nodeObject = new(roomId);
            nodeObject.transform.SetParent(parent);
            RoomNode node = nodeObject.AddComponent<RoomNode>();
            GameObject content = new($"{roomId} content");
            content.transform.SetParent(nodeObject.transform);
            Transform cameraAnchor = new GameObject("Camera Anchor").transform;
            cameraAnchor.SetParent(nodeObject.transform);
            cameraAnchor.position = center;
            entry = new GameObject("Entry").transform;
            entry.SetParent(nodeObject.transform);
            entry.position = center;
            node.Configure(roomId, 1, roomId.EndsWith("01") ? 1 : 2, content, cameraAnchor, entry,
                Array.Empty<RoomDoorway>());
            return node;
        }

        private static RoomDoorway CreateDoorway(
            RoomNode source,
            RoomNode destination,
            Transform destinationEntry,
            RoomGraphController graph)
        {
            GameObject doorwayObject = new($"{source.RoomId} to {destination.RoomId}");
            doorwayObject.transform.SetParent(source.ContentRoot.transform);
            doorwayObject.AddComponent<BoxCollider2D>().isTrigger = true;
            RoomDoorway doorway = doorwayObject.AddComponent<RoomDoorway>();
            doorway.Configure(graph, source, destination, destinationEntry);
            return doorway;
        }

        private static FloorExit CreateFloorExit(Transform parent, out Transform destination)
        {
            GameObject exitObject = new("Phase D Verification Floor Exit");
            exitObject.transform.SetParent(parent);
            exitObject.AddComponent<BoxCollider2D>().isTrigger = true;
            FloorExit floorExit = exitObject.AddComponent<FloorExit>();
            destination = new GameObject("Phase D Floor Destination").transform;
            destination.SetParent(parent);
            destination.position = new Vector2(20f, 0f);
            floorExit.Configure(null, destination, 2, null, null);
            InvokeLifecycle(floorExit, "Awake");
            InvokePrivate(floorExit, "SetUnlocked", true);
            return floorExit;
        }

        private static void RemoveSpawnedSkillProjectiles(HomingSkillProjectile template)
        {
            foreach (HomingSkillProjectile projectile in
                     UnityEngine.Object.FindObjectsByType<HomingSkillProjectile>(FindObjectsSortMode.None))
            {
                if (projectile != template)
                {
                    UnityEngine.Object.DestroyImmediate(projectile.gameObject);
                }
            }
        }

        private static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            InvokePrivate(component, methodName);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().FullName, methodName);
            }

            method.Invoke(target, arguments);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private readonly struct SimpleGraph
        {
            public SimpleGraph(RoomGraphController graph, RoomNode first, RoomNode second, Transform secondEntry)
            {
                Graph = graph;
                First = first;
                Second = second;
                SecondEntry = secondEntry;
            }

            public RoomGraphController Graph { get; }
            public RoomNode First { get; }
            public RoomNode Second { get; }
            public Transform SecondEntry { get; }
        }
    }
}
