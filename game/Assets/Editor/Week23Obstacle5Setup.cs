using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Obstacle-5: the resource obstacle kinds a candidate slot can become next to 마리의 폭탄상자. The room-level rule is
    // unchanged (40% of obstacle rooms, at most one special obstacle); this only adds kinds to the weighted table.
    // Kinds were chosen by the user (2026-10-06); every number here is an implementation default based on the Marie
    // bomb box until Balance-0. Re-running updates the same assets and keeps their GUIDs.
    // Obstacle-6 (2026-10-06) adds the exploding box, 셰이디의 랜덤박스 and 마요의 수집품 상자 to the same table.
    public static class Week23Obstacle5Setup
    {
        // Existing seeds keep their special slot but may now resolve another kind.
        public const int RoomContentVersion = 11;
        public const int MarieWeight = 25;
        public const float VaultRareItemChance = 0.05f;
        public const float VaultRareArtifactShare = 0.3f;
        public const float ExplosionFuse = 0.5f;
        public const int ExplosiveBoxHits = 2;
        public const float RandomBoxExplosionChance = 0.25f;
        // Enemy-6: of the boxes that do not explode, a fifth release five 쥬비 (15% of all boxes), and 83% of the rest
        // pay out, which keeps the payout near 50% and leaves about 10% empty.
        public const float RandomBoxEnemyChance = 0.2f;
        public const int RandomBoxEnemyCount = 5;
        public const float RandomBoxDropChance = 0.83f;
        public const float CollectionBoxArtifactChance = 0.25f;

        public sealed class VariantSpec
        {
            public VariantSpec(string variantId, string displayName, int tableWeight, ObstacleBreakRule breakRule,
                float dropChance, int dropCount, (string dropId, int weight)[] dropWeights, Color intactColor,
                Color crackedColor, float rareItemChance = 0f, float rareArtifactShare = 0f,
                int requiredHits = DestructibleObstacle.DefaultRequiredHits, float explosionChance = 0f,
                float enemyChance = 0f, int enemyCount = 1)
            {
                RequiredHits = requiredHits;
                ExplosionChance = explosionChance;
                EnemyChance = enemyChance;
                EnemyCount = enemyCount;
                VariantId = variantId;
                DisplayName = displayName;
                TableWeight = tableWeight;
                BreakRule = breakRule;
                DropChance = dropChance;
                DropCount = dropCount;
                DropWeights = dropWeights;
                IntactColor = intactColor;
                CrackedColor = crackedColor;
                RareItemChance = rareItemChance;
                RareArtifactShare = rareArtifactShare;
            }

            public string VariantId { get; }
            public string DisplayName { get; }
            public int TableWeight { get; }
            public ObstacleBreakRule BreakRule { get; }
            public float DropChance { get; }
            public int DropCount { get; }
            public (string dropId, int weight)[] DropWeights { get; }
            public Color IntactColor { get; }
            public Color CrackedColor { get; }
            public float RareItemChance { get; }
            public float RareArtifactShare { get; }
            public int RequiredHits { get; }
            public float ExplosionChance { get; }
            public float EnemyChance { get; }
            public int EnemyCount { get; }
            // A kind that always explodes has no drop table.
            public bool HasDropTable => DropWeights != null && DropWeights.Length > 0;
            public string MainDropId => DropWeights.OrderByDescending(entry => entry.weight).First().dropId;
            public string VariantPath => $"{Week20Obstacle4Setup.VariantFolder}/{VariantId}.asset";
            public string DropTablePath =>
                $"{Week17Resource3Setup.DropTableFolder}/obstacle-{VariantId}-drop-table.asset";
        }

        // The three food boxes share one candidate mix of hearts and SP. The rarer the box, the more likely it drops
        // and the more pickups it leaves: 에르핀 < 에슈르 < 리코타.
        private static readonly (string dropId, int weight)[] FoodBoxWeights =
        {
            ("heart", 45), ("sp", 45), ("gold", 8), ("pit", 2),
        };

        public static readonly VariantSpec GoldRock = new("gold-rock", "황금돌", 25, ObstacleBreakRule.Hits, 0.2f, 1,
            new[] { ("gold", 60), ("heart", 10), ("sp", 10), ("bomb", 10), ("key", 8), ("pit", 2) },
            new Color(1f, 0.82f, 0.25f), new Color(0.55f, 0.4f, 0.08f));

        public static readonly VariantSpec MayoKeyBundle = new("mayo-key-bundle", "마요의 열쇠꾸러미", 15,
            ObstacleBreakRule.Hits, 0.2f, 1,
            new[] { ("key", 60), ("gold", 12), ("heart", 9), ("sp", 9), ("bomb", 8), ("pit", 2) },
            new Color(0.75f, 0.8f, 0.9f), new Color(0.35f, 0.4f, 0.5f));

        public static readonly VariantSpec ErpinSnackBox = new("erpin-snack-box", "에르핀의 간식상자", 15,
            ObstacleBreakRule.Hits, 0.2f, 1, FoodBoxWeights,
            new Color(0.6f, 0.9f, 0.55f), new Color(0.25f, 0.45f, 0.2f));

        public static readonly VariantSpec EshurBreadBox = new("eshur-bread-box", "에슈르의 빵상자", 10,
            ObstacleBreakRule.Hits, 0.35f, 2, FoodBoxWeights,
            new Color(0.93f, 0.75f, 0.5f), new Color(0.5f, 0.35f, 0.18f));

        public static readonly VariantSpec RicottaFoodBox = new("ricotta-food-box", "리코타의 음식상자", 5,
            ObstacleBreakRule.Hits, 0.5f, 3, FoodBoxWeights,
            new Color(1f, 0.97f, 0.88f), new Color(0.6f, 0.55f, 0.45f));

        // A bomb or a key is the cost, so the vault always pays out: three pickups of gold or keys, and rarely one
        // spell or artifact on top.
        public static readonly VariantSpec SistVault = new("sist-vault", "시스트의 금고", 5,
            ObstacleBreakRule.BombOrKey, 1f, 3, new[] { ("gold", 65), ("key", 35) },
            new Color(0.35f, 0.38f, 0.5f), new Color(0.15f, 0.16f, 0.22f),
            VaultRareItemChance, VaultRareArtifactShare);

        // Obstacle-6. Two hits or a bomb arm a short fuse, then it explodes exactly like a player bomb: it hurts
        // enemies and the player, breaks obstacles (so boxes chain) and opens secret walls, diamond chests and vaults.
        public static readonly VariantSpec ExplosiveBox = new("explosive-box", "폭발 상자", 15,
            ObstacleBreakRule.Hits, 0f, 1, null,
            new Color(0.9f, 0.25f, 0.2f), new Color(0.45f, 0.1f, 0.08f),
            requiredHits: ExplosiveBoxHits, explosionChance: 1f);

        // One seeded outcome: an explosion (25%), five 쥬비 (15%), three pickups (about 50%) or nothing.
        public static readonly VariantSpec ShadyRandomBox = new("shady-random-box", "셰이디의 랜덤박스", 7,
            ObstacleBreakRule.Hits, RandomBoxDropChance, 3,
            new[] { ("gold", 30), ("heart", 20), ("sp", 20), ("key", 15), ("bomb", 15) },
            new Color(0.5f, 0.3f, 0.7f), new Color(0.22f, 0.12f, 0.34f),
            explosionChance: RandomBoxExplosionChance, enemyChance: RandomBoxEnemyChance,
            enemyCount: RandomBoxEnemyCount);

        // The rarest kind: always two pickups, and one artifact on top a quarter of the time.
        public static readonly VariantSpec MayoCollectionBox = new("mayo-collection-box", "마요의 수집품 상자", 3,
            ObstacleBreakRule.Hits, 1f, 2,
            new[] { ("gold", 30), ("key", 25), ("bomb", 20), ("sp", 13), ("heart", 12) },
            new Color(0.2f, 0.75f, 0.8f), new Color(0.08f, 0.35f, 0.4f),
            CollectionBoxArtifactChance, 1f);

        public static readonly VariantSpec[] Variants =
        {
            GoldRock, MayoKeyBundle, ErpinSnackBox, EshurBreadBox, RicottaFoodBox, SistVault,
            ExplosiveBox, ShadyRandomBox, MayoCollectionBox,
        };

        public static IEnumerable<(string variantId, int weight)> ExpectedTableWeights() =>
            new[] { ("marie-bomb-box", MarieWeight) }
                .Concat(Variants.Select(spec => (spec.VariantId, spec.TableWeight)));

        [MenuItem("Trickal Fan Game/Week 23/Setup Obstacle-5 Resource Obstacles")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Obstacle-5 setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Opening the scene first keeps the assets built below loaded for the summary.
            RaiseRoomContentVersion();
            ObstacleVariantTable table = Week20Obstacle4Setup.EnsureVariantAssets();
            if (!table.TryValidate(out string error))
                throw new InvalidOperationException($"Obstacle-5 built an invalid obstacle variant table. {error}");
            Debug.Log("Obstacle-5 setup complete: the Fairy Kingdom special obstacle table holds " +
                      string.Join(", ", table.Entries.Select(entry => $"{entry.Variant.VariantId} {entry.Weight}")) +
                      $" at a {table.SpecialRoomChance:P0} room chance, at most one per room.");
        }

        public static string[] CreatedAssetPaths() => Variants
            .SelectMany(spec => spec.HasDropTable
                ? new[] { spec.VariantPath, spec.DropTablePath }
                : new[] { spec.VariantPath })
            .ToArray();

        // Every entry of the special obstacle table, built on the Marie bomb box that Obstacle-4 owns. Obstacle-4
        // setup calls this too, so re-running it keeps the Obstacle-5 kinds.
        public static ObstacleVariantEntry[] BuildEntries(ObstacleVariantDefinition marie)
        {
            if (marie == null) throw new ArgumentNullException(nameof(marie));
            List<ObstacleVariantEntry> entries = new() { new ObstacleVariantEntry(marie, MarieWeight) };
            foreach (VariantSpec spec in Variants)
                entries.Add(new ObstacleVariantEntry(EnsureVariant(spec), spec.TableWeight));
            return entries.ToArray();
        }

        private static ObstacleVariantDefinition EnsureVariant(VariantSpec spec)
        {
            ResourceDropTable dropTable = spec.HasDropTable
                ? Week17Resource3Setup.EnsureTable(spec.DropTablePath, spec.DropChance, spec.DropWeights,
                    $"Configure {spec.VariantId} drops")
                : null;
            ObstacleVariantDefinition variant =
                AssetDatabase.LoadAssetAtPath<ObstacleVariantDefinition>(spec.VariantPath);
            if (variant == null)
            {
                variant = ScriptableObject.CreateInstance<ObstacleVariantDefinition>();
                AssetDatabase.CreateAsset(variant, spec.VariantPath);
            }

            Undo.RecordObject(variant, $"Configure {spec.VariantId} obstacle variant");
            variant.Configure(spec.VariantId, spec.RequiredHits, dropTable, spec.IntactColor, spec.CrackedColor);
            variant.ConfigureBreakAndDrops(spec.BreakRule, spec.DropCount);
            PlacedBomb bomb = spec.ExplosionChance > 0f
                ? AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special2Setup.PlacedBombPrefabPath)
                    ?.GetComponent<PlacedBomb>()
                : null;
            if (spec.ExplosionChance > 0f && bomb == null)
                throw new InvalidOperationException($"{spec.VariantId} needs the Special-2 placed bomb Prefab.");
            variant.ConfigureExplosion(spec.ExplosionChance, ExplosionFuse, bomb);
            variant.ConfigureEnemies(spec.EnemyChance, spec.EnemyCount,
                spec.EnemyChance > 0f ? Week23Enemy6Setup.EnsurePrefab() : null);
            if (spec.RareItemChance > 0f)
            {
                SingleUseItemPickup spellPickup = AssetDatabase
                    .LoadAssetAtPath<GameObject>(Week21Slot0Setup.PickupPrefabPath)?.GetComponent<SingleUseItemPickup>();
                ItemPickup artifactPickup = AssetDatabase
                    .LoadAssetAtPath<GameObject>(Week22Flight0Setup.ArtifactPickupPrefabPath)?.GetComponent<ItemPickup>();
                if (spellPickup == null || artifactPickup == null)
                    throw new InvalidOperationException(
                        $"{spec.VariantId} needs the Slot-0 single-use pickup and the artifact pickup Prefabs.");
                variant.ConfigureRareItems(spec.RareItemChance, spec.RareArtifactShare,
                    Week22Chest1Setup.FindSingleUseItems(ItemKind.SingleUseSpell), spellPickup, artifactPickup);
            }
            else
            {
                variant.ConfigureRareItems(0f, 0f, Array.Empty<ItemDefinition>(), null, null);
            }

            if (!variant.TryValidate(out string error))
                throw new InvalidOperationException($"Obstacle-5 built an invalid variant. {error}");
            EditorUtility.SetDirty(variant);
            return variant;
        }

        private static void RaiseRoomContentVersion()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null) throw new InvalidOperationException("Obstacle-5 requires the Game Scene generator.");
            if (generator.RoomContentVersion >= RoomContentVersion) return;
            Undo.RecordObject(generator, "Raise Obstacle-5 room content version");
            generator.ConfigureTemplates(RoomContentVersion, generator.RoomTemplates.ToArray());
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Obstacle-5 setup.");
        }
    }
}
