#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrickalFanGame.Run
{
    // Local measurement only. Durations use real time (including pause/menu time), never combat simulation time.
    public sealed class DevelopmentPlaytestRecord
    {
        private readonly Dictionary<int, double> floorSeconds = new();
        private readonly SortedSet<string> assistance = new(StringComparer.Ordinal);
        private readonly SortedDictionary<string, int> itemUses = new(StringComparer.Ordinal);
        private double lastTime;
        private int currentFloor;
        public int Seed { get; }
        public string CharacterId { get; }
        public int RoomContentVersion { get; }
        public int EncounterContentVersion { get; }
        public bool IsFinished { get; private set; }
        public string Outcome { get; private set; } = "IN_PROGRESS";
        public bool IsAssisted => assistance.Count > 0;
        public IReadOnlyDictionary<string, int> ItemUses => itemUses;

        public DevelopmentPlaytestRecord(int seed, int floor, double now, string characterId = "unknown",
            int roomContentVersion = 0, int encounterContentVersion = 0)
        {
            Seed = seed;
            CharacterId = characterId;
            RoomContentVersion = roomContentVersion;
            EncounterContentVersion = encounterContentVersion;
            currentFloor = floor > 0 ? floor : 1;
            lastTime = now;
            floorSeconds[currentFloor] = 0;
        }

        public void EnterFloor(int floor, double now)
        {
            if (IsFinished || floor < 1) return;
            Accumulate(now);
            currentFloor = floor;
            floorSeconds.TryAdd(floor, 0);
        }

        public void MarkAssisted(string action)
        { if (!IsFinished && !string.IsNullOrWhiteSpace(action)) assistance.Add(action); }

        // Single-use item uses are recorded only here, never on the server (Contract-0 §4.2).
        public void RecordItemUse(string itemId)
        {
            if (IsFinished || string.IsNullOrWhiteSpace(itemId)) return;
            itemUses[itemId] = itemUses.TryGetValue(itemId, out int count) ? count + 1 : 1;
        }

        public bool Finish(string outcome, double now)
        {
            if (IsFinished) return false;
            Accumulate(now);
            Outcome = outcome;
            IsFinished = true;
            return true;
        }

        public double GetFloorSeconds(int floor, double now)
        {
            double seconds = floorSeconds.TryGetValue(floor, out double stored) ? stored : 0;
            return seconds + (!IsFinished && floor == currentFloor ? Math.Max(0, now - lastTime) : 0);
        }

        public string Format(double now)
        {
            string durations = string.Join(", ", floorSeconds.Keys.OrderBy(floor => floor)
                .Select(floor => $"F{floor}={GetFloorSeconds(floor, now).ToString("F1", CultureInfo.InvariantCulture)}s"));
            string tools = assistance.Count == 0 ? "none" : string.Join(",", assistance);
            string uses = itemUses.Count == 0
                ? "none"
                : string.Join(",", itemUses.Select(pair => $"{pair.Key}x{pair.Value}"));
            return $"[Play-1] seed={Seed} character={CharacterId} content={RoomContentVersion}/{EncounterContentVersion} " +
                   $"result={Outcome} real-time-including-pauses: {durations}; assistance={tools}; item-uses={uses}";
        }

        private void Accumulate(double now)
        {
            now = Math.Max(lastTime, now);
            floorSeconds[currentFloor] += now - lastTime;
            lastTime = now;
        }
    }
}
#endif
