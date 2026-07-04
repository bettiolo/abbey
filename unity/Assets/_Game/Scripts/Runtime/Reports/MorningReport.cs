using System.Collections.Generic;
using System.Globalization;
using Abbey.Core;

namespace Abbey.Reports
{
    /// <summary>
    /// The facts of one night, reduced from the shared <see cref="GameEventLog"/>
    /// into counts and flags. Everything here is derived ONLY from log records —
    /// never a live scene query — so the whole thing is deterministic and
    /// EditMode-testable without a scene (GAME.md §4: one log, many consumers;
    /// VERTICAL_SLICE_SPEC §3, minutes 18–20: the morning consequence).
    /// </summary>
    public struct MorningReportData
    {
        public int NightNumber;

        // --- Villagers ---------------------------------------------------
        /// <summary>Distinct villagers seen acting in the window (roster estimate).</summary>
        public int KnownVillagers;
        public int Dead;
        public int Missing;
        public int Injured;      // still alive, but wounded at dawn
        public int Rescued;      // escorted back into Safe light
        public int RescueAttempts;
        /// <summary>KnownVillagers - Dead - Missing, or -1 when nobody was seen.</summary>
        public int Survivors;

        // --- Resources ---------------------------------------------------
        public int FoodConsumed;
        public int WoodConsumed;
        public int WoodBurnedIntoFires; // consume records whose reason is a refuel
        public int OilConsumed;
        public int FoodGained;
        public int WoodGained;

        // --- Light / fire ------------------------------------------------
        public int FiresLost;    // LightExtinguished count
        public int FiresRelit;   // LightIgnited count
        public bool SacredFlameLost;

        // --- Hound bond --------------------------------------------------
        public bool HoundPresent;
        /// <summary>The hound's last known state word (e.g. "Following", "Missing", "Chained").</summary>
        public string HoundDisposition;
        /// <summary>-1 trust fell, 0 steady/unknown, +1 trust rose across the night.</summary>
        public int HoundTrustDirection;
        /// <summary>-1 fear eased, 0 steady/unknown, +1 fear rose across the night.</summary>
        public int HoundFearDirection;
        public bool HoundFedByHero;
        public bool HoundAnsweredBell;
        public bool HoundIgnoredBell;
        public bool HoundKilledMonster;
        public bool HoundProtectedHero;
        public bool HoundAteKillAlone;
        public bool HoundWentMissing;
        public bool HoundStillChained;

        // --- Nightmares --------------------------------------------------
        public int MonstersFaced;
        public int MonstersKilled;
        public int Whispers;
        public bool ShadowSeen;
        public int PanicEvents;
        /// <summary>Monsters that appeared during a night the settlement lived through.</summary>
        public int NightmareEncountersSurvived;

        // --- What villagers now believe about the hero -------------------
        public bool HeroRescuedSomeone;
        public bool HeroCarriedFireIntoDark;
        public int BellRangCount;
        public bool HeroDied;
        public bool HeroBitten;

        /// <summary>True when nobody died, went missing, was hurt, and no fire was lost.</summary>
        public bool WasQuietNight =>
            Dead == 0 && Missing == 0 && Injured == 0 && FiresLost == 0 &&
            PanicEvents == 0 && !HeroDied;
    }

    /// <summary>
    /// Pure, deterministic reducer: give it the log records for one night window and
    /// it returns a <see cref="MorningReportData"/>. Parses the REAL event vocabulary
    /// emitted across the merged codebase (resource / salvage / job / build / abbey /
    /// hound_* / nightmare / whisper / panic_event / villager_* / VillagerState /
    /// Light* / hero_* records). Same records in ⇒ same data out.
    /// </summary>
    public static class MorningReport
    {
        public static MorningReportData Build(IReadOnlyList<GameEventLog.Record> window)
        {
            var data = new MorningReportData
            {
                HoundDisposition = string.Empty,
                Survivors = -1,
                NightNumber = 0
            };

            if (window == null || window.Count == 0)
            {
                return data;
            }

            var known = new HashSet<string>();
            var deadNames = new HashSet<string>();
            var missingNames = new HashSet<string>();
            var injuredNames = new HashSet<string>();
            var rescuedNames = new HashSet<string>();
            var rescueAttemptNames = new HashSet<string>();
            var villagerFinalState = new Dictionary<string, string>();

            var killedMonsters = new HashSet<string>();

            bool haveFirstTrust = false;
            float firstTrust = 0f, lastTrust = 0f;
            bool haveFirstFear = false;
            float firstFear = 0f, lastFear = 0f;
            string houndDisposition = string.Empty;

            for (int i = 0; i < window.Count; i++)
            {
                var rec = window[i];
                string type = rec.Type;
                string d = rec.Data ?? string.Empty;

                switch (type)
                {
                    // ---------- phase / night framing ----------
                    case "night_begins":
                    case "NightSummary":
                        int n = ParseIntAfter(d, "night=");
                        if (n > 0) data.NightNumber = n;
                        break;

                    // ---------- villager casualties ----------
                    case "villager_died":
                        RegisterVillager(known, d);
                        deadNames.Add(d);
                        break;
                    case "villager_died_at":
                    {
                        string name = TokenAfter(d, "name=");
                        if (name.Length > 0) { known.Add(name); deadNames.Add(name); }
                        break;
                    }
                    case "villager_missing":
                        RegisterVillager(known, d);
                        missingNames.Add(d);
                        break;
                    case "villager_injured_by_darkness":
                        RegisterVillager(known, d);
                        injuredNames.Add(d);
                        break;
                    case "villager_rescue_started":
                    case "hero_rescue_started":
                        RegisterVillager(known, d);
                        rescueAttemptNames.Add(d);
                        break;
                    case "villager_reached_light":
                    case "villager_released_in_dark":
                    case "villager_finishing_work":
                    case "villager_deposited_resource":
                        RegisterVillager(known, d);
                        break;
                    case "VillagerRescued":
                        RegisterVillager(known, d);
                        rescuedNames.Add(d);
                        break;
                    case "VillagerEndangered":
                        RegisterVillager(known, d);
                        break;
                    case "VillagerState":
                    {
                        // "{name} {State}->{next}"
                        int space = d.IndexOf(' ');
                        int arrow = d.IndexOf("->");
                        if (space > 0 && arrow > space)
                        {
                            string name = d.Substring(0, space);
                            string next = FirstToken(d.Substring(arrow + 2));
                            known.Add(name);
                            villagerFinalState[name] = next;
                        }
                        break;
                    }

                    // ---------- resources ----------
                    case "resource":
                        AccumulateResource(d, ref data);
                        break;

                    // ---------- light / fire ----------
                    case "LightExtinguished":
                        data.FiresLost++;
                        if (d.Contains("sacred=True")) data.SacredFlameLost = true;
                        break;
                    case "LightIgnited":
                        data.FiresRelit++;
                        break;

                    // ---------- hound bond ----------
                    case "hound_state":
                    {
                        string s = ParseHoundState(d);
                        if (s.Length > 0) houndDisposition = s;
                        data.HoundPresent = true;
                        break;
                    }
                    case "hound_fed":
                        data.HoundPresent = true;
                        data.HoundFedByHero = true;
                        break;
                    case "hero_fed_hound":
                        data.HoundFedByHero = true;
                        break;
                    case "hound_answered_bell":
                    case "hound_reached_bell":
                        data.HoundPresent = true;
                        data.HoundAnsweredBell = true;
                        break;
                    case "hound_ignored_bell":
                        data.HoundPresent = true;
                        data.HoundIgnoredBell = true;
                        break;
                    case "hound_killed_monster":
                        data.HoundPresent = true;
                        data.HoundKilledMonster = true;
                        AddArrowTarget(killedMonsters, d);
                        break;
                    case "hound_intervention":
                        data.HoundPresent = true;
                        if (d.Contains("protect_hero") || d.Contains("save_hero"))
                            data.HoundProtectedHero = true;
                        if (d.Contains("ate_kill")) data.HoundAteKillAlone = true;
                        if (d.Contains("went_missing")) data.HoundWentMissing = true;
                        break;
                    case "hound_dragged_corpse":
                        data.HoundPresent = true;
                        data.HoundAteKillAlone = true;
                        break;
                    case "hound_choice":
                    case "hound_calmed_by_bell":
                    case "hound_took_hit":
                    case "hound_engaged_monster":
                    case "hound_attacked_monster":
                        data.HoundPresent = true;
                        break;

                    // ---------- nightmares ----------
                    case "MonsterSpawned":
                        data.MonstersFaced++;
                        break;
                    case "monster_killed":
                        killedMonsters.Add(d);
                        break;
                    case "monster_attacked_villager":
                        // final villager state (Injured/Dead) is reconstructed from
                        // VillagerState; nothing extra to tally here.
                        break;
                    case "whisper":
                        data.Whispers++;
                        break;
                    case "nightmare":
                        if (d.StartsWith("shadow")) data.ShadowSeen = true;
                        break;
                    case "panic_event":
                        if (!d.Contains("skipped")) data.PanicEvents++;
                        break;

                    // ---------- hero acts (belief flags) ----------
                    case "hero_rang_bell":
                        data.BellRangCount++;
                        break;
                    case "hero_raised_flame":
                        data.HeroCarriedFireIntoDark = true;
                        break;
                    case "hero_rescue_released":
                        if (d.Contains("safe=True")) data.HeroRescuedSomeone = true;
                        break;
                    case "hero_bitten_by_hound":
                        data.HeroBitten = true;
                        break;
                    case "hero_died":
                        data.HeroDied = true;
                        break;
                }

                // Track hound trust/fear direction from any record that carries them.
                if (type.Length > 6 && type[0] == 'h' && type[1] == 'o' &&
                    (d.Contains("trust=") || d.Contains("fear=")))
                {
                    if (TryParseFloatAfter(d, "trust=", out float t))
                    {
                        if (!haveFirstTrust) { firstTrust = t; haveFirstTrust = true; }
                        lastTrust = t;
                    }
                    if (TryParseFloatAfter(d, "fear=", out float f))
                    {
                        if (!haveFirstFear) { firstFear = f; haveFirstFear = true; }
                        lastFear = f;
                    }
                }
            }

            // Fold reconstructed final states into casualty sets (captures monster
            // injuries/deaths that only surface as VillagerState transitions).
            foreach (var kv in villagerFinalState)
            {
                switch (kv.Value)
                {
                    case "Dead": deadNames.Add(kv.Key); break;
                    case "Missing": missingNames.Add(kv.Key); break;
                    case "Injured": injuredNames.Add(kv.Key); break;
                }
            }

            // Precedence: a dead villager is not also missing/injured, etc.
            foreach (var name in deadNames) { missingNames.Remove(name); injuredNames.Remove(name); }
            foreach (var name in missingNames) { injuredNames.Remove(name); }

            data.Dead = deadNames.Count;
            data.Missing = missingNames.Count;
            data.Injured = injuredNames.Count;
            data.Rescued = rescuedNames.Count;
            data.RescueAttempts = rescueAttemptNames.Count;
            data.KnownVillagers = known.Count;
            data.Survivors = known.Count > 0
                ? System.Math.Max(0, known.Count - data.Dead - data.Missing)
                : -1;

            data.MonstersKilled = killedMonsters.Count;
            data.NightmareEncountersSurvived = data.MonstersFaced;

            if (data.Rescued > 0) data.HeroRescuedSomeone = true;
            if (data.BellRangCount == 0)
            {
                // Fall back to the bus-level BellRang if the hero record is absent.
                for (int i = 0; i < window.Count; i++)
                {
                    if (window[i].Type == "BellRang") data.BellRangCount++;
                }
            }

            // Hound disposition + trust/fear direction.
            if (data.HoundWentMissing || houndDisposition == "Missing")
            {
                data.HoundWentMissing = true;
                houndDisposition = "Missing";
            }
            data.HoundDisposition = houndDisposition;
            data.HoundStillChained = data.HoundPresent && houndDisposition == "Chained";
            data.HoundTrustDirection = haveFirstTrust ? Sign(lastTrust - firstTrust) : 0;
            data.HoundFearDirection = haveFirstFear ? Sign(lastFear - firstFear) : 0;

            return data;
        }

        // ------------------------------------------------------------------
        // Parsing helpers (all invariant, allocation-light)
        // ------------------------------------------------------------------

        static void RegisterVillager(HashSet<string> known, string name)
        {
            if (!string.IsNullOrEmpty(name)) known.Add(name);
        }

        static void AccumulateResource(string d, ref MorningReportData data)
        {
            // "{id} +{n} ({reason})" | "{id} -{n} ({reason})" | "{id} overflow {n} (...)"
            int space = d.IndexOf(' ');
            if (space <= 0) return;
            string id = d.Substring(0, space);
            string rest = d.Substring(space + 1);
            if (rest.Length == 0) return;

            char sign = rest[0];
            if (sign != '+' && sign != '-') return; // ignore overflow / other shapes

            int amount = ParseLeadingInt(rest.Substring(1));
            if (amount <= 0) return;

            bool refuel = d.IndexOf("refuel", System.StringComparison.Ordinal) >= 0
                          || d.IndexOf("tender", System.StringComparison.Ordinal) >= 0;

            if (sign == '-')
            {
                switch (id)
                {
                    case "food": data.FoodConsumed += amount; break;
                    case "wood":
                        data.WoodConsumed += amount;
                        if (refuel) data.WoodBurnedIntoFires += amount;
                        break;
                    case "oil": data.OilConsumed += amount; break;
                }
            }
            else
            {
                switch (id)
                {
                    case "food": data.FoodGained += amount; break;
                    case "wood": data.WoodGained += amount; break;
                }
            }
        }

        static string ParseHoundState(string d)
        {
            int arrow = d.IndexOf("->");
            if (arrow >= 0) return FirstToken(d.Substring(arrow + 2));
            int start = d.IndexOf(" start ");
            if (start >= 0) return FirstToken(d.Substring(start + 7));
            return string.Empty;
        }

        static void AddArrowTarget(HashSet<string> set, string d)
        {
            int arrow = d.IndexOf("-> ");
            if (arrow >= 0)
            {
                string t = FirstToken(d.Substring(arrow + 3));
                if (t.Length > 0) set.Add(t);
            }
        }

        static string FirstToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
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
            return ParseLeadingInt(tok);
        }

        static int ParseLeadingInt(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int i = 0;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i == 0) return 0;
            int.TryParse(s.Substring(0, i), out int v);
            return v;
        }

        static bool TryParseFloatAfter(string d, string marker, out float value)
        {
            value = 0f;
            int idx = d.IndexOf(marker, System.StringComparison.Ordinal);
            if (idx < 0) return false;
            string tok = FirstToken(d.Substring(idx + marker.Length));
            return float.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   || float.TryParse(tok, out value);
        }

        static int Sign(float delta)
        {
            const float eps = 1e-4f;
            if (delta > eps) return 1;
            if (delta < -eps) return -1;
            return 0;
        }
    }
}
