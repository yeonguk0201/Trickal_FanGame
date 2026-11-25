using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public enum RoomLayoutCellKind
    {
        Empty,
        // A rock whose kind the room's variant table decides from the seed.
        Rock,
        // An obstacle whose kind the Layout fixes.
        FixedObstacle,
        Tree,
        Pit,
        SpawnPoint,
        Chest,
        // Trap-1: a trap without a collider (바닥 가시, 주기 가시, 장판, 거미줄).
        FloorTrap,
        // Trap-1: a trap that is a fixed-kind obstacle (불, 가시 돌, 독버섯).
        ObstacleTrap,
    }

    // One symbol of the room text format. A new cell kind needs one entry here and one placement branch in the
    // importer.
    public readonly struct RoomLayoutSymbol
    {
        public RoomLayoutSymbol(char symbol, RoomLayoutCellKind kind, string description, string variantId = null,
            ChestKind chestKind = ChestKind.Normal, int spawnIndex = -1,
            RoomTrapKind trapKind = RoomTrapKind.FloorSpikes, bool secondPhase = false)
        {
            Symbol = symbol;
            Kind = kind;
            Description = description;
            VariantId = variantId;
            ChestKind = chestKind;
            SpawnIndex = spawnIndex;
            TrapKind = trapKind;
            SecondPhase = secondPhase;
        }

        public char Symbol { get; }
        public RoomLayoutCellKind Kind { get; }
        public string Description { get; }
        public string VariantId { get; }
        public ChestKind ChestKind { get; }
        public int SpawnIndex { get; }
        // Read only when IsTrap.
        public RoomTrapKind TrapKind { get; }
        // 주기 가시 only: the B phase, half a cycle after A.
        public bool SecondPhase { get; }
        public bool IsTrap => Kind is RoomLayoutCellKind.FloorTrap or RoomLayoutCellKind.ObstacleTrap;
        public bool IsObstacle => Kind is RoomLayoutCellKind.Rock or RoomLayoutCellKind.FixedObstacle or
            RoomLayoutCellKind.Tree or RoomLayoutCellKind.ObstacleTrap;
    }

    // The fixed grid of a Room Profile. One symbol is one 1x1 cell and the grid is centered on the room, so cell
    // centers land on the 0.5 Layout grid. The grid is exactly the walkable floor inside the wall boundaries, which
    // is narrower than the wall colliders' inner edge.
    public sealed class RoomLayoutProfile
    {
        public RoomLayoutProfile(string profileId, string profilePath, Vector2 roomSize, string sourcePrefabPath,
            int columns, int rows, int spawnPointCount)
        {
            ProfileId = profileId;
            ProfilePath = profilePath;
            RoomSize = roomSize;
            SourcePrefabPath = sourcePrefabPath;
            Columns = columns;
            Rows = rows;
            SpawnPointCount = spawnPointCount;
            Floor = FairyVillageArtworkSetup.WalkableFloor(roomSize);
        }

        public string ProfileId { get; }
        public string ProfilePath { get; }
        public Vector2 RoomSize { get; }
        public string SourcePrefabPath { get; }
        public int Columns { get; }
        public int Rows { get; }
        // The Encounter-4 SpawnPoint count of the profile.
        public int SpawnPointCount { get; }
        // Where actors can stand; the wall boundaries are built on its edges.
        public Rect Floor { get; }
        public Rect GridRect => new(-Columns * 0.5f, -Rows * 0.5f, Columns, Rows);

        public Vector2 CellCenter(int column, int row) =>
            new(column - (Columns - 1) * 0.5f, (Rows - 1) * 0.5f - row);

        public Rect CellRect(int column, int row, int width = 1, int height = 1)
        {
            Vector2 topLeft = CellCenter(column, row) + new Vector2(-0.5f, 0.5f);
            return Rect.MinMaxRect(topLeft.x, topLeft.y - height, topLeft.x + width, topLeft.y);
        }
    }

    public readonly struct RoomLayoutCell
    {
        public RoomLayoutCell(int column, int row, RoomLayoutSymbol symbol, Vector2 center)
        {
            Column = column;
            Row = row;
            Symbol = symbol;
            Center = center;
        }

        public int Column { get; }
        public int Row { get; }
        public RoomLayoutSymbol Symbol { get; }
        public Vector2 Center { get; }
        // Position-based, so editing one cell never renames another cell's obstacle, pit or chest.
        public string Id(string prefix) => $"{prefix}-c{Column:00}-r{Row:00}";
    }

    public readonly struct RoomLayoutPit
    {
        public RoomLayoutPit(string pitId, Rect bounds)
        {
            PitId = pitId;
            Bounds = bounds;
        }

        public string PitId { get; }
        public Rect Bounds { get; }
    }

    public sealed class RoomLayoutDocument
    {
        public string Id { get; internal set; }
        public RoomLayoutProfile Profile { get; internal set; }
        public int Weight { get; internal set; } = RoomTemplateDefinition.DefaultSelectionWeight;
        public int Difficulty { get; internal set; }
        public int MinimumFloor { get; internal set; } = 1;
        public int MaximumFloor { get; internal set; } = 99;
        public string Note { get; internal set; } = string.Empty;
        // Layout-Wave: wave1..wave6 in order. Empty keeps the old rule (the rolled Encounter's waves).
        public RoomLayoutWave[] Waves { get; internal set; } = Array.Empty<RoomLayoutWave>();
        // Rows top to bottom, each exactly Profile.Columns symbols.
        public string[] Rows { get; internal set; } = Array.Empty<string>();
        // Header and grid with line endings normalized; the importer hashes it to skip unchanged rooms.
        public string NormalizedText { get; internal set; } = string.Empty;

        public IEnumerable<RoomLayoutCell> Cells()
        {
            for (int row = 0; row < Rows.Length; row++)
            for (int column = 0; column < Rows[row].Length; column++)
                yield return new RoomLayoutCell(column, row, RoomLayoutFormat.Symbols[Rows[row][column]],
                    Profile.CellCenter(column, row));
        }

        public IEnumerable<RoomLayoutCell> CellsOf(RoomLayoutCellKind kind) =>
            Cells().Where(cell => cell.Symbol.Kind == kind);

        public IEnumerable<RoomLayoutCell> Obstacles => Cells().Where(cell => cell.Symbol.IsObstacle);

        public IEnumerable<RoomLayoutCell> Traps => Cells().Where(cell => cell.Symbol.IsTrap);

        public Vector2[] SpawnPoints => CellsOf(RoomLayoutCellKind.SpawnPoint)
            .OrderBy(cell => cell.Symbol.SpawnIndex).Select(cell => cell.Center).ToArray();

        // Pit cells merge into as few rectangles as a top-left greedy sweep finds: each unclaimed pit cell grows
        // right, then down while every cell of the row span is an unclaimed pit.
        public List<RoomLayoutPit> Pits()
        {
            List<RoomLayoutPit> pits = new();
            bool[,] claimed = new bool[Profile.Columns, Rows.Length];
            bool IsFreePit(int column, int row) =>
                !claimed[column, row] && RoomLayoutFormat.Symbols[Rows[row][column]].Kind == RoomLayoutCellKind.Pit;
            for (int row = 0; row < Rows.Length; row++)
            for (int column = 0; column < Profile.Columns; column++)
            {
                if (!IsFreePit(column, row)) continue;
                int width = 1;
                while (column + width < Profile.Columns && IsFreePit(column + width, row)) width++;
                int height = 1;
                while (row + height < Rows.Length &&
                       Enumerable.Range(column, width).All(candidate => IsFreePit(candidate, row + height)))
                {
                    height++;
                }

                for (int y = row; y < row + height; y++)
                for (int x = column; x < column + width; x++)
                    claimed[x, y] = true;
                pits.Add(new RoomLayoutPit($"pit-c{column:00}-r{row:00}", Profile.CellRect(column, row, width, height)));
            }

            return pits;
        }
    }

    // The room text format (T7): a `key: value` header, a `grid:` line, then one text row per grid row.
    public static class RoomLayoutFormat
    {
        public const string FileSuffix = ".room.txt";
        public const string GridMarker = "grid:";
        public const string CommentPrefix = "//";
        public const int MaximumWeight = 1000;
        public const int MaximumDifficulty = 5;

        public static readonly IReadOnlyDictionary<char, RoomLayoutSymbol> Symbols = BuildSymbols();

        public static readonly RoomLayoutProfile[] Profiles =
        {
            // The whole-unit size of FairyVillageArtworkSetup.WalkableFloor per profile.
            new(Week14Room3Setup.SmallProfileId, Week14Room3Setup.SmallProfilePath, Week14Room3Setup.SmallSize,
                Week14Room3Setup.SmallPrefabPath, 10, 5, 4),
            new(Week14Room1Setup.BasicProfileId, Week14Room1Setup.BasicProfilePath, RoomLayout.RoomSize,
                Week8GridFloorSetup.PrefabPath, 13, 7, 5),
            new(Week14Room3Setup.WideProfileId, Week14Room3Setup.WideProfilePath, Week14Room3Setup.WideSize,
                Week14Room3Setup.WidePrefabPath, 21, 7, 5),
            new(Week14Room6Setup.TallProfileId, Week14Room6Setup.TallProfilePath, Week14Room6Setup.TallSize,
                Week14Room6Setup.TallPrefabPath, 13, 11, 5),
            new(Week14Room6Setup.LargeProfileId, Week14Room6Setup.LargeProfilePath, Week14Room6Setup.LargeSize,
                Week14Room6Setup.LargePrefabPath, 21, 11, 6),
        };

        // Layout-Wave: the enemy names a wave line uses, kebab-case of EncounterEnemyRole (stable IDs; append only).
        public const string AutoWave = "auto";
        public static readonly IReadOnlyDictionary<string, EncounterEnemyRole> EnemyIds = BuildEnemyIds();

        public static string EnemyId(EncounterEnemyRole role) =>
            System.Text.RegularExpressions.Regex.Replace(role.ToString(), "(?<=[a-z])([A-Z])", "-$1").ToLowerInvariant();

        private static Dictionary<string, EncounterEnemyRole> BuildEnemyIds()
        {
            Dictionary<string, EncounterEnemyRole> ids = new(StringComparer.Ordinal);
            foreach (EncounterEnemyRole role in Enum.GetValues(typeof(EncounterEnemyRole)))
                if (role != EncounterEnemyRole.Boss) ids.Add(EnemyId(role), role);
            return ids;
        }

        public static string WaveKey(int waveNumber) => $"wave{waveNumber}";

        // Layout-Wave-2: the variant names after '!' (stable; append only). No '!' keeps the floor's roll.
        public static readonly IReadOnlyDictionary<string, RoomLayoutVariant> VariantIds =
            new Dictionary<string, RoomLayoutVariant>(StringComparer.Ordinal)
            {
                ["normal"] = RoomLayoutVariant.Normal,
                ["elite"] = RoomLayoutVariant.Elite,
                ["champion"] = RoomLayoutVariant.Champion,
                ["champion-puddle"] = RoomLayoutVariant.ChampionPuddle,
                ["champion-shots"] = RoomLayoutVariant.ChampionShots,
                ["champion-blast"] = RoomLayoutVariant.ChampionBlast,
                ["champion-undying"] = RoomLayoutVariant.ChampionUndying,
            };

        public static string VariantId(RoomLayoutVariant variant) =>
            VariantIds.FirstOrDefault(entry => entry.Value == variant).Key;

        public const int MaximumWaveEntryCount = 12;

        // "auto", or entries split by commas: "chaser*3, sniper|ranged*2" places three 불효자손-광기 and two enemies
        // each picked from 저격·원거리. A missing "*n" means one. Layout-Wave-2: "!elite" fixes the entry's variant
        // and "@5/6" limits it to SpawnPoints 5 and 6 (at least as many points as enemies):
        // "sniper*2!champion@5/6".
        public static bool TryParseWave(string value, out RoomLayoutWave wave, out string error)
        {
            wave = RoomLayoutWave.Auto;
            string text = (value ?? string.Empty).Trim();
            if (string.Equals(text, AutoWave, StringComparison.Ordinal))
            {
                error = null;
                return true;
            }

            List<RoomLayoutWaveEntry> entries = new();
            foreach (string rawEntry in text.Split(','))
            {
                string entry = rawEntry.Trim();
                if (entry.Length == 0)
                {
                    error = $"Wave '{text}' has an empty entry. Use 'auto' or 'chaser*3, sniper'.";
                    return false;
                }

                int[] points = Array.Empty<int>();
                int at = entry.IndexOf('@');
                if (at >= 0)
                {
                    string[] parts = entry.Substring(at + 1).Split('/');
                    List<int> parsed = new();
                    foreach (string part in parts)
                    {
                        if (!int.TryParse(part.Trim(), out int point) || point < 1 || parsed.Contains(point - 1))
                        {
                            error = $"Wave entry '{entry}' needs distinct SpawnPoint numbers after '@', like '@5/6'.";
                            return false;
                        }

                        parsed.Add(point - 1);
                    }

                    points = parsed.ToArray();
                    entry = entry.Substring(0, at).Trim();
                }

                RoomLayoutVariant variant = RoomLayoutVariant.Random;
                int bang = entry.IndexOf('!');
                if (bang >= 0)
                {
                    string variantId = entry.Substring(bang + 1).Trim();
                    if (!VariantIds.TryGetValue(variantId, out variant))
                    {
                        error = $"Unknown variant '{variantId}'. Use one of: {string.Join(", ", VariantIds.Keys)}.";
                        return false;
                    }

                    entry = entry.Substring(0, bang).Trim();
                }

                int count = 1;
                int star = entry.IndexOf('*');
                if (star >= 0)
                {
                    if (!int.TryParse(entry.Substring(star + 1).Trim(), out count) || count < 1 ||
                        count > MaximumWaveEntryCount)
                    {
                        error = $"Wave entry '{entry}' needs a count from 1 to {MaximumWaveEntryCount} after '*'.";
                        return false;
                    }

                    entry = entry.Substring(0, star).Trim();
                }

                if (points.Length > 0 && points.Length < count)
                {
                    error = $"Wave entry '{rawEntry.Trim()}' puts {count} enemies on {points.Length} SpawnPoints.";
                    return false;
                }

                List<EncounterEnemyCandidate> candidates = new();
                foreach (string rawId in entry.Split('|'))
                {
                    string id = rawId.Trim();
                    if (!EnemyIds.TryGetValue(id, out EncounterEnemyRole role))
                    {
                        error = $"Unknown enemy '{id}'. Use one of: {string.Join(", ", EnemyIds.Keys)}.";
                        return false;
                    }

                    if (candidates.Any(candidate => candidate.EnemyRole == role))
                    {
                        error = $"Wave entry '{rawEntry.Trim()}' lists '{id}' twice.";
                        return false;
                    }

                    if (!RoomTemplateDefinition.LayoutVariantAllows(variant, role))
                    {
                        error = $"Wave entry '{rawEntry.Trim()}': '{id}' cannot be {VariantId(variant)}.";
                        return false;
                    }

                    candidates.Add(new EncounterEnemyCandidate(role, 1));
                }

                entries.Add(new RoomLayoutWaveEntry(new EncounterSpawnRule(candidates.ToArray(), count, null, "all"),
                    points, variant));
            }

            wave = new RoomLayoutWave(false, entries.ToArray());
            error = null;
            return true;
        }

        public static string FormatWave(RoomLayoutWave wave) => wave.IsAuto
            ? AutoWave
            : string.Join(", ", wave.Entries.Select(entry =>
                string.Join("|", entry.Rule.Candidates.Select(candidate => EnemyId(candidate.EnemyRole))) +
                (entry.Rule.Count > 1 ? $"*{entry.Rule.Count}" : string.Empty) +
                (entry.Variant != RoomLayoutVariant.Random ? $"!{VariantId(entry.Variant)}" : string.Empty) +
                (entry.IsPinned ? "@" + string.Join("/", entry.SpawnPoints.Select(point => point + 1)) : string.Empty)));

        public static RoomLayoutProfile FindProfile(string profileId) =>
            Profiles.FirstOrDefault(profile => string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal));

        // fileName is the file's name without its folder; the room ID must match it.
        public static bool TryParse(string text, string fileName, out RoomLayoutDocument document, out string error)
        {
            document = null;
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(FileSuffix, StringComparison.Ordinal))
            {
                error = $"A room file name must end with '{FileSuffix}'.";
                return false;
            }

            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            Dictionary<string, string> header = new(StringComparer.Ordinal);
            int gridStart = -1;
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length == 0 || line.StartsWith(CommentPrefix, StringComparison.Ordinal)) continue;
                if (line == GridMarker)
                {
                    gridStart = index + 1;
                    break;
                }

                int separator = line.IndexOf(':');
                if (separator <= 0)
                {
                    error = $"Line {index + 1} is not a 'key: value' header line: '{line}'.";
                    return false;
                }

                string key = line.Substring(0, separator).Trim();
                if (!header.TryAdd(key, line.Substring(separator + 1).Trim()))
                {
                    error = $"Header key '{key}' appears twice.";
                    return false;
                }
            }

            if (gridStart < 0)
            {
                error = $"The file has no '{GridMarker}' line.";
                return false;
            }

            RoomLayoutDocument parsed = new();
            if (!TryReadHeader(header, fileName, parsed, out error)) return false;

            List<string> rows = new();
            for (int index = gridStart; index < lines.Length; index++) rows.Add(lines[index].TrimEnd());
            while (rows.Count > 0 && rows[^1].Length == 0) rows.RemoveAt(rows.Count - 1);
            if (!TryReadGrid(rows, gridStart, parsed, out error)) return false;

            if (!header.ContainsKey("difficulty"))
            {
                bool hasTerrain = parsed.Obstacles.Any() || parsed.CellsOf(RoomLayoutCellKind.Pit).Any() ||
                                  parsed.Traps.Any();
                parsed.Difficulty = hasTerrain ? Week19Difficulty1Setup.ObstacleLayoutModifier : 0;
            }

            parsed.NormalizedText = string.Join("\n", header.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => $"{entry.Key}: {entry.Value}").Append(GridMarker).Concat(parsed.Rows));
            document = parsed;
            error = null;
            return true;
        }

        // The grid as text with SpawnPoints removed, for finding rooms that are the same Layout mirrored.
        public static string TerrainSignature(RoomLayoutDocument document, bool flipHorizontal, bool flipVertical)
        {
            IEnumerable<string> rows = document.Rows.Select(row =>
            {
                char[] cells = row.Select(symbol =>
                    Symbols[symbol].Kind == RoomLayoutCellKind.SpawnPoint ? '.' : symbol).ToArray();
                if (flipHorizontal) Array.Reverse(cells);
                return new string(cells);
            });
            return string.Join("\n", flipVertical ? rows.Reverse() : rows);
        }

        private static bool TryReadHeader(Dictionary<string, string> header, string fileName,
            RoomLayoutDocument document, out string error)
        {
            foreach (string key in header.Keys)
            {
                if (key is "id" or "profile" or "weight" or "difficulty" or "floors" or "note") continue;
                if (Enumerable.Range(1, RoomTemplateDefinition.MaximumLayoutWaves).Any(number => key == WaveKey(number)))
                    continue;
                error = $"Unknown header key '{key}'. Use id, profile, weight, difficulty, floors, note or " +
                        $"wave1~wave{RoomTemplateDefinition.MaximumLayoutWaves}.";
                return false;
            }

            if (!header.TryGetValue("id", out string id) || !header.TryGetValue("profile", out string profileId))
            {
                error = "The header needs 'id' and 'profile'.";
                return false;
            }

            if (id.Length == 0 || id.Any(character => (character < 'a' || character > 'z') &&
                                                      (character < '0' || character > '9') && character != '-'))
            {
                error = $"Room ID '{id}' must use lowercase ASCII letters, digits, or hyphens.";
                return false;
            }

            if (fileName != id + FileSuffix)
            {
                error = $"Room ID '{id}' must match its file name ('{id}{FileSuffix}').";
                return false;
            }

            document.Profile = FindProfile(profileId);
            if (document.Profile == null)
            {
                error = $"Unknown profile '{profileId}'. Use " +
                        $"{string.Join(", ", Profiles.Select(profile => profile.ProfileId))}.";
                return false;
            }

            if (!id.StartsWith(profileId + "-", StringComparison.Ordinal) || id.Length == profileId.Length + 1)
            {
                error = $"Room ID '{id}' must start with its profile ('{profileId}-').";
                return false;
            }

            document.Id = id;
            if (header.TryGetValue("weight", out string weight) &&
                (!int.TryParse(weight, out int parsedWeight) || parsedWeight < 1 || parsedWeight > MaximumWeight))
            {
                error = $"weight '{weight}' must be a whole number from 1 to {MaximumWeight}.";
                return false;
            }

            if (header.ContainsKey("weight")) document.Weight = int.Parse(header["weight"]);
            if (header.TryGetValue("difficulty", out string difficulty))
            {
                if (!int.TryParse(difficulty, out int parsedDifficulty) || parsedDifficulty < 0 ||
                    parsedDifficulty > MaximumDifficulty)
                {
                    error = $"difficulty '{difficulty}' must be a whole number from 0 to {MaximumDifficulty}.";
                    return false;
                }

                document.Difficulty = parsedDifficulty;
            }

            if (header.TryGetValue("floors", out string floors))
            {
                string[] parts = floors.Split('-');
                if (parts.Length is < 1 or > 2 || !int.TryParse(parts[0], out int minimum) ||
                    !int.TryParse(parts[^1], out int maximum) || minimum < 1 || maximum < minimum)
                {
                    error = $"floors '{floors}' must be one floor ('2') or a range ('1-3').";
                    return false;
                }

                document.MinimumFloor = minimum;
                document.MaximumFloor = maximum;
            }

            if (header.TryGetValue("note", out string note)) document.Note = note;

            // Layout-Wave: wave1, wave2, ... with no gap; none keeps the old rule.
            List<RoomLayoutWave> waves = new();
            for (int number = 1; number <= RoomTemplateDefinition.MaximumLayoutWaves; number++)
            {
                if (!header.TryGetValue(WaveKey(number), out string waveText)) continue;
                if (number != waves.Count + 1)
                {
                    error = $"'{WaveKey(number)}' needs '{WaveKey(waves.Count + 1)}' before it.";
                    return false;
                }

                if (!TryParseWave(waveText, out RoomLayoutWave wave, out error))
                {
                    error = $"{WaveKey(number)}: {error}";
                    return false;
                }

                int lastPoint = wave.Entries.SelectMany(entry => entry.SpawnPoints).DefaultIfEmpty(-1).Max();
                if (lastPoint >= document.Profile.SpawnPointCount)
                {
                    error = $"{WaveKey(number)}: SpawnPoint {lastPoint + 1} does not exist; " +
                            $"'{document.Profile.ProfileId}' has 1~{document.Profile.SpawnPointCount}.";
                    return false;
                }

                waves.Add(wave);
            }

            document.Waves = waves.ToArray();
            error = null;
            return true;
        }

        private static bool TryReadGrid(List<string> rows, int gridStart, RoomLayoutDocument document,
            out string error)
        {
            RoomLayoutProfile profile = document.Profile;
            if (rows.Count != profile.Rows)
            {
                error = $"Profile '{profile.ProfileId}' needs {profile.Rows} grid rows of {profile.Columns} symbols; " +
                        $"the file has {rows.Count} rows.";
                return false;
            }

            Dictionary<int, int> spawnCounts = new();
            for (int row = 0; row < rows.Count; row++)
            {
                if (rows[row].Length != profile.Columns)
                {
                    error = $"Grid row {row + 1} (line {gridStart + row + 1}) has {rows[row].Length} symbols; " +
                            $"profile '{profile.ProfileId}' needs {profile.Columns}.";
                    return false;
                }

                foreach (char symbol in rows[row])
                {
                    if (!Symbols.TryGetValue(symbol, out RoomLayoutSymbol entry))
                    {
                        error = $"Grid row {row + 1} uses unknown symbol '{symbol}'.";
                        return false;
                    }

                    if (entry.Kind != RoomLayoutCellKind.SpawnPoint) continue;
                    spawnCounts.TryGetValue(entry.SpawnIndex, out int count);
                    spawnCounts[entry.SpawnIndex] = count + 1;
                }
            }

            for (int index = 0; index < profile.SpawnPointCount; index++)
            {
                if (spawnCounts.TryGetValue(index, out int count) && count == 1) continue;
                error = $"Profile '{profile.ProfileId}' needs SpawnPoints 1~{profile.SpawnPointCount} exactly once " +
                        $"each; '{index + 1}' appears {count} times.";
                return false;
            }

            if (spawnCounts.Keys.Any(index => index >= profile.SpawnPointCount))
            {
                error = $"Profile '{profile.ProfileId}' has only SpawnPoints 1~{profile.SpawnPointCount}.";
                return false;
            }

            document.Rows = rows.ToArray();
            error = null;
            return true;
        }

        private static Dictionary<char, RoomLayoutSymbol> BuildSymbols()
        {
            List<RoomLayoutSymbol> symbols = new()
            {
                new RoomLayoutSymbol('.', RoomLayoutCellKind.Empty, "floor"),
                new RoomLayoutSymbol('#', RoomLayoutCellKind.Rock, "rock; the seed may turn one per room into a special obstacle"),
                new RoomLayoutSymbol('T', RoomLayoutCellKind.Tree, "tree (blocks flight)"),
                new RoomLayoutSymbol('o', RoomLayoutCellKind.Pit, "pit; neighbouring cells merge"),
                new RoomLayoutSymbol('c', RoomLayoutCellKind.Chest, "normal chest", chestKind: ChestKind.Normal),
                new RoomLayoutSymbol('g', RoomLayoutCellKind.Chest, "golden chest", chestKind: ChestKind.Golden),
                new RoomLayoutSymbol('d', RoomLayoutCellKind.Chest, "diamond chest", chestKind: ChestKind.Diamond),
                new RoomLayoutSymbol('M', RoomLayoutCellKind.FixedObstacle, "마리의 폭탄상자", "marie-bomb-box"),
                new RoomLayoutSymbol('G', RoomLayoutCellKind.FixedObstacle, "황금돌", "gold-rock"),
                new RoomLayoutSymbol('K', RoomLayoutCellKind.FixedObstacle, "마요의 열쇠꾸러미", "mayo-key-bundle"),
                new RoomLayoutSymbol('S', RoomLayoutCellKind.FixedObstacle, "에르핀의 간식상자", "erpin-snack-box"),
                new RoomLayoutSymbol('B', RoomLayoutCellKind.FixedObstacle, "에슈르의 빵상자", "eshur-bread-box"),
                new RoomLayoutSymbol('F', RoomLayoutCellKind.FixedObstacle, "리코타의 음식상자", "ricotta-food-box"),
                new RoomLayoutSymbol('V', RoomLayoutCellKind.FixedObstacle, "시스트의 금고", "sist-vault"),
                new RoomLayoutSymbol('X', RoomLayoutCellKind.FixedObstacle, "폭발 상자", "explosive-box"),
                new RoomLayoutSymbol('R', RoomLayoutCellKind.FixedObstacle, "셰이디의 랜덤박스", "shady-random-box"),
                new RoomLayoutSymbol('Q', RoomLayoutCellKind.FixedObstacle, "마요의 수집품 상자", "mayo-collection-box"),
                new RoomLayoutSymbol('^', RoomLayoutCellKind.FloorTrap, "바닥 가시", trapKind: RoomTrapKind.FloorSpikes),
                new RoomLayoutSymbol('a', RoomLayoutCellKind.FloorTrap, "주기 가시 (A 위상, 내려간 상태로 시작)",
                    trapKind: RoomTrapKind.PeriodicSpikes),
                new RoomLayoutSymbol('b', RoomLayoutCellKind.FloorTrap, "주기 가시 (B 위상, 올라온 상태로 시작)",
                    trapKind: RoomTrapKind.PeriodicSpikes, secondPhase: true),
                new RoomLayoutSymbol('~', RoomLayoutCellKind.FloorTrap, "장판", trapKind: RoomTrapKind.DamagePuddle),
                new RoomLayoutSymbol('w', RoomLayoutCellKind.FloorTrap, "거미줄", trapKind: RoomTrapKind.Spiderweb),
                new RoomLayoutSymbol('m', RoomLayoutCellKind.FloorTrap, "진흙케이크 (1회용, Trap-2)",
                    trapKind: RoomTrapKind.MudCake),
                new RoomLayoutSymbol('*', RoomLayoutCellKind.ObstacleTrap, "불 (비행도 막음)",
                    trapKind: RoomTrapKind.Fire),
                new RoomLayoutSymbol('Z', RoomLayoutCellKind.ObstacleTrap, "가시 돌", trapKind: RoomTrapKind.SpikedRock),
                new RoomLayoutSymbol('P', RoomLayoutCellKind.ObstacleTrap, "독버섯",
                    trapKind: RoomTrapKind.PoisonMushroom),
            };
            for (int index = 0; index < 9; index++)
            {
                symbols.Add(new RoomLayoutSymbol((char)('1' + index), RoomLayoutCellKind.SpawnPoint,
                    $"SpawnPoint {index + 1}", spawnIndex: index));
            }

            return symbols.ToDictionary(symbol => symbol.Symbol);
        }
    }
}
