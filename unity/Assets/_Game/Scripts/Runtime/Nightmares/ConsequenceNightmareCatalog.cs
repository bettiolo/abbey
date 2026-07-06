using System.Collections.Generic;
using Abbey.Buildings;
using Abbey.Core;
using Abbey.Decrees;
using Abbey.Morale;
using UnityEngine;

namespace Abbey.Nightmares
{
    /// <summary>
    /// An immutable read of the moral state a consequence-nightmare trigger keys off:
    /// the active standing law tags, the four pressures the rules test, whether the abbey
    /// has gone Broken, whether a villager death sits in the log, and which per-death grave
    /// / rite tags the log holds. <see cref="ConsequenceNightmareCatalog.BuildContext"/>
    /// folds this from the live systems; tests craft one directly. Pure data — deterministic.
    /// </summary>
    public readonly struct ConsequenceContext
    {
        public readonly string[] ActiveTags;
        public readonly float Hunger;
        public readonly float OldFaith;
        public readonly float Sanctity;
        public readonly bool BrokenForm;
        public readonly bool AnyVillagerDeath;
        public readonly float ForestDebt;
        public readonly bool BellTowerRepaired;
        readonly HashSet<string> _logTags;

        public ConsequenceContext(string[] activeTags, float hunger, float oldFaith,
            float sanctity, bool brokenForm, bool anyVillagerDeath, HashSet<string> logTags,
            float forestDebt = 0f, bool bellTowerRepaired = false)
        {
            ActiveTags = activeTags ?? System.Array.Empty<string>();
            Hunger = hunger;
            OldFaith = oldFaith;
            Sanctity = sanctity;
            BrokenForm = brokenForm;
            AnyVillagerDeath = anyVillagerDeath;
            _logTags = logTags;
            ForestDebt = forestDebt;
            BellTowerRepaired = bellTowerRepaired;
        }

        public bool HasActiveTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }
            for (int i = 0; i < ActiveTags.Length; i++)
            {
                if (ActiveTags[i] == tag)
                {
                    return true;
                }
            }
            return false;
        }

        public bool HasLogTag(string tag)
        {
            return !string.IsNullOrEmpty(tag) && _logTags != null && _logTags.Contains(tag);
        }
    }

    /// <summary>
    /// The consequence-nightmare set: pure trigger evaluation over a
    /// <see cref="ConsequenceContext"/> using the data-driven thresholds in
    /// <see cref="ThreatConfig"/>. Each species arms under exactly one documented condition:
    ///
    ///  * Hunger Wight — Fasting law OR Hunger ≥ threshold.
    ///  * Dead Worker  — Forced night-labour law AND a logged villager death.
    ///  * Grave Crawler — a Mass Graves / Use the Dead per-death grave tag in the log.
    ///  * Chain Hound  — Chained hound law OR a Broken abbey.
    ///  * Faceless Saint — pagan rites forbidden AND (Old-faith ≥ threshold OR Sanctity ≤ threshold).
    ///  * Root Walker / Bell Mimic / Antler Wraith / Hollow Deer / Charcoal Dead —
    ///    forest-debt and restraint tags promoted into Map 1's systems-test map.
    ///
    /// Static + deterministic so EditMode tests exercise it with no scene. The
    /// <see cref="NightmareDirector"/> only evaluates when a <see cref="LawSystem"/> exists
    /// (consequences are moral-state driven), so a bare test world spawns none.
    /// </summary>
    public static class ConsequenceNightmareCatalog
    {
        /// <summary>The tags the fold scans the log for (grave tags + rite tags).</summary>
        static readonly string[] TrackedLogTags =
        {
            LawTags.GraveMass, LawTags.GraveUsed, LawTags.GraveFullRites,
            LawTags.OfferingMade, LawTags.SecretRite,
            "old_growth_cutting", "overhunting", "grove_intrusion",
            "night_burning", "forced_forest_labour", "false_bell_lure",
            "replanting", "grove_shrine", "deer_protected", "tree_burial",
            "forest_restraint"
        };

        /// <summary>
        /// Folds the current moral state from the live systems: law tags from the
        /// <see cref="LawSystem"/>, pressures from the <see cref="PressureSystem"/>, the
        /// abbey form from <see cref="AbbeyState"/>, and a single scan of the event log for
        /// the tracked grave / rite tags and any villager death.
        /// </summary>
        public static ConsequenceContext BuildContext()
        {
            var laws = LawSystem.Instance;
            string[] tags = laws != null ? laws.ActiveTags() : System.Array.Empty<string>();

            var pressures = PressureSystem.Instance;
            float hunger = pressures != null ? pressures.Hunger : 0f;
            float oldFaith = pressures != null ? pressures.OldFaith : 0f;
            float sanctity = pressures != null ? pressures.Sanctity : 0.5f;

            bool broken = AbbeyState.CurrentForm == AbbeyForm.Broken;

            var logTags = new HashSet<string>();
            bool anyDeath = false;
            var records = GameEventLog.Records;
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                if (rec.Type == "villager_died")
                {
                    anyDeath = true;
                }
                if (string.IsNullOrEmpty(rec.Data))
                {
                    continue;
                }
                TrackResourceAliases(rec.Data, logTags);
                for (int t = 0; t < TrackedLogTags.Length; t++)
                {
                    if (rec.Data.Contains(TrackedLogTags[t]))
                    {
                        logTags.Add(TrackedLogTags[t]);
                    }
                }
            }

            var threat = ThreatSourceSystem.Instance;
            float forestDebt = threat != null ? threat.PressureFor(ThreatSourceType.Forest) : 0f;

            return new ConsequenceContext(tags, hunger, oldFaith, sanctity, broken, anyDeath,
                logTags, forestDebt, AbbeyState.BellTowerRepaired);
        }

        static void TrackResourceAliases(string data, HashSet<string> logTags)
        {
            if (logTags == null || string.IsNullOrEmpty(data))
            {
                return;
            }
            if (data.Contains("old_wood +"))
            {
                logTags.Add("old_growth_cutting");
            }
            if (data.Contains("venison +"))
            {
                logTags.Add("overhunting");
            }
            if (data.Contains("charcoal +"))
            {
                logTags.Add("night_burning");
            }
        }

        /// <summary>All rules whose species is armed under the given context, in config order.</summary>
        public static List<ConsequenceTriggerRule> EvaluateArmed(ThreatConfig cfg, ConsequenceContext ctx)
        {
            var armed = new List<ConsequenceTriggerRule>();
            if (cfg == null || cfg.triggers == null)
            {
                return armed;
            }
            for (int i = 0; i < cfg.triggers.Count; i++)
            {
                var rule = cfg.triggers[i];
                if (rule != null && IsArmed(rule, ctx))
                {
                    armed.Add(rule);
                }
            }
            return armed;
        }

        /// <summary>Whether one rule's species is armed under the context (per-species logic).</summary>
        public static bool IsArmed(ConsequenceTriggerRule rule, ConsequenceContext ctx)
        {
            if (rule == null)
            {
                return false;
            }
            switch (rule.type)
            {
                case NightmareType.HungerWight:
                    return AnyActiveTag(rule, ctx)
                           || (rule.hungerAtLeast >= 0f && ctx.Hunger >= rule.hungerAtLeast);

                case NightmareType.DeadWorker:
                    // Forced night labour standing AND a death happened under it.
                    return AnyActiveTag(rule, ctx)
                           && (!rule.requireVillagerDeath || ctx.AnyVillagerDeath);

                case NightmareType.GraveCrawler:
                    return AnyLogTag(rule, ctx);

                case NightmareType.ChainHound:
                    return AnyActiveTag(rule, ctx) || (rule.armOnBrokenForm && ctx.BrokenForm);

                case NightmareType.FacelessSaint:
                    if (!AnyActiveTag(rule, ctx))
                    {
                        return false;
                    }
                    bool oldFaithHigh = rule.oldFaithAtLeast >= 0f && ctx.OldFaith >= rule.oldFaithAtLeast;
                    bool sanctityLow = rule.sanctityAtMost <= 1f && ctx.Sanctity <= rule.sanctityAtMost;
                    return (oldFaithHigh || sanctityLow) && !AnyBlockedTag(rule, ctx);

                case NightmareType.RootWalker:
                    return ForestDebtEnough(rule, ctx)
                           || (AnyLogTag(rule, ctx) && !AnyBlockedTag(rule, ctx));

                case NightmareType.BellMimic:
                    if (rule.requiresBellTowerRepaired && !ctx.BellTowerRepaired)
                    {
                        return false;
                    }
                    return (ForestDebtEnough(rule, ctx) || AnyLogTag(rule, ctx))
                           && !AnyBlockedTag(rule, ctx);

                case NightmareType.AntlerWraith:
                    return (ForestDebtEnough(rule, ctx) || AnyLogTag(rule, ctx))
                           && !AnyBlockedTag(rule, ctx);

                case NightmareType.HollowDeer:
                    return AnyLogTag(rule, ctx) && !AnyBlockedTag(rule, ctx);

                case NightmareType.CharcoalDead:
                    return (ForestDebtEnough(rule, ctx) || AnyLogTag(rule, ctx))
                           && !AnyBlockedTag(rule, ctx);

                default:
                    return false;
            }
        }

        static bool ForestDebtEnough(ConsequenceTriggerRule rule, ConsequenceContext ctx)
        {
            return rule.forestDebtAtLeast >= 0f && ctx.ForestDebt >= rule.forestDebtAtLeast;
        }

        static bool AnyActiveTag(ConsequenceTriggerRule rule, ConsequenceContext ctx)
        {
            if (rule.activeTagAny == null)
            {
                return false;
            }
            for (int i = 0; i < rule.activeTagAny.Length; i++)
            {
                if (ctx.HasActiveTag(rule.activeTagAny[i]))
                {
                    return true;
                }
            }
            return false;
        }

        static bool AnyLogTag(ConsequenceTriggerRule rule, ConsequenceContext ctx)
        {
            if (rule.logTagAny == null)
            {
                return false;
            }
            for (int i = 0; i < rule.logTagAny.Length; i++)
            {
                if (ctx.HasLogTag(rule.logTagAny[i]))
                {
                    return true;
                }
            }
            return false;
        }

        static bool AnyBlockedTag(ConsequenceTriggerRule rule, ConsequenceContext ctx)
        {
            if (rule.blockedByLogTagAny == null)
            {
                return false;
            }
            for (int i = 0; i < rule.blockedByLogTagAny.Length; i++)
            {
                if (ctx.HasLogTag(rule.blockedByLogTagAny[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
