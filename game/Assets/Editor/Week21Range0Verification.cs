using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Range-0: projectile travel distance = flight time × shot speed. The player basic attack flies about 5.3 units,
    // each ranged enemy has its own range below the room width, boss barrages and the homing skill keep their time
    // limits. The shot speed effect is opened as a contract only; the toy telescope (item-15) adds +30% flight time.
    public static class Week21Range0Verification
    {
        private static readonly (string Path, Type Controller, float Lifetime, float ExpectedRange)[] EnemyRanges =
        {
            (Week21Range0Setup.RangedPrefabPath, typeof(RangedEnemyController),
                Week21Range0Setup.RangedProjectileLifetime, 9f),
            (Week19Spawn2Setup.QuickRangedPrefabPath, typeof(MobileRangedEnemyController),
                Week21Range0Setup.QuickRangedProjectileLifetime, 9.1f),
            (Week15Enemy4Setup.SniperPrefabPath, typeof(LongRangeSniperController),
                Week21Range0Setup.SniperProjectileLifetime, 14.3f),
            (Week21Range0Setup.ArcherPrefabPath, typeof(LongRangeSniperController),
                Week21Range0Setup.ArcherProjectileLifetime, 11.9f),
            (Week21Range0Setup.MagePrefabPath, typeof(LongRangeSniperController),
                Week21Range0Setup.MageProjectileLifetime, 9.8f),
        };

        private const string TelescopeId = "item-15";
        private const float TelescopeLifetimeBonus = 0.3f;

        public static void SetupAndVerifyBatch()
        {
            Week21Range0Setup.Setup();
            string[] guids = EnemyRanges.Select(entry => AssetDatabase.AssetPathToGUID(entry.Path)).ToArray();
            Week21Range0Setup.Setup();
            Assert(guids.All(guid => !string.IsNullOrWhiteSpace(guid)) &&
                   EnemyRanges.Select(entry => AssetDatabase.AssetPathToGUID(entry.Path)).SequenceEqual(guids),
                "Range-0 setup changed an enemy Prefab GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 21/Verify Range-0 Projectile Range")]
        public static void Verify()
        {
            ValidateContract();
            ValidateEnemyRanges();
            ValidatePlayerRange();
            ValidatePlayerProjectileExpiry();
            ValidateEnemyProjectileExpiry();
            Debug.Log("Range-0 verification passed: travel distance = flight time × shot speed; the player basic " +
                      "attack flies about 5.3 units, ranged enemies have their own ranges below the room width, " +
                      "projectiles expire at the end of their flight time, and the shot speed / flight time " +
                      "effects stack on the player; only the toy telescope uses flight time (+30%, range about 6.9).");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.ProjectileSpeedPercent == 33 &&
                   (int)ItemEffectType.ProjectileLifetimePercent == 34 &&
                   (int)ItemEffectType.CurrentBossRoomSpeedPercent == 32,
                "Range-0 must append effect types 33 and 34 without renumbering earlier effects.");
            Assert(!new ItemEffectEntry(ItemEffectType.ProjectileSpeedPercent).TryValidate(out _) &&
                   !new ItemEffectEntry(ItemEffectType.ProjectileLifetimePercent).TryValidate(out _),
                "Shot speed and flight time effects without a positive magnitude must be invalid.");
            Assert(new ItemEffectEntry(ItemEffectType.ProjectileSpeedPercent, 0.2f).TryValidate(out _) &&
                   new ItemEffectEntry(ItemEffectType.ProjectileLifetimePercent, 0.3f).TryValidate(out _),
                "Shot speed and flight time effects with a positive magnitude must be valid.");

            ItemDefinition item = CreateDefinition("item-range0-desc", 1,
                new ItemEffectEntry(ItemEffectType.ProjectileLifetimePercent, 0.3f),
                new ItemEffectEntry(ItemEffectType.ProjectileSpeedPercent, 0.2f));
            try
            {
                Assert(ArtifactEffectDescription.Build(item) == "사거리 +30% · 탄속 +20% (사거리 증가)",
                    "Range-0 effects must describe the range and shot speed bonuses.");
            }
            finally
            {
                Object.DestroyImmediate(item);
            }

            ItemDefinition[] assets = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Items" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(definition => definition != null)
                .ToArray();
            // Artifact-2: 실라의 바람살 is the first item that uses the shot speed effect.
            Assert(assets.Where(definition => definition.Effects.Any(effect =>
                        effect.EffectType == ItemEffectType.ProjectileSpeedPercent))
                    .Select(definition => definition.ItemId)
                    .All(itemId => itemId == Week23Artifact2Setup.WindArrowId),
                "Only 실라의 바람살 may use the shot speed effect.");
            Assert(assets.Where(definition => definition.Effects.Any(effect =>
                        effect.EffectType == ItemEffectType.ProjectileLifetimePercent))
                    .Select(definition => definition.ItemId).SequenceEqual(new[] { TelescopeId }),
                "Only the toy telescope (item-15) may use the flight time effect.");

            ItemDefinition telescope = LoadTelescope();
            Assert(telescope.IsValid && telescope.MaxStacks == 1 && telescope.Effects.Count == 3 &&
                   telescope.Effects[0].EffectType == ItemEffectType.AttackDamagePercent &&
                   telescope.Effects[1].EffectType == ItemEffectType.DistanceDamage &&
                   telescope.Effects[2].EffectType == ItemEffectType.ProjectileLifetimePercent &&
                   Mathf.Approximately(telescope.Effects[2].Magnitude, TelescopeLifetimeBonus),
                "The toy telescope must keep its attack and distance damage effects and add +30% flight time.");
            Assert(ArtifactEffectDescription.Build(telescope) ==
                   "공격력 +15% · 2~6m 거리 비례 피해 최대 +40% · 사거리 +30%",
                "The toy telescope description must list all three effects.");
        }

        private static void ValidateEnemyRanges()
        {
            foreach ((string path, Type controllerType, float lifetime, float expectedRange) in EnemyRanges)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert(prefab != null, $"Range-0 is missing Prefab '{path}'.");
                Component controller = prefab.GetComponent(controllerType);
                Assert(controller != null, $"'{path}' must keep its {controllerType.Name}.");

                SerializedObject serialized = new(controller);
                float speed = serialized.FindProperty("projectileSpeed").floatValue;
                float configuredLifetime = serialized.FindProperty("projectileLifetime").floatValue;
                float range = speed * configuredLifetime;
                Assert(Mathf.Approximately(configuredLifetime, lifetime),
                    $"'{path}' projectile lifetime must be {lifetime}s, found {configuredLifetime}s.");
                Assert(Mathf.Abs(range - expectedRange) <= 0.05f,
                    $"'{path}' range must be about {expectedRange} (speed {speed} × {configuredLifetime}s = {range}).");
                Assert(range < RoomLayout.Width,
                    $"'{path}' range {range} must stay below the room width so shots no longer always reach a wall.");
            }
        }

        private static void ValidatePlayerRange()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerProjectileAttack sceneAttack = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerProjectileAttack>(true)).Single();
            SerializedObject serializedAttack = new(sceneAttack);
            float sceneSpeed = serializedAttack.FindProperty("baseProjectileSpeed").floatValue;
            float sceneLifetime = serializedAttack.FindProperty("baseProjectileLifetime").floatValue;
            Assert(Mathf.Approximately(sceneLifetime, Week21Range0Setup.PlayerProjectileLifetime) &&
                   Mathf.Abs(sceneSpeed * sceneLifetime - Week21Range0Setup.PlayerBaseRange) <= 0.05f,
                $"The Game Scene player basic attack must fly about 5.3 units (speed {sceneSpeed} × {sceneLifetime}s).");

            GameObject player = new("Range-0 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory));
            ItemDefinition speedItem = CreateDefinition("item-range0-speed", 2,
                new ItemEffectEntry(ItemEffectType.ProjectileSpeedPercent, 0.25f));
            ItemDefinition lifetimeItem = CreateDefinition("item-range0-lifetime", 1,
                new ItemEffectEntry(ItemEffectType.ProjectileLifetimePercent, 0.5f));
            try
            {
                PlayerProjectileAttack attack = player.AddComponent<PlayerProjectileAttack>();
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                InvokeLifecycle(inventory, "Awake");
                InvokeLifecycle(attack, "Awake");
                PlayerStats stats = player.GetComponent<PlayerStats>();

                Assert(Mathf.Approximately(stats.ProjectileSpeedMultiplier, 1f) &&
                       Mathf.Approximately(stats.ProjectileLifetimeMultiplier, 1f) &&
                       Mathf.Approximately(attack.ProjectileSpeed * attack.ProjectileLifetime,
                           Week21Range0Setup.PlayerBaseRange),
                    "Without items the player basic attack must fly about 5.3 units.");

                Assert(inventory.TryAcquire(speedItem) && inventory.TryAcquire(speedItem),
                    "The shot speed contract item must be acquirable twice.");
                Assert(Mathf.Approximately(stats.ProjectileSpeedMultiplier, 1.5f) &&
                       Mathf.Approximately(attack.ProjectileSpeed, 12f) &&
                       Mathf.Approximately(attack.ProjectileLifetime, Week21Range0Setup.PlayerProjectileLifetime),
                    "Shot speed bonuses must stack additively and leave the flight time unchanged.");
                Assert(Mathf.Approximately(attack.ProjectileSpeed * attack.ProjectileLifetime,
                        Week21Range0Setup.PlayerBaseRange * 1.5f),
                    "A faster shot must fly farther because travel distance = flight time × shot speed.");

                Assert(inventory.TryAcquire(lifetimeItem), "The flight time contract item must be acquirable.");
                Assert(Mathf.Approximately(stats.ProjectileLifetimeMultiplier, 1.5f) &&
                       Mathf.Approximately(attack.ProjectileLifetime, Week21Range0Setup.PlayerProjectileLifetime * 1.5f) &&
                       Mathf.Approximately(attack.ProjectileSpeed * attack.ProjectileLifetime,
                           Week21Range0Setup.PlayerBaseRange * 2.25f),
                    "A flight time bonus must lengthen the range without changing the shot speed.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(speedItem);
                Object.DestroyImmediate(lifetimeItem);
            }

            GameObject telescopePlayer = new("Range-0 Telescope Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory));
            try
            {
                PlayerProjectileAttack attack = telescopePlayer.AddComponent<PlayerProjectileAttack>();
                InvokeLifecycle(telescopePlayer.GetComponent<Health>(), "Awake");
                InvokeLifecycle(telescopePlayer.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(telescopePlayer.GetComponent<PlayerStats>(), "Awake");
                PlayerInventory inventory = telescopePlayer.GetComponent<PlayerInventory>();
                InvokeLifecycle(inventory, "Awake");
                InvokeLifecycle(attack, "Awake");
                Assert(inventory.TryAcquire(LoadTelescope()) && !inventory.TryAcquire(LoadTelescope()),
                    "The toy telescope must stay a one-stack artifact.");
                Assert(Mathf.Approximately(attack.ProjectileSpeed, 8f) &&
                       Mathf.Approximately(attack.ProjectileLifetime, Week21Range0Setup.PlayerProjectileLifetime * 1.3f) &&
                       Mathf.Abs(attack.ProjectileSpeed * attack.ProjectileLifetime -
                                 Week21Range0Setup.PlayerBaseRange * 1.3f) <= 0.01f,
                    "The toy telescope must lengthen the basic attack range from about 5.3 to about 6.9.");
            }
            finally
            {
                Object.DestroyImmediate(telescopePlayer);
            }
        }

        private static ItemDefinition LoadTelescope()
        {
            ItemDefinition telescope = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Items/{TelescopeId}.asset");
            Assert(telescope != null && telescope.ItemId == TelescopeId, "Range-0 is missing the toy telescope item-15.");
            return telescope;
        }

        private static void ValidatePlayerProjectileExpiry()
        {
            GameObject ownerObject = new("Range-0 Projectile Owner");
            Health owner = ownerObject.AddComponent<Health>();
            try
            {
                Projectile configured = CreateProjectile();
                configured.Launch(Vector2.right * 8f, owner,
                    new DamageContext(ownerObject, DamageSourceType.PlayerProjectile, 1f), configuredLifetime: 0.5f);
                Assert(Mathf.Approximately(configured.Lifetime, 0.5f) &&
                       Mathf.Approximately(configured.RemainingLifetime, 0.5f),
                    "Launch must use the configured flight time.");
                Assert(!configured.TickLifetime(0.49f) && configured != null,
                    "A projectile must keep flying before its flight time ends.");
                Assert(configured.TickLifetime(0.02f) && configured == null,
                    "A projectile must disappear immediately when its flight time ends.");

                Projectile fallback = CreateProjectile();
                SerializedObject serializedFallback = new(fallback);
                float serializedLifetime = serializedFallback.FindProperty("lifetime").floatValue;
                fallback.Launch(Vector2.right, owner,
                    new DamageContext(ownerObject, DamageSourceType.PlayerProjectile, 1f));
                Assert(Mathf.Approximately(fallback.Lifetime, serializedLifetime),
                    "Launch without a flight time must fall back to the serialized lifetime.");
                Object.DestroyImmediate(fallback.gameObject);
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
            }
        }

        private static void ValidateEnemyProjectileExpiry()
        {
            float launchTime = Time.time;
            EnemyProjectile projectile = EnemyProjectile.Create(Vector2.zero, Vector2.right, null,
                EnemyDamageTier.Light, 5f, Week21Range0Setup.RangedProjectileLifetime, null);
            try
            {
                projectile.TickLifetime(launchTime + Week21Range0Setup.RangedProjectileLifetime - 0.05f);
                Assert(projectile != null && projectile.IsLaunched,
                    "An enemy projectile must keep flying before its lifetime ends.");
                projectile.TickLifetime(launchTime + Week21Range0Setup.RangedProjectileLifetime + 0.05f);
                Assert(projectile == null || !projectile.IsLaunched,
                    "An enemy projectile must disappear when its lifetime ends.");
            }
            finally
            {
                if (projectile != null) Object.DestroyImmediate(projectile.gameObject);
            }
        }

        private static Projectile CreateProjectile()
        {
            GameObject projectileObject = new("Range-0 Verification Projectile");
            Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            projectileObject.AddComponent<CircleCollider2D>();
            return projectileObject.AddComponent<Projectile>();
        }

        private static ItemDefinition CreateDefinition(string itemId, int maxStacks, params ItemEffectEntry[] effects)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = itemId;
            definition.ConfigureContract(itemId, itemId, ItemRarity.Common, true, maxStacks, effects);
            return definition;
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
