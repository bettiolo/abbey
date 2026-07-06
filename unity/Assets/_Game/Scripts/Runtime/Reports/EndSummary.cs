using System.Collections.Generic;
using System.Text;
using Abbey.Core;

namespace Abbey.Reports
{
    /// <summary>
    /// The whole-run facts, reduced from the shared <see cref="GameEventLog"/> into a flat
    /// record set. Like <see cref="MorningReportData"/> but for the campaign close (P3-14):
    /// the chapters lived through, the laws enacted, the dead and how they were buried, the
    /// hound's final path, the abbey's final form, the threat/mitigation history and the
    /// spring-ship launch. Everything here is derived ONLY from log records, so the summary
    /// is deterministic and EditMode-testable without a scene.
    /// </summary>
    public struct EndSummaryData
    {
        public List<string> Chapters;        // chapter display names, in order reached
        public List<string> LawsEnacted;     // decree tags, in order (deduped)
        public int Deaths;                    // distinct villagers dead across the run
        public int Burials;                   // burial rites performed
        public string HoundPath;              // the hound's final evolution path word
        public string AbbeyForm;              // the abbey's final transformation form word
        public int Overdrives;                // overdrive activations across the run
        public int ConsequenceNightmares;     // P3-11 threat manifestations
        public int ThreatMitigations;         // P3-11 threats laid to rest
        public bool ShipLaunched;             // a spring-ship launch record was seen
        public string ShipResult;             // the launch result word (ShipSailed / …)
        public int Sailed;                    // souls aboard at launch
        public int Stayed;                    // souls who kept the shore
    }

    /// <summary>
    /// The campaign end-summary chronicle (ROADMAP Phase 3 item 13: "end summary reflecting
    /// actual choices"). A pure, deterministic composer in the P2-07 storybook voice —
    /// <see cref="Build"/> reduces the log to <see cref="EndSummaryData"/> and
    /// <see cref="Compose"/> maps that to a short chronicle whose sentences are DATA
    /// (templates), never hard-coded per run. Same log ⇒ same chronicle. The facts it names
    /// (laws, deaths, the hound's path, the abbey's form, who sailed) are exactly what the
    /// run wrote to the log, so the chronicle can never drift from the run.
    /// </summary>
    public static class EndSummary
    {
        // ------------------------------------------------------------------
        // Reduce
        // ------------------------------------------------------------------

        public static EndSummaryData Build(IReadOnlyList<GameEventLog.Record> log)
        {
            var data = new EndSummaryData
            {
                Chapters = new List<string>(),
                LawsEnacted = new List<string>(),
                HoundPath = string.Empty,
                AbbeyForm = string.Empty,
                ShipResult = string.Empty,
            };

            if (log == null || log.Count == 0)
            {
                return data;
            }

            var dead = new HashSet<string>();

            for (int i = 0; i < log.Count; i++)
            {
                var rec = log[i];
                string type = rec.Type;
                string d = rec.Data ?? string.Empty;

                switch (type)
                {
                    case "chapter":
                    {
                        string name = Between(d, "name=\"", "\"");
                        if (name.Length > 0 && (data.Chapters.Count == 0
                            || data.Chapters[data.Chapters.Count - 1] != name))
                        {
                            data.Chapters.Add(name);
                        }
                        break;
                    }
                    case "decree":
                    {
                        string tag = TokenAfter(d, "tag=");
                        if (tag.Length > 0 && !data.LawsEnacted.Contains(tag))
                        {
                            data.LawsEnacted.Add(tag);
                        }
                        break;
                    }
                    case "villager_died":
                        if (d.Length > 0) dead.Add(d);
                        break;
                    case "villager_died_at":
                    {
                        string name = TokenAfter(d, "name=");
                        if (name.Length > 0) dead.Add(name);
                        break;
                    }
                    case "VillagerState":
                    {
                        int arrow = d.IndexOf("->");
                        int space = d.IndexOf(' ');
                        if (space > 0 && arrow > space)
                        {
                            string next = FirstToken(d.Substring(arrow + 2));
                            if (next == "Dead") dead.Add(d.Substring(0, space));
                        }
                        break;
                    }
                    case "burial":
                        data.Burials++;
                        break;
                    case "hound_evolved":
                    {
                        // "from -> to"
                        int arrow = d.IndexOf("->");
                        if (arrow >= 0)
                        {
                            data.HoundPath = FirstToken(d.Substring(arrow + 2).TrimStart());
                        }
                        break;
                    }
                    case "abbey_transformation":
                    {
                        // "prev->next (score=…)"
                        int arrow = d.IndexOf("->");
                        if (arrow >= 0)
                        {
                            data.AbbeyForm = FirstToken(d.Substring(arrow + 2));
                        }
                        break;
                    }
                    case "overdrive_activated":
                        data.Overdrives++;
                        break;
                    case "consequence_nightmare":
                        data.ConsequenceNightmares++;
                        break;
                    case "threat_mitigation":
                        data.ThreatMitigations++;
                        break;
                    case "spring_ship":
                        if (d.StartsWith("launched"))
                        {
                            data.ShipLaunched = true;
                            data.ShipResult = TokenAfter(d, "result=");
                            data.Sailed = ParseIntAfter(d, "sailed=");
                            data.Stayed = ParseIntAfter(d, "stayed=");
                        }
                        break;
                }
            }

            data.Deaths = dead.Count;
            return data;
        }

        // ------------------------------------------------------------------
        // Compose (storybook voice; sentences are data)
        // ------------------------------------------------------------------

        public static string Compose(IReadOnlyList<GameEventLog.Record> log)
        {
            return Compose(Build(log));
        }

        public static string Compose(EndSummaryData d)
        {
            var lines = new List<string>(12);

            // Opening — the shape of the year.
            if (d.Chapters != null && d.Chapters.Count > 0)
            {
                lines.Add($"The year turned through {Join(d.Chapters)}, and this is how it is "
                          + "remembered.");
            }
            else
            {
                lines.Add("The year turned once around the abbey, and this is how it is remembered.");
            }

            // Laws.
            if (d.LawsEnacted != null && d.LawsEnacted.Count > 0)
            {
                lines.Add($"By decree we lived under {Join(d.LawsEnacted)} — laws we chose, and "
                          + "must answer for.");
            }
            else
            {
                lines.Add("We passed no laws; what order we kept, we kept by habit alone.");
            }

            // The dead.
            if (d.Deaths > 0)
            {
                string buried = d.Burials > 0
                    ? $", and {Cardinal(d.Burials)} were given the rites"
                    : ", and the ground took them without ceremony";
                lines.Add($"{Cap(Cardinal(d.Deaths))} of us did not see the ship{buried}.");
            }
            else
            {
                lines.Add("Not one of us was lost to the dark — a thing worth the telling.");
            }

            // The hound.
            if (!string.IsNullOrEmpty(d.HoundPath) && d.HoundPath != "Unevolved")
            {
                lines.Add(HoundLine(d.HoundPath));
            }

            // The abbey's form.
            if (!string.IsNullOrEmpty(d.AbbeyForm) && d.AbbeyForm != "Balanced")
            {
                lines.Add($"The abbey became a {d.AbbeyForm} in the end; that is what our choices "
                          + "made of these stones.");
            }

            // Threats faced and laid to rest (P3-11).
            if (d.ConsequenceNightmares > 0 || d.ThreatMitigations > 0)
            {
                lines.Add($"The dark answered our sins {Cardinal(d.ConsequenceNightmares)} time"
                          + $"{(d.ConsequenceNightmares == 1 ? string.Empty : "s")}, and "
                          + $"{Cardinal(d.ThreatMitigations)} of its haunts we put to rest.");
            }

            // Overdrives.
            if (d.Overdrives > 0)
            {
                lines.Add($"{Cap(Cardinal(d.Overdrives))} time"
                          + $"{(d.Overdrives == 1 ? string.Empty : "s")} we drove ourselves past "
                          + "safe measure to hold the light.");
            }

            // The launch.
            if (d.ShipLaunched && d.ShipResult == "ShipSailed")
            {
                lines.Add($"When the spring tide came the ship stood ready, and "
                          + $"{Cardinal(d.Sailed)} sailed while {Cardinal(d.Stayed)} kept the "
                          + "shore. The abbey at world's end had launched its answer to the sea.");
            }
            else if (d.ShipLaunched)
            {
                lines.Add("When the spring tide came the ship was not ready, and the shore held us "
                          + "another turn of the year.");
            }

            return string.Join(" ", lines);
        }

        static string HoundLine(string path)
        {
            switch (path)
            {
                case "Guardian":
                    return "The hound became a Guardian, and stood the nights beside us to the end.";
                case "War":
                    return "The hound became a War-beast, a weapon we made and could not unmake.";
                case "Starved":
                    return "The hound went Starved and half-wild, answering only its own hunger.";
                case "Sacred":
                    return "The hound became a Sacred thing, and the old faith gathered around it.";
                case "Broken":
                    return "The hound was Broken by our hand, and trusted no one at the last.";
                default:
                    return $"The hound walked the {path} path by the year's turning.";
            }
        }

        // ------------------------------------------------------------------
        // Parsing / prose helpers
        // ------------------------------------------------------------------

        static readonly string[] Cardinals =
        {
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve"
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

        static string Join(IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            if (items.Count == 1) return items[0];
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(i == items.Count - 1 ? " and " : ", ");
                }
                sb.Append(items[i]);
            }
            return sb.ToString();
        }

        static string FirstToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.TrimStart();
            int sp = s.IndexOf(' ');
            return sp < 0 ? s : s.Substring(0, sp);
        }

        static string TokenAfter(string d, string marker)
        {
            int idx = d.IndexOf(marker, System.StringComparison.Ordinal);
            if (idx < 0) return string.Empty;
            return FirstToken(d.Substring(idx + marker.Length));
        }

        static int ParseIntAfter(string d, string marker)
        {
            string tok = TokenAfter(d, marker);
            if (string.IsNullOrEmpty(tok)) return 0;
            int i = 0;
            while (i < tok.Length && (char.IsDigit(tok[i]) || (i == 0 && tok[i] == '-'))) i++;
            return int.TryParse(tok.Substring(0, i), out int v) ? v : 0;
        }

        static string Between(string d, string open, string close)
        {
            int a = d.IndexOf(open, System.StringComparison.Ordinal);
            if (a < 0) return string.Empty;
            a += open.Length;
            int b = d.IndexOf(close, a, System.StringComparison.Ordinal);
            if (b < 0) return string.Empty;
            return d.Substring(a, b - a);
        }
    }
}
