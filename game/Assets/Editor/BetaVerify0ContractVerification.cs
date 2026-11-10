using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TrickalFanGame.Character;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Verify-0 (docs/30 §3): compares every Unity item asset with the Backend item catalog
    // (backend/src/contracts/item-catalog.ts) and the Unity characters with the Backend seed, then writes the
    // differences to Logs/Verify0/contract-diff.md. Fails on a missing ID or a different rarity, isActive, maxStacks
    // or effect. A different display name is listed but does not fail.
    public static class BetaVerify0ContractVerification
    {
        private const float Tolerance = 0.0001f;

        private static readonly string[] EffectNumberFields =
        {
            "magnitude", "healthThreshold", "secondaryMagnitude", "integerAmount", "minimumDistance", "maximumDistance",
            "radius", "intervalSeconds", "spreadAngleDegrees", "scaleMultiplier", "durationSeconds",
        };

        [MenuItem("Trickal Fan Game/Beta/Verify Verify-0 Unity Backend Contract")]
        public static void Verify()
        {
            string backendRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "backend"));
            string catalogPath = Path.Combine(backendRoot, "src", "contracts", "item-catalog.ts");
            string seedPath = Path.Combine(backendRoot, "prisma", "seed.ts");
            Assert(File.Exists(catalogPath), $"The Backend item catalog is missing at {catalogPath}.");
            Assert(File.Exists(seedPath), $"The Backend seed is missing at {seedPath}.");

            CatalogRead backend = ReadBackendCatalog(File.ReadAllText(catalogPath, Encoding.UTF8));
            Dictionary<string, ItemDefinition> unity = LoadUnityItems(out List<string> failures);
            var warnings = new List<string>();

            foreach (string id in unity.Keys.Except(backend.Entries.Keys).Except(backend.IdsOnly).OrderBy(id => id))
                failures.Add($"`{id}`: Unity에만 있다(Backend 카탈로그에 없음).");
            foreach (string id in backend.Entries.Keys.Except(unity.Keys).OrderBy(id => id))
                failures.Add($"`{id}`: Backend 카탈로그에만 있다(Unity 에셋 없음).");
            // The `.map` spread holds retired items the Backend keeps for past Run records.
            foreach (string id in backend.IdsOnly.OrderBy(id => id))
                warnings.Add(unity.ContainsKey(id)
                    ? $"`{id}`: Backend가 `.map`으로 만든 레거시 항목이라 ID만 대조했다."
                    : $"`{id}`: Backend에만 있는 레거시 항목(과거 전적 보존용).");

            foreach (KeyValuePair<string, Dictionary<string, object>> pair in backend.Entries.OrderBy(pair => pair.Key))
            {
                if (!unity.TryGetValue(pair.Key, out ItemDefinition item)) continue;
                CompareItem(pair.Key, pair.Value, item, failures, warnings);
            }

            string seed = File.ReadAllText(seedPath, Encoding.UTF8);
            string[] backendCharacters = Regex.Matches(seed, @"const\s+character\s*=\s*\{\s*id:\s*'(?<id>[^']+)'")
                .Cast<Match>().Select(match => match.Groups["id"].Value).ToArray();
            Assert(backendCharacters.Length > 0, "No character was found in backend/prisma/seed.ts.");
            string[] unityCharacters = AssetDatabase.FindAssets("t:CharacterDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(character => character != null).Select(character => character.CharacterId).ToArray();
            foreach (string id in unityCharacters.Except(backendCharacters))
                failures.Add($"캐릭터 `{id}`: Unity에만 있다(Backend seed에 없음).");
            foreach (string id in backendCharacters.Except(unityCharacters))
                warnings.Add($"캐릭터 `{id}`: Backend seed에만 있다.");

            string reportPath = WriteReport(unity.Count, backend, unityCharacters, failures, warnings);
            Assert(failures.Count == 0,
                $"{failures.Count} Unity↔Backend contract difference(s), see {reportPath}. First: {failures.FirstOrDefault()}");
            Debug.Log($"Verify-0 contract verification passed: {unity.Count} Unity items match the Backend catalog " +
                      $"({backend.IdsOnly.Count} legacy by ID only) and characters match the seed. " +
                      $"{warnings.Count} note(s) in {reportPath}.");
        }

        private static Dictionary<string, ItemDefinition> LoadUnityItems(out List<string> failures)
        {
            failures = new List<string>();
            var items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null) continue;
                if (string.IsNullOrWhiteSpace(item.ItemId))
                    failures.Add($"`{path}`: 아이템 ID가 비어 있다.");
                else if (items.ContainsKey(item.ItemId))
                    failures.Add($"`{item.ItemId}`: 정의 에셋이 둘 이상이다(`{path}`).");
                else
                    items.Add(item.ItemId, item);
            }

            return items;
        }

        private static void CompareItem(string id, Dictionary<string, object> backend, ItemDefinition item,
            List<string> failures, List<string> warnings)
        {
            string rarity = (string)backend["rarity"];
            if (!string.Equals(rarity, item.Rarity.ToString(), StringComparison.OrdinalIgnoreCase))
                failures.Add($"`{id}` 등급: Unity {item.Rarity}, Backend {rarity}.");
            if ((bool)backend["isActive"] != item.IsActive)
                failures.Add($"`{id}` isActive: Unity {item.IsActive}, Backend {backend["isActive"]}.");
            int maxStacks = (int)(double)backend["maxStacks"];
            if (maxStacks != item.MaxStacks)
                failures.Add($"`{id}` maxStacks: Unity {item.MaxStacks}, Backend {maxStacks}.");
            if ((string)backend["name"] != item.DisplayName)
                warnings.Add($"`{id}` 이름: Unity `{item.DisplayName}`, Backend `{backend["name"]}`.");

            var backendEffects = ((List<object>)backend["effects"]).Cast<Dictionary<string, object>>().ToList();
            IReadOnlyList<ItemEffectEntry> unityEffects = item.Effects;
            if (backendEffects.Count != unityEffects.Count)
            {
                failures.Add($"`{id}` 효과 수: Unity {unityEffects.Count} ({EffectNames(unityEffects)}), " +
                             $"Backend {backendEffects.Count} ({string.Join(", ", backendEffects.Select(e => e["type"]))}).");
                return;
            }

            for (int i = 0; i < backendEffects.Count; i++)
            {
                Dictionary<string, object> effect = backendEffects[i];
                ItemEffectEntry entry = unityEffects[i];
                if ((string)effect["type"] != entry.EffectType.ToString())
                {
                    failures.Add($"`{id}` 효과 {i + 1}: Unity {entry.EffectType}, Backend {effect["type"]}.");
                    continue;
                }

                foreach (string field in EffectNumberFields)
                {
                    double expected = effect.TryGetValue(field, out object value) ? (double)value : 0d;
                    double actual = UnityEffectNumber(entry, field);
                    if (Math.Abs(expected - actual) > Tolerance)
                        failures.Add($"`{id}` 효과 {i + 1} {entry.EffectType}.{field}: Unity {actual.ToString(CultureInfo.InvariantCulture)}, " +
                                     $"Backend {expected.ToString(CultureInfo.InvariantCulture)}.");
                }
            }
        }

        private static string EffectNames(IReadOnlyList<ItemEffectEntry> effects) =>
            string.Join(", ", effects.Select(effect => effect.EffectType.ToString()));

        private static double UnityEffectNumber(ItemEffectEntry entry, string field) => field switch
        {
            "magnitude" => entry.Magnitude,
            "healthThreshold" => entry.HealthThreshold,
            "secondaryMagnitude" => entry.SecondaryMagnitude,
            "integerAmount" => entry.IntegerAmount,
            "minimumDistance" => entry.MinimumDistance,
            "maximumDistance" => entry.MaximumDistance,
            "radius" => entry.Radius,
            "intervalSeconds" => entry.IntervalSeconds,
            "spreadAngleDegrees" => entry.SpreadAngleDegrees,
            "scaleMultiplier" => entry.ScaleMultiplier,
            "durationSeconds" => entry.DurationSeconds,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };

        private static string WriteReport(int unityCount, CatalogRead backend, string[] characters,
            List<string> failures, List<string> warnings)
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", BetaVerify0Runner.ReportFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "contract-diff.md");
            var text = new StringBuilder();
            text.AppendLine("# Verify-0 Unity↔Backend 계약 차이");
            text.AppendLine();
            text.AppendLine($"- 실행: {DateTime.Now:yyyy-MM-dd HH:mm}");
            text.AppendLine($"- Unity 아이템 {unityCount}개, Backend 카탈로그 {backend.Entries.Count + backend.IdsOnly.Count}개" +
                            $"(그중 ID만 대조 {backend.IdsOnly.Count}개), Unity 캐릭터 {string.Join(", ", characters)}");
            text.AppendLine($"- 차이(FAIL) {failures.Count}개, 참고(WARN) {warnings.Count}개");
            text.AppendLine();
            text.AppendLine("## 차이");
            text.AppendLine();
            foreach (string failure in failures) text.AppendLine("- " + failure);
            if (failures.Count == 0) text.AppendLine("없음");
            text.AppendLine();
            text.AppendLine("## 참고");
            text.AppendLine();
            foreach (string warning in warnings) text.AppendLine("- " + warning);
            if (warnings.Count == 0) text.AppendLine("없음");
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            return path;
        }

        // Reads ITEM_CATALOG as data. Plain object entries are compared in full. A `...[ ... ].map(...)` spread is
        // read for its tuple IDs only, because its fields come from code.
        private static CatalogRead ReadBackendCatalog(string source)
        {
            int start = source.IndexOf("ITEM_CATALOG", StringComparison.Ordinal);
            Assert(start >= 0, "ITEM_CATALOG was not found in the Backend item catalog.");
            var reader = new LiteralReader(source, source.IndexOf('[', start));
            var read = new CatalogRead();
            reader.Expect('[');
            while (!reader.TryConsume(']'))
            {
                if (reader.TryConsume("..."))
                {
                    var tuples = (List<object>)reader.ReadValue();
                    foreach (List<object> tuple in tuples.Cast<List<object>>()) read.IdsOnly.Add((string)tuple[0]);
                    reader.SkipCall();
                }
                else
                {
                    var entry = (Dictionary<string, object>)reader.ReadValue();
                    string id = (string)entry["id"];
                    Assert(!read.Entries.ContainsKey(id) && !read.IdsOnly.Contains(id),
                        $"Backend catalog lists `{id}` twice.");
                    read.Entries.Add(id, entry);
                }

                reader.TryConsume(',');
            }

            Assert(read.Entries.Count > 0, "No entry was read from ITEM_CATALOG.");
            return read;
        }

        private sealed class CatalogRead
        {
            public readonly Dictionary<string, Dictionary<string, object>> Entries = new(StringComparer.Ordinal);
            public readonly HashSet<string> IdsOnly = new(StringComparer.Ordinal);
        }

        // A small reader for the TypeScript literal subset the catalog uses: objects, arrays, single-quoted strings,
        // numbers, booleans and `('X' as const)`.
        private sealed class LiteralReader
        {
            private readonly string text;
            private int position;

            public LiteralReader(string text, int position)
            {
                this.text = text;
                this.position = position;
            }

            public void Expect(char value)
            {
                SkipSpace();
                Assert(position < text.Length && text[position] == value,
                    $"Backend catalog: expected '{value}' near {Context()}.");
                position++;
            }

            public bool TryConsume(char value) => TryConsume(value.ToString());

            public bool TryConsume(string value)
            {
                SkipSpace();
                if (string.CompareOrdinal(text, position, value, 0, value.Length) != 0) return false;
                position += value.Length;
                return true;
            }

            public object ReadValue()
            {
                SkipSpace();
                char current = text[position];
                if (current == '{') return ReadObject();
                if (current == '[') return ReadArray();
                if (current == '\'') return ReadString();
                if (TryConsume("true")) return true;
                if (TryConsume("false")) return false;
                Match number = Regex.Match(text.Substring(position, Math.Min(40, text.Length - position)),
                    @"^-?\d[\d_]*(\.\d+)?([eE][-+]?\d+)?");
                Assert(number.Success, $"Backend catalog: unsupported value near {Context()}.");
                position += number.Length;
                return double.Parse(number.Value.Replace("_", string.Empty), CultureInfo.InvariantCulture);
            }

            public void SkipCall()
            {
                // `.map((...) => ({ ... }))`: skip to the matching closing parenthesis.
                Assert(TryConsume(".map"), $"Backend catalog: expected .map after a spread near {Context()}.");
                SkipSpace();
                int depth = 0;
                do
                {
                    char current = text[position++];
                    if (current == '\'') { position--; ReadString(); continue; }
                    if (current == '(') depth++;
                    else if (current == ')') depth--;
                } while (depth > 0);
            }

            private Dictionary<string, object> ReadObject()
            {
                Expect('{');
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                while (!TryConsume('}'))
                {
                    SkipSpace();
                    Match key = Regex.Match(text.Substring(position, Math.Min(64, text.Length - position)), @"^[A-Za-z_]\w*");
                    Assert(key.Success, $"Backend catalog: expected a key near {Context()}.");
                    position += key.Length;
                    Expect(':');
                    result[key.Value] = ReadValue();
                    TryConsume(',');
                }

                return result;
            }

            private List<object> ReadArray()
            {
                Expect('[');
                var result = new List<object>();
                while (!TryConsume(']'))
                {
                    result.Add(ReadValue());
                    TryConsume(',');
                }

                return result;
            }

            private string ReadString()
            {
                Expect('\'');
                var value = new StringBuilder();
                while (text[position] != '\'')
                {
                    if (text[position] == '\\') position++;
                    value.Append(text[position++]);
                }

                position++;
                return value.ToString();
            }

            private void SkipSpace()
            {
                while (position < text.Length)
                {
                    if (char.IsWhiteSpace(text[position])) position++;
                    else if (string.CompareOrdinal(text, position, "//", 0, 2) == 0)
                        position = Math.Max(position + 2, text.IndexOf('\n', position));
                    else break;
                }
            }

            private string Context() => text.Substring(position, Math.Min(40, text.Length - position)).Replace("\n", " ");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[Verify-0] " + message);
        }
    }
}
