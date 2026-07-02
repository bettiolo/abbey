using System.Collections.Generic;

namespace Abbey.Core
{
    /// <summary>
    /// The one-event-log-many-consumers backbone (GAME.md §4). Append-only list of
    /// timestamped records of everything meaningful the player and simulation did.
    /// Later phases derive moral pressures, hound evolution, nightmare selection and
    /// morning reports from this log. Static so any system can write without wiring.
    /// </summary>
    public static class GameEventLog
    {
        public readonly struct Record
        {
            public readonly float Time;
            public readonly string Type;
            public readonly string Data;

            public Record(float time, string type, string data)
            {
                Time = time;
                Type = type;
                Data = data;
            }

            public override string ToString()
            {
                return $"[{Time:F2}] {Type}: {Data}";
            }
        }

        static readonly List<Record> _records = new List<Record>();

        public static IReadOnlyList<Record> Records => _records;

        public static int Count => _records.Count;

        public static void Append(float time, string type, string data)
        {
            _records.Add(new Record(time, type, data));
        }

        /// <summary>Appends using the game clock's total time when available.</summary>
        public static void Append(string type, string data)
        {
            float time = GameClock.Instance != null ? GameClock.Instance.TotalTime : 0f;
            Append(time, type, data);
        }

        /// <summary>Test isolation. The log is append-only during play.</summary>
        public static void Clear()
        {
            _records.Clear();
        }
    }
}
