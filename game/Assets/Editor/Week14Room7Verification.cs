using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14Room7Verification
    {
        private const float ActorRadius = 0.5f;

        [MenuItem("Trickal Fan Game/Week 14/Verify Room-7 Fixed Pillar Layout")]
        public static void Verify()
        {
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null &&
                   generator.RoomContentVersion >= Week14Room7Setup.RoomContentVersion &&
                   generator.EncounterContentVersion >= Week14Room7Setup.EncounterContentVersion,
                "Run Room-7 Setup before verification.");

            RoomTemplateDefinition template = generator.RoomTemplates.Single(candidate =>
                candidate.TemplateId == Week14Room7Setup.TemplateId);
            EncounterDefinition encounter = generator.EncounterDefinitions.Single(candidate =>
                candidate.EncounterId == Week14Room7Setup.EncounterId);
            Assert(template.TryValidate(out string error), error);
            Assert(encounter.TryValidateFor(template, 1, out error), error);
            ValidateCatalogSelection(generator);
            ValidatePrefabAndPassages(template);
            ValidateCollisionBehavior(template);
            Week14Room6Verification.Verify();
            Debug.Log("Week 14 Room-7 verification passed: the fixed pillar has no health or drops, keeps all " +
                      "four required entrance routes open, blocks player-sized movement and both projectile lines, " +
                      "stops charging enemies, and remains compatible with chaser/ranged/charging Encounters.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week14Room7Setup.Setup();
            Dictionary<string, string> guids = Week14Room7Setup.CreatedAssetPaths().ToDictionary(
                path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week14Room7Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Room-7 Setup changed the Game Scene GUID.");
            foreach (KeyValuePair<string, string> entry in guids)
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Room-7 Setup changed or lost the GUID for {entry.Key}.");
            Verify();
        }

        private static void ValidateCatalogSelection(FloorGenerator generator)
        {
            bool sawPillar = false;
            bool sawCompatiblePair = false;
            for (int seed = 1; seed <= 1024 && !sawCompatiblePair; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                foreach (GeneratedRoomNode node in graph.Nodes)
                {
                    if (node.TemplateId != Week14Room7Setup.TemplateId) continue;
                    sawPillar = true;
                    Assert(node.RoomType == RoomType.Normal,
                        "The pillar Layout must only be assigned to Normal rooms.");
                    if (node.Role != GeneratedRoomRole.Intermediate) continue;
                    Assert(node.Encounter != null &&
                           node.Encounter.TryValidateFor(node.Template, node.FloorNumber,
                               node.DirectionalConnections, out error), error);
                    if (node.EncounterId == Week14Room7Setup.EncounterId)
                        sawCompatiblePair = true;
                }
            }
            Assert(sawPillar && sawCompatiblePair,
                "1024 seeds must select the pillar Layout and its compatible Encounter together at least once.");
        }

        private static void ValidatePrefabAndPassages(RoomTemplateDefinition template)
        {
            RoomPrefab room = template.RoomPrefabAsset.GetComponent<RoomPrefab>();
            RoomStaticObstacle[] obstacles = template.RoomPrefabAsset
                .GetComponentsInChildren<RoomStaticObstacle>(true);
            Assert(room != null && obstacles.Length == 1, "The pillar Layout must contain exactly one static obstacle.");
            RoomStaticObstacle obstacle = obstacles[0];
            Assert(obstacle.ObstacleId == Week14Room7Setup.ObstacleId,
                "The pillar Layout has an incorrect stable obstacle ID.");
            Assert(obstacle.TryValidate(out string error), error);
            Assert(obstacle.GetComponent<Health>() == null &&
                   obstacle.GetComponent<ItemDropSource>() == null,
                "The fixed pillar must not be destructible or grant drops.");

            BoxCollider2D collider = obstacle.GetComponent<BoxCollider2D>();
            Vector2 obstacleCenter = room.transform.InverseTransformPoint(collider.bounds.center);
            Vector2 obstacleSize = Vector2.Scale(collider.size, obstacle.transform.localScale);
            Assert(Approximately(obstacleCenter, Vector2.zero) &&
                   Approximately(obstacleSize, Week14Room7Setup.PillarSize),
                "The fixed pillar must occupy the authored 3x3 center footprint.");

            Rect blocked = ExpandedRect(obstacleCenter, obstacleSize, ActorRadius);
            foreach (RoomTemplateDoor door in template.DoorSlots)
            {
                Assert(!blocked.Overlaps(RequiredPassageBounds(door)),
                    $"The pillar overlaps the {door.Direction} required door passage.");
                Assert(HasOrthogonalRoute(door.SafeEntryPosition, blocked, template.Profile.MovementBounds),
                    $"A player-sized actor has no fixed-layout route from the {door.Direction} safe entry.");
            }

            float topLane = template.Profile.MovementBounds.yMax - blocked.yMax;
            float bottomLane = blocked.yMin - template.Profile.MovementBounds.yMin;
            float leftLane = blocked.xMin - template.Profile.MovementBounds.xMin;
            float rightLane = template.Profile.MovementBounds.xMax - blocked.xMax;
            Assert(topLane >= 2f && bottomLane >= 2f && leftLane >= 2f && rightLane >= 2f,
                "The pillar must leave at least two world units of player-sized clearance on every side.");
        }

        private static void ValidateCollisionBehavior(RoomTemplateDefinition template)
        {
            GameObject roomInstance = UnityEngine.Object.Instantiate(template.RoomPrefabAsset);
            GameObject actorRoot = new("Room-7 Collision Verification");
            try
            {
                RoomStaticObstacle obstacle = roomInstance.GetComponentInChildren<RoomStaticObstacle>(true);
                BoxCollider2D pillar = obstacle.GetComponent<BoxCollider2D>();
                Physics2D.SyncTransforms();
                int environmentMask = 1 << LayerMask.NameToLayer("Environment");
                RaycastHit2D movementHit = Physics2D.CircleCast(new Vector2(-6f, 0f), ActorRadius,
                    Vector2.right, 12f, environmentMask);
                Assert(movementHit.collider == pillar,
                    "A player-sized movement sweep must be blocked by the central pillar.");
                RaycastHit2D sightHit = Physics2D.Raycast(new Vector2(-6f, 0f), Vector2.right,
                    12f, environmentMask);
                Assert(sightHit.collider == pillar,
                    "The central pillar must block the direct projectile line of sight.");

                ValidateEnemyProjectile(pillar);
                ValidatePlayerProjectile(actorRoot.transform, pillar);
                ValidateChaser(actorRoot.transform, pillar);
                ValidateChargingEnemy(actorRoot.transform, pillar);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorRoot);
                UnityEngine.Object.DestroyImmediate(roomInstance);
            }
        }

        private static void ValidateEnemyProjectile(Collider2D pillar)
        {
            EnemyProjectile projectile = EnemyProjectile.Create(new Vector2(-6f, 0f), Vector2.right,
                null, 1f, 5f, 2f, null);
            bool stopped = projectile.TryHit(pillar);
            Assert(stopped && projectile == null,
                "An enemy projectile must stop when it reaches the Environment pillar.");
        }

        private static void ValidatePlayerProjectile(Transform parent, Collider2D pillar)
        {
            GameObject ownerObject = new("Projectile Owner");
            ownerObject.transform.SetParent(parent);
            Health owner = ownerObject.AddComponent<Health>();
            GameObject projectileObject = new("Player Projectile Verification");
            projectileObject.transform.SetParent(parent);
            Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            projectileObject.AddComponent<CircleCollider2D>();
            Projectile projectile = projectileObject.AddComponent<Projectile>();
            projectile.Launch(Vector2.right, owner,
                new DamageContext(ownerObject, DamageSourceType.PlayerAttack, 1f));
            MethodInfo hit = typeof(Projectile).GetMethod("Hit", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(hit != null, "The player projectile collision handler is missing.");
            hit.Invoke(projectile, new object[] { pillar });
            Assert(projectile == null, "A player projectile must be consumed by the solid pillar.");
        }

        private static void ValidateChaser(Transform parent, Collider2D pillar)
        {
            GameObject target = new("Chaser Target");
            target.transform.SetParent(parent);
            target.transform.position = new Vector2(6f, 0f);
            target.AddComponent<Health>();
            GameObject chaserObject = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(Week14Encounter2Setup.ChaserPrefabPath),
                new Vector2(-6f, 0f), Quaternion.identity, parent);
            EnemyChase chase = chaserObject.GetComponent<EnemyChase>();
            chase.Configure(2f, 20f, 0.8f);
            chase.SetTarget(target.transform);
            chase.TickChase();
            Rigidbody2D body = chaserObject.GetComponent<Rigidbody2D>();
            Assert(body.linearVelocity.x > 0f,
                "The chaser must attempt its existing direct movement toward a target behind the pillar.");
            ContactFilter2D filter = new();
            filter.SetLayerMask(1 << LayerMask.NameToLayer("Environment"));
            RaycastHit2D[] hits = new RaycastHit2D[8];
            int count = body.Cast(body.linearVelocity.normalized, filter, hits, 12f);
            Assert(hits.Take(count).Any(hit => hit.collider == pillar),
                "The chaser Rigidbody sweep must be stopped by the fixed pillar collision.");
        }

        private static void ValidateChargingEnemy(Transform parent, Collider2D pillar)
        {
            GameObject target = new("Charging Target");
            target.transform.SetParent(parent);
            target.transform.position = new Vector2(6f, 0f);
            target.AddComponent<Health>();
            GameObject enemyObject = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(Week14Encounter2Setup.ChargingPrefabPath),
                new Vector2(-6f, 0f), Quaternion.identity, parent);
            ChargingEnemyController charging = enemyObject.GetComponent<ChargingEnemyController>();
            charging.Configure(20f, 0.01f, 9f, 1f, 0.5f, 1f, 1f);
            charging.SetTarget(target.transform);
            charging.TickBehavior(0f);
            charging.TickBehavior(0.02f);
            Assert(charging.State == ChargingEnemyState.Dashing &&
                   charging.TryResolveCollision(pillar, 0.03f) &&
                   charging.State == ChargingEnemyState.Recovering &&
                   enemyObject.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero,
                "A charging enemy must enter recovery and stop on the Environment pillar.");
        }

        private static bool HasOrthogonalRoute(Vector2 start, Rect blocked, Rect movementBounds)
        {
            Vector2 destination = -start;
            float upperY = blocked.yMax + 0.75f;
            float lowerY = blocked.yMin - 0.75f;
            float leftX = blocked.xMin - 0.75f;
            float rightX = blocked.xMax + 0.75f;
            Vector2[][] routes =
            {
                new[] { start, new Vector2(start.x, upperY), new Vector2(destination.x, upperY), destination },
                new[] { start, new Vector2(start.x, lowerY), new Vector2(destination.x, lowerY), destination },
                new[] { start, new Vector2(leftX, start.y), new Vector2(leftX, destination.y), destination },
                new[] { start, new Vector2(rightX, start.y), new Vector2(rightX, destination.y), destination },
            };
            return routes.Any(route => RouteIsClear(route, blocked, movementBounds));
        }

        private static bool RouteIsClear(IReadOnlyList<Vector2> points, Rect blocked, Rect bounds)
        {
            if (points.Any(point => !bounds.Contains(point) || blocked.Contains(point))) return false;
            for (int index = 1; index < points.Count; index++)
            {
                Vector2 first = points[index - 1];
                Vector2 second = points[index];
                if (Mathf.Approximately(first.x, second.x))
                {
                    if (first.x >= blocked.xMin && first.x <= blocked.xMax &&
                        Mathf.Max(Mathf.Min(first.y, second.y), blocked.yMin) <=
                        Mathf.Min(Mathf.Max(first.y, second.y), blocked.yMax)) return false;
                }
                else if (first.y >= blocked.yMin && first.y <= blocked.yMax &&
                         Mathf.Max(Mathf.Min(first.x, second.x), blocked.xMin) <=
                         Mathf.Min(Mathf.Max(first.x, second.x), blocked.xMax)) return false;
            }
            return true;
        }

        private static Rect ExpandedRect(Vector2 center, Vector2 size, float expansion) =>
            new(center - size * 0.5f - Vector2.one * expansion,
                size + Vector2.one * expansion * 2f);

        private static Rect RequiredPassageBounds(RoomTemplateDoor door)
        {
            float halfWidth = RoomLayout.DoorOpeningLength * 0.5f;
            Vector2 minimum = Vector2.Min(door.SlotPosition, door.SafeEntryPosition);
            Vector2 maximum = Vector2.Max(door.SlotPosition, door.SafeEntryPosition);
            if (door.Direction == RoomDoorDirection.Left || door.Direction == RoomDoorDirection.Right)
            {
                minimum.y -= halfWidth;
                maximum.y += halfWidth;
            }
            else
            {
                minimum.x -= halfWidth;
                maximum.x += halfWidth;
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static bool Approximately(Vector2 first, Vector2 second) =>
            (first - second).sqrMagnitude < 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
