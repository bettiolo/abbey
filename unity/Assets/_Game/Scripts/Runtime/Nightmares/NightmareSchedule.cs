using System.Collections.Generic;
using System.Globalization;
using Abbey.Core;

namespace Abbey.Nightmares
{
    /// <summary>The three concrete Phase 2 nightmare species (GAME_DESIGN.md §9).</summary>
    public enum NightmareType
    {
        PaleHound,
        DrownedSailor,
        LanternMoth
    }

    /// <summary>Everything the Phase 2 night script can fire at a scheduled moment.</summary>
    public enum NightmareEventKind
    {
        SpawnPaleHound,
        SpawnDrownedSailor,
        SpawnLanternMoth,
        Whisper,
        Shadow,
        Panic
    }

    /// <summary>
    /// Parser for the data-driven Phase 2 night script
    /// (<see cref="PrototypeConfig.phase2NightSchedule"/>). Each entry is
    /// "fraction:kind" — fraction 0..1 of the night, kind one of pale_hound,
    /// drowned_sailor, lantern_moth, whisper, shadow, panic. Parsing is culture
    /// invariant and pure; bad entries are skipped and reported through the
    /// caller-supplied error list so the director can log them. Static and
    /// deterministic so EditMode tests can exercise it directly.
    /// </summary>
    public static class NightmareSchedule
    {
        public readonly struct Entry
        {
            /// <summary>Time into the night, 0..1 of the night duration.</summary>
            public readonly float Fraction;

            public readonly NightmareEventKind Kind;

            public Entry(float fraction, NightmareEventKind kind)
            {
                Fraction = fraction;
                Kind = kind;
            }

            public override string ToString()
            {
                return $"{Fraction:F2}:{Kind}";
            }
        }

        /// <summary>
        /// Parses schedule lines into entries sorted ascending by fraction
        /// (stable: same-fraction entries keep their authored order). Invalid
        /// lines are skipped and appended to <paramref name="errors"/> when the
        /// caller provides a list.
        /// </summary>
        public static List<Entry> Parse(IReadOnlyList<string> lines, List<string> errors = null)
        {
            var entries = new List<Entry>();
            if (lines == null)
            {
                return entries;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (!TryParseLine(line, out Entry entry))
                {
                    errors?.Add(line ?? "<null>");
                    continue;
                }

                // Stable insertion sort by fraction: authored order wins ties.
                int at = entries.Count;
                while (at > 0 && entries[at - 1].Fraction > entry.Fraction)
                {
                    at--;
                }
                entries.Insert(at, entry);
            }
            return entries;
        }

        /// <summary>Parses one "fraction:kind" line. Fraction is clamped to 0..1.</summary>
        public static bool TryParseLine(string line, out Entry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            int split = line.IndexOf(':');
            if (split <= 0 || split >= line.Length - 1)
            {
                return false;
            }

            string fractionPart = line.Substring(0, split).Trim();
            string kindPart = line.Substring(split + 1).Trim();

            if (!float.TryParse(fractionPart, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out float fraction))
            {
                return false;
            }
            if (float.IsNaN(fraction) || float.IsInfinity(fraction))
            {
                return false;
            }
            if (!TryParseKind(kindPart, out NightmareEventKind kind))
            {
                return false;
            }

            entry = new Entry(fraction < 0f ? 0f : fraction > 1f ? 1f : fraction, kind);
            return true;
        }

        public static bool TryParseKind(string id, out NightmareEventKind kind)
        {
            switch (id)
            {
                case "pale_hound": kind = NightmareEventKind.SpawnPaleHound; return true;
                case "drowned_sailor": kind = NightmareEventKind.SpawnDrownedSailor; return true;
                case "lantern_moth": kind = NightmareEventKind.SpawnLanternMoth; return true;
                case "whisper": kind = NightmareEventKind.Whisper; return true;
                case "shadow": kind = NightmareEventKind.Shadow; return true;
                case "panic": kind = NightmareEventKind.Panic; return true;
                default: kind = default; return false;
            }
        }
    }
}
