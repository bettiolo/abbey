using System;
using System.Collections.Generic;
using System.Text;

namespace Abbey.Reports
{
    /// <summary>
    /// One candidate storybook line: a condition over the night's facts plus a
    /// string template with {slots}. Lines are DATA — the composer never hard-codes
    /// prose. Within a section the first matching line (evaluated worst-first) wins,
    /// so the same log always yields the same sentence (no RNG).
    /// </summary>
    public readonly struct ReportLine
    {
        public readonly Func<MorningReportData, bool> Condition;
        public readonly string Template;

        public ReportLine(Func<MorningReportData, bool> condition, string template)
        {
            Condition = condition;
            Template = template;
        }
    }

    /// <summary>
    /// Composes the dawn report's storybook prose (GAME_DESIGN.md §14; GAME.md voice:
    /// quiet, weighty, folk-tale). Deterministic and pure: <see cref="Compose"/> maps a
    /// <see cref="MorningReportData"/> to 4–8 sentences by walking ordered template
    /// tables in a fixed worst-first priority. No randomness — identical data in ⇒
    /// identical prose out.
    ///
    /// The tables are public and static so tests can assert priority ordering and that
    /// the hound line matches the bond records.
    /// </summary>
    public static class MorningReportProse
    {
        public const int MaxSentences = 8;

        // ------------------------------------------------------------------
        // Template tables (DATA). Ordered worst-first: the first line whose
        // condition holds is the one that speaks.
        // ------------------------------------------------------------------

        /// <summary>Always emits exactly one line (the last condition is always true).</summary>
        public static readonly ReportLine[] Opening =
        {
            new ReportLine(d => d.HeroDied,
                "The bell is silent this morning; the one who rang it did not live to see the light return."),
            new ReportLine(d => d.Dead > 0,
                "Dawn came grey and slow over the abbey, and it did not come for all of us."),
            new ReportLine(d => d.Missing > 0,
                "Morning found us counting by the ashes, and the counting came up short."),
            new ReportLine(d => d.Injured > 0 || d.PanicEvents > 0,
                "The light returned thin and cold, and the night had left its marks on us."),
            new ReportLine(d => true,
                "Morning rose soft and unhurried over the hill, as though the dark had never come."),
        };

        /// <summary>Emit-all: each matching casualty line is spoken, worst-first.</summary>
        public static readonly ReportLine[] Villagers =
        {
            new ReportLine(d => d.Dead > 0,
                "{deadCap} of us did not see the morning."),
            new ReportLine(d => d.Missing > 0,
                "{missingCap} walked into the dark and did not walk back; we do not yet name them dead."),
            new ReportLine(d => d.Injured > 0,
                "{injuredCap} of us woke wounded, and will keep to the fire until the flesh knits."),
        };

        public static readonly ReportLine[] Rescue =
        {
            new ReportLine(d => d.Rescued > 0,
                "The Bellkeeper carried {rescued} back out of the dark before it could close over them."),
        };

        /// <summary>Always emits one line: the fate of the fires.</summary>
        public static readonly ReportLine[] Fire =
        {
            new ReportLine(d => d.SacredFlameLost,
                "The abbey flame went out in the night — that is not a thing we speak of lightly."),
            new ReportLine(d => d.FiresLost > 0 && d.FiresRelit > 0,
                "A fire or two guttered and died, but quick hands carried the light back before the cold could settle."),
            new ReportLine(d => d.FiresLost > 0,
                "Fires went dark and stayed dark, and the cold crept a little nearer the door."),
            new ReportLine(d => true,
                "The fires held through the night, and the light kept its ground."),
        };

        /// <summary>
        /// The hound line — emitted only when a hound acted in the window. Worst-first,
        /// and each branch matches a specific set of bond records (missing, angry/feared,
        /// protected the hero, hunted alone, answered the bell, fed, still chained).
        /// </summary>
        public static readonly ReportLine[] Hound =
        {
            new ReportLine(d => d.HoundWentMissing || d.HoundDisposition == "Missing",
                "The hound is gone — its chain lies empty in the tower, and the tower is colder for it."),
            new ReportLine(d => d.HoundDisposition == "Angry"
                                || (d.HoundIgnoredBell && d.HoundTrustDirection < 0),
                "The hound would not be gentled; it watched us with the old anger and kept to the dark of the tower."),
            new ReportLine(d => d.HoundProtectedHero,
                "When the dark closed on the Bellkeeper, the hound broke its chain and stood between — it remembers whose hand fed it."),
            new ReportLine(d => d.HoundKilledMonster && d.HoundAteKillAlone,
                "The hound killed in the night and dragged the carcass off into the dark to eat alone; it answers no bell but its own hunger."),
            new ReportLine(d => d.HoundAnsweredBell,
                "The hound came when the bell called, and something in it has begun to trust the hand that rings it."),
            new ReportLine(d => d.HoundFedByHero || IsBondedWord(d.HoundDisposition),
                "The hound ate from the Bellkeeper's hand and slept easier; it is not ours yet, but it is no longer only the dark's."),
            new ReportLine(d => d.HoundStillChained,
                "The hound stayed chained in the tower through the whole of the night, wary and watchful, trusting no one."),
            new ReportLine(d => true,
                "The hound endured the night, as it always has."),
        };

        /// <summary>Always emits one line: what pressed at the edge of the light.</summary>
        public static readonly ReportLine[] Nightmares =
        {
            new ReportLine(d => d.PanicEvents > 0,
                "Fear ran through the camp like cold water at least once, and had to be gathered back by hand."),
            new ReportLine(d => d.ShadowSeen,
                "A shape stood at the forest's edge and came no closer, and none who saw it have spoken of it since."),
            new ReportLine(d => d.MonstersFaced > 0,
                "{monstersCap} pale things tested the edge of the light through the night and found it holding."),
            new ReportLine(d => d.Whispers > 0,
                "Voices whispered from the unlit road, close enough to name, and were gone by morning."),
            new ReportLine(d => true,
                "Nothing troubled the edge of the light; the dark, for once, kept to itself."),
        };

        /// <summary>Always emits one line: what the settlement now believes of the hero.</summary>
        public static readonly ReportLine[] Belief =
        {
            new ReportLine(d => d.HeroBitten,
                "The Bellkeeper's hand is bound this morning where the hound's teeth found it, and the lesson was not lost."),
            new ReportLine(d => d.HeroRescuedSomeone && d.HeroCarriedFireIntoDark,
                "They saw the Bellkeeper walk into the dark with fire in hand and come back leading the lost — this is how the old stories start."),
            new ReportLine(d => d.HeroRescuedSomeone,
                "The village remembers who came for them when the light failed."),
            new ReportLine(d => d.HeroCarriedFireIntoDark,
                "They saw the Bellkeeper carry flame out into the dark, and did not look away."),
            new ReportLine(d => d.BellRangCount > 0,
                "The bell rang {bells} time{bellsPlural} in the night, and each time the dark drew back a step."),
            new ReportLine(d => true,
                "The settlement wakes and the abbey still stands, and that is enough this morning."),
        };

        // ------------------------------------------------------------------
        // Composition
        // ------------------------------------------------------------------

        public static string Compose(MorningReportData data)
        {
            var slots = BuildSlots(data);
            var lines = new List<string>(MaxSentences + 2);

            lines.Add(SelectOne(Opening, data, slots));       // always 1
            EmitAll(Villagers, data, slots, lines);           // 0..3
            int rescueIndex = -1;
            string rescue = SelectFirst(Rescue, data, slots);
            if (rescue != null) { rescueIndex = lines.Count; lines.Add(rescue); }
            int fireIndex = lines.Count;
            lines.Add(SelectOne(Fire, data, slots));          // always 1
            int houndIndex = -1;
            if (data.HoundPresent)
            {
                houndIndex = lines.Count;
                lines.Add(SelectOne(Hound, data, slots));     // 0..1
            }
            int nightmareIndex = lines.Count;
            lines.Add(SelectOne(Nightmares, data, slots));    // always 1
            lines.Add(SelectOne(Belief, data, slots));        // always 1 (closing)

            // Clamp to MaxSentences, shedding the lowest-priority non-essential lines
            // first (positive-news rescue, then the hound flavour, then the dread beat).
            TrimToMax(lines, ref rescueIndex, ref houndIndex, ref nightmareIndex, ref fireIndex);

            return string.Join(" ", lines);
        }

        static void TrimToMax(List<string> lines, ref int rescueIndex, ref int houndIndex,
                              ref int nightmareIndex, ref int fireIndex)
        {
            // Drop order: rescue, hound, nightmares. Opening/villagers/fire/closing stay.
            while (lines.Count > MaxSentences)
            {
                int drop = rescueIndex >= 0 ? rescueIndex
                         : houndIndex >= 0 ? houndIndex
                         : nightmareIndex >= 0 ? nightmareIndex
                         : -1;
                if (drop < 0) break; // nothing left we are willing to drop
                lines.RemoveAt(drop);
                Shift(ref rescueIndex, drop);
                Shift(ref houndIndex, drop);
                Shift(ref nightmareIndex, drop);
                Shift(ref fireIndex, drop);
            }
        }

        static void Shift(ref int index, int removedAt)
        {
            if (index == removedAt) index = -1;
            else if (index > removedAt) index--;
        }

        static void EmitAll(ReportLine[] table, MorningReportData data,
                            IDictionary<string, string> slots, List<string> into)
        {
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i].Condition(data))
                {
                    into.Add(Fill(table[i].Template, slots));
                }
            }
        }

        static string SelectOne(ReportLine[] table, MorningReportData data,
                                IDictionary<string, string> slots)
        {
            string s = SelectFirst(table, data, slots);
            return s ?? string.Empty;
        }

        static string SelectFirst(ReportLine[] table, MorningReportData data,
                                  IDictionary<string, string> slots)
        {
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i].Condition(data))
                {
                    return Fill(table[i].Template, slots);
                }
            }
            return null;
        }

        static bool IsBondedWord(string state)
        {
            switch (state)
            {
                case "Fed":
                case "Following":
                case "Guarding":
                case "Trusting":
                case "Protective":
                    return true;
                default:
                    return false;
            }
        }

        // ------------------------------------------------------------------
        // Slots
        // ------------------------------------------------------------------

        static readonly string[] Cardinals =
        {
            "zero", "one", "two", "three", "four", "five", "six",
            "seven", "eight", "nine", "ten", "eleven", "twelve"
        };

        static string Cardinal(int n)
        {
            if (n >= 0 && n < Cardinals.Length) return Cardinals[n];
            return n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        static Dictionary<string, string> BuildSlots(MorningReportData d)
        {
            return new Dictionary<string, string>
            {
                { "dead", Cardinal(d.Dead) },
                { "deadCap", Cap(Cardinal(d.Dead)) },
                { "missing", Cardinal(d.Missing) },
                { "missingCap", Cap(Cardinal(d.Missing)) },
                { "injured", Cardinal(d.Injured) },
                { "injuredCap", Cap(Cardinal(d.Injured)) },
                { "rescued", d.Rescued == 1 ? "one soul" : Cardinal(d.Rescued) + " souls" },
                { "monsters", Cardinal(d.MonstersFaced) },
                { "monstersCap", Cap(Cardinal(d.MonstersFaced)) },
                { "whispers", Cardinal(d.Whispers) },
                { "bells", Cardinal(d.BellRangCount) },
                { "bellsPlural", d.BellRangCount == 1 ? string.Empty : "s" },
                { "survivors", Cardinal(System.Math.Max(0, d.Survivors)) },
                { "night", d.NightNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "food", d.FoodConsumed.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "wood", d.WoodConsumed.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "oil", d.OilConsumed.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            };
        }

        static string Fill(string template, IDictionary<string, string> slots)
        {
            if (template.IndexOf('{') < 0) return template;
            var sb = new StringBuilder(template);
            foreach (var kv in slots)
            {
                sb.Replace("{" + kv.Key + "}", kv.Value);
            }
            return sb.ToString();
        }
    }
}
